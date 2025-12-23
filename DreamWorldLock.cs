// ======================================================
// DreamWorldLock.cs (NEW)
// Keeps player in DreamWorld until dynasty is started.
// - If DynastyStarted == false and a real gameplay scene loads,
//   it sends you back to DreamWorld (after a short delay).
// - Ignores MainMenu / Title / LowMemory_TransitionScene.
// ======================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class DreamWorldLock : MonoBehaviour
    {
        private const string DREAMWORLD_SCENE = "DreamWorld";
        private const string TRANSITION_SCENE = "LowMemory_TransitionScene";

        private const float RETURN_DELAY = 1.0f;
        private const float COOLDOWN_SECONDS = 6.0f;

        private DynastyCore _core;
        private float _lastReturnTime;

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_core == null || _core.MasterData == null) return;
            if (_core.MasterData.DynastyStarted) return;

            string s = scene.name;

            if (IsMenuLike(s)) return;
            if (s == TRANSITION_SCENE) return;
            if (s == DREAMWORLD_SCENE) return;

            // debounce to avoid ping-pong spam
            if (Time.realtimeSinceStartup - _lastReturnTime < COOLDOWN_SECONDS)
                return;

            _lastReturnTime = Time.realtimeSinceStartup;
            StartCoroutine(ReturnToDreamWorldSoon());
        }

        private IEnumerator ReturnToDreamWorldSoon()
        {
            yield return new WaitForSeconds(RETURN_DELAY);

            // need a real local character to avoid loading too early
            Character local = SafeGetLocalCharacter();
            if (local == null) yield break;

            if (SceneManager.GetActiveScene().name == DREAMWORLD_SCENE) yield break;

            Debug.Log("[Dynasty] DreamWorldLock: Dynasty not started. Returning to DreamWorld.");
            SceneManager.LoadScene(DREAMWORLD_SCENE);
        }

        private static Character SafeGetLocalCharacter()
        {
            try
            {
                if (CharacterManager.Instance == null) return null;
                return CharacterManager.Instance.GetFirstLocalCharacter();
            }
            catch { return null; }
        }

        private static bool IsMenuLike(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return true;

            if (sceneName == "MainMenu") return true;
            if (sceneName == "TitleScreen") return true;

            string lower = sceneName.ToLowerInvariant();
            if (lower.Contains("titlescreen")) return true;
            if (lower.Contains("startscreen")) return true;
            if (lower.Contains("mainscreen")) return true;

            return false;
        }
    }
}
