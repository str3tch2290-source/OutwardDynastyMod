using System;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class DynastyCore : BaseUnityPlugin
    {
        public const string GUID = "com.stretch.outwarddynasty";
        public const string NAME = "Outward Dynasty";
        public const string VERSION = "1.0.0";

        // Scene used for Dynasty setup staging.
        private const string DREAMWORLD_SCENE = "DreamWorld";
        public static DynastyCore Instance;

        public DynastySaveData MasterData;

        public bool IsDynastyModeEnabled { get; private set; }

        public static bool DynastyEnabled => Instance != null && Instance.IsDynastyModeEnabled;

        public static bool SetupInProgress =>
            Instance != null &&
            Instance.IsDynastyModeEnabled &&
            Instance.MasterData != null &&
            (!Instance.MasterData.DynastyStarted || !Instance.MasterData.PlayerPlaced);

        private bool SetupComplete
        {
            get
            {
                try
                {
                    return MasterData != null && MasterData.DynastyStarted && MasterData.PlayerPlaced;
                }
                catch { return false; }
            }
        }

        public static bool StartingDynasty => Instance != null && Instance._startingDynasty;
        private bool _startingDynasty;

        // Companion authority / desync halt
        private bool _authorityHaltApplied = false;
        private float _preHaltTimescale = 1f;

        private float _startLatchUntilRealtime;
        private bool _showLoading;
        private GUIStyle _loadingStyle;

        // Companion init latch (avoids reflection spam)
        private bool _companionInitAttempted = false;

        // Debug overlay (wired directly in Core): press F6 in-game.
        private DynastyNewspaperOverlay _newspaper;
        private bool _newspaperLoggedReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Companion authority client + halt overlay
            if (gameObject.GetComponent<CompanionClient>() == null)
                gameObject.AddComponent<CompanionClient>();
            if (gameObject.GetComponent<AuthorityHUD>() == null)
                gameObject.AddComponent<AuthorityHUD>();
            if (gameObject.GetComponent<SoulEchoSystem>() == null)
                gameObject.AddComponent<SoulEchoSystem>();

            TryAttachAndInit("DynastyLogSpamFilter");

            var harmony = new Harmony(GUID);

            // Patch all normal [HarmonyPatch] classes first
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Your existing manual patchers
            try { DynastyDialogueGate.Apply(harmony); } catch { }

            // ✅ IMPORTANT: actually apply the campaign blocker (manual patcher)
            try { VanillaCampaignBlocker.Apply(harmony); }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] VanillaCampaignBlocker.Apply failed (non-fatal): " + e);
            }

            try { MasterData = DynastySaveManager.Load(); }
            catch { MasterData = new DynastySaveData(); }

            // Ensure factions/towns exist and are seeded BEFORE any UI reads them
            try
            {
                // FactionsLibrary is static; just seed defaults.
                FactionsLibrary.EnsureDefaults(MasterData);

                // Persist seeded defaults so next boot is not empty
                DynastySaveManager.Save(MasterData);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] FactionsLibrary.EnsureDefaults failed (non-fatal): " + e);
            }

            IsDynastyModeEnabled = (MasterData != null) && MasterData.DynastyModeEnabled;

            // Apply ASM faction menu hijack so ASM can display Dynasty factions
            try
            {
                AsmFactionMenuHijack.Apply(harmony, Logger);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty][ASM] Hijack Apply failed (non-fatal): " + e);
            }

            TryAttachAndInit("DynastyModeToggleMenu");
            TryAttachAndInit("DynastyHUD");
            TryAttachAndInit("DynastySaveBinder");

            TryAttachAndInit("ConfirmSetup");

            TryAttachAndInit("DreamWorldLock");
            TryAttachAndInit("DreamWorldSanitizer");
            TryAttachAndInit("HeavenRedirectsManager");

            TryAttachAndInit("TimeManager");
            TryAttachAndInit("EnvironmentManager");
            TryAttachAndInit("DynastyPlayerEffects");
            TryAttachAndInit("PermadeathManager");
            TryAttachAndInit("tradeInfluencePath");
            TryAttachAndInit("DynastyTick30MinManager");
            TryAttachAndInit("DynastyNpcSimManager");
            TryAttachAndInit("DynastyQuestContentManager");
            TryAttachAndInit("DynastyNpcInteractionUI");
            TryAttachAndInit("SoulEchoSystem");


            var menu = GetComponent<DynastyMenu>();
            if (menu == null) menu = gameObject.AddComponent<DynastyMenu>();
            menu.Initialize(this);

            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log($"[Dynasty] Core booted. Enabled={IsDynastyModeEnabled} DynastyStarted={MasterData.DynastyStarted} PlayerPlaced={MasterData.PlayerPlaced}");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (_startingDynasty)
            {
                // DO NOT release the start latch just because the character object exists.
                // Outward instantiates the local character BEFORE the first world scene load.
                // If we release here, DreamWorldLock stops redirecting and we fall back into Cierzo.
                // We only release once DreamWorld is actually active (or setup is fully completed).
                if (SetupComplete || SceneManager.GetActiveScene().name == DREAMWORLD_SCENE)
                {
                    _startingDynasty = false;
                    _showLoading = false;
                    Debug.Log("[Dynasty] StartingDynasty latch released (DreamWorld active or setup complete).");
                }
            }

            // Companion authority polling (halts game on desync / invalid state)
            try
            {
                var cc = CompanionClient.Instance;
                if (cc != null && DynastyEnabled)
                {
                    // Ensure dynasty id on the companion client without hard-coding an API surface.
                    // (Fixes CS1061 when CompanionClient does not expose Initialize()).
                    if (!_companionInitAttempted)
                    {
                        _companionInitAttempted = true;
                        var d = DynastyDataAccess.Get();
                        if (d != null && !string.IsNullOrEmpty(d.DynastyId))
                            EnsureCompanionHasDynastyId(cc, d.DynastyId);
                    }

                    if (cc.Halted)
                    {
                        if (!_authorityHaltApplied)
                        {
                            _preHaltTimescale = Time.timeScale;
                            _authorityHaltApplied = true;
                        }
                        Time.timeScale = 0f;
                    }
                    else if (_authorityHaltApplied)
                    {
                        Time.timeScale = Mathf.Clamp(_preHaltTimescale, 0.05f, 5f);
                        _authorityHaltApplied = false;
                    }
                }
            }
            catch { }

            // F6 debug: show faction "newspaper" overlay.
            // This lives in Core intentionally so it cannot be lost by excluding a separate .cs file from the VS project.
            try
            {
                if (!_newspaperLoggedReady)
                {
                    _newspaperLoggedReady = true;
                    Debug.Log("[Dynasty][Newspaper] Ready. Press F6 to open the faction debug paper.");
                }

                if (InputProxy.GetKeyDown(KeyCode.F6))
                {
                    if (_newspaper == null)
                        _newspaper = gameObject.GetComponent<DynastyNewspaperOverlay>();
                    if (_newspaper == null)
                        _newspaper = gameObject.AddComponent<DynastyNewspaperOverlay>();

                    Debug.Log("[Dynasty][Newspaper] Hotkey pressed (F6). Showing report.");
                    _newspaper.Show(15f);
                }
            }
            catch { /* never crash the game for a debug overlay */ }
        }

        private void OnGUI()
        {
            if (!_showLoading) return;

            if (_loadingStyle == null)
            {
                _loadingStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "Loading Dynasty Start.", _loadingStyle);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_startingDynasty)
            {
                // Keep the latch alive across transition scenes until we successfully enter DreamWorld.
                _startLatchUntilRealtime = Math.Max(_startLatchUntilRealtime, Time.realtimeSinceStartup + 1.25f);

                if (scene.name == DREAMWORLD_SCENE)
                {
                    _startingDynasty = false;
                    _showLoading = false;
                    Debug.Log("[Dynasty] StartingDynasty latch released (DreamWorld scene loaded).");
                }
            }
        }

        public void SetDynastyMode(bool enabled)
        {
            IsDynastyModeEnabled = enabled;

            if (MasterData == null)
                MasterData = new DynastySaveData();

            MasterData.DynastyModeEnabled = enabled;

            // If Dynasty is being enabled while setup is incomplete, engage the "starting dynasty" latch.
            // This is the intent signal DreamWorldLock uses to redirect the next world load to DreamWorld.
            if (enabled && (!MasterData.DynastyStarted || !MasterData.PlayerPlaced))
            {
                if (!_startingDynasty)
                {
                    _startingDynasty = true;
                    _startLatchUntilRealtime = Time.realtimeSinceStartup + 2.0f;
                    _showLoading = true;
                    Debug.Log("[Dynasty] SetDynastyMode: setup incomplete -> StartingDynasty latch engaged.");
                }
            }

            Debug.Log($"[Dynasty] SetDynastyMode -> {enabled}");
            SaveDynastySafe();
        }

        public void SaveDynasty() => SaveDynastySafe();

        public void SaveDynastySafe()
        {
            try
            {
                if (MasterData == null)
                    MasterData = new DynastySaveData();

                DynastySaveManager.Save(MasterData);
            }
            catch (Exception e)
            {
                Debug.LogError("[Dynasty] SaveDynasty failed: " + e);
            }
        }

        public static void CancelDynastySetup()
        {
            if (Instance == null) return;

            Instance._startingDynasty = false;
            Instance._showLoading = false;

            Instance.IsDynastyModeEnabled = false;

            if (Instance.MasterData == null)
                Instance.MasterData = new DynastySaveData();

            Instance.MasterData.DynastyModeEnabled = false;
            Instance.MasterData.DynastyStarted = false;
            Instance.MasterData.PlayerPlaced = false;

            Instance.SaveDynastySafe();
            Debug.Log("[Dynasty] CancelDynastySetup -> dynasty disabled");
        }

        public static void StartDynastyAt(string sceneName, string echoesText)
        {
            if (Instance == null) return;

            Instance.IsDynastyModeEnabled = true;
            if (Instance.MasterData == null)
                Instance.MasterData = new DynastySaveData();

            Instance.MasterData.DynastyModeEnabled = true;

            Instance.MasterData.DynastyStarted = false;
            Instance.MasterData.PlayerPlaced = false;
            Instance.SaveDynastySafe();

            var confirm = Instance.GetComponent<ConfirmSetup>();
            if (confirm != null)
            {
                Instance._startingDynasty = true;
                Instance._startLatchUntilRealtime = Time.realtimeSinceStartup + 2.0f;
                Instance._showLoading = true;

                string status;
                bool ok = confirm.ConfirmAndContinue(sceneName, sceneName, echoesText, out status);
                Debug.Log("[Dynasty] StartDynastyAt routed via ConfirmSetup -> " + ok + " (" + status + ")");
                return;
            }

            Debug.LogWarning("[Dynasty] ConfirmSetup missing; falling back to direct SceneManager.LoadScene (not recommended).");
            SceneManager.LoadScene(sceneName);
        }

        private bool HasLocalPlayerCharacter()
        {
            try
            {
                // Best-effort check; keep it simple and non-crashy
                return CharacterManager.Instance != null &&
                       CharacterManager.Instance.GetFirstLocalCharacter() != null;
            }
            catch { return false; }
        }

        private void TryAttachAndInit(string typeName)
        {
            try
            {
                var t = Type.GetType("OutwardDynasty." + typeName + ", OutwardDynasty");
                if (t == null) return;

                if (!typeof(MonoBehaviour).IsAssignableFrom(t)) return;

                if (GetComponent(t) != null) return;

                var c = gameObject.AddComponent(t) as MonoBehaviour;
                var mi = t.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null) mi.Invoke(c, new object[] { this });
            }
            catch { }
        }

        /// <summary>
        /// Keeps DynastyCore decoupled from the exact CompanionClient API surface.
        /// Tries, in order:
        /// 1) CompanionClient.Initialize(string dynastyId)
        /// 2) Set CompanionClient.DynastyId property/field
        /// 3) CompanionClient.Initialize() (no args)
        /// </summary>
        private static void EnsureCompanionHasDynastyId(object companionClient, string dynastyId)
        {
            if (companionClient == null || string.IsNullOrEmpty(dynastyId)) return;

            try
            {
                var t = companionClient.GetType();

                // 1) Initialize(string)
                var miInitStr = t.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                if (miInitStr != null)
                {
                    miInitStr.Invoke(companionClient, new object[] { dynastyId });
                    Debug.Log("[Dynasty][Companion] Initialized via Initialize(string).");
                    return;
                }

                // 2) DynastyId property/field
                var pi = t.GetProperty("DynastyId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null && pi.CanWrite && pi.PropertyType == typeof(string))
                {
                    pi.SetValue(companionClient, dynastyId, null);
                    Debug.Log("[Dynasty][Companion] Set DynastyId via property.");
                    return;
                }

                var fi = t.GetField("DynastyId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null && fi.FieldType == typeof(string))
                {
                    fi.SetValue(companionClient, dynastyId);
                    Debug.Log("[Dynasty][Companion] Set DynastyId via field.");
                    return;
                }

                // 3) Initialize() no args
                var miInit0 = t.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (miInit0 != null)
                {
                    miInit0.Invoke(companionClient, null);
                    Debug.Log("[Dynasty][Companion] Called Initialize() (no args). DynastyId could not be set directly.");
                    return;
                }

                Debug.LogWarning("[Dynasty][Companion] No Initialize method and no DynastyId member found. Companion integration may be inactive.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty][Companion] EnsureCompanionHasDynastyId failed (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Simple IMGUI overlay that prints Dynasty faction/town data.
        /// Lives inside DynastyCore so the VS project cannot accidentally exclude it.
        /// </summary>
        private class DynastyNewspaperOverlay : MonoBehaviour
        {
            private bool _visible;
            private float _hideAtRealtime;
            private Vector2 _scroll;

            private string _cachedTitle;
            private string _cachedText;

            private GUIStyle _titleStyle;
            private GUIStyle _bodyStyle;
            private GUIStyle _smallStyle;

            public void Show(float durationSeconds)
            {
                _cachedTitle = $"Dynasty Gazette — {DateTime.Now:yyyy-MM-dd HH:mm}";
                _cachedText = BuildReport();
                _visible = true;
                _hideAtRealtime = Time.realtimeSinceStartup + Mathf.Max(0.5f, durationSeconds);
            }

            private void Update()
            {
                if (_visible && Time.realtimeSinceStartup >= _hideAtRealtime)
                    _visible = false;
            }

            private void OnGUI()
            {
                if (!_visible) return;
                EnsureStyles();

                var pad = 18f;
                var w = Mathf.Min(Screen.width - 40f, 900f);
                var h = Mathf.Min(Screen.height - 40f, 650f);
                var x = (Screen.width - w) * 0.5f;
                var y = (Screen.height - h) * 0.5f;

                GUI.Box(new Rect(x, y, w, h), GUIContent.none);
                GUI.Label(new Rect(x + pad, y + pad, w - pad * 2, 40f), _cachedTitle ?? "Dynasty Gazette", _titleStyle);

                var bodyTop = y + pad + 46f;
                var bodyHeight = h - (pad + 46f) - (pad + 28f);
                var contentWidth = (w - pad * 2) - 20f;
                var viewRect = new Rect(0, 0, contentWidth, MeasureTextHeight(_cachedText, _bodyStyle, contentWidth));

                _scroll = GUI.BeginScrollView(
                    new Rect(x + pad, bodyTop, w - pad * 2, bodyHeight),
                    _scroll,
                    viewRect
                );

                GUI.Label(new Rect(0, 0, viewRect.width, viewRect.height), _cachedText ?? "(no data)", _bodyStyle);
                GUI.EndScrollView();

                var remaining = Mathf.Max(0f, _hideAtRealtime - Time.realtimeSinceStartup);
                GUI.Label(
                    new Rect(x + pad, y + h - pad - 22f, w - pad * 2, 22f),
                    $"Press F6 to refresh. Auto-closes in {remaining:0.0}s.",
                    _smallStyle
                );
            }

            private void EnsureStyles()
            {
                if (_titleStyle != null) return;

                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };

                _bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    richText = false
                };

                _smallStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.LowerLeft,
                    wordWrap = true
                };
            }

            private static float MeasureTextHeight(string text, GUIStyle style, float width)
            {
                if (string.IsNullOrEmpty(text)) return 20f;
                return style.CalcHeight(new GUIContent(text), width) + 6f;
            }

            private static string BuildReport()
            {
                var core = DynastyCore.Instance;
                if (core == null || core.MasterData == null)
                    return "DynastyCore or MasterData not available.\n\nIf this persists, your save load / DynastySaveManager may be failing.";

                var data = core.MasterData;
                var lines = new List<string>();

                lines.Add("=== DYNASTY FACTIONS (DEBUG) ===");
                lines.Add($"DynastyModeEnabled: {data.DynastyModeEnabled}");
                lines.Add($"DynastyStarted:    {data.DynastyStarted}");
                lines.Add($"PlayerPlaced:     {data.PlayerPlaced}");
                lines.Add(string.Empty);

                if (data.Factions == null || data.Factions.Count == 0)
                {
                    lines.Add("No factions found in save data.");
                    lines.Add("Expected: FactionsLibrary.EnsureDefaults should seed factions on first boot.");
                    return string.Join("\n", lines);
                }

                for (int i = 0; i < data.Factions.Count; i++)
                {
                    var f = data.Factions[i];
                    if (f == null) continue;
                    lines.Add($"[{i}] {(f.Name ?? f.ToString())}");
                    lines.Add($"  Treasury: {f.Treasury:0}");
                    lines.Add($"  Bankrupt: {f.Bankrupt}");
                    lines.Add($"  ActiveWars: {(f.ActiveWars == null ? 0 : f.ActiveWars.Count)}");
                    if (f.IsTrogFaction)
                        lines.Add($"  TrogFamine: {f.TrogFamineStat:0.00}");
                    lines.Add(string.Empty);
                }

                if (data.Towns != null && data.Towns.Count > 0)
                {
                    lines.Add("=== TOWNS (DEBUG) ===");
                    for (int i = 0; i < data.Towns.Count; i++)
                    {
                        var t = data.Towns[i];
                        if (t == null) continue;
                        var townId = TryGetString(t, "TownID") ?? TryGetString(t, "Name") ?? "(unknown town)";
                        var owner = TryGetString(t, "OwnerFaction") ?? "(no owner)";
                        lines.Add($"- {townId}  |  Owner: {owner}");
                    }
                    lines.Add(string.Empty);
                }

                lines.Add("=== NOTE ===");
                lines.Add("If ASM faction menu is empty but this paper shows factions, the issue is UI hijack/injection, not save data.");
                return string.Join("\n", lines);
            }

            private static string TryGetString(object obj, string prop)
            {
                try
                {
                    var pi = obj.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pi == null) return null;
                    return pi.GetValue(obj, null) as string;
                }
                catch { return null; }
            }
        }
    }
}
// DYNASTY MARSHMELLOW
