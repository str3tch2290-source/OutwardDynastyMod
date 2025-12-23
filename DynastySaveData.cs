using System;
using System.Collections.Generic;

namespace OutwardDynasty
{
    [Serializable]
    public class DynastySaveData
    {
        // Bump this when you add fields that old saves won't have (JsonUtility will default them safely anyway)
        public int DynastyVersion = 2;

        // -----------------------------
        // Core run flags
        // -----------------------------
        // "Dynasty rules are active / run exists"
        public bool DynastyStarted = false;

        // "Player has been placed into the world, stop forcing DreamWorld"
        // New field to fix redirect flow.
        public bool PlayerPlaced = false;

        // -----------------------------
        // Permadeath / bans
        // -----------------------------
        public bool PermadeathBanned = false;
        public string PermadeathBanReason = "NONE";
        public string PermadeathBanTimestampUtc = "NONE";

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
    }

    [Serializable]
    public class TownData
    {
        public string TownID = "UNKNOWN";
        public string OwnerFaction = "NONE";
        public float GateHP = 1000f;
        public int EntryTax = 0;
    }

    [Serializable]
    public class FactionData
    {
        public string Name = "UNKNOWN";

        public int Aggression;
        public int Immigration;
        public int Population;
        public int EconomyScore;

        public float PlayerSupport;
        public float NationBills;
        public float BanditStrength;

        // Keep as string because other systems compare/assign strings
        public string WarStatus = "PEACE";

        public FactionData() { }
        public FactionData(string name) { Name = name; }
    }
}
