
using System;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastyDialogueLines
    {
        public static string GetSmallTalk(DynastySaveData data, NpcSimData npc)
        {
            if (npc == null) return "";

            string town = DynastyWorldContext.GetCurrentTownName();
            string owner = DynastyWorldContext.GetTownOwnerFaction(data, town);

            // Tone varies with disposition
            if (npc.Disposition < 30f)
                return $"{npc.DisplayName} eyes you warily.\n\n\"Careful. Folks don't last long stirring trouble in {town}.\"";

            if (npc.Disposition < 60f)
                return $"\"If you're here to trade, keep it simple. {owner} watches everyone these days.\"";

            if (npc.Disposition < 80f)
                return $"\"I hear the road has been rough. If you need work, ask around. I might know something.\"";

            return $"\"Good to see you. If you're looking for real opportunity, the Dynasty is where the future is.\"";
        }

        public static string GetRumor(DynastySaveData data, string town)
        {
            if (data == null) return "No news.";
            var t = FindTown(data, town);
            if (t == null) return "No news.";

            string gate = t.GateHP < 300f ? "The gate looks battered." : "The gate stands firm.";
            string eco = t.EconomyScore < 30 ? "Money is tight." : t.EconomyScore < 70 ? "Trade feels steady." : "Coin is flowing.";
            string pop = t.Population < 200 ? "The streets feel empty." : t.Population < 700 ? "The town is lively." : "Crowds pack the market.";

            return $"{gate}\n{eco}\n{pop}\n\nRumor: \"Someone's recruiting capable hands. The Dynasty pays in certainty.\"";
        }

        private static TownData FindTown(DynastySaveData data, string town)
        {
            if (data.Towns == null) return null;
            for (int i = 0; i < data.Towns.Count; i++)
            {
                var t = data.Towns[i];
                if (t == null) continue;
                if (string.Equals(t.TownName, town, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }
    }
}
