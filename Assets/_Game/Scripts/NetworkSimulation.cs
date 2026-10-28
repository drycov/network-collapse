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
        public string EndpointA;
        public string EndpointB;
        public string Prefix;
        public float RepairRemaining;
        /// <summary>Directional flow for visualization: +A	o B, -B	o A.</summary>
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
        public int EquipmentUpgrades { get; private set; }
        public bool UpgradeDevice(int id)
        {
            if (GameOver || id < 0 || id >= Nodes.Count || Money < 40 || Nodes[id].Failed) return false;
            Money -= 40;
            Nodes[id].Capacity += Nodes[id].Tier == EquipmentTier.Core ? 80 : 35;
            EquipmentUpgrades++;
            Step();
            return true;
        }
        public void GrantCredits(int amount) { if (amount > 0) Money += amount; }
        public float Availability { get; private set; } = 1f;
        public float DroppedTraffic { get; private set; }
        public int FailedNodes => Nodes.FindAll(n => n.Failed).Count;
        public int FailedLinks => Links.FindAll(l => l.Failed).Count;
        public IncidentScenario LastScenario { get; private set; }

        // Progression
        public int Score { get; private set; }
        public int Level { get; private set; } = 1;
        public float ObjectiveTimer { get; private set; } = 80f;
        public float ObjectiveProgress { get; private set; }
        public float TargetAvailability { get; private set; } = 0.84f;
        public bool ObjectiveComplete { get; private set; }
        public bool GameOver { get; private set; }

        [Serializable] public sealed class Snapshot
        {
            public int Level, Money, Score, TickCount, EquipmentUpgrades;
            public float ObjectiveProgress;
            public bool ObjectiveComplete, GameOver;
            public List<NetworkNode> Nodes = new List<NetworkNode>();
            public List<NetworkLink> Links = new List<NetworkLink>();
        }
        public Snapshot Capture()
        {
            var s = new Snapshot { Level = Level, Money = Money, Score = Score,
                TickCount = TickCount, ObjectiveProgress = ObjectiveProgress,
                ObjectiveComplete = ObjectiveComplete, GameOver = GameOver,
                EquipmentUpgrades = EquipmentUpgrades };
            foreach (var node in Nodes) s.Nodes.Add(UnityEngine.JsonUtility.FromJson<NetworkNode>(UnityEngine.JsonUtility.ToJson(node)));
            foreach (var link in Links) s.Links.Add(UnityEngine.JsonUtility.FromJson<NetworkLink>(UnityEngine.JsonUtility.ToJson(link)));
            return s;
        }
        public bool Restore(Snapshot s)
        {
            if (s == null || s.Nodes == null || s.Links == null || s.Nodes.Count < 2) return false;
            foreach (var link in s.Links)
                if (link.A < 0 || link.B < 0 || link.A >= s.Nodes.Count || link.B >= s.Nodes.Count) return false;
            Nodes.Clear(); Links.Clear();
            Nodes.AddRange(s.Nodes); Links.AddRange(s.Links);
            Level = Mathf.Max(1, s.Level); Money = Mathf.Max(0, s.Money);
            Score = Mathf.Max(0, s.Score); TickCount = Mathf.Max(0, s.TickCount);
            ObjectiveProgress = Mathf.Max(0, s.ObjectiveProgress);
            ObjectiveComplete = s.ObjectiveComplete; GameOver = s.GameOver;
            EquipmentUpgrades = Mathf.Max(0, s.EquipmentUpgrades);
            TargetAvailability = Mathf.Clamp(.80f + Level * .015f, .80f, .93f);
            ObjectiveTimer = 70f + Level * 10f;
            Interfaces.Clear(); Subnets.Clear(); RoutingTables.Clear(); NeighborTables.Clear(); SwitchingTables.Clear(); Vlans.Clear();
            for (int i = 0; i < Links.Count; i++)
            {
                var l = Links[i];
                Interfaces.Add(new NetworkInterface { NodeId = l.A, LinkIndex = i, Name = "eth" + i, Ipv4Address = l.EndpointA, VlanId = Nodes[l.A].VlanId, Mtu = Nodes[l.A].Mtu, AdminUp = Nodes[l.A].AdminEnabled });
                Interfaces.Add(new NetworkInterface { NodeId = l.B, LinkIndex = i, Name = "eth" + i, Ipv4Address = l.EndpointB, VlanId = Nodes[l.B].VlanId, Mtu = Nodes[l.B].Mtu, AdminUp = Nodes[l.B].AdminEnabled });
                Subnets.Add(new Subnet { NetworkAddress = l.EndpointA, PrefixLength = 31, Purpose = "Restored link " + i });
            }
            // Recompute derived routing without advancing mission timers or economy.
            RecomputeTrafficOnly();
            return true;
        }
        private void RecomputeTrafficOnly()
        {
            foreach (var n in Nodes) n.Load = 0;
            foreach (var l in Links) { l.Load = 0; l.FlowAB = 0; }
            PrimaryPaths.Clear(); BackupPaths.Clear(); PrimaryTraffic.Clear(); BackupTraffic.Clear();
            float requested = 0, delivered = 0;
            for (int i = 1; i < Nodes.Count; i++)
            {
                var n = Nodes[i]; requested += n.Demand;
                if (n.Failed || !n.AdminEnabled) continue;
                var path = FindOptimalPath(i, 0, n.Demand);
                if (path == null) continue;
                PrimaryPaths[i] = path; PrimaryTraffic[i] = n.Demand;
                RecordFlow(path, i, n.Demand);
                n.Load += n.Demand; Nodes[0].Load += n.Demand; delivered += n.Demand;
            }
            Availability = requested > 0 ? delivered / requested : 1f;
            DroppedTraffic = Mathf.Max(0, requested - delivered);
            RebuildDerivedTables();
        }

        public NetworkSimulation(int seed = 2026, int level = 1)
        {
            random = new System.Random(seed + level * 97);
            Level = Mathf.Max(1, level);
            TargetAvailability = Mathf.Clamp(0.80f + Level * 0.015f, 0.80f, 0.93f);
            ObjectiveTimer = 70f + Level * 10f;
            BuildTopology();
            Step();
        }

        // Seeded asymmetric, hierarchical network: dual core, distributed aggregation and access spokes.
        // Addressing uses RFC1918 for management and RFC3021 /31 on simulated routed P2P links.
        private void BuildTopology()
        {
            Nodes.Clear();
            Links.Clear();
            Interfaces.Clear(); Vlans.Clear(); Subnets.Clear(); RoutingTables.Clear();
            NeighborTables.Clear(); SwitchingTables.Clear();
            PrimaryPaths.Clear(); BackupPaths.Clear(); PrimaryTraffic.Clear(); BackupTraffic.Clear();
            int count = 16 + Mathf.Min(Level, 6);
            for (int i = 0; i < count; i++)
            {
                EquipmentTier tier = i < 2 ? EquipmentTier.Core :
                    i < 7 ? EquipmentTier.Distribution : EquipmentTier.Access;
                float x, y;
                if (i == 0) { x = -3.2f; y = 4.6f; }
                else if (i == 1) { x = 3.5f; y = 4.1f; }
                else if (i < 7)
                {
                    x = -6.2f + (i - 2) * 3.1f + RandomRange(-.55f, .55f);
                    y = 1.0f + RandomRange(-.9f, .6f);
                }
                else
                {
                    int group = (i - 7) % 5;
                    int row = (i - 7) / 5;
                    x = -6.5f + group * 3.15f + RandomRange(-.9f, .8f);
                    y = -2.0f - row * 2.0f + RandomRange(-.85f, .65f);
                }
                var role = tier == EquipmentTier.Core ? DeviceRole.Router :
                    tier == EquipmentTier.Distribution ? DeviceRole.Layer3Switch : DeviceRole.Layer2Switch;
                Nodes.Add(new NetworkNode
                {
                    Id = i, Tier = tier, Role = role,
                    Hostname = (tier == EquipmentTier.Core ? "cr" : tier == EquipmentTier.Distribution ? "ds" : "as") + "-" + i.ToString("00"),
                    ManagementIp = "10.200." + (i / 254) + "." + (i % 254 + 1),
                    VlanId = tier == EquipmentTier.Access ? 100 + ((i - 7) % 5) * 10 : 1,
                    Routing = role == DeviceRole.Layer2Switch ? RoutingMode.Static : RoutingMode.OspfSimulated,
                    Position = new Vector2(x, y),
                    Demand = tier == EquipmentTier.Core ? 0 : 4 + random.Next(8 + Level * 2),
                    Capacity = tier == EquipmentTier.Core ? 350 : tier == EquipmentTier.Distribution ? 140 : 45
                });
            }
            AddLink(0, 1, 180, false);
            for (int i = 2; i < Nodes.Count; i++)
            {
                if (i < 7)
                {
                    AddLink(i, (i % 2), 100, false);
                    if (i % 2 == 0) AddLink(i, 1, 70, false);
                }
                else
                {
                    int parent = 2 + (i - 7) % 5;
                    AddLink(i, parent, 45, false);
                    if ((i - 7) % 4 == 0)
                        AddLink(i, 2 + ((parent - 1) % 5), 35, false);
                }
            }
            // A few unequal cross-connects create loops and diverse restoration routes.
            for (int i = 2; i < 6; i++)
                if (random.NextDouble() < .72) AddLink(i, i + 1, 60, false);
        }

        private float RandomRange(float low, float high) => low + (float)random.NextDouble() * (high - low);

        public bool ConfigureNode(int id, int vlan, int mtu, bool enabled, RoutingMode routing)
        {
            if (id < 0 || id >= Nodes.Count || vlan < 1 || vlan > 4094 ||
                mtu < 576 || mtu > 9216) return false;
            var node = Nodes[id];
            node.VlanId = vlan;
            node.Mtu = mtu;
            node.AdminEnabled = enabled;
            node.Routing = node.Role == DeviceRole.Layer2Switch ? RoutingMode.Static : routing;
            foreach (var iface in Interfaces) if (iface.NodeId == id) { iface.Mtu = mtu; iface.VlanId = vlan; iface.AdminUp = enabled; }
            Step();
            return true;
        }

        private void AddLink(int a, int b, float capacity, bool built)
        {
            int index = Links.Count;
            string prefix = "10.254." + (index / 128) + "." + ((index % 128) * 2);
            Links.Add(new NetworkLink {
                A = a, B = b, Capacity = capacity, PlayerBuilt = built,
                Prefix = prefix + "/31", EndpointA = prefix, EndpointB = "10.254." + (index / 128) + "." + ((index % 128) * 2 + 1)
            });
            var left = Nodes[a]; var right = Nodes[b];
            Interfaces.Add(new NetworkInterface { Name = "eth" + index, NodeId = a, LinkIndex = index,
                Ipv4Address = Links[index].EndpointA, VlanId = left.VlanId, Mtu = left.Mtu,
                Mode = left.Role == DeviceRole.Layer2Switch ? InterfaceMode.Access : InterfaceMode.Routed });
            Interfaces.Add(new NetworkInterface { Name = "eth" + index, NodeId = b, LinkIndex = index,
                Ipv4Address = Links[index].EndpointB, VlanId = right.VlanId, Mtu = right.Mtu,
                Mode = right.Role == DeviceRole.Layer2Switch ? InterfaceMode.Access : InterfaceMode.Routed });
            Subnets.Add(new Subnet { NetworkAddress = prefix, PrefixLength = 31, Purpose = "Simulated link " + index });
        }

        public int FindLink(int a, int b) =>
            Links.FindIndex(l => (l.A == a && l.B == b) || (l.A == b && l.B == a));

        public bool BuildLink(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count ||
                FindLink(a, b) >= 0 || Money < BuildCost) return false;
            Money -= BuildCost;
            AddLink(a, b, 48 + Level * 2, true);
            Step();
            return true;
        }

        public bool UpgradeLink(int index)
        {
            if (index < 0 || index >= Links.Count || Money < UpgradeCost || Links[index].Capacity >= 240)
                return false;
            Money -= UpgradeCost;
            Links[index].Capacity = Mathf.Min(240, Links[index].Capacity + 40);
            Step();
            return true;
        }

        /// <summary>Advance one discrete routing tick. Pass real dt to progress repair timers.</summary>
        public void Step(float dt = 0f)
        {
            if (GameOver) return;

            TickCount++;

            // Repair timers
            if (dt > 0f)
            {
                foreach (var node in Nodes)
                {
                    if (node.Failed && node.RepairRemaining > 0f)
                    {
                        node.RepairRemaining -= dt;
                        if (node.RepairRemaining <= 0f)
                        {
                            node.RepairRemaining = 0f;
                            node.Failed = false;
                        }
                    }
                }
                foreach (var link in Links)
                {
                    if (link.Failed && link.RepairRemaining > 0f)
                    {
                        link.RepairRemaining -= dt;
                        if (link.RepairRemaining <= 0f)
                        {
                            link.RepairRemaining = 0f;
                            link.Failed = false;
                        }
                    }
                }
            }

            foreach (var node in Nodes) node.Load = 0f;
            PrimaryPaths.Clear(); BackupPaths.Clear(); PrimaryTraffic.Clear(); BackupTraffic.Clear();
            foreach (var link in Links)
            {
                link.Load = 0f;
                link.FlowAB = 0f;
            }

            float requested = 0f, delivered = 0f;

            // Shuffle sources to remove order bias
            var order = new List<int>();
            for (int i = 1; i < Nodes.Count; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            foreach (int source in order)
            {
                var sender = Nodes[source];
                requested += sender.Demand;
                if (sender.Failed || !sender.AdminEnabled || Nodes[0].Failed || !Nodes[0].AdminEnabled) continue;

                var path = FindOptimalPath(source, 0, sender.Demand);
                if (path == null || path.Count == 0) continue;

                // Prefer a disjoint alternate when available. Split demand across two paths
                // only when both can carry the half-flow; otherwise use the primary.
                var backup = FindOptimalPath(source, 0, sender.Demand * .5f, path);
                bool split = backup != null && backup.Count > 0;
                float primaryAmount = split ? sender.Demand * .5f : sender.Demand;
                float backupAmount = split ? sender.Demand - primaryAmount : 0f;
                bool enough = true;
                foreach (int li in path)
                    if (Links[li].Failed || Links[li].Load + primaryAmount > Links[li].Capacity) enough = false;
                if (split) foreach (int li in backup)
                    if (Links[li].Failed || Links[li].Load + backupAmount > Links[li].Capacity) enough = false;
                if (!enough) continue;

                PrimaryPaths[source] = new List<int>(path);
                PrimaryTraffic[source] = primaryAmount;
                if (split)
                {
                    BackupPaths[source] = new List<int>(backup);
                    BackupTraffic[source] = backupAmount;
                }
                sender.Load = sender.Demand;
                RecordFlow(path, source, primaryAmount);
                if (split) RecordFlow(backup, source, backupAmount);
                Nodes[0].Load += sender.Demand;
                delivered += sender.Demand;
            }

            // Mild upstream load from transit
            foreach (var link in Links)
            {
                if (link.Load <= 0f) continue;
                Nodes[link.A].Load += link.Load * 0.06f;
                Nodes[link.B].Load += link.Load * 0.06f;
            }

            RebuildDerivedTables();
            Availability = requested > 0f ? delivered / requested : 1f;
            DroppedTraffic = Mathf.Max(0f, requested - delivered);

            int income = Mathf.RoundToInt(delivered / 55f);
            int upkeep = 1 + FailedNodes + FailedLinks / 2;
            // Early levels offer recovery time before costs and incidents escalate.
            if (Level == 1 && TickCount < 15) upkeep = Mathf.Min(upkeep, 1);
            Money = Mathf.Max(0, Money + income - upkeep);
            Score += Mathf.RoundToInt(delivered * 0.35f + Availability * 6f);

            // Objective
            float stepTime = dt > 0f ? dt : 2f;
            if (!ObjectiveComplete)
            {
                if (Availability >= TargetAvailability)
                    ObjectiveProgress += stepTime;
                else
                    ObjectiveProgress = Mathf.Max(0f, ObjectiveProgress - stepTime * 0.4f);

                if (ObjectiveProgress >= ObjectiveTimer)
                {
                    ObjectiveComplete = true;
                    Money += 50 + Level * 20;
                    Score += 250 * Level;
                }
            }

            if (Nodes[0].Failed || (Availability < (Level <= 2 ? .15f : .22f) && TickCount > (Level <= 2 ? 20 : 12)))
                GameOver = true;
        }

        // Capacity-aware Dijkstra. A channel must have enough residual bandwidth for the full demand.
        // Cost combines distance in hops, utilization after reservation and physical length.
        public List<int> FindOptimalPath(int start, int goal, float demand, List<int> excludedLinks = null)
        {
            if (start < 0 || goal < 0 || start >= Nodes.Count || goal >= Nodes.Count ||
                Nodes[start].Failed || Nodes[goal].Failed || !Nodes[start].AdminEnabled || !Nodes[goal].AdminEnabled || demand <= 0f) return null;
            if (start == goal) return new List<int>();
            int count = Nodes.Count;
            var distance = new float[count];
            var parent = new int[count];
            var parentLink = new int[count];
            var visited = new bool[count];
            for (int i = 0; i < count; i++) { distance[i] = float.PositiveInfinity; parent[i] = -1; }
            distance[start] = 0f;
            for (int iteration = 0; iteration < count; iteration++)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                    if (!visited[i] && (current < 0 || distance[i] < distance[current])) current = i;
                if (current < 0 || float.IsPositiveInfinity(distance[current])) break;
                if (current == goal) break;
                visited[current] = true;
                for (int i = 0; i < Links.Count; i++)
                {
                    var link = Links[i];
                    if (link.Failed || (excludedLinks != null && excludedLinks.Contains(i)) || link.Capacity <= 0f || link.Capacity - link.Load + 0.0001f < demand ||
                        !Interfaces.Exists(n => n.LinkIndex == i && n.NodeId == link.A && n.AdminUp) ||
                        !Interfaces.Exists(n => n.LinkIndex == i && n.NodeId == link.B && n.AdminUp)) continue;
                    int next = link.A == current ? link.B : link.B == current ? link.A : -1;
                    if (next < 0 || visited[next] || Nodes[next].Failed || !Nodes[next].AdminEnabled) continue;
                    float utilization = Mathf.Clamp01((link.Load + demand) / link.Capacity);
                    float length = Vector2.Distance(Nodes[current].Position, Nodes[next].Position);
                    float cost = 1f + 3f * utilization * utilization + .015f * length;
                    float proposed = distance[current] + cost;
                    if (proposed < distance[next] - 0.0001f)
                    {
                        distance[next] = proposed;
                        parent[next] = current;
                        parentLink[next] = i;
                    }
                }
            }
            if (parent[goal] < 0) return null;
            var path = new List<int>();
            for (int n = goal; n != start; n = parent[n]) path.Add(parentLink[n]);
            path.Reverse();
            return path;
        }

        public List<int> FindDisplayRoute(int source) =>
            source > 0 && source < Nodes.Count ? FindOptimalPath(source, 0, Nodes[source].Demand) : null;

        private void RecordFlow(List<int> path, int source, float amount)
        {
            int previous = source;
            foreach (int index in path)
            {
                var link = Links[index];
                link.Load += amount;
                if (link.A == previous) { link.FlowAB += amount; previous = link.B; }
                else { link.FlowAB -= amount; previous = link.A; }
            }
        }

        private void RebuildDerivedTables()
        {
            RoutingTables.Clear(); NeighborTables.Clear(); SwitchingTables.Clear(); Vlans.Clear();
            foreach (var node in Nodes)
            {
                var routing = new RoutingTable { NodeId = node.Id };
                if (PrimaryPaths.TryGetValue(node.Id, out var primary) && primary.Count > 0)
                {
                    int li = primary[0]; var link = Links[li];
                    routing.Routes.Add(new RouteEntry { Destination = "0.0.0.0", PrefixLength = 0,
                        NextHop = link.A == node.Id ? link.EndpointB : link.EndpointA,
                        OutInterface = "eth" + li, Metric = 10, Active = true });
                }
                if (BackupPaths.TryGetValue(node.Id, out var backup) && backup.Count > 0)
                {
                    int li = backup[0]; var link = Links[li];
                    routing.Routes.Add(new RouteEntry { Destination = "0.0.0.0", PrefixLength = 0,
                        NextHop = link.A == node.Id ? link.EndpointB : link.EndpointA,
                        OutInterface = "eth" + li, Metric = 20, Active = false });
                }
                RoutingTables.Add(routing);
                NeighborTables.Add(new NeighborTable { NodeId = node.Id });
                SwitchingTables.Add(new SwitchingTable { NodeId = node.Id });
                if (!Vlans.Exists(v => v.Id == node.VlanId))
                    Vlans.Add(new VLAN { Id = node.VlanId, Name = "VLAN " + node.VlanId });
                Vlans.Find(v => v.Id == node.VlanId).MemberNodes.Add(node.Id);
            }
            foreach (var iface in Interfaces)
            {
                var link = Links[iface.LinkIndex];
                iface.OperationalUp = iface.AdminUp && !link.Failed && !Nodes[link.A].Failed && !Nodes[link.B].Failed;
            }
        }

        /// <summary>Start timed repair of a failed node (costs money, takes time).</summary>
        public bool StartNodeRepair(int id)
        {
            if (id < 0 || id >= Nodes.Count) return false;
            var node = Nodes[id];
            if (!node.Failed || node.RepairRemaining > 0f || Money < RepairCost) return false;
            Money -= RepairCost;
            node.RepairRemaining = NodeRepairSeconds;
            return true;
        }

        /// <summary>Instant toggle (debug / scenario) — prefers timed repair when failed.</summary>
        public bool ToggleFailure(int id)
        {
            if (id < 0 || id >= Nodes.Count) return false;
            if (Nodes[id].Failed)
                return StartNodeRepair(id);
            Nodes[id].Failed = true;
            Nodes[id].RepairRemaining = 0f;
            Step();
            return true;
        }

        public bool StartLinkRepair(int index)
        {
            if (index < 0 || index >= Links.Count) return false;
            var link = Links[index];
            if (!link.Failed || link.RepairRemaining > 0f || Money < LinkRepairCost) return false;
            Money -= LinkRepairCost;
            link.RepairRemaining = LinkRepairSeconds;
            return true;
        }

        public bool RepairLink(int index) => StartLinkRepair(index);

        public void RandomFailure()
        {
            if (random.NextDouble() < 0.72)
            {
                var available = Nodes.FindAll(n => !n.Failed && n.Id != 0);
                if (available.Count == 0) return;
                available[random.Next(available.Count)].Failed = true;
                LastScenario = IncidentScenario.PowerFailure;
            }
            else
            {
                var available = Links.FindAll(l => !l.Failed);
                if (available.Count == 0) return;
                available[random.Next(available.Count)].Failed = true;
                LastScenario = IncidentScenario.FiberCut;
            }
            Step();
        }

        public void StartIncident(IncidentScenario type)
        {
            LastScenario = type;
            if (type == IncidentScenario.FiberCut)
            {
                var links = Links.FindAll(l => !l.Failed);
                if (links.Count > 0) links[random.Next(links.Count)].Failed = true;
            }
            else if (type == IncidentScenario.PowerFailure)
            {
                RandomFailure();
                return;
            }
            else if (type == IncidentScenario.Cascade)
            {
                int index = random.Next(1, Nodes.Count);
                Nodes[index].Failed = true;
                foreach (var link in Links)
                    if ((link.A == index || link.B == index) && random.NextDouble() < 0.6)
                        link.Failed = true;
            }
            Step();
        }

        public void AdvanceToNextLevel()
        {
            if (!ObjectiveComplete) return;
            Level++;
            Score += 120 * Level;
            Money += 40;
            ObjectiveComplete = false;
            ObjectiveProgress = 0f;
            TargetAvailability = Mathf.Clamp(0.80f + Level * 0.015f, 0.80f, 0.93f);
            ObjectiveTimer = 70f + Level * 10f;
            GameOver = false;
            TickCount = 0;
            LastScenario = IncidentScenario.None;
            BuildTopology();
            Step();
        }
    }
}
