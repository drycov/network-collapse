using UnityEngine;

namespace NetworkCollapse
{
    public sealed class NetworkCameraController : MonoBehaviour
    {
        private Camera cam;
        private Vector3 dragOrigin;
        private bool dragging;
        public bool PointerOverHud { get; set; }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = gameObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.transform.position = new Vector3(0, 0, -20);
            cam.backgroundColor = new Color(.025f, .039f, .071f);
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                dragging = true;
                dragOrigin = ScreenWorld(Input.mousePosition);
            }
            if (!Input.GetMouseButton(1) && !Input.GetMouseButton(2)) dragging = false;
            if (dragging)
            {
                Vector3 delta = dragOrigin - ScreenWorld(Input.mousePosition);
                transform.position += new Vector3(delta.x, delta.y, 0);
            }
            if (!PointerOverHud)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > .01f)
                {
                    Vector3 before = ScreenWorld(Input.mousePosition);
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Pow(.88f, wheel), 3.5f, 18f);
                    Vector3 after = ScreenWorld(Input.mousePosition);
                    transform.position += before - after;
                }
            }
            if (Input.GetKeyDown(KeyCode.F)) ResetView();
        }

        public Vector3 ScreenWorld(Vector3 screen)
        {
            if (cam == null) Awake();
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector3(world.x, world.y, 0);
        }

        public void ResetView()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            transform.position = new Vector3(0, 0, -20);
            cam.orthographicSize = 9f;
        }
    }
}
