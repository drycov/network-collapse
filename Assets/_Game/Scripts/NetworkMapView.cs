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
            public DeviceRole Role;
        }

        private readonly List<NodeView> nodeViews = new List<NodeView>();
        private readonly List<LineRenderer> linkViews = new List<LineRenderer>();
        private readonly List<Transform> particles = new List<Transform>();
        private readonly List<int> particleLinks = new List<int>();

        private NetworkSimulation simulation;
        private Sprite packetSprite;
        private Sprite haloSprite;
        private Sprite routerSprite;
        private Sprite switchSprite;
        private Sprite accessSprite;
        private Material lineMaterial;
        private int selected = -1;
        private int secondSelected = -1;
        private int selectedLink = -1;
        private float clock;
        private readonly List<LineRenderer> backgroundGrid = new List<LineRenderer>();
        private EquipmentTier? visibleTier;
        private readonly HashSet<int> routeLinks = new HashSet<int>();
        private readonly HashSet<int> backupRouteLinks = new HashSet<int>();
        private int routeSource = -1;
        public bool ShowRoutes { get; private set; } = true;
        private LineRenderer previewLine;

        public void SetRouteVisibility(bool enabled) { ShowRoutes = enabled; Refresh(); }

        public void HighlightRoute(int source)
        {
            routeSource = source;
            routeLinks.Clear();
            backupRouteLinks.Clear();
            if (simulation != null)
            {
                List<int> path = null;
                if (!simulation.PrimaryPaths.TryGetValue(source, out path))
                    path = simulation.FindDisplayRoute(source);
                if (path != null) foreach (int link in path) routeLinks.Add(link);
                if (simulation.BackupPaths.TryGetValue(source, out var backup))
                    foreach (int link in backup) backupRouteLinks.Add(link);
            }
            Refresh();
        }

        public void SetBuildPreview(int source, Vector3 world, int target = -1)
        {
            if (previewLine == null && simulation != null)
            {
                var obj = new GameObject("Construction Preview");
                obj.transform.SetParent(transform, false);
                previewLine = obj.AddComponent<LineRenderer>();
                previewLine.material = lineMaterial;
                previewLine.positionCount = 2;
                previewLine.sortingOrder = 5;
                previewLine.startWidth = previewLine.endWidth = .11f;
            }
            bool valid = simulation != null && source >= 0 && source < simulation.Nodes.Count;
            if (previewLine == null) return;
            previewLine.enabled = valid;
            if (!valid) return;
            Vector3 end = target >= 0 && target < simulation.Nodes.Count
                ? (Vector3)simulation.Nodes[target].Position : new Vector3(world.x, world.y, 0);
            previewLine.SetPosition(0, simulation.Nodes[source].Position);
            previewLine.SetPosition(1, end);
            bool allowed = target < 0 ? !simulation.GameOver : simulation.BuildBlockReason(source, target) == null;
            Color color = allowed ? new Color(.3f, 1f, .67f, .85f) : new Color(1f, .32f, .27f, .85f);
            previewLine.startColor = previewLine.endColor = color;
        }

        public void SetTierFilter(EquipmentTier? tier) { visibleTier = tier; Refresh(); }

        public int SelectedNode => selected;
        public int SelectedLink => selectedLink;

        public void Initialize(NetworkSimulation model)
        {
            ClearMap();
            simulation = model;

            routerSprite = NetworkIconFactory.Create(DeviceRole.Router);
            switchSprite = NetworkIconFactory.Create(DeviceRole.Layer3Switch);
            accessSprite = NetworkIconFactory.Create(DeviceRole.Layer2Switch);
            haloSprite = NetworkIconFactory.CreateHalo();
            packetSprite = accessSprite;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            lineMaterial = new Material(shader);
            BuildTerminalGrid();

            for (int i = 0; i < model.Links.Count; i++)
                AddLinkVisual(i);

            foreach (var node in model.Nodes)
            {
                var root = new GameObject(node.Hostname ?? ("Device N" + node.Id)).transform;
                root.SetParent(transform, false);
                root.position = node.Position;

                var halo = new GameObject("Selection").AddComponent<SpriteRenderer>();
                halo.transform.SetParent(root, false);
                halo.sprite = haloSprite;
                halo.transform.localScale = Vector3.one * 1.15f;
                halo.color = new Color(.38f, .8f, 1f, .35f);
                halo.sortingOrder = 2;

                var repair = new GameObject("Repair").AddComponent<SpriteRenderer>();
                repair.transform.SetParent(root, false);
                repair.sprite = haloSprite;
                repair.transform.localScale = Vector3.one * .95f;
                repair.color = new Color(1f, .82f, .18f, .45f);
                repair.sortingOrder = 1;
                repair.enabled = false;

                var renderer = new GameObject("Device").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root, false);
                renderer.sprite = SpriteFor(node.Role);
                renderer.transform.localScale = Vector3.one * .72f;
                renderer.sortingOrder = 3;

                nodeViews.Add(new NodeView
                {
                    Root = root,
                    Sprite = renderer,
                    Halo = halo,
                    RepairRing = repair,
                    Role = node.Role
                });
            }

            selected = -1;
            secondSelected = -1;
            selectedLink = -1;
            routeLinks.Clear();
            backupRouteLinks.Clear();
            routeSource = -1;
            previewLine = null;
            Refresh();
        }

        private Sprite SpriteFor(DeviceRole role)
        {
            if (role == DeviceRole.Router) return routerSprite;
            if (role == DeviceRole.Layer3Switch) return switchSprite;
            return accessSprite;
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

        public int HitTest(Vector3 world, float threshold = .5f)
        {
            if (simulation == null) return -1;
            for (int i = 0; i < simulation.Nodes.Count; i++)
                if ((!visibleTier.HasValue || simulation.Nodes[i].Tier == visibleTier.Value) &&
                    Vector2.Distance(simulation.Nodes[i].Position, new Vector2(world.x, world.y)) < threshold)
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
                if (d < best) { best = d; bestIdx = i; }
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
                    c = new Color(1f, .84f, .28f);
                else if (node.Tier == EquipmentTier.Distribution)
                    c = new Color(.35f, .65f, 1f);
                else
                    c = new Color(.25f, .92f, .72f);

                bool visible = !visibleTier.HasValue || node.Tier == visibleTier.Value;
                nodeViews[i].Sprite.sprite = SpriteFor(node.Role);
                nodeViews[i].Sprite.color = c;
                nodeViews[i].Root.gameObject.SetActive(visible);
                nodeViews[i].Halo.enabled = i == selected || i == secondSelected;
                nodeViews[i].Halo.color = i == secondSelected
                    ? new Color(1f, .75f, .19f, .55f)
                    : new Color(.38f, .8f, 1f, .4f);
                float scale = node.Tier == EquipmentTier.Core ? 1.65f
                    : node.Tier == EquipmentTier.Distribution ? 1.28f : 1.05f;
                nodeViews[i].Root.localScale = Vector3.one * scale;

                bool repairing = node.Failed && node.RepairRemaining > 0f;
                nodeViews[i].RepairRing.enabled = repairing;
            }

            while (linkViews.Count < simulation.Links.Count)
                AddLinkVisual(linkViews.Count);

            for (int i = 0; i < simulation.Links.Count; i++)
            {
                var link = simulation.Links[i];
                bool constructing = link.ConstructionRemaining > 0f;
                bool down = link.Failed || simulation.Nodes[link.A].Failed || simulation.Nodes[link.B].Failed;
                Color col = constructing ? new Color(.95f, .69f, .26f) : down
                    ? new Color(.36f, .39f, .48f)
                    : Color.Lerp(new Color(.12f, .62f, .77f), new Color(1f, .48f, .22f),
                        Mathf.Clamp01(link.Capacity > 0 ? link.Load / link.Capacity : 1));

                if (ShowRoutes && backupRouteLinks.Contains(i)) col = new Color(.75f, .42f, 1f);
                if (ShowRoutes && routeLinks.Contains(i)) col = new Color(.95f, .94f, .29f);
                if (i == selectedLink) col = Color.white;

                linkViews[i].startColor = linkViews[i].endColor = col;
                float w = constructing ? .035f : down ? .04f : Mathf.Lerp(.05f, .095f, Mathf.Clamp01(link.Load / Mathf.Max(1f, link.Capacity)));
                if (link.PlayerBuilt) w += .015f;
                if (ShowRoutes && backupRouteLinks.Contains(i)) w = .12f;
                if (ShowRoutes && routeLinks.Contains(i)) w = .16f;
                linkViews[i].startWidth = linkViews[i].endWidth = w;
                linkViews[i].SetPosition(0, simulation.Nodes[link.A].Position);
                linkViews[i].SetPosition(1, simulation.Nodes[link.B].Position);
            }
        }

        private void BuildTerminalGrid()
        {
            for (int coordinate = -14; coordinate <= 14; coordinate += 2)
            {
                AddGridLine(new Vector3(coordinate, -11, 0), new Vector3(coordinate, 11, 0), coordinate == 0);
                AddGridLine(new Vector3(-15, coordinate, 0), new Vector3(15, coordinate, 0), coordinate == 0);
            }
        }

        private void AddGridLine(Vector3 a, Vector3 b, bool axis)
        {
            var obj = new GameObject(axis ? "Radar Axis" : "Radar Grid");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.material = lineMaterial;
            line.sortingOrder = -10;
            line.startWidth = line.endWidth = axis ? .018f : .008f;
            Color color = axis ? new Color(.12f, .35f, .37f, .55f) : new Color(.065f, .22f, .24f, .35f);
            line.startColor = line.endColor = color;
            backgroundGrid.Add(line);
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
                sr.sprite = packetSprite;
                sr.color = new Color(.4f, .96f, 1f);
                sr.sortingOrder = 2;
                packet.localScale = Vector3.one * .14f;
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
                    float pulse = 0.9f + 0.12f * Mathf.Sin(clock * 5.5f);
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
                bool active = link.ConstructionRemaining <= 0f && !link.Failed &&
                              !simulation.Nodes[link.A].Failed &&
                              !simulation.Nodes[link.B].Failed &&
                              link.Load > 0.4f;

                particles[i].gameObject.SetActive(active);
                if (!active) continue;

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
            backgroundGrid.Clear();
            linkViews.Clear();
            particles.Clear();
            particleLinks.Clear();
            if (lineMaterial != null) Destroy(lineMaterial);
            DestroySprite(routerSprite);
            DestroySprite(switchSprite);
            DestroySprite(accessSprite);
            DestroySprite(haloSprite);
            routerSprite = switchSprite = accessSprite = haloSprite = packetSprite = null;
        }

        private static void DestroySprite(Sprite s)
        {
            if (s == null) return;
            var t = s.texture;
            Object.Destroy(s);
            if (t != null) Object.Destroy(t);
        }

        private void OnDestroy() { ClearMap(); }
    }
}
