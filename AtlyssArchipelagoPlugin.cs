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
            { "Spiraling In The Grove", BASE_LOCATION_ID + 201 },  // ADDED: was missing entirely
            { "Hell In The Grove", BASE_LOCATION_ID + 202 },  // RENUMBERED: was 201
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

            // Weapons - Melee (One-Handed Sword/Mace, Strength)
            { "Crypt Blade", "(lv-2) WEAPON_Crypt Blade (Sword, Strength)" },
            { "Femur Club", "(lv-2) WEAPON_Femur Club (Sword, Strength)" },
            { "Ironbark Sword", "(lv-2) WEAPON_Ironbark Sword (Sword, Strength)" },
            { "Slimecrust Blade", "(lv-2) WEAPON_Slimecrust Blade (Sword, Strength)" },
            { "Gilded Sword", "(lv-4) WEAPON_Gilded Sword (Sword, Strength)" },
            { "Splitbark Club", "(lv-4) WEAPON_Splitbark Club (Sword, Strength)" },
            { "Demicrypt Blade", "(lv-6) WEAPON_Demicrypt Blade (Sword, Strength)" },
            { "Dense Mace", "(lv-6) WEAPON_Dense Mace (Sword, Strength)" },
            { "Iron Sword", "(lv-6) WEAPON_Iron Sword (Sword, Strength)" },
            { "Dawn Mace", "(lv-8) WEAPON_Dawn Mace (Sword, Strength)" },
            { "Rude Blade", "(lv-8) WEAPON_Rude Blade (Sword, Strength)" },
            { "Vile Blade", "(lv-8) WEAPON_Vile Blade (Sword, Strength)" },
            { "Amberite Sword", "(lv-12) WEAPON_Amberite Sword (Sword, Strength)" },
            { "Nethercrypt Blade", "(lv-12) WEAPON_Nethercrypt Blade (Sword, Strength)" },
            { "Coldgeist Blade", "(lv-16) WEAPON_Coldgeist Blade (Sword, Strength)" },
            { "Mithril Sword", "(lv-16) WEAPON_Mithril Sword (Sword, Strength)" },
            { "Serrated Blade", "(lv-16) WEAPON_Serrated Blade (Sword, Strength)" },
            { "Nulrok Mace", "(lv-20) WEAPON_Nulrok Mace (Sword, Strength)" },
            { "Firebreath Blade", "(lv-22) WEAPON_Firebreath Blade (Sword, Strength)" },
            { "Valdur Blade", "(lv-24) WEAPON_Valdur Blade (Sword, Strength)" },
            { "Fier Blade", "(lv-26) WEAPON_Fier Blade (Sword, Strength)" },

            // Weapons - Hammers (Two-Handed Heavy Melee, Strength)
            { "Slimek Axehammer", "(lv-4) WEAPON_Slimek Axehammer (Hammer, Strength)" },
            { "Dense Hammer", "(lv-6) WEAPON_Dense Hammer (Hammer, Strength)" },
            { "Iron Axehammer", "(lv-6) WEAPON_Iron Axehammer (Hammer, Strength)" },
            { "Crypt Pounder", "(lv-8) WEAPON_Crypt Pounder (Hammer, Strength)" },
            { "Quake Pummeler", "(lv-18) WEAPON_Quake Pummeler (Hammer, Strength)" },

            // Weapons - Greatblades (Two-Handed Heavy Melee, Strength)
            { "Mini Geist Scythe", "(lv-4) WEAPON_Mini Geist Scythe (Greatblade, Strength)" },
            { "Geist Scythe", "(lv-6) WEAPON_Geist Scythe (Greatblade, Strength)" },
            { "Stone Greatblade", "(lv-8) WEAPON_Stone Greatblade (Greatblade, Strength)" },
            { "Amberite Warstar", "(lv-12) WEAPON_Amberite Warstar (Greatblade, Strength)" },
            { "Dolkin's Axe", "(lv-12) WEAPON_Dolkin's Axe (Greatblade, Strength)" },
            { "Poltergeist Scythe", "(lv-14) WEAPON_Poltergeist Scythe (Greatblade, Strength)" },
            { "Coldgeist Punisher", "(lv-16) WEAPON_Coldgeist Punisher (Greatblade, Strength)" },
            { "Deadwood Axe", "(lv-16) WEAPON_Deadwood Axe (Greatblade, Strength)" },
            { "Mithril Greatsword", "(lv-16) WEAPON_Mithril Greatsword (Greatblade, Strength)" },
            { "Deathknight Runeblade", "(lv-22) WEAPON_Deathknight Runeblade (Greatblade, Strength)" },
            { "Ryzer Greataxe", "(lv-26) WEAPON_Ryzer Greataxe (Greatblade, Strength)" },

            // Weapons - Polearms (Two-Handed, Strength)
            { "Dense Spear", "(lv-6) WEAPON_Dense Spear (Polearm, Strength)" },
            { "Iron Spear", "(lv-6) WEAPON_Iron Spear (Polearm, Strength)" },
            { "Cryptsinge Halberd", "(lv-8) WEAPON_Cryptsinge Halberd (Polearm, Strength)" },
            { "Mekspear", "(lv-8) WEAPON_Mekspear (Polearm, Strength)" },
            { "Amberite Halberd", "(lv-12) WEAPON_Amberite Halberd (Polearm, Strength)" },
            { "Necroroyal Halberd", "(lv-12) WEAPON_Necroroyal Halberd (Polearm, Strength)" },
            { "Sinner Bardiche", "(lv-12) WEAPON_Sinner Bardiche (Polearm, Strength)" },
            { "Mithril Halberd", "(lv-16) WEAPON_Mithril Halberd (Polearm, Strength)" },
            { "Ragespear", "(lv-16) WEAPON_Ragespear (Polearm, Strength)" },
            { "Serrated Spear", "(lv-16) WEAPON_Serrated Spear (Polearm, Strength)" },
            { "Sapphite Spear", "(lv-18) WEAPON_Sapphite Spear (Polearm, Strength)" },
            { "Nulrok Spear", "(lv-20) WEAPON_Nulrok Spear (Polearm, Strength)" },
            { "Cryotribe Spear", "(lv-22) WEAPON_Cryotribe Spear (Polearm, Strength)" },
            { "Flametribe Spear", "(lv-22) WEAPON_Flametribe Spear (Polearm, Strength)" },

            // Weapons - Scepters (One-Handed, Mind)
            { "Marrow Bauble", "(lv-2) WEAPON_Marrow Bauble (Scepter, Mind)" },
            { "Splitbark Scepter", "(lv-2) WEAPON_Splitbark Scepter (Scepter, Mind)" },
            { "Demicrypt Bauble", "(lv-6) WEAPON_Demicrypt Bauble (Scepter, Mind)" },
            { "Iron Scepter", "(lv-6) WEAPON_Iron Scepter (Scepter, Mind)" },
            { "Cryo Cane", "(lv-8) WEAPON_Cryo Cane (Scepter, Mind)" },
            { "Slime Diva Baton", "(lv-8) WEAPON_Slime Diva Baton (Scepter, Mind)" },
            { "Pyre Cane", "(lv-12) WEAPON_Pyre Cane (Scepter, Mind)" },
            { "Wizwand", "(lv-12) WEAPON_Wizwand (Scepter, Mind)" },
            { "Nethercrypt Bauble", "(lv-14) WEAPON_Nethercrypt Bauble (Scepter, Mind)" },
            { "Aquapetal Staff", "(lv-16) WEAPON_Aquapetal Staff (Scepter, Mind)" },
            { "Flamepetal Staff", "(lv-16) WEAPON_Flamepetal Staff (Scepter, Mind)" },
            { "Mithril Scepter", "(lv-16) WEAPON_Mithril Scepter (Scepter, Mind)" },
            { "Sapphite Scepter", "(lv-18) WEAPON_Sapphite Scepter (Scepter, Mind)" },
            { "Voalstark Wand", "(lv-24) WEAPON_Voalstark Wand (Scepter, Mind)" },

            // Weapons - Bells (Two-Handed, Mind)
            { "Cryptcall Bell", "(lv-8) WEAPON_Cryptcall Bell (Magic Bell, Mind)" },
            { "Iron Bell", "(lv-8) WEAPON_Iron Bell (Magic Bell, Mind)" },
            { "Coldgeist Frostcaller", "(lv-16) WEAPON_Coldgeist Frostcaller (Magic Bell, Mind)" },
            { "Mithril Bell", "(lv-16) WEAPON_Mithril Bell (Magic Bell, Mind)" },
            { "Colossus Tone", "(lv-18) WEAPON_Colossus Tone (Magic Bell, Mind)" },
            { "Sapphite Bell", "(lv-18) WEAPON_Sapphite Bell (Magic Bell, Mind)" },

            // Weapons - Katars (Two-Handed, Dexterity)
            { "Slimecrust Katars", "(lv-2) WEAPON_Slimecrust Katars (Katars, Dexterity)" },
            { "Cryptsinge Katars", "(lv-4) WEAPON_Cryptsinge Katars (Katars, Dexterity)" },
            { "Slimek Shivs", "(lv-4) WEAPON_Slimek Shivs (Katars, Dexterity)" },
            { "Deathgel Shivs", "(lv-6) WEAPON_Deathgel Shivs (Katars, Dexterity)" },
            { "Dense Katars", "(lv-6) WEAPON_Dense Katars (Katars, Dexterity)" },
            { "Iron Katars", "(lv-8) WEAPON_Iron Katars (Katars, Dexterity)" },
            { "Runic Katars", "(lv-10) WEAPON_Runic Katars (Katars, Dexterity)" },
            { "Geistlord Claws", "(lv-12) WEAPON_Geistlord Claws (Katars, Dexterity)" },
            { "Hellsludge Shivs", "(lv-14) WEAPON_Hellsludge Shivs (Katars, Dexterity)" },
            { "Mithril Katars", "(lv-14) WEAPON_Mithril Katars (Katars, Dexterity)" },
            { "Frostbite Claws", "(lv-16) WEAPON_Frostbite Claws (Katars, Dexterity)" },
            { "Serrated Knuckles", "(lv-16) WEAPON_Serrated Knuckles (Katars, Dexterity)" },
            { "Rummok Bladerings", "(lv-18) WEAPON_Rummok Bladerings (Katars, Dexterity)" },
            { "Sapphite Katars", "(lv-18) WEAPON_Sapphite Katars (Katars, Dexterity)" },
            { "Golemfist Katars", "(lv-20) WEAPON_Golemfist Katars (Katars, Dexterity)" },

            // Weapons - Bows (Two-Handed Ranged, Dexterity)
            { "Crypt Bow", "(lv-2) WEAPON_Crypt Bow (Bow, Dexterity)" },
            { "Demicrypt Bow", "(lv-6) WEAPON_Demicrypt Bow (Bow, Dexterity)" },
            { "Iron Bow", "(lv-6) WEAPON_Iron Bow (Bow, Dexterity)" },
            { "Mekspike Bow", "(lv-8) WEAPON_Mekspike Bow (Bow, Dexterity)" },
            { "Menace Bow", "(lv-8) WEAPON_Menace Bow (Bow, Dexterity)" },
            { "Petrified Bow", "(lv-12) WEAPON_Petrified Bow (Bow, Dexterity)" },
            { "Mithril Bow", "(lv-14) WEAPON_Mithril Bow (Bow, Dexterity)" },
            { "Necroroyal Bow", "(lv-14) WEAPON_Necroroyal Bow (Bow, Dexterity)" },
            { "Coldgeist Bow", "(lv-16) WEAPON_Coldgeist Bow (Bow, Dexterity)" },
            { "Serrated Longbow", "(lv-16) WEAPON_Serrated Longbow (Bow, Dexterity)" },
            { "Torrentius Longbow", "(lv-24) WEAPON_Torrentius Longbow (Bow, Dexterity)" },

            // Weapons - Shotguns (Two-Handed Ranged, Dexterity)
            { "Amberite Boomstick", "(lv-12) WEAPON_Amberite Boomstick (Shotgun, Dexterity)" },
            { "Magitek Burstgun", "(lv-20) WEAPON_Magitek Burstgun (Shotgun, Dexterity)" },
            { "Follycannon", "(lv-26) WEAPON_Follycannon (Shotgun, Dexterity)" },

            // Armor - Helms
            { "Agility Ears", "(lv-1) HELM_Agility Ears" },
            { "Festive Hat", "(lv-1) HELM_Festive Hat" },
            { "Fishin Hat", "(lv-1) HELM_Fishin Hat" },
            { "Leather Cap", "(lv-1) HELM_Leather Cap" },
            { "Newfold Halo", "(lv-1) HELM_Newfold Halo" },
            { "Orefinder Hat", "(lv-1) HELM_Orefinder Hat" },
            { "Spooky Hat", "(lv-1) HELM_Spooky Hat" },
            { "Top Hat", "(lv-1) HELM_Top Hat" },
            { "Wizard Hat", "(lv-1) HELM_Wizard Hat" },
            { "Acolyte Hood", "(lv-4) HELM_Acolyte Hood" },
            { "Cryptsinge Halo", "(lv-4) HELM_Cryptsinge Halo" },
            { "Initiate Spectacles", "(lv-4) HELM_Initiate Spectacles" },
            { "Demicrypt Halo", "(lv-6) HELM_Demicrypt Halo" },
            { "Dense Helm", "(lv-6) HELM_Dense Helm" },
            { "Diva Crown", "(lv-6) HELM_Diva Crown" },
            { "Iron Halo", "(lv-6) HELM_Iron Halo" },
            { "Necromancer Hood", "(lv-8) HELM_Necromancer Hood" },
            { "Geistlord Crown", "(lv-10) HELM_Geistlord Crown" },
            { "Journeyman Spectacles", "(lv-10) HELM_Journeyman Spectacles" },
            { "Amberite Helm", "(lv-12) HELM_Amberite Helm" },
            { "Focus Circlet", "(lv-12) HELM_Focus Circlet" },
            { "Magistrate Circlet", "(lv-12) HELM_Magistrate Circlet" },
            { "Rage Circlet", "(lv-12) HELM_Rage Circlet" },
            { "Focusi Glasses", "(lv-14) HELM_Focusi Glasses" },
            { "Nethercrypt Halo", "(lv-14) HELM_Nethercrypt Halo" },
            { "Carbuncle Hat", "(lv-16) HELM_Carbuncle Hat" },
            { "Geistlord Eye", "(lv-16) HELM_Geistlord Eye" },
            { "Glyphgrift Halo", "(lv-16) HELM_Glyphgrift Halo" },
            { "Jestercast Memory", "(lv-16) HELM_Jestercast Memory" },
            { "Knightguard Halo", "(lv-16) HELM_Knightguard Halo" },
            { "Mithril Halo", "(lv-16) HELM_Mithril Halo" },
            { "Sapphite Mindhat", "(lv-18) HELM_Sapphite Mindhat" },
            { "Dire Helm", "(lv-22) HELM_Dire Helm" },
            { "Druidic Halo", "(lv-22) HELM_Druidic Halo" },
            { "Guardel Helm", "(lv-22) HELM_Guardel Helm" },
            { "Leathen Cap", "(lv-22) HELM_Leathen Cap" },
            { "Boarus Helm", "(lv-24) HELM_Boarus Helm" },
            { "Deathknight Helm", "(lv-24) HELM_Deathknight Helm" },
            { "Emerock Halo", "(lv-24) HELM_Emerock Halo" },
            { "Wizlad Hood", "(lv-24) HELM_Wizlad Hood" },
            { "Boarus Torment", "(lv-26) HELM_Boarus Torment" },

            // Armor - Capes
            { "Initiate Cloak", "(lv-2) CAPE_Initiate Cloak" },
            { "Slimewoven Cloak", "(lv-4) CAPE_Slimewoven Cloak" },
            { "Nokket Cloak", "(lv-6) CAPE_Nokket Cloak" },
            { "Rugged Cloak", "(lv-6) CAPE_Rugged Cloak" },
            { "Regazuul Cape", "(lv-10) CAPE_Regazuul Cape" },
            { "Flux Cloak", "(lv-12) CAPE_Flux Cloak" },
            { "Cozy Cloak", "(lv-14) CAPE_Cozy Cloak" },
            { "Nethercrypt Cloak", "(lv-14) CAPE_Nethercrypt Cloak" },
            { "Cobblerage Cloak", "(lv-16) CAPE_Cobblerage Cloak" },
            { "Deathward Cape", "(lv-16) CAPE_Deathward Cape" },
            { "Forlorn Cloak", "(lv-16) CAPE_Forlorn Cloak" },
            { "Meshlink Cape", "(lv-16) CAPE_Meshlink Cape" },
            { "Sagecaller Cape", "(lv-16) CAPE_Sagecaller Cape" },
            { "Roudon Cape", "(lv-18) CAPE_Roudon Cape" },
            { "Blueversa Cape", "(lv-20) CAPE_Blueversa Cape" },
            { "Greenversa Cape", "(lv-20) CAPE_Greenversa Cape" },
            { "Nulversa Cape", "(lv-20) CAPE_Nulversa Cape" },
            { "Redversa Cape", "(lv-20) CAPE_Redversa Cape" },
            { "Windgolem Cloak", "(lv-22) CAPE_Windgolem Cloak" },
            { "Mekwar Drape", "(lv-24) CAPE_Mekwar Drape" },

            // Armor - Chestpieces
            { "Aero Top", "(lv-1) CHESTPIECE_Aero Top" },
            { "Bunhost Garb", "(lv-1) CHESTPIECE_Bunhost Garb" },
            { "Festive Coat", "(lv-1) CHESTPIECE_Festive Coat" },
            { "Fisher Overalls", "(lv-1) CHESTPIECE_Fisher Overalls" },
            { "Leather Top", "(lv-1) CHESTPIECE_Leather Top" },
            { "Necro Marrow", "(lv-1) CHESTPIECE_Necro Marrow" },
            { "Noble Shirt", "(lv-1) CHESTPIECE_Noble Shirt" },
            { "Nutso Top", "(lv-1) CHESTPIECE_Nutso Top" },
            { "Orefinder Vest", "(lv-1) CHESTPIECE_Orefinder Vest" },
            { "Ritualist Garb", "(lv-1) CHESTPIECE_Ritualist Garb" },
            { "Sagecloth Top", "(lv-1) CHESTPIECE_Sagecloth Top" },
            { "Silken Top", "(lv-1) CHESTPIECE_Silken Top" },
            { "Spooky Garment", "(lv-1) CHESTPIECE_Spooky Garment" },
            { "Vampiric Coat", "(lv-1) CHESTPIECE_Vampiric Coat" },
            { "Ghostly Tabard", "(lv-2) CHESTPIECE_Ghostly Tabard" },
            { "Poacher Cloth", "(lv-2) CHESTPIECE_Poacher Cloth" },
            { "Ragged Shirt", "(lv-2) CHESTPIECE_Ragged Shirt" },
            { "Slimecrust Chest", "(lv-2) CHESTPIECE_Slimecrust Chest" },
            { "Worn Robe", "(lv-2) CHESTPIECE_Worn Robe" },
            { "Cryptsinge Chest", "(lv-4) CHESTPIECE_Cryptsinge Chest" },
            { "Journeyman Vest", "(lv-4) CHESTPIECE_Journeyman Vest" },
            { "Slimek Chest", "(lv-4) CHESTPIECE_Slimek Chest" },
            { "Dense Chestpiece", "(lv-6) CHESTPIECE_Dense Chestpiece" },
            { "Trodd Tunic", "(lv-6) CHESTPIECE_Trodd Tunic" },
            { "Iron Chestpiece", "(lv-7) CHESTPIECE_Iron Chestpiece" },
            { "Tattered Battlerobe", "(lv-8) CHESTPIECE_Tattered Battlerobe" },
            { "Apprentice Robe", "(lv-10) CHESTPIECE_Apprentice Robe" },
            { "Duelist Garb", "(lv-10) CHESTPIECE_Duelist Garb" },
            { "Skywrill Tabard", "(lv-10) CHESTPIECE_Skywrill Tabard" },
            { "Sleeper's Robe", "(lv-10) CHESTPIECE_Sleeper's Robe" },
            { "Warrior Chest", "(lv-10) CHESTPIECE_Warrior Chest" },
            { "Amberite Breastplate", "(lv-12) CHESTPIECE_Amberite Breastplate" },
            { "Golem Chestpiece", "(lv-12) CHESTPIECE_Golem Chestpiece" },
            { "Lord Breastplate", "(lv-12) CHESTPIECE_Lord Breastplate" },
            { "Nethercrypt Tabard", "(lv-12) CHESTPIECE_Nethercrypt Tabard" },
            { "Reapsow Garb", "(lv-12) CHESTPIECE_Reapsow Garb" },
            { "Witchlock Robe", "(lv-12) CHESTPIECE_Witchlock Robe" },
            { "Chainmail Guard", "(lv-14) CHESTPIECE_Chainmail Guard" },
            { "Ornamented Battlerobe", "(lv-14) CHESTPIECE_Ornamented Battlerobe" },
            { "Carbuncle Robe", "(lv-16) CHESTPIECE_Carbuncle Robe" },
            { "Chainscale Chest", "(lv-16) CHESTPIECE_Chainscale Chest" },
            { "Gemveil Raiment", "(lv-16) CHESTPIECE_Gemveil Raiment" },
            { "King Breastplate", "(lv-16) CHESTPIECE_King Breastplate" },
            { "Mercenary Vestment", "(lv-16) CHESTPIECE_Mercenary Vestment" },
            { "Mithril Chestpiece", "(lv-16) CHESTPIECE_Mithril Chestpiece" },
            { "Reaper Gi", "(lv-16) CHESTPIECE_Reaper Gi" },
            { "Witchwizard Robe", "(lv-16) CHESTPIECE_Witchwizard Robe" },
            { "Berserker Chestpiece", "(lv-18) CHESTPIECE_Berserker Chestpiece" },
            { "Fuguefall Duster", "(lv-18) CHESTPIECE_Fuguefall Duster" },
            { "Magilord Overalls", "(lv-18) CHESTPIECE_Magilord Overalls" },
            { "Monolith Chestpiece", "(lv-18) CHESTPIECE_Monolith Chestpiece" },
            { "Sapphite Guard", "(lv-18) CHESTPIECE_Sapphite Guard" },
            { "Druidic Robe", "(lv-20) CHESTPIECE_Druidic Robe" },
            { "Emerock Chestpiece", "(lv-20) CHESTPIECE_Emerock Chestpiece" },
            { "Fortified Vestment", "(lv-20) CHESTPIECE_Fortified Vestment" },
            { "Roudon Chestpiece", "(lv-20) CHESTPIECE_Roudon Chestpiece" },
            { "Earthbind Tabard", "(lv-22) CHESTPIECE_Earthbind Tabard" },
            { "Gemveil Breastplate", "(lv-22) CHESTPIECE_Gemveil Breastplate" },
            { "Roudon Robe", "(lv-22) CHESTPIECE_Roudon Robe" },
            { "Ruggrok Vest", "(lv-22) CHESTPIECE_Ruggrok Vest" },
            { "Executioner Vestment", "(lv-24) CHESTPIECE_Executioner Vestment" },
            { "Fender Garb", "(lv-24) CHESTPIECE_Fender Garb" },
            { "Wizlad Robe", "(lv-24) CHESTPIECE_Wizlad Robe" },

            // Armor - Leggings
            { "Aero Pants", "(lv-1) LEGGINGS_Aero Pants" },
            { "Bunhost Leggings", "(lv-1) LEGGINGS_Bunhost Leggings" },
            { "Festive Trousers", "(lv-1) LEGGINGS_Festive Trousers" },
            { "Leather Britches", "(lv-1) LEGGINGS_Leather Britches" },
            { "Necro Caustics", "(lv-1) LEGGINGS_Necro Caustics" },
            { "Noble Pants", "(lv-1) LEGGINGS_Noble Pants" },
            { "Nutso Pants", "(lv-1) LEGGINGS_Nutso Pants" },
            { "Orefinder Trousers", "(lv-1) LEGGINGS_Orefinder Trousers" },
            { "Ritualist Straps", "(lv-1) LEGGINGS_Ritualist Straps" },
            { "Sagecloth Shorts", "(lv-1) LEGGINGS_Sagecloth Shorts" },
            { "Silken Loincloth", "(lv-1) LEGGINGS_Silken Loincloth" },
            { "Vampiric Leggings", "(lv-1) LEGGINGS_Vampiric Leggings" },
            { "Ghostly Legwraps", "(lv-2) LEGGINGS_Ghostly Legwraps" },
            { "Journeyman Shorts", "(lv-2) LEGGINGS_Journeyman Shorts" },
            { "Slimecrust Leggings", "(lv-2) LEGGINGS_Slimecrust Leggings" },
            { "Journeyman Leggings", "(lv-4) LEGGINGS_Journeyman Leggings" },
            { "Slimek Leggings", "(lv-4) LEGGINGS_Slimek Leggings" },
            { "Dense Leggings", "(lv-6) LEGGINGS_Dense Leggings" },
            { "Sash Leggings", "(lv-8) LEGGINGS_Sash Leggings" },
            { "Warrior Leggings", "(lv-10) LEGGINGS_Warrior Leggings" },
            { "Amberite Leggings", "(lv-12) LEGGINGS_Amberite Leggings" },
            { "Chainmail Leggings", "(lv-12) LEGGINGS_Chainmail Leggings" },
            { "Darkcloth Pants", "(lv-12) LEGGINGS_Darkcloth Pants" },
            { "Lord Greaves", "(lv-12) LEGGINGS_Lord Greaves" },
            { "Reapsow Pants", "(lv-12) LEGGINGS_Reapsow Pants" },
            { "Witchlock Loincloth", "(lv-12) LEGGINGS_Witchlock Loincloth" },
            { "King Greaves", "(lv-16) LEGGINGS_King Greaves" },
            { "Mercenary Leggings", "(lv-16) LEGGINGS_Mercenary Leggings" },
            { "Reaper Leggings", "(lv-16) LEGGINGS_Reaper Leggings" },
            { "Stridebond Pants", "(lv-16) LEGGINGS_Stridebond Pants" },
            { "Witchwizard Garterbelt", "(lv-16) LEGGINGS_Witchwizard Garterbelt" },
            { "Berserker Leggings", "(lv-18) LEGGINGS_Berserker Leggings" },
            { "Fuguefall Pants", "(lv-18) LEGGINGS_Fuguefall Pants" },
            { "Magilord Boots", "(lv-18) LEGGINGS_Magilord Boots" },
            { "Sapphite Leggings", "(lv-18) LEGGINGS_Sapphite Leggings" },
            { "Jadewail Trousers", "(lv-20) LEGGINGS_Jadewail Trousers" },
            { "Temrak Britches", "(lv-20) LEGGINGS_Temrak Britches" },
            { "Eschek Greaves", "(lv-22) LEGGINGS_Eschek Greaves" },
            { "Gemveil Leggings", "(lv-22) LEGGINGS_Gemveil Leggings" },
            { "Executioner Leggings", "(lv-24) LEGGINGS_Executioner Leggings" },
            { "Fender Leggings", "(lv-24) LEGGINGS_Fender Leggings" },

            // Armor - Shields
            { "Wooden Shield", "(lv-1) SHIELD_Wooden Shield" },
            { "Crypt Buckler", "(lv-4) SHIELD_Crypt Buckler" },
            { "Slimek Shield", "(lv-4) SHIELD_Slimek Shield" },
            { "Demicrypt Buckler", "(lv-6) SHIELD_Demicrypt Buckler" },
            { "Dense Shield", "(lv-6) SHIELD_Dense Shield" },
            { "Iron Shield", "(lv-6) SHIELD_Iron Shield" },
            { "Iris Shield", "(lv-8) SHIELD_Iris Shield" },
            { "Omen Shield", "(lv-8) SHIELD_Omen Shield" },
            { "Amberite Shield", "(lv-12) SHIELD_Amberite Shield" },
            { "Slabton Shield", "(lv-12) SHIELD_Slabton Shield" },
            { "Mithril Shield", "(lv-14) SHIELD_Mithril Shield" },
            { "Nethercrypt Shield", "(lv-14) SHIELD_Nethercrypt Shield" },
            { "Rustweary Shield", "(lv-16) SHIELD_Rustweary Shield" },
            { "Rustwise Shield", "(lv-16) SHIELD_Rustwise Shield" },
            { "Sapphite Shield", "(lv-18) SHIELD_Sapphite Shield" },
            { "Rigor Buckler", "(lv-20) SHIELD_Rigor Buckler" },
            { "Daemon Shield", "(lv-22) SHIELD_Daemon Shield" },
            { "Irisun Shield", "(lv-22) SHIELD_Irisun Shield" },

            // Accessories - Trinkets (Rings)
            { "Old Ring", "(lv-1) RING_Old Ring" },
            { "Ring Of Ambition", "(lv-1) RING_Ring Of Ambition" },
            { "Nograd's Amulet", "(lv-2) RING_Nograd's Amulet" },
            { "The One Ring", "(lv-2) RING_The One Ring" },
            { "Ambersquire Ring", "(lv-6) RING_Ambersquire Ring" },
            { "Emeraldfocus Ring", "(lv-6) RING_Emeraldfocus Ring" },
            { "Sapphireweave Ring", "(lv-6) RING_Sapphireweave Ring" },
            { "Edon's Pendant", "(lv-8) RING_Edon's Pendant" },
            { "Geistlord Ring", "(lv-12) RING_Geistlord Ring" },
            { "Students Ring", "(lv-12) RING_Students Ring" },
            { "Pearlpond Ring", "(lv-14) RING_Pearlpond Ring" },
            { "Slitherwraith Ring", "(lv-14) RING_Slitherwraith Ring" },
            { "Geistlord Band", "(lv-16) RING_Geistlord Band" },
            { "Jadetrout Ring", "(lv-16) RING_Jadetrout Ring" },
            { "Orbos Ring", "(lv-16) RING_Orbos Ring" },
            { "Valor Ring", "(lv-16) RING_Valor Ring" },
            { "Earthwoken Ring", "(lv-18) RING_Earthwoken Ring" },
            { "Noji Talisman", "(lv-20) RING_Noji Talisman" },
            { "Valdur Effigy", "(lv-24) RING_Valdur Effigy" },
            { "Glyphik Booklet", "(lv-26) RING_Glyphik Booklet" },
            { "Tessellated Drive", "(lv-26) RING_Tessellated Drive" },

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
                // FIX #3: Guard against null InputFields during early scene loading.
                // When auto-connect fires before the settings menu has been created,
                // apServer/apSlot are still null, causing NullReferenceException.
                if (apServer == null || apSlot == null)
                {
                    Logger.LogWarning("[AtlyssAP] UI not ready yet (InputFields are null). Retrying later...");
                    connecting = false;
                    return;
                }

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
                        if (slotData.ContainsKey("random_portals"))
                        {
                            randomPortalsEnabled = Convert.ToInt32(slotData["random_portals"]) == 1;
                            Logger.LogInfo($"[AtlyssAP] Portal Mode: {(randomPortalsEnabled ? "Random Portals" : "Progressive Portals")}");
                        }
                        // CHANGED: Updated equipment slot data reading for Gated/Random system.
                        // Was: equipmentProgressionOption with "Progressive" vs "Random" logging.
                        // Now: equipmentGatingOption with "Gated" vs "Random" logging.
                        // The slot data key "equipment_progression" stays the same (matches Python options.py).
                        // Value 0 = Random (equipment placed anywhere), Value 1 = Gated (equipment restricted by location tier).
                        // This is purely informational on the C# side - gating is enforced during seed generation in Python.
                        if (slotData.ContainsKey("equipment_progression"))
                        {
                            equipmentGatingOption = Convert.ToInt32(slotData["equipment_progression"]);
                            Logger.LogInfo($"[AtlyssAP] Equipment: {(equipmentGatingOption == 1 ? "Gated" : "Random")}");
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
                    ArchipelagoSpikeStorage.ClearAPSession();
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

                // Reset progressive counters
                progressivePortalCount = 0;
                // REMOVED: progressiveEquipmentTier reset - Progressive Equipment no longer exists.
                // Equipment is now distributed via Gated/Random item_rules during Python seed generation.

                // NEW: Reset shop sanity state
                _shopSanity.Reset();

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

                SendAPChatMessage(
                    $"Received <color=yellow>{itemName}</color> " +
                    $"from <color=#00FFFF>{fromPlayerName}</color>!"
                );
                HandleReceivedItem(itemName);
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
                // === PROGRESSIVE PORTAL ===
                // Each copy received unlocks the next area in the fixed sequence.
                if (itemName == "Progressive Portal")
                {
                    progressivePortalCount++;
                    Logger.LogInfo($"[AtlyssAP] Progressive Portal #{progressivePortalCount}");

                    if (progressivePortalCount <= _progressivePortalOrder.Count)
                    {
                        string portalName = _progressivePortalOrder[progressivePortalCount - 1];
                        if (_portalScenes.ContainsKey(portalName))
                        {
                            string sceneName = _portalScenes[portalName];
                            portalLocker.UnblockAccessToScene(sceneName);
                            // FIX #4: Null-guard the chat send for portal unlocks.
                            // Player/chat may not be ready when portals unlock during early item receive.
                            SendAPChatMessage($"<color=#00FFFF>{portalName.Replace(" Portal", "")} unlocked!</color>");
                        }
                    }
                    return;
                }

                // REMOVED: Entire "Progressive Equipment" handling block.
                // Progressive Equipment item no longer exists in the item pool.
                // Equipment distribution is now controlled by the Gated/Random option in Python:
                //   - Gated mode: item_rules restrict equipment tiers to appropriate-level locations during seed generation
                //   - Random mode: equipment can appear at any location
                // In both modes, the C# plugin simply receives equipment items normally and stores them in Spike.
                // The old code incremented progressiveEquipmentTier, picked random equipment based on player level,
                // and added it to storage. This is no longer needed since actual equipment items are placed directly
                // in the seed by the Python randomizer.

                // === INDIVIDUAL PORTAL ITEMS (Random Portals mode only) ===
                if (_portalItemsReceived.ContainsKey(itemName))
                {
                    _portalItemsReceived[itemName] = true;
                    Logger.LogInfo($"[AtlyssAP] Received {itemName}!");

                    if (_portalScenes.ContainsKey(itemName))
                    {
                        string sceneName = _portalScenes[itemName];
                        portalLocker.UnblockAccessToScene(sceneName);
                        // FIX #4: Same null-guard concern as progressive portals above
                        SendAPChatMessage($"<color=#00FFFF>{itemName.Replace(" Portal", "")} unlocked!</color>");
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

        // Progressive portal unlocking is now handled directly in HandleReceivedItem
        // by incrementing progressivePortalCount and unlocking the next portal in sequence.

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

        // FIX: Changed filler quantity logic so ALL non-equipment items give 10 instead of 1.
        // Old code only checked for "Pack" suffix, so individual trade items like Sugshrimp,
        // Bonefish, Agility Stone, etc. were only giving 1. Now we check whether the item
        // maps to an equipment type (WEAPON_, HELM_, etc.) - if yes, give 1; otherwise give 10.
        private int DetermineItemQuantity(string itemName)
        {
            // Equipment items should only give 1 (weapons, armor, accessories)
            if (ItemNameMapping.TryGetValue(itemName, out string gameItemName))
            {
                string[] equipmentPrefixes = { "WEAPON_", "HELM_", "CHESTPIECE_", "LEGGINGS_", "CAPE_", "SHIELD_", "RING_" };
                foreach (string prefix in equipmentPrefixes)
                {
                    if (gameItemName.Contains(prefix))
                        return 1;  // Equipment: always 1
                }
            }
            return 10;  // All filler (consumables, trade items, fish, ores, etc.): always 10
        }

        // REMOVED: GetEquipmentForLevel helper method - no longer needed.
        // Was used by Progressive Equipment to find eligible gear based on player level.
        // Progressive Equipment item has been removed from the pool entirely.
        // Equipment is now placed directly in the seed by the Python randomizer using
        // Gated (tier-restricted by location level) or Random (unrestricted) item_rules.

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
                // FIX #4: Check chat is not null BEFORE calling SetValue on it.
                // Previously maxOnscreenMessages.SetValue(chat, 50) ran before the null check,
                // causing NullReferenceException when chat wasn't ready during early item receives.
                if (chat == null) return;
                maxOnscreenMessages.SetValue(chat, 50);

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