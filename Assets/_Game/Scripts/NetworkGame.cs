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
            hud.IncidentRequested += type => { simulation.StartIncident(type); Refresh(); };
            hud.BuildLinkRequested += (a, b) => { if (simulation.BuildLink(a, b)) Refresh(); };
            hud.UpgradeLinkRequested += id => { if (simulation.UpgradeLink(id)) Refresh(); };
            hud.RepairLinkRequested += id => { if (simulation.RepairLink(id)) Refresh(); };
            ResetGame();
        }

        private void ResetGame()
        {
            simulation = new NetworkSimulation();
            selectingTarget = false;
            elapsed = 0f;
            map.Initialize(simulation);
            hud.Initialize(simulation);
            hud.Select(-1);
            cameraController.ResetView();
        }

        private void RandomFailure()
        {
            simulation.RandomFailure();
            Refresh();
        }

        private void OnDeviceAction(int id)
        {
            if (simulation.ToggleFailure(id))
                Refresh();
        }

        private void Refresh()
        {
            map.Refresh();
            hud.Refresh();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            while (elapsed >= 2f)
            {
                elapsed -= 2f;
                simulation.Step(2f);
                Refresh();
            }
            bool overPanel = hud.IsPointerOverHud(Input.mousePosition);
            cameraController.PointerOverHud = overPanel;
            if (Input.GetKeyDown(KeyCode.Space)) RandomFailure();
            if (Input.GetKeyDown(KeyCode.R)) ResetGame();
            if (Input.GetKeyDown(KeyCode.B) && hud.SelectedId >= 0) selectingTarget = true;
            if (Input.GetKeyDown(KeyCode.Escape)) selectingTarget = false;
            if (Input.GetMouseButtonDown(0) && !overPanel)
            {
                int id = map.HitTest(cameraController.ScreenWorld(Input.mousePosition));
                if (selectingTarget && id >= 0 && id != hud.SelectedId)
                {
                    hud.SelectTarget(id);
                    map.SetBuildTarget(id);
                    selectingTarget = false;
                }
                else if (!selectingTarget)
                {
                    map.SetSelected(id);
                    hud.Select(id);
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
