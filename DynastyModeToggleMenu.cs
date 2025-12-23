// ======================================================
// FILE: DynastyModeToggleMenu.cs
// PURPOSE:
// - Always provides a simple on-screen toggle for Dynasty mode.
// - Shows in MainMenu / TitleScreen AND in gameplay.
// - Hotkey: F8 toggles On/Off anywhere.
// ======================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class DynastyModeToggleMenu : MonoBehaviour
    {
        private DynastyCore _core;

        private bool _stylesReady;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _button;

        private Rect _rect = new Rect(20, 20, 420, 170);

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_core == null) return;

            // Hotkey toggle anywhere
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _core.IsDynastyModeEnabled = !_core.IsDynastyModeEnabled;
                Debug.Log("[Dynasty] F8 toggle -> IsDynastyModeEnabled = " + _core.IsDynastyModeEnabled);
            }
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
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (_core == null) return;

            // Show in menu scenes AND in gameplay.
            // (If you ever want "menu only", we can restrict this.)
            EnsureStyles();

            _rect = GUI.Window(9911, _rect, DrawWindow, "Outward Dynasty");
        }

        private void DrawWindow(int id)
        {
            string scene = SceneManager.GetActiveScene().name;

            GUILayout.Label("Dynasty Mode Toggle", _title);
            GUILayout.Space(6);

            GUILayout.Label("Scene: " + scene, _label);
            GUILayout.Label("Dynasty Enabled: " + (_core.IsDynastyModeEnabled ? "YES" : "NO"), _label);

            string started = (_core.MasterData != null && _core.MasterData.DynastyStarted) ? "YES" : "NO";
            GUILayout.Label("Dynasty Started: " + started, _label);

            GUILayout.Space(8);

            string btnText = _core.IsDynastyModeEnabled ? "Turn Dynasty OFF" : "Turn Dynasty ON";
            if (GUILayout.Button(btnText, _button, GUILayout.Height(36)))
            {
                _core.IsDynastyModeEnabled = !_core.IsDynastyModeEnabled;
                Debug.Log("[Dynasty] UI toggle -> IsDynastyModeEnabled = " + _core.IsDynastyModeEnabled);
            }

            GUILayout.Space(6);
            GUILayout.Label("Hotkey: F8 (toggle anywhere)", _label);

            GUI.DragWindow();
        }
    }
}
