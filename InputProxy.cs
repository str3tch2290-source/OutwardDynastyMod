using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Reflection-based wrapper around UnityEngine.Input so we don't hard-bind to a specific UnityEngine module.
    /// Outward DE (Unity 2020) places Input in UnityEngine.InputLegacyModule, but some builds may bind it to CoreModule.
    /// Using reflection avoids TypeLoadException when the module name differs.
    /// </summary>
    internal static class InputProxy
    {
        private static bool _init;
        private static Type _inputType;

        private static MethodInfo _getKeyDown;
        private static MethodInfo _getKey;
        private static MethodInfo _getKeyUp;

        private static bool Ensure()
        {
            if (_init) return _inputType != null;
            _init = true;

            // Try common module-qualified names first.
            _inputType =
                Type.GetType("UnityEngine.Input, UnityEngine.InputLegacyModule", false) ??
                Type.GetType("UnityEngine.Input, UnityEngine.CoreModule", false) ??
                Type.GetType("UnityEngine.Input, UnityEngine", false);

            if (_inputType == null)
            {
                // Fallback: scan loaded assemblies.
                try
                {
                    _inputType = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Select(a =>
                        {
                            try { return a.GetType("UnityEngine.Input", false); }
                            catch { return null; }
                        })
                        .FirstOrDefault(t => t != null);
                }
                catch
                {
                    _inputType = null;
                }
            }

            if (_inputType == null) return false;

            // Resolve methods (public static bool GetKeyDown(KeyCode) etc.)
            var flags = BindingFlags.Public | BindingFlags.Static;
            var keyCodeType = typeof(KeyCode);

            _getKeyDown = _inputType.GetMethod("GetKeyDown", flags, null, new Type[] { keyCodeType }, null);
            _getKey = _inputType.GetMethod("GetKey", flags, null, new Type[] { keyCodeType }, null);
            _getKeyUp = _inputType.GetMethod("GetKeyUp", flags, null, new Type[] { keyCodeType }, null);

            return true;
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (!Ensure() || _getKeyDown == null) return false;
            try { return (bool)_getKeyDown.Invoke(null, new object[] { key }); }
            catch { return false; }
        }

        public static bool GetKey(KeyCode key)
        {
            if (!Ensure() || _getKey == null) return false;
            try { return (bool)_getKey.Invoke(null, new object[] { key }); }
            catch { return false; }
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (!Ensure() || _getKeyUp == null) return false;
            try { return (bool)_getKeyUp.Invoke(null, new object[] { key }); }
            catch { return false; }
        }
    }
}
