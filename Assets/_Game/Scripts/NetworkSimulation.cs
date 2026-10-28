using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public enum NodeStatus { Healthy, Overloaded, Offline, Repairing }

    [Serializable]
    public sealed class NetworkNode
    {
        public int Id;
        public Vector2 Position;
        public float Demand;
        public float Capacity;
        public float Load;
        public bool Failed;
        public float RepairRemaining; // seconds left while repairing
        public NodeStatus Status
        {
            get
            {
                if (Failed) return RepairRemaining > 0f ? NodeStatus.Repairing : NodeStatus.Offline;
                return Load > Capacity ? NodeStatus.Overloaded : NodeStatus.Healthy;
            }
        }
    }

    [Serializable]
    public sealed class NetworkLink
    {
        public int A;
        public int B;
        public float Capacity;
        public float Load;
        public bool Failed;
        public float RepairRemaining;
        /// <summary>Directional flow for visualization: positive = A	o B, negative = B	o A.</summary>
        public float FlowAB;
    }

    public sealed class NetworkSimulation
    {
        public readonly List<NetworkNode> Nodes = new List<NetworkNode>();
        public readonly List<NetworkLink> Links = new List<NetworkLink>();

        private readonly System.Random random;
        private readonly Dictionary<(int, int), int> linkIndex = new Dictionary<(int, int), int>();

        public int TickCount { get; private set; }
        public int Money { get; private set; } = 120;
        public float Availability { get; private set; } = 1f;
        public float DroppedTraffic { get; private set; }
        public int FailedNodes => Nodes.FindAll(n => n.Failed).Count;
        public int FailedLinks => Links.FindAll(l => l.Failed).Count;

        // Progression / objectives
        public int Score { get; private set; }
        public int Level { get; private set; } = 1;
        public float ObjectiveTimer { get; private set; } = 90f; // seconds to hold target availability
        public float ObjectiveProgress { get; private set; }
        public float TargetAvailability { get; private set; } = 0.82f;
        public bool ObjectiveComplete { get; private set; }
        public bool GameOver { get; private set; }

        public const int NodeRepairCost = 18;
        public const int LinkRepairCost = 12;
        public const float NodeRepairSeconds = 8f;
        public const float LinkRepairSeconds = 5f;

        public NetworkSimulation(int seed = 2026, int level = 1)
        {
            random = new System.Random(seed + level * 97);
            Level = Mathf.Max(1, level);
            TargetAvailability = Mathf.Clamp(0.78f + Level * 0.02f, 0.78f, 0.94f);
            ObjectiveTimer = 70f + Level * 12f;
            BuildTopology();
            Step();
        }

        private void BuildTopology()
        {
            Nodes.Clear();
            Links.Clear();
            linkIndex.Clear();

            int nodeCount = 14 + Mathf.Min(Level, 8);
            for (int i = 0; i < nodeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / nodeCount;
                float radius = (i % 3 == 0) ? 6.6f : (i % 2 == 0 ? 5.1f : 3.9f);
                float demand = 4 + random.Next(4 + Level * 2);
                float capacity = 28 + random.Next(12) + Level;
                Nodes.Add(new NetworkNode
                {
                    Id = i,
                    Position = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
                    Demand = demand,
                    Capacity = capacity
                });
            }

            // Ring
            for (int i = 0; i < Nodes.Count; i++)
                AddLink(i, (i + 1) % Nodes.Count, 40 + Level * 3);

            // Diameters / chords
            int chords = 6 + Level;
            for (int i = 0; i < chords; i++)
            {
                int a = i % Nodes.Count;
                int b = (i + Nodes.Count / 2 + (i % 3)) % Nodes.Count;
                if (a != b) AddLink(a, b, 55 + Level * 4);
            }

            // Hub (node 0) gets extra capacity
            Nodes[0].Capacity += 25 + Level * 5;
            Nodes[0].Demand = 0; // sink / core
        }

        private void AddLink(int a, int b, float capacity)
        {
            if (a == b) return;
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            if (linkIndex.ContainsKey((lo, hi))) return;
            linkIndex[(lo, hi)] = Links.Count;
            Links.Add(new NetworkLink { A = a, B = b, Capacity = capacity });
        }

        public int FindLinkIndex(int a, int b)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            return linkIndex.TryGetValue((lo, hi), out int idx) ? idx : -1;
        }

        /// <summary>
        /// Advance simulation by one discrete tick (routing + economy).
        /// Call from game loop every ~1.5–2s. Also advances repair timers when dt is provided.
        /// </summary>
        public void Step(float dt = 0f)
        {
            if (GameOver) return;

            TickCount++;

            // Advance repairs
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

            // Reset loads & directional flow
            foreach (var node in Nodes) node.Load = 0f;
            foreach (var link in Links)
            {
                link.Load = 0f;
                link.FlowAB = 0f;
            }

            float delivered = 0f, requested = 0f;

            // Process sources in random order each tick to remove order bias
            var order = new List<int>();
            for (int i = 1; i < Nodes.Count; i++) order.Add(i);
            Shuffle(order);

            foreach (int source in order)
            {
                var sender = Nodes[source];
                requested += sender.Demand;
                if (sender.Failed || Nodes[0].Failed) continue;

                var path = FindPath(source, 0);
                if (path == null || path.Count == 0) continue;

                float traffic = sender.Demand;
                bool capable = true;
                foreach (int index in path)
                {
                    if (Links[index].Failed || Links[index].Load + traffic > Links[index].Capacity)
                    {
                        capable = false;
                        break;
                    }
                }
                if (!capable) continue;

                // Commit traffic and directional flow
                sender.Load += traffic;
                int prev = source;
                foreach (int index in path)
                {
                    var link = Links[index];
                    link.Load += traffic;
                    // Determine direction
                    if (link.A == prev)
                    {
                        link.FlowAB += traffic;
                        prev = link.B;
                    }
                    else
                    {
                        link.FlowAB -= traffic;
                        prev = link.A;
                    }
                }
                Nodes[0].Load += traffic;
                delivered += traffic;
            }

            Availability = requested <= 0f ? 1f : delivered / requested;
            DroppedTraffic = Mathf.Max(0f, requested - delivered);

            // Economy: income from delivered traffic, small upkeep
            int income = Mathf.RoundToInt(delivered / 40f);
            int upkeep = 1 + FailedNodes + FailedLinks;
            Money += income - upkeep;
            if (Money < 0) Money = 0;

            // Score: delivered + availability bonus
            Score += Mathf.RoundToInt(delivered * 0.4f + Availability * 8f);

            // Objective tracking
            if (!ObjectiveComplete)
            {
                if (Availability >= TargetAvailability)
                    ObjectiveProgress += dt > 0f ? dt : 2f;
                else
                    ObjectiveProgress = Mathf.Max(0f, ObjectiveProgress - (dt > 0f ? dt * 0.5f : 1f));

                if (ObjectiveProgress >= ObjectiveTimer)
                {
                    ObjectiveComplete = true;
                    Money += 40 + Level * 15;
                    Score += 200 * Level;
                }
            }

            // Soft fail: core down or availability collapses for too long
            if (Nodes[0].Failed || (Availability < 0.25f && TickCount > 15))
                GameOver = true;
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private List<int> FindPath(int start, int goal)
        {
            int n = Nodes.Count;
            var visited = new bool[n];
            var parentNode = new int[n];
            var parentLink = new int[n];
            for (int i = 0; i < n; i++) parentNode[i] = -1;

            var q = new Queue<int>();
            q.Enqueue(start);
            visited[start] = true;

            while (q.Count > 0)
            {
                int current = q.Dequeue();
                if (current == goal) break;

                for (int i = 0; i < Links.Count; i++)
                {
                    var link = Links[i];
                    if (link.Failed || link.Load >= link.Capacity) continue;

                    int next = link.A == current ? link.B : (link.B == current ? link.A : -1);
                    if (next < 0 || visited[next] || Nodes[next].Failed) continue;

                    parentNode[next] = current;
                    parentLink[next] = i;
                    visited[next] = true;
                    q.Enqueue(next);
                }
            }

            if (!visited[goal]) return null;

            var result = new List<int>();
            for (int current = goal; current != start; current = parentNode[current])
                result.Add(parentLink[current]);
            result.Reverse();
            return result;
        }

        public bool StartNodeRepair(int id)
        {
            if (id < 0 || id >= Nodes.Count) return false;
            var node = Nodes[id];
            if (!node.Failed || node.RepairRemaining > 0f) return false;
            if (Money < NodeRepairCost) return false;

            Money -= NodeRepairCost;
            node.RepairRemaining = NodeRepairSeconds;
            return true;
        }

        public bool ForceNodeFailure(int id)
        {
            if (id < 0 || id >= Nodes.Count || id == 0) return false; // protect core from manual kill in early levels
            if (Nodes[id].Failed) return false;
            Nodes[id].Failed = true;
            Nodes[id].RepairRemaining = 0f;
            Step();
            return true;
        }

        public bool StartLinkRepair(int linkId)
        {
            if (linkId < 0 || linkId >= Links.Count) return false;
            var link = Links[linkId];
            if (!link.Failed || link.RepairRemaining > 0f) return false;
            if (Money < LinkRepairCost) return false;

            Money -= LinkRepairCost;
            link.RepairRemaining = LinkRepairSeconds;
            return true;
        }

        public bool ForceLinkFailure(int linkId)
        {
            if (linkId < 0 || linkId >= Links.Count) return false;
            if (Links[linkId].Failed) return false;
            Links[linkId].Failed = true;
            Links[linkId].RepairRemaining = 0f;
            Step();
            return true;
        }

        public void RandomFailure()
        {
            // Prefer nodes, occasionally links
            if (random.NextDouble() < 0.7 || Links.Count == 0)
            {
                var operational = Nodes.FindAll(n => !n.Failed && n.Id != 0);
                if (operational.Count == 0) return;
                operational[random.Next(operational.Count)].Failed = true;
            }
            else
            {
                var operational = Links.FindAll(l => !l.Failed);
                if (operational.Count == 0) return;
                operational[random.Next(operational.Count)].Failed = true;
            }
            Step();
        }

        public void AdvanceToNextLevel()
        {
            if (!ObjectiveComplete) return;
            Level++;
            Score += 100 * Level;
            Money += 30;
            ObjectiveComplete = false;
            ObjectiveProgress = 0f;
            TargetAvailability = Mathf.Clamp(0.78f + Level * 0.02f, 0.78f, 0.94f);
            ObjectiveTimer = 70f + Level * 12f;
            GameOver = false;
            TickCount = 0;
            BuildTopology();
            Step();
        }
    }
}
