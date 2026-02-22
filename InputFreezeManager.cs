using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Best-effort input blocker: disables common control scripts on the local character while a freeze is active.
    /// This is intentionally defensive and reversible.
    /// </summary>
    public class InputFreezeManager : MonoBehaviour
    {
        private readonly Dictionary<Behaviour, bool> _prior = new Dictionary<Behaviour, bool>();
        private bool _enabled;

        private string _reason;

        public void Enable(string reason)
        {
            _reason = reason ?? "";
            if (_enabled) return;
            _enabled = true;
            TryDisableLocalCharacterControls();
        }

        public void Disable()
        {
            if (!_enabled) return;
            _enabled = false;

            foreach (var kv in _prior)
            {
                try
                {
                    if (kv.Key != null)
                        kv.Key.enabled = kv.Value;
                }
                catch { }
            }
            _prior.Clear();
            _reason = null;
        }

        private void TryDisableLocalCharacterControls()
        {
            try
            {
                var c = CharacterManager.Instance != null ? CharacterManager.Instance.GetFirstLocalCharacter() : null;
                if (c == null) return;

                var go = c.gameObject;
                var behaviours = go.GetComponentsInChildren<Behaviour>(true);

                foreach (var b in behaviours)
                {
                    if (b == null) continue;
                    var tn = b.GetType().Name;

                    // Heuristic: disable likely control components only
                    if (IsControlComponentName(tn))
                    {
                        if (!_prior.ContainsKey(b))
                        {
                            _prior[b] = b.enabled;
                        }
                        try { b.enabled = false; } catch { }
                    }
                }
            }
            catch { }
        }

        private bool IsControlComponentName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;

            string n = typeName.ToLowerInvariant();
            if (n.Contains("input") || n.Contains("control") || n.Contains("controller") || n.Contains("motor") || n.Contains("rewired"))
            {
                // Avoid disabling UI/EventSystem stuff accidentally
                if (n.Contains("eventsystem") || n.Contains("uicontroller")) return false;
                return true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (!_enabled) return;
            // Small unobtrusive banner
            GUI.Label(new Rect(10, 10, 900, 30), "[Dynasty] Frozen: " + _reason);
        }
    }
}
