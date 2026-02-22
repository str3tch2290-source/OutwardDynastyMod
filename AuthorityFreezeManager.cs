using System;
using UnityEngine;

namespace OutwardDynasty
{
    /// <summary>
    /// Handles: Companion grace timer (10s), then hard freeze + input block.
    /// Also used during host migration.
    /// </summary>
    public class AuthorityFreezeManager : MonoBehaviour
    {
        public static AuthorityFreezeManager Instance;

        private float _disconnectStartedUnscaled = -1f;
        private bool _frozen;
        private float _preFreezeTimeScale = 1f;

        private InputFreezeManager _inputFreeze;

        public float GraceSeconds = 10f;

        public bool IsFrozen => _frozen;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _inputFreeze = gameObject.GetComponent<InputFreezeManager>();
            if (_inputFreeze == null) _inputFreeze = gameObject.AddComponent<InputFreezeManager>();
        }

        public void BeginFreeze(string reason)
        {
            if (_frozen) return;
            _preFreezeTimeScale = Time.timeScale;
            _frozen = true;
            Time.timeScale = 0f;
            _inputFreeze.Enable(reason);
        }

        public void EndFreeze()
        {
            if (!_frozen) return;
            _frozen = false;
            Time.timeScale = Mathf.Clamp(_preFreezeTimeScale, 0.05f, 5f);
            _inputFreeze.Disable();
            _disconnectStartedUnscaled = -1f;
        }

        public void NotifyConnected()
        {
            _disconnectStartedUnscaled = -1f;
            // Do not auto-unfreeze here; unfreeze is controlled by DynastyCore authority + migration.
        }

        
private bool ShouldFreezeOnDisconnect()
{
    try
    {
        // If we never connected, allow offline-solo play (warn only).
        var cc = CompanionClient.Instance;
        if (cc == null) return false;
        if (!cc.EverConnected) return false;

        // If player explicitly chose Join mode, companion is required.
        if (DynastyMenu.ForceJoinMode) return true;

        // Host can continue solo if companion dies, unless a migration is in progress.
        if (HostMigrationManager.IsMigrationInProgress) return true;

        return true; // connected session implies multiplayer authority
    }
    catch { return true; }
}

public void NotifyDisconnected()
        {
            if (_disconnectStartedUnscaled < 0f)
                _disconnectStartedUnscaled = Time.unscaledTime;
        }

        private void Update()
        {
            if (_frozen) return;

            if (_disconnectStartedUnscaled >= 0f)
            {
                float elapsed = Time.unscaledTime - _disconnectStartedUnscaled;
                if (elapsed >= GraceSeconds)
                {
                    if (ShouldFreezeOnDisconnect())
                    BeginFreeze("Companion disconnected (grace expired).");
                    else
                    {
                        // Soft allow solo-only
                        _disconnectStartedUnscaled = -1f;
                        Debug.LogWarning("[Dynasty] Companion disconnected; continuing offline-solo (no authority).");
                        DynastyHistory.LogEvent("companion_disconnected_solo");
                    }
}
            }
        }
    }
}
