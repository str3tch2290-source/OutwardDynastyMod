using System;

namespace OutwardDynasty
{
    // Bootstraps default DynastySaveData schema so simulation has something to run on.
    // Safe to call repeatedly; only fills missing lists/records.
    public static class FactionsLibrary
    {
                // Back-compat: older code may call EnsureDefaults
        public static void EnsureDefaults(DynastySaveData data) => EnsureSeeded(data);

public static void EnsureSeeded(DynastySaveData data)
        {
            if (data == null) return;

            if (string.IsNullOrEmpty(data.DynastyId))
                data.DynastyId = Guid.NewGuid().ToString("N");

            if (data.WorldSeed == 0)
                data.WorldSeed = unchecked((int)DateTime.UtcNow.Ticks);

            if (data.Factions == null) data.Factions = new System.Collections.Generic.List<FactionData>();
            if (data.Towns == null) data.Towns = new System.Collections.Generic.List<TownData>();
            if (data.Regions == null) data.Regions = new System.Collections.Generic.List<RegionData>();
            if (data.Caravans == null) data.Caravans = new System.Collections.Generic.List<CaravanData>();

            // Regions (vanilla-ish)
            EnsureRegion(data, "Chersonese");
            EnsureRegion(data, "Enmerkar");
            EnsureRegion(data, "HallowedMarsh");
            EnsureRegion(data, "Abrassar");
            EnsureRegion(data, "AntiquePlateau");
            EnsureRegion(data, "Caldera");
            EnsureRegion(data, "DreamWorld"); // null sandbox hybrid

            // Major factions (minimal economics)
            EnsureFaction(data, "Holy Mission", treasury: 5000);
            EnsureFaction(data, "Blue Chamber", treasury: 5000);
            EnsureFaction(data, "Heroic Kingdom", treasury: 5000);
            EnsureFaction(data, "Sorobor Academy", treasury: 3500);

            // Minor factions
            var trog = EnsureFaction(data, "Troglodytes", treasury: 500);
            trog.IsTrogFaction = true;
            trog.TrogFamineStat = 0.25f;

            EnsureFaction(data, "Bandits", treasury: 800);
            EnsureFaction(data, "Traders", treasury: 1200);

            // Towns (each produces all-but-one resource; missing differs per town)
            EnsureTown(data, "Cierzo", "Chersonese", ResourceType.Ore);
            EnsureTown(data, "Berg", "Enmerkar", ResourceType.Salt);
            EnsureTown(data, "Monsoon", "HallowedMarsh", ResourceType.Wood);
            EnsureTown(data, "Levant", "Abrassar", ResourceType.Water);
            EnsureTown(data, "Harmattan", "AntiquePlateau", ResourceType.Food);
            EnsureTown(data, "New Sirocco", "Caldera", ResourceType.Cloth);

            // Caravans: one per faction (major + Traders)
            EnsureCaravan(data, "Holy Mission", "Chersonese", "HallowedMarsh");
            EnsureCaravan(data, "Blue Chamber", "Enmerkar", "Chersonese");
            EnsureCaravan(data, "Heroic Kingdom", "Abrassar", "Enmerkar");
            EnsureCaravan(data, "Sorobor Academy", "AntiquePlateau", "Caldera");
            EnsureCaravan(data, "Traders", "Chersonese", "Abrassar");
        }

        private static RegionData EnsureRegion(DynastySaveData data, string regionId)
        {
            var r = data.Regions.Find(x => x != null && x.RegionId == regionId);
            if (r == null)
            {
                r = new RegionData(regionId) { Population = 200 };
                data.Regions.Add(r);
            }
            if (r.Stock == null) r.Stock = new System.Collections.Generic.Dictionary<ResourceType, int>();
            return r;
        }

        private static FactionData EnsureFaction(DynastySaveData data, string name, float treasury)
        {
            var f = data.Factions.Find(x => x != null && x.Name == name);
            if (f == null)
            {
                f = new FactionData(name) { Treasury = treasury };
                data.Factions.Add(f);
            }
            if (f.ActiveWars == null) f.ActiveWars = new System.Collections.Generic.List<WarData>();
            return f;
        }

        private static void EnsureTown(DynastySaveData data, string town, string region, ResourceType missing)
        {
            var t = data.Towns.Find(x => x != null && x.TownName == town);
            if (t == null)
            {
                t = new TownData(town, region)
                {
                    Population = 200,
                    EconomyScore = 50,
                    MissingResource = missing
                };
                data.Towns.Add(t);
            }
            if (t.Stock == null) t.Stock = new System.Collections.Generic.Dictionary<ResourceType, int>();
        }

        private static void EnsureCaravan(DynastySaveData data, string faction, string start, string dest)
        {
            var c = data.Caravans.Find(x => x != null && x.FactionName == faction);
            if (c == null)
            {
                c = new CaravanData
                {
                    CaravanId = "car_" + faction.Replace(" ", "_"),
                    FactionName = faction,
                    CurrentRegion = start,
                    DestinationRegion = dest,
                    DaysUntilArrival = 0,
                    Profit = 0
                };
                c.Route = TradeRoutes.BuildRoute(start, dest);
                data.Caravans.Add(c);
            }
            if (c.Route == null) c.Route = TradeRoutes.BuildRoute(start, dest);
        }
    }
}
