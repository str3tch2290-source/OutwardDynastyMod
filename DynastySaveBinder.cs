// ======================================================
// FILE: DynastySaveBinder.cs
// PURPOSE:
// - If we booted with DynastySave_New.json (no character yet),
//   bind the current MasterData to the local character as soon as one exists.
// - This prevents "global" staging state from affecting everyone.
// ======================================================

using UnityEngine;

namespace OutwardDynasty
{
    public class DynastySaveBinder : MonoBehaviour
    {
        private DynastyCore _core;
        private bool _bound;

        public void Initialize(DynastyCore core)
        {
            _core = core;
        }

        private void Update()
        {
            if (_bound) return;
            if (_core == null || _core.MasterData == null) return;

            // Wait until a real local character exists
            Character local = SafeGetLocalCharacter();
            if (local == null) return;

            // Force-cache ID so Delete/Save never hits staging by accident
            DynastySaveManager.ForceCacheCharacterID(local.UID.ToString());

            // IMPORTANT: this creates DynastySave_<UID>.json and deletes staging/legacy
            _core.SaveDynasty();

            Debug.Log("[Dynasty] SaveBinder: bound staging save to character UID: " + local.UID);

            _bound = true;
            enabled = false;
        }

        private static Character SafeGetLocalCharacter()
        {
            try
            {
                if (CharacterManager.Instance == null) return null;
                return CharacterManager.Instance.GetFirstLocalCharacter();
            }
            catch
            {
                return null;
            }
        }
    }
}
