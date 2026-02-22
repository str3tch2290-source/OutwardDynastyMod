using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    // Ghetto placeholder: Soul Echo is currently triggered by keybind (F7) OR can be wired to item use later.
    // Responsibilities:
    // - Allow entry to DreamWorld (null sandbox) for trade + join staging
    // - Allow join window when host consumes an echo (joins happen when everyone is asleep; placeholder bypass)
    public class SoulEchoSystem : MonoBehaviour
    {
        private DynastyCore _core;
        private bool _joinWindowOpen = false;
        private float _cooldown = 0f;

        public void Initialize(DynastyCore core) => _core = core;

        private void Update()
        {
            if (_core == null || !_core.IsDynastyModeEnabled) return;
            _cooldown = Mathf.Max(0f, _cooldown - Time.unscaledDeltaTime);

            // F7: consume Soul Echo (host) to open join window OR go to DreamWorld
            if (_cooldown <= 0f && InputProxy.GetKeyDown(KeyCode.F7))
            {
                _cooldown = 0.5f;

                if (_core.MasterData == null) return;
                if (_core.MasterData.SoulEchos <= 0)
                {
                    Debug.Log("[Dynasty][SoulEcho] No Soul Echos.");
                    return;
                }

                _core.MasterData.SoulEchos--;

                // If already in DreamWorld, toggle join window; else travel to DreamWorld
                if (SceneManager.GetActiveScene().name != "DreamWorld")
                {
                    Debug.Log("[Dynasty][SoulEcho] Travelling to DreamWorld.");
                    SceneManager.LoadScene("DreamWorld");
                    return;
                }

                _joinWindowOpen = true;
                Debug.Log("[Dynasty][SoulEcho] Join window opened (placeholder; real gating is sleep-sync).");
            }
        }

        private void OnGUI()
        {
            if (!_joinWindowOpen) return;

            float w = 440f;
            float h = 200f;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            GUI.Box(new Rect(x, y, w, h), "Soul Echo - Join Window (Host)");

            GUI.Label(new Rect(x + 20, y + 40, w - 40, 40),
                "Placeholder join gate. In final flow, join occurs only when everyone is asleep.\nHost cannot leave until joiner selects a region.");

            if (GUI.Button(new Rect(x + 20, y + 120, w - 40, 30), "CLOSE"))
                _joinWindowOpen = false;
        }
    }
}