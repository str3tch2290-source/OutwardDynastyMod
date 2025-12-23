// ======================================================
// ConfirmSetup.cs
// - Commits dynasty setup flags
// - Saves dynasty
// - Loads selected start scene
// C# 7.3 safe (NO top-level statements)
// ======================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class ConfirmSetup : MonoBehaviour
    {
        private DynastyCore _core;

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        /// <summary>
        /// Commits setup and loads the chosen scene.
        /// Returns true if it saved and initiated scene load.
        /// </summary>
        public bool ConfirmAndContinue(string displayName, string sceneName, string echoesText, out string status)
        {
            status = "";

            try
            {
                if (_core == null || _core.MasterData == null)
                {
                    status = "Core not ready yet.";
                    return false;
                }

                int echoes;
                if (!int.TryParse(echoesText, out echoes) || echoes < 0)
                {
                    status = "Starting Echoes must be a number (0+).";
                    return false;
                }

                if (string.IsNullOrEmpty(sceneName))
                {
                    status = "No target scene for this starting point.";
                    return false;
                }

                Debug.Log(string.Format("[Dynasty] Confirm setup: '{0}' => Scene='{1}' Echoes={2}",
                    displayName, sceneName, echoes));

                // Commit flags: dynasty exists + stop redirecting back to DreamWorld
                _core.MasterData.DynastyStarted = true;
                _core.MasterData.PlayerPlaced = true;

                // TODO later: store echoes/startpoint in save if you add fields

                _core.SaveDynasty();

                // Load target scene
                string currentScene = SceneManager.GetActiveScene().name;
                if (string.Equals(currentScene, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    status = "Already in target scene.";
                    return true;
                }

                status = "Loading: " + displayName + "...";
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] ConfirmSetup failed: " + ex);
                status = "ERROR (see log).";
                return false;
            }
        }
    }
}
