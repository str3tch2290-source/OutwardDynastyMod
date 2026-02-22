using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OutwardDynasty
{
    [Serializable]
    public class DynastySnapshotEnvelope
    {
        public string dynastyJson;
        public List<PlayerInventorySnapshot> players = new List<PlayerInventorySnapshot>();
    }

    [Serializable]
    public class PlayerInventorySnapshot
    {
        public string memberGuid;
        public List<ItemStackSnapshot> items = new List<ItemStackSnapshot>();
    }

    [Serializable]
    public class ItemStackSnapshot
    {
        public int itemID;
        public int qty;
    }

    public static class DynastySnapshotManager
    {
        public static string BuildSnapshotJson(DynastySaveData masterData)
        {
            var env = new DynastySnapshotEnvelope();
            env.dynastyJson = JsonUtility.ToJson(masterData ?? new DynastySaveData());

            try
            {
                var p = new PlayerInventorySnapshot { memberGuid = DynastyIdentity.MemberGuid };
                CaptureLocalInventory(p);
                env.players.Add(p);
            }
            catch { }

            return JsonUtility.ToJson(env);
        }

        public static bool TryApplySnapshotJson(string snapshotJson, DynastyCore core, out string status)
        {
            status = null;
            if (string.IsNullOrEmpty(snapshotJson))
            {
                status = "Empty snapshot.";
                return false;
            }

            try
            {
                var env = JsonUtility.FromJson<DynastySnapshotEnvelope>(snapshotJson);
                if (env == null || string.IsNullOrEmpty(env.dynastyJson))
                {
                    status = "Malformed snapshot envelope.";
                    return false;
                }

                var data = JsonUtility.FromJson<DynastySaveData>(env.dynastyJson);
                if (data != null)
                {
                    core.MasterData = data;
                    DynastySaveManager.Save(core.MasterData);
                }

                // Inventory restore is best-effort; only restore local member's items if present.
                try
                {
                    if (env.players != null)
                    {
                        foreach (var pl in env.players)
                        {
                            if (pl == null) continue;
                            if (string.Equals(pl.memberGuid, DynastyIdentity.MemberGuid, StringComparison.OrdinalIgnoreCase))
                            {
                                RestoreLocalInventory(pl);
                                break;
                            }
                        }
                    }
                }
                catch { }

                status = "Snapshot applied.";
                return true;
            }
            catch (Exception ex)
            {
                status = "Snapshot apply failed: " + ex.Message;
                return false;
            }
        }

        private static void CaptureLocalInventory(PlayerInventorySnapshot snap)
        {
            var c = CharacterManager.Instance != null ? CharacterManager.Instance.GetFirstLocalCharacter() : null;
            if (c == null) return;

            object inv = GetMemberValue(c, "Inventory");
            if (inv == null) return;

            var itemsObj = GetMemberValue(inv, "Items") ?? GetMemberValue(inv, "m_items");
            if (itemsObj == null) return;

            // Items may be IList
            if (itemsObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var it in enumerable)
                {
                    if (it == null) continue;
                    int id = GetIntMember(it, "ItemID", -1);
                    if (id < 0) id = GetIntMember(it, "m_itemID", -1);
                    int qty = GetIntMember(it, "Quantity", 1);
                    if (qty <= 0) qty = 1;
                    if (id < 0) continue;
                    snap.items.Add(new ItemStackSnapshot { itemID = id, qty = qty });
                }
            }
        }

        private static void RestoreLocalInventory(PlayerInventorySnapshot snap)
        {
            // This is intentionally conservative: we do NOT wipe inventory.
            // We only ensure at least the snapshot quantities exist by adding missing items.
            var c = CharacterManager.Instance != null ? CharacterManager.Instance.GetFirstLocalCharacter() : null;
            if (c == null) return;

            object inv = GetMemberValue(c, "Inventory");
            if (inv == null) return;

            foreach (var s in snap.items)
            {
                if (s == null || s.itemID < 0 || s.qty <= 0) continue;

                int current = CountItem(inv, s.itemID);
                int need = s.qty - current;
                if (need <= 0) continue;

                // Best-effort spawn via ItemManager reflection (Outward API).
                TrySpawnItemToInventory(inv, s.itemID, need);
            }
        }

        private static int CountItem(object inv, int itemId)
        {
            try
            {
                var itemsObj = GetMemberValue(inv, "Items") ?? GetMemberValue(inv, "m_items");
                if (itemsObj is System.Collections.IEnumerable enumerable)
                {
                    int total = 0;
                    foreach (var it in enumerable)
                    {
                        if (it == null) continue;
                        int id = GetIntMember(it, "ItemID", -1);
                        if (id < 0) id = GetIntMember(it, "m_itemID", -1);
                        if (id != itemId) continue;
                        int qty = GetIntMember(it, "Quantity", 1);
                        if (qty <= 0) qty = 1;
                        total += qty;
                    }
                    return total;
                }
            }
            catch { }
            return 0;
        }

        private static void TrySpawnItemToInventory(object inv, int itemId, int qty)
        {
            try
            {
                // ItemManager.Instance?.SpawnItem(int, int) etc. We use reflection to avoid signature coupling.
                var itemMgrType = Type.GetType("ItemManager") ?? Type.GetType("ItemManager, Assembly-CSharp");
                if (itemMgrType == null) return;

                var instProp = itemMgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var inst = instProp != null ? instProp.GetValue(null, null) : null;
                if (inst == null) return;

                MethodInfo spawn = null;
                foreach (var m in itemMgrType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name != "SpawnItem") continue;
                    var ps = m.GetParameters();
                    if (ps.Length >= 2 && ps[0].ParameterType == typeof(int) && ps[1].ParameterType == typeof(int))
                    {
                        spawn = m;
                        break;
                    }
                }
                if (spawn == null) return;

                var item = spawn.Invoke(inst, new object[] { itemId, qty });
                if (item == null) return;

                // Inventory.AddItem(Item) via reflection
                var add = inv.GetType().GetMethod("AddItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (add != null)
                {
                    add.Invoke(inv, new object[] { item });
                }
            }
            catch { }
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanRead)
            {
                try { return p.GetValue(obj, null); } catch { }
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                try { return f.GetValue(obj); } catch { }
            }

            return null;
        }

        private static int GetIntMember(object obj, string name, int def)
        {
            try
            {
                var v = GetMemberValue(obj, name);
                if (v == null) return def;
                if (v is int i) return i;
            }
            catch { }
            return def;
        }
    }
}
