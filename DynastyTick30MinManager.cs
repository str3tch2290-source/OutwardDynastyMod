using System;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Ticks the deterministic sim approximately every 30 in-game minutes.
    /// This version avoids hard compile-time dependencies on DynastyCore flags
    /// and DynastySaveManager methods that may differ between branches.
    /// </summary>
    public class DynastyTick30MinManager : MonoBehaviour
    {
        private DynastyCore _core;

        // Fallback: run every 120 real seconds if we can't read in-game time safely.
        private const float FALLBACK_REAL_SECONDS_PER_TICK = 120f;
        private float _nextFallbackTickAt;

        public void Initialize(DynastyCore core)
        {
            _core = core;
            _nextFallbackTickAt = Time.realtimeSinceStartup + FALLBACK_REAL_SECONDS_PER_TICK;
        }

        private void Update()
        {
            if (_core == null) return;

            // Do NOT reference DynastyCore.IsDynastyEnabled / IsDynastyEnabled here (compile-time brittle).
            // If your core wants to disable ticking, it can disable/destroy this component.

            // If we can read in-game minutes via reflection, use that.
            if (TryShouldTickByGameTime(out _))
            {
                DoTick();
                return;
            }

            // Fallback to real-time ticking.
            if (Time.realtimeSinceStartup >= _nextFallbackTickAt)
            {
                _nextFallbackTickAt = Time.realtimeSinceStartup + FALLBACK_REAL_SECONDS_PER_TICK;
                DoTick();
            }
        }

        private void DoTick()
        {
            try
            {
                var data = DynastyDataAccess.Get();
                if (data == null) return;

                SimulationEngine.ProcessWorldTick(data);

                // Persist snapshot if a compatible method exists, via reflection.
                TryCommitSnapshotReflective("Tick30Min");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Tick failed: " + ex);
            }
        }

        private void TryCommitSnapshotReflective(string reason)
        {
            try
            {
                // Common method names we might have in your branch.
                var t = typeof(DynastySaveManager);
                var m =
                    t.GetMethod("TryCommitSnapshot", new[] { typeof(string) }) ??
                    t.GetMethod("CommitSnapshot", new[] { typeof(string) }) ??
                    t.GetMethod("TryCommit", new[] { typeof(string) }) ??
                    t.GetMethod("Commit", new[] { typeof(string) });

                if (m != null)
                {
                    m.Invoke(null, new object[] { reason });
                }
            }
            catch
            {
                // intentionally swallow; ticking should not hard-fail if persistence API differs
            }
        }

        /// <summary>
        /// Attempts to decide tick cadence based on in-game time, without referencing
        /// EnvironmentManager.Instance at compile time (some builds don't have it).
        /// </summary>
        private bool TryShouldTickByGameTime(out string reason)
        {
            reason = null;

            try
            {
                var envType = Type.GetType("EnvironmentManager");
                if (envType == null) return false;

                var envObj = UnityEngine.Object.FindObjectOfType(envType);
                if (envObj == null) return false;

                var prop = envType.GetProperty("WorldTime") ?? envType.GetProperty("Time") ?? envType.GetProperty("CurrentTime");
                if (prop == null) return false;

                object val = prop.GetValue(envObj, null);
                if (val == null) return false;

                double t = 0;
                if (val is float f) t = f;
                else if (val is double d) t = d;
                else return false;

                var data = DynastyDataAccess.Get();
                if (data == null) return false;

                var lastTickProp = data.GetType().GetProperty("LastGameMinuteTick");
                if (lastTickProp == null) return false;

                double last = Convert.ToDouble(lastTickProp.GetValue(data, null) ?? 0.0);
                if (t < last) last = 0;

                if (t - last >= 30.0)
                {
                    lastTickProp.SetValue(data, t, null);
                    reason = "GameTime";
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
