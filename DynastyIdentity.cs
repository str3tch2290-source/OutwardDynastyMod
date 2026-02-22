using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Stable per-install Dynasty Member GUID.
    /// This is the identity sent to the Companion Authority (not SteamID, not character UID).
    /// </summary>
    public static class DynastyIdentity
    {
        private static string _memberGuid;
        private static readonly object _lock = new object();

        public static string MemberGuid
        {
            get
            {
                lock (_lock)
                {
                    if (!string.IsNullOrEmpty(_memberGuid)) return _memberGuid;
                    _memberGuid = LoadOrCreate();
                    return _memberGuid;
                }
            }
        }

        private static string LoadOrCreate()
        {
            try
            {
                string cfgDir = Paths.ConfigPath;
                if (string.IsNullOrEmpty(cfgDir))
                    cfgDir = Application.persistentDataPath;

                string path = Path.Combine(cfgDir, "OutwardDynasty_memberGuid.txt");
                if (File.Exists(path))
                {
                    var txt = File.ReadAllText(path).Trim();
                    if (Guid.TryParse(txt, out var g))
                        return g.ToString();
                }

                var guid = Guid.NewGuid().ToString();
                try { File.WriteAllText(path, guid); } catch { /* ignore */ }
                return guid;
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
