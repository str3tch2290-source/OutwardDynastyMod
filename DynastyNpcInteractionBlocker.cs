// ======================================================
// DynastyNpcInteractionBlocker.cs  (MANUAL APPLY - SAFE)
//
// Goal:
// - When Dynasty Mode is enabled (outside DreamWorld), prevent any NPC-driven
//   interaction UI from opening (shops, trainers, merchant menus), even if
//   some path bypasses DialogueGate.
// - DO NOT touch world interactables (doors, beds, chests, stashes, etc).
//
// Implementation:
// - Reflection-scan for a small set of "NPC interaction UI" types/methods.
// - Patch with a prefix that returns false during Dynasty lockdown.
// - If nothing matches (game version changes), it fails safe without crashing.
// ======================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    public static class DynastyNpcInteractionBlocker
    {
        private static bool _applied;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();

        // Keep this list SMALL and NPC-specific to avoid collateral damage.
        private static readonly string[] TypeNameHints =
        {
            "ShopMenu",
            "Merchant",
            "Trainer",
            "Dialogue", // backup: some builds open trade from dialogue-adjacent paths
        };

        // Common entry method names across builds.
        private static readonly string[] MethodNameHints =
        {
            "Show",
            "Open",
            "Init",
            "StartTrade",
            "StartTraining",
            "StartMerchant",
            "OnShow",
        };

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            try
            {
                var targets = FindTargets();
                var finalTargets = targets.Where(m => m != null && !_patched.Contains(m)).ToList();

                Debug.Log($"[Dynasty] DynastyNpcInteractionBlocker: patching {finalTargets.Count} method(s).");
                if (finalTargets.Count == 0)
                {
                    Debug.LogWarning("[Dynasty] DynastyNpcInteractionBlocker: found 0 targets. (Non-fatal; may be game version differences.)");
                    return;
                }

                var prefixInfo = typeof(DynastyNpcInteractionBlocker).GetMethod(nameof(Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                var prefix = new HarmonyMethod(prefixInfo);

                foreach (var m in finalTargets)
                {
                    try
                    {
                        harmony.Patch(m, prefix: prefix);
                        _patched.Add(m);
                        Debug.Log($"[Dynasty]   - {m.DeclaringType?.FullName}.{m.Name}()");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Dynasty] DynastyNpcInteractionBlocker: failed patch {m?.DeclaringType?.FullName}.{m?.Name}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] DynastyNpcInteractionBlocker.Apply failed (non-fatal): " + e);
            }
        }

        private static bool Prefix(MethodBase __originalMethod)
        {
            try
            {
                if (DynastyCore.Instance == null) return true;
                if (!DynastyCore.Instance.IsDynastyModeEnabled) return true;

                // Always allow DreamWorld (ASM/Dynasty setup flow lives there).
                if (DreamWorldLock.IsInDreamWorld()) return true;

                // Lockdown: block NPC interaction UI.
                var dt = __originalMethod?.DeclaringType;
                Debug.Log($"[Dynasty] NPC interaction blocked (Dynasty lockdown): {dt?.FullName}.{__originalMethod?.Name}");
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static List<MethodBase> FindTargets()
        {
            var list = new List<MethodBase>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name ?? "";
                if (an.StartsWith("System", StringComparison.OrdinalIgnoreCase)) continue;
                if (an.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)) continue;
                if (an.StartsWith("Mono", StringComparison.OrdinalIgnoreCase)) continue;
                if (an.StartsWith("Unity", StringComparison.OrdinalIgnoreCase)) continue;
                if (an.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (an.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;
                    var full = t.FullName ?? "";
                    if (!TypeNameHints.Any(h => full.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                    // Avoid patching our own mod types even if they match a hint.
                    if (full.IndexOf("OutwardDynasty", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var m in methods)
                    {
                        if (m == null) continue;
                        var mn = m.Name ?? "";
                        if (!MethodNameHints.Any(h => mn.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                        list.Add(m);
                    }
                }
            }

            // Distinct by metadata token if possible.
            return list.Distinct().ToList();
        }
    }
}
