
using System;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastyQuestRules
    {
        public static bool IsQuestOfferableHere(DynastySaveData data, DynastyQuestState q, NpcSimData npc)
        {
            if (data == null || q == null || npc == null) return false;
            string town = DynastyWorldContext.GetCurrentTownName();

            // Town quests offered by locals in their home town
            if (HasFlag(q, "TOWN"))
            {
                if (!string.Equals(npc.HomeTown, town, StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }

            // Personal quests: offered by high-adventure NPCs, friendlier disposition
            if (HasFlag(q, "PERSONAL"))
            {
                if (npc.Adventure < 35f) return false;
                if (npc.Disposition < 45f) return false;
                return true;
            }

            return true;
        }

        public static void AcceptQuest(DynastySaveData data, DynastyQuestState q, NpcSimData npc)
        {
            if (data == null || q == null || npc == null) return;

            q.Status = DynastyQuestStatus.Active;
            q.StepIndex = 0;
            q.Progress01 = 0f;
            q.GiverNpcId = npc.NpcId;
            q.Seed = (data.WorldSeed ^ q.QuestId.GetHashCode() ^ npc.NpcId.GetHashCode());

            // Some immediate effects: accepting builds relationship
            npc.Disposition = Mathf.Clamp(npc.Disposition + 2f, 0f, 100f);
        }

        private static bool HasFlag(DynastyQuestState q, string flag)
        {
            if (q.Flags == null) return false;
            for (int i = 0; i < q.Flags.Count; i++)
                if (string.Equals(q.Flags[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
