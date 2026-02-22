// ======================================================
// DynastyDialogueGate.cs
//
// Goal:
// - Block *vanilla campaign / faction-join* dialogues while Dynasty Mode is enabled
// - BUT DO NOT block the Alternate Start "ghetto menu" while you are in DreamWorld setup.
//
// Why:
// - ASM's start selection/menu is implemented via DialogueTree/NodeCanvas UI dialogue.
// - If we block StartDialogue while in DreamWorld setup, the menu disappears even though
//   OnStartDestiny runs and the Soul-Guides spawn.
//
// Rule:
// - If Dynasty is starting (enabled && !DynastyStarted) AND active scene is DreamWorld:
//     allow ALL dialogues (menu must work)
// - Otherwise:
//     allow normal dialogues
//     BUT block known vanilla faction/campaign entry dialogues (Monsoon/Berg/Levant/etc.)
// ======================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public static class DynastyDialogueGate
    {
        private static bool _applied;

        // These are the vanilla dialogue tree names (DialogueTree asset names) that kick off
        // the base-game faction campaigns. This list is intentionally small & safe.
        // Add to it as you identify more entrypoints.
        private static readonly HashSet<string> BlockedDialogueTreeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Holy Mission (Monsoon)
            "Dialogue_Guard_Neut_Purifier",
            "Dialogue_Oliele_Neut_Initial",
            "Dialogue_Oliele_Neut_Prequest",
        };

        // Some of these dialogues can be triggered through multiple tree names depending on patch/DLC.
        // This is a backstop that blocks specific NPCs that are *only* used for vanilla campaign onboarding.
        private static readonly HashSet<string> BlockedInterlocutorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Monsoon faction onboarding NPC
            "name_unpc_oliele_01",
        };

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            int patched = 0;

            // Patch DialogueTreeController.StartDialogue(...) overloads
            try
            {
                var t = AccessTools.TypeByName("DialogueTreeController");
                if (t != null)
                {
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (m.Name != "StartDialogue") continue;

                        var prefix = new HarmonyMethod(typeof(DynastyDialogueGate)
                            .GetMethod(nameof(StartDialogue_Prefix), BindingFlags.Static | BindingFlags.NonPublic));

                        harmony.Patch(m, prefix: prefix);
                        patched++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] DialogueGate: failed to patch DialogueTreeController.StartDialogue: " + e);
            }

            Debug.Log($"[Dynasty] DialogueGate applied. Patched {patched} dialogue entrypoint(s).");
        }

        private static bool IsDynastyModeEnabled()
        {
            var core = DynastyCore.Instance;
            if (core == null || core.MasterData == null) return false;
            return core.IsDynastyModeEnabled;
        }

        private static bool IsStartingDynasty()
        {
            var core = DynastyCore.Instance;
            if (core == null || core.MasterData == null) return false;
            return core.IsDynastyModeEnabled && !core.MasterData.DynastyStarted;
        }

        private static bool IsInDreamWorld()
        {
            try
            {
                var s = SceneManager.GetActiveScene();
                return s.IsValid() && string.Equals(s.name, "DreamWorld", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // IMPORTANT:
        // We intentionally ALLOW dialogues during DreamWorld setup so ASM menu works.
        private static bool ShouldAllowAllDialoguesNow()
        {
            return IsStartingDynasty() && IsInDreamWorld();
        }

        private static string TryGetDialogueTreeName(object[] args)
        {
            if (args == null) return null;

            // Common patterns in Outward:
            // - DialogueTree (UnityEngine.Object) argument with .name
            // - string dialogue name
            foreach (var a in args)
            {
                if (a == null) continue;

                if (a is string s && !string.IsNullOrWhiteSpace(s))
                    return s;

                // UnityEngine.Object has a 'name' property.
                var t = a.GetType();
                var nameProp = t.GetProperty("name", BindingFlags.Instance | BindingFlags.Public);
                if (nameProp != null && nameProp.PropertyType == typeof(string))
                {
                    var n = nameProp.GetValue(a, null) as string;
                    if (!string.IsNullOrWhiteSpace(n))
                        return n;
                }
            }

            return null;
        }

        private static string TryGetInterlocutorName(object[] args)
        {
            if (args == null) return null;

            // Outward often passes Character / CharacterDialogue / Transform etc.
            // We try to find something with a 'name' that looks like an in-game character name key.
            foreach (var a in args)
            {
                if (a == null) continue;
                var t = a.GetType();

                // UnityEngine.Object or anything with 'name'
                var nameProp = t.GetProperty("name", BindingFlags.Instance | BindingFlags.Public);
                if (nameProp != null && nameProp.PropertyType == typeof(string))
                {
                    var n = nameProp.GetValue(a, null) as string;
                    if (!string.IsNullOrWhiteSpace(n) && n.StartsWith("name_", StringComparison.OrdinalIgnoreCase))
                        return n;
                }
            }

            return null;
        }

        private static bool ShouldBlockThisDialogue(object[] args)
        {
            if (!IsDynastyModeEnabled()) return false;
            if (IsInDreamWorld()) return false; // never block DreamWorld (ASM setup/menu)

            var treeName = TryGetDialogueTreeName(args);
            if (!string.IsNullOrWhiteSpace(treeName) && BlockedDialogueTreeNames.Contains(treeName))
                return true;

            var npcName = TryGetInterlocutorName(args);
            if (!string.IsNullOrWhiteSpace(npcName) && BlockedInterlocutorNames.Contains(npcName))
                return true;

            return false;
        }

        // Prefix signature should tolerate any StartDialogue overload.
        // Harmony will fill __args for most overloads.
        private static bool StartDialogue_Prefix(object __instance, object[] __args, MethodBase __originalMethod)
{
    // If Dynasty Mode is off, do not interfere.
    if (!IsDynastyModeEnabled())
        return true;

    // Always allow DreamWorld dialogues (ASM/Dynasty setup UI lives here).
    if (IsInDreamWorld())
        return true;

    // Dynasty Mode ON (outside DreamWorld) => hard-lock vanilla NPC dialogue/shops/trainers/campaign.
    try
    {
        var tree = TryGetDialogueTreeName(__args) ?? "<unknown>";
        Debug.Log($"[Dynasty] DialogueGate blocked dialogue (Dynasty lockdown): {tree} (method: {__originalMethod?.Name})");
    }
    catch { }

    return false;
}
}
}
