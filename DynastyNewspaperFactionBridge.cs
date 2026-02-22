// =======================================================
// Dynasty – Newspaper Faction Bridge (DROP-IN)
//
// CODEWORD: DYNASTY MARSHMELLOW
//
// What this fixes (no surgery, no edits):
//   Some saves end up with FactionData entries whose Name field is empty or
//   missing (older JSON / schema drift). When the Gazette/Newspaper UI prints
//   them, it falls back to f.ToString() which shows the type name:
//     "OutwardDynasty.FactionData"
//
// This file patches DynastySaveManager.Load() and normalizes faction names
// in memory, so BOTH:
//   - the Dynasty Gazette overlay
//   - any UI that reads DynastySaveData.Factions
// will see readable faction names again.
//
// It does NOT:
//   - modify any existing file
//   - resurrect vanilla campaign factions
//   - touch DreamWorld gating
//   - write to disk immediately (it will persist on next normal save)
// =======================================================

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Harmony patch: after dynasty save data is loaded, ensure faction names are valid.
    /// </summary>
    [HarmonyPatch(typeof(DynastySaveManager), nameof(DynastySaveManager.Load))]
    internal static class DynastyNewspaperFactionBridge_LoadPatch
    {
        private static void Postfix(ref DynastySaveData __result)
        {
            try
            {
                if (__result == null) return;

                int changed = DynastyNewspaperFactionBridge.NormalizeFactionNames(__result);
                if (changed > 0)
                {
                    Debug.Log($"[Dynasty][NewspaperBridge] Normalized {changed} faction name(s) after load. (Will persist on next save)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dynasty][NewspaperBridge] Post-load normalization failed (non-fatal):\n" + ex);
            }
        }
    }

    /// <summary>
    /// Optional: also normalize right before saving, to guarantee persistence even if
    /// something injects blank names after load.
    /// </summary>
    [HarmonyPatch(typeof(DynastySaveManager), nameof(DynastySaveManager.Save))]
    internal static class DynastyNewspaperFactionBridge_SavePatch
    {
        private static void Prefix(DynastySaveData data)
        {
            try
            {
                if (data == null) return;
                DynastyNewspaperFactionBridge.NormalizeFactionNames(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dynasty][NewspaperBridge] Pre-save normalization failed (non-fatal):\n" + ex);
            }
        }
    }

    internal static class DynastyNewspaperFactionBridge
    {
        // Matches the default seed order used in FactionsLibrary.EnsureDefaults().
        // Only applied when the save has the expected count and names are blank.
        private static readonly string[] DefaultFactionNames =
        {
            "Blue Chamber",
            "Heroic Kingdom",
            "Holy Mission",
            "Sorobor Academy",
            "Troglodytes",
            "Settlers"
        };

        /// <summary>
        /// Ensures every FactionData has a usable Name.
        /// Returns how many names were changed.
        /// </summary>
        public static int NormalizeFactionNames(DynastySaveData data)
        {
            if (data == null) return 0;
            if (data.Factions == null || data.Factions.Count == 0) return 0;

            // If they are all missing and the count matches the default seed, map them.
            bool allMissing = true;
            for (int i = 0; i < data.Factions.Count; i++)
            {
                var f = data.Factions[i];
                if (f == null) continue;
                if (!IsMissingName(f.Name)) { allMissing = false; break; }
            }

            int changed = 0;

            if (allMissing && data.Factions.Count == DefaultFactionNames.Length)
            {
                for (int i = 0; i < data.Factions.Count; i++)
                {
                    var f = data.Factions[i];
                    if (f == null) continue;
                    string target = DefaultFactionNames[i];
                    if (IsMissingName(f.Name) || f.Name == "UNKNOWN")
                    {
                        f.Name = target;
                        changed++;
                    }
                }

                return changed;
            }

            // Otherwise, just ensure every entry has something readable.
            for (int i = 0; i < data.Factions.Count; i++)
            {
                var f = data.Factions[i];
                if (f == null) continue;

                if (IsMissingName(f.Name))
                {
                    f.Name = $"Faction {i + 1}";
                    changed++;
                }
                else if (string.Equals(f.Name, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
                {
                    // Keep UNKNOWN from polluting the UI.
                    f.Name = $"Faction {i + 1}";
                    changed++;
                }
            }

            return changed;
        }

        private static bool IsMissingName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            // Treat the most common JSON drift placeholders as missing.
            var n = name.Trim();
            if (n.Length == 0) return true;
            if (n.Equals("(null)", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.Equals("NONE", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
    }
}
