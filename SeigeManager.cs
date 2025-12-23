using UnityEngine;

namespace OutwardDynasty
{
    public class SiegeManager : MonoBehaviour
    {
        public static SiegeManager Instance;

        public void Initialize()
        {
            Instance = this;
            Debug.Log("Siege Manager Initialized.");
        }

        public void TriggerSiege(string townID)
        {
            // Logic for town attacks using NationBills/BanditStrength from FactionData
        }
    }
}