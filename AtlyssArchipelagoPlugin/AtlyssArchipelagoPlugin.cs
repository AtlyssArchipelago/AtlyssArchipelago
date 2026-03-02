using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Newtonsoft.Json.Linq;

namespace AtlyssArchipelagoWIP
{
    // Split into partial classes for organization:
    // AtlyssArchipelagoPlugin.cs  - Plugin core, fields, lifecycle
    // DataTables.cs               - Item/location/quest dictionaries
    // ConnectionManager.cs        - Connect, disconnect, session tracking
    // ItemHandler.cs              - Receiving items, creating ItemData, currency
    // LocationDetection.cs        - Polling for levels, quests, skills, sending checks
    // ChatAndCommands.cs          - Chat messages, /commands, packet handling, DeathLink

    [BepInPlugin("com.azrael.atlyss.ap", "Atlyss Archipelago", "1.3.1")]
    public partial class AtlyssArchipelagoPlugin : BaseUnityPlugin
    {
        public static AtlyssArchipelagoPlugin Instance { get; private set; }
        public static ManualLogSource StaticLogger { get; private set; }
        private static Harmony _harmony;
        private static GameObject scriptHolder;
        private static PortalUnlocks portalLocker;

        private static readonly FieldInfo maxOnscreenMessages = AccessTools.Field(typeof(ChatBehaviour), "_maxGameLogicLines");

        public ConfigEntry<string> cfgServer;
        public ConfigEntry<string> cfgSlot;
        public ConfigEntry<string> cfgPassword;
        public ConfigEntry<bool> cfgDeathlink;
        private ConfigEntry<bool> cfgAutoConnect;

        public InputField apServer;
        public InputField apSlot;
        public InputField apPassword;
        public Toggle apDeathlink;

        // CHANGED: Made public so ShopSanity can access it for sending location checks
        // Was: private ArchipelagoSession _session;
        public ArchipelagoSession _session;
        public bool connected;
        private bool connecting;
        private Dictionary<string, object> slotData = new Dictionary<string, object>();

        public DeathLinkService _dlService;
        public static int reactingToDeathLink;

        private int goalOption = 3;
        public bool randomPortalsEnabled = false;
        // CHANGED: Renamed from equipmentProgressionOption to equipmentGatingOption
        // Now tracks Gated (1) vs Random (0) equipment placement mode.
        // Gated mode restricts equipment tiers to appropriate-level locations during seed generation (Python-side).
        // Random mode allows equipment to appear anywhere. Both modes are purely seed-generation logic;
        // the C# plugin just receives and stores equipment normally regardless of this setting.
        private int equipmentGatingOption = 0;
        private bool shopSanityEnabled = false;

        // Progressive item counters (incremented each time a progressive item is received)
        private int progressivePortalCount = 0;
        // REMOVED: progressiveEquipmentTier counter - Progressive Equipment item no longer exists.
        // Equipment distribution is now handled by Gated/Random item_rules during Python seed generation.
        // The C# plugin just receives equipment items normally without needing to track tiers.

        // UPDATED: Expanded from 2 portals to 11 portals with dictionary tracking
        private Dictionary<string, bool> _portalItemsReceived = new Dictionary<string, bool>
        {
            { "Outer Sanctum Portal", false },
            { "Effold Terrace Portal", false },
            { "Arcwood Pass Portal", false },
            { "Tull Valley Portal", false },
            { "Crescent Road Portal", false },
            { "Catacombs Portal", false },
            { "Luvora Garden Portal", false },
            { "Crescent Keep Portal", false },
            { "Tull Enclave Portal", false },
            { "Bularr Fortress Portal", false },
            { "Grove Portal", false },
        };

        // NEW: Maps portal item names to their scene paths
        private Dictionary<string, string> _portalScenes = new Dictionary<string, string>
        {
            { "Outer Sanctum Portal", "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity" },
            { "Effold Terrace Portal", "Assets/Scenes/00_zone_forest/_zone00_effoldTerrace.unity" },
            { "Arcwood Pass Portal", "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity" },
            { "Tull Valley Portal", "Assets/Scenes/00_zone_forest/_zone00_tuulValley.unity" },
            { "Crescent Road Portal", "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity" },
            { "Catacombs Portal", "Assets/Scenes/map_dungeon00_sanctumCatacombs.unity" },
            { "Luvora Garden Portal", "Assets/Scenes/00_zone_forest/_zone00_luvoraGarden.unity" },
            { "Crescent Keep Portal", "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity" },
            { "Tull Enclave Portal", "Assets/Scenes/00_zone_forest/_zone00_tuulEnclave.unity" },
            { "Bularr Fortress Portal", "Assets/Scenes/00_zone_forest/_zone00_bularFortress.unity" },
            { "Grove Portal", "Assets/Scenes/map_dungeon01_crescentGrove.unity" },
        };

        // NEW: Progressive unlock order (matches Python regions.py)
        private List<string> _progressivePortalOrder = new List<string>
        {
            "Outer Sanctum Portal",
            "Arcwood Pass Portal",
            "Catacombs Portal",
            "Effold Terrace Portal",
            "Tull Valley Portal",
            "Crescent Road Portal",
            "Luvora Garden Portal",
            "Crescent Keep Portal",
            "Tull Enclave Portal",
            "Grove Portal",
            "Bularr Fortress Portal",
        };

        // NEW: Shop sanity system (50 locations across 10 merchants)
        public ArchipelagoShopSanity _shopSanity;

        private readonly HashSet<long> _reportedChecks = new HashSet<long>();

        private int _lastLevel = 0;
        // NEW: Track previous profession levels to detect fishing/mining increases
        private int _previousFishingLevel = 1;
        private int _previousMiningLevel = 1;

        private HashSet<string> _completedQuests = new HashSet<string>();
        private bool _questDebugLogged = false;

        private string currentSessionId = "";
        private const string SESSION_FILE = "ap_session.json";

        private void Awake()
        {
            Instance = this;
            StaticLogger = Logger;
            cfgServer = Config.Bind("Connection", "Server", "localhost",
                "Archipelago host. You can also put host:port here.");
            cfgSlot = Config.Bind("Connection", "Slot", "Player",
                "Your Archipelago slot name.");
            cfgPassword = Config.Bind("Connection", "Password", "",
                "Room password (optional).");
            cfgAutoConnect = Config.Bind("Connection", "AutoConnect", false,
                "Auto-connect on game start.");
            cfgDeathlink = Config.Bind("Connection", "DeathLink", false,
                "If DeathLink should be enabled (can be changed ingame).");
            Logger.LogInfo("=== [AtlyssAP] Plugin loaded! Version 1.3.1 ===");
            Logger.LogInfo("[AtlyssAP] ALL QUESTS + Commands + Item In Spike Storage + 419 ITEMS (304 Equipment) + 50 Shop Locations!");
            Logger.LogInfo("[AtlyssAP] Press F5 to connect to Archipelago");

            _harmony = new Harmony("com.azrael.atlyss.ap.harmony");

            scriptHolder = new GameObject("Archipelago Script Holder");
            DontDestroyOnLoad(scriptHolder);
            portalLocker = scriptHolder.AddComponent<PortalUnlocks>();

            // NEW: Initialize shop sanity system (50 locations across 10 merchants)
            _shopSanity = new ArchipelagoShopSanity(this, Logger);

            try
            {
                _harmony.PatchAll();
                Logger.LogInfo("[AtlyssAP] Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to apply Harmony patches: {ex.Message}");
            }
        }

        private void Start()
        {
            if (cfgAutoConnect.Value)
            {
                Logger.LogInfo("[AtlyssAP] Auto-connecting...");
                TryConnect();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                TryConnect();
            }

            if (connected)
            {
                PollForLevelChanges();
                PollForQuestCompletions();
                // NEW: Poll for fishing and mining level changes
                PollForSkillLevelChanges();

                // REMOVED: Shop sanity polling - NO LONGER NEEDED
                // Purchases are now handled immediately via ShopPurchasePatch Harmony patch
                // Old polling system has been replaced with instant purchase detection
                // if (shopSanityEnabled && _shopSanity.IsInitialized)
                // {
                //     _shopSanity.PollForPurchases(_session);
                // }
            }
        }

        public void ReenableSettingsTabs() // helper function for the settings menu harmony patch
        {
            StartCoroutine(EnableSettingsTabs());
        }

        private IEnumerator EnableSettingsTabs()
        {
            yield return new WaitForEndOfFrame();
            GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_tabButtons").SetActive(true);
        }
    }
}