using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public enum NodeStatus { Healthy, Overloaded, Offline, Repairing }
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
        public float RepairRemaining;

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
        private readonly System.Random random;

        public int TickCount { get; private set; }
        public int Money { get; private set; } = 150;
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

        public NetworkSimulation(int seed = 2026, int level = 1)
        {
            random = new System.Random(seed + level * 97);
            Level = Mathf.Max(1, level);
            TargetAvailability = Mathf.Clamp(0.80f + Level * 0.015f, 0.80f, 0.93f);
            ObjectiveTimer = 70f + Level * 10f;
            BuildTopology();
            Step();
        }

        private void BuildTopology()
        {
            Nodes.Clear();
            Links.Clear();

            int count = 16 + Mathf.Min(Level, 6);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                float radius = i % 3 == 0 ? 6.5f : 4.6f;
                var tier = i == 0 ? EquipmentTier.Core
                    : i % 3 == 0 ? EquipmentTier.Distribution
                    : EquipmentTier.Access;
                Nodes.Add(new NetworkNode
                {
                    Id = i,
                    Tier = tier,
                    Position = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                    Demand = i == 0 ? 0 : 4 + random.Next(8 + Level * 2),
                    Capacity = tier == EquipmentTier.Core ? 280 + Level * 15
                        : tier == EquipmentTier.Distribution ? 100 + Level * 8
                        : 32 + Level * 2
                });
            }

            for (int i = 0; i < Nodes.Count; i++)
                AddLink(i, (i + 1) % Nodes.Count, 42 + Level * 2, false);

            int chords = 7 + Level / 2;
            for (int i = 0; i < chords; i++)
            {
                int a = i % Nodes.Count;
                int b = (i + Nodes.Count / 2 + (i % 3)) % Nodes.Count;
                if (a != b && FindLink(a, b) < 0)
                    AddLink(a, b, 58 + Level * 3, false);
            }
        }

        private void AddLink(int a, int b, float capacity, bool built) =>
            Links.Add(new NetworkLink { A = a, B = b, Capacity = capacity, PlayerBuilt = built });

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
                if (sender.Failed || Nodes[0].Failed) continue;

                var path = FindPath(source, 0, sender.Demand);
                if (path == null || path.Count == 0) continue;

                bool enough = true;
                foreach (int li in path)
                {
                    if (Links[li].Failed || Links[li].Load + sender.Demand > Links[li].Capacity)
                    {
                        enough = false;
                        break;
                    }
                }
                if (!enough) continue;

                sender.Load = sender.Demand;
                int prev = source;
                foreach (int li in path)
                {
                    var link = Links[li];
                    link.Load += sender.Demand;
                    if (link.A == prev)
                    {
                        link.FlowAB += sender.Demand;
                        prev = link.B;
                    }
                    else
                    {
                        link.FlowAB -= sender.Demand;
                        prev = link.A;
                    }
                }
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

            Availability = requested > 0f ? delivered / requested : 1f;
            DroppedTraffic = Mathf.Max(0f, requested - delivered);

            int income = Mathf.RoundToInt(delivered / 55f);
            int upkeep = 1 + FailedNodes + FailedLinks / 2;
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

            if (Nodes[0].Failed || (Availability < 0.22f && TickCount > 12))
                GameOver = true;
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
            for (int n = goal; n != start; n = parents[n])
                result.Add(parentLinks[n]);
            result.Reverse();
            return result;
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
