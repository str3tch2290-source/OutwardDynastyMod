// ======================================================
// HeavenRedirectManager.cs (REWRITE)
// Redirects to DreamWorld until the dynasty player is "placed".
// Fixes: "doesn't redirect when I start the dynasty now"
// ======================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class HeavenRedirectManager : MonoBehaviour
    {
        private const string HEAVEN_SCENE = "DreamWorld";

        private DynastyCore _core;

        // Prevent spam-loading the scene repeatedly.
        private bool _redirectedThisScene;
        private string _lastSceneName = "";

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
            // Reset redirect-per-scene guard whenever a new scene loads
            _redirectedThisScene = false;
            _lastSceneName = scene.name;
        }

        private void Update()
        {
            if (_core == null || _core.MasterData == null) return;

            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName)) return;

            // Never redirect from menu/title/loading scenes
            if (IsMenuLikeScene(sceneName)) return;

            // Never redirect if already in DreamWorld
            if (string.Equals(sceneName, HEAVEN_SCENE, StringComparison.OrdinalIgnoreCase)) return;

            // Only redirect after a real local character exists
            Character local = SafeGetLocalCharacter();
            if (local == null) return;

            // If permadeath banned or dynasty disabled, do not redirect
            if (_core.MasterData.PermadeathBanned) return;
            if (!_core.IsDynastyModeEnabled) return;

            // ------------------------------------------------------
            // ✅ NEW RULE:
            // Redirect until PlayerPlaced == true
            //
            // DynastyStarted means "rules are active"
            // PlayerPlaced means "stop forcing DreamWorld"
            // ------------------------------------------------------
            if (_core.MasterData.PlayerPlaced) return;

            // If we already redirected in this scene, don't spam
            if (_redirectedThisScene) return;

            // Optional: If you ONLY want to redirect before dynasty is started,
            // uncomment this. But for your described flow you likely want
            // redirect until placed, even after Start Dynasty.
            //
            // if (_core.MasterData.DynastyStarted) return;

            _redirectedThisScene = true;
            Debug.Log("[Dynasty] Redirecting to DreamWorld (dynasty player not placed yet)...");
            SceneManager.LoadScene(HEAVEN_SCENE);
        }

        private static Character SafeGetLocalCharacter()
        {
            try
            {
                if (CharacterManager.Instance == null)
                    return null;

                return CharacterManager.Instance.GetFirstLocalCharacter();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMenuLikeScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return true;

            if (sceneName == "MainMenu") return true;
            if (sceneName == "TitleScreen") return true;

            string lower = sceneName.ToLowerInvariant();

            if (lower.Contains("titlescreen")) return true;
            if (lower.Contains("startscreen")) return true;
            if (lower.Contains("mainscreen")) return true;
            if (lower.Contains("boot")) return true;
            if (lower.Contains("loading")) return true;

            return false;
        }
    }
}
