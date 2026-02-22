// =======================================================
// Dynasty – Faction Debug Newspaper (SELF-CONTAINED)
// Shows Dynasty faction data in an in-world "newspaper" overlay for ~15 seconds.
//
// Trigger: hotkey (default F6)
//
// IMPORTANT (why this exists):
// Your mod calls harmony.PatchAll() INSIDE DynastyCore.Awake().
// That means any Harmony patch targeting DynastyCore.Awake() will NOT run for the
// currently-executing Awake call (patch is applied mid-flight).
//
// So this file bootstraps on DynastyCore.Update() instead (first frame after Awake).
//
// CODEWORD: DYNASTY MARSHMELLOW
// =======================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Bootstraps the newspaper system without requiring edits to existing files.
    /// We hook DynastyCore.Update() (which runs AFTER Awake has finished) to ensure
    /// the bootstrap actually executes.
    /// </summary>
    [HarmonyPatch(typeof(DynastyCore), "Update")]
    internal static class FactionDebugNewspaper_Bootstrap
    {
        private static bool _attached;

        private static void Postfix(DynastyCore __instance)
        {
            try
            {
                if (_attached) return;
                if (__instance == null) return;
                if (__instance.gameObject == null) return;

                if (__instance.GetComponent<FactionDebugNewspaperSystem>() == null)
                    __instance.gameObject.AddComponent<FactionDebugNewspaperSystem>();

                _attached = true;
                Debug.Log("[Dynasty][Newspaper] Attached to DynastyCore. Press F6 in-game to open.");

                // Optional: once attached, we can unpatch ourselves to reduce overhead.
                // Safe because DynastyCore already exists and component is now driving everything.
                try
                {
                    var harmony = new Harmony(DynastyCore.GUID + ".newspaper.bootstrap");
                    harmony.Unpatch(MethodBase(__instance.GetType(), "Update"), HarmonyPatchType.Postfix, DynastyCore.GUID);
                }
                catch
                {
                    // Non-fatal. Leaving the postfix is harmless.
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty][Newspaper] Bootstrap failed (non-fatal): " + e);
            }
        }

        private static MethodInfo MethodBase(Type t, string name)
        {
            return t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    /// <summary>
    /// Minimal, Unity-safe overlay that prints Dynasty faction information.
    /// Designed as a debugging workaround when ASM UI injection/hijack isn't reliable.
    /// </summary>
    internal class FactionDebugNewspaperSystem : MonoBehaviour
    {
        private const float DefaultDurationSeconds = 15f;
        // Hotkey: Shift+F6
        private const KeyCode Hotkey = KeyCode.F6;

        private bool _visible;
        private float _hideAtRealtime;

        private string _cachedText;
        private string _cachedTitle;
        private Vector2 _scroll;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;

        private bool _loggedReady;

        private void Update()
        {
            if (!_loggedReady)
            {
                _loggedReady = true;
                Debug.Log("[Dynasty][Newspaper] Ready. Press F6 to open the faction debug paper.");
            }

            // Trigger: hotkey
            if (InputProxy.GetKeyDown(Hotkey))
            {
                Debug.Log("[Dynasty][Newspaper] Hotkey pressed (F6). Showing report.");
                Show(DefaultDurationSeconds);
            }

            // Auto-hide
            if (_visible && Time.realtimeSinceStartup >= _hideAtRealtime)
                _visible = false;
        }

        public void Show(float durationSeconds)
        {
            _cachedTitle = BuildTitle();
            _cachedText = BuildFactionReport();

            _visible = true;
            _hideAtRealtime = Time.realtimeSinceStartup + Mathf.Max(0.5f, durationSeconds);
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
                $"Press {Hotkey} to refresh. Auto-closes in {remaining:0.0}s.",
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

        private static string BuildTitle()
        {
            var date = DateTime.Now;
            return $"Dynasty Gazette — {date:yyyy-MM-dd HH:mm}";
        }

        private static string BuildFactionReport()
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

                lines.Add($"[{i}] {Safe(f.Name ?? f.ToString())}");

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

                    var townId = Safe(TryGetString(t, "TownID") ?? TryGetString(t, "Name") ?? "(unknown town)");
                    var owner  = Safe(TryGetString(t, "OwnerFaction") ?? "(no owner)");

                    lines.Add($"- {townId}  |  Owner: {owner}");
                }
                lines.Add(string.Empty);
            }

            lines.Add("=== NOTE ===");
            lines.Add("If ASM faction menu is empty but this paper shows factions, the issue is UI hijack/injection, not save data.");

            return string.Join("\n", lines);
        }

        private static string Safe(string s) => string.IsNullOrEmpty(s) ? "(null)" : s;

        private static int? TryGetInt(object obj, string fieldOrProp)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            var f = t.GetField(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(obj);

            var p = t.GetProperty(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(int))
                return (int)p.GetValue(obj, null);

            return null;
        }

        private static string TryGetString(object obj, string fieldOrProp)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            var f = t.GetField(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
                return (string)f.GetValue(obj);

            var p = t.GetProperty(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string))
                return (string)p.GetValue(obj, null);

            return null;
        }

        private static void TryAppendInt(List<string> lines, string label, int? value)
        {
            if (value.HasValue) lines.Add($"{label}: {value.Value}");
        }
    }
}
