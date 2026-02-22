// ======================================================
// ConfirmSetup.cs (ROBUST SPAWN + PREFER NON-ONESHOT LOADER)
// v3
//
// Fixes:
// - Selecting PlayerSpawns container at (0,0,0)
// - Snapping to ocean plane at y ~ -6.9
// - No region loading screen (prefers non-OneShot LoadLevel)
// ======================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class ConfirmSetup : MonoBehaviour
    {
        private const string HEAVEN_SCENE = "DreamWorld";

        private DynastyCore _core;

        private bool _waitingForPlacement;
        private string _targetScene;

        public void Initialize(DynastyCore core) => _core = core;


// Attempts to resolve a scene name that is actually loadable in the current Outward build.
// Some regions use "*NewTerrain" variants depending on version/DLC.
private static string ResolveLoadableSceneName(string sceneName)
{
    if (string.IsNullOrEmpty(sceneName))
        return sceneName;

    // 1) As-is
    if (Application.CanStreamedLevelBeLoaded(sceneName))
        return sceneName;

    // 2) Common suffix used by several regions
    string nt = sceneName.EndsWith("NewTerrain", StringComparison.OrdinalIgnoreCase)
        ? sceneName
        : sceneName + "NewTerrain";
    if (!string.Equals(nt, sceneName, StringComparison.OrdinalIgnoreCase) &&
        Application.CanStreamedLevelBeLoaded(nt))
        return nt;

    // 3) A few builds use "NewTerrain" with different casing; try exact common known names.
    // (Keep this list tiny and safe; companion authority is the source of truth.)
    switch (sceneName)
    {
        case "HallowedMarsh":
            if (Application.CanStreamedLevelBeLoaded("HallowedMarshNewTerrain"))
                return "HallowedMarshNewTerrain";
            break;
        case "AntiquePlateau":
            if (Application.CanStreamedLevelBeLoaded("AntiquePlateauNewTerrain"))
                return "AntiquePlateauNewTerrain";
            break;
        case "Caldera":
            if (Application.CanStreamedLevelBeLoaded("CalderaNewTerrain"))
                return "CalderaNewTerrain";
            break;
    }

    return sceneName; // unresolved
}
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        public bool ConfirmAndContinue(string displayName, string sceneName, string echoesText, out string status)
        {
            status = "";

            if (_core == null || _core.MasterData == null)
            {
                status = "Dynasty system not ready.";
                return false;
            }

            if (!_core.IsDynastyModeEnabled)
            {
                status = "Dynasty mode is disabled.";
                return false;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                status = "Invalid starting location.";
                return false;
            }

            // Resolve scene names across Outward versions (some regions are *NewTerrain).
            string resolvedScene = ResolveLoadableSceneName(sceneName);
            if (!Application.CanStreamedLevelBeLoaded(resolvedScene))
            {
                status = "Scene not loadable: " + sceneName;
                return false;
            }

            sceneName = resolvedScene;

            if (!int.TryParse(echoesText, out var echoes) || echoes < 0)
            {
                status = "Starting Echoes must be a number (0+).";
                return false;
            }

            Debug.Log("[Dynasty] Confirm start -> " + sceneName);

            _core.MasterData.SoulEchos = echoes;
            _core.MasterData.DynastyStarted = true;
            _core.MasterData.PlayerPlaced = false;
            _core.SaveDynasty();

            _waitingForPlacement = true;
            _targetScene = sceneName;

            status = "Loading " + displayName + "...";

            if (SceneManager.GetActiveScene().name == HEAVEN_SCENE)
                CleanupASMSoulGuides();

            StartCoroutine(CoLoadTargetScene_ViaLoaderPreferNormal(sceneName));
            return true;
        }

        // --------------------------------------------------
        // Loader (prefer non-OneShot so region loading screen can appear)
        // --------------------------------------------------
                private IEnumerator CoLoadTargetScene_ViaLoaderPreferNormal(string targetScene)
        {
            // Give Unity one frame so any scene bootstrappers can spawn.
            yield return null;

            const float timeout = 6f;
            float t0 = Time.realtimeSinceStartup;

            object loader = null;
            while (loader == null && Time.realtimeSinceStartup - t0 < timeout)
            {
                loader = FindNetworkLevelLoaderInstance();
                if (loader == null)
                    yield return null;
            }

            // In some contexts (especially DreamWorld), NetworkLevelLoader may not exist yet.
            // For MVP we prefer it, but we will fall back to a normal scene load so the button
            // actually works and we can iterate on proper host sync later.
            if (loader == null)
            {
                Debug.LogWarning("[Dynasty] NetworkLevelLoader not found after timeout; falling back to SceneManager.LoadScene for '" + targetScene + "'.");
                _waitingForPlacement = false;
                try
                {
                    SceneManager.LoadScene(targetScene);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Dynasty] Fallback SceneManager.LoadScene failed for '" + targetScene + "': " + e);
                }
                yield break;
            }

            if (!InvokeBestLoadLevel(loader, targetScene))
            {
                Debug.LogWarning("[Dynasty] InvokeBestLoadLevel failed for '" + targetScene + "'. Falling back to SceneManager.LoadScene.");
                _waitingForPlacement = false;
                try
                {
                    SceneManager.LoadScene(targetScene);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Dynasty] Fallback SceneManager.LoadScene failed for '" + targetScene + "': " + e);
                }
                yield break;
            }
        }


        private object FindNetworkLevelLoaderInstance()
        {
            try
            {
                if (NetworkLevelLoader.Instance != null)
                    return NetworkLevelLoader.Instance;
            }
            catch { }

            try
            {
                var t = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(x => x != null && x.Name == "NetworkLevelLoader");

                if (t == null) return null;

                var instProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (instProp != null)
                {
                    var inst = instProp.GetValue(null, null);
                    if (inst != null) return inst;
                }

                var instField = t.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (instField != null)
                {
                    var inst = instField.GetValue(null);
                    if (inst != null) return inst;
                }

                var found = FindObjectOfType(t);
                if (found != null) return found;
            }
            catch { }

            return null;
        }

        private bool InvokeBestLoadLevel(object loaderInstance, string targetScene)
        {
            try
            {
                var t = loaderInstance.GetType();

                var candidates = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m =>
                    {
                        if (m == null) return false;
                        var name = m.Name.ToLowerInvariant();
                        if (!name.Contains("loadlevel")) return false;
                        var ps = m.GetParameters();
                        return ps.Length >= 1 && ps[0].ParameterType == typeof(string);
                    })
                    .ToList();

                if (candidates.Count == 0)
                {
                    Debug.LogWarning("[Dynasty] NetworkLevelLoader has no LoadLevel*(string, ...) method.");
                    return false;
                }

                // Prefer NOT OneShot (so the normal loading flow/splash has a chance)
                // Prefer method name exactly "LoadLevel" or contains "LoadLevel" but not "OneShot"
                // Prefer more params (often includes flags for loading UI)
                var best = candidates
                    .OrderBy(m => m.Name.ToLowerInvariant().Contains("oneshot") ? 10 : 0)
                    .ThenBy(m => m.Name.Equals("LoadLevel", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenByDescending(m => m.GetParameters().Length)
                    .First();

                var p = best.GetParameters();
                object[] args = new object[p.Length];

                args[0] = targetScene;

                for (int i = 1; i < p.Length; i++)
                {
                    var pt = p[i].ParameterType;
                    var pn = (p[i].Name ?? "").ToLowerInvariant();

                    if (pt == typeof(bool))
                    {
                        // Turn on anything that looks like "show loading screen/ui"
                        if (pn.Contains("show") || pn.Contains("display") || pn.Contains("loading") || pn.Contains("fade"))
                            args[i] = true;
                        else
                            args[i] = false;
                    }
                    else if (pt == typeof(int)) args[i] = 0;
                    else if (pt == typeof(float)) args[i] = 0f;
                    else if (pt.IsEnum) args[i] = Activator.CreateInstance(pt);
                    else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                }

                Debug.Log("[Dynasty] Using loader: " + t.FullName + "." + best.Name + "(" + string.Join(", ", p.Select(x => x.ParameterType.Name + " " + x.Name)) + ")");
                best.Invoke(loaderInstance, args);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] InvokeBestLoadLevel failed: " + e);
                return false;
            }
        }

        // --------------------------------------------------
        // Scene loaded / spawn
        // --------------------------------------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_waitingForPlacement) return;
            if (!string.Equals(scene.name, _targetScene, StringComparison.OrdinalIgnoreCase)) return;

            _waitingForPlacement = false;

            if (_core == null || _core.MasterData == null) return;

            Debug.Log("[Dynasty] Target scene loaded; waiting for grounded placement: " + scene.name);
            StartCoroutine(CoSpawnWatchdog(scene.name));
        }

        private IEnumerator CoSpawnWatchdog(string sceneName)
        {
            // Let colliders/terrain build
            yield return null;
            yield return null;
            yield return new WaitForSeconds(1.25f);

            float start = Time.realtimeSinceStartup;
            float duration = 25f;

            Vector3? markerPos = FindBestSpawnMarkerPositionV3();

            Character c0 = SafeGetLocalCharacter();
            if (c0 != null && markerPos.HasValue)
            {
                TrySnapToBestGround(c0, markerPos.Value);
            }

            while (Time.realtimeSinceStartup - start < duration)
            {
                Character c = SafeGetLocalCharacter();
                if (c == null)
                {
                    yield return null;
                    continue;
                }

                Vector3 pos = c.transform.position;
                bool underWorld = float.IsNaN(pos.y) || pos.y < -25f || pos.y > 9999f;

                // If our current ground under us is water-like, treat as bad
                bool onBadGround = !TryFindBestGround(pos, out var goodHit, maxDistance: 12000f, forbidWater: true);

                if (underWorld || onBadGround)
                {
                    Vector3 baseXZ = markerPos ?? pos;

                    if (TrySnapToBestGround(c, baseXZ))
                    {
                        yield return new WaitForSeconds(0.35f);
                        continue;
                    }
                }

                // Finalize when we have non-water ground beneath us
                if (!underWorld && TryFindBestGround(pos, out var finalHit, maxDistance: 12000f, forbidWater: true))
                {
                    // If somehow slightly below, lift
                    if (pos.y < finalHit.point.y - 0.5f)
                    {
                        c.transform.position = finalHit.point + Vector3.up * 1.25f;
                        ClearMomentum(c);
                        yield return new WaitForSeconds(0.35f);
                        continue;
                    }

                    _core.MasterData.PlayerPlaced = true;
                    _core.SaveDynasty();
                    Debug.Log("[Dynasty] PlayerPlaced = true (grounded) " + sceneName);
                    yield break;
                }

                yield return new WaitForSeconds(0.25f);
            }

            Debug.LogWarning("[Dynasty] SpawnWatchdog timed out; leaving player as-is.");
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

        // --------------------------------------------------
        // Spawn marker v3:
        // - Never accept container (0,0,0)
        // - If we find "PlayerSpawns", inspect children
        // --------------------------------------------------
        private Vector3? FindBestSpawnMarkerPositionV3()
        {
            try
            {
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                if (roots == null || roots.Length == 0) return null;

                List<Transform> raw = new List<Transform>(512);

                foreach (var go in roots)
                {
                    if (go == null) continue;
                    var all = go.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < all.Length; i++)
                    {
                        var t = all[i];
                        if (t == null) continue;
                        string n = (t.name ?? "").ToLowerInvariant();

                        // If it's a container called PlayerSpawns, add its children (not itself)
                        if (n == "playerspawns" || n.Contains("playerspawns"))
                        {
                            for (int c = 0; c < t.childCount; c++)
                            {
                                var ch = t.GetChild(c);
                                if (ch != null) raw.Add(ch);
                            }
                            continue;
                        }

                        // Strong positives
                        if (n.Contains("playerstart") || n.Contains("player_start") ||
                            n.Contains("playerspawn") || n.Contains("player_spawn") ||
                            n.Contains("spawnpoint") || n.Contains("spawn_point") ||
                            n.Contains("startpoint") || n.Contains("start_point") ||
                            n.Contains("entrypoint") || n.Contains("entry_point"))
                        {
                            raw.Add(t);
                        }
                    }
                }

                // Clean: reject origin + reject under-map markers
                var candidates = raw
                    .Where(t =>
                    {
                        if (t == null) return false;
                        var p = t.position;
                        if (Mathf.Abs(p.x) < 0.01f && Mathf.Abs(p.y) < 0.01f && Mathf.Abs(p.z) < 0.01f) return false; // origin container
                        if (p.y < -200f) return false;
                        return true;
                    })
                    .ToList();

                if (candidates.Count == 0) return null;

                Transform best = null;
                float bestScore = float.NegativeInfinity;

                foreach (var t in candidates)
                {
                    if (!TryFindBestGround(t.position, out var hit, maxDistance: 12000f, forbidWater: true))
                        continue;

                    float score = hit.point.y;
                    string n = (t.name ?? "").ToLowerInvariant();

                    if (n.Contains("playerstart") || n.Contains("player_start")) score += 50f;
                    if (n.Contains("entry") || n.Contains("arrival")) score += 10f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = t;
                    }
                }

                if (best != null)
                {
                    Debug.Log("[Dynasty] Found spawn marker (v3): " + best.name + " @ " + best.position);
                    return best.position;
                }
            }
            catch { }

            return null;
        }

        // --------------------------------------------------
        // Grounding: SphereCastAll and choose BEST hit
        // --------------------------------------------------
        private bool TryFindBestGround(Vector3 xz, out RaycastHit bestHit, float maxDistance, bool forbidWater)
        {
            bestHit = default;

            Vector3 rayStart = new Vector3(xz.x, 6000f, xz.z);

            RaycastHit[] hits;
            try
            {
                hits = Physics.SphereCastAll(rayStart, 0.8f, Vector3.down, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            }
            catch
            {
                return false;
            }

            if (hits == null || hits.Length == 0)
                return false;

            float bestScore = float.NegativeInfinity;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;

                if (forbidWater && IsWaterLike(h.collider))
                    continue;

                // Prefer higher surfaces (town floors above sea)
                float score = h.point.y;

                // Prefer real ground colliders a bit
                var col = h.collider;
                if (col is TerrainCollider) score += 25f;
                if (col is MeshCollider) score += 5f;

                // Reject extreme low unless nothing else exists
                if (score < -200f) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHit = h;
                    found = true;
                }
            }

            return found;
        }

        private bool TrySnapToBestGround(Character c, Vector3 baseXZ)
        {
            float[] rings = { 0f, 3f, 6f, 10f, 16f, 24f, 36f, 52f, 72f, 96f };

            for (int r = 0; r < rings.Length; r++)
            {
                float d = rings[r];

                Vector3[] offsets =
                {
                    new Vector3(0,0,0),
                    new Vector3( d,0, 0),
                    new Vector3(-d,0, 0),
                    new Vector3(0,0, d),
                    new Vector3(0,0,-d),
                    new Vector3( d,0, d),
                    new Vector3(-d,0, d),
                    new Vector3( d,0,-d),
                    new Vector3(-d,0,-d),
                };

                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 probe = new Vector3(baseXZ.x + offsets[i].x, 0f, baseXZ.z + offsets[i].z);

                    if (TryFindBestGround(probe, out var hit, maxDistance: 12000f, forbidWater: true))
                    {
                        c.transform.position = hit.point + Vector3.up * 1.25f;
                        ClearMomentum(c);
                        Debug.Log("[Dynasty] SafeSpawn snapped to ground at " + c.transform.position + " (best-hit no-water)");
                        return true;
                    }
                }
            }

            // If we truly cannot avoid water, fall back once (rare)
            if (TryFindBestGround(baseXZ, out var anyHit, maxDistance: 12000f, forbidWater: false))
            {
                c.transform.position = anyHit.point + Vector3.up * 1.25f;
                ClearMomentum(c);
                Debug.LogWarning("[Dynasty] SafeSpawn fallback snapped (water allowed) at " + c.transform.position);
                return true;
            }

            return false;
        }

        private bool IsWaterLike(Collider col)
        {
            try
            {
                if (col == null) return false;

                string n = (col.name ?? "").ToLowerInvariant();
                string go = (col.gameObject != null ? (col.gameObject.name ?? "") : "").ToLowerInvariant();

                if (n.Contains("water") || n.Contains("ocean") || n.Contains("sea")) return true;
                if (go.Contains("water") || go.Contains("ocean") || go.Contains("sea")) return true;

                // Material-name water check (often the reliable one)
                var rend = col.GetComponent<Renderer>() ?? col.GetComponentInParent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    string mn = (rend.sharedMaterial.name ?? "").ToLowerInvariant();
                    if (mn.Contains("water") || mn.Contains("ocean") || mn.Contains("sea")) return true;
                }
            }
            catch { }

            return false;
        }

        private void ClearMomentum(Character c)
        {
            try
            {
                var rb = c.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            catch { }
        }

        private void CleanupASMSoulGuides()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                int killed = 0;

                for (int i = 0; i < all.Length; i++)
                {
                    var go = all[i];
                    if (go == null || !go.scene.IsValid()) continue;

                    string n = go.name.ToLowerInvariant();
                    if (n.Contains("soul-guide") || n.Contains("startchooser"))
                    {
                        Destroy(go);
                        killed++;
                    }
                }

                if (killed > 0)
                    Debug.Log("[Dynasty] Cleanup: destroyed " + killed + " ASM Soul-Guide object(s).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Dynasty] Cleanup failed: " + e.Message);
            }
        }
    }
}