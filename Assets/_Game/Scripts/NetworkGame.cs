using UnityEngine;

namespace NetworkCollapse
{
    /// <summary>Composition root: simulation, world view, camera and HUD.</summary>
    public sealed class NetworkGame : MonoBehaviour
    {
        private NetworkSimulation simulation;
        private NetworkMapView map;
        private NetworkHud hud;
        private NetworkCameraController cameraController;
        private float elapsed;
        private bool selectingTarget;
        private NetworkCampaign campaign;
        private bool incidentPending;

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

            map = new GameObject("Network Map").AddComponent<NetworkMapView>();
            hud = new GameObject("Network HUD").AddComponent<NetworkHud>();
            hud.DeviceAction += OnDeviceAction;
            hud.RandomFailureRequested += RandomFailure;
            hud.ResetRequested += ResetGame;
            hud.IncidentRequested += type => { if (simulation.GameOver) return; simulation.StartIncident(type); incidentPending = true; hud.AddEvent("Сценарий: " + type); Refresh(); };
            hud.BuildLinkRequested += (a, b) => { if (!simulation.GameOver && simulation.BuildLink(a, b)) { hud.AddEvent($"Канал N{a:00}–N{b:00} построен"); campaign.OnAction(MissionKind.BuildLink); map.SetBuildPreview(-1, Vector3.zero); hud.Select(a); map.SetSelected(a); Refresh(); } };
            hud.UpgradeLinkRequested += id => { if (!simulation.GameOver && simulation.UpgradeLink(id)) { hud.AddEvent("Модернизирован канал #" + id); campaign.OnAction(MissionKind.UpgradeLink); Refresh(); } };
            hud.RepairLinkRequested += id => { if (!simulation.GameOver && simulation.RepairLink(id)) { hud.AddEvent("Ремонт канала #" + id + " начат"); Refresh(); } };
            hud.NextLevelRequested += OnNextLevel;
            hud.UseInventoryRequested += id => { if (campaign.UseItem(id, simulation)) Refresh(); };
            hud.ConfigureDeviceRequested += (id, vlan, mtu, enabled, routing) =>
            {
                if (simulation.GameOver) return;
                if (simulation.ConfigureNode(id, vlan, mtu, enabled, routing))
                {
                    hud.AddEvent("Настройки применены: N" + id.ToString("00")); campaign.OnAction(MissionKind.ConfigureDevice);
                    Refresh();
                }
                else hud.AddEvent("Ошибка настройки: VLAN 1-4094, MTU 576-9216");
            };
            hud.BuildModeChanged += enabled => { selectingTarget = false; hud.AddEvent(enabled ? "Строительство: выберите первый узел" : "Строительство выключено"); };
            hud.FilterChanged += tier => { map.SetTierFilter(tier); hud.AddEvent("Фильтр: " + (tier.HasValue ? tier.Value.ToString() : "Все")); };
            hud.RouteVisibilityChanged += enabled => map.SetRouteVisibility(enabled);
            ResetGame();
        }

        private void ResetGame()
        {
            simulation = new NetworkSimulation();
            campaign = new NetworkCampaign();
            campaign.Notification += message => hud.AddEvent(message);
            campaign.CreditsAwarded += amount => simulation.GrantCredits(amount);
            incidentPending = false;
            selectingTarget = false;
            elapsed = 0f;
            map.Initialize(simulation);
            hud.Initialize(simulation);
            hud.SetCampaign(campaign);
            hud.Select(-1);
            hud.AddEvent("Диспетчерская запущена");
            cameraController.ResetView();
        }

        private void OnNextLevel()
        {
            if (!simulation.ObjectiveComplete) return;
            int next = simulation.Level + 1;
            // Keep score/money continuity via AdvanceToNextLevel
            simulation.AdvanceToNextLevel();
            selectingTarget = false;
            elapsed = 0f;
            map.Initialize(simulation);
            hud.Initialize(simulation);
            hud.Select(-1);
            hud.AddEvent("Уровень " + simulation.Level + " запущен");
            cameraController.ResetView();
        }

        private void RandomFailure()
        {
            if (simulation.GameOver) return;
            simulation.RandomFailure();
            hud.AddEvent("Авария: " + simulation.LastScenario);
            Refresh();
        }

        private void OnDeviceAction(int id)
        {
            if (!simulation.GameOver && simulation.ToggleFailure(id))
            {
                hud.AddEvent("Действие с узлом N" + id.ToString("00"));
                if (simulation.Nodes[id].RepairRemaining > 0f) campaign.OnAction(MissionKind.RepairDevice);
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
                if (incidentPending && !simulation.GameOver && simulation.Availability >= .60f)
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
            if (Input.GetKeyDown(KeyCode.Escape)) { selectingTarget = false; if (hud.BuildMode) hud.SetBuildMode(false); }

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
                        map.HighlightRoute(id);
                    }
                    else
                    {
                        int linkId = map.HitTestLink(world);
                        if (linkId >= 0)
                        {
                            map.SetSelectedLink(linkId);
                            hud.SelectLink(linkId);
                            map.HighlightRoute(-1);
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (hud != null) Destroy(hud.gameObject);
            if (map != null) Destroy(map.gameObject);
        }
    }
}
