// ======================================================
// DynastyASMStartBlocker.cs
//
// Goal: Dynasty fully replaces ASM start flow.
//
// What it does:
// - When Dynasty is enabled AND you are in DreamWorld,
//   block Scenario.OnStartDestiny from running.
//
// This prevents:
// - "OnStartDestiny" scenario gear injection during your dynasty setup
// - extra systems initializing that are meant for vanilla/ASM starts
// ======================================================

using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    [HarmonyPatch]
    public static class DynastyASMStartBlocker
    {
        // Patch by name to avoid hard references if method signature shifts
        static System.Reflection.MethodBase TargetMethod()
        {
            // Scenario is in the main game assembly; this should exist in DE.
            var t = AccessTools.TypeByName("Scenario");
            if (t == null) return null;

            // Method name in your logs: OnStartDestiny
            return AccessTools.Method(t, "OnStartDestiny");
        }

        // Return false = skip original
        static bool Prefix()
        {
            try
            {
                if (!DynastyCore.DynastyEnabled)
                    return true;

                var scene = SceneManager.GetActiveScene().name;
                if (!string.Equals(scene, "DreamWorld", System.StringComparison.OrdinalIgnoreCase))
                    return true;

                Debug.Log("[Dynasty] ASM flow blocked: Scenario.OnStartDestiny suppressed in DreamWorld (Dynasty replaces ASM).");
                return false;
            }
            catch
            {
                // If anything goes wrong, do not break the game boot
                return true;
            }
        }
    }
}
