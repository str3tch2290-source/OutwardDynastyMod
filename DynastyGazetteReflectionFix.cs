// =======================================================
// Dynasty – Gazette Reflection Fix (version-safe)
// Keeps the Gazette feature while preventing hard crashes when the game's
// internal methods move/rename between Outward versions.
//
// This file patches DynastyCore's nested DynastyNewspaperOverlay reflection helpers
// so they can read BOTH properties and fields (public or non-public).
//
// IMPORTANT:
// - Every patch is guarded by Prepare(). If the target method isn't found,
//   Harmony will SKIP the patch (no crash).
// - TryGetInt has two variants across builds (int? and int). We support both.
// =======================================================

using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace OutwardDynasty
{
    internal static class DynastyGazetteReflectionHelpers
    {
        internal static string GetString(object obj, string fieldOrProp)
        {
            if (obj == null || string.IsNullOrEmpty(fieldOrProp))
                return null;

            try
            {
                var t = obj.GetType();

                // Field first (covers backing fields / older data classes)
                var f = t.GetField(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(string))
                    return (string)f.GetValue(obj);

                // Then property
                var p = t.GetProperty(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(string))
                    return (string)p.GetValue(obj, null);

                return null;
            }
            catch
            {
                return null;
            }
        }

        internal static int? GetNullableInt(object obj, string fieldOrProp)
        {
            if (obj == null || string.IsNullOrEmpty(fieldOrProp))
                return null;

            try
            {
                var t = obj.GetType();

                var f = t.GetField(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    if (f.FieldType == typeof(int?)) return (int?)f.GetValue(obj);
                    if (f.FieldType == typeof(int)) return (int)f.GetValue(obj);
                }

                var p = t.GetProperty(fieldOrProp, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    if (p.PropertyType == typeof(int?)) return (int?)p.GetValue(obj, null);
                    if (p.PropertyType == typeof(int)) return (int)p.GetValue(obj, null);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        internal static int GetInt(object obj, string fieldOrProp)
        {
            // If not found, default to 0 (matches common "TryGet" fallback behavior)
            return GetNullableInt(obj, fieldOrProp) ?? 0;
        }
    }

    // --- TryGetString(object, string) -> string ---
    [HarmonyPatch]
    internal static class DynastyGazetteReflectionFix_TryGetString
    {
        private static MethodBase _target;

        static bool Prepare()
        {
            var nested = AccessTools.Inner(typeof(DynastyCore), "DynastyNewspaperOverlay");
            if (nested == null) return false;

            _target = AccessTools.Method(nested, "TryGetString", new[] { typeof(object), typeof(string) });
            return _target != null;
        }

        static MethodBase TargetMethod() => _target;

        static bool Prefix(object obj, string prop, ref string __result)
        {
            __result = DynastyGazetteReflectionHelpers.GetString(obj, prop);
            return false; // skip original
        }
    }

    // --- TryGetInt(object, string) -> int? ---
    [HarmonyPatch]
    internal static class DynastyGazetteReflectionFix_TryGetInt_Nullable
    {
        private static MethodInfo _target;

        static bool Prepare()
        {
            var nested = AccessTools.Inner(typeof(DynastyCore), "DynastyNewspaperOverlay");
            if (nested == null) return false;

            _target = AccessTools.Method(nested, "TryGetInt", new[] { typeof(object), typeof(string) }) as MethodInfo;
            return _target != null && _target.ReturnType == typeof(int?);
        }

        static MethodBase TargetMethod() => _target;

        static bool Prefix(object obj, string prop, ref int? __result)
        {
            __result = DynastyGazetteReflectionHelpers.GetNullableInt(obj, prop);
            return false; // skip original
        }
    }

    // --- TryGetInt(object, string) -> int ---
    [HarmonyPatch]
    internal static class DynastyGazetteReflectionFix_TryGetInt_Int
    {
        private static MethodInfo _target;

        static bool Prepare()
        {
            var nested = AccessTools.Inner(typeof(DynastyCore), "DynastyNewspaperOverlay");
            if (nested == null) return false;

            _target = AccessTools.Method(nested, "TryGetInt", new[] { typeof(object), typeof(string) }) as MethodInfo;
            return _target != null && _target.ReturnType == typeof(int);
        }

        static MethodBase TargetMethod() => _target;

        static bool Prefix(object obj, string prop, ref int __result)
        {
            __result = DynastyGazetteReflectionHelpers.GetInt(obj, prop);
            return false; // skip original
        }
    }
}
