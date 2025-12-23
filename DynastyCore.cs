// ======================================================
// FILE: DynastyCore.cs
// FULL REWRITE
// PURPOSE:
// - Boot mod, patch Harmony, load save.
// - Attach ALL runtime systems, including the Main Menu toggle window.
// ======================================================

using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace OutwardDynasty
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class DynastyCore : BaseUnityPlugin
    {
        public const string GUID = "com.yourname.outwarddynasty";
        public const string NAME = "Outward Dynasty";
        public const string VERSION = "1.0.0";

        public static DynastyCore Instance;

        public DynastySaveData MasterData;

        // Session toggle (this is what the main menu button controls)
        public bool IsDynastyModeEnabled = true;

        private void Awake()
        {
            Instance = this;

            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll();

            // Load save (staging or per-character, depending on availability)
            MasterData = DynastySaveManager.Load();

            // Seed defaults
            gameObject.AddComponent<FactionsLibrary>().EnsureDefaults(MasterData);

            // =========================
            // Attach systems
            // =========================

            // MAIN MENU / IN-GAME TOGGLE WINDOW (this is your missing button)
            gameObject.AddComponent<DynastyModeToggleMenu>().Initialize(this);

            // Simulation/time
            gameObject.AddComponent<TimeManager>().Initialize(this);

            // Visuals/environment
            gameObject.AddComponent<EnvironmentManager>().Initialize(this);

            // UI + setup
            gameObject.AddComponent<DynastyHUD>().Initialize(this);
            gameObject.AddComponent<DynastyMenu>().Initialize(this);

            // Local-only effects
            gameObject.AddComponent<DynastyPlayerEffects>().Initialize(this);

            // Redirect to DreamWorld for setup
            gameObject.AddComponent<HeavenRedirectManager>().Initialize(this);

            // Permadeath logic
            gameObject.AddComponent<PermadeathManager>();

            // Siege placeholder
            SiegeManager siege = gameObject.AddComponent<SiegeManager>();
            siege.Initialize();

            // Optional companion app client
            gameObject.AddComponent<CompanionClient>();

            Debug.Log("[Dynasty] DynastyCore initialized.");
        }

        public void SaveDynasty()
        {
            DynastySaveManager.Save(MasterData);
            CompanionClient.Instance?.SendDynastySnapshot();
        }

        public void DisableGameplaySystems()
        {
            IsDynastyModeEnabled = false;
            Debug.Log("[Dynasty] Dynasty mode disabled (session toggle).");
        }

        public void EnableGameplaySystems()
        {
            IsDynastyModeEnabled = true;
            Debug.Log("[Dynasty] Dynasty mode enabled (session toggle).");
        }
    }
}
