// ===============================
// WorldSimulation.cs (REPLACEMENT)
// PURE DATA ONLY (no CharacterManager / reflection)
// ===============================
using UnityEngine;

namespace OutwardDynasty
{
    public static class WorldSimulation
    {
        public static void ProcessDailyUpdate(DynastySaveData data)
        {
            if (data == null) return;

            data.DayCount++;

            // Apocalypse trigger
            if (data.DayCount >= 500 && !data.IsApocalypseActive)
            {
                data.IsApocalypseActive = true;
                data.ScourgeMultiplier = 2.0f;
            }

            // Bandit Logic (data-only)
            if (data.Towns != null && data.Factions != null)
            {
                foreach (var town in data.Towns)
                {
                    if (town == null) continue;

                    if (town.GateHP < 500)
                    {
                        var faction = data.Factions.Find(f => f != null && f.Name == town.OwnerFaction);
                        if (faction != null)
                        {
                            faction.BanditStrength += 5f;
                        }
                    }
                }
            }

            // NOTE:
            // Player penalties are NOT applied here anymore (pure simulation).
            // If you want penalties, do them in a separate runtime executor system.
        }
    }
}
