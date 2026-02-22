using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    /// <summary>
    /// Prevent AlternateStart scenario flow from running while Dynasty setup is active in DreamWorld.
    /// This avoids duplicate Soul-Guides, duplicate InteractionActivators, and coroutine null crashes.
    /// </summary>
    public static class AlternateStartDynastyGuard
    {
        private static bool _applied;

        public static void Apply(Harmony harmony)
        {
            if (_applied) return;
            _applied = true;

            try
            {
                // Types live in AlternateStart.dll
                var scenarioType = AccessTools.TypeByName("AlternateStart.StartScenarios.Scenario");
                var scenarioManagerType = AccessTools.TypeByName("AlternateStart.ScenarioManager");

                int patched = 0;

                if (scenarioType != null)
                {
                    // IEnumerator StartScenario()
                    var startScenario = AccessTools.Method(scenarioType, "StartScenario", new Type[0]);
                    if (startScenario != null && typeof(IEnumerator).IsAssignableFrom(((MethodInfo)startScenario).ReturnType))
                    {
                        harmony.Patch(startScenario,
                            prefix: new HarmonyMethod(typeof(AlternateStartDynastyGuard), nameof(StartScenario_Prefix)));
                        patched++;
                    }

                    // void OnStartDestiny()
                    var onStartDestiny = AccessTools.Method(scenarioType, "OnStartDestiny", new Type[0]);
                    if (onStartDestiny != null)
                    {
                        harmony.Patch(onStartDestiny,
                            prefix: new HarmonyMethod(typeof(AlternateStartDynastyGuard), nameof(OnStartDestiny_Prefix)));
                        patched++;
                    }
                }

                if (scenarioManagerType != null)
                {
                    // Some versions have IEnumerator CheckStartPassives()
                    var checkStartPassives = AccessTools.Method(scenarioManagerType, "CheckStartPassives", new Type[0]);
                    if (checkStartPassives != null && typeof(IEnumerator).IsAssignableFrom(((MethodInfo)checkStartPassives).ReturnType))
                    {
                        harmony.Patch(checkStartPassives,
                            prefix: new HarmonyMethod(typeof(AlternateStartDynastyGuard), nameof(CheckStartPassives_Prefix)));
                        patched++;
                    }
                }

                Debug.Log("[Dynasty] AlternateStartDynastyGuard applied. Patched " + patched + " method(s).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dynasty] AlternateStartDynastyGuard failed: " + ex);
            }
        }

        // -----------------------------
        // Prefixes
        // -----------------------------

        // For IEnumerator methods we must NEVER return null.
        private static bool StartScenario_Prefix(ref IEnumerator __result)
        {
            if (!ShouldBlockAlternateStart()) return true;

            Debug.Log("[Dynasty] Blocking AlternateStart Scenario.StartScenario during DreamWorld dynasty setup.");
            __result = Empty();
            return false;
        }

        private static bool CheckStartPassives_Prefix(ref IEnumerator __result)
        {
            if (!ShouldBlockAlternateStart()) return true;

            Debug.Log("[Dynasty] Blocking AlternateStart ScenarioManager.CheckStartPassives during DreamWorld dynasty setup.");
            __result = Empty();
            return false;
        }

        private static bool OnStartDestiny_Prefix()
        {
            if (!ShouldBlockAlternateStart()) return true;

            Debug.Log("[Dynasty] Blocking AlternateStart Scenario.OnStartDestiny during DreamWorld dynasty setup.");
            return false;
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        private static bool ShouldBlockAlternateStart()
        {
            try
            {
                var core = DynastyCore.Instance;
                if (core == null) return false;
                if (core.MasterData == null) return false;
                if (!core.IsDynastyModeEnabled) return false;

                // Only when dynasty still needs setup
                bool needsSetup =
                    !core.MasterData.DynastyStarted ||
                    (core.MasterData.DynastyStarted && !core.MasterData.PlayerPlaced);

                if (!needsSetup) return false;

                // Only in DreamWorld
                var scene = SceneManager.GetActiveScene().name;
                if (!string.Equals(scene, "DreamWorld", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }
}
