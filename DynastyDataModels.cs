using System;
using System.Collections.Generic;

namespace OutwardDynasty
{
    // --- Core Enums ---
    public enum DynastyQuestStatus
    {
        Inactive = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    public enum NpcTaskType
    {
        Idle = 0,
        Patrol = 1,
        TradeRoute = 2,
        Raid = 3
    }

    // Roles used by NPC resolver/UI. Keep broad; expand later.
    public enum NpcRole
    {
        Ambient = 0,
        Merchant = 1,
        Trainer = 2,
        Guard = 3,
        Leader = 4,
        QuestGiver = 5,

        // Some UI/code refers to "Contact"
        Contact = 6,

        // Internal / display variants
        ContactAgent = 7
    }

    // --- Player layer (per-character) ---
    [Serializable]
    public class PlayerDynastyRecord
    {
        public string CharacterUID;
        public string CharacterName;

        // Multi-allegiance model: positive/negative scores per faction
        public Dictionary<string, float> FactionScores = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public string CurrentRegionId;
        public string CurrentTownId;

        // Minimal identity fields; inventory is still owned by the character in-game
        public int Level;
        public bool IsAlive = true;
    }

    // --- Dynasty quests (data-mode) ---
    [Serializable]
    public class DynastyQuestState
    {
        public string QuestId;

        // UI expects these
        public string Title;
        public int StepIndex;
        public int Seed;

        public DynastyQuestStatus Status = DynastyQuestStatus.Inactive;

        // Generic progression for simple tick-driven quests (escort, etc.)
        public float Progress01;

        // Optional linkage
        public string GiverNpcId;
        public string TargetTownId;

        public List<string> Flags = new List<string>();
    }

    // --- Dynasty arcs (meta quest chains) ---
    [Serializable]
    public class DynastyArcState
    {
        public string ArcId;

        // UI expects these
        public string Title;
        public int Seed;

        public DynastyQuestStatus Status = DynastyQuestStatus.Inactive;

        public int Stage;
        public List<string> Flags = new List<string>();
    }

    // --- NPC simulation record (data-mode) ---
    [Serializable]
    public class NpcSimData
    {
        public string NpcId;
        public string DisplayName;

        // Ownership / placement
        public string RegionId;
        public string TownId;
        public string Faction;

        // Some parts of the code refer to these names
        public string HomeTown;
        public string CurrentTown;

        public bool IsAlive = true;

        // Social stats (0-100)
        public float Disposition = 50f;
        public float Adventure = 25f;
        public float Loyalty = 25f;
        public float Wealth = 25f;
        public float Influence = 10f;
        public float Fear = 10f;

        // Contact / agent system
        public bool IsContact;
        public bool IsRareCompanionCandidate;

        // Used by UI/interaction
        public NpcRole Role = NpcRole.Ambient;

        // If this NPC is owned/created by a particular player contact system
        public string ContactOwnerMemberGuid;

        // Task simulation
        public NpcTaskType Task = NpcTaskType.Idle;
        public float TaskProgress01;
        public int TaskSeed;
    }
}
