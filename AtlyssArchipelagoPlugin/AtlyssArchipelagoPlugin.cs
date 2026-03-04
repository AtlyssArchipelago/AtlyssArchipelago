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
        // Class options from slot data (matches Options.py: fighter=0, bandit=1, mystic=2)
        public int mainClassOption = 0;
        public int secondaryClassOption = 0;
        private bool shopSanityEnabled = false;

        // Progressive item counter
        private int progressivePortalCount = 0;

        // Portal tracking for random portal mode (all 17 portals from partner's Items.py)
        private Dictionary<string, bool> _portalItemsReceived = new Dictionary<string, bool>
        {
            { "Sanctum Portal", false },
            { "Outer Sanctum Portal", false },
            { "Arcwood Pass Portal", false },
            { "Effold Terrace Portal", false },
            { "Tuul Valley Portal", false },
            { "Catacombs Portal", false },
            { "Cresent Road Portal", false },
            { "Tuul Enclave Portal", false },
            { "Luvora Garden Portal", false },
            { "Cresent Keep Portal", false },
            { "Bularr Fortress Portal", false },
            { "Cresent Grove lvl 1 Portal", false },
            { "Cresent Grove lvl 2 Portal", false },
            { "Gate of the Moon Portal", false },
            { "Wall of the Stars Portal", false },
            { "Redwoud Portal", false },
            { "Trial of the Stars Portal", false },
        };

        // Maps portal item names to their scene paths (used by PortalUnlocks)
        // TODO: Scene paths for new areas need to be confirmed with dnSpy/Unity Explorer
        private Dictionary<string, string> _portalScenes = new Dictionary<string, string>
        {
            { "Sanctum Portal", "Assets/Scenes/00_zone_forest/_zone00_sanctum.unity" },
            { "Outer Sanctum Portal", "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity" },
            { "Arcwood Pass Portal", "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity" },
            { "Effold Terrace Portal", "Assets/Scenes/00_zone_forest/_zone00_effoldTerrace.unity" },
            { "Tuul Valley Portal", "Assets/Scenes/00_zone_forest/_zone00_tuulValley.unity" },
            { "Catacombs Portal", "Assets/Scenes/map_dungeon00_sanctumCatacombs.unity" },
            { "Cresent Road Portal", "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity" },
            { "Tuul Enclave Portal", "Assets/Scenes/00_zone_forest/_zone00_tuulEnclave.unity" },
            { "Luvora Garden Portal", "Assets/Scenes/00_zone_forest/_zone00_luvoraGarden.unity" },
            { "Cresent Keep Portal", "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity" },
            { "Bularr Fortress Portal", "Assets/Scenes/00_zone_forest/_zone00_bularFortress.unity" },
            { "Cresent Grove lvl 1 Portal", "Assets/Scenes/map_dungeon01_crescentGrove.unity" },
            { "Cresent Grove lvl 2 Portal", "Assets/Scenes/map_dungeon01_crescentGrove.unity" },
            { "Gate of the Moon Portal", "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity" },
            { "Wall of the Stars Portal", "Assets/Scenes/00_zone_forest/_zone00_wallOfTheStars.unity" },
            { "Redwoud Portal", "Assets/Scenes/map_zone00_redwoud.unity" },
            { "Trial of the Stars Portal", "Assets/Scenes/00_zone_forest/_zone00_trialOfTheStars.unity" },
        };

        // Progressive unlock order (sorted by portal_counts from partner's Locations.py)
        // Areas with portal_count 0 are always accessible in progressive mode
        private List<string> _progressivePortalOrder = new List<string>
        {
            "Outer Sanctum Portal",       // 1
            "Arcwood Pass Portal",        // 2
            "Catacombs Portal",           // 3
            "Effold Terrace Portal",      // 4
            "Tuul Valley Portal",         // 5
            "Cresent Road Portal",        // 6
            "Luvora Garden Portal",       // 7
            "Cresent Keep Portal",        // 8
            "Tuul Enclave Portal",        // 9
            "Cresent Grove lvl 1 Portal", // 10 (Grove lvl 2 also needs 10)
            "Bularr Fortress Portal",     // 11
        };

        // Shop sanity system (60 locations across 12 merchants)
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
            Logger.LogInfo("[AtlyssAP] ALL QUESTS + Commands + Item In Spike Storage + Progressive Equipment + 60 Shop Locations + 16 Achievements!");
            Logger.LogInfo("[AtlyssAP] Press F5 to connect to Archipelago");

            _harmony = new Harmony("com.azrael.atlyss.ap.harmony");

            scriptHolder = new GameObject("Archipelago Script Holder");
            DontDestroyOnLoad(scriptHolder);
            portalLocker = scriptHolder.AddComponent<PortalUnlocks>();

            // Initialize shop sanity system (60 locations across 12 merchants)
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