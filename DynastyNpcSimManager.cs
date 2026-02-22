
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    /// <summary>
    /// Keeps Dynasty data-NPCs seeded and enforces a conservative render cap on spawned non-player Characters.
    /// This is intentionally best-effort and uses reflection to stay version-tolerant.
    /// </summary>
    public class DynastyNpcSimManager : MonoBehaviour
    {
        private DynastyCore _core;
        private float _nextScanTime = 0f;

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void Update()
        {
            if (_core == null || _core.MasterData == null) return;
            if (!_core.IsDynastyModeEnabled || !_core.MasterData.DynastyStarted) return;

            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + 2.0f; // scan every 2s

            if (SceneManager.GetActiveScene().name == "DreamWorld") return;

            // Ensure NPC seeds exist for current town (data-mode)
            EnsureTownNpcsSeeded();

            // Enforce render cap
            EnforceRenderCap();
        }

        private void EnsureTownNpcsSeeded()
        {
            try
            {
                var data = _core.MasterData;
                if (data.Npcs == null) data.Npcs = new List<NpcSimData>();

                string town = DynastyWorldContext.GetCurrentTownName();
                if (string.IsNullOrEmpty(town)) return;

                // Ensure at least a few ambient NPCs exist per town so quests have candidates.
                int existing = 0;
                for (int i = 0; i < data.Npcs.Count; i++)
                    if (data.Npcs[i] != null && data.Npcs[i].IsAlive && string.Equals(data.Npcs[i].HomeTown, town, StringComparison.OrdinalIgnoreCase))
                        existing++;

                int target = 18; // data-mode pool; rendered cap remains small
                if (existing >= target) return;

                int needed = target - existing;
                var rng = new System.Random(data.WorldSeed ^ town.GetHashCode());

                for (int i = 0; i < needed; i++)
                {
                    var npc = new NpcSimData();
                    npc.NpcId = "NPC_" + town.Replace(" ", "_") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    npc.DisplayName = DynastyNameGen.Generate(rng);
                    npc.HomeTown = town;
                    npc.CurrentTown = town;
                    npc.Faction = DynastyWorldContext.GetTownOwnerFaction(data, town);

                    // stats
                    npc.Disposition = 40f + (float)rng.NextDouble() * 20f;
                    npc.Adventure = (float)rng.NextDouble() * 100f;
                    npc.Loyalty = (float)rng.NextDouble() * 100f;
                    npc.Wealth = (float)rng.NextDouble() * 100f;
                    npc.Influence = (float)rng.NextDouble() * 40f;
                    npc.Fear = (float)rng.NextDouble() * 30f;

                    npc.Role = NpcRole.Ambient;
                    if (rng.NextDouble() < 0.10) npc.Role = NpcRole.Guard;
                    if (rng.NextDouble() < 0.08) npc.Role = NpcRole.Merchant;

                    npc.Task = NpcTaskType.Idle;
                    npc.TaskSeed = rng.Next();

                    data.Npcs.Add(npc);
                }
            }
            catch { }
        }

        private void EnforceRenderCap()
        {
            try
            {
                int cap = DynastyWorldContext.GetRenderNpcCap(_core.MasterData);
                if (cap <= 0) return;

                Character local = SafeGetLocalCharacter();
                if (local == null) return;

                var npcs = SafeGetAllNonPlayerCharacters(local);
                if (npcs == null) return;

                if (npcs.Count <= cap) return;

                // Remove farthest first, never remove a quest-anchored NPC (best-effort via name tag)
                npcs.Sort((a, b) =>
                {
                    float da = Vector3.Distance(local.transform.position, a.transform.position);
                    float db = Vector3.Distance(local.transform.position, b.transform.position);
                    return db.CompareTo(da); // farthest first
                });

                int toRemove = npcs.Count - cap;
                for (int i = 0; i < npcs.Count && toRemove > 0; i++)
                {
                    var c = npcs[i];
                    if (c == null) continue;

                    string n = SafeGetName(c);
                    if (!string.IsNullOrEmpty(n) && n.IndexOf("DYNASTY_", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    try
                    {
                        UnityEngine.Object.Destroy(c.gameObject);
                        toRemove--;
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static Character SafeGetLocalCharacter()
        {
            try
            {
                if (CharacterManager.Instance == null) return null;
                return CharacterManager.Instance.GetFirstLocalCharacter();
            }
            catch { return null; }
        }

        private static List<Character> SafeGetAllNonPlayerCharacters(Character local)
        {
            var result = new List<Character>();
            try
            {
                if (CharacterManager.Instance == null) return result;

                // Try common property/field patterns to enumerate characters.
                object listObj = null;
                var t = CharacterManager.Instance.GetType();

                var p = t.GetProperty("Characters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead) listObj = p.GetValue(CharacterManager.Instance, null);

                if (listObj == null)
                {
                    var f = t.GetField("m_characters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) listObj = f.GetValue(CharacterManager.Instance);
                }

                if (listObj is IEnumerable enumerable)
                {
                    foreach (var o in enumerable)
                    {
                        var c = o as Character;
                        if (c == null) continue;
                        if (c == local) continue;
                        if (IsPlayer(c)) continue;
                        result.Add(c);
                    }
                }
            }
            catch { }
            return result;
        }

        private static bool IsPlayer(Character c)
        {
            try
            {
                // common patterns
                var t = c.GetType();
                var p = t.GetProperty("IsPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool))
                    return (bool)p.GetValue(c, null);

                // fallback: if c has PlayerStats or has LocalPlayer
                var ps = t.GetProperty("PlayerStats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ps != null && ps.GetValue(c, null) != null) return true;
            }
            catch { }
            return false;
        }

        private static string SafeGetName(Character c)
        {
            try
            {
                if (c == null) return "";
                return c.name ?? "";
            }
            catch { return ""; }
        }
    }
}
