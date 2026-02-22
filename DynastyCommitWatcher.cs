using UnityEngine;
using BepInEx;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    /// <summary>
    /// Commit triggers:
    /// - On local sleep start (edge)
    /// - On scene/zone change (best-effort proxy for "players end in same zone")
    /// </summary>
    public class DynastyCommitWatcher : MonoBehaviour
    {
        private bool _prevSleeping;
        private string _lastScene;

        private void Start()
        {
            _prevSleeping = DynastySleepState.IsLocalSleeping;
            _lastScene = SceneManager.GetActiveScene().name;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            var core = DynastyCore.Instance;
            if (core == null || !core.IsDynastyModeEnabled || core.MasterData == null || !core.MasterData.DynastyStarted)
                return;

            bool nowSleeping = DynastySleepState.IsLocalSleeping;
            if (nowSleeping && !_prevSleeping)
            {
                TryCommit("sleep_start");
            }
            _prevSleeping = nowSleeping;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var core = DynastyCore.Instance;
            if (core == null || !core.IsDynastyModeEnabled || core.MasterData == null || !core.MasterData.DynastyStarted)
                return;

            if (!string.Equals(_lastScene, scene.name))
            {
                _lastScene = scene.name;
                TryCommit("zone_change");
            }
        }

        private void TryCommit(string reason)
        {
            try
            {
                var core = DynastyCore.Instance;
                if (core == null || core.MasterData == null) return;

                string snap = DynastySnapshotManager.BuildSnapshotJson(core.MasterData);

                // Save local copy as the latest commit (for rollback / resync)
                DynastyLocalCommitStore.SaveLatest(snap);

                // Push to Companion if available
                var cc = CompanionClient.Instance;
                if (cc != null && cc.AuthorityGranted)
                {
                    cc.PushSnapshot(snap, out var _);
                }
            }
            catch { }
        }
    }

    public static class DynastyLocalCommitStore
    {
        private const string FILE = "DynastyLatestCommit.json";

        public static void SaveLatest(string snapshotJson)
        {
            try
            {
                var path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, FILE);
                System.IO.File.WriteAllText(path, snapshotJson ?? "");
            }
            catch { }
        }

        public static string LoadLatest()
        {
            try
            {
                var path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, FILE);
                if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
            }
            catch { }
            return null;
        }
    }
}
