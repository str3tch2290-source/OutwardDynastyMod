using System.Collections.Generic;

namespace OutwardDynasty
{
    // Very small routing helper. Later: load from external map files.
    internal static class TradeRoutes
    {
        // Adjacency list (future mods can extend cleanly by patching this at runtime)
        private static readonly Dictionary<string, List<string>> Adj = new Dictionary<string, List<string>>
        {
            ["Chersonese"] = new List<string> { "Enmerkar", "Abrassar", "HallowedMarsh" },
            ["Enmerkar"] = new List<string> { "Chersonese", "Abrassar", "AntiquePlateau" },
            ["HallowedMarsh"] = new List<string> { "Chersonese", "Abrassar" },
            ["Abrassar"] = new List<string> { "Chersonese", "Enmerkar", "HallowedMarsh", "AntiquePlateau" },
            ["AntiquePlateau"] = new List<string> { "Enmerkar", "Abrassar", "Caldera" },
            ["Caldera"] = new List<string> { "AntiquePlateau" },
            ["DreamWorld"] = new List<string>() // null layer
        };

        public static List<string> BuildRoute(string start, string dest)
        {
            // Simple BFS for shortest path. If unknown, return direct.
            if (start == dest) return new List<string> { start };
            if (!Adj.ContainsKey(start) || !Adj.ContainsKey(dest))
                return new List<string> { start, dest };

            var q = new Queue<string>();
            var prev = new Dictionary<string, string>();
            q.Enqueue(start);
            prev[start] = null;

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == dest) break;
                foreach (var n in Adj[cur])
                {
                    if (prev.ContainsKey(n)) continue;
                    prev[n] = cur;
                    q.Enqueue(n);
                }
            }

            if (!prev.ContainsKey(dest))
                return new List<string> { start, dest };

            var path = new List<string>();
            var node = dest;
            while (node != null)
            {
                path.Add(node);
                node = prev[node];
            }
            path.Reverse();
            return path;
        }

        public static bool CanEnterRegion(DynastySaveData data, string regionId)
        {
            // Constraint: no more than 2 caravans in the same region at the same time.
            if (data == null || data.Regions == null) return true;
            var r = data.Regions.Find(x => x != null && x.RegionId == regionId);
            if (r == null) return true;
            return r.CaravanCount < 2;
        }
    }
}
