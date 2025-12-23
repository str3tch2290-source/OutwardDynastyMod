// ======================================================
// DreamWorldSanitizer.cs (REWRITE - SCENE SCOPED)
// Fixes:
// - Disabling "144 NavMeshAgent(s)" globally (too broad)
// - Only touches objects that are actually in the DreamWorld scene
//
// What it does:
// - On DreamWorld load: disables NavMeshAgent components found under DreamWorld roots
// - After local player exists: destroys non-local Characters near player that have NavMeshAgent
// ======================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class DreamWorldSanitizer : MonoBehaviour
    {
        private const string DREAMWORLD_SCENE = "DreamWorld";

        private const float CLEAN_RADIUS = 60f;
        private const int PASSES = 10;
        private const float PASS_DELAY = 0.5f;
        private const float INITIAL_DELAY = 0.75f;

        private DynastyCore _core;
        private bool _deleteRoutineRunning;

        private static readonly string[] NameWhitelist =
        {
            "Soul-Guide",
            "Soul Guide",
            "Guide",
            "Trainer",
            "Guard",
            "Vendor",
            "Merchant",
            "Eto Akiyuki"
        };

        public void Initialize(DynastyCore core)
        {
            _core = core;
            Debug.Log("[Dynasty] DreamWorldSanitizer attached.");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsDreamWorld(scene.name)) return;

            _deleteRoutineRunning = false;

            // ✅ Disable agents ONLY under DreamWorld scene roots (not global)
            StartCoroutine(DisableAgentsInDreamWorldNextFrame(scene));
        }

        private IEnumerator DisableAgentsInDreamWorldNextFrame(Scene dreamWorldScene)
        {
            yield return null; // let objects spawn

            try
            {
                if (!dreamWorldScene.isLoaded) yield break;

                int disabled = DisableNavMeshAgentsInScene(dreamWorldScene);
                if (disabled > 0)
                    Debug.Log($"[Dynasty] DreamWorldSanitizer: disabled {disabled} NavMeshAgent(s) in DreamWorld scene.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dynasty] DreamWorldSanitizer: agent-disable error (non-fatal): " + ex);
            }
        }

        private void Update()
        {
            if (_core == null) return;
            if (!_core.IsDynastyModeEnabled) return;

            if (!IsDreamWorld(SceneManager.GetActiveScene().name)) return;
            if (_deleteRoutineRunning) return;

            Character local = SafeGetLocalCharacter();
            if (local == null) return;

            _deleteRoutineRunning = true;
            StartCoroutine(DeleteRoutine(local));
        }

        private IEnumerator DeleteRoutine(Character local)
        {
            yield return new WaitForSeconds(INITIAL_DELAY);

            for (int pass = 0; pass < PASSES; pass++)
            {
                yield return new WaitForSeconds(PASS_DELAY);

                if (!IsDreamWorld(SceneManager.GetActiveScene().name))
                    yield break;

                if (local == null)
                    yield break;

                int removed = RemoveNavMeshAgentCharactersNear(local, CLEAN_RADIUS);
                if (removed > 0)
                    Debug.Log($"[Dynasty] DreamWorldSanitizer pass {pass + 1}/{PASSES}: removed {removed} mob(s).");
            }
        }

        private int DisableNavMeshAgentsInScene(Scene scene)
        {
            int disabled = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            if (roots == null) return 0;

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null) continue;

                // find any components named "NavMeshAgent" under this root
                Component[] comps = root.GetComponentsInChildren<Component>(true);
                if (comps == null) continue;

                for (int c = 0; c < comps.Length; c++)
                {
                    Component comp = comps[c];
                    if (comp == null) continue;

                    if (!string.Equals(comp.GetType().Name, "NavMeshAgent", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // comp.enabled = false (reflection-safe)
                    try
                    {
                        var prop = comp.GetType().GetProperty("enabled");
                        if (prop != null && prop.PropertyType == typeof(bool))
                        {
                            prop.SetValue(comp, false, null);
                            disabled++;
                        }
                    }
                    catch { }
                }
            }

            return disabled;
        }

        private int RemoveNavMeshAgentCharactersNear(Character local, float radius)
        {
            int removed = 0;

            Scene active = SceneManager.GetActiveScene();
            Vector3 center = local.transform.position;
            float r2 = radius * radius;

            Character[] all = GameObject.FindObjectsOfType<Character>(); // scene-only
            if (all == null) return 0;

            for (int i = 0; i < all.Length; i++)
            {
                Character c = all[i];
                if (c == null) continue;
                if (IsLocalPlayerSafe(c)) continue;

                GameObject go = c.gameObject;
                if (go == null) continue;
                if (go.scene != active) continue;

                Vector3 pos = c.transform.position;
                if ((pos - center).sqrMagnitude > r2) continue;

                string name = go.name ?? "";
                if (ContainsAny(name, NameWhitelist))
                    continue;

                // only remove if it has NavMeshAgent
                if (go.GetComponent("NavMeshAgent") == null)
                    continue;

                Debug.Log($"[Dynasty] DreamWorldSanitizer destroying mob: '{go.name}'");
                Destroy(go);
                removed++;
            }

            return removed;
        }

        private static bool IsDreamWorld(string sceneName)
        {
            return string.Equals(sceneName, DREAMWORLD_SCENE, StringComparison.OrdinalIgnoreCase);
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

        private static bool IsLocalPlayerSafe(Character c)
        {
            try { return c != null && c.IsLocalPlayer; }
            catch { return false; }
        }

        private static bool ContainsAny(string haystack, string[] needles)
        {
            if (string.IsNullOrEmpty(haystack) || needles == null) return false;
            string h = haystack.ToLowerInvariant();
            for (int i = 0; i < needles.Length; i++)
            {
                string n = needles[i];
                if (string.IsNullOrEmpty(n)) continue;
                if (h.Contains(n.ToLowerInvariant())) return true;
            }
            return false;
        }
    }
}
