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
        private NetworkCampaign campaign;
        private NetworkAudio audio;
        private bool incidentPending;
        private int lastVoicedChapter = -1;
        private const string SaveKey = "NetworkCollapse.Campaign.v1";
        private void SaveCampaign()
        {
            if (campaign == null) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(campaign.Capture()));
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
            cameraController = cam.GetComponent<NetworkCameraController>();
            if (cameraController == null)
                cameraController = cam.gameObject.AddComponent<NetworkCameraController>();

            audio = new GameObject("Network Audio").AddComponent<NetworkAudio>();
            map = new GameObject("Network Map").AddComponent<NetworkMapView>();
            hud = new GameObject("Network HUD").AddComponent<NetworkHud>();
            hud.DeviceAction += OnDeviceAction;
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
            hud.BuildLinkRequested += (a, b) =>
            {
                if (!simulation.GameOver && simulation.BuildLink(a, b))
                {
                    hud.AddEvent($"Канал N{a:00}–N{b:00} построен");
                    campaign.OnAction(MissionKind.BuildLink);
                    audio.Play(SoundCue.LinkBuilt);
                    map.SetBuildPreview(-1, Vector3.zero);
                    hud.Select(a);
                    map.SetSelected(a);
                    Refresh();
                }
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

        private void ResetGame()
        {
            simulation = new NetworkSimulation();
            campaign = new NetworkCampaign();
            campaign.Notification += message => { hud.AddEvent(message); audio.Play(message.StartsWith("Достижение:") ? SoundCue.Achievement : SoundCue.Mission); };
            campaign.CreditsAwarded += amount => simulation.GrantCredits(amount);
            campaign.StateChanged += SaveCampaign;
            LoadCampaign();
            incidentPending = false;
            selectingTarget = false;
            elapsed = 0f;
            map.Initialize(simulation);
            hud.Initialize(simulation);
            hud.SetCampaign(campaign);
            hud.Select(-1);
            hud.AddEvent("Диспетчерская запущена · " + campaign.ChapterTitle);
            hud.AddEvent(campaign.StoryBriefing);
            lastVoicedChapter = campaign.Chapter;
            audio.PlayVoice("mission_" + (campaign.Chapter + 1));
            cameraController.ResetView();
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
            elapsed += Time.deltaTime;
            while (elapsed >= 2f)
            {
                elapsed -= 2f;
                simulation.Step(2f);
                campaign.OnTick(simulation);
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
            }

            if ((hud.BuildMode || selectingTarget) && hud.SelectedId >= 0)
            {
                Vector3 pointer = cameraController.ScreenWorld(Input.mousePosition);
                int hover = overPanel ? -1 : map.HitTest(pointer);
                map.SetBuildPreview(hud.SelectedId, pointer, hover >= 0 ? hover : hud.TargetId);
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
