using System;
using System.Reflection;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastySleepState
    {
        public static bool IsLocalSleeping { get; internal set; }
    }

    /// <summary>
    /// Best-effort local sleeping detection.
    /// We do not hard-depend on specific Outward internal APIs.
    /// </summary>
    public class DynastySleepStateUpdater : MonoBehaviour
    {
        private float _nextPoll;

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.25f;

            try
            {
                var c = CharacterManager.Instance != null ? CharacterManager.Instance.GetFirstLocalCharacter() : null;
                if (c == null)
                {
                    DynastySleepState.IsLocalSleeping = false;
                    return;
                }

                // Try common patterns: property IsSleeping, IsInSleep, or private field m_isSleeping
                var t = c.GetType();

                bool val;
                if (TryGetBoolProp(c, t, "IsSleeping", out val) ||
                    TryGetBoolProp(c, t, "IsInSleep", out val) ||
                    TryGetBoolProp(c, t, "Sleeping", out val) ||
                    TryGetBoolField(c, t, "m_isSleeping", out val) ||
                    TryGetBoolField(c, t, "isSleeping", out val))
                {
                    DynastySleepState.IsLocalSleeping = val;
                    return;
                }

                DynastySleepState.IsLocalSleeping = false;
            }
            catch
            {
                DynastySleepState.IsLocalSleeping = false;
            }
        }

        private static bool TryGetBoolProp(object obj, Type t, string name, out bool v)
        {
            v = false;
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p == null || p.PropertyType != typeof(bool) || !p.CanRead) return false;
            try { v = (bool)p.GetValue(obj, null); return true; } catch { return false; }
        }

        private static bool TryGetBoolField(object obj, Type t, string name, out bool v)
        {
            v = false;
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null || f.FieldType != typeof(bool)) return false;
            try { v = (bool)f.GetValue(obj); return true; } catch { return false; }
        }
    }
}
