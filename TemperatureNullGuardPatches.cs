// ======================================================
// TemperatureNullGuardPatches.cs (FINAL – VERSION SAFE)
// ======================================================
// Fixes temperature-related NRE spam during loads
// WITHOUT patching any temperature getters.
// Safe across all Outward builds.
// ======================================================

using System;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    // Guard TemperatureExposureDisplay.Update
    [HarmonyPatch(typeof(TemperatureExposureDisplay), "Update")]
    public static class Patch_TemperatureExposureDisplay_Update
    {
        [HarmonyPrefix]
        private static bool Prefix(TemperatureExposureDisplay __instance)
        {
            try
            {
                if (__instance == null)
                    return false;

                // Unity fake-null protection
                if (__instance is UnityEngine.Object uo && !uo)
                    return false;

                return true; // run vanilla Update
            }
            catch
            {
                // swallow during scene loads
                return false;
            }
        }
    }
}
