using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    public enum NodeStatus { Healthy, Overloaded, Offline }

    [Serializable]
    public sealed class NetworkNode
    {
        public int Id;
        public Vector2 Position;
        public float Demand;
        public float Capacity;
        public float Load;
        public bool Failed;
        public NodeStatus Status => Failed ? NodeStatus.Offline : Load > Capacity ? NodeStatus.Overloaded : NodeStatus.Healthy;
    }

    [Serializable]
    public sealed class NetworkLink
    {
        public int A;
        public int B;
        public float Capacity;
        public float Load;
        public bool Failed;
    }

    public sealed class NetworkSimulation
    {
        public readonly List<NetworkNode> Nodes = new List<NetworkNode>();
        public readonly List<NetworkLink> Links = new List<NetworkLink>();
        private readonly System.Random random;
        public int TickCount { get; private set; }
        public int Money { get; private set; } = 100;
        public float Availability { get; private set; } = 1f;
        public float DroppedTraffic { get; private set; }
        public int FailedNodes => Nodes.FindAll(n => n.Failed).Count;

        public NetworkSimulation(int seed = 2026)
        {
            random = new System.Random(seed);
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2f / 18;
                float radius = (i % 3 == 0) ? 6.4f : 4.5f;
                Nodes.Add(new NetworkNode { Id = i, Position = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius), Demand = 5 + random.Next(13), Capacity = 35 });
            }
            for (int i = 0; i < Nodes.Count; i++) AddLink(i, (i + 1) % Nodes.Count, 45);
            for (int i = 0; i < 9; i++) AddLink(i, (i + 9) % Nodes.Count, 65);
            Step();
        }

        private void AddLink(int a, int b, float capacity) => Links.Add(new NetworkLink { A = a, B = b, Capacity = capacity });

        // Deterministic demand routing: each active node sends its demand toward node 0 via shortest hop path.
        public void Step()
        {
            TickCount++;
            foreach (var node in Nodes) node.Load = 0f;
            foreach (var link in Links) link.Load = 0f;
            float delivered = 0f, requested = 0f;
            for (int source = 1; source < Nodes.Count; source++)
            {
                var sender = Nodes[source];
                requested += sender.Demand;
                if (sender.Failed || Nodes[0].Failed) continue;
                var path = FindPath(source, 0);
                if (path == null) continue;
                float traffic = sender.Demand;
                bool capable = true;
                foreach (int index in path) if (Links[index].Load + traffic > Links[index].Capacity) capable = false;
                if (!capable) continue;
                sender.Load += traffic;
                foreach (int index in path) Links[index].Load += traffic;
                delivered += traffic;
            }
            Availability = requested <= 0 ? 1 : delivered / requested;
            DroppedTraffic = Mathf.Max(0f, requested - delivered);
            Money += Mathf.RoundToInt(delivered / 50f);
        }

        private List<int> FindPath(int start, int goal)
        {
            var visited = new bool[Nodes.Count];
            var parentNode = new int[Nodes.Count];
            var parentLink = new int[Nodes.Count];
            for (int i = 0; i < parentNode.Length; i++) parentNode[i] = -1;
            var q = new Queue<int>(); q.Enqueue(start); visited[start] = true;
            while (q.Count > 0)
            {
                int current = q.Dequeue();
                if (current == goal) break;
                for (int i = 0; i < Links.Count; i++)
                {
                    var link = Links[i];
                    if (link.Failed || link.Load >= link.Capacity) continue;
                    int next = link.A == current ? link.B : link.B == current ? link.A : -1;
                    if (next < 0 || visited[next] || Nodes[next].Failed) continue;
                    parentNode[next] = current; parentLink[next] = i; visited[next] = true; q.Enqueue(next);
                }
            }
            if (!visited[goal]) return null;
            var result = new List<int>();
            for (int current = goal; current != start; current = parentNode[current]) result.Add(parentLink[current]);
            result.Reverse(); return result;
        }

        public bool ToggleFailure(int id)
        {
            if (id < 0 || id >= Nodes.Count) return false;
            if (Nodes[id].Failed && Money < 15) return false;
            if (Nodes[id].Failed) Money -= 15;
            Nodes[id].Failed = !Nodes[id].Failed;
            Step(); return true;
        }

        public void RandomFailure()
        {
            var operational = Nodes.FindAll(n => !n.Failed && n.Id != 0);
            if (operational.Count == 0) return;
            operational[random.Next(operational.Count)].Failed = true;
            Step();
        }
    }
}
