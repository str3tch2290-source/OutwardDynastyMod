// ======================================================
// DynastyPlacementGuard.cs
//
// Fixes compile errors:
// - Does NOT use core.Enabled
// - Does NOT use core.LogInfo
// - Does NOT use BaseUnityPlugin.Logger (protected/inaccessible in your setup)
//
// Uses a standalone BepInEx log source.
//
// ======================================================

using System;
using System.Reflection;
using BepInEx.Logging;

namespace OutwardDynasty
{
    public static class DynastyPlacementGuard
    {
        // If true, we try to detect a dynasty enabled flag via reflection.
        // If no flag is found, we assume enabled.
        private const bool RespectDynastyEnabledFlagIfFound = true;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("DynastyPlacementGuard");

        /// <summary>
        /// Call this when you have finished placing the player (DreamWorld exit, first town spawn, etc.)
        /// This is the key that stops DreamWorld redirect loops:
        ///   MasterData.PlayerPlaced = true;
        ///   SaveDynasty();
        /// </summary>
        public static void MarkPlayerPlaced(DynastyCore core, string reason = null)
        {
            if (core == null)
            {
                Log.LogWarning("[DynastyPlacementGuard] MarkPlayerPlaced called with null core.");
                return;
            }

            if (RespectDynastyEnabledFlagIfFound && !IsDynastyEnabled(core))
            {
                Log.LogInfo($"[DynastyPlacementGuard] Dynasty appears disabled; not marking PlayerPlaced. ({reason ?? "no reason"})");
                return;
            }

            try
            {
                bool didSet = TrySetPlayerPlaced(core, true);
                bool didSave = TryInvokeSaveDynasty(core);

                Log.LogInfo($"[DynastyPlacementGuard] PlayerPlaced finalized. set={didSet} save={didSave} ({reason ?? "no reason"})");
            }
            catch (Exception ex)
            {
                Log.LogError($"[DynastyPlacementGuard] Failed to mark PlayerPlaced: {ex}");
            }
        }

        /// <summary>
        /// Optional: call repeatedly; it will only mark once if not already placed.
        /// </summary>
        public static void EnsurePlacedOnce(DynastyCore core, string reason = null)
        {
            if (core == null) return;

            if (RespectDynastyEnabledFlagIfFound && !IsDynastyEnabled(core))
                return;

            if (TryGetPlayerPlaced(core, out bool placed) && placed)
                return;

            MarkPlayerPlaced(core, reason ?? "EnsurePlacedOnce");
        }

        // -----------------------
        // Reflection helpers
        // -----------------------

        private static bool IsDynastyEnabled(DynastyCore core)
        {
            try
            {
                var t = core.GetType();

                // property candidates
                string[] propNames = { "Enabled", "IsEnabled", "DynastyEnabled", "ModeEnabled", "IsDynastyEnabled" };
                foreach (var name in propNames)
                {
                    var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.PropertyType == typeof(bool))
                        return (bool)p.GetValue(core);
                }

                // field candidates
                string[] fieldNames = { "Enabled", "_enabled", "m_enabled", "DynastyEnabled", "_dynastyEnabled", "ModeEnabled" };
                foreach (var name in fieldNames)
                {
                    var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType == typeof(bool))
                        return (bool)f.GetValue(core);
                }

                // ConfigEntry<bool> candidates (read .Value)
                string[] configNames = { "ConfigEnabled", "_configEnabled", "DynastyEnabledConfig", "_dynastyEnabledConfig" };
                foreach (var name in configNames)
                {
                    var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f == null) continue;

                    var ft = f.FieldType;
                    if (!ft.IsGenericType) continue;

                    // "ConfigEntry`1" etc
                    var genDef = ft.GetGenericTypeDefinition();
                    if (genDef == null) continue;

                    if (genDef.Name.Contains("ConfigEntry"))
                    {
                        var valProp = ft.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
                        if (valProp != null && valProp.PropertyType == typeof(bool))
                        {
                            object cfg = f.GetValue(core);
                            return cfg != null && (bool)valProp.GetValue(cfg);
                        }
                    }
                }
            }
            catch
            {
                // ignore and default true
            }

            // If we can't find an enabled flag, assume enabled so nothing breaks.
            return true;
        }

        private static bool TrySetPlayerPlaced(DynastyCore core, bool value)
        {
            var t = core.GetType();

            // Find MasterData (property or field)
            object masterData = null;
            var mdProp = t.GetProperty("MasterData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mdProp != null) masterData = mdProp.GetValue(core);

            if (masterData == null)
            {
                var mdField = t.GetField("MasterData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mdField != null) masterData = mdField.GetValue(core);
            }

            if (masterData == null)
            {
                Log.LogWarning("[DynastyPlacementGuard] Could not find MasterData on DynastyCore.");
                return false;
            }

            var mdt = masterData.GetType();

            // Set PlayerPlaced (property or field)
            var ppProp = mdt.GetProperty("PlayerPlaced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ppProp != null && ppProp.PropertyType == typeof(bool))
            {
                ppProp.SetValue(masterData, value);
                return true;
            }

            var ppField = mdt.GetField("PlayerPlaced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ppField != null && ppField.FieldType == typeof(bool))
            {
                ppField.SetValue(masterData, value);
                return true;
            }

            Log.LogWarning("[DynastyPlacementGuard] MasterData found but no bool PlayerPlaced field/property.");
            return false;
        }

        private static bool TryGetPlayerPlaced(DynastyCore core, out bool value)
        {
            value = false;

            var t = core.GetType();

            object masterData = null;
            var mdProp = t.GetProperty("MasterData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mdProp != null) masterData = mdProp.GetValue(core);

            if (masterData == null)
            {
                var mdField = t.GetField("MasterData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mdField != null) masterData = mdField.GetValue(core);
            }

            if (masterData == null) return false;

            var mdt = masterData.GetType();

            var ppProp = mdt.GetProperty("PlayerPlaced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ppProp != null && ppProp.PropertyType == typeof(bool))
            {
                value = (bool)ppProp.GetValue(masterData);
                return true;
            }

            var ppField = mdt.GetField("PlayerPlaced", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ppField != null && ppField.FieldType == typeof(bool))
            {
                value = (bool)ppField.GetValue(masterData);
                return true;
            }

            return false;
        }

        private static bool TryInvokeSaveDynasty(DynastyCore core)
        {
            var t = core.GetType();

            // common method names
            string[] names = { "SaveDynasty", "Save", "SaveMasterData", "SaveState" };
            foreach (var n in names)
            {
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null) continue;

                if (m.GetParameters().Length == 0)
                {
                    m.Invoke(core, null);
                    return true;
                }
            }

            Log.LogWarning("[DynastyPlacementGuard] Could not find SaveDynasty() (or equivalent) on DynastyCore.");
            return false;
        }
    }
}
