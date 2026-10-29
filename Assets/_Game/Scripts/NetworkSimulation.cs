using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public enum NodeStatus { Healthy, Overloaded, Offline, Repairing }
    public enum EquipmentTier { Core, Distribution, Access }
    public enum DeviceRole { Router, Layer3Switch, Layer2Switch }
    public enum RoutingMode { Static, OspfSimulated }
    public enum IncidentScenario { None, FiberCut, PowerFailure, Cascade }
    public enum LinkMedium { Copper, Fiber, Radio }

    [Serializable]
    public sealed class NetworkNode
    {
        public int Id;
        public Vector2 Position;
        public EquipmentTier Tier;
        public float Demand;
        public float Capacity;
        public float Load;
        public bool Failed;
        public float RepairRemaining;
        public string Hostname;
        public string OriginOperator;
        public int OriginAsn;
        public string ManagementIp;
        public int VlanId;
        public bool AdminEnabled = true;
        public DeviceRole Role;
        public RoutingMode Routing = RoutingMode.Static;
        public int OspfArea;
        public int Mtu = 1500;

        public NodeStatus Status
        {
            get
            {
                if (Failed)
                    return RepairRemaining > 0f ? NodeStatus.Repairing : NodeStatus.Offline;
                return Load > Capacity ? NodeStatus.Overloaded : NodeStatus.Healthy;
            }
        }
    }

    [Serializable]
    public sealed class NetworkLink
    {
        public int A, B;
        public float Capacity, Load;
        public bool Failed;
        public bool PlayerBuilt;
        public LinkMedium Medium;
        public float ConstructionRemaining;
        public int PortA, PortB;
        public int MaterialCost;
        public string EndpointA;
        public string EndpointB;
        public string Prefix;
        public float RepairRemaining;
        /// <summary>Directional flow for visualization: +A to B, -B to A.</summary>
        public float FlowAB;
    }

    public sealed class NetworkSimulation
    {
        public const int RepairCost = 15;
        public const int LinkRepairCost = 12;
        public const int BuildCost = 35;
        public const int UpgradeCost = 25;
        public const float NodeRepairSeconds = 7f;
        public const float LinkRepairSeconds = 4.5f;

        public readonly List<NetworkNode> Nodes = new List<NetworkNode>();
        public readonly List<NetworkLink> Links = new List<NetworkLink>();
        public readonly List<NetworkInterface> Interfaces = new List<NetworkInterface>();
        public readonly List<VLAN> Vlans = new List<VLAN>();
        public readonly List<Subnet> Subnets = new List<Subnet>();
        public readonly List<RoutingTable> RoutingTables = new List<RoutingTable>();
        public readonly List<NeighborTable> NeighborTables = new List<NeighborTable>();
        public readonly List<SwitchingTable> SwitchingTables = new List<SwitchingTable>();
        public readonly Dictionary<int, List<int>> PrimaryPaths = new Dictionary<int, List<int>>();
        public readonly Dictionary<int, List<int>> BackupPaths = new Dictionary<int, List<int>>();
        public readonly Dictionary<int, float> PrimaryTraffic = new Dictionary<int, float>();
        public readonly Dictionary<int, float> BackupTraffic = new Dictionary<int, float>();
        private readonly System.Random random;

        public int TickCount { get; private set; }
        public int Money { get; private set; } = 180;
        public int Customers { get; private set; } = 12;
        public int AcquiredMaintenance { get; private set; }
        public int AcquiredSites { get; private set; }
        public int MarketReach { get; private set; } = 1;
        public string OperatorScale => Customers >= 2000 ? "Транснациональный" : Customers >= 500 ? "Национальный" : Customers >= 120 ? "Региональный" : "Локальный ISP";
        public int EquipmentUpgrades { get; private set; }
        // ... rest of file truncated for this call - will follow with full push via alternate
    }
}
