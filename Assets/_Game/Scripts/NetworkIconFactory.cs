using UnityEngine;

namespace NetworkCollapse
{
    /// <summary>Procedural sprites for access-network gear (no external art).</summary>
    public static class NetworkIconFactory
    {
        public static Sprite Create(DeviceRole role, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = Color.clear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            Color ink = Color.white;
            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;

            switch (role)
            {
                case DeviceRole.Router:
                    DrawRouter(tex, size, ink);
                    break;
                case DeviceRole.Layer3Switch:
                    DrawSwitch(tex, size, ink, true);
                    break;
                default:
                    DrawAccess(tex, size, ink);
                    break;
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CreateHalo(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float rOuter = c - 1f;
            float rInner = rOuter - 4f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (d <= rOuter && d >= rInner)
                    tex.SetPixel(x, y, Color.white);
                else if (d < rInner)
                    tex.SetPixel(x, y, new Color(1, 1, 1, 0.15f));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static void DrawRouter(Texture2D tex, int size, Color ink)
        {
            // Hexagon body (classic router glyph)
            float c = (size - 1) * 0.5f;
            float r = size * 0.38f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (PointInHex(x - c, y - c, r))
                    tex.SetPixel(x, y, ink);
            }
            // Cut center window
            float hole = r * 0.42f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (PointInHex(x - c, y - c, hole))
                    tex.SetPixel(x, y, Color.clear);
            }
            // Antenna stubs
            FillRect(tex, (int)(c - 2), (int)(c + r * 0.75f), 4, (int)(size * 0.12f), ink);
            FillRect(tex, (int)(c - size * 0.12f), (int)(c + r * 0.55f), (int)(size * 0.24f), 3, ink);
        }

        private static void DrawSwitch(Texture2D tex, int size, Color ink, bool layer3)
        {
            int m = size / 8;
            FillRect(tex, m, m + 4, size - 2 * m, size - 2 * m - 6, ink);
            // Port teeth along bottom
            int ports = 6;
            int pw = Mathf.Max(2, (size - 2 * m) / (ports * 2));
            for (int i = 0; i < ports; i++)
            {
                int px = m + 4 + i * (pw * 2);
                FillRect(tex, px, m, pw, 5, ink);
            }
            // Status LEDs
            for (int i = 0; i < 4; i++)
                FillRect(tex, m + 6 + i * 8, size - m - 10, 4, 4, ink);
            if (layer3)
            {
                // Small "3" block / routing diamond
                int cx = size / 2, cy = size / 2;
                FillRect(tex, cx - 6, cy - 6, 12, 12, Color.clear);
                FillRect(tex, cx - 2, cy - 8, 4, 16, ink);
                FillRect(tex, cx - 8, cy - 2, 16, 4, ink);
            }
        }

        private static void DrawAccess(Texture2D tex, int size, Color ink)
        {
            // OLT / access node: rounded chassis + fiber pigtail
            int m = size / 7;
            FillCircle(tex, size / 2, size / 2 + 2, size / 3, ink);
            // Inner cut
            FillCircle(tex, size / 2, size / 2 + 2, size / 6, Color.clear);
            // Fiber drop downward
            FillRect(tex, size / 2 - 2, m / 2, 4, size / 3, ink);
            FillRect(tex, size / 2 - 8, m / 2, 16, 3, ink);
            // Side ports
            FillRect(tex, m, size / 2, 5, 8, ink);
            FillRect(tex, size - m - 5, size / 2, 5, 8, ink);
        }

        private static bool PointInHex(float x, float y, float r)
        {
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            if (ay > r * 0.8660254f) return false;
            return ax <= r - ay * 0.5f / 0.8660254f;
        }

        private static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                if (i >= 0 && j >= 0 && i < tex.width && j < tex.height)
                    tex.SetPixel(i, j, c);
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int r, Color c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) tex.SetPixel(x, y, c);
            }
        }
    }
}
