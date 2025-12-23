using System;
using System.Collections.Generic;

namespace OutwardDynasty
{
    [Serializable]
    public sealed class DynastyMasterData
    {
        public int DayCount;
        public int Bonds;
        public int Influence;

        // Your code expects this list exists.
        public List<string> CitizenIDs = new List<string>();

        public bool DynastyStarted;
        public bool PermadeathBanned;
        public bool IsApocalypseActive;

        public string CurrentHostCharacterID;

        public DynastyMasterData()
        {
            DayCount = 0;
            Bonds = 0;
            Influence = 0;
            DynastyStarted = false;
            PermadeathBanned = false;
            IsApocalypseActive = false;
            CurrentHostCharacterID = string.Empty;
        }
    }
}
