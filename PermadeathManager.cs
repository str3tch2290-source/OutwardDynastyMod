// ===============================
// PermadeathManager.cs (REWRITE)
// Per-character dynasty wipe on death
// NO persistent ban -> player can start a NEW dynasty after dying
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

        // Change if your menu scene name differs
        private const string MENU_SCENE = "MainMenu";

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

                    // If dynasty never started, don't enforce wipe.
                    if (!core.MasterData.DynastyStarted) return;

                    // Echo prevents death wipe
                    if (__instance.Inventory != null && __instance.Inventory.ItemCount(ECHO_ITEM_ID) > 0)
                    {
                        __instance.Inventory.RemoveItem(ECHO_ITEM_ID, 1);
                        __instance.CharacterUI?.ShowInfoNotification("An Echo shatters... You live again.");
                        return;
                    }

                    // Apocalypse can still wipe, but DOES NOT ban forever.
                    // (If you want apocalypse to be "hard ban", tell me and I’ll re-add it.)
                    WipeDynastyAndReturnToMenu(__instance, core, core.MasterData.IsApocalypseActive ? "Apocalypse Death" : "Permadeath");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Dynasty] Permadeath postfix error:\n" + ex);
                }
            }

            private static void WipeDynastyAndReturnToMenu(Character c, DynastyCore core, string reason)
            {
                Debug.LogError($"[Dynasty] DYNASTY WIPE: {c.Name} | Reason={reason}");

                // 1) Delete this character's dynasty save file
                DynastySaveManager.DeleteCurrentCharacterSave();

                // 2) Reset in-memory state to a fresh dynasty (not started)
                // This allows the SAME character to start a new dynasty immediately.
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
                var lib = core.GetComponent<FactionsLibrary>();
                if (lib != null) lib.EnsureDefaults(core.MasterData);

                // 3) Save fresh state (creates a new dynasty file for this character)
                core.SaveDynasty();

                // 4) Notify player + kick to menu
                c.CharacterUI?.ShowInfoNotification("PERMADEATH: Your dynasty was wiped. You can start a new dynasty.");

                if (_instance != null)
                    _instance.StartCoroutine(_instance.KickToMenu());
                else
                    SceneManager.LoadScene(MENU_SCENE, LoadSceneMode.Single);
            }
        }

        private IEnumerator KickToMenu()
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("[Dynasty] Loading menu after dynasty wipe.");
            SceneManager.LoadScene(MENU_SCENE, LoadSceneMode.Single);
        }
    }
}
