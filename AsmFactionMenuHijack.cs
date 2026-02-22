using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OutwardDynasty
{
    /// <summary>
    /// Unity-version-safe dropdown hijack helper.
    /// Outward's Unity version does not support FindObjectsOfType<T>(bool includeInactive),
    /// so we use Resources.FindObjectsOfTypeAll instead.
    /// </summary>
    public static class AsmFactionMenuHijack
    {
        private static bool _didRun = false;

        /// <summary>
        /// Old call-site compatibility: DynastyCore calls Apply(harmony, logger) to "enable" ASM hijacking.
        /// We keep this overload as a safe no-op initializer. The actual UI option injection is done by Apply(List&lt;string&gt;).
        /// </summary>
        public static void Apply(Harmony harmony, ManualLogSource logger)
        {
            try
            {
                logger?.LogInfo("[Dynasty][ASM] AsmFactionMenuHijack.Apply(Harmony, Logger) initialized.");
            }
            catch { }

            // Intentionally no Harmony patching here: this helper only performs runtime UI option injection.
            // If you later add Harmony patches for ASM screens, do it here.
        }

        /// <summary>
        /// Backwards-compatible entrypoint for injecting dropdown options.
        /// </summary>
        public static void Apply(List<string> names) => TryHijack(names);

        /// <summary>
        /// Attempts to locate any Dropdown (and dropdown-like components) and replace options with provided names.
        /// Includes inactive objects using Resources.FindObjectsOfTypeAll.
        /// </summary>
        public static void TryHijack(List<string> names)
        {
            if (names == null) names = new List<string>();
            if (_didRun) return;

            bool did = false;

            // Older-Unity-safe way: includes inactive objects
            foreach (var dd in Resources.FindObjectsOfTypeAll<Dropdown>())
            {
                if (dd == null) continue;
                if (!dd.gameObject.scene.IsValid()) continue; // skip prefabs/assets

                dd.ClearOptions();
                dd.AddOptions(names);
                did = true;
            }

            // Fallback: reflection-based for custom dropdown-like components
            foreach (var comp in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (comp == null) continue;
                if (!comp.gameObject.scene.IsValid()) continue;

                var tmpType = comp.GetType();
                if (tmpType == null) continue;

                var clear = tmpType.GetMethod("ClearOptions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var add = tmpType.GetMethod(
                    "AddOptions",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(List<string>) },
                    null
                );

                if (clear == null || add == null) continue;

                clear.Invoke(comp, null);
                add.Invoke(comp, new object[] { names });
                did = true;
            }

            _didRun = did;
        }
    }
}
