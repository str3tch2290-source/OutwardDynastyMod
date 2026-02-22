using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    // Setup UI in DreamWorld.
    // Supports HOST and JOIN.
    public class DynastyMenu : MonoBehaviour
    {
        // Set by MainMenu overlay before loading DreamWorld
        public static bool ForceJoinMode = false;
        public static string ForceJoinHost = "127.0.0.1";

        private const string HEAVEN_SCENE = "DreamWorld";

        private DynastyCore _core;
        private int _selectedStartIndex;
        private string _echoesText = "0";
        private string _status = "";

        private bool _dynastyCreated = false;
        private bool _showCreateConfirm = false;
        private bool _showStartConfirm = false;
        private StartOption _pendingStart;
        private int _pendingEchoes;

private enum NetMode { Host, Join }
        private NetMode _mode = NetMode.Host;

        private string _joinHost = "127.0.0.1";
        private string _dynastyId = "";

        private struct StartOption
        {
            public string Label;
            public string Scene;

            public StartOption(string label, string scene)
            {
                Label = label;
                Scene = scene;
            }
        }

        private StartOption[] _starts = new StartOption[]
        {
            new StartOption("Cierzo (Chersonese)", "CierzoNewTerrain"),
            new StartOption("Berg (Enmerkar)", "EnmerkarForest"),
            new StartOption("Monsoon (Hallowed Marsh)", "HallowedMarsh"),
            new StartOption("Levant (Abrassar)", "AbrassarDesert"),
            new StartOption("Harmattan (Antique Plateau)", "AntiquePlateau"),
            new StartOption("New Sirocco (Caldera)", "Caldera")
        };

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != HEAVEN_SCENE)
                return;
        }

        
private void OnGUI()
{
    if (SceneManager.GetActiveScene().name != HEAVEN_SCENE)
        return;

    float w = 640f;
    float h = 500f;
    float x = 20f;
    float y = 20f;

    GUI.Box(new Rect(x, y, w, h), "Outward Dynasty - Setup");

    float cx = x + 20f;
    float cy = y + 40f;
    float labelW = 170f;
    float fieldW = w - 40f - labelW - 10f;
    float rowH = 24f;
    float gap = 8f;

    // MODE
    GUI.Label(new Rect(cx, cy, labelW, rowH), "Mode");
    if (GUI.Button(new Rect(cx + labelW + 10f, cy, 150f, rowH), _mode == NetMode.Host ? "HOST" : "JOIN"))
        _mode = _mode == NetMode.Host ? NetMode.Join : NetMode.Host;
    cy += rowH + gap;

    // JOIN OPTIONS
    if (_mode == NetMode.Join)
    {
        GUI.Label(new Rect(cx, cy, labelW, rowH), "Host Address");
        _joinHost = GUI.TextField(new Rect(cx + labelW + 10f, cy, fieldW, rowH), _joinHost);
        cy += rowH + gap;

        GUI.Label(new Rect(cx, cy, labelW, rowH), "Dynasty ID (optional)");
        _dynastyId = GUI.TextField(new Rect(cx + labelW + 10f, cy, fieldW, rowH), _dynastyId);
        cy += rowH + gap;
    }

    // START LOCATION
    GUI.Label(new Rect(cx, cy, labelW, rowH), "Starting Location");
    _selectedStartIndex = Mathf.Clamp(_selectedStartIndex, 0, _starts.Length - 1);
    var currentStart = _starts[_selectedStartIndex];

    float navBtnW = 32f;
    float navH = rowH;
    float labelX = cx + labelW + 10f;

    if (GUI.Button(new Rect(labelX, cy, navBtnW, navH), "<"))
        _selectedStartIndex = (_selectedStartIndex - 1 + _starts.Length) % _starts.Length;

    GUI.Box(new Rect(labelX + navBtnW + 5f, cy, fieldW - (navBtnW * 2f + 10f), navH), currentStart.Label);

    if (GUI.Button(new Rect(labelX + fieldW - navBtnW, cy, navBtnW, navH), ">"))
        _selectedStartIndex = (_selectedStartIndex + 1) % _starts.Length;

    cy += rowH + gap;

    // STARTING SOUL ECHOS
    GUI.Label(new Rect(cx, cy, labelW, rowH), "Starting Soul Echos");
    _echoesText = GUI.TextField(new Rect(cx + labelW + 10f, cy, 120f, rowH), _echoesText);
    cy += rowH + gap;

    // Scene validation hint (soft check; ConfirmSetup will do the authoritative check + resolve)
    bool loadable =
        Application.CanStreamedLevelBeLoaded(currentStart.Scene) ||
        Application.CanStreamedLevelBeLoaded(currentStart.Scene + "NewTerrain");

    if (!loadable)
    {
        GUI.Label(new Rect(cx, cy, w - 40f, rowH),
            "Scene not loadable: " + currentStart.Scene + "  (will try *NewTerrain; verify scene name)");
        cy += rowH + gap;
    }

    // STATUS
    if (!string.IsNullOrEmpty(_status))
    {
        GUI.Label(new Rect(cx, cy, w - 40f, rowH * 2f), _status);
        cy += rowH * 2f + gap;
    }

    // ACTIONS
    int echoes = 0;
    int.TryParse(_echoesText, out echoes);

    if (_mode == NetMode.Host)
    {
        if (GUI.Button(new Rect(cx, cy, w - 40f, 32f), "CREATE DYNASTY (HOST)"))
        {
            _pendingStart = currentStart;
            _pendingEchoes = echoes;
            _showCreateConfirm = true;
            _showStartConfirm = false;
        }
        cy += 32f + gap;

        GUI.enabled = _dynastyCreated;
        if (GUI.Button(new Rect(cx, cy, w - 40f, 32f),
            _dynastyCreated ? "START DYNASTY (LOAD WORLD)" : "START DYNASTY (CREATE FIRST)"))
        {
            _pendingStart = currentStart;
            _pendingEchoes = echoes;
            _showStartConfirm = true;
            _showCreateConfirm = false;
        }
        GUI.enabled = true;
        cy += 32f + gap;

        if (_dynastyCreated)
        {
            GUI.Label(new Rect(cx, cy, w - 40f, rowH),
                "Dynasty created. Share Dynasty ID with joiners if needed.");
            cy += rowH + gap;
        }
    }
    else
    {
        if (GUI.Button(new Rect(cx, cy, w - 40f, 32f), "CONNECT (JOIN)"))
            HandleJoin();

        cy += 32f + gap;
    }

    // CANCEL (always last)
    if (GUI.Button(new Rect(cx, y + h - 50f, w - 40f, 32f), "CANCEL (Turn Dynasty OFF)"))
    {
        DynastyCore.CancelDynastySetup();
        _status = "Dynasty disabled.";
    }

    // SIMPLE CONFIRM MODALS

            if (_showCreateConfirm)
            {
                DrawConfirmModal(x, y, w, h,
                    "Create Dynasty?",
                    "This will register you as HOST with the Companion Authority. Continue?",
                    onYes: () =>
                    {
                        _showCreateConfirm = false;
                        HandleCreateDynasty(_pendingStart, _pendingEchoes);
                    },
                    onNo: () => { _showCreateConfirm = false; _status = "Create cancelled."; }
                );
            }

            if (_showStartConfirm)
            {
                DrawConfirmModal(x, y, w, h,
                    "Start Dynasty?",
                    "This will load the starting region and commit you into the dynasty runtime. Continue?",
                    onYes: () =>
                    {
                        _showStartConfirm = false;
                        HandleStartWorld(_pendingStart, _pendingEchoes);
                    },
                    onNo: () => { _showStartConfirm = false; _status = "Start cancelled."; }
                );
            }
// CANCEL
            if (GUI.Button(new Rect(x + 20, y + 285, w - 40, 30), "CANCEL (Turn Dynasty OFF)"))
            {
                DynastyCore.CancelDynastySetup();
                _status = "Dynasty disabled.";
            }

            GUI.Label(
                new Rect(x + 20, y + 325, w - 40, 25),
                "F6 = World Inspector (read-only). Companion app arbitration can halt time on desync."
            );
        }

        private void HandleCreateDynasty(StartOption start, int echoes)
        {
            if (_core == null)
            {
                _status = "DynastyCore missing.";
                return;
            }

            var cc = CompanionClient.Instance;
            if (cc == null)
            {
                _status = "CompanionClient missing.";
                return;
            }

            // Make sure the authority is up (host convenience).
            TryLaunchCompanionApp();

            cc.Port = 9876;
            cc.Host = "127.0.0.1";

            // If dynastyId is blank, generate one (stable enough for MVP).
            string dynastyId = string.IsNullOrEmpty(_dynastyId) ? ("DY-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) : _dynastyId.Trim();

            if (!cc.RegisterHost(dynastyId, start.Label, start.Scene, echoes, out string status))
            {
                _status = status;
                _dynastyCreated = false;
                return;
            }

            _dynastyId = dynastyId;
            _dynastyCreated = true;
            _status = "Dynasty created as HOST. Dynasty ID: " + dynastyId;
        }

        private void HandleStartWorld(StartOption start, int echoes)
        {
            if (!_dynastyCreated)
            {
                _status = "Create dynasty first.";
                return;
            }

            if (_core == null)
            {
                _status = "DynastyCore missing.";
                return;
            }

            // Load the chosen region through ConfirmSetup (DreamWorld-safe path)
            var confirm = _core.gameObject.GetComponent<ConfirmSetup>();
            if (confirm == null)
                confirm = _core.gameObject.AddComponent<ConfirmSetup>();

            if (!confirm.ConfirmAndContinue(start.Label, start.Scene, echoes.ToString(), out string status))
                _status = status;
            else
                _status = status;
        }

        private void HandleJoin()
        {
            var cc = CompanionClient.Instance;
            if (cc == null)
            {
                _status = "CompanionClient missing.";
                return;
            }

            cc.Port = 9876;
            cc.Host = string.IsNullOrEmpty(_joinHost) ? "127.0.0.1" : _joinHost.Trim();

            string dynastyId = string.IsNullOrEmpty(_dynastyId) ? "" : _dynastyId.Trim();

            if (!cc.RegisterJoin(dynastyId, out string joinStatus))
            {
                _status = joinStatus;
                return;
            }

            cc.StartHeartbeat(false);
            _status = "Join configured. Waiting for host authority + Soul Echo injection.";
        }

        private void DrawConfirmModal(float x, float y, float w, float h, string title, string body, Action onYes, Action onNo)
        {
            // Dim background
            GUI.Box(new Rect(x, y, w, h), "");

            float mw = w - 80;
            float mh = 140;
            float mx = x + 40;
            float my = y + (h / 2f) - (mh / 2f);

            GUI.Box(new Rect(mx, my, mw, mh), title);
            GUI.Label(new Rect(mx + 15, my + 35, mw - 30, 50), body);

            if (GUI.Button(new Rect(mx + 15, my + mh - 45, (mw - 45) / 2f, 30), "YES"))
                onYes?.Invoke();

            if (GUI.Button(new Rect(mx + 30 + (mw - 45) / 2f, my + mh - 45, (mw - 45) / 2f, 30), "NO"))
                onNo?.Invoke();
        }

        private void TryLaunchCompanionApp()
        {
            try
            {
                // Avoid launching duplicates
                try
                {
                    foreach (var p in Process.GetProcessesByName("OutwardDynastyCompanion"))
                    {
                        if (!p.HasExited) return;
                    }
                }
                catch { /* ignore */ }

                string exe = null;

                // 1) Same folder as Outward executable
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidate = Path.Combine(baseDir, "OutwardDynastyCompanion.exe");
                if (File.Exists(candidate)) exe = candidate;

                // 2) Same folder as this mod assembly (common when bundled in plugins)
                if (exe == null)
                {
                    string asmDir = Path.GetDirectoryName(typeof(DynastyMenu).Assembly.Location);
                    if (!string.IsNullOrEmpty(asmDir))
                    {
                        candidate = Path.Combine(asmDir, "OutwardDynastyCompanion.exe");
                        if (File.Exists(candidate)) exe = candidate;
                    }
                }

                // 3) BepInEx plugin folder (resolved via reflection so we don't hard reference it here)
                if (exe == null)
                {
                    var bepPaths = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                        .FirstOrDefault(t => t != null && t.FullName == "BepInEx.Paths");

                    if (bepPaths != null)
                    {
                        var prop = bepPaths.GetProperty("PluginPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var pluginPath = prop != null ? prop.GetValue(null, null) as string : null;
                        if (!string.IsNullOrEmpty(pluginPath))
                        {
                            candidate = Path.Combine(pluginPath, "OutwardDynastyCompanion.exe");
                            if (File.Exists(candidate)) exe = candidate;

                            if (exe == null)
                            {
                                // common layout: plugins/OutwardDynasty/OutwardDynastyCompanion.exe
                                candidate = Path.Combine(pluginPath, "OutwardDynasty", "OutwardDynastyCompanion.exe");
                                if (File.Exists(candidate)) exe = candidate;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe)
                });
            }
            catch (Exception ex)
            {
                _status = "Failed to launch companion app: " + ex.Message;
            }
        }
    }
}
