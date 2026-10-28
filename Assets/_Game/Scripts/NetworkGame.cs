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
            ResetGame();
        }

        private void ResetGame()
        {
            simulation = new NetworkSimulation();
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
                simulation.Step();
                Refresh();
            }
            bool overPanel = hud.IsPointerOverHud(Input.mousePosition);
            cameraController.PointerOverHud = overPanel;
            if (Input.GetKeyDown(KeyCode.Space)) RandomFailure();
            if (Input.GetKeyDown(KeyCode.R)) ResetGame();
            if (Input.GetMouseButtonDown(0) && !overPanel)
            {
                int id = map.HitTest(cameraController.ScreenWorld(Input.mousePosition));
                map.SetSelected(id);
                hud.Select(id);
            }
        }

        private void OnDestroy()
        {
            if (hud != null) Destroy(hud.gameObject);
            if (map != null) Destroy(map.gameObject);
        }
    }
}
