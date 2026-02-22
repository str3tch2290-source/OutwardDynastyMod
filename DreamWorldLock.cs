// DreamWorldLock.cs
// Keeps the game INSIDE DreamWorld until the player confirms dynasty setup.
//
// Fixes:
// - NetworkLevelLoader can keep a queued "real world" load running.
// - Old behavior rewrote that load to DreamWorld repeatedly -> DreamWorld reload loop.
//
// This version's rules:
// - Never interfere with LowMemory_TransitionScene
// - Always allow DreamWorld
// - Only enforce the lock when Dynasty setup is actually in progress AND one of:
//     a) DynastyCore.StartingDynasty is latched (fresh setup flow), OR
//     b) we are already inside DreamWorld (so we must cancel queued world loads)
//
// Why the gating matters:
// If a player simply toggles DynastyModeEnabled and then loads an existing save,
// we MUST NOT force DreamWorld. Redirecting should only happen once the setup flow
// has actually entered DreamWorld.

using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    [HarmonyPatch]
    public static class DreamWorldLock
    {
        private const string DreamWorldScene = "DreamWorld";
        private const string TransitionScene = "LowMemory_TransitionScene";


        /// <summary>True if the currently active Unity scene is DreamWorld.</summary>
        public static bool IsInDreamWorld()
        {
            try
            {
                var s = SceneManager.GetActiveScene();
                return string.Equals(s.name, DreamWorldScene, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // Light spam guard
        private static float _lastBlockLogTime;
        private static string _lastBlockedLevel;

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("NetworkLevelLoader");
            if (t == null) return null;

            // Preferred signature (common in Outward)
            var m = AccessTools.Method(t, "LoadLevel", new Type[]
            {
                typeof(string), typeof(int), typeof(float), typeof(bool)
            });
            if (m != null) return m;

            // Fallback: any LoadLevel(string, ...)
            foreach (var cand in t.GetMethods(System.Reflection.BindingFlags.Instance |
                                             System.Reflection.BindingFlags.Public |
                                             System.Reflection.BindingFlags.NonPublic))
            {
                if (cand.Name != "LoadLevel") continue;
                var p = cand.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType == typeof(string))
                    return cand;
            }

            return null;
        }

        private static bool IsActiveScene(string name)
        {
            try
            {
                var s = SceneManager.GetActiveScene();
                return s.IsValid() && string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool Eq(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when we should actively keep the game in DreamWorld.
        ///
        /// Key point: we only start forcing DreamWorld once the setup flow is actually engaged.
        /// That means either:
        /// - DynastyCore.StartingDynasty latch is active (fresh setup), OR
        /// - we're already inside DreamWorld (so we must cancel queued world loads)
        /// </summary>
        private static bool LockIsActive()
        {
            // If core isn't up, do nothing.
            if (DynastyCore.Instance == null || DynastyCore.Instance.MasterData == null)
                return false;

            // Setup must be in progress (either DynastyStarted==false OR PlayerPlaced==false)
            if (!DynastyCore.SetupInProgress)
                return false;

            // DO NOT redirect existing saves just because DynastyModeEnabled was toggled.
            // Only enforce once we're actually in the setup flow.
            if (DynastyCore.StartingDynasty)
                return true;

            // If we are already in DreamWorld during setup, we MUST keep blocking queued world loads.
            if (IsActiveScene(DreamWorldScene))
                return true;

            return false;
        }

        private static void LogBlocked(string target, string reason)
        {
            if (Eq(_lastBlockedLevel, target) && Time.realtimeSinceStartup - _lastBlockLogTime < 2f)
                return;

            _lastBlockedLevel = target;
            _lastBlockLogTime = Time.realtimeSinceStartup;

            Debug.Log($"[Dynasty] DreamWorldLock: blocked load '{target}' ({reason})");
        }

        // IMPORTANT: return bool so we can cancel queued loads cleanly.
        [HarmonyPrefix]
        private static bool LoadLevel_Prefix(ref string _levelName)
        {
            if (!LockIsActive())
                return true;

            if (string.IsNullOrEmpty(_levelName))
                return true;

            // Never interfere with transition scene
            if (Eq(_levelName, TransitionScene))
                return true;

            // Always allow DreamWorld loads
            if (Eq(_levelName, DreamWorldScene))
                return true;

            bool inDreamWorld = IsActiveScene(DreamWorldScene);

            if (inDreamWorld)
{
    // In DreamWorld during setup:
    // - BEFORE confirm: block all real-world loads (keeps us in DreamWorld)
    // - AFTER confirm (DynastyStarted=true, PlayerPlaced still pending): allow the world load to proceed
    bool started = false;
    try { started = DynastyCore.Instance != null && DynastyCore.Instance.MasterData != null && DynastyCore.Instance.MasterData.DynastyStarted; }
    catch { }

    if (started)
    {
        Debug.Log($"[Dynasty] DreamWorldLock: allowing load '{_levelName}' (DynastyStarted=true; PlayerPlaced pending)");
        return true;
    }

    LogBlocked(_levelName, "setup not confirmed; already in DreamWorld -> cancel queued world load");
    return false; // skip original
}

            // Not yet in DreamWorld:
            // Only redirect if the user is actively starting a dynasty (the latch is true).
            // If they are loading an existing save with DynastyModeEnabled toggled, latch is false,
            // and we won't redirect at all.
            if (DynastyCore.StartingDynasty)
            {
                Debug.Log($"[Dynasty] DreamWorldLock: redirecting load '{_levelName}' (setup not confirmed) -> '{DreamWorldScene}'");
                _levelName = DreamWorldScene;
            }

            return true;
        }
    }
}
