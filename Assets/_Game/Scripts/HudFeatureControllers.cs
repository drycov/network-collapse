using System;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    /// <summary>Explicit view boundary for feature-specific HUD components.</summary>
    public abstract class HudFeatureController
    {
        public VisualElement Root { get; }
        public VisualElement Content { get; }
        protected HudFeatureController(string asset, string rootName, string contentName)
        {
            Root = HudViewAssets.Instantiate(asset, rootName);
            if (Root == null) throw new InvalidOperationException(asset + ".uxml is missing");
            Content = Root.Q<VisualElement>(contentName);
            if (Content == null) throw new InvalidOperationException(asset + ".uxml missing " + contentName);
        }
        public void SetContent(VisualElement content)
        {
            Content.Clear();
            if (content != null) Content.Add(content);
        }
    }
    public sealed class HudMarketController : HudFeatureController
    {
        private readonly Func<IspWorld> world;
        private readonly Action<IspWorld> render;
        public HudMarketController(Func<IspWorld> world, Action<IspWorld> render)
            : base("Market", "market-root", "market-content")
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.render = render ?? throw new ArgumentNullException(nameof(render));
        }
        public void Refresh() { var value = world(); if (value != null) render(value); }
    }
    public sealed class HudMissionController : HudFeatureController
    {
        private readonly Func<NetworkCampaign> campaign;
        public HudMissionController(Func<NetworkCampaign> campaign)
            : base("Missions", "missions-root", "missions-content")
        {
            this.campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        }
        public NetworkCampaign Current => campaign();
    }
    public sealed class HudInspectorController : HudFeatureController
    {
        private readonly Func<NetworkSimulation> simulation;
        public HudInspectorController(Func<NetworkSimulation> simulation)
            : base("Inspector", "inspector-root", "inspector-content")
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }
        public bool ValidNode(int id) => simulation() != null && id >= 0 && id < simulation().Nodes.Count;
    }
    public sealed class HudMapController : HudFeatureController
    {
        private readonly Func<NetworkSimulation> simulation;
        public HudMapController(Func<NetworkSimulation> simulation)
            : base("Map", "map-overlay", "map-status")
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }
        public int NodeCount => simulation()?.Nodes.Count ?? 0;
    }
}
