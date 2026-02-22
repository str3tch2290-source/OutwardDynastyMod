// ======================================================
// DynastyModeToggleMenu.cs  (FULL REWRITE)
//
// Goals:
// - Keep your "F8 saved me" workflow, but make it deterministic.
// - F8 = show/hide this ghetto UI window
// - Shift+F8 = toggle Dynasty mode ON/OFF (escape hatch)
// - When entering DreamWorld while starting dynasty (Enabled && !Started), auto-show window.
// - Do NOT mess with ASM. This is strictly your overlay.
// ======================================================

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class DynastyModeToggleMenu : MonoBehaviour
    {
        private DynastyCore _core;

        private bool _showWindow = true;
        private Rect _windowRect = new Rect(12, 12, 420, 280);

        private bool _stylesReady;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _button;
        private GUIStyle _tiny;

        // After toggling dynasty mode via hotkey, keep window visible briefly
        private float _forceVisibleUntil;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // If we enter DreamWorld while starting dynasty, make sure the overlay is visible.
            if (IsDreamWorld(scene.name) && IsStartingDynasty())
            {
                _showWindow = true;
                _forceVisibleUntil = Time.realtimeSinceStartup + 2f;
                Debug.Log("[Dynasty] UI auto-shown (entered DreamWorld while starting dynasty).");
            }
        }

        private void Update()
        {
            if (_core == null) return;

            // SHIFT+F8 = emergency Dynasty ON/OFF
            if (InputProxy.GetKeyDown(KeyCode.F8) && (InputProxy.GetKey(KeyCode.LeftShift) || InputProxy.GetKey(KeyCode.RightShift)))
            {
                bool newValue = !_core.IsDynastyModeEnabled;
                _core.SetDynastyMode(newValue);
                Debug.Log("[Dynasty] Hotkey Shift+F8 -> SetDynastyMode " + newValue);

                // Force show window briefly so you can see state
                _showWindow = true;
                _forceVisibleUntil = Time.realtimeSinceStartup + 2f;
                return;
            }

            // F8 (no shift) = toggle UI visibility
            if (InputProxy.GetKeyDown(KeyCode.F8))
            {
                _showWindow = !_showWindow;
                Debug.Log("[Dynasty] UI visibility toggled -> " + _showWindow);
            }

            // If we are starting dynasty in DreamWorld, and we just toggled mode, keep it visible briefly
            if (Time.realtimeSinceStartup < _forceVisibleUntil)
                _showWindow = true;
        }

        private bool IsStartingDynasty()
        {
            if (_core == null || _core.MasterData == null) return false;
            return _core.IsDynastyModeEnabled && !_core.MasterData.DynastyStarted;
        }

        private static bool IsDreamWorld(string sceneName)
        {
            return string.Equals(sceneName, "DreamWorld", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };

            _tiny = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true
            };

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (_core == null) return;
            if (!_showWindow) return;

            EnsureStyles();
            _windowRect = GUI.Window(424242, _windowRect, DrawWindow, "Dynasty (Ghetto UI)");
        }

        private void DrawWindow(int id)
        {
            string scene = SceneManager.GetActiveScene().name;

            GUILayout.Label("Dynasty Control", _title);
            GUILayout.Space(6);

            bool enabled = _core.IsDynastyModeEnabled;
            bool started = _core.MasterData != null && _core.MasterData.DynastyStarted;
            bool placed = _core.MasterData != null && _core.MasterData.PlayerPlaced;

            GUILayout.Label($"Scene: {scene}", _label);
            GUILayout.Label($"Enabled: {(enabled ? "YES" : "NO")}", _label);
            GUILayout.Label($"Started: {(started ? "YES" : "NO")}", _label);
            GUILayout.Label($"Placed:  {(placed ? "YES" : "NO")}", _label);

            GUILayout.Space(10);

            if (GUILayout.Button(enabled ? "Disable Dynasty Mode" : "Enable Dynasty Mode", _button))
            {
                bool newValue = !enabled;
                _core.SetDynastyMode(newValue);
                Debug.Log("[Dynasty] UI dynasty toggle -> " + newValue);

                // Show briefly for feedback
                _showWindow = true;
                _forceVisibleUntil = Time.realtimeSinceStartup + 2f;
            }

            GUILayout.Space(8);
            GUILayout.Label("Hotkeys:", _label);
            GUILayout.Label("F8 = Show/Hide this window", _tiny);
            GUILayout.Label("Shift+F8 = Toggle Dynasty Mode (escape hatch)", _tiny);

            if (IsDreamWorld(scene) && enabled && !started)
                GUILayout.Label("DreamWorld setup active: you should confirm via your current start flow.", _tiny);

            GUI.DragWindow();
        }
    }
}
