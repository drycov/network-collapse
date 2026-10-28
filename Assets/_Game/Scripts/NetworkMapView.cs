using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public sealed class NetworkMapView : MonoBehaviour
    {
        private sealed class NodeView
        {
            public Transform Root;
            public SpriteRenderer Sprite;
            public SpriteRenderer Halo;
            public SpriteRenderer RepairRing;
        }

        private readonly List<NodeView> nodeViews = new List<NodeView>();
        private readonly List<LineRenderer> linkViews = new List<LineRenderer>();
        private readonly List<Transform> particles = new List<Transform>();
        private readonly List<int> particleLinks = new List<int>();

        private NetworkSimulation simulation;
        private Sprite sprite;
        private Material lineMaterial;
        private int selected = -1;
        private int secondSelected = -1;
        private int selectedLink = -1;
        private float clock;
        private EquipmentTier? visibleTier;
        public void SetTierFilter(EquipmentTier? tier) { visibleTier = tier; Refresh(); }

        public int SelectedNode => selected;
        public int SelectedLink => selectedLink;

        public void Initialize(NetworkSimulation model)
        {
            ClearMap();
            simulation = model;

            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    texture.SetPixel(x, y, (new Vector2(x - 15.5f, y - 15.5f).sqrMagnitude < 210f) ? Color.white : Color.clear);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 32);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            lineMaterial = new Material(shader);

            for (int i = 0; i < model.Links.Count; i++)
                AddLinkVisual(i);

            foreach (var node in model.Nodes)
            {
                var root = new GameObject("Device N" + node.Id).transform;
                root.SetParent(transform, false);
                root.position = node.Position;

                var halo = new GameObject("Selection").AddComponent<SpriteRenderer>();
                halo.transform.SetParent(root, false);
                halo.sprite = sprite;
                halo.transform.localScale = Vector3.one * .92f;
                halo.color = new Color(.38f, .8f, 1f, .3f);
                halo.sortingOrder = 2;

                var repair = new GameObject("Repair").AddComponent<SpriteRenderer>();
                repair.transform.SetParent(root, false);
                repair.sprite = sprite;
                repair.transform.localScale = Vector3.one * .78f;
                repair.color = new Color(1f, .82f, .18f, .4f);
                repair.sortingOrder = 1;
                repair.enabled = false;

                var renderer = new GameObject("Device").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root, false);
                renderer.sprite = sprite;
                renderer.transform.localScale = Vector3.one * .53f;
                renderer.sortingOrder = 3;

                nodeViews.Add(new NodeView { Root = root, Sprite = renderer, Halo = halo, RepairRing = repair });
            }

            selected = -1;
            secondSelected = -1;
            selectedLink = -1;
            Refresh();
        }

        public void SetSelected(int index)
        {
            selected = index;
            secondSelected = -1;
            selectedLink = -1;
            Refresh();
        }

        public void SetBuildTarget(int index)
        {
            secondSelected = index;
            selectedLink = -1;
            Refresh();
        }

        public void SetSelectedLink(int index)
        {
            selectedLink = index;
            selected = -1;
            secondSelected = -1;
            Refresh();
        }

        public int HitTest(Vector3 world, float threshold = .45f)
        {
            if (simulation == null) return -1;
            for (int i = 0; i < simulation.Nodes.Count; i++)
                if ((!visibleTier.HasValue || simulation.Nodes[i].Tier == visibleTier.Value) && Vector2.Distance(simulation.Nodes[i].Position, new Vector2(world.x, world.y)) < threshold)
                    return i;
            return -1;
        }

        public int HitTestLink(Vector3 world, float threshold = .2f)
        {
            if (simulation == null) return -1;
            float best = threshold;
            int bestIdx = -1;
            for (int i = 0; i < simulation.Links.Count; i++)
            {
                var link = simulation.Links[i];
                Vector2 a = simulation.Nodes[link.A].Position;
                Vector2 b = simulation.Nodes[link.B].Position;
                float d = DistanceToSegment(new Vector2(world.x, world.y), a, b);
                if (d < best)
                {
                    best = d;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + ab * t);
        }

        public void Refresh()
        {
            if (simulation == null) return;

            for (int i = 0; i < simulation.Nodes.Count; i++)
            {
                var node = simulation.Nodes[i];
                Color c;
                if (node.Failed)
                    c = node.RepairRemaining > 0f ? new Color(1f, .7f, .15f) : new Color(1f, .24f, .32f);
                else if (node.Status == NodeStatus.Overloaded)
                    c = new Color(1f, .57f, .2f);
                else if (node.Tier == EquipmentTier.Core)
                    c = new Color(1f, .79f, .3f);
                else if (node.Tier == EquipmentTier.Distribution)
                    c = new Color(.3f, .61f, 1f);
                else
                    c = new Color(.21f, .88f, .73f);

                bool visible = !visibleTier.HasValue || node.Tier == visibleTier.Value;
                nodeViews[i].Sprite.color = c;
                nodeViews[i].Root.gameObject.SetActive(visible);
                nodeViews[i].Halo.enabled = i == selected || i == secondSelected;
                nodeViews[i].Halo.color = i == secondSelected
                    ? new Color(1f, .75f, .19f, .5f)
                    : new Color(.38f, .8f, 1f, .3f);
                nodeViews[i].Root.localScale = node.Tier == EquipmentTier.Core ? Vector3.one * 1.55f
                    : node.Tier == EquipmentTier.Distribution ? Vector3.one * 1.22f
                    : Vector3.one;

                bool repairing = node.Failed && node.RepairRemaining > 0f;
                nodeViews[i].RepairRing.enabled = repairing;
            }

            while (linkViews.Count < simulation.Links.Count)
                AddLinkVisual(linkViews.Count);

            for (int i = 0; i < simulation.Links.Count; i++)
            {
                var link = simulation.Links[i];
                bool down = link.Failed || simulation.Nodes[link.A].Failed || simulation.Nodes[link.B].Failed;
                Color col = down
                    ? new Color(.36f, .39f, .48f)
                    : Color.Lerp(new Color(.12f, .62f, .77f), new Color(1f, .48f, .22f),
                        Mathf.Clamp01(link.Capacity > 0 ? link.Load / link.Capacity : 1));

                if (i == selectedLink)
                    col = Color.Lerp(col, Color.white, 0.5f);

                linkViews[i].startColor = linkViews[i].endColor = col;
                float w = down ? .04f : Mathf.Lerp(.05f, .095f, Mathf.Clamp01(link.Load / Mathf.Max(1f, link.Capacity)));
                if (link.PlayerBuilt) w += .015f;
                linkViews[i].startWidth = linkViews[i].endWidth = w;
                linkViews[i].SetPosition(0, simulation.Nodes[link.A].Position);
                linkViews[i].SetPosition(1, simulation.Nodes[link.B].Position);
            }
        }

        private void AddLinkVisual(int index)
        {
            var link = simulation.Links[index];
            var obj = new GameObject(link.PlayerBuilt ? "Built Link " + index : "Link " + link.A + "-" + link.B);
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, simulation.Nodes[link.A].Position);
            line.SetPosition(1, simulation.Nodes[link.B].Position);
            line.material = lineMaterial;
            line.sortingOrder = 0;
            line.startWidth = line.endWidth = .055f;
            linkViews.Add(line);

            for (int p = 0; p < 2; p++)
            {
                var packet = new GameObject("Traffic packet").transform;
                packet.SetParent(transform, false);
                var sr = packet.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(.4f, .96f, 1f);
                sr.sortingOrder = 2;
                packet.localScale = Vector3.one * .11f;
                particles.Add(packet);
                particleLinks.Add(index);
            }
        }

        private void Update()
        {
            if (simulation == null) return;
            clock += Time.deltaTime;

            for (int i = 0; i < nodeViews.Count; i++)
            {
                if (nodeViews[i].RepairRing != null && nodeViews[i].RepairRing.enabled)
                {
                    float pulse = 0.72f + 0.14f * Mathf.Sin(clock * 5.5f);
                    nodeViews[i].RepairRing.transform.localScale = Vector3.one * pulse;
                }
            }

            for (int i = 0; i < particles.Count; i++)
            {
                int linkIndex = particleLinks[i];
                if (linkIndex >= simulation.Links.Count)
                {
                    particles[i].gameObject.SetActive(false);
                    continue;
                }

                var link = simulation.Links[linkIndex];
                bool active = !link.Failed &&
                              !simulation.Nodes[link.A].Failed &&
                              !simulation.Nodes[link.B].Failed &&
                              link.Load > 0.4f;

                particles[i].gameObject.SetActive(active);
                if (!active) continue;

                // Direction from FlowAB
                bool forward = link.FlowAB >= 0f;
                float speed = .16f + .2f * Mathf.Clamp01(link.Load / Mathf.Max(1f, link.Capacity));
                float phase = (i % 2) * .5f;
                float t = Mathf.Repeat(clock * speed + phase, 1f);

                Vector3 from = simulation.Nodes[link.A].Position;
                Vector3 to = simulation.Nodes[link.B].Position;
                if (!forward) (from, to) = (to, from);

                particles[i].position = Vector3.Lerp(from, to, t);
            }
        }

        private void ClearMap()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            nodeViews.Clear();
            linkViews.Clear();
            particles.Clear();
            particleLinks.Clear();
            if (lineMaterial != null) Destroy(lineMaterial);
            if (sprite != null)
            {
                var texture = sprite.texture;
                Destroy(sprite);
                Destroy(texture);
            }
        }

        private void OnDestroy() { ClearMap(); }
    }
}
