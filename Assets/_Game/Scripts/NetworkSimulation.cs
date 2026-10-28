using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public enum NodeStatus { Healthy, Overloaded, Offline }
    public enum EquipmentTier { Core, Distribution, Access }
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
        public NodeStatus Status => Failed ? NodeStatus.Offline : Load > Capacity ? NodeStatus.Overloaded : NodeStatus.Healthy;
    }

    [Serializable]
    public sealed class NetworkLink
    {
        public int A, B;
        public float Capacity, Load;
        public bool Failed;
        public bool PlayerBuilt;
    }

    public sealed class NetworkSimulation
    {
        public const int RepairCost = 15;
        public const int BuildCost = 35;
        public const int UpgradeCost = 25;
        public readonly List<NetworkNode> Nodes = new List<NetworkNode>();
        public readonly List<NetworkLink> Links = new List<NetworkLink>();
        private readonly System.Random random;
        public int TickCount { get; private set; }
        public int Money { get; private set; } = 150;
        public float Availability { get; private set; } = 1f;
        public float DroppedTraffic { get; private set; }
        public int FailedNodes => Nodes.FindAll(n => n.Failed).Count;
        public IncidentScenario LastScenario { get; private set; }

        public NetworkSimulation(int seed = 2026)
        {
            random = new System.Random(seed);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2f / 18f;
                float radius = i % 3 == 0 ? 6.4f : 4.5f;
                var tier = i == 0 ? EquipmentTier.Core : i % 3 == 0 ? EquipmentTier.Distribution : EquipmentTier.Access;
                Nodes.Add(new NetworkNode {
                    Id = i, Tier = tier,
                    Position = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                    Demand = i == 0 ? 0 : 5 + random.Next(13),
                    Capacity = tier == EquipmentTier.Core ? 300 : tier == EquipmentTier.Distribution ? 120 : 35
                });
            }
            for (int i = 0; i < Nodes.Count; i++) AddLink(i, (i + 1) % Nodes.Count, 45, false);
            for (int i = 0; i < 9; i++) AddLink(i, i + 9, 65, false);
            Step();
        }

        private void AddLink(int a, int b, float capacity, bool built) =>
            Links.Add(new NetworkLink { A = a, B = b, Capacity = capacity, PlayerBuilt = built });

        public int FindLink(int a, int b) => Links.FindIndex(l => (l.A == a && l.B == b) || (l.A == b && l.B == a));

        public bool BuildLink(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count ||
                FindLink(a, b) >= 0 || Money < BuildCost) return false;
            Money -= BuildCost;
            AddLink(a, b, 45, true);
            Step();
            return true;
        }

        public bool UpgradeLink(int index)
        {
            if (index < 0 || index >= Links.Count || Money < UpgradeCost || Links[index].Capacity >= 240) return false;
            Money -= UpgradeCost;
            Links[index].Capacity = Mathf.Min(240, Links[index].Capacity + 45);
            Step();
            return true;
        }

        public void Step()
        {
            TickCount++;
            foreach (var node in Nodes) node.Load = 0;
            foreach (var link in Links) link.Load = 0;
            float requested = 0, delivered = 0;
            for (int source = 1; source < Nodes.Count; source++)
            {
                var sender = Nodes[source];
                requested += sender.Demand;
                if (sender.Failed || Nodes[0].Failed) continue;
                var path = FindPath(source, 0, sender.Demand);
                if (path == null) continue;
                bool enough = true;
                foreach (int li in path)
                    if (Links[li].Load + sender.Demand > Links[li].Capacity) { enough = false; break; }
                if (!enough) continue;
                sender.Load = sender.Demand;
                foreach (int li in path) Links[li].Load += sender.Demand;
                delivered += sender.Demand;
            }
            // A congested backbone can temporarily overload upstream equipment.
            foreach (var link in Links)
            {
                if (link.Load <= 0) continue;
                Nodes[link.A].Load += link.Load * .08f;
                Nodes[link.B].Load += link.Load * .08f;
            }
            Availability = requested > 0 ? delivered / requested : 1f;
            DroppedTraffic = Mathf.Max(0, requested - delivered);
            Money += Mathf.RoundToInt(delivered / 60f);
        }

        private List<int> FindPath(int start, int goal, float demand)
        {
            var visited = new bool[Nodes.Count];
            var parents = new int[Nodes.Count];
            var parentLinks = new int[Nodes.Count];
            for (int i = 0; i < parents.Length; i++) parents[i] = -1;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == goal) break;
                for (int i = 0; i < Links.Count; i++)
                {
                    var link = Links[i];
                    if (link.Failed || link.Load + demand > link.Capacity) continue;
                    int next = link.A == current ? link.B : link.B == current ? link.A : -1;
                    if (next < 0 || visited[next] || Nodes[next].Failed) continue;
                    visited[next] = true;
                    parents[next] = current;
                    parentLinks[next] = i;
                    queue.Enqueue(next);
                }
            }
            if (!visited[goal]) return null;
            var result = new List<int>();
            for (int n = goal; n != start; n = parents[n]) result.Add(parentLinks[n]);
            result.Reverse();
            return result;
        }

        public bool ToggleFailure(int id)
        {
            if (id < 0 || id >= Nodes.Count || (Nodes[id].Failed && Money < RepairCost)) return false;
            if (Nodes[id].Failed) Money -= RepairCost;
            Nodes[id].Failed = !Nodes[id].Failed;
            Step();
            return true;
        }

        public void RandomFailure()
        {
            var available = Nodes.FindAll(n => !n.Failed && n.Id != 0);
            if (available.Count == 0) return;
            available[random.Next(available.Count)].Failed = true;
            LastScenario = IncidentScenario.PowerFailure;
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
                int index = random.Next(Nodes.Count);
                Nodes[index].Failed = true;
                foreach (var link in Links)
                    if ((link.A == index || link.B == index) && random.NextDouble() < .65)
                        link.Failed = true;
            }
            Step();
        }

        public bool RepairLink(int index)
        {
            if (index < 0 || index >= Links.Count || !Links[index].Failed || Money < RepairCost) return false;
            Money -= RepairCost;
            Links[index].Failed = false;
            Step();
            return true;
        }
    }
}
