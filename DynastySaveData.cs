using System;
using System.Collections.Generic;

namespace OutwardDynasty
{
    [Serializable]
    public class DynastySaveData
    {
        // Bump this when you add fields that old saves won't have.
        // JsonUtility will default missing fields safely anyway.
        public int DynastyVersion = 6;

        // Unique dynasty/session id (lets Companion App host multiple dynasties simultaneously)
        public string DynastyId = "";

        // Master enable/disable switch for Dynasty mode
        public bool DynastyModeEnabled = false;

        // Seeded RNG for probabilistic simulation (stable across clients)
        public int WorldSeed = 0;

        // -----------------------------
        // Core run flags
        // -----------------------------
        public bool DynastyEnabled = false;
        public bool DynastyStarted = false;

        // If true, the dynasty is barred from permadeath mode (guardrail / moderation / legacy saves).
        public bool PermadeathBanned = false;
        public bool PlayerPlaced = false;

        // -----------------------------
        // Global timeline / apocalypse
        // -----------------------------
        public int DayCount = 0;
        public bool IsApocalypseActive = false;
        public float ScourgeMultiplier = 1f;

        // -----------------------------
        // Economy / meta resources
        // -----------------------------
        public int Bonds = 0;
        public int Influence = 0;

        // Soul Echos (ghetto item placeholder)
        public int SoulEchos = 0;

        // Numerical inflation index. (I1) Interpreted labels are derived in UI.
        public float InflationIndex = 1f;

        // -----------------------------
        // Host authority marker (optional, used by your architecture)
        // -----------------------------
        public string CurrentHostCharacterID = "NONE";

        // -----------------------------
        // World state
        // -----------------------------
        public List<string> CitizenIDs = new List<string>();
        public List<TownData> Towns = new List<TownData>();
        public List<FactionData> Factions = new List<FactionData>();

        // Region-level simulation (economy, scarcity, migration)
        public List<RegionData> Regions = new List<RegionData>();

        // One trade caravan per faction (with routing constraints)
        public List<CaravanData> Caravans = new List<CaravanData>();

        // --- Multiplayer/player layer (minimal per-character metadata; inventory stays vanilla) ---
        public List<PlayerDynastyRecord> Players = new List<PlayerDynastyRecord>();

        // --- NPC simulation layer (data-mode NPCs; rendered NPCs reflect these) ---
        public List<NpcSimData> Npcs = new List<NpcSimData>();

        // --- Dynasty quest layer (v1: data-backed quests with dialogue wrappers; optional bridge to vanilla quests later) ---
        public List<DynastyQuestState> DynastyQuests = new List<DynastyQuestState>();

        // --- Dynasty arc quest layer ---
        public List<DynastyArcState> DynastyArcs = new List<DynastyArcState>();

    }

    // -----------------------------
    // Regions / Resources
    // -----------------------------
    public enum ResourceType
    {
        Food,
        Water,
        Wood,
        Ore,
        Cloth,
        Salt,
        ManaStone
    }

    [Serializable]
    public class RegionData
    {
        public string RegionId = "";      // e.g. "Chersonese"
        public string OwnerFaction = "";  // faction name or "NONE"
        public int Population = 0;

        // Town economy rule: each town produces all but one main resource.
        // Region aggregates scarcity numerically.
        public float Scarcity = 0f;       // 0..1+
        public Dictionary<ResourceType, int> Stock = new Dictionary<ResourceType, int>();

        // Tracks migration desire (0..1+)
        public float MigrationPressure = 0f;

        // Trade slot occupancy: number of caravans currently in region (simulation-only)
        public int CaravanCount = 0;

        public RegionData() { }
        public RegionData(string id) { RegionId = id; }
    }

    [Serializable]
    public class CaravanData
    {
        public string CaravanId = "";
        public string FactionName = "";
        public string CurrentRegion = "";
        public string DestinationRegion = "";
        public int RouteIndex = 0;
        public List<string> Route = new List<string>();

        // When >0, caravan is travelling and will arrive after N days.
        public int DaysUntilArrival = 0;

        // Profit accrual
        public int Profit = 0;

        // Escort marker (player/faction)
        public string EscortBy = "NONE";
    }

    [Serializable]
    public class TownData
    {
        public string TownName;
        public string RegionId;

        // Control / defenses
        public string OwnerFaction = "NONE";
        public float GateHP = 1000f;

        // Economy simulation
        public int Population;
        public int EconomyScore;

        // Each town produces all-but-one resource; missing resource drives trade need.
        public ResourceType MissingResource = ResourceType.Food;

        // Local resource stock (coarse)
        public Dictionary<ResourceType, int> Stock = new Dictionary<ResourceType, int>();

        public TownData() { }
        public TownData(string townName, string regionId)
        {
            TownName = townName;
            RegionId = regionId;
        }
    }

    [Serializable]
    public class WarData
    {
        public string EnemyFaction = "";
        public int StartDay = 0;
        public string Status = "War"; // War/Truce/Peace
    }

    [Serializable]
    public class FactionData
    {
        public string Name;

        // Economy / solvency
        public float Treasury = 0f;
        public bool Bankrupt = false;

        // Total faction population (used to drive render-cap in scenes; computed from towns each tick)
        public int Population = 0;

        // War system supports multiple simultaneous wars
        public List<WarData> ActiveWars = new List<WarData>();

        // AI levers
        public float PlayerSupport;
        public float NationBills;
        public float BanditStrength;

        // Keep as string because other systems compare/assign strings
        public string WarStatus = "PEACE";

        // Trogs special
        public bool IsTrogFaction = false;
        public float TrogFamineStat = 0f;

        public FactionData() { }
        public FactionData(string name) { Name = name; }
    

// -----------------------------
// Player layer (per-client)
// -----------------------------
[Serializable]
public class PlayerDynastyRecord
{
    public string MemberGuid = "";
    public string DisplayName = "";

    // Soft team/allegiance scores (0-100). Multiple allegiances allowed.
    public List<FactionScore> FactionScores = new List<FactionScore>();

    // Last known location (for convene return + host migration recovery)
    public string LastScene = "";
    public string LastTown = "";
    public string LastBedId = "";

    public PlayerDynastyRecord() { }
    public PlayerDynastyRecord(string guid, string name)
    {
        MemberGuid = guid ?? "";
        DisplayName = name ?? "";
    }
}

[Serializable]
public class FactionScore
{
    public string FactionName = "";
    public float Score = 0f; // 0..100

    public FactionScore() { }
    public FactionScore(string faction, float score) { FactionName = faction; Score = score; }
}

// -----------------------------
// NPC simulation layer
// -----------------------------
public enum NpcRole
{
    Ambient,
    Merchant,
    Guard,
    Leader,
    Trainer,
    Contact
}

public enum NpcTaskType
{
    Idle,
    GuardTown,
    TradeRoute,
    Recruit,
    Patrol,
    Escort,
    Raid,
    Recover
}

[Serializable]
public class NpcSimData
{
    public string NpcId = "";
    public string DisplayName = "";
    public string HomeTown = "";
    public string CurrentTown = "";
    public string Faction = "";

    public NpcRole Role = NpcRole.Ambient;

    // Social stats
    public float Disposition = 50f; // 0..100
    public float Adventure = 25f;   // 0..100
    public float Loyalty = 25f;     // 0..100
    public float Wealth = 25f;      // 0..100
    public float Influence = 10f;   // 0..100
    public float Fear = 10f;        // 0..100

    // Tasking
    public NpcTaskType Task = NpcTaskType.Idle;
    public float TaskProgress01 = 0f;
    public int TaskSeed = 0;
    public bool IsAlive = true;

    // Contact agent (data-mode)
    public bool IsContact = false;
    public string ContactOwnerMemberGuid = "";

    public NpcSimData() { }
}

// -----------------------------
// Dynasty quest layer
// -----------------------------
public enum DynastyQuestStatus
{
    Inactive,
    Active,
    Completed,
    Failed
}

[Serializable]
public class DynastyQuestState
{
    public string QuestId = "";
    public string Title = "";
    public string GiverNpcId = "";
    public DynastyQuestStatus Status = DynastyQuestStatus.Inactive;

    // generic step tracking
    public int StepIndex = 0;
    public List<string> Flags = new List<string>();

    // used for escort/progress-in-data-mode
    public float Progress01 = 0f;
    public int Seed = 0;

    public DynastyQuestState() { }
}

// -----------------------------
// Dynasty arc layer
// -----------------------------
[Serializable]
public class DynastyArcState
{
    public string ArcId = "";
    public string Title = "";
    public DynastyQuestStatus Status = DynastyQuestStatus.Inactive;
    public int Stage = 0;
    public List<string> Flags = new List<string>();
    public DynastyArcState() { }
}

}
}