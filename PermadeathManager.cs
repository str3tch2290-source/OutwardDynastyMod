// ===============================
// PermadeathManager.cs (REWRITE)
// Per-character dynasty wipe on death
// Sends player to DreamWorld (setup) instead of a menu scene
// ===============================

using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardDynasty
{
    public class PermadeathManager : MonoBehaviour
    {
        private const int ECHO_ITEM_ID = 9000500;

        // Use the same heaven scene as HeavenRedirectsManager
        private const string HEAVEN_SCENE = "DreamWorld";

        private static PermadeathManager _instance;

        private void Awake()
        {
            _instance = this;
        }

        // =============================
        // Harmony Patch: Character.Die
        // =============================
        [HarmonyPatch(typeof(Character), "Die")]
        private class CharacterDeathPatch
        {
            private static void Postfix(Character __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (!__instance.IsLocalPlayer) return;

                    DynastyCore core = DynastyCore.Instance;
                    if (core == null || core.MasterData == null) return;

                    // Only enforce permadeath when dynasty is actually running.
                    if (!core.IsDynastyModeEnabled) return;
                    if (!core.MasterData.DynastyStarted) return;

                    // Echo prevents death wipe
                    if (__instance.Inventory != null && __instance.Inventory.ItemCount(ECHO_ITEM_ID) > 0)
                    {
                        __instance.Inventory.RemoveItem(ECHO_ITEM_ID, 1);
                        __instance.CharacterUI?.ShowInfoNotification("An Echo shatters... You live again.");
                        return;
                    }

                    string reason = core.MasterData.IsApocalypseActive ? "Apocalypse Death" : "Permadeath";
                    WipeDynastyAndReturnToHeaven(__instance, core, reason);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Dynasty] Permadeath postfix error:\n" + ex);
                }
            }

            private static void WipeDynastyAndReturnToHeaven(Character c, DynastyCore core, string reason)
            {
                Debug.LogError($"[Dynasty] DYNASTY WIPE: {c.Name} | Reason={reason}");

                // 1) Delete this character's dynasty save file
                DynastySaveManager.DeleteCurrentCharacterSave();

                // 2) Reset in-memory state to a fresh dynasty (not started)
                core.MasterData = new DynastySaveData
                {
                    DynastyStarted = false,
                    PlayerPlaced = false,
                    DayCount = 0,
                    IsApocalypseActive = false,
                    ScourgeMultiplier = 1f,
                    Bonds = 0,
                    Influence = 0,
                    CurrentHostCharacterID = "NONE"
                };

                // Keep lists non-null (JsonUtility-safe)
                if (core.MasterData.CitizenIDs == null) core.MasterData.CitizenIDs = new System.Collections.Generic.List<string>();
                if (core.MasterData.Towns == null) core.MasterData.Towns = new System.Collections.Generic.List<TownData>();
                if (core.MasterData.Factions == null) core.MasterData.Factions = new System.Collections.Generic.List<FactionData>();

                // Seed defaults again (factions/towns)
                // FactionsLibrary is static; no component instance.
                var lib = (object)null;
                if (lib != null) FactionsLibrary.EnsureDefaults(core.MasterData);

                // 3) Save fresh state (creates a new dynasty file for this character)
                core.SaveDynasty();

                // 4) Notify player + go to DreamWorld setup
                c.CharacterUI?.ShowInfoNotification("PERMADEATH: Your dynasty was wiped. Returning to DreamWorld...");

                if (_instance != null)
                    _instance.StartCoroutine(_instance.KickToHeaven());
                else
                    SceneManager.LoadScene(HEAVEN_SCENE, LoadSceneMode.Single);
            }
        }

        private IEnumerator KickToHeaven()
        {
            // short delay so UI notification can appear
            yield return new WaitForSeconds(1.0f);

            Debug.Log("[Dynasty] Loading DreamWorld after dynasty wipe.");
            SceneManager.LoadScene(HEAVEN_SCENE, LoadSceneMode.Single);
        }
    }
}
