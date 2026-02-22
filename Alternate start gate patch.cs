// AlternateStartGatePatch.cs
// Only blocks ASM OnStartDestiny when DYNASTY MODE is enabled AND we're NOT starting a dynasty.
// This preserves normal ASM behavior for normal games.

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    [HarmonyPatch]
    public static class AlternateStartGatePatch
    {
        private static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("AlternateStart.StartScenarios.Scenario");
            if (t == null)
            {
                Debug.Log("[Dynasty] ASM gate: Scenario type not found (ASM not installed?).");
                return null;
            }

            var m = AccessTools.DeclaredMethod(t, "OnStartDestiny");
            if (m == null)
            {
                Debug.LogWarning("[Dynasty] ASM gate: Declared Scenario.OnStartDestiny not found.");
                return null;
            }

            return m;
        }

        private static bool Prefix()
        {
            try
            {
                // If Dynasty isn't running, NEVER interfere with ASM.
                if (DynastyCore.Instance == null || !DynastyCore.Instance.IsDynastyModeEnabled)
                    return true;

                // If Dynasty is enabled and we are currently starting the dynasty, allow ASM
                // (your DreamWorld setup/menu depends on it).
                if (IsStartingDynasty())
                    return true;

                // Dynasty is enabled but NOT starting (i.e. already started / loading normal gameplay).
                // Block ASM so it doesn't re-run scenario logic on load.
                Debug.Log("[Dynasty] ASM gate: blocked Scenario.OnStartDestiny (dynasty active, not starting).");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] ASM gate exception: " + ex);
                return true; // fail-open so you don't brick boot
            }
        }

        private static bool IsStartingDynasty()
        {
            if (DynastyCore.Instance == null) return false;
            if (!DynastyCore.Instance.IsDynastyModeEnabled) return false;

            var md = DynastyCore.Instance.MasterData;

            // Starting dynasty = enabled AND not started yet
            if (md != null && md.DynastyStarted) return false;

            return true;
        }
    }
}
