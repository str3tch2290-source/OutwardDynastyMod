
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Seeds v1 quests (3 personal, 3 town, 3 arcs) into DynastySaveData.
    /// This doesn't force-start them; they are offered via DynastyNpcInteractionUI.
    /// </summary>
    public class DynastyQuestContentManager : MonoBehaviour
    {
        private DynastyCore _core;

        public void Initialize(DynastyCore core) => _core = core;

        private void Start()
        {
            try { EnsureQuestSeeds(); } catch { }
        }

        public void EnsureQuestSeeds()
        {
            if (_core == null || _core.MasterData == null) return;
            var data = _core.MasterData;

            if (data.DynastyQuests == null) data.DynastyQuests = new List<DynastyQuestState>();
            if (data.DynastyArcs == null) data.DynastyArcs = new List<DynastyArcState>();

            SeedQuest(data, "P_ESCORT", "Escort Job", personal: true);
            SeedQuest(data, "P_TRAINING", "Training Gig", personal: true);
            SeedQuest(data, "P_RIVAL", "Rival Dispute", personal: true);

            SeedQuest(data, "T_REPAIR_GATE", "Repair the Gate", personal: false);
            SeedQuest(data, "T_SUPPLY", "Supply Shortage", personal: false);
            SeedQuest(data, "T_RECRUIT_CONTACT", "Recruit a Contact", personal: false);

            SeedArc(data, "A_FOUNDING", "Found the Dynasty");
            SeedArc(data, "A_TRADE_ROUTE", "Secure a Trade Route");
            SeedArc(data, "A_FACTION_PRESSURE", "Faction Pressure");
        }

        private void SeedQuest(DynastySaveData data, string id, string title, bool personal)
        {
            if (HasQuest(data, id)) return;

            var q = new DynastyQuestState
            {
                QuestId = id,
                Title = title,
                Status = DynastyQuestStatus.Inactive,
                StepIndex = 0,
                Seed = (data.WorldSeed ^ id.GetHashCode())
            };

            // Flag helps UI bucket them
            q.Flags.Add(personal ? "PERSONAL" : "TOWN");

            data.DynastyQuests.Add(q);
        }

        private void SeedArc(DynastySaveData data, string id, string title)
        {
            if (HasArc(data, id)) return;

            var a = new DynastyArcState
            {
                ArcId = id,
                Title = title,
                Status = DynastyQuestStatus.Inactive,
                Stage = 0
            };
            data.DynastyArcs.Add(a);
        }

        private bool HasQuest(DynastySaveData data, string id)
        {
            if (data.DynastyQuests == null) return false;
            for (int i = 0; i < data.DynastyQuests.Count; i++)
            {
                var q = data.DynastyQuests[i];
                if (q == null) continue;
                if (string.Equals(q.QuestId, id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private bool HasArc(DynastySaveData data, string id)
        {
            if (data.DynastyArcs == null) return false;
            for (int i = 0; i < data.DynastyArcs.Count; i++)
            {
                var a = data.DynastyArcs[i];
                if (a == null) continue;
                if (string.Equals(a.ArcId, id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
