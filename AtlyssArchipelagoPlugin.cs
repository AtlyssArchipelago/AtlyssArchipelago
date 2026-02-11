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
using System.Collections.Concurrent;  // ADDED: For ConcurrentQueue to safely pass items from network thread to main thread
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Newtonsoft.Json.Linq;

namespace AtlyssArchipelagoWIP
{
    [BepInPlugin("com.azrael.atlyss.ap", "Atlyss Archipelago", "1.3.1")]
    public class AtlyssArchipelagoPlugin : BaseUnityPlugin
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
        public int areaAccessOption = 0;
        private bool shopSanityEnabled = false;

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

        // ADDED: Thread-safe queue for items received from Archipelago network thread.
        // OnItemReceived fires from a background thread, but Unity collections (like
        // vendor inventories, Spike storage) can only be modified on the main thread.
        // Without this queue, concurrent access causes InvalidOperationException spam.
        private readonly ConcurrentQueue<(string itemName, string fromPlayer)> _receivedItemQueue
            = new ConcurrentQueue<(string, string)>();

        private int _lastLevel = 0;
        // NEW: Track previous profession levels to detect fishing/mining increases
        private int _previousFishingLevel = 1;
        private int _previousMiningLevel = 1;

        private HashSet<string> _completedQuests = new HashSet<string>();
        private bool _questDebugLogged = false;

        private string currentSessionId = "";
        private const string SESSION_FILE = "ap_session.json";

        private const long BASE_LOCATION_ID = 591000;
        private const long DEFEAT_SLIME_DIVA = BASE_LOCATION_ID + 1;
        private const long DEFEAT_LORD_ZUULNERUDA = BASE_LOCATION_ID + 2;
        private const long DEFEAT_GALIUS = BASE_LOCATION_ID + 3;
        private const long DEFEAT_COLOSSUS = BASE_LOCATION_ID + 4;
        private const long DEFEAT_LORD_KALUUZ = BASE_LOCATION_ID + 5;
        private const long DEFEAT_VALDUR = BASE_LOCATION_ID + 6;
        private const long REACH_LEVEL_2 = BASE_LOCATION_ID + 10;

        // CORRECTED: Fixed multiple quest name typos to match locations.py exactly
        // Changes made:
        // - "The Colosseum" → "The Colossus"
        // - "Summons'" → "Summore'" (all instances)
        // - "Cleansing Terrace" → "Cleaning Terrace"
        // - "Ambente Ingots" → "Amberite Ingots"
        // - "Battlecage Rage" → "Rattlecage Rage"
        // - "Wicked Wizbars" → "Wicked Wizboars"
        // - "Reckoning Foes" → "Beckoning Foes"
        // - "Makin' a Giant Chestpiece" → "Makin' a Golem Chestpiece"
        // - "Canmore'" → "Summore'" (all instances)
        // - "Finding Armagorn" → "Finding Ammagon"
        // - "Reviling_the_Rageboars" → "Reviling the Rageboars" (removed underscore)
        // - "Reviling the Ragebears" → "Reviling More Rageboars"
        // - "Purging the Grave" → "Purging the Grove"
        // - "Summon'" → "Summore'" (all instances)
        // - ADDED "Spiraling In The Grove" (was missing)
        // - Renumbered IDs after 201 to accommodate missing quest
        private static readonly Dictionary<string, long> AllQuestToLocation = new Dictionary<string, long>
        {
            { "Diva Must Die", DEFEAT_SLIME_DIVA },
            { "The Voice of Zuulneruda", DEFEAT_LORD_ZUULNERUDA },
            { "Gatling Galius", DEFEAT_GALIUS },
            { "The Colossus", DEFEAT_COLOSSUS },  // FIXED: was "The Colosseum"

            { "A Warm Welcome", BASE_LOCATION_ID + 30 },
            { "Communing Catacombs", BASE_LOCATION_ID + 31 },

            { "Dense Ingots", BASE_LOCATION_ID + 100 },
            { "Ghostly Goods", BASE_LOCATION_ID + 101 },
            { "Killing Tomb", BASE_LOCATION_ID + 102 },
            { "Night Spirits", BASE_LOCATION_ID + 103 },
            { "Ridding Slimes", BASE_LOCATION_ID + 104 },
            { "Summore' Spectral Powder!", BASE_LOCATION_ID + 105 },  // FIXED: was "Summons'"

            { "Call of Fury", BASE_LOCATION_ID + 110 },
            { "Cold Shoulder", BASE_LOCATION_ID + 111 },
            { "Focusin' in", BASE_LOCATION_ID + 112 },

            { "Cleaning Terrace", BASE_LOCATION_ID + 115 },  // FIXED: was "Cleansing Terrace"
            { "Huntin' Hogs", BASE_LOCATION_ID + 116 },

            { "Amberite Ingots", BASE_LOCATION_ID + 120 },  // FIXED: was "Ambente Ingots"
            { "Makin' a Mekspear", BASE_LOCATION_ID + 121 },
            { "Makin' More Mekspears", BASE_LOCATION_ID + 122 },
            { "Purging the Undead", BASE_LOCATION_ID + 123 },
            { "Rattlecage Rage", BASE_LOCATION_ID + 124 },  // FIXED: was "Battlecage Rage"
            { "Ancient Beings", BASE_LOCATION_ID + 125 },

            { "Makin' a Vile Blade", BASE_LOCATION_ID + 130 },
            { "Makin' a Wizwand", BASE_LOCATION_ID + 131 },
            { "Makin' More Vile Blades", BASE_LOCATION_ID + 132 },
            { "Makin' More Wizwands", BASE_LOCATION_ID + 133 },
            { "Sapphite Ingots", BASE_LOCATION_ID + 134 },

            { "Devious Pact", BASE_LOCATION_ID + 140 },
            { "Disciple of Magic", BASE_LOCATION_ID + 141 },
            { "Mastery of Dexterity", BASE_LOCATION_ID + 142 },
            { "Mastery of Mind", BASE_LOCATION_ID + 143 },
            { "Mastery of Strength", BASE_LOCATION_ID + 144 },
            { "Strength and Honor", BASE_LOCATION_ID + 145 },
            { "Wicked Wizboars", BASE_LOCATION_ID + 146 },  // FIXED: was "Wicked Wizbars"

            { "Beckoning Foes", BASE_LOCATION_ID + 150 },  // FIXED: was "Reckoning Foes"
            { "Blossom of Life", BASE_LOCATION_ID + 151 },
            { "Consumed Madness", BASE_LOCATION_ID + 152 },
            { "Eradicating the Undead", BASE_LOCATION_ID + 153 },
            { "Makin' a Golem Chestpiece", BASE_LOCATION_ID + 154 },  // FIXED: was "Makin' a Giant Chestpiece"
            { "Summore' Golem Chestpieces", BASE_LOCATION_ID + 155 },  // FIXED: was "Canmore' Golem Chestpieces"
            { "Whatta' Rush!", BASE_LOCATION_ID + 156 },

            { "Finding Ammagon", BASE_LOCATION_ID + 160 },  // FIXED: was "Finding Armagorn"
            { "Reviling the Rageboars", BASE_LOCATION_ID + 161 },  // FIXED: was "Reviling_the_Rageboars" (removed underscore)
            { "Reviling More Rageboars", BASE_LOCATION_ID + 162 },  // FIXED: was "Reviling the Ragebears"

            { "Makin' a Ragespear", BASE_LOCATION_ID + 165 },
            { "Makin' More Ragespears", BASE_LOCATION_ID + 166 },
            { "Purging the Grove", BASE_LOCATION_ID + 167 },  // FIXED: was "Purging the Grave"
            { "Searching for the Grove", BASE_LOCATION_ID + 168 },
            { "Tethering Grove", BASE_LOCATION_ID + 169 },
            { "Up and Over It", BASE_LOCATION_ID + 170 },

            { "Makin' a Monolith Chestpiece", BASE_LOCATION_ID + 175 },
            { "Summore' Monolith Chestpieces", BASE_LOCATION_ID + 176 },  // FIXED: was "Summons' Monolith Chestpieces"

            { "Facing Foes", BASE_LOCATION_ID + 180 },

            { "Cleansing the Grove", BASE_LOCATION_ID + 200 },
            { "Hell In The Grove", BASE_LOCATION_ID + 201 },  // FIXED: was swapped with Spiraling (now matches locations.py)
            { "Spiraling In The Grove", BASE_LOCATION_ID + 202 },  // FIXED: was swapped with Hell (now matches locations.py)
            { "Makin' a Firebreath Blade", BASE_LOCATION_ID + 203 },  // RENUMBERED: was 202
            { "Nulversa Magica", BASE_LOCATION_ID + 204 },  // RENUMBERED: was 203
            { "Nulversa Viscera", BASE_LOCATION_ID + 205 },  // RENUMBERED: was 204
            { "Nulversa, Greenveras!", BASE_LOCATION_ID + 206 },  // RENUMBERED: was 205
            { "Summore' Firebreath Blades", BASE_LOCATION_ID + 207 },  // FIXED & RENUMBERED: was "Summon'" at 206

            { "The Gall of Galius", BASE_LOCATION_ID + 220 },

            { "Makin' a Follycannon", BASE_LOCATION_ID + 240 },
            { "Makin' More Follycannons", BASE_LOCATION_ID + 241 },
            { "The Glyphik Booklet", BASE_LOCATION_ID + 242 }
        };

        private static readonly Dictionary<long, string> LocationIdToName = new Dictionary<long, string>
        {
            { 591010, "Reach Level 2" },
            { 591011, "Reach Level 4" },
            { 591012, "Reach Level 6" },
            { 591013, "Reach Level 8" },
            { 591014, "Reach Level 10" },
            { 591015, "Reach Level 12" },
            { 591016, "Reach Level 14" },
            { 591017, "Reach Level 16" },
            { 591018, "Reach Level 18" },
            { 591019, "Reach Level 20" },
            { 591020, "Reach Level 22" },
            { 591021, "Reach Level 24" },
            { 591022, "Reach Level 26" },
            { 591023, "Reach Level 28" },
            { 591024, "Reach Level 30" },
            { 591025, "Reach Level 32" },

            { 591001, "Defeat Slime Diva" },
            { 591002, "Defeat Lord Zuulneruda" },
            { 591003, "Defeat Galius" },
        };

        // NEW: Fishing level locations (591400-591408) - Added for fishing progression tracking
        private static readonly Dictionary<int, long> FishingLevelLocations = new Dictionary<int, long>
        {
            { 2, 591400 },
            { 3, 591401 },
            { 4, 591402 },
            { 5, 591403 },
            { 6, 591404 },
            { 7, 591405 },
            { 8, 591406 },
            { 9, 591407 },
            { 10, 591408 }
        };

        // NEW: Mining level locations (591409-591417) - Added for mining progression tracking
        private static readonly Dictionary<int, long> MiningLevelLocations = new Dictionary<int, long>
        {
            { 2, 591409 },
            { 3, 591410 },
            { 4, 591411 },
            { 5, 591412 },
            { 6, 591413 },
            { 7, 591414 },
            { 8, 591415 },
            { 9, 591416 },
            { 10, 591417 }
        };

        private string GetLocationName(long locationId)
        {
            if (LocationIdToName.TryGetValue(locationId, out string name))
                return name;
            return $"Location {locationId}";
        }

        // Removed: 6 class tomes from item pool (Fighter, Mystic, Bandit, Paladin, Magus, Bishop)
        // Removed: 9 skill scrolls from item pool (Alacrity, Sturdy, Fira, Crya, Str/Dex/Mind Mastery, Taunt, Curis)
        // UPDATED: Now includes 139 trade items (monster drops, ores, fish, special items)
        // New total: 261 items (was 191)
        public static readonly Dictionary<string, string> ItemNameMapping = new Dictionary<string, string>
        {
            // Consumables
            { "Bunbag Pack", "(lv-0) STATUSCONSUMABLE_Bunbag" },
            { "Bunjar Pack", "(lv-0) STATUSCONSUMABLE_Bunjar" },
            { "Bunpot Pack", "(lv-0) STATUSCONSUMABLE_Bunpot" },
            { "Regen Potion Pack", "(lv-10) STATUSCONSUMABLE_Regen Potion" },
            { "Regen Vial Pack", "(lv-0) STATUSCONSUMABLE_Regen Vial" },

            { "Magiclove Pack", "(lv-0) STATUSCONSUMABLE_Magiclove" },
            { "Magiflower Pack", "(lv-0) STATUSCONSUMABLE_Magiflower" },
            { "Magileaf Pack", "(lv-0) STATUSCONSUMABLE_Magileaf" },

            { "Stamstar Pack", "(lv-0) STATUSCONSUMABLE_Stamstar" },

            { "Agility Potion Pack", "(lv-10) STATUSCONSUMABLE_Agility Potion" },
            { "Agility Vial Pack", "(lv-0) STATUSCONSUMABLE_Agility Vial" },
            { "Bolster Potion Pack", "(lv-10) STATUSCONSUMABLE_Bolster Potion" },
            { "Bolster Vial Pack", "(lv-0) STATUSCONSUMABLE_Bolster Vial" },
            { "Wisdom Potion Pack", "(lv-10) STATUSCONSUMABLE_Wisdom Potion" },
            { "Wisdom Vial Pack", "(lv-0) STATUSCONSUMABLE_Wisdom Vial" },

            { "Tome of Greater Experience", "(lv-0) STATUSCONSUMABLE_Tome of Greater Experience" },
            { "Tome of Experience", "(lv-0) STATUSCONSUMABLE_Tome of Experience" },
            { "Tome of Lesser Experience", "(lv-0) STATUSCONSUMABLE_Tome of Lesser Experience" },

            { "Carrot Cake Pack", "(lv-0) STATUSCONSUMABLE_Carrot Cake" },
            { "Minchroom Juice Pack", "(lv-0) STATUSCONSUMABLE_Minchroom Juice" },
            { "Spectral Powder Pack", "(lv-0) STATUSCONSUMABLE_Spectral Powder" },

            // Trade Items - Badges
            { "Geistlord Badge Pack", "TRADEITEM_Geistlord Badge" },
            { "Coldgeist Badge Pack", "TRADEITEM_Coldgeist Badge" },
            { "Earthcore Badge Pack", "TRADEITEM_Earthcore Badge" },
            { "Windcore Badge Pack", "TRADEITEM_Windcore Badge" },

            // Trade Items - Clusters/Ingots (existing)
            { "Iron Cluster Pack", "TRADEITEM_Iron Cluster" },
            { "Copper Cluster Pack", "TRADEITEM_Copper Cluster" },
            { "Mithril Cluster Pack", "TRADEITEM_Mithril Cluster" },
            { "Dense Ingot Pack", "TRADEITEM_Dense Ingot" },
            { "Sapphite Ingot Pack", "TRADEITEM_Sapphite Ingot" },
            { "Amberite Ingot Pack", "TRADEITEM_Amberite Ingot" },

            { "Soul Pearl", "TRADEITEM_Soul Pearl" },
            { "Experience Bond Pack", "TRADEITEM_Experience Bond" },

            // Weapons
            { "Wood Sword", "(lv-1) WEAPON_Wood Sword (Sword, Strength)" },
            { "Wooden Bow", "(lv-1) WEAPON_Wooden Bow (Bow, Dexterity)" },
            { "Wood Scepter", "(lv-1) WEAPON_Wood Scepter (Scepter, Mind)" },
            { "Crypt Blade", "(lv-2) WEAPON_Crypt Blade (Sword, Strength)" },
            { "Slimecrust Blade", "(lv-2) WEAPON_Slimecrust Blade (Sword, Strength)" },

            { "Gilded Sword", "(lv-4) WEAPON_Gilded Sword (Sword, Strength)" },
            { "Mini Geist Scythe", "(lv-4) WEAPON_Mini Geist Scythe (Greatblade, Strength)" },
            { "Iron Sword", "(lv-6) WEAPON_Iron Sword (Sword, Strength)" },
            { "Iron Bow", "(lv-6) WEAPON_Iron Bow (Bow, Dexterity)" },
            { "Dense Hammer", "(lv-6) WEAPON_Dense Hammer (Hammer, Strength)" },
            { "Dense Katars", "(lv-6) WEAPON_Dense Katars (Katars, Dexterity)" },

            { "Vile Blade", "(lv-8) WEAPON_Vile Blade (Sword, Strength)" },
            { "Mekspear", "(lv-8) WEAPON_Mekspear (Polearm, Strength)" },
            { "Menace Bow", "(lv-8) WEAPON_Menace Bow (Bow, Dexterity)" },
            { "Cryptcall Bell", "(lv-8) WEAPON_Cryptcall Bell (Magic Bell, Mind)" },

            { "Wizwand", "(lv-12) WEAPON_Wizwand (Scepter, Mind)" },
            { "Amberite Sword", "(lv-12) WEAPON_Amberite Sword (Sword, Strength)" },
            { "Geistlord Claws", "(lv-12) WEAPON_Geistlord Claws (Katars, Dexterity)" },
            { "Petrified Bow", "(lv-12) WEAPON_Petrified Bow (Bow, Dexterity)" },

            { "Mithril Sword", "(lv-16) WEAPON_Mithril Sword (Sword, Strength)" },
            { "Mithril Bow", "(lv-14) WEAPON_Mithril Bow (Bow, Dexterity)" },
            { "Ragespear", "(lv-16) WEAPON_Ragespear (Polearm, Strength)" },
            { "Coldgeist Blade", "(lv-16) WEAPON_Coldgeist Blade (Sword, Strength)" },

            { "Sapphite Spear", "(lv-18) WEAPON_Sapphite Spear (Polearm, Strength)" },
            { "Colossus Tone", "(lv-18) WEAPON_Colossus Tone (Magic Bell, Mind)" },
            { "Magitek Burstgun", "(lv-20) WEAPON_Magitek Burstgun (Shotgun, Dexterity)" },

            { "Firebreath Blade", "(lv-22) WEAPON_Firebreath Blade (Sword, Strength)" },
            { "Valdur Blade", "(lv-24) WEAPON_Valdur Blade (Sword, Strength)" },
            { "Torrentius Longbow", "(lv-24) WEAPON_Torrentius Longbow (Bow, Dexterity)" },

            { "Follycannon", "(lv-26) WEAPON_Follycannon (Shotgun, Dexterity)" },
            { "Fier Blade", "(lv-26) WEAPON_Fier Blade (Sword, Strength)" },

            // Armor - Helms
            { "Leather Cap", "(lv-1) HELM_Leather Cap" },
            { "Fishin Hat", "(lv-1) HELM_Fishin Hat" },
            { "Iron Halo", "(lv-6) HELM_Iron Halo" },
            { "Dense Helm", "(lv-6) HELM_Dense Helm" },
            { "Diva Crown", "(lv-6) HELM_Diva Crown" },
            { "Geistlord Crown", "(lv-10) HELM_Geistlord Crown" },
            { "Amberite Helm", "(lv-12) HELM_Amberite Helm" },
            { "Mithril Halo", "(lv-16) HELM_Mithril Halo" },
            { "Sapphite Mindhat", "(lv-18) HELM_Sapphite Mindhat" },
            { "Wizlad Hood", "(lv-24) HELM_Wizlad Hood" },
            { "Deathknight Helm", "(lv-24) HELM_Deathknight Helm" },

            // Armor - Chestpieces
            { "Leather Top", "(lv-1) CHESTPIECE_Leather Top" },
            { "Noble Shirt", "(lv-1) CHESTPIECE_Noble Shirt" },
            { "Iron Chestpiece", "(lv-6) CHESTPIECE_Iron Chestpiece" },
            { "Dense Chestpiece", "(lv-6) CHESTPIECE_Dense Chestpiece" },
            { "Golem Chestpiece", "(lv-12) CHESTPIECE_Golem Chestpiece" },
            { "Amberite Breastplate", "(lv-12) CHESTPIECE_Amberite Breastplate" },
            { "Mithril Chestpiece", "(lv-16) CHESTPIECE_Mithril Chestpiece" },
            { "King Breastplate", "(lv-16) CHESTPIECE_King Breastplate" },
            { "Monolith Chestpiece", "(lv-18) CHESTPIECE_Monolith Chestpiece" },
            { "Sapphite Guard", "(lv-18) CHESTPIECE_Sapphite Guard" },
            { "Wizlad Robe", "(lv-24) CHESTPIECE_Wizlad Robe" },
            { "Executioner Vestment", "(lv-24) CHESTPIECE_Executioner Vestment" },

            // Armor - Leggings
            { "Leather Britches", "(lv-1) LEGGINGS_Leather Britches" },
            { "Dense Leggings", "(lv-6) LEGGINGS_Dense Leggings" },
            { "Amberite Leggings", "(lv-12) LEGGINGS_Amberite Leggings" },
            { "Lord Greaves", "(lv-12) LEGGINGS_Lord Greaves" },
            { "King Greaves", "(lv-16) LEGGINGS_King Greaves" },
            { "Sapphite Leggings", "(lv-18) LEGGINGS_Sapphite Leggings" },
            { "Berserker Leggings", "(lv-18) LEGGINGS_Berserker Leggings" },
            { "Executioner Leggings", "(lv-24) LEGGINGS_Executioner Leggings" },

            // Accessories - Capes
            { "Initiate Cloak", "(lv-4) CAPE_Initiate Cloak" },
            { "Nokket Cloak", "(lv-6) CAPE_Nokket Cloak" },
            { "Regazuul Cape", "(lv-10) CAPE_Regazuul Cape" },
            { "Flux Cloak", "(lv-12) CAPE_Flux Cloak" },
            { "Nulversa Cape", "(lv-20) CAPE_Nulversa Cape" },
            { "Windgolem Cloak", "(lv-22) CAPE_Windgolem Cloak" },

            // Accessories - Shields
            { "Wooden Shield", "(lv-1) SHIELD_Wooden Shield" },
            { "Iron Shield", "(lv-6) SHIELD_Iron Shield" },
            { "Dense Shield", "(lv-6) SHIELD_Dense Shield" },
            { "Amberite Shield", "(lv-12) SHIELD_Amberite Shield" },
            { "Sapphite Shield", "(lv-18) SHIELD_Sapphite Shield" },

            // Accessories - Rings
            { "Old Ring", "(lv-1) RING_Old Ring" },
            { "Ring Of Ambition", "(lv-1) RING_Ring Of Ambition" },
            { "Sapphireweave Ring", "(lv-6) RING_Sapphireweave Ring" },
            { "Emeraldfocus Ring", "(lv-6) RING_Emeraldfocus Ring" },
            { "Ambersquire Ring", "(lv-6) RING_Ambersquire Ring" },
            { "Geistlord Ring", "(lv-12) RING_Geistlord Ring" },
            { "Geistlord Band", "(lv-16) RING_Geistlord Band" },
            { "Valor Ring", "(lv-16) RING_Valor Ring" },
            { "Valdur Effigy", "(lv-24) RING_Valdur Effigy" },

            // NEW: Trade Items - Monster Drops (139 items added)
            { "Aqua Muchroom Cap", "TRADEITEM_Aqua Muchroom Cap" },
            { "Barknaught Face", "TRADEITEM_Barknaught Face" },
            { "Blightwood Log", "TRADEITEM_Blightwood Log" },
            { "Blightwood Stick", "TRADEITEM_Blightwood Stick" },
            { "Blue Minchroom Cap", "TRADEITEM_Blue Minchroom Cap" },
            { "Boomboar Gear", "TRADEITEM_Boomboar Gear" },
            { "Boomboar Head", "TRADEITEM_Boomboar Head" },
            { "Boomboar Pouch", "TRADEITEM_Boomboar Pouch" },
            { "Burnrose", "TRADEITEM_Burnrose" },
            { "Carbuncle Foot", "TRADEITEM_Carbuncle Foot" },
            { "Cursed Note", "TRADEITEM_Cursed Note" },
            { "Deadwood Log", "TRADEITEM_Deadwood Log" },
            { "Deathgel Core", "TRADEITEM_Deathgel Core" },
            { "Deathknight Gauntlet", "TRADEITEM_Deathknight Gauntlet" },
            { "Demigolem Core", "TRADEITEM_Demigolem Core" },
            { "Demigolem Gem", "TRADEITEM_Demigolem Gem" },
            { "Diva Necklace", "TRADEITEM_Diva Necklace" },
            { "Firebreath Gland", "TRADEITEM_Firebreath Gland" },
            { "Fluxfern", "TRADEITEM_Fluxfern" },
            { "Gale Muchroom Cap", "TRADEITEM_Gale Muchroom Cap" },
            { "Geist Collar", "TRADEITEM_Geist Collar" },
            { "Ghostdust", "TRADEITEM_Ghostdust" },
            { "Golem Core", "TRADEITEM_Golem Core" },
            { "Golem Gem", "TRADEITEM_Golem Gem" },
            { "Green Lipstick", "TRADEITEM_Green Lipstick" },
            { "Hellsludge Core", "TRADEITEM_Hellsludge Core" },
            { "Maw Eye", "TRADEITEM_Maw Eye" },
            { "Mekboar Head", "TRADEITEM_Mekboar Head" },
            { "Mekboar Spear", "TRADEITEM_Mekboar Spear" },
            { "Mekboar Nail", "TRADEITEM_Mekboar Nail" },
            { "Mekboar Nosering", "TRADEITEM_Mekboar Nosering" },
            { "Mekboar Spine", "TRADEITEM_Mekboar Spine" },
            { "Monolith Core", "TRADEITEM_Monolith Core" },
            { "Monolith Gem", "TRADEITEM_Monolith Gem" },
            { "Mouth Bittertooth", "TRADEITEM_Mouth Bittertooth" },
            { "Mouth Eye", "TRADEITEM_Mouth Eye" },
            { "Rageboar Head", "TRADEITEM_Rageboar Head" },
            { "Rageboar Spear", "TRADEITEM_Rageboar Spear" },
            { "Red Minchroom Cap", "TRADEITEM_Red Minchroom Cap" },
            { "Rock", "TRADEITEM_Rock" },
            { "Slime Core", "TRADEITEM_Slime Core" },
            { "Slime Diva Ears", "TRADEITEM_Slime Diva Ears" },
            { "Slime Ears", "TRADEITEM_Slime Ears" },
            { "Slimek Core", "TRADEITEM_Slimek Core" },
            { "Slimek Ears", "TRADEITEM_Slimek Ears" },
            { "Slimek Eye", "TRADEITEM_Slimek Eye" },
            { "Vinethorn", "TRADEITEM_Vinethorn" },
            { "Vout Antennae", "TRADEITEM_Vout Antennae" },
            { "Vout Wing", "TRADEITEM_Vout Wing" },
            { "Warboar Axe", "TRADEITEM_Warboar Axe" },
            { "Warboar Head", "TRADEITEM_Warboar Head" },
            { "Wizboar Head", "TRADEITEM_Wizboar Head" },
            { "Wizboar Scepter", "TRADEITEM_Wizboar Scepter" },
            { "Geistlord Nails", "TRADEITEM_Geistlord Nails" },

            // NEW: Trade Items - Ores/Ingots (non-Pack versions)
            { "Amberite Ingot", "TRADEITEM_Amberite Ingot" },
            { "Amberite Ore", "TRADEITEM_Amberite Ore" },
            { "Dense Ingot", "TRADEITEM_Dense Ingot" },
            { "Dense Ore", "TRADEITEM_Dense Ore" },
            { "Copper Cluster", "TRADEITEM_Copper Cluster" },
            { "Iron Cluster", "TRADEITEM_Iron Cluster" },
            { "Mithril Cluster", "TRADEITEM_Mithril Cluster" },
            { "Sapphite Ingot", "TRADEITEM_Sapphite Ingot" },
            { "Sapphite Ore", "TRADEITEM_Sapphite Ore" },
            { "Coal", "TRADEITEM_Coal" },

            // NEW: Trade Items - Fish
            { "Big Wan", "TRADEITEM_Big Wan" },
            { "Bittering Katfish", "TRADEITEM_Bittering Katfish" },
            { "Bonefish", "TRADEITEM_Bonefish" },
            { "Smiling Wrellfish", "TRADEITEM_Smiling Wrellfish" },
            { "Squangfish", "TRADEITEM_Squangfish" },
            { "Sugeel", "TRADEITEM_Sugeel" },
            { "Sugshrimp", "TRADEITEM_Sugshrimp" },
            { "Windtail Fish", "TRADEITEM_Windtail Fish" },
            { "Old Boot", "TRADEITEM_Old Boot" },
            { "Bronze Arrows", "TRADEITEM_Bronze Arrows" },

            // NEW: Trade Items - Special Stones/Gems
            { "Agility Stone", "TRADEITEM_Agility Stone" },
            { "Angela's Tear", "TRADEITEM_Angela's Tear" },
            { "Epic Carrot", "TRADEITEM_Epic Carrot" },
            { "Experience Bond", "TRADEITEM_Experience Bond" },
            { "Flux Stone", "TRADEITEM_Flux Stone" },
            { "Illusion Stone", "TRADEITEM_Illusion Stone" },
            { "Might Stone", "TRADEITEM_Might Stone" },
            { "Starlight Gem", "TRADEITEM_Starlight Gem" }
        };

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
            Logger.LogInfo("[AtlyssAP] ALL QUESTS + Commands + Item Drops + 261 ITEMS + 50 Shop Locations!"); // UPDATED
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
                // ADDED: Process items received from Archipelago on the main thread.
                // This drains the ConcurrentQueue that OnItemReceived fills from the
                // network thread, ensuring all Unity object modifications happen safely.
                while (_receivedItemQueue.TryDequeue(out var received))
                {
                    try
                    {
                        SendAPChatMessage(
                            $"Received <color=yellow>{received.itemName}</color> " +
                            $"from <color=#00FFFF>{received.fromPlayer}</color>!"
                        );
                        HandleReceivedItem(received.itemName);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[AtlyssAP] Error processing queued item '{received.itemName}': {ex.Message}");
                    }
                }

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

        private void PollForLevelChanges()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
                if (stats == null) return;
                int currentLevel = stats.Network_currentLevel;

                if (currentLevel != _lastLevel)
                {
                    Logger.LogInfo($"[AtlyssAP] Level changed: {_lastLevel} -> {currentLevel}");

                    if (currentLevel >= 2 && currentLevel <= 32 && currentLevel % 2 == 0)
                    {
                        long locationId = REACH_LEVEL_2 + ((currentLevel - 2) / 2);
                        if (!_reportedChecks.Contains(locationId))
                        {
                            SendCheckById(locationId);

                            SendAPChatMessage(
                                $"Found <color=yellow>Reach Level {currentLevel}</color>! " +
                                $"Sent item to another player!"
                            );
                            Logger.LogInfo($"[AtlyssAP] Reached Level {currentLevel} milestone!");
                        }
                    }
                    _lastLevel = currentLevel;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error polling level: {ex.Message}");
            }
        }

        // NEW: Poll for fishing and mining level changes - Added to track profession skill levels
        private void PollForSkillLevelChanges()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;

                // Get PlayerStats component to access profession data
                PlayerStats playerStats = localPlayer._pStats;
                if (playerStats == null) return;

                // Search through professions SyncList to find fishing and mining by name
                for (int i = 0; i < playerStats._syncProfessions.Count; i++)
                {
                    ProfessionStruct profession = playerStats._syncProfessions[i];

                    // Get the ScriptableProfession to access the profession name
                    ScriptableProfession scriptableProfession = null;
                    if (GameManager._current != null && GameManager._current._statLogics != null)
                    {
                        if (i < GameManager._current._statLogics._scriptableProfessions.Length)
                        {
                            scriptableProfession = GameManager._current._statLogics._scriptableProfessions[i];
                        }
                    }

                    if (scriptableProfession == null)
                        continue;

                    // Check if this is the Fishing profession
                    if (scriptableProfession._professionName == "Fishing")
                    {
                        int currentFishingLevel = profession._professionLvl;
                        if (currentFishingLevel > _previousFishingLevel)
                        {
                            // Send checks for all levels between previous and current
                            for (int level = _previousFishingLevel + 1; level <= currentFishingLevel; level++)
                            {
                                if (FishingLevelLocations.TryGetValue(level, out long locationId))
                                {
                                    SendCheckById(locationId);
                                    SendAPChatMessage(
                                        $"Found <color=yellow>Fishing Level {level}</color>! " +
                                        $"Sent item to another player!"
                                    );
                                    Logger.LogInfo($"[AtlyssAP] Fishing level {level} reached!");
                                }
                            }
                            _previousFishingLevel = currentFishingLevel;
                        }
                    }

                    // Check if this is the Mining profession
                    if (scriptableProfession._professionName == "Mining")
                    {
                        int currentMiningLevel = profession._professionLvl;
                        if (currentMiningLevel > _previousMiningLevel)
                        {
                            // Send checks for all levels between previous and current
                            for (int level = _previousMiningLevel + 1; level <= currentMiningLevel; level++)
                            {
                                if (MiningLevelLocations.TryGetValue(level, out long locationId))
                                {
                                    SendCheckById(locationId);
                                    SendAPChatMessage(
                                        $"Found <color=yellow>Mining Level {level}</color>! " +
                                        $"Sent item to another player!"
                                    );
                                    Logger.LogInfo($"[AtlyssAP] Mining level {level} reached!");
                                }
                            }
                            _previousMiningLevel = currentMiningLevel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error checking skill levels: {ex.Message}");
            }
        }

        private void PollForQuestCompletions()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                PlayerQuesting questing = localPlayer.GetComponent<PlayerQuesting>();
                if (questing == null)
                {
                    return;
                }
                if (questing._finishedQuests == null)
                {
                    return;
                }

                if (!_questDebugLogged && questing._finishedQuests.Count > 0)
                {
                    Logger.LogInfo($"[AtlyssAP] DEBUG: Found {questing._finishedQuests.Count} completed quests");
                    foreach (var quest in questing._finishedQuests.Keys)
                    {
                        Logger.LogInfo($"[AtlyssAP] DEBUG: Completed quest: '{quest}'");
                    }
                    _questDebugLogged = true;
                }

                foreach (var kvp in AllQuestToLocation)
                {
                    string questName = kvp.Key;
                    long locationId = kvp.Value;

                    if (questing._finishedQuests.ContainsKey(questName) && !_completedQuests.Contains(questName))
                    {
                        SendCheckById(locationId);
                        _completedQuests.Add(questName);

                        SendAPChatMessage(
                            $"Found <color=yellow>{questName}</color>! " +
                            $"Sent item to another player!"
                        );
                        Logger.LogInfo($"[AtlyssAP] Quest completed: {questName}");
                        if (questName == "Gatling Galius")
                        {
                            SendAPChatMessage(
                                $"<color=gold>VICTORY!</color> " +
                                $"<color=yellow>You completed your goal!</color>"
                            );
                            Logger.LogInfo($"[AtlyssAP] VICTORY! Defeated final boss Galius!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error polling quests: {ex.Message}");
            }
        }

        private bool IsNewSession(string sessionId)
        {
            try
            {
                string sessionPath = Path.Combine(Application.persistentDataPath, SESSION_FILE);
                if (!File.Exists(sessionPath))
                    return true;

                string savedId = File.ReadAllText(sessionPath);
                return savedId != sessionId;
            }
            catch
            {
                return true;
            }
        }

        private void SaveSessionId(string sessionId)
        {
            try
            {
                string sessionPath = Path.Combine(Application.persistentDataPath, SESSION_FILE);
                File.WriteAllText(sessionPath, sessionId);
                Logger.LogInfo($"Saved session ID: {sessionId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save session ID: {ex.Message}");
            }
        }

        private void TryConnect()
        {
            if (connected)
            {
                Logger.LogWarning("[AtlyssAP] Already connected.");
                return;
            }
            if (connecting)
            {
                Logger.LogWarning("[AtlyssAP] Connection in progress...");
                return;
            }
            connecting = true;
            try
            {
                Logger.LogInfo("[AtlyssAP] === CONNECTING TO ARCHIPELAGO ===");
                Logger.LogInfo($"[AtlyssAP] Server: {apServer.text}");
                Logger.LogInfo($"[AtlyssAP] Slot: {apSlot.text}");
                _session = ArchipelagoSessionFactory.CreateSession(apServer.text);
                _dlService = _session.CreateDeathLinkService();
                Logger.LogInfo("[AtlyssAP] Session and DeathLink service created");
                string password = string.IsNullOrWhiteSpace(apPassword.text) ? null : apPassword.text;
                LoginResult login = _session.TryConnectAndLogin(
                    "ATLYSS",
                    apSlot.text,
                    ItemsHandlingFlags.AllItems,
                    tags: apDeathlink ? new[] { "DeathLink" } : Array.Empty<string>(),
                    password: password,
                    requestSlotData: true
                );
                if (!login.Successful)
                {
                    LoginFailure failure = login as LoginFailure;
                    if (failure != null && failure.Errors != null && failure.Errors.Length > 0)
                    {
                        Logger.LogError($"[AtlyssAP] Login failed: {string.Join(", ", failure.Errors)}");
                    }
                    else
                    {
                        Logger.LogError("[AtlyssAP] Login failed.");
                    }
                    Disconnect();
                    connecting = false;
                    return;
                }
                Logger.LogInfo("[AtlyssAP] Login successful!");
                cfgServer.Value = apServer.text; // update the config with the new values the player entered (in case they are different)
                cfgSlot.Value = apSlot.text;
                cfgPassword.Value = apPassword.text;

                try
                {
                    LoginSuccessful loginSuccess = login as LoginSuccessful;
                    if (loginSuccess != null && loginSuccess.SlotData != null)
                    {
                        slotData = loginSuccess.SlotData;
                        if (slotData.ContainsKey("goal"))
                        {
                            goalOption = Convert.ToInt32(slotData["goal"]);
                            string[] goalNames = { "Slime Diva", "Lord Zuulneruda", "Colossus", "Galius", "Lord Kaluuz", "Valdur", "All Bosses", "All Quests", "Level 32" };
                            Logger.LogInfo($"[AtlyssAP] Goal: {goalNames[goalOption]}");
                        }
                        if (slotData.ContainsKey("area_access"))
                        {
                            areaAccessOption = Convert.ToInt32(slotData["area_access"]);
                            string[] areaNames = { "Locked", "Unlocked", "Progressive" };
                            Logger.LogInfo($"[AtlyssAP] Area Access: {areaNames[areaAccessOption]}");
                        }
                        if (slotData.ContainsKey("shop_sanity"))
                        {
                            shopSanityEnabled = Convert.ToInt32(slotData["shop_sanity"]) == 1;
                            Logger.LogInfo($"[AtlyssAP] Shop Sanity: {(shopSanityEnabled ? "Enabled" : "Disabled")}");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("[AtlyssAP] Login successful but slot data is null - using default options");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AtlyssAP] Could not read slot data: {ex.Message}");
                }

                SendAPChatMessage("<color=yellow>Connected to multiworld!</color>");

                string[] goalMessages = {
                    "Your goal: <color=red>Defeat Slime Diva</color>",
                    "Your goal: <color=red>Defeat Lord Zuulneruda</color>",
                    "Your goal: <color=red>Defeat Colossus</color>",
                    "Your goal: <color=red>Defeat Galius</color>",
                    "Your goal: <color=red>Defeat Lord Kaluuz</color>",
                    "Your goal: <color=red>Defeat Valdur</color>",
                    "Your goal: <color=red>Defeat All Bosses</color>",
                    "Your goal: <color=yellow>Complete All Quests</color>",
                    "Your goal: <color=#00FFFF>Reach Level 32</color>"
                };
                if (goalOption >= 0 && goalOption < goalMessages.Length)
                {
                    SendAPChatMessage(goalMessages[goalOption]);
                }

                portalLocker.ApplyAreaAccessMode();

                // NEW: Send location scouts if shop sanity is enabled (scouts all 50 locations)
                if (shopSanityEnabled)
                {
                    _shopSanity.SendLocationScouts(_session);
                }

                string newSessionId = $"{apServer.text}_{apSlot.text}_{_session.RoomState.Seed}";
                if (IsNewSession(newSessionId))
                {
                    Logger.LogInfo("[AtlyssAP] New AP session detected - clearing storage");
                    ArchipelagoSpikeStorage.ClearAllAPBanks();
                    SaveSessionId(newSessionId);
                }
                currentSessionId = newSessionId;

                SpikePatch.InitializeAPStorage();

                try
                {
                    _session.Socket.SendPacket(new GetDataPackagePacket { Games = new[] { "ATLYSS" } });
                    Logger.LogInfo("[AtlyssAP] Data package requested.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AtlyssAP] Data package warning: {ex.Message}");
                }
                try
                {
                    _session.Socket.SendPacket(new SyncPacket());
                    Logger.LogInfo("[AtlyssAP] Sync packet sent.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AtlyssAP] Sync packet warning: {ex.Message}");
                }
                _session.Items.ItemReceived += OnItemReceived;
                _session.Socket.PacketReceived += OnPacketReceived;
                _dlService.OnDeathLinkReceived += OnDeathLinkReceived;
                _session.Locations.CheckedLocationsUpdated += (locations) =>
                {
                    Logger.LogInfo($"[AtlyssAP] Server confirmed {locations.Count} checked location(s)");
                };

                Player localPlayer = Player._mainPlayer;
                if (localPlayer != null)
                {
                    PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        _lastLevel = stats.Network_currentLevel;
                        Logger.LogInfo($"[AtlyssAP] Starting at level {_lastLevel}");

                        // NEW: Reset tracked profession levels on connection
                        _previousFishingLevel = 1;
                        _previousMiningLevel = 1;
                    }
                }
                connected = true;
                connecting = false;
                Logger.LogInfo("=== [AtlyssAP] Connected and ready! ===");
                Logger.LogInfo("[AtlyssAP] Automatic detection active - level-ups and quests will be tracked!");
                Logger.LogInfo("[AtlyssAP] Items will be sent to Spike storage!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Connection failed: {ex.Message}");
                Logger.LogError($"[AtlyssAP] Stack: {ex.StackTrace}");
                Disconnect();
                connecting = false;
                connected = false;
            }
        }

        private void Disconnect()
        {
            try
            {
                if (_session != null)
                {
                    try { _session.Items.ItemReceived -= OnItemReceived; } catch { }
                    try
                    {
                        if (_session.Socket != null)
                            _session.Socket.DisconnectAsync();
                    }
                    catch { }
                }
            }
            finally
            {
                _session = null;
                connected = false;
                connecting = false;
                _reportedChecks.Clear();
                _completedQuests.Clear();
                _lastLevel = 0;
                // NEW: Reset profession level tracking on disconnect
                _previousFishingLevel = 1;
                _previousMiningLevel = 1;
                _questDebugLogged = false;

                // UPDATED: Reset all 11 portal tracking states
                foreach (var key in _portalItemsReceived.Keys.ToList())
                {
                    _portalItemsReceived[key] = false;
                }

                // NEW: Reset shop sanity state
                _shopSanity.Reset();

                // ADDED: Drain any remaining queued items on disconnect
                while (_receivedItemQueue.TryDequeue(out _)) { }

                Logger.LogInfo("[AtlyssAP] Disconnected.");
            }
        }

        private void SendCheckById(long locationId)
        {
            if (!connected || _session == null)
            {
                Logger.LogError("[AtlyssAP] Not connected; cannot send check.");
                return;
            }
            if (_reportedChecks.Contains(locationId))
            {
                return;
            }
            try
            {
                if (_session != null && _session.Socket != null && _session.Socket.Connected)
                {
                    _session.Locations.CompleteLocationChecks(locationId);
                    _reportedChecks.Add(locationId);
                    Logger.LogInfo($"[AtlyssAP] Sent check ID: {locationId}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to send check: {ex.Message}");
            }
        }

        private void OnDeathLinkReceived(DeathLink dl)
        {
            reactingToDeathLink = 2;
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogWarning("[AtlyssAP] Player not found for DeathLink! Possibly on Main Menu?");
                    reactingToDeathLink = 0;
                    return;
                }
                localPlayer._playerZoneType = ZoneType.Field; // if this is set to `Safe`, as it is in Sanctum for example, the player cannot die from anything.
                localPlayer._statusEntity.Subtract_Health(10000);
                Logger.LogMessage("[AtlyssAP] DeathLink Received!");
                string DeathLinkMessage;
                if (dl.Cause.IsNullOrWhiteSpace())
                {
                    DeathLinkMessage = $"Killed by {dl.Source} on Archipelago.";
                }
                else
                {
                    DeathLinkMessage = $"{dl.Cause}.";
                }
                try
                {
                    GameObject.Find("_GameUI_InGame").GetComponent<ErrorPromptTextManager>().Init_ErrorPrompt(DeathLinkMessage); // this is that large red text in the top middle of the screen
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[AtlyssAP] Failed to display DeathLink message: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to process DeathLink: {ex.Message}");
                reactingToDeathLink = 0;
            }
        }

        private void OnItemReceived(ReceivedItemsHelper helper)
        {
            try
            {
                ItemInfo item = helper.DequeueItem();
                string itemName = helper.GetItemName(item.ItemId, item.ItemGame) ?? $"Item {item.ItemId}";
                string fromPlayerName = _session.Players.GetPlayerName(item.Player) ?? $"Player {item.Player}";
                Logger.LogInfo($"[AtlyssAP] Received: {itemName} from {fromPlayerName}");

                // CHANGED: Queue the item for main-thread processing instead of handling here.
                // This callback fires from the Archipelago network thread. Directly modifying
                // Unity objects (Spike storage, vendor inventories, player inventory) from this
                // thread causes InvalidOperationException: "Operations that change non-concurrent
                // collections must have exclusive access." The queue is processed in Update().
                _receivedItemQueue.Enqueue((itemName, fromPlayerName));
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error receiving item: {ex.Message}");
            }
        }

        private void HandleReceivedItem(string itemName)
        {
            try
            {
                // UPDATED: Check if it's any of the 11 portal items
                if (_portalItemsReceived.ContainsKey(itemName))
                {
                    _portalItemsReceived[itemName] = true;
                    Logger.LogInfo($"[AtlyssAP] Received {itemName}!");

                    if (!_portalScenes.ContainsKey(itemName))
                    {
                        Logger.LogError($"[AtlyssAP] Unknown portal item: {itemName}");
                        return;
                    }

                    string sceneName = _portalScenes[itemName];

                    if (areaAccessOption == 0) // Locked mode
                    {
                        portalLocker.UnblockAccessToScene(sceneName);
                        SendAPChatMessage($"<color=#00FFFF>{itemName.Replace(" Portal", "")} unlocked!</color>");
                    }
                    else if (areaAccessOption == 2) // Progressive mode
                    {
                        CheckProgressiveUnlocks(); // NEW: Check progressive unlock chain
                    }
                    return;
                }

                // Currency handling
                if (itemName.StartsWith("Crowns ("))  // FIXED: was EndsWith(" Crowns") - now properly catches "Crowns (Small)", "Crowns (Medium)", etc.
                {
                    int amount = GetCurrencyAmount(itemName);
                    if (amount > 0)
                    {
                        GiveCurrency(amount);
                        SendAPChatMessage($"<color=yellow>Received {amount} Crowns!</color>");
                        Logger.LogInfo($"[AtlyssAP] Gave {amount} crowns to player");
                    }
                    return;
                }

                if (ItemNameMapping.TryGetValue(itemName, out string gameItemName))
                {
                    int quantity = DetermineItemQuantity(itemName);

                    try
                    {
                        ItemData itemData = CreateItemData(gameItemName, quantity);

                        if (itemData != null)
                        {
                            if (ArchipelagoSpikeStorage.AddItemToAPSpike(itemData))
                            {
                                SendAPChatMessage($"<color=yellow>Received {itemName}!</color> Check Spike's storage!");
                                Logger.LogInfo($"[AtlyssAP] Added {itemName} to Spike storage");
                            }
                            else
                            {
                                Logger.LogWarning($"[AtlyssAP] Failed to add {itemName} to storage - banks full!");
                                SendAPChatMessage($"<color=red>Storage full! Could not store {itemName}</color>");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[AtlyssAP] Failed to process item {itemName}: {ex.Message}");
                    }
                }
                else
                {
                    Logger.LogWarning($"[AtlyssAP] Unknown item: {itemName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error handling item '{itemName}': {ex.Message}");
            }
        }

        // NEW: Check progressive unlock chain (called when receiving any portal in progressive mode)
        private void CheckProgressiveUnlocks()
        {
            for (int i = 0; i < _progressivePortalOrder.Count; i++)
            {
                string portalName = _progressivePortalOrder[i];
                string sceneName = _portalScenes[portalName];

                // Check if all previous portals are received
                bool canUnlock = true;
                for (int j = 0; j <= i; j++)
                {
                    if (!_portalItemsReceived[_progressivePortalOrder[j]])
                    {
                        canUnlock = false;
                        break;
                    }
                }

                // Unlock if we have all required portals and scene is still locked
                if (canUnlock && portalLocker.IsSceneLocked(sceneName))
                {
                    portalLocker.UnblockAccessToScene(sceneName);
                    SendAPChatMessage($"<color=#00FFFF>{portalName.Replace(" Portal", "")} unlocked!</color>");
                }
            }
        }

        private ItemData CreateItemData(string gameItemName, int quantity)
        {
            string itemName = gameItemName;

            int lastUnderscoreIndex = gameItemName.LastIndexOf('_');
            if (lastUnderscoreIndex >= 0 && lastUnderscoreIndex < gameItemName.Length - 1)
            {
                itemName = gameItemName.Substring(lastUnderscoreIndex + 1);
            }

            Logger.LogInfo($"[AtlyssAP] DEBUG: Looking up item: '{itemName}'");

            ScriptableItem scriptableItem = null;
            List<string> attemptedNames = new List<string>();

            scriptableItem = GameManager._current.Locate_Item(itemName);
            attemptedNames.Add($"1. '{itemName}'");

            if (scriptableItem == null && itemName.Contains(" (") && itemName.EndsWith(")"))
            {
                int parenIndex = itemName.LastIndexOf(" (");
                string baseItemName = itemName.Substring(0, parenIndex);
                Logger.LogInfo($"[AtlyssAP] DEBUG: Attempt 2 - base name: '{baseItemName}'");
                scriptableItem = GameManager._current.Locate_Item(baseItemName);
                attemptedNames.Add($"2. '{baseItemName}'");
            }

            if (scriptableItem == null && itemName != gameItemName)
            {
                Logger.LogInfo($"[AtlyssAP] DEBUG: Attempt 3 - full Unity name: '{gameItemName}'");
                scriptableItem = GameManager._current.Locate_Item(gameItemName);
                attemptedNames.Add($"3. '{gameItemName}'");
            }

            if (scriptableItem == null)
            {
                string noSpaces = itemName.Replace(" ", "");
                Logger.LogInfo($"[AtlyssAP] DEBUG: Attempt 4 - no spaces: '{noSpaces}'");
                scriptableItem = GameManager._current.Locate_Item(noSpaces);
                attemptedNames.Add($"4. '{noSpaces}'");
            }

            if (scriptableItem == null && gameItemName.Contains("WEAPON_"))
            {
                string afterWeapon = gameItemName.Substring(gameItemName.IndexOf("WEAPON_") + 7);
                if (afterWeapon.Contains(" ("))
                {
                    string weaponOnly = afterWeapon.Substring(0, afterWeapon.IndexOf(" ("));
                    Logger.LogInfo($"[AtlyssAP] DEBUG: Attempt 5 - weapon name only: '{weaponOnly}'");
                    scriptableItem = GameManager._current.Locate_Item(weaponOnly);
                    attemptedNames.Add($"5. '{weaponOnly}'");
                }
            }

            if (scriptableItem == null)
            {
                Logger.LogError($"[AtlyssAP] Could not find ScriptableItem for: '{itemName}'");
                Logger.LogError($"[AtlyssAP] Original Unity name: '{gameItemName}'");
                Logger.LogError($"[AtlyssAP] Tried {attemptedNames.Count} variations:");
                foreach (string attempt in attemptedNames)
                {
                    Logger.LogError($"[AtlyssAP]   - {attempt}");
                }
                Logger.LogWarning($"[AtlyssAP] This item may not be available in the current game version or uses a different name format");
                return null;
            }

            Logger.LogInfo($"[AtlyssAP] DEBUG: Found ScriptableItem: {scriptableItem._itemName}");

            ItemData itemData = new ItemData
            {
                _itemName = scriptableItem._itemName,
                _quantity = quantity,
                _maxQuantity = 99,
                _modifierID = 0,
                _isEquipped = false,
                _slotNumber = 0
            };

            Logger.LogInfo($"[AtlyssAP] Successfully created ItemData for: {scriptableItem._itemName}");
            return itemData;
        }

        private int GetCurrencyAmount(string itemName)
        {
            if (itemName == "Crowns (Small)") return 100;
            if (itemName == "Crowns (Medium)") return 500;
            if (itemName == "Crowns (Large)") return 2000;
            if (itemName == "Crowns (Huge)") return 5000;
            return 100;
        }

        private int DetermineItemQuantity(string itemName)
        {
            if (itemName.EndsWith("Pack"))
            {
                if (itemName.Contains("Badge") || itemName.Contains("Cluster") ||
                    itemName.Contains("Ingot") || itemName.Contains("Bond"))
                {
                    return 3;
                }
                return 5;
            }
            return 1;
        }

        private void GiveCurrency(int amount)
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogError("[AtlyssAP] Player not found!");
                    return;
                }
                PlayerInventory inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory == null)
                {
                    Logger.LogError("[AtlyssAP] PlayerInventory not found!");
                    return;
                }
                inventory.Network_heldCurrency += amount;

                SendAPChatMessage(
                    $"<color=yellow>+{amount} Crowns</color> added to wallet!"
                );
                Logger.LogInfo($"[AtlyssAP] Gave {amount} Crowns! New total: {inventory.Network_heldCurrency}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to give currency: {ex.Message}");
            }
        }

        public void SendAPChatMessage(string message)
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                ChatBehaviour chat = localPlayer._chatBehaviour;
                maxOnscreenMessages.SetValue(chat, 50);
                if (chat == null) return;

                chat.Init_GameLogicMessage(
                    $"<color=#00ff00>[Archipelago]</color> {message}"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to send chat message: {ex.Message}");
            }
        }

        public void HandleArchipelagoCommand(string message)
        {
            if (!connected || _session == null)
            {
                SendAPChatMessage("<color=red>Not connected to Archipelago!</color>");
                return;
            }

            string command = message.TrimStart('/').Trim();
            string[] parts = command.Split(new[] { ' ' }, 2);
            string cmd = parts[0].ToLower();
            string args = parts.Length > 1 ? parts[1] : "";
            Logger.LogInfo($"[AtlyssAP] Command received: {cmd} {args}");
            switch (cmd)
            {
                case "release":
                    HandleReleaseCommand();
                    break;
                case "collect":
                    HandleCollectCommand();
                    break;
                case "hint":
                    HandleHintCommand(args);
                    break;
                case "help":
                    HandleHelpCommand();
                    break;
                case "players":
                    HandlePlayersCommand();
                    break;
                case "status":
                    HandleStatusCommand();
                    break;
                default:
                    try
                    {
                        _session.Socket.SendPacket(new SayPacket { Text = message });
                        Logger.LogInfo($"[AtlyssAP] Sent command to server: {message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[AtlyssAP] Failed to send command: {ex.Message}");
                        SendAPChatMessage($"<color=red>Unknown command: /{cmd}</color>");
                    }
                    break;
            }
        }

        private void HandleReleaseCommand()
        {
            try
            {
                _session.Say("!release");
                SendAPChatMessage("<color=yellow>Release requested!</color>");
                Logger.LogInfo("[AtlyssAP] Release command executed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to execute release: {ex.Message}");
                SendAPChatMessage("<color=red>Failed to execute release</color>");
            }
        }

        private void HandleCollectCommand()
        {
            try
            {
                _session.Say("!collect");
                SendAPChatMessage("<color=yellow>Collect requested!</color>");
                Logger.LogInfo("[AtlyssAP] Collect command executed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to execute collect: {ex.Message}");
                SendAPChatMessage("<color=red>Failed to execute collect</color>");
            }
        }

        private void HandleHintCommand(string args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    SendAPChatMessage("<color=yellow>Usage: /hint [item name]</color>");
                    return;
                }

                SendAPChatMessage($"<color=yellow>Requesting hint for: {args}</color>");
                _session.Say($"!hint {args}");
                Logger.LogInfo($"[AtlyssAP] Hint requested for: {args}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to request hint: {ex.Message}");
            }
        }

        private void HandleHelpCommand()
        {
            SendAPChatMessage("<color=yellow>Archipelago Commands:</color>");
            SendAPChatMessage("/release - Release remaining items");
            SendAPChatMessage("/collect - Collect items from others");
            SendAPChatMessage("/hint [item] - Request hint");
            SendAPChatMessage("/players - List connected players");
            SendAPChatMessage("/status - Show completion status");
            SendAPChatMessage("/help - Show this message");
        }

        private void HandlePlayersCommand()
        {
            try
            {
                var players = _session.Players.AllPlayers;
                int playerCount = 0;
                foreach (var p in players)
                {
                    playerCount++;
                }
                SendAPChatMessage($"<color=yellow>Connected Players ({playerCount}):</color>");
                foreach (var player in players)
                {
                    string name = player.Name ?? $"Player {player.Slot}";
                    string game = player.Game ?? "Unknown";
                    SendAPChatMessage($"- {name} ({game})");
                }
                Logger.LogInfo($"[AtlyssAP] Listed {playerCount} players");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to list players: {ex.Message}");
                SendAPChatMessage("<color=red>Failed to get player list</color>");
            }
        }

        private void HandleStatusCommand()
        {
            try
            {
                int checkedCount = _reportedChecks.Count;
                int totalLevelMilestones = 16;
                int totalQuests = AllQuestToLocation.Count;
                int totalCount = totalLevelMilestones + totalQuests;
                float percentage = (float)checkedCount / totalCount * 100f;
                SendAPChatMessage($"<color=yellow>Progress: {checkedCount}/{totalCount} ({percentage:F1}%)</color>");
                SendAPChatMessage($"Level milestones: {_lastLevel}/32");
                SendAPChatMessage($"Quest completions: {_completedQuests.Count}/{totalQuests}");
                Logger.LogInfo($"[AtlyssAP] Status: {checkedCount}/{totalCount} locations ({totalQuests} quests, {totalLevelMilestones} levels)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to show status: {ex.Message}");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            if (packet is PrintJsonPacket && !(packet is ItemPrintJsonPacket) && !(packet is HintPrintJsonPacket))
            {
                SendAPChatMessage($"{packet.ToJObject().SelectToken("data[0].text")?.Value<string>()}");
            }
            else if (packet is HintPrintJsonPacket)
            {
                string combinedMessage;
                if (packet.ToJObject()["receiving"].ToObject<int>() == _session.Players.ActivePlayer.Slot) // if the receiving player is us...
                {
                    var NetItem = packet.ToJObject()["item"].ToObject<NetworkItem>(); // Get the item data from the hint
                    var playerSending = _session.Players.GetPlayerInfo(NetItem.Player).Alias; // Find the finding player's slot alias
                    var itemReceiving = _session.Items.GetItemName(NetItem.Item); // Get the item's name
                    var findingLocation = _session.Locations.GetLocationNameFromId(NetItem.Location, _session.Players.GetPlayerInfo(NetItem.Player).Game); // Get the location's name
                    combinedMessage = $"Your <color=yellow>{itemReceiving}</color> is at <color=yellow>{findingLocation}</color> in <color=#00FFFF>{playerSending}'s</color> world.";
                    SendAPChatMessage(combinedMessage);
                }
                else if (packet.ToJObject()["item"].ToObject<NetworkItem>().Player == _session.Players.ActivePlayer.Slot) // if the sending player is us...
                {
                    var NetItem = packet.ToJObject()["item"].ToObject<NetworkItem>(); // Get the item data
                    var playerReceiving = _session.Players.GetPlayerInfo(NetItem.Player).Alias; // Find the receiving player's slot alias
                    var itemSending = _session.Items.GetItemName(NetItem.Item, _session.Players.GetPlayerInfo(NetItem.Player).Game); // Get the item's name
                    var findingLocation = _session.Locations.GetLocationNameFromId(NetItem.Location); // Get the location's name
                    combinedMessage = $"<color=#00FFFF>{playerReceiving}'s</color> <color=yellow>{itemSending}</color> is at your <color=yellow>{findingLocation}</color>";
                    SendAPChatMessage(combinedMessage);
                }
                else // ap should prevent this, but it's better to be prepared
                {
                    Logger.LogWarning($"[AtlyssAP] Received a hint for a different player. Ignoring.");
                }
            }
        }

        public void ToggleDeathLink(bool enabled)
        {
            if (_session == null || _dlService == null || !_session.Socket.Connected)
            {
                return;
            }
            if (enabled)
            {
                _dlService.EnableDeathLink();
            }
            else
            {
                _dlService.DisableDeathLink();
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