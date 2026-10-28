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
        }

        private readonly List<NodeView> nodeViews = new List<NodeView>();
        private readonly List<LineRenderer> linkViews = new List<LineRenderer>();
        private readonly List<Transform> particles = new List<Transform>();
        private readonly List<int> particleLinks = new List<int>();
        private NetworkSimulation simulation;
        private Sprite sprite;
        private Material lineMaterial;
        private int selected = -1;
        private float clock;

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
            {
                var link = model.Links[i];
                var obj = new GameObject("Link " + link.A + "-" + link.B);
                obj.transform.SetParent(transform, false);
                var lr = obj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, model.Nodes[link.A].Position);
                lr.SetPosition(1, model.Nodes[link.B].Position);
                lr.material = lineMaterial;
                lr.sortingOrder = 0;
                lr.startWidth = lr.endWidth = .055f;
                linkViews.Add(lr);
                for (int p = 0; p < 2; p++)
                {
                    var particle = new GameObject("Traffic packet").transform;
                    particle.SetParent(transform, false);
                    var renderer = particle.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.color = new Color(.4f, .96f, 1f);
                    renderer.sortingOrder = 2;
                    particle.localScale = Vector3.one * .11f;
                    particles.Add(particle);
                    particleLinks.Add(i);
                }
            }
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
                var renderer = new GameObject("Device").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(root, false);
                renderer.sprite = sprite;
                renderer.transform.localScale = Vector3.one * .53f;
                renderer.sortingOrder = 3;
                nodeViews.Add(new NodeView { Root = root, Sprite = renderer, Halo = halo });
            }
            Refresh();
        }

        public void SetSelected(int index)
        {
            selected = index;
            Refresh();
        }

        public int HitTest(Vector3 world, float threshold = .45f)
        {
            if (simulation == null) return -1;
            for (int i = 0; i < simulation.Nodes.Count; i++)
                if (Vector2.Distance(simulation.Nodes[i].Position, new Vector2(world.x, world.y)) < threshold)
                    return i;
            return -1;
        }

        public void Refresh()
        {
            if (simulation == null) return;
            for (int i = 0; i < simulation.Nodes.Count; i++)
            {
                var node = simulation.Nodes[i];
                nodeViews[i].Sprite.color = node.Failed ? new Color(1f, .24f, .32f) :
                    node.Status == NodeStatus.Overloaded ? new Color(1f, .57f, .2f) :
                    i == 0 ? new Color(1f, .79f, .3f) : new Color(.21f, .88f, .73f);
                nodeViews[i].Halo.enabled = i == selected;
            }
            for (int i = 0; i < simulation.Links.Count; i++)
            {
                var link = simulation.Links[i];
                linkViews[i].startColor = linkViews[i].endColor =
                    link.Failed || simulation.Nodes[link.A].Failed || simulation.Nodes[link.B].Failed ?
                    new Color(.36f, .39f, .48f) :
                    Color.Lerp(new Color(.12f, .62f, .77f), new Color(1f, .48f, .22f),
                    Mathf.Clamp01(link.Capacity > 0 ? link.Load / link.Capacity : 1));
            }
        }

        private void Update()
        {
            if (simulation == null) return;
            clock += Time.deltaTime;
            for (int i = 0; i < particles.Count; i++)
            {
                int linkIndex = particleLinks[i];
                var link = simulation.Links[linkIndex];
                bool active = !link.Failed && !simulation.Nodes[link.A].Failed && !simulation.Nodes[link.B].Failed && link.Load > 0;
                particles[i].gameObject.SetActive(active);
                if (!active) continue;
                float t = Mathf.Repeat(clock * (.22f + .14f * Mathf.Clamp01(link.Load / link.Capacity)) + (i % 2) * .5f, 1f);
                particles[i].position = Vector3.Lerp(simulation.Nodes[link.A].Position, simulation.Nodes[link.B].Position, t);
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
