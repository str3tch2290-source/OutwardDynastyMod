using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    /// <summary>
    /// Minimal main-menu Host/Join entrypoints.
    /// Shows only on non-gameplay scenes (best-effort) and loads DreamWorld for setup.
    /// </summary>
    public class MainMenuDynastyOverlay : MonoBehaviour
    {
        private DynastyCore _core;
        private Rect _rect = new Rect(20, 140, 260, 140);
        private string _joinHost = "127.0.0.1";
        private bool _show;

        private GUIStyle _title, _label, _btn;

        public void Initialize(DynastyCore core) => _core = core;

        private void Update()
        {
            if (_core == null) return;
            if (!_core.IsDynastyModeEnabled) { /* still show, but indicates disabled */ }

            // Best-effort: show in scenes that look like menus, hide during normal gameplay.
            var s = SceneManager.GetActiveScene();
            var name = s.IsValid() ? (s.name ?? "") : "";
            bool looksLikeMenu =
                name.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("main", StringComparison.OrdinalIgnoreCase) >= 0;

            bool isDreamWorld = name.Equals("DreamWorld", StringComparison.OrdinalIgnoreCase);

            // If a character is spawned and we're not in a menu, hide.
            bool likelyGameplay = !looksLikeMenu && !isDreamWorld && CharacterManagerHasLocal();

            _show = !likelyGameplay && !isDreamWorld;
        }

        private bool CharacterManagerHasLocal()
        {
            try
            {
                // Avoid hard refs; Outward types may not resolve at compile time in some setups.
                var t = Type.GetType("CharacterManager, Assembly-CSharp");
                if (t == null) return false;
                var instProp = t.GetProperty("Instance");
                var inst = instProp != null ? instProp.GetValue(null, null) : null;
                if (inst == null) return false;
                var localCharProp = t.GetProperty("LocalCharacter");
                var local = localCharProp != null ? localCharProp.GetValue(inst, null) : null;
                return local != null;
            }
            catch { return false; }
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }

        private void OnGUI()
        {
            if (!_show) return;
            EnsureStyles();
            _rect = GUI.Window(912345, _rect, Draw, "Dynasty");
        }

        private void Draw(int id)
        {
            GUILayout.Label("Outward Dynasty", _title);

            GUILayout.Space(6);
            GUILayout.Label("Companion is required (auto-launch off).", _label);

            GUILayout.Space(8);
            if (GUILayout.Button("Host Dynasty", _btn, GUILayout.Height(28)))
            {
                DynastyMenu.ForceJoinMode = false;
                BeginDynastySetup();
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Join:", GUILayout.Width(36));
            _joinHost = GUILayout.TextField(_joinHost ?? "127.0.0.1");
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Join Dynasty", _btn, GUILayout.Height(28)))
            {
                DynastyMenu.ForceJoinMode = true;
                DynastyMenu.ForceJoinHost = string.IsNullOrEmpty(_joinHost) ? "127.0.0.1" : _joinHost.Trim();
                BeginDynastySetup();
            }

            GUI.DragWindow();
        }

        private void BeginDynastySetup()
        {
            try
            {
                if (_core == null) return;
                _core.IsDynastyModeEnabled = true;
                if (_core.MasterData == null) _core.MasterData = new DynastySaveData();
                _core.MasterData.DynastyModeEnabled = true;
                _core.MasterData.DynastyStarted = false;
                _core.MasterData.PlayerPlaced = false;
                _core.SaveDynastySafe();

                // Load DreamWorld; DreamWorldLock + sanitizer will keep us safe.
                SceneManager.LoadScene("DreamWorld");
                DynastyHistory.LogEvent("dynasty_setup_enter", new System.Collections.Generic.Dictionary<string, object>
                {
                    {"mode", DynastyMenu.ForceJoinMode ? "join" : "host"},
                    {"joinHost", DynastyMenu.ForceJoinMode ? DynastyMenu.ForceJoinHost : ""}
                });
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dynasty] Failed to begin dynasty setup from main menu: " + ex);
            }
        }
    }
}
