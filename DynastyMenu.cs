// ======================================================
// DynastyMenu.cs
// - Setup UI only (DreamWorld)
// - Starting Location is CYCLEABLE
// - Includes ALL starting points you listed
// - Uses ConfirmSetup.cs to commit + load scene
// ======================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class DynastyMenu : MonoBehaviour
    {
        private const string DREAMWORLD_SCENE = "DreamWorld";

        // ======================================================
        // Starting points (DisplayName -> SceneName)
        // ======================================================
        private static readonly List<StartPoint> StartPoints = new List<StartPoint>
        {
            // Major Cities
            new StartPoint("Cierzo (Town)", "CierzoNewTerrain"),
            new StartPoint("Cierzo (Destroyed)", "CierzoDestroyed"),
            new StartPoint("Berg (Town)", "Berg"),
            new StartPoint("Monsoon (Town)", "Monsoon"),
            new StartPoint("Levant (Town)", "Levant"),
            new StartPoint("Harmattan (Town)", "Harmattan"),
            new StartPoint("New Sirocco (Town)", "New Sirocco"),

            // Regions
            new StartPoint("Chersonese (Region)", "ChersoneseNewTerrain"),
            new StartPoint("Enmerkar Forest (Region)", "Enmerkar"),
            new StartPoint("Hallowed Marsh (Region)", "HallowedMarsh"),
            new StartPoint("Abrassar Desert (Region)", "Abrassar"),
            new StartPoint("Antique Plateau (Region)", "AntiqueField"),

            // Utility / Strategic
            new StartPoint("Tutorial", "CierzoTutorial"),
            new StartPoint("Shipwreck Start (Storage/Beach Exit)", "Chersonese_Dungeon8"),

            // Conflux
            new StartPoint("Conflux Mountain — Main Chamber", "Chersonese_Dungeon4_CommonPath"),
            new StartPoint("Conflux Mountain — Blue Chamber Path", "Chersonese_Dungeon4_BlueChamber"),

            // Quest hubs
            new StartPoint("Vendavel Fortress", "Chersonese_Dungeon1"),
            new StartPoint("Ancient Hive", "Abrassar_Dungeon5"),
            new StartPoint("Old Sirocco", "Old Sirocco"),
            new StartPoint("Abandoned Living Quarters", "AntiqueField_Dungeon1"),
        };

        private DynastyCore _core;
        private ConfirmSetup _confirm;

        private bool _showSetupWindow;
        private string _startingEchoesText = "0";
        private int _startPointIndex = 0;
        private string _statusLine = "";

        public void Initialize(DynastyCore core)
        {
            _core = core;

            _confirm = gameObject.GetComponent<ConfirmSetup>();
            if (_confirm == null)
                _confirm = gameObject.AddComponent<ConfirmSetup>();

            _confirm.Initialize(_core);

            ClampIndex();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            RefreshForScene(SceneManager.GetActiveScene().name);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshForScene(scene.name);
        }

        private void RefreshForScene(string sceneName)
        {
            _statusLine = "";

            bool inDreamWorld = string.Equals(sceneName, DREAMWORLD_SCENE, StringComparison.OrdinalIgnoreCase);

            if (_core == null || _core.MasterData == null)
            {
                _showSetupWindow = inDreamWorld;
                return;
            }

            // Show setup if not started OR not placed (works with redirect flow)
            _showSetupWindow = inDreamWorld && (!_core.MasterData.DynastyStarted || !_core.MasterData.PlayerPlaced);

            ClampIndex();
        }

        private void ClampIndex()
        {
            if (StartPoints == null || StartPoints.Count == 0)
            {
                _startPointIndex = 0;
                return;
            }

            _startPointIndex = Mathf.Clamp(_startPointIndex, 0, StartPoints.Count - 1);
        }

        private void OnGUI()
        {
            if (!_showSetupWindow) return;

            float w = 680f;
            float h = 380f;
            float x = 20f;
            float y = 20f;

            string title = "Outward Dynasty — Setup (DreamWorld)";
            if (_core != null && _core.MasterData != null && _core.MasterData.DynastyStarted && !_core.MasterData.PlayerPlaced)
                title = "Outward Dynasty — Resume Setup (DreamWorld)";

            GUI.Box(new Rect(x, y, w, h), title);

            if (_core == null || _core.MasterData == null)
            {
                GUI.Label(new Rect(x + 14f, y + 34f, w - 28f, 60f),
                    "DynastyCore / SaveData not ready yet.\nWindow is running, but you can't start until core is initialized.");
                return;
            }

            float rowY = y + 46f;
            float rowH = 26f;
            float pad = 10f;

            // Starting point selector
            GUI.Label(new Rect(x + 14f, rowY, 170f, rowH), "Starting Location:");

            StartPoint sp = GetSelectedStartPoint();

            float btnW = 34f;
            float nameW = 380f;

            Rect prevRect = new Rect(x + 14f + 170f, rowY, btnW, rowH);
            Rect nameRect = new Rect(prevRect.xMax + 6f, rowY, nameW, rowH);
            Rect nextRect = new Rect(nameRect.xMax + 6f, rowY, btnW, rowH);

            if (GUI.Button(prevRect, "<")) StepStartPoint(-1);
            GUI.Box(nameRect, sp.DisplayName);
            if (GUI.Button(nextRect, ">")) StepStartPoint(+1);

            rowY += rowH + 4f;
            GUI.Label(new Rect(x + 14f + 170f + btnW + 6f, rowY, w - 28f, rowH),
                "Scene: " + sp.SceneName);

            rowY += rowH + pad;

            // Echoes input
            GUI.Label(new Rect(x + 14f, rowY, 170f, rowH), "Starting Echoes:");
            _startingEchoesText = GUI.TextField(new Rect(x + 14f + 170f, rowY, 380f, rowH), _startingEchoesText);

            rowY += rowH + pad;

            GUI.Label(new Rect(x + 14f, rowY, w - 28f, rowH),
                "DreamWorld is the Aether hub where players connect.");

            rowY += rowH + pad;

            // Start/Continue button
            bool canStart;
            string reason;
            EvaluateStartConditions(out canStart, out reason);

            Rect startBtnRect = new Rect(x + 14f, rowY + 10f, w - 28f, 38f);

            string btnText = (!_core.MasterData.DynastyStarted) ? "START DYNASTY" : "CONFIRM & CONTINUE";

            GUI.enabled = canStart;
            bool clicked = GUI.Button(startBtnRect, btnText);
            GUI.enabled = true;

            Rect reasonRect = new Rect(x + 14f, startBtnRect.yMax + 6f, w - 28f, 44f);
            GUI.Label(reasonRect, canStart ? "Ready." : ("Not ready: " + reason));

            Rect statusRect = new Rect(x + 14f, y + h - 40f, w - 28f, 26f);
            GUI.Label(statusRect, string.IsNullOrEmpty(_statusLine) ? "" : _statusLine);

            if (clicked)
            {
                string status;
                bool ok = _confirm.ConfirmAndContinue(sp.DisplayName, sp.SceneName, _startingEchoesText, out status);
                _statusLine = status;
                if (ok) _showSetupWindow = false;
            }
        }

        private void EvaluateStartConditions(out bool canStart, out string reason)
        {
            canStart = false;
            reason = "Unknown";

            Character local = SafeGetLocalCharacter();
            if (local == null)
            {
                reason = "No local character detected yet.";
                return;
            }

            if (StartPoints == null || StartPoints.Count == 0)
            {
                reason = "No starting points configured.";
                return;
            }

            StartPoint sp = GetSelectedStartPoint();
            if (sp == null || string.IsNullOrEmpty(sp.SceneName))
            {
                reason = "Selected starting point is invalid.";
                return;
            }

            int echoes;
            if (!int.TryParse(_startingEchoesText, out echoes) || echoes < 0)
            {
                reason = "Starting Echoes must be a number (0+).";
                return;
            }

            canStart = true;
            reason = "OK";
        }

        private StartPoint GetSelectedStartPoint()
        {
            if (StartPoints == null || StartPoints.Count == 0)
                return new StartPoint("NONE", "");

            ClampIndex();
            return StartPoints[_startPointIndex];
        }

        private void StepStartPoint(int delta)
        {
            if (StartPoints == null || StartPoints.Count == 0) return;

            _startPointIndex += delta;
            if (_startPointIndex < 0) _startPointIndex = StartPoints.Count - 1;
            if (_startPointIndex >= StartPoints.Count) _startPointIndex = 0;
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

        private sealed class StartPoint
        {
            public readonly string DisplayName;
            public readonly string SceneName;

            public StartPoint(string displayName, string sceneName)
            {
                DisplayName = displayName;
                SceneName = sceneName;
            }
        }
    }
}
