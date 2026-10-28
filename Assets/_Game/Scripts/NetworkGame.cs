using UnityEngine;

namespace NetworkCollapse
{
    public sealed class NetworkGame : MonoBehaviour
    {
        private NetworkSimulation simulation;
        private Camera gameCamera;
        private float elapsed;
        private Texture2D pixel;
        private GUIStyle header;
        private GUIStyle label;
        private GUIStyle button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (FindAnyObjectByType<NetworkGame>() == null)
                new GameObject("Network Collapse — Bootstrap").AddComponent<NetworkGame>();
        }

        private void Awake()
        {
            simulation = new NetworkSimulation();
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var go = new GameObject("Main Camera");
                gameCamera = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 9f;
            gameCamera.transform.position = new Vector3(0, 0, -20);
            gameCamera.backgroundColor = new Color(0.035f, 0.055f, 0.095f);
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white); 
            pixel.Apply();
            header = new GUIStyle { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            label = new GUIStyle { fontSize = 16, normal = { textColor = new Color(0.75f, 0.88f, 0.97f) } };
            // GUI.skin is available only during OnGUI.
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= 2f) { elapsed = 0; simulation.Step(); }
            if (Input.GetKeyDown(KeyCode.Space)) simulation.RandomFailure();
            if (Input.GetMouseButtonDown(0) && !OverHud(Input.mousePosition))
            {
                Vector3 world = gameCamera.ScreenToWorldPoint(Input.mousePosition);
                foreach (var node in simulation.Nodes)
                    if (Vector2.Distance(node.Position, world) < 0.42f) { simulation.ToggleFailure(node.Id); break; }
            }
        }

        private bool OverHud(Vector3 mouse) => mouse.x < 300 && Screen.height - mouse.y < 215;

        private void OnGUI()
        {
            if (button == null) button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
            // World-space map drawn through GUI keeps the MVP independent of sprites/material assets.
            foreach (var link in simulation.Links)
            {
                var a = Project(simulation.Nodes[link.A].Position);
                var b = Project(simulation.Nodes[link.B].Position);
                var color = link.Failed ? Color.gray : Color.Lerp(new Color(0.08f, 0.75f, 0.85f), new Color(1f, 0.45f, 0.22f), Mathf.Clamp01(link.Load / link.Capacity));
                DrawLine(a, b, color, 3);
            }
            foreach (var node in simulation.Nodes)
            {
                Vector2 pos = Project(node.Position);
                Color c = node.Failed ? new Color(0.95f, 0.24f, 0.27f) : node.Id == 0 ? new Color(0.98f, 0.8f, 0.28f) : new Color(0.24f, 0.9f, 0.71f);
                DrawRect(new Rect(pos.x - 10, pos.y - 10, 20, 20), c);
                GUI.Label(new Rect(pos.x + 13, pos.y - 13, 60, 22), "N" + node.Id, label);
            }
            DrawRect(new Rect(12, 12, 286, 202), new Color(0.075f, 0.11f, 0.18f, .95f));
            GUI.Label(new Rect(25, 24, 270, 34), "NETWORK COLLAPSE", header);
            GUI.Label(new Rect(25, 65, 270, 24), $"Доступность: {simulation.Availability:P0}", label);
            GUI.Label(new Rect(25, 92, 270, 24), $"Бюджет: {simulation.Money} | Тик: {simulation.TickCount}", label);
            GUI.Label(new Rect(25, 119, 270, 24), "Красный узел: авария", label);
            if (GUI.Button(new Rect(25, 155, 254, 43), "СЛУЧАЙНАЯ АВАРИЯ [SPACE]", button)) simulation.RandomFailure();
            GUI.Label(new Rect(Screen.width - 345, Screen.height - 32, 340, 25), "Клик: вывести узел из строя / починить (15)", label);
        }

        private Vector2 Project(Vector2 world)
        {
            Vector3 screen = gameCamera.WorldToScreenPoint(new Vector3(world.x, world.y, 0));
            return new Vector2(screen.x, Screen.height - screen.y);
        }
        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = previous;
        }
        private void DrawLine(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 diff = b - a;
            if (diff.sqrMagnitude < 0.01f) return;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg, a);
            DrawRect(new Rect(a.x, a.y - width / 2f, diff.magnitude, width), color);
            GUI.matrix = old;
        }
        private void OnDestroy() { if (pixel != null) Destroy(pixel); }
    }
}
