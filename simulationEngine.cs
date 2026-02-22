using System;
using UnityEngine;

namespace OutwardDynasty
{
    public static class SimulationEngine
    {
        // Runs on the DYNASTY SAVE DATA (not runtime objects)
        // Called by host-authority tick only.
        public static void ProcessWorldTick(DynastySaveData data)
        {
            if (data == null) return;

            // Ensure seeded defaults so sim never NREs on new/old saves
            FactionsLibrary.EnsureSeeded(data);

            // 1) Advance day counter
            data.DayCount++;

            // 2) Inflation drifts with scarcity + war + bankruptcies (numeric only; labels in UI)
            float scarcityAvg = 0f;
            if (data.Regions != null && data.Regions.Count > 0)
            {
                for (int i = 0; i < data.Regions.Count; i++)
                    scarcityAvg += Mathf.Max(0f, data.Regions[i]?.Scarcity ?? 0f);
                scarcityAvg /= Mathf.Max(1, data.Regions.Count);
            }

            int activeWars = 0;
            if (data.Factions != null)
                for (int i = 0; i < data.Factions.Count; i++)
                    activeWars += data.Factions[i]?.ActiveWars?.Count ?? 0;

            float warPressure = Mathf.Clamp(activeWars / 4f, 0f, 3f);
            float bankruptPressure = 0f;
            if (data.Factions != null)
                for (int i = 0; i < data.Factions.Count; i++)
                    if (data.Factions[i] != null && data.Factions[i].Bankrupt) bankruptPressure += 0.25f;

            data.InflationIndex = Mathf.Clamp(
                data.InflationIndex + 0.01f * scarcityAvg + 0.005f * warPressure + bankruptPressure * 0.01f,
                0.5f, 10f);

            // 3) Region scarcity & population pressure
            SimulateRegions(data);

            // 4) Faction economy / bankruptcy
            SimulateFactions(data);

            // 5) Wars (probabilistic, seeded)
            SimulateWars(data);

            // 6) Trade caravans
            SimulateCaravans(data);

            // 7) Trogs special target selection
            SimulateTrogs(data);

            // Dynasty extensions: NPC data-mode + quest/arc progression + population rollups
            SimulateNpcsAndQuests(data);
            ComputeFactionPopulation(data);
        }

        private static System.Random Rng(DynastySaveData data)
        {
            // deterministic: seed + day
            int seed = data.WorldSeed ^ (data.DayCount * 73856093);
            return new System.Random(seed);
        }

        private static void SimulateRegions(DynastySaveData data)
        {
            if (data.Regions == null) return;
            var rng = Rng(data);

            for (int i = 0; i < data.Regions.Count; i++)
            {
                var r = data.Regions[i];
                if (r == null) continue;

                // Reset caravan count; caravans will re-add during SimulateCaravans
                r.CaravanCount = 0;

                // Scarcity drifts upward if population high and trade disrupted; downward if stable
                float popPressure = Mathf.Clamp(r.Population / 500f, 0f, 2f);
                float delta = (0.02f * popPressure) - 0.01f;
                // random shocks
                if (rng.NextDouble() < 0.05) delta += 0.05f;
                r.Scarcity = Mathf.Clamp(r.Scarcity + delta, 0f, 2f);

                // Scarcity kills NPCs and causes migration attempts
                if (r.Scarcity > 1.0f)
                {
                    int deaths = Mathf.Clamp((int)(r.Population * 0.02f * (r.Scarcity - 1f)), 0, 30);
                    r.Population = Mathf.Max(0, r.Population - deaths);
                    r.MigrationPressure = Mathf.Clamp(r.MigrationPressure + 0.1f * (r.Scarcity - 1f), 0f, 3f);
                }
                else
                {
                    r.MigrationPressure = Mathf.Max(0f, r.MigrationPressure - 0.05f);
                }
            }

            // Migration resolution: push from high pressure to lowest scarcity region
            for (int i = 0; i < data.Regions.Count; i++)
            {
                var from = data.Regions[i];
                if (from == null) continue;
                if (from.MigrationPressure < 1f) continue;
                int movers = Mathf.Clamp((int)(from.Population * 0.01f * from.MigrationPressure), 0, 25);
                if (movers <= 0) continue;

                RegionData best = null;
                float bestScore = float.MaxValue;
                for (int j = 0; j < data.Regions.Count; j++)
                {
                    var to = data.Regions[j];
                    if (to == null) continue;
                    if (to.RegionId == "DreamWorld") continue;
                    float score = to.Scarcity + Mathf.Clamp(to.Population / 1000f, 0f, 1f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = to;
                    }
                }

                if (best != null && best != from)
                {
                    from.Population = Mathf.Max(0, from.Population - movers);
                    best.Population += movers;
                    from.MigrationPressure = Mathf.Max(0f, from.MigrationPressure - 0.25f);
                }
            }
        }

        private static void SimulateFactions(DynastySaveData data)
        {
            if (data.Factions == null) return;
            var rng = Rng(data);

            for (int i = 0; i < data.Factions.Count; i++)
            {
                var f = data.Factions[i];
                if (f == null) continue;

                // Wars cost money
                float upkeep = 25f * (f.ActiveWars?.Count ?? 0);
                // Trade profits add money (caravans handle detail; keep base income)
                float income = 20f;

                // Bankrupt factions have reduced income and may collapse
                if (f.Bankrupt) income *= 0.5f;

                f.Treasury += income - upkeep;

                if (!f.Bankrupt && f.Treasury < -200f)
                {
                    f.Bankrupt = true;
                    Debug.Log("[Dynasty] FACTION BANKRUPT: " + f.Name);
                }

                // Recovery chance
                if (f.Bankrupt && f.Treasury > 200f && rng.NextDouble() < 0.10)
                {
                    f.Bankrupt = false;
                    Debug.Log("[Dynasty] FACTION RECOVERED: " + f.Name);
                }
            }
        }

        private static void SimulateWars(DynastySaveData data)
        {
            if (data.Factions == null) return;
            var rng = Rng(data);

            // Multi-war: each day, each faction may start a new war with low probability
            for (int i = 0; i < data.Factions.Count; i++)
            {
                var a = data.Factions[i];
                if (a == null) continue;
                if (a.Name == "Bandits") continue;

                // War probability increases if bankrupt or high scarcity globally
                double p = 0.02;
                if (a.Bankrupt) p += 0.02;
                if (data.InflationIndex > 2f) p += 0.01;

                if (rng.NextDouble() < p)
                {
                    // pick random enemy != self
                    var b = data.Factions[rng.Next(0, data.Factions.Count)];
                    if (b == null || b == a) continue;

                    if (a.ActiveWars == null) a.ActiveWars = new System.Collections.Generic.List<WarData>();
                    bool already = false;
                    for (int w = 0; w < a.ActiveWars.Count; w++)
                        if (a.ActiveWars[w] != null && a.ActiveWars[w].EnemyFaction == b.Name && a.ActiveWars[w].Status == "War")
                            already = true;
                    if (already) continue;

                    a.ActiveWars.Add(new WarData { EnemyFaction = b.Name, StartDay = data.DayCount, Status = "War" });
                }
            }

            // Resolve old wars
            for (int i = 0; i < data.Factions.Count; i++)
            {
                var f = data.Factions[i];
                if (f?.ActiveWars == null) continue;
                for (int w = f.ActiveWars.Count - 1; w >= 0; w--)
                {
                    var war = f.ActiveWars[w];
                    if (war == null) continue;
                    int age = data.DayCount - war.StartDay;
                    if (age < 10) continue;

                    // chance to end
                    double endP = 0.03 + (age * 0.002);
                    if (rng.NextDouble() < endP)
                        war.Status = "Peace";
                }
            }
        }

        private static void SimulateCaravans(DynastySaveData data)
        {
            if (data.Caravans == null) return;

            // Count current occupancy by region
            for (int i = 0; i < data.Caravans.Count; i++)
            {
                var c = data.Caravans[i];
                if (c == null) continue;
                var r = data.Regions.Find(x => x != null && x.RegionId == c.CurrentRegion);
                if (r != null) r.CaravanCount++;
            }

            for (int i = 0; i < data.Caravans.Count; i++)
            {
                var c = data.Caravans[i];
                if (c == null) continue;

                if (c.Route == null || c.Route.Count == 0)
                    c.Route = TradeRoutes.BuildRoute(c.CurrentRegion, c.DestinationRegion);

                // Travel progress
                if (c.DaysUntilArrival > 0)
                {
                    c.DaysUntilArrival--;
                    continue;
                }

                // Advance along route
                if (c.RouteIndex < 0) c.RouteIndex = 0;
                if (c.RouteIndex >= c.Route.Count) c.RouteIndex = c.Route.Count - 1;

                // At destination? flip route back to origin (trade loop)
                if (c.CurrentRegion == c.DestinationRegion)
                {
                    // profit and set new destination: choose a town that misses a resource
                    c.Profit += 50;
                    var old = c.DestinationRegion;
                    c.DestinationRegion = (old == "Chersonese") ? "Abrassar" : "Chersonese";
                    c.Route = TradeRoutes.BuildRoute(c.CurrentRegion, c.DestinationRegion);
                    c.RouteIndex = 0;
                }

                // Next hop
                string next = null;
                if (c.Route != null && c.RouteIndex + 1 < c.Route.Count)
                    next = c.Route[c.RouteIndex + 1];
                else
                    next = c.DestinationRegion;

                if (!TradeRoutes.CanEnterRegion(data, next))
                {
                    // wait (route congestion)
                    c.DaysUntilArrival = 1;
                    continue;
                }

                // move
                var curR = data.Regions.Find(x => x != null && x.RegionId == c.CurrentRegion);
                if (curR != null) curR.CaravanCount = Mathf.Max(0, curR.CaravanCount - 1);

                c.CurrentRegion = next;
                c.RouteIndex++;

                var newR = data.Regions.Find(x => x != null && x.RegionId == c.CurrentRegion);
                if (newR != null) newR.CaravanCount++;

                c.DaysUntilArrival = 1; // 1 day per hop (coarse)
            }
        }

        private static void SimulateTrogs(DynastySaveData data)
        {
            if (data.Factions == null || data.Regions == null) return;

            var trog = data.Factions.Find(x => x != null && x.IsTrogFaction);
            if (trog == null) return;

            // Trogs famine increases slowly; resets when they take a place
            trog.TrogFamineStat = Mathf.Clamp(trog.TrogFamineStat + 0.02f, 0f, 2f);

            if (trog.TrogFamineStat < 1.0f) return;

            // Choose lowest population target owned by any major/minor faction (not DreamWorld)
            RegionData target = null;
            int bestPop = int.MaxValue;
            for (int i = 0; i < data.Regions.Count; i++)
            {
                var r = data.Regions[i];
                if (r == null) continue;
                if (r.RegionId == "DreamWorld") continue;
                if (r.Population <= 0) continue;

                if (r.Population < bestPop)
                {
                    bestPop = r.Population;
                    target = r;
                }
            }

            if (target != null)
            {
                // Taking a place reduces target population and resets famine regen until next take
                int raidLoss = Mathf.Clamp((int)(target.Population * 0.05f), 0, 40);
                target.Population = Mathf.Max(0, target.Population - raidLoss);
                target.Scarcity = Mathf.Clamp(target.Scarcity + 0.25f, 0f, 2f);
                trog.TrogFamineStat = 0f;
            }
        }

        private static void ComputeFactionPopulation(DynastySaveData data)
        {
            if (data.Factions == null) return;

            // reset
            for (int i = 0; i < data.Factions.Count; i++)
            {
                var f = data.Factions[i];
                if (f != null) f.Population = 0;
            }

            // sum from towns
            if (data.Towns != null)
            {
                for (int i = 0; i < data.Towns.Count; i++)
                {
                    var t = data.Towns[i];
                    if (t == null) continue;

                    string owner = string.IsNullOrEmpty(t.OwnerFaction) ? "NONE" : t.OwnerFaction;
                    if (owner == "NONE") continue;

                    var f = FindFaction(data, owner);
                    if (f != null) f.Population += Mathf.Max(0, t.Population);
                }
            }
        }

        private static FactionData FindFaction(DynastySaveData data, string name)
        {
            if (data.Factions == null) return null;
            for (int i = 0; i < data.Factions.Count; i++)
            {
                var f = data.Factions[i];
                if (f == null) continue;
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
            return null;
        }

        private static void SimulateNpcsAndQuests(DynastySaveData data)
        {
            if (data.Npcs == null) return;
            var rng = Rng(data);

            // NPC task progression (30-min tick)
            for (int i = 0; i < data.Npcs.Count; i++)
            {
                var n = data.Npcs[i];
                if (n == null || !n.IsAlive) continue;

                // simple drift
                n.Fear = Mathf.Clamp(n.Fear + (float)(rng.NextDouble() * 0.5 - 0.25), 0f, 100f);

                float speed = 0.02f; // ~50 ticks to complete
                if (n.Task == NpcTaskType.Patrol) speed = 0.03f;
                if (n.Task == NpcTaskType.TradeRoute) speed = 0.025f;
                if (n.Task == NpcTaskType.Raid) speed = 0.04f;

                if (n.Task == NpcTaskType.Idle) continue;

                n.TaskProgress01 = Mathf.Clamp01(n.TaskProgress01 + speed);

                if (n.TaskProgress01 >= 1f)
                {
                    ResolveNpcTask(data, n);
                    n.TaskProgress01 = 0f;
                    n.Task = NpcTaskType.Idle;
                }
            }

            // Quest progression (data-mode)
            if (data.DynastyQuests != null)
            {
                for (int i = 0; i < data.DynastyQuests.Count; i++)
                {
                    var q = data.DynastyQuests[i];
                    if (q == null || q.Status != DynastyQuestStatus.Active) continue;

                    // Escort progresses automatically in data mode
                    if (string.Equals(q.QuestId, "P_ESCORT", StringComparison.OrdinalIgnoreCase))
                    {
                        q.Progress01 = Mathf.Clamp01(q.Progress01 + 0.05f);
                        if (q.Progress01 >= 1f)
                        {
                            q.Status = DynastyQuestStatus.Completed;
                            ApplyQuestReward(data, q);
                        }
                    }
                }
            }

            // Arc progression: rule-based, no siege needed
            if (data.DynastyArcs != null)
            {
                for (int i = 0; i < data.DynastyArcs.Count; i++)
                {
                    var a = data.DynastyArcs[i];
                    if (a == null || a.Status != DynastyQuestStatus.Active) continue;

                    if (string.Equals(a.ArcId, "A_FOUNDING", StringComparison.OrdinalIgnoreCase))
                        ProgressFoundingArc(data, a);

                    if (string.Equals(a.ArcId, "A_TRADE_ROUTE", StringComparison.OrdinalIgnoreCase))
                        ProgressTradeRouteArc(data, a);

                    if (string.Equals(a.ArcId, "A_FACTION_PRESSURE", StringComparison.OrdinalIgnoreCase))
                        ProgressFactionPressureArc(data, a);
                }
            }
        }

        private static void ResolveNpcTask(DynastySaveData data, NpcSimData n)
        {
            // deterministic-ish per npc
            int seed = (n.TaskSeed ^ data.WorldSeed ^ (data.DayCount * 31));
            var r = new System.Random(seed);

            // success chance from adventure + loyalty - fear
            float score = (n.Adventure * 0.6f) + (n.Loyalty * 0.3f) - (n.Fear * 0.4f);
            float pSuccess = Mathf.Clamp01((score - 10f) / 100f);

            bool success = r.NextDouble() < pSuccess;

            // Failure can mean death (per your rule)
            if (!success)
            {
                // harsher for raids/patrols
                float deathChance = (n.Task == NpcTaskType.Raid) ? 0.35f : 0.15f;
                if (r.NextDouble() < deathChance)
                {
                    n.IsAlive = false;
                    return;
                }

                // otherwise fear rises and disposition drops a bit
                n.Fear = Mathf.Clamp(n.Fear + 10f, 0f, 100f);
                n.Disposition = Mathf.Clamp(n.Disposition - 4f, 0f, 100f);
                return;
            }

            // Success: small gains
            n.Wealth = Mathf.Clamp(n.Wealth + 2f, 0f, 100f);
            n.Influence = Mathf.Clamp(n.Influence + 1f, 0f, 100f);
            n.Fear = Mathf.Clamp(n.Fear - 3f, 0f, 100f);
        }

        private static void ApplyQuestReward(DynastySaveData data, DynastyQuestState q)
        {
            // Pure data rewards: economy/population changes.
            // Inventory rewards should be executed via runtime executors later.
            if (q == null) return;

            if (string.Equals(q.QuestId, "T_SUPPLY", StringComparison.OrdinalIgnoreCase))
            {
                var town = data.Towns != null && data.Towns.Count > 0 ? data.Towns[0] : null;
                if (town != null)
                {
                    town.EconomyScore = Mathf.Clamp(town.EconomyScore + 5, 0, 100);
                    town.Population = Mathf.Clamp(town.Population + 10, 50, 1200);
                }
            }
        }

        private static void ProgressFoundingArc(DynastySaveData data, DynastyArcState a)
        {
            if (a.Stage == 0)
            {
                // Pick home town later via dialogue; for now move to stage 1 once dynasty started.
                a.Stage = 1;
                a.Flags.Add("HOME_TOWN_PENDING");
                return;
            }

            if (a.Stage == 1)
            {
                // When player recruits at least one contact, complete founding.
                bool hasContact = false;
                if (data.Npcs != null)
                {
                    for (int i = 0; i < data.Npcs.Count; i++)
                    {
                        var n = data.Npcs[i];
                        if (n != null && n.IsAlive && n.IsContact) { hasContact = true; break; }
                    }
                }

                if (hasContact)
                {
                    a.Stage = 2;
                    a.Status = DynastyQuestStatus.Completed;
                }
            }
        }

        private static void ProgressTradeRouteArc(DynastySaveData data, DynastyArcState a)
        {
            if (a.Stage == 0)
            {
                a.Stage = 1;
                a.Flags.Add("ROUTE_PENDING");
                return;
            }

            if (a.Stage == 1)
            {
                // If caravans exist and have moved, complete.
                if (data.Caravans != null && data.Caravans.Count > 0)
                {
                    a.Stage = 2;
                    a.Status = DynastyQuestStatus.Completed;
                }
            }
        }

        private static void ProgressFactionPressureArc(DynastySaveData data, DynastyArcState a)
        {
            if (a.Stage == 0)
            {
                a.Stage = 1;
                a.Flags.Add("STANCE_PENDING");
                return;
            }

            if (a.Stage == 1)
            {
                // once at least one war exists, consider the pressure arc complete for v1
                bool anyWar = false;
                if (data.Factions != null)
                {
                    for (int i = 0; i < data.Factions.Count; i++)
                    {
                        var f = data.Factions[i];
                        if (f != null && f.ActiveWars != null && f.ActiveWars.Count > 0) { anyWar = true; break; }
                    }
                }
                if (anyWar)
                {
                    a.Stage = 2;
                    a.Status = DynastyQuestStatus.Completed;
                }
            }
        }
    }
}
