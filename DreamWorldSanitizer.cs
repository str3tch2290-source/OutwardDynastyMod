// ======================================================
// DreamWorldSanitizer.cs (REWRITE - NO NavMeshAgent REF)
//
// Fixes:
// - "fly through sky" in DreamWorld by stabilizing the player immediately
// - avoids UnityEngine.AIModule dependency (no NavMeshAgent)
//
// What it does on DreamWorld load:
// - waits for local character
// - freezes momentum + temporarily disables gravity/controller
// - snaps to a stable ground point (raycast from above)
// - disables common AI/brain scripts in DreamWorld (by name scan)
// ======================================================

using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class DreamWorldSanitizer : MonoBehaviour
    {
        private const string HEAVEN_SCENE = "DreamWorld";

        private DynastyCore _core;

        private bool _running;
        private Coroutine _co;

        public void Initialize(DynastyCore core) => _core = core;

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_co != null) StopCoroutine(_co);
            _co = null;
            _running = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid()) return;
            if (!scene.name.Equals(HEAVEN_SCENE, StringComparison.OrdinalIgnoreCase)) return;

            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(CoSanitizeDreamWorld());
        }

        private IEnumerator CoSanitizeDreamWorld()
        {
            if (_running) yield break;
            _running = true;

            // let scene boot a little
            yield return null;
            yield return null;

            // Disable "AI-ish" scripts in DreamWorld WITHOUT NavMeshAgent
            int disabled = 0;
            try
            {
                var allBehaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                for (int i = 0; i < allBehaviours.Length; i++)
                {
                    var b = allBehaviours[i];
                    if (b == null) continue;
                    if (!b.gameObject.scene.IsValid()) continue;
                    if (!b.gameObject.scene.name.Equals(HEAVEN_SCENE, StringComparison.OrdinalIgnoreCase)) continue;

                    string tn = b.GetType().Name;

                    // High-signal filters (safe + broad)
                    bool looksLikeAI =
                        tn.IndexOf("NavMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tn.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tn.IndexOf("Brain", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!looksLikeAI) continue;

                    try
                    {
                        if (b.enabled)
                        {
                            b.enabled = false;
                            disabled++;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            Debug.Log($"[Dynasty] DreamWorldSanitizer: disabled {disabled} AI-ish component(s) in DreamWorld scene.");

            // Wait for local character
            float t0 = Time.realtimeSinceStartup;
            Character c = null;
            while (c == null && Time.realtimeSinceStartup - t0 < 6f)
            {
                c = SafeGetLocalCharacter();
                if (c == null) yield return null;
            }

            if (c == null)
            {
                _running = false;
                yield break;
            }

            // Stabilize player
            Rigidbody rb = null;
            CharacterController cc = null;

            try { rb = c.GetComponent<Rigidbody>(); } catch { }
            try { cc = c.GetComponent<CharacterController>(); } catch { }

            bool hadGravity = false;
            bool hadCCEnabled = false;

            if (rb != null)
            {
                hadGravity = rb.useGravity;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
            }

            if (cc != null)
            {
                hadCCEnabled = cc.enabled;
                cc.enabled = false;
            }

            // Try multiple snap attempts as colliders come online
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (TryFindDreamWorldGround(out var ground))
                {
                    SetCharacterPosition(c, ground + Vector3.up * 1.25f);
                    if (rb != null)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                yield return new WaitForSeconds(0.25f);

                if (IsGroundedEnough(c.transform.position))
                    break;
            }

            // Hold stability briefly (prevents drift/fall after snap)
            float holdStart = Time.realtimeSinceStartup;
            float holdSeconds = 2.5f;

            while (Time.realtimeSinceStartup - holdStart < holdSeconds)
            {
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                yield return null;
            }

            // Restore
            if (cc != null) cc.enabled = hadCCEnabled;
            if (rb != null) rb.useGravity = hadGravity;

            _running = false;
        }

        private static Character SafeGetLocalCharacter()
        {
            try
            {
                return CharacterManager.Instance != null
                    ? CharacterManager.Instance.GetFirstLocalCharacter()
                    : null;
            }
            catch { return null; }
        }

        private static void SetCharacterPosition(Character c, Vector3 pos)
        {
            try { c.transform.position = pos; } catch { }
        }

        private static bool IsGroundedEnough(Vector3 pos)
        {
            RaycastHit hit;
            Vector3 start = pos + Vector3.up * 2f;
            if (Physics.SphereCast(start, 0.6f, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                return (pos.y - hit.point.y) < 3.0f;
            return false;
        }

        private static bool TryFindDreamWorldGround(out Vector3 ground)
        {
            ground = Vector3.zero;

            try
            {
                // Probe a few spots around the expected center
                for (int r = 0; r < 6; r++)
                {
                    float d = r * 3f;
                    Vector3[] offsets =
                    {
                        new Vector3(0,0,0),
                        new Vector3( d,0, 0), new Vector3(-d,0, 0),
                        new Vector3(0,0, d), new Vector3(0,0,-d),
                        new Vector3( d,0, d), new Vector3(-d,0, d),
                        new Vector3( d,0,-d), new Vector3(-d,0,-d),
                    };

                    for (int i = 0; i < offsets.Length; i++)
                    {
                        var probe = new Vector3(offsets[i].x, 800f, offsets[i].z);
                        if (Physics.SphereCast(probe, 0.9f, Vector3.down, out var hit, 3000f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            ground = hit.point;
                            return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}
