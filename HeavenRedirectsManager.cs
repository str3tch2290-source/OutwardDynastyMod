// ======================================================
// HeavenRedirectsManager.cs (DISABLED)
// Dynasty fully replaces ASM start flow.
//
// Why:
// - Your log proves this class is sending you BACK to DreamWorld AFTER
//   you already loaded & grounded in Monsoon:
//
//   [Dynasty] Redirecting to DreamWorld for dynasty setup (ASM Aether).
//
// That behavior is incompatible with "Dynasty replaces ASM".
//
// This file remains as a component so DynastyCore's TryAttachAndInit
// won't fail, but it performs NO redirects.
// ======================================================

using UnityEngine;

namespace OutwardDynasty
{
    public class HeavenRedirectsManager : MonoBehaviour
    {
        private DynastyCore _core;

        public void Initialize(DynastyCore core)
        {
            _core = core;
            Debug.Log("[Dynasty] HeavenRedirectsManager: disabled (Dynasty replaces ASM start flow).");
        }

        // Intentionally no Update(), no scene load hooks, no redirects.
    }
}
