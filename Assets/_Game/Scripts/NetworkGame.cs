using UnityEngine;

namespace NetworkCollapse
{
    /// <summary>Composition root: simulation, world view, camera, HUD and campaign.</summary>
    public sealed class NetworkGame : MonoBehaviour
    {
        private NetworkSimulation simulation;
        private NetworkMapView map;
        private NetworkHud hud;
        private NetworkCameraController cameraController;
        private float elapsed;
        private bool selectingTarget;
        private bool paused;
        private NetworkCampaign campaign;
        private IspWorld world;
        private const string WorldSaveKey = "NetworkCollapse.World.v1";
        private void MarketAction(string message, bool topologyChanged = false)
        {
            hud.AddEvent(message);
            if (topologyChanged) { map.Initialize(simulation); cameraController.FrameNetwork(simulation); hud.Select(-1); }
            hud.Refresh(); SaveCampaign();
        }
        private NetworkAudio audio;
        private bool incidentPending;
        private int lastVoicedChapter = -1;
        private const string SaveKey = "NetworkCollapse.Campaign.v1";
        private const string NetworkSaveKey = "NetworkCollapse.Network.v1";
        private bool restoreOnNextReset = true;
        private bool firstRunTutorial;
        private int tutorialStep;
        private void SaveCampaign()
        {
            if (campaign == null) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(campaign.Capture()));
            if (world != null) PlayerPrefs.SetString(WorldSaveKey, JsonUtility.ToJson(world));
            if (simulation != null) PlayerPrefs.SetString(NetworkSaveKey, JsonUtility.ToJson(simulation.Capture()));
            PlayerPrefs.Save();
        }
        private void LoadCampaign()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                var saved = JsonUtility.FromJson<NetworkCampaign.SaveData>(PlayerPrefs.GetString(SaveKey));
                campaign.Restore(saved);
            }
            catch (System.Exception ex) { Debug.LogWarning("Campaign save rejected: " + ex.Message); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (FindAnyObjectByType<NetworkGame>() == null)
                new GameObject("Network Collapse - Bootstrap").AddComponent<NetworkGame>();
        }

        private void Awake()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cam = cameraObject.AddComponent<Camera>();
            }
            if (FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            cameraController = cam.GetComponent<NetworkCameraController>();
            if (cameraController == null)
                cameraController = cam.gameObject.AddComponent<NetworkCameraController>();

            audio = new GameObject("Network Audio").AddComponent<NetworkAudio>();
            map = new GameObject("Network Map").AddComponent<NetworkMapView>();
            hud = new GameObject("Network HUD").AddComponent<NetworkHud>();
            hud.DeviceAction += OnDeviceAction;
            hud.CityExpansionRequested += i => MarketAction(world.OpenCity(simulation, i), true);
            hud.ContractRequested += (city, tariff) => MarketAction(world.SignCustomers(simulation, city, tariff));
            hud.RivalAcquisitionRequested += i => MarketAction(world.AcquireRival(simulation, i), true);
            hud.PartnerConnectionRequested += i => MarketAction(world.ConnectPartner(simulation, i));
            hud.ExpandNetworkRequested += () =>
            {
                if (simulation.ExpandNetwork())
                {
                    map.Initialize(simulation);
                    cameraController.FrameNetwork(simulation);
                    hud.Select(-1);
                    hud.AddEvent("Новый район подключён. Узлов: " + simulation.Nodes.Count);
                    SaveCampaign(); Refresh();
                }
                else hud.AddEvent("Не удалось расширить сеть: проверьте бюджет или предел узлов.");
            };
            hud.AcquireCustomersRequested += () =>
            {
                if (simulation.AcquireCustomers())
                {
                    hud.AddEvent("Новая рекламная кампания: клиентов " + simulation.Customers);
                    SaveCampaign(); Refresh();
                }
                else hud.AddEvent("Кампания отклонена: недостаточно средств или мощности Access.");
            };
            hud.DiagnoseRequested += id => { hud.AddEvent("NOC / " + simulation.Diagnose(id)); audio.Play(SoundCue.Select); };
            hud.ScanNetworkRequested += () => { hud.AddEvent(simulation.ScanNetwork()); audio.Play(SoundCue.Select); };
            hud.RandomFailureRequested += RandomFailure;
            hud.ResetRequested += ResetGame;
            hud.IncidentRequested += type =>
            {
                if (simulation.GameOver) return;
                simulation.StartIncident(type);
                incidentPending = true;
                hud.AddEvent("Сценарий: " + type);
                audio.Play(SoundCue.Alert);
                Refresh();
            };
            hud.EngineeredBuildRequested += (a, b, medium, speed) =>
            {
                int cost = simulation.GetBuildCost(a, b, medium, speed);
                if (simulation.BuildLink(a, b, medium, speed))
                {
                    hud.AddEvent($"Монтаж N{a:00}–N{b:00}: {medium}, {speed} Mbps, {cost} кр.");
                    campaign.OnAction(MissionKind.BuildLink);
                    if (firstRunTutorial && tutorialStep == 2) { firstRunTutorial = false; PlayerPrefs.SetInt("NetworkCollapse.TutorialSeen", 1); hud.SetTutorial(""); }
                    audio.Play(SoundCue.LinkBuilt);
                    hud.Select(a); map.SetSelected(a); SaveCampaign(); Refresh();
                }
                else { hud.AddEvent("Монтаж отклонён: " + simulation.BuildBlockReason(a, b, medium, speed)); audio.Play(SoundCue.Error); }
            };
            hud.UpgradeLinkRequested += id =>
            {
                if (!simulation.GameOver && simulation.UpgradeLink(id))
                {
                    hud.AddEvent("Модернизирован канал #" + id);
                    campaign.OnAction(MissionKind.UpgradeLink);
                    audio.Play(SoundCue.Confirm);
                    Refresh();
                }
            };
            hud.RepairLinkRequested += id =>
            {
                if (!simulation.GameOver && simulation.RepairLink(id))
                {
                    hud.AddEvent("Ремонт канала #" + id + " начат");
                    campaign.OnAction(MissionKind.RepairDevice);
                    audio.Play(SoundCue.Repair);
                    Refresh();
                }
            };
            hud.NextLevelRequested += OnNextLevel;
            hud.PauseRequested += TogglePause;
            hud.VolumeChanged += value => audio.SetVolume(value);
            hud.MuteChanged += value => audio.SetMuted(value);
            hud.PerformanceModeChanged += low => { Application.targetFrameRate = low ? 30 : 60; QualitySettings.vSyncCount = low ? 0 : 1; };
            audio.SetVolume(PlayerPrefs.GetFloat("NC.Volume", .48f));
            audio.SetMuted(PlayerPrefs.GetInt("NC.Muted", 0) != 0);
            Application.targetFrameRate = PlayerPrefs.GetInt("NC.LowGraphics", 0) != 0 ? 30 : 60;
            hud.UpgradeDeviceRequested += id => { if (simulation.UpgradeDevice(id)) { hud.AddEvent("Оборудование модернизировано: N" + id); audio.Play(SoundCue.Confirm); Refresh(); } };
            hud.UseInventoryRequested += id =>
            {
                if (campaign.UseItem(id, simulation)) { SaveCampaign(); Refresh(); }
            };
            hud.ConfigureDeviceRequested += (id, vlan, mtu, enabled, routing) =>
            {
                if (simulation.GameOver) return;
                if (simulation.ConfigureNode(id, vlan, mtu, enabled, routing))
                {
                    hud.AddEvent("Настройки применены: N" + id.ToString("00"));
                    campaign.OnAction(MissionKind.ConfigureDevice);
                    Refresh();
                }
                else hud.AddEvent("Ошибка настройки: VLAN 1-4094, MTU 576-9216");
            };
            hud.BuildModeChanged += enabled =>
            {
                selectingTarget = false;
                hud.AddEvent(enabled ? "Строительство: выберите первый узел" : "Строительство выключено");
            };
            hud.FilterChanged += tier =>
            {
                map.SetTierFilter(tier);
                hud.AddEvent("Фильтр: " + (tier.HasValue ? tier.Value.ToString() : "Все"));
            };
            hud.RouteVisibilityChanged += enabled => map.SetRouteVisibility(enabled);
            ResetGame();
        }

        private void TogglePause()
        {
            paused = !paused;
            hud.SetPaused(paused);
        }

        private void ResetGame()
        {
            paused = false;
            bool restore = restoreOnNextReset;
            restoreOnNextReset = false;
            if (!restore) { PlayerPrefs.DeleteKey(NetworkSaveKey); PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.DeleteKey(WorldSaveKey); }
            simulation = new NetworkSimulation();
            world = IspWorld.Create();
            if (restore && PlayerPrefs.HasKey(WorldSaveKey))
            {
                try { var loaded = JsonUtility.FromJson<IspWorld>(PlayerPrefs.GetString(WorldSaveKey)); if (loaded != null && loaded.Cities != null && loaded.Tariffs != null) world = loaded; }
                catch (System.Exception ex) { Debug.LogWarning("World save rejected: " + ex.Message); }
            }
            firstRunTutorial = !PlayerPrefs.HasKey("NetworkCollapse.TutorialSeen");
            tutorialStep = 0;
            if (restore && PlayerPrefs.HasKey(NetworkSaveKey))
            {
                try { simulation.Restore(JsonUtility.FromJson<NetworkSimulation.Snapshot>(PlayerPrefs.GetString(NetworkSaveKey))); }
                catch (System.Exception ex) { Debug.LogWarning("Network save rejected: " + ex.Message); }
            }
            campaign = new NetworkCampaign();
            campaign.Notification += message => { hud.AddEvent(message); audio.Play(message.StartsWith("Достижение:") ? SoundCue.Achievement : SoundCue.Mission); };
            campaign.CreditsAwarded += amount => simulation.GrantCredits(amount);
            campaign.StateChanged += SaveCampaign;
            if (restore) LoadCampaign();
            incidentPending = false;
            selectingTarget = false;
            elapsed = 0f;
            map.Initialize(simulation);
            hud.Initialize(simulation);
            hud.SetCampaign(campaign);
            hud.SetWorld(world);
            hud.Select(-1);
            hud.SetPaused(false);
            hud.SetTutorial(firstRunTutorial ? "ОБУЧЕНИЕ 1/3: Выберите любой узел сети на карте." : "");
            if (firstRunTutorial) hud.ShowBriefing();
            hud.AddEvent("Диспетчерская запущена · " + campaign.ChapterTitle);
            hud.AddEvent(campaign.StoryBriefing);
            lastVoicedChapter = campaign.Chapter;
            audio.PlayVoice("mission_" + (campaign.Chapter + 1));
            cameraController.FrameNetwork(simulation);
        }

        private void OnNextLevel()
        {
            if (!simulation.ObjectiveComplete) return;
            simulation.AdvanceToNextLevel();
            selectingTarget = false;
            elapsed = 0f;
            map.Initialize(simulation);
            hud.Initialize(simulation);
            hud.SetCampaign(campaign);
            hud.Select(-1);
            hud.AddEvent("Уровень " + simulation.Level + " · " + campaign.ChapterTitle);
            cameraController.ResetView();
        }

        private void RandomFailure()
        {
            if (simulation.GameOver) return;
            simulation.RandomFailure();
            incidentPending = true;
            hud.AddEvent("Авария: " + simulation.LastScenario);
            Refresh();
        }

        private void OnDeviceAction(int id)
        {
            if (!simulation.GameOver && simulation.ToggleFailure(id))
            {
                hud.AddEvent("Действие с узлом N" + id.ToString("00"));
                if (simulation.Nodes[id].RepairRemaining > 0f)
                    campaign.OnAction(MissionKind.RepairDevice);
                Refresh();
            }
        }

        private void Refresh()
        {
            map.HighlightRoute(hud.SelectedId);
            hud.Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Alpha1)) hud.SelectScreen(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) { hud.SelectScreen(1); if (firstRunTutorial && tutorialStep == 1) { tutorialStep = 2; hud.SetTutorial("ОБУЧЕНИЕ 3/3: Нажмите 1, включите строительство и соедините два узла."); } }
            if (Input.GetKeyDown(KeyCode.Alpha3)) hud.SelectScreen(2);
            if (!paused) elapsed += Time.deltaTime;
            while (!paused && elapsed >= 2f)
            {
                elapsed -= 2f;
                simulation.Step(2f);
                campaign.OnTick(simulation);
                world.Tick(simulation);
                if (campaign.Chapter != lastVoicedChapter)
                {
                    lastVoicedChapter = campaign.Chapter;
                    if (!audio.PlayVoice("mission_" + (campaign.Chapter + 1))) audio.Play(SoundCue.Mission);
                }
                if (incidentPending && !simulation.GameOver && simulation.Availability >= 0.60f)
                {
                    campaign.OnAction(MissionKind.SurviveIncident);
                    incidentPending = false;
                }
                Refresh();
            }

            bool overPanel = hud.IsPointerOverHud(Input.mousePosition);
            cameraController.PointerOverHud = overPanel;

            if (Input.GetKeyDown(KeyCode.Space)) RandomFailure();
            if (Input.GetKeyDown(KeyCode.R)) ResetGame();
            if (Input.GetKeyDown(KeyCode.B) && hud.SelectedId >= 0) selectingTarget = true;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                selectingTarget = false;
                if (hud.BuildMode) hud.SetBuildMode(false);
                if (hud.TargetId >= 0 && hud.SelectedId >= 0) { hud.Select(hud.SelectedId); map.SetSelected(hud.SelectedId); }
            }

            if ((hud.BuildMode || selectingTarget) && hud.SelectedId >= 0)
            {
                Vector3 pointer = cameraController.ScreenWorld(Input.mousePosition);
                int hover = overPanel ? -1 : map.HitTest(pointer);
                map.SetBuildPreview(hud.SelectedId, pointer, hover >= 0 ? hover : hud.TargetId, hud.SelectedMedium, hud.SelectedSpeed);
            }
            else map.SetBuildPreview(-1, Vector3.zero);

            if (Input.GetMouseButtonDown(0) && !overPanel)
            {
                Vector3 world = cameraController.ScreenWorld(Input.mousePosition);
                int id = map.HitTest(world);

                if ((selectingTarget || hud.BuildMode) && id >= 0 && hud.SelectedId >= 0 && id != hud.SelectedId)
                {
                    hud.SelectTarget(id);
                    map.SetBuildTarget(id);
                    selectingTarget = false;
                }
                else if (!selectingTarget)
                {
                    if (id >= 0)
                    {
                        map.SetSelected(id);
                        hud.Select(id);
                        if (firstRunTutorial && tutorialStep == 0) { tutorialStep = 1; hud.SetTutorial("ОБУЧЕНИЕ 2/3: Нажмите 2 для просмотра заданий."); }
                        audio.Play(SoundCue.Select);
                        map.HighlightRoute(id);
                    }
                    else
                    {
                        int linkId = map.HitTestLink(world);
                        if (linkId >= 0)
                        {
                            map.SetSelectedLink(linkId);
                            hud.SelectLink(linkId);
                            audio.Play(SoundCue.Select);
                            map.HighlightRoute(-1);
                        }
                    }
                }
            }
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveCampaign(); }

        private void OnApplicationQuit() { SaveCampaign(); }

        private void OnDestroy()
        {
            SaveCampaign();
            if (audio != null) Destroy(audio.gameObject);
            if (hud != null) Destroy(hud.gameObject);
            if (map != null) Destroy(map.gameObject);
        }
    }
}
