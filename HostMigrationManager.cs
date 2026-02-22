using System;
using UnityEngine;

namespace OutwardDynasty
{
    public static class HostMigrationManager
    {
        public static bool IsMigrationInProgress { get; private set; }
        public static string CurrentHostMemberGuid { get; private set; } = "";

        private static bool _voted;

        public static void Begin(string reason)
        {
            if (IsMigrationInProgress) return;
            IsMigrationInProgress = true;
            DynastyHistory.LogEvent("host_migration_begin", new System.Collections.Generic.Dictionary<string, object>{{"reason", reason}});
            _voted = false;

            try { AuthorityFreezeManager.Instance?.BeginFreeze("Host migration: " + reason); } catch { }

            try
            {
                var cc = CompanionClient.Instance;
                if (cc != null)
                {
                    // If we are the (still alive) current host, push a snapshot before election.
                    try
                    {
                        if (cc.LocalIsHost && DynastyCore.Instance != null && DynastyCore.Instance.MasterData != null)
                        {
                            string snap = DynastySnapshotManager.BuildSnapshotJson(DynastyCore.Instance.MasterData);
                            DynastyLocalCommitStore.SaveLatest(snap);
                            cc.PushSnapshot(snap, out var _);
                        }
                    }
                    catch { }

                    cc.StartHostElection(reason, out var _);
                }
            }
            catch { }
        }

        public static void Tick()
        {
            if (!IsMigrationInProgress) return;

            // Auto-vote for self (baseline). Companion tiebreaks among votes.
            if (!_voted)
            {
                _voted = true;
                try
                {
                    var cc = CompanionClient.Instance;
                    if (cc != null)
                    {
                        cc.SendHostVote(DynastyIdentity.MemberGuid, out var _);
                    }
                }
                catch { }
            }

            // If Companion never resolves, we stay frozen until it does (by design).
        }

        public static void OnCompanionAck(object ackObj)
        {
            if (ackObj == null) return;

            try
            {
                var t = ackObj.GetType();

                bool mig = GetBool(t, ackObj, "migrationInProgress", false);
                string newHost = GetString(t, ackObj, "newHostClientId", "");
                string snap = GetString(t, ackObj, "snapshotJson", "");

                if (mig) IsMigrationInProgress = true;
            DynastyHistory.LogEvent("host_migration_begin", new System.Collections.Generic.Dictionary<string, object>{{"reason", reason}});

                if (!string.IsNullOrEmpty(newHost))
                {
                    CurrentHostMemberGuid = newHost;

                    // If we are the new host and Companion provided a snapshot, apply it.
                    if (string.Equals(newHost, DynastyIdentity.MemberGuid, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(snap) &&
                        DynastyCore.Instance != null)
                    {
                        DynastySnapshotManager.TryApplySnapshotJson(snap, DynastyCore.Instance, out var _);
                        DynastyLocalCommitStore.SaveLatest(snap);
                    }

                    // Migration complete
                    IsMigrationInProgress = false;
                    try { AuthorityFreezeManager.Instance?.EndFreeze(); } catch { }
                }
            }
            catch { }
        }

        private static bool GetBool(Type t, object obj, string name, bool def)
        {
            var f = t.GetField(name);
            if (f != null && f.FieldType == typeof(bool))
            {
                try { return (bool)f.GetValue(obj); } catch { }
            }
            var p = t.GetProperty(name);
            if (p != null && p.PropertyType == typeof(bool))
            {
                try { return (bool)p.GetValue(obj, null); } catch { }
            }
            return def;
        }

        private static string GetString(Type t, object obj, string name, string def)
        {
            var f = t.GetField(name);
            if (f != null && f.FieldType == typeof(string))
            {
                try { return (string)f.GetValue(obj); } catch { }
            }
            var p = t.GetProperty(name);
            if (p != null && p.PropertyType == typeof(string))
            {
                try { return (string)p.GetValue(obj, null); } catch { }
            }
            return def;
        }
    }
}
