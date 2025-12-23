// ===============================
// TimeManager.cs (REPLACEMENT)
// Per-character dynasty safe + pure simulation
// ===============================
using UnityEngine;

namespace OutwardDynasty
{
    public class TimeManager : MonoBehaviour
    {
        private DynastyCore _core;

        // Daily tick settings
        private const int SYNC_HOUR = 4; // 4 AM

        private bool _hasProcessedToday = false;
        private int _lastHour = -1;

        private bool _timeSynced = false;
        private int _lastSyncedDynastyDay = int.MinValue;

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void Update()
        {
            if (_core == null) return;
            if (!_core.IsDynastyModeEnabled) return;

            // Must be bound to a character dynasty
            if (_core.MasterData == null) return;

            // DreamWorld / new run gate
            if (!_core.MasterData.DynastyStarted) return;

            // TOD system must exist
            if (TOD_Sky.Instance == null) return;

            // Don't run during loading/pauses
            if (NetworkLevelLoader.Instance != null && NetworkLevelLoader.Instance.IsGameplayPaused)
                return;

            // 1) Sync time once when gameplay is active (or if dynasty day changed unexpectedly)
            if (!_timeSynced || _lastSyncedDynastyDay != _core.MasterData.DayCount)
            {
                SyncTimeToDynasty();
                _timeSynced = true;
                _lastSyncedDynastyDay = _core.MasterData.DayCount;
            }

            // 2) Read hour
            int currentHour = (int)TOD_Sky.Instance.Cycle.Hour;

            // 3) Hour change handling
            if (currentHour != _lastHour)
            {
                // Leaving the tick hour resets the daily flag so tomorrow can process again
                if (currentHour != SYNC_HOUR)
                    _hasProcessedToday = false;

                _lastHour = currentHour;
            }

            // 4) Daily tick at 4 AM (once)
            if (currentHour == SYNC_HOUR && !_hasProcessedToday)
            {
                PerformDailyUpdate();
                _hasProcessedToday = true;
            }
        }

        private void SyncTimeToDynasty()
        {
            if (_core == null || _core.MasterData == null) return;
            if (TOD_Sky.Instance == null) return;

            int dynastyDay = _core.MasterData.DayCount;

            Debug.Log("[Dynasty] Syncing world time to dynasty day: " + dynastyDay);

            // Sync world day to dynasty day
            TOD_Sky.Instance.Cycle.Day = dynastyDay;

            // Optional: make brand-new dynasties start in the morning
            if (dynastyDay == 0)
            {
                TOD_Sky.Instance.Cycle.Hour = 8.0f;
            }
        }

        private void PerformDailyUpdate()
        {
            if (_core == null || _core.MasterData == null) return;

            Debug.Log("[Dynasty] 4 AM reached. Running dynasty daily update...");

            // 1) Run pure-data simulation
            WorldSimulation.ProcessDailyUpdate(_core.MasterData);

            // 2) Persist dynasty (per-character save)
            _core.SaveDynasty();
        }
    }
}
