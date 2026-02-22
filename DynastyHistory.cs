using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Lightweight local play-history log (JSONL) so history is accessible even when disconnected.
    /// Also mirrors events to the Companion (best-effort) via CompanionClient.SendHistoryEvent().
    /// </summary>
    public static class DynastyHistory
    {
        private static readonly object _lock = new object();
        private static string _cachedPath;

        public static string GetHistoryPath(string dynastyId)
        {
            if (string.IsNullOrEmpty(dynastyId))
                dynastyId = "unknown";
            var safe = dynastyId.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            var dir = Path.Combine(Paths.ConfigPath, "OutwardDynasty");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "history_" + safe + ".jsonl");
        }

        public static void LogEvent(string type, Dictionary<string, object> fields = null)
        {
            try
            {
                var core = DynastyCore.Instance;
                var did = core != null ? (core.MasterData != null ? core.MasterData.DynastyId : "") : "";
                if (string.IsNullOrEmpty(_cachedPath) || !_cachedPath.Contains(did))
                    _cachedPath = GetHistoryPath(did);

                var payload = new HistoryEvent
                {
                    tsUtc = DateTime.UtcNow.ToString("o"),
                    dynastyId = did,
                    clientId = CompanionClient.Instance != null ? CompanionClient.Instance.ClientId : SystemInfo.deviceUniqueIdentifier,
                    memberGuid = DynastyIdentity.MemberGuid,
                    type = type,
                    fields = fields ?? new Dictionary<string, object>()
                };

                string json = MiniJson.Serialize(payload);

                lock (_lock)
                {
                    File.AppendAllText(_cachedPath, json + "\n");
                }

                // Best-effort mirror to Companion
                try
                {
                    var cc = CompanionClient.Instance;
                    if (cc != null && cc.Connected)
                        cc.SendHistoryEvent(json, out var _);
                }
                catch { }
            }
            catch { }
        }

        [Serializable]
        private class HistoryEvent
        {
            public string tsUtc;
            public string dynastyId;
            public string clientId;
            public string memberGuid;
            public string type;
            public Dictionary<string, object> fields;
        }

        /// <summary>
        /// Tiny JSON serializer (enough for our simple event payloads).
        /// </summary>
        private static class MiniJson
        {
            public static string Serialize(object obj)
            {
                try
                {
                    // Unity JsonUtility can't serialize Dictionary well; do a minimal manual serializer.
                    if (obj is HistoryEvent he)
                    {
                        return "{" +
                               "\"tsUtc\":" + S(he.tsUtc) + "," +
                               "\"dynastyId\":" + S(he.dynastyId) + "," +
                               "\"clientId\":" + S(he.clientId) + "," +
                               "\"memberGuid\":" + S(he.memberGuid) + "," +
                               "\"type\":" + S(he.type) + "," +
                               "\"fields\":" + SerializeDict(he.fields) +
                               "}";
                    }
                }
                catch { }
                return "{}";
            }

            private static string SerializeDict(Dictionary<string, object> d)
            {
                if (d == null) return "{}";
                var first = true;
                var s = "{";
                foreach (var kv in d)
                {
                    if (kv.Key == null) continue;
                    if (!first) s += ",";
                    first = false;
                    s += S(kv.Key) + ":" + SerializeValue(kv.Value);
                }
                s += "}";
                return s;
            }

            private static string SerializeValue(object v)
            {
                if (v == null) return "null";
                if (v is bool b) return b ? "true" : "false";
                if (v is int || v is long || v is float || v is double) return v.ToString().Replace(",", ".");
                return S(v.ToString());
            }

            private static string S(string s)
            {
                if (s == null) s = "";
                s = s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                return "\"" + s + "\"";
            }
        }
    }
}
