using UnityEngine;
using HarmonyLib;

namespace OutwardDynasty
{
    // LOCAL EFFECTS ONLY: runs on each player machine
    public class DynastyPlayerEffects : MonoBehaviour
    {
        private DynastyCore _core;

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void Update()
        {
            if (_core == null) return;
            if (!_core.IsDynastyModeEnabled) return;

            // Only apply effects during gameplay
            if (NetworkLevelLoader.Instance != null && NetworkLevelLoader.Instance.IsGameplayPaused)
                return;

            // Apply effects once per day at the same tick hour (4 AM) if you want,
            // but for now we keep it simple and do nothing here.
        }

        // Call this from TimeManager right after the daily update if desired.
        public void ApplyDailyPlayerPenalty()
        {
            Character local = CharacterManager.Instance != null ? CharacterManager.Instance.GetFirstLocalCharacter() : null;
            if (local == null || local.Stats == null) return;

            // Burnt stamina via reflection (Outward internal field)
            var field = AccessTools.Field(typeof(CharacterStats), "m_burntStamina");
            if (field == null) return;

            float current = (float)field.GetValue(local.Stats);
            field.SetValue(local.Stats, current + 15f);

            Debug.Log("[Dynasty] Applied daily stamina burn penalty.");
        }
    }
}
