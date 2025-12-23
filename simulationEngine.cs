using UnityEngine;

namespace OutwardDynasty
{
    public static class SimulationEngine
    {
        // Runs on the DYNASTY SAVE DATA (not runtime objects)
        public static void ProcessWorldTick(DynastySaveData data)
        {
            if (data == null) return;
            if (data.Factions == null) return;

            for (int i = 0; i < data.Factions.Count; i++)
            {
                FactionData faction = data.Factions[i];
                if (faction == null) continue;

                // 1) POPULATION & ECON LOGIC
                float growth = faction.Immigration / 1000f;
                int delta = Mathf.Max(1, (int)(faction.Population * growth));
                faction.Population += delta;

                // small economy drift with growth
                faction.EconomyScore = Mathf.Clamp(faction.EconomyScore + (delta / 1000), 0, 100);

                // 2) AI VOLITION: DECIDE WAR STATUS
                if (faction.Aggression > 70 && faction.EconomyScore < 40)
                {
                    if (faction.WarStatus == "Peace")
                    {
                        faction.WarStatus = "Mobilizing";
                        Debug.Log("[Dynasty] " + faction.Name + " is mobilizing due to resource scarcity!");
                    }
                    else if (faction.WarStatus == "Mobilizing")
                    {
                        faction.WarStatus = "At War";
                        Debug.Log("[Dynasty] " + faction.Name + " has entered WAR.");
                        // Spawns happen elsewhere (executor systems), not here.
                    }
                }

                // 3) AI VOLITION: NOTICE PLAYER HELP
                if (faction.PlayerSupport > 50f && faction.WarStatus == "Mobilizing")
                {
                    faction.WarStatus = "Peace";
                    faction.PlayerSupport = Mathf.Max(0f, faction.PlayerSupport - 10f);
                    Debug.Log("[Dynasty] " + faction.Name + " cancelled mobilization due to player diplomatic aid.");
                }
            }
        }
    }
}
