// ======================================================
// DynastyQuestGate.cs
//
// Goal:
// - Stop quests from appearing in the UI during Dynasty Mode.
//   (You reported "quests still pop up" even though campaign blockers found 0 targets.)
//
// Strategy (safe + version tolerant):
// 1) Block common quest-add entrypoints if found (reflection).
// 2) If we can't block (no targets found), we still HIDE/CLEAR quest log once per load.
//
// No surgery: drop-in file. Uses Harmony PatchAll.
//
// ======================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    [HarmonyPatch]
    public static class DynastyQuestGate
    {
        [HarmonyPrepare]
        private static bool Prepare()
        {
            // Quest lockdown is handled by VanillaCampaignBlocker.TryApplyQuestLockdown.
            // This class remains for legacy UI fallback, but we disable Harmony patching here
            // to avoid hard failures on game versions where quest entrypoints differ.
            return false;
        }

        private static bool _clearedThisScene = false;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var targets = new List<MethodBase>();

            // Try the most likely quest add methods
            Add(targets, "QuestLog", "AddQuest");
            Add(targets, "QuestLog", "RegisterQuest");
            Add(targets, "QuestKnowledge", "AddQuest");
            Add(targets, "QuestKnowledge", "RegisterQuest");
            Add(targets, "QuestManager", "StartQuest");
            Add(targets, "QuestManager", "TryStartQuest");
            Add(targets, "QuestManager", "ActivateQuest");

            // Fallback: any method named "AddQuest" on any type that contains "Quest"
            AddByContains(targets, "Quest", "AddQuest");

            if (targets.Count > 0)
                Debug.Log($"[Dynasty] QuestGate: patching {targets.Count} quest entrypoint(s).");
            else
                Debug.LogWarning("[Dynasty] QuestGate: found 0 quest entrypoints to patch (will use UI hide/clear fallback).");

            return targets;
        }

        private static void Add(List<MethodBase> list, string typeName, string methodName)
        {
            try
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) return;

                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (m == null) continue;
                    if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) continue;
                    list.Add(m);
                }
            }
            catch { }
        }

        private static void AddByContains(List<MethodBase> list, string typeNameContains, string methodName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }

                    foreach (var t in types)
                    {
                        if (t == null) continue;
                        if (t.FullName == null) continue;
                        if (!t.FullName.Contains(typeNameContains)) continue;

                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                        {
                            if (m == null) continue;
                            if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) continue;
                            list.Add(m);
                        }
                    }
                }
            }
            catch { }
        }

        private static bool Prefix(MethodBase __originalMethod)
        {
            try
            {
                if (!ShouldBlockQuests())
                    return true;

                Debug.Log($"[Dynasty] QuestGate blocked: {__originalMethod?.DeclaringType?.FullName}.{__originalMethod?.Name}()");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dynasty] QuestGate exception (fail-open): " + ex);
                return true;
            }
        }

        private static bool ShouldBlockQuests()
        {
            var core = DynastyCore.Instance;
            if (core == null || core.MasterData == null) return false;

            // Block quests only when dynasty mode is active.
            return core.IsDynastyModeEnabled;
        }

        // --- Fallback: clear quest UI data once per scene when dynasty is active ---
        [HarmonyPatch(typeof(SceneManager), "Internal_SceneLoaded")]
        [HarmonyPostfix]
        private static void OnSceneLoadedFallback()
        {
            try
            {
                _clearedThisScene = false;
            }
            catch { }
        }

        [HarmonyPatch]
        private static MethodBase TargetMethod()
        {
            try
            {
                // Avoid hard reference to GameManager (can differ across builds/mod stacks).
                var asm = typeof(Character).Assembly; // Assembly-CSharp
                var t = asm.GetType("GameManager", false);
                if (t == null) return null;
                return t.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch { return null; }
        }
        [HarmonyPostfix]
        private static void TryClearQuestUI()
        {
            try
            {
                if (_clearedThisScene) return;
                if (!ShouldBlockQuests()) return;

                // Try to find QuestLog/QuestKnowledge instances and clear obvious lists.
                // This is best-effort (reflection) and won't crash if fields differ.
                ClearByTypeName("QuestLog");
                ClearByTypeName("QuestKnowledge");

                _clearedThisScene = true;
                Debug.Log("[Dynasty] QuestGate fallback: attempted to clear quest UI lists for this scene.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dynasty] QuestGate fallback exception: " + ex);
            }
        }

        private static void ClearByTypeName(string typeName)
        {
            var t = AccessTools.TypeByName(typeName);
            if (t == null) return;

            // Find any existing objects of this type
            var objs = UnityEngine.Object.FindObjectsOfType(t);
            if (objs == null) return;

            foreach (var o in objs)
            {
                if (o == null) continue;

                // Clear any List<> fields that smell like quests
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields)
                {
                    try
                    {
                        if (f == null) continue;
                        if (!typeof(System.Collections.IList).IsAssignableFrom(f.FieldType)) continue;

                        var name = f.Name?.ToLowerInvariant() ?? "";
                        if (!(name.Contains("quest") || name.Contains("active") || name.Contains("known") || name.Contains("log")))
                            continue;

                        var list = f.GetValue(o) as System.Collections.IList;
                        list?.Clear();
                    }
                    catch { }
                }
            }
        }
    }
}