using System.Collections.Generic;
using UnityEngine;

namespace OutwardDynasty
{
    // This is a BOOTSTRAPPER only.
    // It seeds default factions/towns into the dynasty save if missing.
    public class FactionsLibrary : MonoBehaviour
    {
        public static FactionsLibrary Instance;

        private void Awake()
        {
            Instance = this;
        }

        public void EnsureDefaults(DynastySaveData data)
        {
            if (data == null) return;

            if (data.Factions == null)
                data.Factions = new List<FactionData>();

            if (data.Towns == null)
                data.Towns = new List<TownData>();

            // Seed factions if empty
            if (data.Factions.Count == 0)
            {
                Debug.Log("[Dynasty] Seeding default factions...");

                data.Factions.Add(new FactionData("Blue Chamber") { EconomyScore = 60, Population = 500, Aggression = 30, Immigration = 20 });
                data.Factions.Add(new FactionData("Heroic Kingdom") { EconomyScore = 55, Population = 450, Aggression = 45, Immigration = 15 });
                data.Factions.Add(new FactionData("Holy Mission") { EconomyScore = 50, Population = 420, Aggression = 35, Immigration = 25 });
                data.Factions.Add(new FactionData("Sorobor Academy") { EconomyScore = 70, Population = 300, Aggression = 20, Immigration = 10 });
            }

            // Seed towns if empty
            if (data.Towns.Count == 0)
            {
                Debug.Log("[Dynasty] Seeding default towns...");

                data.Towns.Add(new TownData { TownID = "Cierzo", OwnerFaction = "Blue Chamber", GateHP = 1000f, EntryTax = 0 });
                data.Towns.Add(new TownData { TownID = "Berg", OwnerFaction = "Blue Chamber", GateHP = 1000f, EntryTax = 0 });
                data.Towns.Add(new TownData { TownID = "Levant", OwnerFaction = "Heroic Kingdom", GateHP = 1000f, EntryTax = 0 });
                data.Towns.Add(new TownData { TownID = "Monsoon", OwnerFaction = "Holy Mission", GateHP = 1000f, EntryTax = 0 });
            }
        }
    }
}
