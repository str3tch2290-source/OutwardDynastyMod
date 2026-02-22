// ======================================================
// DynastySceneRedirector.cs
//
// Intercepts ALL scene load requests and reroutes them to DreamWorld
// ONLY when the character is STARTING a dynasty:
//   - DynastyMode enabled
//   - Dynasty not started yet
//
// This fixes the "redirect too late" issue where the original async load
// continues and OnStartDestiny fires anyway.
// ======================================================

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    [HarmonyPatch]
    public static class DynastySceneRedirector
    {
        private const string DREAMWORLD_SCENE = "DreamWorld";

        // Avoid infinite loops
        private static bool _redirecting;

        private static readonly string[] IgnoreScenes =
        {
            DREAMWORLD_SCENE,
            "LowMemory_TransitionScene",
            "TitleScreen",
            "StartScreen_DEFED",
            "StartScreen_DEFED_D",
            "StartScreen_DEFED_E",
        };

        private static bool ShouldIgnore(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return true;
            foreach (var s in IgnoreScenes)
                if (sceneName == s) return true;

            var lower = sceneName.ToLowerInvariant();
            if (lower.Contains("title") || lower.Contains("startscreen")) return true;

            return false;
        }

        private static bool IsStartingDynasty()
        {
            if (DynastyCore.Instance == null) return false;
            if (!DynastyCore.Instance.IsDynastyModeEnabled) return false;

            var md = DynastyCore.Instance.MasterData;
            if (md != null && md.DynastyStarted) return false;

            return true;
        }

        // Patch SceneManager.LoadScene(string, LoadSceneMode)
        [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadScene), new Type[] { typeof(string), typeof(LoadSceneMode) })]
        [HarmonyPrefix]
        private static bool LoadScene_Prefix(ref string sceneName, LoadSceneMode mode)
        {
            if (_redirecting) return true;
            if (!IsStartingDynasty()) return true;

            // If someone tries to load a real world scene during dynasty-start, reroute to DreamWorld.
            if (!ShouldIgnore(sceneName))
            {
                Debug.Log($"[Dynasty] SceneRedirector: rerouting LoadScene '{sceneName}' -> '{DREAMWORLD_SCENE}'");
                _redirecting = true;
                sceneName = DREAMWORLD_SCENE;
            }

            return true; // allow call to proceed (with modified sceneName)
        }

        // Patch SceneManager.LoadSceneAsync(string, LoadSceneMode)
        [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadSceneAsync), new Type[] { typeof(string), typeof(LoadSceneMode) })]
        [HarmonyPrefix]
        private static bool LoadSceneAsync_Prefix(ref string sceneName, LoadSceneMode mode)
        {
            if (_redirecting) return true;
            if (!IsStartingDynasty()) return true;

            if (!ShouldIgnore(sceneName))
            {
                Debug.Log($"[Dynasty] SceneRedirector: rerouting LoadSceneAsync '{sceneName}' -> '{DREAMWORLD_SCENE}'");
                _redirecting = true;
                sceneName = DREAMWORLD_SCENE;
            }

            return true;
        }

        // Once DreamWorld is active, release the redirect lock so later loads work normally.
        [HarmonyPatch(typeof(SceneManager), "Internal_SceneLoaded")]
        [HarmonyPostfix]
        private static void AnySceneLoaded_Postfix(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == DREAMWORLD_SCENE)
            {
                _redirecting = false;
            }
        }
    }
}
