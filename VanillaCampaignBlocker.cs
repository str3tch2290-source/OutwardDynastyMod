// ======================================================
// VanillaCampaignBlocker.cs  (MANUAL APPLY - SAFE)
//
// Goal:
// - Block VANILLA campaign / new-game entrypoints when Dynasty is "starting" (enabled + not started)
// - DO NOT interfere with Alternate Start Mod (ASM).
//
// This version does NOT use PatchAll scanning attributes.
// It only patches targets if it finds them -> no Harmony crash.
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
    public static class VanillaCampaignBlocker
    {
        private static bool _applied;

        // Names VCB scans for (broad on purpose)
        private static readonly string[] MethodNameCandidates =
        {
            "OnStartDestiny",
            "StartDestiny",

            "StartNewGame",
            "BeginNewGame",
            "StartScenario",
            "BeginScenario",

            "StartTutorial",
            "BeginTutorial",

            "StartQuest",
            "TryStartQuest",
        };

        private static readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            var targets = FindTargets();

            // Filter ASM + dedupe + avoid repatch
            var finalTargets = new List<MethodBase>();
            foreach (var m in targets)
            {
                if (m == null) continue;
                if (_patched.Contains(m)) continue;
                if (IsAlternateStartMethod(m)) continue;

                finalTargets.Add(m);
            }

            Debug.Log($"[Dynasty] VanillaCampaignBlocker: patching {finalTargets.Count} entrypoint(s).");


            if (finalTargets.Count == 0)
            {
                Debug.LogWarning("[Dynasty] VanillaCampaignBlocker: found 0 targets. (This will NOT crash; it just means no vanilla entrypoints matched.)");
            }
            else
            {
                var prefixInfo = typeof(VanillaCampaignBlocker).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic);
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
                        Debug.LogWarning($"[Dynasty] VanillaCampaignBlocker: failed patch {m?.DeclaringType?.FullName}.{m?.Name}: {e.Message}");
                    }
                }
            }

            // Quest lockdown (separate from entrypoints; version-robust).
            TryApplyQuestLockdown(harmony);

        }

        private static bool ShouldBlockNow()
        {
            var core = DynastyCore.Instance;
            if (core == null || core.MasterData == null) return false;

            // Block only while "starting dynasty"
            return core.IsDynastyModeEnabled && !core.MasterData.DynastyStarted;
        }

        private static bool Prefix(MethodBase __originalMethod)
        {
            // Never block ASM (belt + suspenders)
            if (IsAlternateStartMethod(__originalMethod))
                return true;

            if (!ShouldBlockNow())
                return true;

            Debug.Log("[Dynasty] VanillaCampaignBlocker: blocked vanilla campaign entrypoint (starting dynasty).");
            return false;
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

                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var m in methods)
                    {
                        if (m == null) continue;
                        if (!MethodNameCandidates.Contains(m.Name)) continue;

                        list.Add(m);
                    }
                }
            }

            return list.Distinct().ToList();
        }

        private static bool IsAlternateStartMethod(MethodBase m)
        {
            try
            {
                if (m == null) return false;

                var dt = m.DeclaringType;
                var full = dt?.FullName ?? "";
                var ns = dt?.Namespace ?? "";
                var asm = dt?.Assembly?.GetName()?.Name ?? "";

                if (full.IndexOf("AlternateStart", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (ns.IndexOf("AlternateStart", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (asm.IndexOf("AlternateStart", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                return false;
            }
            catch { return false; }
        }

        // ======================================================
        // Quest lockdown (version-robust): prevents vanilla quests from being started/added/activated.
        // Uses the provided Quest_Log.xml list as the canonical set of quest IDs to block.
        // ======================================================

        private static bool _questLockdownApplied;
        private static readonly HashSet<MethodBase> _questPatched = new HashSet<MethodBase>();

        private static readonly HashSet<string> BlockedQuestIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BC_AncPeace_00", "BC_AncPeace_00-b", "BC_AncPeace_01", "BC_AncPeace_02", "BC_AncPeace_03", "BC_AncPeace_04",
            "BC_AncPeace_05", "BC_AncPeace_06", "BC_AncPeace_07", "BC_AncPeace_08", "BC_AncPeace_09", "BC_AncPeace_10",
            "BC_AncPeace_11", "BC_AncPeace_12", "BC_AncPeace_13", "BC_AncPeace_14", "BC_AncPeace_15", "BC_AncPeace_16",
            "BC_AncPeace_17", "BC_AncPeace_18", "BC_AncPeace_19", "BC_AncPeace_20", "BC_Bones_00", "BC_Bones_00-b",
            "BC_Bones_01", "BC_Bones_02", "BC_Bones_03", "BC_Bones_04", "BC_Bones_05", "BC_Bones_06",
            "BC_Bones_07", "BC_Bones_08", "BC_Bones_09", "BC_Bones_10", "BC_Bones_11", "BC_Bones_12",
            "BC_Bones_12-b", "BC_Bones_13", "BC_Bones_14", "BC_Bones_15", "BC_Bones_16", "BC_Bones_17",
            "BC_Bones_18", "BC_Bones_19", "BC_Bones_20", "BC_Bones_21", "BC_Giants_00", "BC_Giants_00-b",
            "BC_Giants_01", "BC_Giants_02", "BC_Giants_02-b", "BC_Giants_03", "BC_Giants_04", "BC_Giants_05",
            "BC_Giants_06", "BC_Giants_07", "BC_Giants_08", "BC_Giants_09", "BC_Giants_10", "BC_Giants_11",
            "BC_Giants_12", "BC_Giants_13", "BC_Mix_Legacy_00", "BC_Mix_Legacy_00-b", "BC_Mix_Legacy_01", "BC_Mix_Legacy_01-b",
            "BC_Mix_Legacy_01-c", "BC_Mix_Legacy_02", "BC_Mix_Legacy_03", "BC_Mix_Legacy_04", "BC_Mix_Legacy_05", "BC_Mix_Legacy_06",
            "BC_Mix_Legacy_07", "BC_Mix_Legacy_08", "BC_Mix_Legacy_09", "BC_Mix_Legacy_10", "BC_Mix_Legacy_11", "BC_Mix_Legacy_12",
            "BC_Mix_Legacy_13", "Errand_Berg_Alch_01", "Errand_Berg_Assassin_01", "Errand_Berg_BK_01", "Errand_Berg_BK_02", "Errand_Berg_BK_03",
            "Errand_Berg_Food_01", "Errand_Berg_General_01", "Errand_BlueSkull_01", "Errand_BlueSkull_02", "Errand_Cierzo_Alch_01", "Errand_Cierzo_BK_01",
            "Errand_Cierzo_BK_02", "Errand_Cierzo_BK_03", "Errand_Cierzo_Cook_01", "Errand_Cierzo_General_01", "Errand_GoldLich_01", "Errand_GoldLich_02",
            "Errand_GoldLich_03", "Errand_GoldLich_04", "Errand_Helen_01", "Errand_Helen_02", "Errand_Idol_01", "Errand_Idol_02",
            "Errand_Idol_03", "Errand_JadeLich_01", "Errand_JadeLich_02", "Errand_Levant_BK_01", "Errand_Levant_BK_02", "Errand_Levant_BK_03",
            "Errand_Levant_Food_01", "Errand_Levant_General_01", "Errand_Lost_01", "Errand_Lost_02", "Errand_Lost_03", "Errand_Machine_01",
            "Errand_Machine_02", "Errand_Machine_03", "Errand_MageBackpack_01", "Errand_MageBackpack_02", "Errand_MageBackpack_03", "Errand_MageBackpack_04",
            "Errand_Mana_01", "Errand_Monsoon_BK_01", "Errand_Monsoon_BK_02", "Errand_Monsoon_BK_03", "Errand_Monsoon_General_01", "Errand_Myriade_01",
            "Errand_Myriade_02", "Errand_Myriade_03", "Errand_Myriade_04", "Errand_Myriade_05", "Errand_Myriade_06", "Errand_Myriade_07",
            "Errand_Slum_01", "Errand_Slum_02", "Errand_Slum_03", "Errand_Slum_04", "Errand_Slum_05", "Errand_Treasure_01",
            "Errand_Treasure_02", "Errand_Treasure_03", "Errand_Treasure_04", "Errand_Treasure_05", "Errand_Water_01", "HK_HeroPeace_00",
            "HK_HeroPeace_00-b", "HK_HeroPeace_01", "HK_HeroPeace_02", "HK_HeroPeace_03", "HK_HeroPeace_04", "HK_HeroPeace_05",
            "HK_HeroPeace_06", "HK_HeroPeace_07", "HK_HeroPeace_08", "HK_HeroPeace_09", "HK_HeroPeace_10", "HK_HeroPeace_11",
            "HK_HeroPeace_12", "HK_HeroPeace_13", "HK_HeroPeace_14", "HK_HeroPeace_15", "HK_HeroPeace_15-b", "HK_HeroPeace_16",
            "HK_HeroPeace_17", "HK_HeroPeace_18", "HK_HeroPeace_18-b", "HK_HeroPeace_19", "HK_HeroPeace_20", "HK_HeroPeace_21",
            "HK_HeroPeace_22", "HK_HeroPeace_23", "HK_HeroPeace_24", "HK_HeroPeace_25", "HK_HeroPeace_26", "HK_HeroPeace_27",
            "HK_MouthsToFeed_00", "HK_MouthsToFeed_00-b", "HK_MouthsToFeed_01", "HK_MouthsToFeed_02", "HK_MouthsToFeed_03", "HK_MouthsToFeed_04",
            "HK_MouthsToFeed_05", "HK_MouthsToFeed_06", "HK_MouthsToFeed_07", "HK_MouthsToFeed_08", "HK_MouthsToFeed_09", "HK_MouthsToFeed_10",
            "HK_MouthsToFeed_11", "HK_SandCorsairs_00", "HK_SandCorsairs_00-b", "HK_SandCorsairs_01", "HK_SandCorsairs_02", "HK_SandCorsairs_03",
            "HK_SandCorsairs_04", "HK_SandCorsairs_05", "HK_SandCorsairs_06", "HK_SandCorsairs_07", "HK_SandCorsairs_08", "HK_SandCorsairs_09",
            "HK_SandCorsairs_10", "HK_SandCorsairs_11", "HK_SandCorsairs_12", "HK_SandCorsairs_13", "HK_SandCorsairs_14", "HK_SandCorsairs_15",
            "HK_SandCorsairs_16", "HK_SandCorsairs_17", "HK_SandCorsairs_18", "HK_Tendflame_00", "HK_Tendflame_00-b", "HK_Tendflame_01",
            "HK_Tendflame_02", "HK_Tendflame_03", "HK_Tendflame_04", "HK_Tendflame_05", "HK_Tendflame_06", "HK_Tendflame_07",
            "HK_Tendflame_08", "HK_Tendflame_09", "HK_Tendflame_10", "HK_Tendflame_11", "HK_Tendflame_12", "HK_Tendflame_13",
            "HK_Tendflame_14", "HK_Tendflame_15", "HM_Doubt_00", "HM_Doubt_00-b", "HM_Doubt_01", "HM_Doubt_02",
            "HM_Doubt_03", "HM_Doubt_04", "HM_Doubt_05", "HM_Doubt_06", "HM_Doubt_07", "HM_Doubt_08",
            "HM_Doubt_09", "HM_Doubt_10", "HM_Doubt_11", "HM_Doubt_12", "HM_Doubt_13", "HM_Doubt_14",
            "HM_HolyPeace_00", "HM_HolyPeace_00-b", "HM_HolyPeace_01", "HM_HolyPeace_02", "HM_HolyPeace_03", "HM_HolyPeace_03-b",
            "HM_HolyPeace_04", "HM_HolyPeace_05", "HM_HolyPeace_06", "HM_HolyPeace_07", "HM_HolyPeace_08", "HM_HolyPeace_09",
            "HM_HolyPeace_10", "HM_HolyPeace_11", "HM_HolyPeace_11-b", "HM_HolyPeace_12", "HM_HolyPeace_13", "HM_HolyPeace_14",
            "HM_HolyPeace_14-b", "HM_HolyPeace_15", "HM_HolyPeace_16", "HM_HolyPeace_17", "HM_HolyPeace_18", "HM_HolyPeace_19",
            "HM_HolyPeace_20", "HM_HolyPeace_21", "HM_HolyPeace_22", "HM_HolyPeace_23", "HM_Questions_00", "HM_Questions_00-b",
            "HM_Questions_01", "HM_Questions_02", "HM_Questions_03", "HM_Questions_04", "HM_Questions_05", "HM_Questions_06",
            "HM_Questions_07", "HM_Questions_08", "HM_Questions_09", "HM_Questions_10", "HM_Questions_11", "HM_Questions_12",
            "HM_Questions_13", "HM_Questions_14", "HM_Truth_00", "HM_Truth_00-b", "HM_Truth_01", "HM_Truth_02",
            "HM_Truth_03", "HM_Truth_04", "HM_Truth_05", "HM_Truth_06", "HM_Truth_07", "HM_Truth_08",
            "HM_Truth_09", "HM_Truth_10", "HM_Truth_11", "HM_Truth_12", "HM_Truth_13", "HM_Truth_14",
            "HM_Truth_15", "HM_Truth_16", "Neut_BloodUnderSun_01", "Neut_BloodUnderSun_02", "Neut_BloodUnderSun_03", "Neut_BloodUnderSun_04",
            "Neut_BloodUnderSun_05", "Neut_BloodUnderSun_06", "Neut_BloodUnderSun_06-b", "Neut_BloodUnderSun_07", "Neut_BloodUnderSun_08", "Neut_BloodUnderSun_09",
            "Neut_BloodUnderSun_10", "Neut_BloodUnderSun_11", "Neut_BloodUnderSun_12", "Neut_BloodUnderSun_13", "Neut_BloodUnderSun_14", "Neut_Call-to_Adv_01",
            "Neut_Call-to_Adv_02", "Neut_Call-to_Adv_03", "Neut_Call-to_Adv_04", "Neut_Call-to_Adv_05", "Neut_Call-to_Adv_06", "Neut_Call-to_Adv_07",
            "Neut_Call-to_Adv_08", "Neut_Call-to_Adv_09", "Neut_Call-to_Adv_10", "Neut_EarnedRest_01", "Neut_Intro_01", "Neut_Intro_02",
            "Neut_Intro_03", "Neut_Intro_04", "Neut_Intro_05", "Neut_Purifier_00", "Neut_Purifier_01", "Neut_Purifier_02",
            "Neut_Purifier_03", "Neut_Purifier_04", "Neut_Purifier_05", "Neut_Purifier_06", "Neut_Purifier_07", "Neut_Purifier_08",
            "Neut_Purifier_09", "Neut_Purifier_10", "Neut_Purifier_11", "Neut_Purifier_12", "Neut_Purifier_13", "Neut_Purifier_14",
            "Neut_Purifier_15", "Neut_Purifier_16", "Neut_Purifier_17", "Neut_Purifier_18", "Neut_Purifier_19", "Neut_prequests_00",
            "Neut_prequests_01", "Neut_prequests_02", "Neut_prequests_03", "Neut_prequests_04", "Neut_prequests_05", "Neut_prequests_06",
            "Neut_prequests_07", "Neut_prequests_08", "Neut_prequests_09", "Neut_prequests_10", "Neut_prequests_11", "Neut_prequests_12",
            "Neut_prequests_13", "Neut_prequests_14", "Neut_prequests_15", "Neut_prequests_16", "Neut_prequests_17", "Neut_prequests_18",
            "Neut_prequests_19", "Neut_prequests_20", "Neut_prequests_21", "Neut_vendavel_01", "Neut_vendavel_02", "Neut_vendavel_03",
            "Neut_vendavel_04", "Neut_vendavel_05", "Neut_vendavel_06", "Neut_vendavel_07", "Neut_vendavel_08", "Neut_vendavel_09",
            "Neut_vendavel_10", "Neut_vendavel_11", "Neut_vendavel_12", "QName_BC_AncPeace", "QName_BC_AshGiants", "QName_BC_MixLegacies",
            "QName_BC_WhispBones", "QName_Errand_Berg_Alch", "QName_Errand_Berg_Assassin", "QName_Errand_Berg_BK", "QName_Errand_Berg_Food", "QName_Errand_Berg_General",
            "QName_Errand_BlueSkull", "QName_Errand_Cierzo_Alch", "QName_Errand_Cierzo_BK", "QName_Errand_Cierzo_Cook", "QName_Errand_Cierzo_General", "QName_Errand_GoldLich",
            "QName_Errand_Helen", "QName_Errand_Idol", "QName_Errand_JadeLich", "QName_Errand_Levant_BK", "QName_Errand_Levant_Food", "QName_Errand_Levant_General",
            "QName_Errand_Lost", "QName_Errand_Machine", "QName_Errand_MageBackpack", "QName_Errand_Mana", "QName_Errand_Monsoon_BK", "QName_Errand_Monsoon_General",
            "QName_Errand_Myriade", "QName_Errand_Slum", "QName_Errand_Treasure", "QName_Errand_Water", "QName_HK_HeroPeace", "QName_HK_MouthFeed",
            "QName_HK_SandCorsairs", "QName_HK_TendFlame", "QName_HM_Doubts", "QName_HM_HolyPeace", "QName_HM_Question", "QName_HM_Truth",
            "QName_Neut_CallToA", "QName_Neut_EarnedRest", "QName_Neut_FactCommit", "QName_Neut_Fraticide", "QName_Neut_Purifier", "QName_Neut_Tutorial",
            "QName_Neut_Vendavel", "loc_key",
        };

        private static readonly string[] QuestMethodHints =
        {
            "StartQuest", "TryStartQuest", "BeginQuest",
            "ActivateQuest", "TriggerQuest",
            "AddQuest", "RegisterQuest",
            "QuestStart",
            // Definitive Edition quest-event pipeline
            "QuestEvent",
            "OnQuestEventAdded",
            "SendSyncQuestEventAdd",
            "ShowQuestEvent",
            "RefreshCurrentQuestEvent"
        };

        private static void TryApplyQuestLockdown(Harmony harmony)
        {
            if (_questLockdownApplied) return;
            _questLockdownApplied = true;

            try
            {
                var targets = FindQuestTargets();
                Debug.Log($"[Dynasty] VanillaCampaignBlocker: quest lockdown found {targets.Count} candidate method(s).");

                if (targets.Count == 0)
                {
                    Debug.LogWarning("[Dynasty] VanillaCampaignBlocker: quest lockdown found 0 targets. (Non-fatal; game version may differ.)");
                    return;
                }

                var prefixInfo = typeof(VanillaCampaignBlocker).GetMethod(nameof(QuestPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                var prefix = new HarmonyMethod(prefixInfo);

                int patched = 0;
                foreach (var m in targets)
                {
                    if (m == null) continue;
                    if (_questPatched.Contains(m)) continue;

                    try
                    {
                        harmony.Patch(m, prefix: prefix);
                        _questPatched.Add(m);
                        patched++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Dynasty] VanillaCampaignBlocker: quest patch failed for {m?.DeclaringType?.FullName}.{m?.Name}: {e.Message}");
                    }
                }

                Debug.Log($"[Dynasty] VanillaCampaignBlocker: quest lockdown patched {patched} method(s).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] VanillaCampaignBlocker: quest lockdown apply failed (non-fatal): " + e);
            }
        }

        
        private static bool IsInDreamWorld()
        {
            try { return SceneManager.GetActiveScene().name == "DreamWorld"; }
            catch { return false; }
        }

private static bool QuestPrefix(object __instance, object[] __args, MethodBase __originalMethod)
        {
            try
            {
                if (DynastyCore.Instance == null) return true;
                if (!DynastyCore.Instance.IsDynastyModeEnabled) return true;
                if (IsInDreamWorld()) return true; // keep DreamWorld usable

                // Identify quest ID if present; log it for confirmation.
                var qid = TryExtractQuestId(__args);
                if (!string.IsNullOrWhiteSpace(qid))
                {
                    if (BlockedQuestIds.Contains(qid))
                    {
                        Debug.Log($"[Dynasty] Quest blocked (Dynasty lockdown): {qid} ({__originalMethod?.DeclaringType?.Name}.{__originalMethod?.Name})");
                        return false;
                    }

                    // If it's a quest ID we don't recognize, still block (goal = no quests available).
                    Debug.Log($"[Dynasty] Quest blocked (unlisted id; still blocked): {qid} ({__originalMethod?.DeclaringType?.Name}.{__originalMethod?.Name})");
                    return false;
                }

                // If we can't extract an ID, still block these quest-start pathways.
                Debug.Log($"[Dynasty] Quest blocked (no id found): {__originalMethod?.DeclaringType?.Name}.{__originalMethod?.Name}");
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static string TryExtractQuestId(object[] args)
        {
            if (args == null) return null;

            foreach (var a in args)
            {
                if (a == null) continue;

                if (a is string s)
                {
                    if (BlockedQuestIds.Contains(s)) return s;
                    // Sometimes quest IDs are prefixed/suffixed; keep it strict to avoid false positives.
                    continue;
                }

                var t = a.GetType();
                // Common fields/properties in Outward quest objects.
                var candidates = new[] { "QuestID", "QuestId", "ID", "Id", "Identifier", "Name", "UID", "Uid", "m_QuestID", "m_QuestId",
                    // DE quest-event identifiers
                    "m_selectedQuestEventUID", "QuestEventUID", "QuestEventId", "QuestEventID",
                    "m_QuestEventUID", "m_QuestEventId", "m_UID", "UID" };
                foreach (var c in candidates)
                {
                    try
                    {
                        var p = t.GetProperty(c, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (p != null && p.PropertyType == typeof(string))
                        {
                            var val = p.GetValue(a, null) as string;
                            if (!string.IsNullOrWhiteSpace(val)) return val;
                        }

                        var f = t.GetField(c, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (f != null && f.FieldType == typeof(string))
                        {
                            var val = f.GetValue(a) as string;
                            if (!string.IsNullOrWhiteSpace(val)) return val;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        private static List<MethodBase> FindQuestTargets()
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
                    if (full.IndexOf("AlternateStart", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                    // Only quest-related types; avoid patching generic UI/menus.
                    if (full.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) < 0 && full.IndexOf("QuestManager", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var m in methods)
                    {
                        if (m == null) continue;
                        var mn = m.Name ?? "";
                        if (!QuestMethodHints.Any(h => mn.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                        list.Add(m);
                    }
                }
            }

            return list.Distinct().ToList();
        }
    }
}