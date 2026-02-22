
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Lightweight interaction overlay:
    /// - Aim at a non-hostile NPC and press G to open Dynasty interaction menu.
    /// - Includes Trade (informational; you can still use vanilla interact), Talk, Rumors, Quests, Recruit Contact.
    /// This is v1 content UI that doesn't depend on NodeCanvas authoring.
    /// </summary>
    public class DynastyNpcInteractionUI : MonoBehaviour
    {
        private DynastyCore _core;

        private bool _open;
        private Character _target;
        private NpcSimData _npc;
        private string _panel = "ROOT"; // ROOT/TALK/RUMORS/QUESTS
        private Vector2 _scroll;

        public void Initialize(DynastyCore core) => _core = core;

        private void Update()
        {
            if (_core == null || _core.MasterData == null) return;
            if (!_core.IsDynastyModeEnabled || !_core.MasterData.DynastyStarted) return;

            if (InputProxy.GetKeyDown(KeyCode.G))
            {
                if (_open)
                {
                    Close();
                }
                else
                {
                    TryOpen();
                }
            }

            if (_open && InputProxy.GetKeyDown(KeyCode.Escape))
                Close();
        }

        private void TryOpen()
        {
            var local = SafeGetLocalCharacter();
            if (local == null) return;

            var hit = FindNpcInFront(local);
            if (hit == null) return;

            // must be non-hostile and not in combat alert for trading (A)
            if (IsHostileOrInCombat(hit)) return;

            _target = hit;
            _npc = DynastyNpcResolver.ResolveOrCreate(_core.MasterData, hit);
            _panel = "ROOT";
            _open = true;
            _scroll = Vector2.zero;
        }

        private void Close()
        {
            _open = false;
            _target = null;
            _npc = null;
            _panel = "ROOT";
        }

        private void OnGUI()
        {
            if (!_open || _npc == null) return;

            float w = 520f;
            float h = 360f;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            GUI.Box(new Rect(x, y, w, h), $"Dynasty: {_npc.DisplayName}");

            // Top info
            GUI.Label(new Rect(x + 20, y + 35, w - 40, 22),
                $"Disposition: {_npc.Disposition:0}   Adventure: {_npc.Adventure:0}   Loyalty: {_npc.Loyalty:0}   Wealth: {_npc.Wealth:0}");

            // Root menu
            if (_panel == "ROOT")
            {
                if (GUI.Button(new Rect(x + 20, y + 70, w - 40, 28), "Trade (blocked if in combat; uses vanilla trade UI)"))
                {
                    // We intentionally do not force-open shop menus (version-volatile).
                    // This button exists to mirror Outward's interaction list; player can press the normal interact.
                }

                if (GUI.Button(new Rect(x + 20, y + 110, w - 40, 28), "Talk"))
                    _panel = "TALK";

                if (GUI.Button(new Rect(x + 20, y + 150, w - 40, 28), "Rumors"))
                    _panel = "RUMORS";

                if (GUI.Button(new Rect(x + 20, y + 190, w - 40, 28), "Quests"))
                    _panel = "QUESTS";

                bool canRecruit = _npc.Disposition >= 80f; // friend threshold
                string recruitLabel = canRecruit ? "Recruit as Contact Agent" : "Recruit as Contact Agent (requires Friend)";
                if (GUI.Button(new Rect(x + 20, y + 230, w - 40, 28), recruitLabel))
                {
                    if (canRecruit)
                        RecruitAsContact();
                }

                if (GUI.Button(new Rect(x + 20, y + h - 50, w - 40, 28), "Close"))
                    Close();
            }
            else
            {
                if (GUI.Button(new Rect(x + 20, y + 70, 90, 24), "< Back"))
                    _panel = "ROOT";

                _scroll = GUI.BeginScrollView(new Rect(x + 20, y + 100, w - 40, h - 150), _scroll, new Rect(0, 0, w - 70, 600));

                if (_panel == "TALK")
                    DrawTalk(0, 0, w - 80);

                if (_panel == "RUMORS")
                    DrawRumors(0, 0, w - 80);

                if (_panel == "QUESTS")
                    DrawQuests(0, 0, w - 80);

                GUI.EndScrollView();

                if (GUI.Button(new Rect(x + 20, y + h - 50, w - 40, 28), "Close"))
                    Close();
            }
        }

        private void DrawTalk(float x, float y, float w)
        {
            string line = DynastyDialogueLines.GetSmallTalk(_core.MasterData, _npc);
            GUI.Label(new Rect(x, y, w, 200), line);

            // Small social choice (branching micro)
            if (GUI.Button(new Rect(x, y + 210, w, 28), "Offer a fair deal (Disposition +2)"))
            {
                _npc.Disposition = Mathf.Clamp(_npc.Disposition + 2f, 0f, 100f);
                DynastySaveManager.Save(_core.MasterData);
            }

            if (GUI.Button(new Rect(x, y + 245, w, 28), "Hard bargain (Wealth +1, Disposition -3)"))
            {
                _npc.Wealth = Mathf.Clamp(_npc.Wealth + 1f, 0f, 100f);
                _npc.Disposition = Mathf.Clamp(_npc.Disposition - 3f, 0f, 100f);
                DynastySaveManager.Save(_core.MasterData);
            }
        }

        private void DrawRumors(float x, float y, float w)
        {
            string town = DynastyWorldContext.GetCurrentTownName();
            string owner = DynastyWorldContext.GetTownOwnerFaction(_core.MasterData, town);
            int cap = DynastyWorldContext.GetRenderNpcCap(_core.MasterData);

            GUI.Label(new Rect(x, y, w, 26), $"Town: {town}   Owner: {owner}   RenderCap: {cap}");

            string r = DynastyDialogueLines.GetRumor(_core.MasterData, town);
            GUI.Label(new Rect(x, y + 30, w, 240), r);
        }

        private void DrawQuests(float x, float y, float w)
        {
            var data = _core.MasterData;
            if (data.DynastyQuests == null) { GUI.Label(new Rect(x, y, w, 26), "No quests seeded."); return; }

            GUI.Label(new Rect(x, y, w, 22), "Available offers:");
            float yy = y + 28;

            foreach (var q in data.DynastyQuests)
            {
                if (q == null) continue;
                if (q.Status != DynastyQuestStatus.Inactive) continue;

                if (!DynastyQuestRules.IsQuestOfferableHere(data, q, _npc)) continue;

                if (GUI.Button(new Rect(x, yy, w, 26), $"Accept: {q.Title}"))
                {
                    DynastyQuestRules.AcceptQuest(data, q, _npc);
                    DynastySaveManager.Save(data);
                }
                yy += 30;
            }

            yy += 10;
            GUI.Label(new Rect(x, yy, w, 22), "Active:");
            yy += 28;

            foreach (var q in data.DynastyQuests)
            {
                if (q == null) continue;
                if (q.Status != DynastyQuestStatus.Active) continue;

                GUI.Label(new Rect(x, yy, w, 48), $"{q.Title} (Step {q.StepIndex + 1})  {q.Progress01:0%}");
                yy += 52;
            }

            yy += 10;
            if (data.DynastyArcs != null)
            {
                GUI.Label(new Rect(x, yy, w, 22), "Dynasty Arcs:");
                yy += 28;
                foreach (var a in data.DynastyArcs)
                {
                    if (a == null) continue;
                    string s = $"{a.Title} - {a.Status} (Stage {a.Stage})";
                    if (GUI.Button(new Rect(x, yy, w, 26), a.Status == DynastyQuestStatus.Inactive ? ("Start: " + s) : s))
                    {
                        if (a.Status == DynastyQuestStatus.Inactive)
                        {
                            a.Status = DynastyQuestStatus.Active;
                            a.Stage = 0;
                            a.Flags.Clear();
                            DynastySaveManager.Save(data);
                        }
                    }
                    yy += 30;
                }
            }
        }

        private void RecruitAsContact()
        {
            try
            {
                if (_npc == null) return;
                if (_npc.IsContact) return;

                _npc.IsContact = true;
                _npc.Role = NpcRole.Contact;
                _npc.ContactOwnerMemberGuid = DynastyIdentity.MemberGuid;

                // small boosts
                _npc.Loyalty = Mathf.Clamp(_npc.Loyalty + 10f, 0f, 100f);
                _npc.Influence = Mathf.Clamp(_npc.Influence + 5f, 0f, 100f);

                DynastySaveManager.Save(_core.MasterData);
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

        private static Character FindNpcInFront(Character local)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return null;

                Ray r = new Ray(cam.transform.position, cam.transform.forward);
                if (!Physics.Raycast(r, out RaycastHit hit, 3.25f)) return null;

                var c = hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
                if (c == null) return null;
                if (c == local) return null;
                if (DynastyNpcSimManager_IsPlayer(c)) return null;

                return c;
            }
            catch { return null; }
        }

        private static bool DynastyNpcSimManager_IsPlayer(Character c)
        {
            try
            {
                var t = c.GetType();
                var p = t.GetProperty("IsPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool))
                    return (bool)p.GetValue(c, null);
                var ps = t.GetProperty("PlayerStats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ps != null && ps.GetValue(c, null) != null) return true;
            }
            catch { }
            return false;
        }

        private static bool IsHostileOrInCombat(Character npc)
        {
            try
            {
                // First: if NPC has "IsAggro" / "InCombat" signals, block.
                var t = npc.GetType();
                // property InCombat / IsInCombat
                foreach (var name in new[] { "InCombat", "IsInCombat", "m_inCombat" })
                {
                    var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(bool))
                    {
                        bool v = (bool)p.GetValue(npc, null);
                        if (v) return true;
                    }
                    var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(bool))
                    {
                        bool v = (bool)f.GetValue(npc);
                        if (v) return true;
                    }
                }

                // Second: check CharacterAI if present
                var aiProp = t.GetProperty("AI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object ai = aiProp != null ? aiProp.GetValue(npc, null) : null;
                if (ai != null)
                {
                    var aiT = ai.GetType();
                    foreach (var name in new[] { "InCombat", "IsInCombat", "IsAlerted", "Alerted" })
                    {
                        var p = aiT.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (p != null && p.PropertyType == typeof(bool))
                        {
                            bool v = (bool)p.GetValue(ai, null);
                            if (v) return true;
                        }
                    }
                }

                // We do not try to detect faction hostility here; "non-hostile" is approximated by "not in combat/alert".
                return false;
            }
            catch { return true; }
        }
    }
}
