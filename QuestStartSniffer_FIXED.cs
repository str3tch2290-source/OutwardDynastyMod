using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Logs a stack trace when quest-ish methods fire while Dynasty mode is enabled.
    /// This is a diagnostic tool: it does NOT block quests.
    /// </summary>
    public static class QuestStartSniffer
    {
        private static bool _applied;
        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();

        private static readonly string[] MethodNameHints =
        {
            "StartQuest", "TryStartQuest", "BeginQuest",
            "ActivateQuest", "TriggerQuest",
            "AddQuest", "RegisterQuest",
            "ShowQuest", "OpenQuest",
            "OnQuest", "QuestStart"
        };

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            var targets = FindTargets();
            Debug.Log($"[Dynasty] QuestStartSniffer: found {targets.Count} candidate method(s).");

            if (targets.Count == 0)
            {
                Debug.LogWarning("[Dynasty] QuestStartSniffer: found 0 candidates (unexpected).");
                return;
            }

            var postfixInfo = typeof(QuestStartSniffer).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = new HarmonyMethod(postfixInfo);

            int patched = 0;
            foreach (var m in targets)
            {
                if (m == null) continue;
                if (_patched.Contains(m)) continue;

                try
                {
                    harmony.Patch(m, postfix: postfix);
                    _patched.Add(m);
                    patched++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Dynasty] QuestStartSniffer: patch failed for " +
                                     $"{m.DeclaringType?.FullName}.{m.Name}: {e.Message}");
                }
            }

            Debug.Log($"[Dynasty] QuestStartSniffer: patched {patched} method(s).");
        }

        private static void Postfix(MethodBase __originalMethod)
        {
            if (DynastyCore.Instance == null) return;
            if (!DynastyCore.Instance.IsDynastyModeEnabled) return;

            var dt = __originalMethod?.DeclaringType;
            var full = dt?.FullName ?? "";
            var name = __originalMethod?.Name ?? "";

            bool questyType =
                full.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                full.IndexOf("QuestLog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                full.IndexOf("QuestManager", StringComparison.OrdinalIgnoreCase) >= 0;

            bool questyMethod = MethodNameHints.Any(h => name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!questyType && !questyMethod) return;

            var st = new System.Diagnostics.StackTrace(2, true);
            Debug.Log("[Dynasty] QuestStartSniffer HIT -> " + full + "." + name);
            Debug.Log("[Dynasty] QuestStartSniffer STACK:\n" + st);
        }

        private static List<MethodBase> FindTargets()
        {
            var list = new List<MethodBase>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name ?? "";

                if (an.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (an.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (an.StartsWith("System", StringComparison.OrdinalIgnoreCase)) continue;
                if (an.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)) continue;
                if (an.StartsWith("Mono", StringComparison.OrdinalIgnoreCase)) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t == null) continue;

                    var full = t.FullName ?? "";

                    bool questyType =
                        full.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        full.IndexOf("QuestLog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        full.IndexOf("QuestManager", StringComparison.OrdinalIgnoreCase) >= 0;

                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var m in methods)
                    {
                        if (m == null) continue;
                        var mn = m.Name ?? "";

                        bool questyMethod = MethodNameHints.Any(h => mn.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!questyType && !questyMethod) continue;

                        if (full.IndexOf("AlternateStart", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                        list.Add(m);
                    }
                }
            }

            return list.Distinct().ToList();
        }
    }
}
