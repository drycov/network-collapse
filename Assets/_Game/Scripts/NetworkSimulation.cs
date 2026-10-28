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
        public int Customers { get; private set; } = 12;
        public int AcquiredMaintenance { get; private set; };
        public int AcquiredSites { get; private set; }
        public int MarketReach { get; private set; } = 1;
        public string OperatorScale => Customers >= 2000 ? "Транснациональный" : Customers >= 500 ? "Национальный" : Customers >= 120 ? "Региональный" : "Локальный ISP";
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
        public bool SpendCredits(int amount) { if (amount < 0 || Money < amount) return false; Money -= amount; return true; }
        public void CreditNet(int amount) { Money = Mathf.Max(0, Money + amount); }
        public void LoseCustomers(int count) { Customers = Mathf.Max(0, Customers - Mathf.Max(0, count)); }
        public bool AddContractCustomers(int count, int cost)
        {
            if (count <= 0 || Money < cost || GameOver) return false;
            var active = Nodes.FindAll(n => n.Tier == EquipmentTier.Access && !n.Failed && n.AdminEnabled);
            float requested = count * .15f, spare = 0f;
            foreach (var node in active) spare += Mathf.Max(0f, node.Capacity - node.Demand);
            if (spare + .001f < requested) return false;
            foreach (var node in active)
            {
                float allocated = Mathf.Min(requested, Mathf.Max(0f, node.Capacity - node.Demand));
                node.Demand += allocated;
                requested -= allocated;
                if (requested <= .001f) break;
            }
            Money -= cost; Customers += count;
            return true;
        }
        public bool IntegrateRivalSites(System.Collections.Generic.List<Vector2> sites, int customers, int purchasePrice, string operatorName = null, int originAsn = 0)
        {
            if (sites == null || sites.Count == 0 || sites.Count + Nodes.Count > 90 ||
                Money < purchasePrice || GameOver) return false;
            // Provision all purchased sites and a backhaul in one atomic game action.
            int initialCount = Nodes.Count;
            int parent = Nodes.FindIndex(n => n.Tier == EquipmentTier.Distribution);
            if (parent < 0) parent = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                int site = AddProceduralNode(i == 0 ? EquipmentTier.Distribution : EquipmentTier.Access,
                    sites[i] * .22f + new Vector2(-3f, -2f));
                Nodes[site].OriginOperator = operatorName ?? "Acquired ISP";
                Nodes[site].OriginAsn = originAsn;
                Nodes[site].Hostname = "ma-" + site.ToString("00");
                int upstream = i == 0 ? parent : initialCount;
                AddLink(upstream, site, i == 0 ? 220f : 80f, false);
                // Acquired networks may contain deferred maintenance.
                if (i % 3 == 2) Nodes[site].Failed = true;
                if (i == 1) Links[Links.Count - 1].Failed = true;
            }
            Money -= purchasePrice;
            Customers += Mathf.Max(0, customers);
            Nodes[initialCount].Demand += customers * .12f;
            MarketReach += sites.Count;
            AcquiredSites += sites.Count;
            AcquiredMaintenance += Mathf.Max(1, sites.Count / 2);
            Step();
            return true;
        }

        public bool ExpandNetworkFree()
        {
            int fee = 70 + MarketReach * 25;
            GrantCredits(fee);
            bool success = ExpandNetwork();
            if (!success) SpendCredits(fee);
            return success;
        }
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
            public int Level, Money, Score, TickCount, EquipmentUpgrades, Customers, MarketReach, AcquiredMaintenance, AcquiredSites;
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
                EquipmentUpgrades = EquipmentUpgrades, Customers = Customers, MarketReach = MarketReach, AcquiredMaintenance = AcquiredMaintenance, AcquiredSites = AcquiredSites };
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
            Customers = Mathf.Max(0, s.Customers == 0 && s.MarketReach == 0 ? 12 : s.Customers); MarketReach = Mathf.Max(1, s.MarketReach);
            AcquiredMaintenance = Mathf.Max(0, s.AcquiredMaintenance);
            AcquiredSites = Mathf.Max(0, s.AcquiredSites);
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
            Nodes.Clear(); Links.Clear(); Interfaces.Clear(); Vlans.Clear(); Subnets.Clear();
            RoutingTables.Clear(); NeighborTables.Clear(); SwitchingTables.Clear();
            PrimaryPaths.Clear(); BackupPaths.Clear(); PrimaryTraffic.Clear(); BackupTraffic.Clear();
            // Small ISP: a gateway, distribution switch and three access sectors.
            AddProceduralNode(EquipmentTier.Core, new Vector2(-2f, 3f));
            AddProceduralNode(EquipmentTier.Distribution, new Vector2(0f, .5f));
            AddLink(0, 1, 150f, false);
            for (int i = 0; i < 3; i++)
            {
                int n = AddProceduralNode(EquipmentTier.Access,
                    new Vector2((i - 1) * 3.3f + RandomRange(-.45f, .45f), -3f + RandomRange(-.55f, .55f)));
                AddLink(1, n, 55f, false);
            }
        }

        private int AddProceduralNode(EquipmentTier tier, Vector2 pos)
        {
            int id = Nodes.Count;
            DeviceRole role = tier == EquipmentTier.Core ? DeviceRole.Router :
                tier == EquipmentTier.Distribution ? DeviceRole.Layer3Switch : DeviceRole.Layer2Switch;
            Nodes.Add(new NetworkNode {
                Id = id, Position = pos, Tier = tier, Role = role,
                Hostname = (tier == EquipmentTier.Core ? "cr" : tier == EquipmentTier.Distribution ? "ds" : "as") + "-" + id.ToString("00"),
                ManagementIp = "10.200." + (id / 254) + "." + (id % 254 + 1),
                VlanId = tier == EquipmentTier.Access ? 100 + id % 30 : 1,
                Routing = role == DeviceRole.Layer2Switch ? RoutingMode.Static : RoutingMode.OspfSimulated,
                Capacity = tier == EquipmentTier.Core ? 1200 : tier == EquipmentTier.Distribution ? 400 : 120,
                Demand = tier == EquipmentTier.Access ? 3f : 0f
            });
            return id;
        }

        public bool ExpandNetwork()
        {
            int fee = 70 + MarketReach * 25;
            if (GameOver || Money < fee || Nodes.Count >= 90) return false;
            // New districts attach to an aggregation device; major expansion adds an aggregation node.
            bool newBackbone = Nodes.Count >= 20 && Nodes.Count % 18 == 0;
            bool aggregation = Nodes.Count % 5 == 0;
            int parent = 1;
            var candidates = Nodes.FindAll(n => n.Tier == EquipmentTier.Distribution);
            if (candidates.Count > 0) parent = candidates[random.Next(candidates.Count)].Id;
            Vector2 anchor = Nodes[parent].Position;
            float angle = (float)(random.NextDouble() * Mathf.PI * 2);
            float distance = 2.7f + (float)random.NextDouble() * 2.1f;
            var pos = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (newBackbone)
            {
                int core = AddProceduralNode(EquipmentTier.Core, pos + new Vector2(3f, 2f));
                AddLink(0, core, 550, false);
                AddLink(parent, core, 250, false);
            }
            else if (aggregation)
            {
                pos += new Vector2(random.Next(-2, 3), random.Next(-2, 3));
                int agg = AddProceduralNode(EquipmentTier.Distribution, pos);
                AddLink(parent, agg, 180, false);
            }
            else
            {
                int access = AddProceduralNode(EquipmentTier.Access, pos);
                AddLink(parent, access, 70, false);
            }
            Money -= fee; MarketReach++;
            Step();
            return true;
        }

        public bool AcquireCustomers()
        {
            if (GameOver || Money < 20) return false;
            var access = Nodes.FindAll(n => n.Tier == EquipmentTier.Access && !n.Failed && n.AdminEnabled);
            if (access.Count == 0) return false;
            var node = access[random.Next(access.Count)];
            int newCustomers = Mathf.Max(3, MarketReach * 2);
            if (node.Demand + newCustomers * .15f > node.Capacity) return false;
            Money -= 20;
            Customers += newCustomers;
            node.Demand += newCustomers * .15f;
            Step();
            return true;
        }

        public string ScanNetwork()
        {
            var problems = new List<string>();
            foreach (var node in Nodes)
            {
                if (node.Failed) problems.Add(node.Hostname + ": отказ оборудования");
                else if (!node.AdminEnabled) problems.Add(node.Hostname + ": admin down");
                else if (node.Status == NodeStatus.Overloaded) problems.Add(node.Hostname + ": перегрузка");
            }
            for (int index = 0; index < Links.Count; index++)
            {
                var link = Links[index];
                if (link.Failed) problems.Add("Канал #" + index + ": авария трассы");
                else if (link.ConstructionRemaining > 0f) problems.Add("Канал #" + index + ": монтаж " + link.ConstructionRemaining.ToString("0.0") + "с");
                else if (link.Capacity > 0 && link.Load / link.Capacity > .9f) problems.Add("Канал #" + index + ": загрузка более 90%");
            }
            return problems.Count == 0 ? "NOC: активных неисправностей не обнаружено" :
                "NOC: обнаружено " + problems.Count + " событий. " + string.Join("; ", problems.GetRange(0, Mathf.Min(6, problems.Count)));
        }

        public string Diagnose(int nodeId)
        {
            if (nodeId < 0 || nodeId >= Nodes.Count) return "Выберите устройство для диагностики";
            var node = Nodes[nodeId];
            if (node.Failed) return node.RepairRemaining > 0f ? "Ремонт выполняется" : "Неисправность: питание/оборудование. Запустите ремонт.";
            if (!node.AdminEnabled) return "Порт управления административно отключён";
            var failedLink = Links.FindIndex(l => (l.A == nodeId || l.B == nodeId) && l.Failed);
            if (failedLink >= 0) return "Обрыв или отказ канала #" + failedLink + ". Выберите линию и запустите ремонт.";
            if (!PrimaryPaths.ContainsKey(nodeId) && nodeId != 0 && node.Demand > 0)
                return "Нет рабочего маршрута к Core; проверьте смежные устройства и ёмкость каналов.";
            if (node.Status == NodeStatus.Overloaded) return "Перегрузка узла, требуется модернизация.";
            return "Диагностика завершена: критических неисправностей нет.";
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
                A = a, B = b, Capacity = capacity, PlayerBuilt = built, Medium = LinkMedium.Fiber,
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

        public const float ConstructionMaxDistance = 13f;
        public float MediumMaxDistance(LinkMedium medium) =>
            medium == LinkMedium.Copper ? 2.0f : medium == LinkMedium.Fiber ? 13f : 10f;
        public float MediumCapacity(LinkMedium medium, int speed) =>
            medium == LinkMedium.Copper ? Mathf.Min(speed, 100) :
            medium == LinkMedium.Radio ? Mathf.Min(speed, 150) : Mathf.Min(speed, 1000);
        public int GetBuildCost(int a, int b) => GetBuildCost(a, b, LinkMedium.Fiber, 100);
        public int GetBuildCost(int a, int b, LinkMedium medium, int speed)
        {
            if (a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count || a == b) return -1;
            float length = Vector2.Distance(Nodes[a].Position, Nodes[b].Position);
            int materialRate = medium == LinkMedium.Copper ? 2 : medium == LinkMedium.Fiber ? 5 : 3;
            int hardware = medium == LinkMedium.Copper ? 8 : medium == LinkMedium.Fiber ? 18 : 26;
            return 12 + Mathf.CeilToInt(length * materialRate) + hardware + Mathf.CeilToInt(MediumCapacity(medium, speed) / 40f);
        }
        public int UsedPorts(int node) => Links.FindAll(l => l.A == node || l.B == node).Count;
        public int PortLimit(int node) => Nodes[node].Role == DeviceRole.Router ? 12 :
            Nodes[node].Role == DeviceRole.Layer3Switch ? 16 : 8;
        public string BuildBlockReason(int a, int b) => BuildBlockReason(a, b, LinkMedium.Fiber, 100);
        public string BuildBlockReason(int a, int b, LinkMedium medium, int speed)
        {
            if (GameOver) return "Игра завершена";
            if (a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count) return "Выберите два узла";
            if (a == b) return "Нельзя соединить узел с собой";
            if (FindLink(a, b) >= 0) return "Соединение уже существует";
            if (Nodes[a].Failed || Nodes[b].Failed || !Nodes[a].AdminEnabled || !Nodes[b].AdminEnabled) return "Оборудование недоступно";
            if (UsedPorts(a) >= PortLimit(a) || UsedPorts(b) >= PortLimit(b)) return "Нет свободных портов";
            if (speed <= 0 || MediumCapacity(medium, speed) < speed) return "Скорость не поддерживается средой";
            if (Vector2.Distance(Nodes[a].Position, Nodes[b].Position) > MediumMaxDistance(medium)) return "Превышена длина трассы";
            if (Money < GetBuildCost(a, b, medium, speed)) return "Недостаточно средств";
            return null;
        }
        public bool BuildLink(int a, int b) => BuildLink(a, b, LinkMedium.Fiber, 100);
        public bool BuildLink(int a, int b, LinkMedium medium, int speed)
        {
            if (BuildBlockReason(a, b, medium, speed) != null) return false;
            int price = GetBuildCost(a, b, medium, speed);
            int portA = UsedPorts(a) + 1, portB = UsedPorts(b) + 1;
            Money -= price;
            AddLink(a, b, speed, true);
            var link = Links[Links.Count - 1];
            link.Medium = medium; link.PortA = portA; link.PortB = portB;
            link.MaterialCost = price;
            link.ConstructionRemaining = 4f + Vector2.Distance(Nodes[a].Position, Nodes[b].Position) *
                (medium == LinkMedium.Fiber ? 1.3f : medium == LinkMedium.Copper ? .9f : .6f);
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
                    if (link.ConstructionRemaining > 0f) link.ConstructionRemaining = Mathf.Max(0f, link.ConstructionRemaining - dt);
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
            Money = Mathf.Max(0, Money + income + Mathf.FloorToInt(Customers * Availability / 14f) - upkeep - AcquiredMaintenance);
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
                    if (link.ConstructionRemaining > 0f || link.Failed || (excludedLinks != null && excludedLinks.Contains(i)) || link.Capacity <= 0f || link.Capacity - link.Load + 0.0001f < demand ||
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

        public int GetLinkRepairCost(int index)
        {
            if (index < 0 || index >= Links.Count) return LinkRepairCost;
            var link = Links[index];
            return link.Medium == LinkMedium.Fiber ? 18 : link.Medium == LinkMedium.Radio ? 15 : 10;
        }
        public float GetLinkRepairTime(int index)
        {
            if (index < 0 || index >= Links.Count) return LinkRepairSeconds;
            return Links[index].Medium == LinkMedium.Fiber ? 8f : Links[index].Medium == LinkMedium.Radio ? 5f : 4f;
        }
        public bool StartLinkRepair(int index)
        {
            if (index < 0 || index >= Links.Count) return false;
            var link = Links[index];
            if (link.ConstructionRemaining > 0f || !link.Failed || link.RepairRemaining > 0f || Money < GetLinkRepairCost(index)) return false;
            Money -= GetLinkRepairCost(index);
            link.RepairRemaining = GetLinkRepairTime(index);
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
            // Preserve company infrastructure between campaign levels.
            Step();
        }
    }
}
