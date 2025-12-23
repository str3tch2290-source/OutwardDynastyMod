using System;
using System.Reflection;

namespace OutwardDynasty
{
    internal static class CustomAttributesCompat
    {
        // Tries to hook CustomAttributes.CustomAttributesBase.OnCharacterLeechEvent if it exists.
        // If the assembly/type/member is missing or renamed, it silently does nothing.
        public static void TryHookLeechEvent(EventHandler handler)
        {
            if (handler == null) return;

            // Try find type in loaded assemblies
            var t = FindType("CustomAttributes.CustomAttributesBase");
            if (t == null) return;

            // The crash you showed was “Field ... not found”,
            // but in many libs this is an event. We try both.
            var evt = t.GetEvent("OnCharacterLeechEvent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (evt != null)
            {
                evt.AddEventHandler(null, handler);
                if (Plugin.Log != null) Plugin.Log.LogInfo("Hooked CustomAttributes OnCharacterLeechEvent (event).");
                return;
            }

            var field = t.GetField("OnCharacterLeechEvent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                // Field might be a delegate; we can combine if compatible.
                var existing = field.GetValue(null) as Delegate;
                var combined = Delegate.Combine(existing, handler);
                field.SetValue(null, combined);
                if (Plugin.Log != null) Plugin.Log.LogInfo("Hooked CustomAttributes OnCharacterLeechEvent (field).");
                return;
            }

            // Not found => do nothing (optional dependency behavior).
        }

        private static Type FindType(string fullName)
        {
            // Prefer Type.GetType if assembly-qualified is known (we don’t know it),
            // so scan loaded assemblies.
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var t = asms[i].GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
