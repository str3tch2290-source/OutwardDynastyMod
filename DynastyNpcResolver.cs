
using System;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastyNpcResolver
    {
        public static NpcSimData ResolveOrCreate(DynastySaveData data, Character npcCharacter)
        {
            if (data == null) return null;
            if (data.Npcs == null) data.Npcs = new System.Collections.Generic.List<NpcSimData>();

            string town = DynastyWorldContext.GetCurrentTownName();
            string name = SafeNpcName(npcCharacter);

            // Try match by display name + current town
            for (int i = 0; i < data.Npcs.Count; i++)
            {
                var n = data.Npcs[i];
                if (n == null || !n.IsAlive) continue;
                if (string.Equals(n.DisplayName, name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(n.CurrentTown, town, StringComparison.OrdinalIgnoreCase))
                    return n;
            }

            // Create deterministic-ish record
            var rng = new System.Random(data.WorldSeed ^ name.GetHashCode() ^ town.GetHashCode());
            var npc = new NpcSimData
            {
                NpcId = "NPC_RENDER_" + Guid.NewGuid().ToString("N").Substring(0, 10),
                DisplayName = string.IsNullOrEmpty(name) ? DynastyNameGen.Generate(rng) : name,
                HomeTown = town,
                CurrentTown = town,
                Faction = DynastyWorldContext.GetTownOwnerFaction(data, town),

                Disposition = 45f + (float)rng.NextDouble() * 15f,
                Adventure = (float)rng.NextDouble() * 100f,
                Loyalty = (float)rng.NextDouble() * 100f,
                Wealth = (float)rng.NextDouble() * 100f,
                Influence = (float)rng.NextDouble() * 30f,
                Fear = (float)rng.NextDouble() * 30f,

                Role = NpcRole.Ambient,
                Task = NpcTaskType.Idle,
                TaskSeed = rng.Next(),
                IsAlive = true
            };

            data.Npcs.Add(npc);
            return npc;
        }

        private static string SafeNpcName(Character c)
        {
            try
            {
                // Use transform/name as fallback. In Outward, Character.Name may exist but we stay safe.
                return (c != null ? c.name : "") ?? "";
            }
            catch { return ""; }
        }
    }
}
