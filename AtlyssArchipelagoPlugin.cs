using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using HarmonyLib;
namespace AtlyssArchipelagoWIP
{
    [BepInPlugin("com.azrael.atlyss.ap", "Atlyss Archipelago", "1.3.1")]
    public class AtlyssArchipelagoPlugin : BaseUnityPlugin
    {

        public static AtlyssArchipelagoPlugin Instance { get; private set; }
        public static ManualLogSource StaticLogger { get; private set; }
        private static Harmony _harmony;

        private ConfigEntry<string> cfgServer;
        private ConfigEntry<int> cfgPort;
        private ConfigEntry<string> cfgSlot;
        private ConfigEntry<string> cfgPassword;
        private ConfigEntry<bool> cfgAutoConnect;

        private ArchipelagoSession _session;
        private bool connected;
        private bool connecting;

        private int goalOption = 3;
        private int areaAccessOption = 0;
        private bool shopSanityEnabled = false;

        private bool _catacombsPortalReceived = false;
        private bool _grovePortalReceived = false;
        private readonly HashSet<long> _reportedChecks = new HashSet<long>();

        private int _lastLevel = 0;
        private HashSet<string> _completedQuests = new HashSet<string>();
        private bool _questDebugLogged = false;

        private class PendingItemDrop
        {
            public string ItemName;
            public Vector3 Position;
            public ScriptableItem ScriptableItem;
        }
        private Queue<PendingItemDrop> _itemDropQueue = new Queue<PendingItemDrop>();
        private float _itemDropCooldown = 0f;
        private const float ITEM_DROP_DELAY = 0.3f;

        private const long BASE_LOCATION_ID = 591000;
        private const long DEFEAT_SLIME_DIVA = BASE_LOCATION_ID + 1;
        private const long DEFEAT_LORD_ZUULNERUDA = BASE_LOCATION_ID + 2;
        private const long DEFEAT_GALIUS = BASE_LOCATION_ID + 3;
        private const long DEFEAT_COLOSSUS = BASE_LOCATION_ID + 4;
        private const long DEFEAT_LORD_KALUUZ = BASE_LOCATION_ID + 5;
        private const long DEFEAT_VALDUR = BASE_LOCATION_ID + 6;
        private const long REACH_LEVEL_2 = BASE_LOCATION_ID + 10;

        private static readonly Dictionary<string, long> AllQuestToLocation = new Dictionary<string, long>
        {

            { "Diva Must Die", DEFEAT_SLIME_DIVA },
            { "The Voice of Zuulneruda", DEFEAT_LORD_ZUULNERUDA },
            { "Gatling Galius", DEFEAT_GALIUS },
            { "The Colosseum", DEFEAT_COLOSSUS },


            { "A Warm Welcome", BASE_LOCATION_ID + 30 },
            { "Communing Catacombs", BASE_LOCATION_ID + 31 },

            { "Dense Ingots", BASE_LOCATION_ID + 100 },
            { "Ghostly Goods", BASE_LOCATION_ID + 101 },
            { "Killing Tomb", BASE_LOCATION_ID + 102 },
            { "Night Spirits", BASE_LOCATION_ID + 103 },
            { "Ridding Slimes", BASE_LOCATION_ID + 104 },
            { "Summons' Spectral Powder!", BASE_LOCATION_ID + 105 },


            { "Call of Fury", BASE_LOCATION_ID + 110 },
            { "Cold Shoulder", BASE_LOCATION_ID + 111 },
            { "Focusin' in", BASE_LOCATION_ID + 112 },


            { "Cleansing Terrace", BASE_LOCATION_ID + 115 },
            { "Huntin' Hogs", BASE_LOCATION_ID + 116 },


            { "Ambente Ingots", BASE_LOCATION_ID + 120 },
            { "Makin' a Mekspear", BASE_LOCATION_ID + 121 },
            { "Makin' More Mekspears", BASE_LOCATION_ID + 122 },
            { "Purging the Undead", BASE_LOCATION_ID + 123 },
            { "Battlecage Rage", BASE_LOCATION_ID + 124 },
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
            { "Wicked Wizbars", BASE_LOCATION_ID + 146 },


            { "Reckoning Foes", BASE_LOCATION_ID + 150 },
            { "Blossom of Life", BASE_LOCATION_ID + 151 },
            { "Consumed Madness", BASE_LOCATION_ID + 152 },
            { "Eradicating the Undead", BASE_LOCATION_ID + 153 },
            { "Makin' a Giant Chestpiece", BASE_LOCATION_ID + 154 },
            { "Canmore' Golem Chestpieces", BASE_LOCATION_ID + 155 },
            { "Whatta' Rush!", BASE_LOCATION_ID + 156 },


            { "Finding Armagorn", BASE_LOCATION_ID + 160 },
            { "Reviling_the_Rageboars", BASE_LOCATION_ID + 161 },
            { "Reviling the Ragebears", BASE_LOCATION_ID + 162 },


            { "Makin' a Ragespear", BASE_LOCATION_ID + 165 },
            { "Makin' More Ragespears", BASE_LOCATION_ID + 166 },
            { "Purging the Grave", BASE_LOCATION_ID + 167 },
            { "Searching for the Grove", BASE_LOCATION_ID + 168 },
            { "Tethering Grove", BASE_LOCATION_ID + 169 },
            { "Up and Over It", BASE_LOCATION_ID + 170 },


            { "Makin' a Monolith Chestpiece", BASE_LOCATION_ID + 175 },
            { "Summons' Monolith Chestpieces", BASE_LOCATION_ID + 176 },


            { "Facing Foes", BASE_LOCATION_ID + 180 },


            { "Cleansing the Grove", BASE_LOCATION_ID + 200 },
            { "Hell In The Grove", BASE_LOCATION_ID + 201 },
            { "Makin' a Firebreath Blade", BASE_LOCATION_ID + 202 },
            { "Nulversa Magica", BASE_LOCATION_ID + 203 },
            { "Nulversa Viscera", BASE_LOCATION_ID + 204 },
            { "Nulversa, Greenveras!", BASE_LOCATION_ID + 205 },
            { "Summon' Firebreath Blades", BASE_LOCATION_ID + 206 },


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

        private string GetLocationName(long locationId)
        {
            if (LocationIdToName.TryGetValue(locationId, out string name))
                return name;
            return $"Location {locationId}";
        }

        private static readonly Dictionary<string, string> ItemNameMapping = new Dictionary<string, string>
        {


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

            { "Geistlord Badge Pack", "TRADEITEM_Geistlord Badge" },
            { "Coldgeist Badge Pack", "TRADEITEM_Coldgeist Badge" },
            { "Earthcore Badge Pack", "TRADEITEM_Earthcore Badge" },
            { "Windcore Badge Pack", "TRADEITEM_Windcore Badge" },


            { "Iron Cluster Pack", "TRADEITEM_Iron Cluster" },
            { "Copper Cluster Pack", "TRADEITEM_Copper Cluster" },
            { "Mithril Cluster Pack", "TRADEITEM_Mithril Cluster" },
            { "Dense Ingot Pack", "TRADEITEM_Dense Ingot" },
            { "Sapphite Ingot Pack", "TRADEITEM_Sapphite Ingot" },
            { "Amberite Ingot Pack", "TRADEITEM_Amberite Ingot" },


            { "Soul Pearl", "TRADEITEM_Soul Pearl" },
            { "Experience Bond Pack", "TRADEITEM_Experience Bond" },

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


            { "Leather Britches", "(lv-1) LEGGINGS_Leather Britches" },
            { "Dense Leggings", "(lv-6) LEGGINGS_Dense Leggings" },
            { "Amberite Leggings", "(lv-12) LEGGINGS_Amberite Leggings" },
            { "Lord Greaves", "(lv-12) LEGGINGS_Lord Greaves" },
            { "King Greaves", "(lv-16) LEGGINGS_King Greaves" },
            { "Sapphite Leggings", "(lv-18) LEGGINGS_Sapphite Leggings" },
            { "Berserker Leggings", "(lv-18) LEGGINGS_Berserker Leggings" },
            { "Executioner Leggings", "(lv-24) LEGGINGS_Executioner Leggings" },


            { "Initiate Cloak", "(lv-4) CAPE_Initiate Cloak" },
            { "Nokket Cloak", "(lv-6) CAPE_Nokket Cloak" },
            { "Regazuul Cape", "(lv-10) CAPE_Regazuul Cape" },
            { "Flux Cloak", "(lv-12) CAPE_Flux Cloak" },
            { "Nulversa Cape", "(lv-20) CAPE_Nulversa Cape" },
            { "Windgolem Cloak", "(lv-22) CAPE_Windgolem Cloak" },


            { "Wooden Shield", "(lv-1) SHIELD_Wooden Shield" },
            { "Iron Shield", "(lv-6) SHIELD_Iron Shield" },
            { "Dense Shield", "(lv-6) SHIELD_Dense Shield" },
            { "Amberite Shield", "(lv-12) SHIELD_Amberite Shield" },
            { "Sapphite Shield", "(lv-18) SHIELD_Sapphite Shield" },


            { "Old Ring", "(lv-1) RING_Old Ring" },
            { "Ring Of Ambition", "(lv-1) RING_Ring Of Ambition" },
            { "Sapphireweave Ring", "(lv-6) RING_Sapphireweave Ring" },
            { "Emeraldfocus Ring", "(lv-6) RING_Emeraldfocus Ring" },
            { "Ambersquire Ring", "(lv-6) RING_Ambersquire Ring" },
            { "Geistlord Ring", "(lv-12) RING_Geistlord Ring" },
            { "Geistlord Band", "(lv-16) RING_Geistlord Band" },
            { "Valor Ring", "(lv-16) RING_Valor Ring" },
            { "Valdur Effigy", "(lv-24) RING_Valdur Effigy" },


            { "Tome of the Fighter", "(lv-10) CLASSTOME_Tome of the Fighter" },
            { "Tome of the Mystic", "(lv-10) CLASSTOME_Tome of the Mystic" },
            { "Tome of the Bandit", "(lv-10) CLASSTOME_Tome of the Bandit" },
            { "Tome of the Paladin", "(lv-28) CLASSTOME_Tome of the Paladin" },
            { "Tome of the Magus", "(lv-28) CLASSTOME_Tome of the Magus" },
            { "Tome of the Bishop", "(lv-28) CLASSTOME_Tome of the Bishop" },


            { "Skill Scroll (Alacrity)", "(lv-1) SKILLSCROLL_Skill Scroll (Alacrity)" },
            { "Skill Scroll (Sturdy)", "(lv-1) SKILLSCROLL_Skill Scroll (Sturdy)" },
            { "Skill Scroll (Fira)", "(lv-4) SKILLSCROLL_Skill Scroll (Fira)" },
            { "Skill Scroll (Crya)", "(lv-4) SKILLSCROLL_Skill Scroll (Crya)" },
            { "Skill Scroll (Strength Mastery)", "(lv-10) SKILLSCROLL_Skill Scroll (Strength Mastery)" },
            { "Skill Scroll (Dexterity Mastery)", "(lv-10) SKILLSCROLL_Skill Scroll (Dexterity Mastery)" },
            { "Skill Scroll (Mind Mastery)", "(lv-10) SKILLSCROLL_Skill Scroll (Mind Mastery)" },
            { "Skill Scroll (Taunt)", "(lv-12) SKILLSCROLL_Skill Scroll (Taunt)" },
            { "Skill Scroll (Curis)", "(lv-12) SKILLSCROLL_Skill Scroll (Curis)" }
        };

        private void Awake()
        {
            Instance = this;
            StaticLogger = Logger;
            cfgServer = Config.Bind("Connection", "Server", "localhost",
                "Archipelago host. You can also put host:port here.");
            cfgPort = Config.Bind("Connection", "Port", 38281,
                "Archipelago port (ignored if Server includes :port).");
            cfgSlot = Config.Bind("Connection", "Slot", "Player",
                "Your Archipelago slot name.");
            cfgPassword = Config.Bind("Connection", "Password", "",
                "Room password (optional).");
            cfgAutoConnect = Config.Bind("Connection", "AutoConnect", false,
                "Auto-connect on game start.");
            Logger.LogInfo("=== [AtlyssAP] Plugin loaded! Version 1.3.1 ===");
            Logger.LogInfo("[AtlyssAP] ALL QUESTS + Commands + Item Drops + 137 ITEMS!");
            Logger.LogInfo("[AtlyssAP] Press F5 to connect to Archipelago");

            _harmony = new Harmony("com.azrael.atlyss.ap.harmony");
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
            }

            ProcessItemDropQueue();

            if (connected && (areaAccessOption == 0 || areaAccessOption == 2))
            {
                EnforcePortalLocks();
            }
        }

        private void ProcessItemDropQueue()
        {

            if (_itemDropCooldown > 0f)
            {
                _itemDropCooldown -= Time.deltaTime;
                return;
            }

            if (_itemDropQueue.Count == 0)
                return;

            PendingItemDrop drop = _itemDropQueue.Dequeue();
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogError("[AtlyssAP] Player not found when spawning from queue!");
                    return;
                }

                ItemData itemData = new ItemData
                {
                    _itemName = drop.ScriptableItem._itemName,
                    _quantity = 1,
                    _maxQuantity = 99,
                    _modifierID = 0,
                    _isEquipped = false,
                    _slotNumber = 0
                };

                GameManager._current.Server_SpawnNetItemObject(
                    localPlayer.gameObject,
                    itemData,
                    null,
                    drop.Position,
                    0,
                    localPlayer.gameObject.scene
                );
                Logger.LogInfo($"[AtlyssAP] Dropped {drop.ItemName} at {drop.Position} ({_itemDropQueue.Count} remaining in queue)");

                _itemDropCooldown = ITEM_DROP_DELAY;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to spawn {drop.ItemName}: {ex.Message}");
                Logger.LogError($"[AtlyssAP] Stack: {ex.StackTrace}");
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
                ParseServer(cfgServer.Value, cfgPort.Value, out string host, out int port);
                Logger.LogInfo($"[AtlyssAP] Server: {host}:{port}");
                Logger.LogInfo($"[AtlyssAP] Slot: {cfgSlot.Value}");
                _session = ArchipelagoSessionFactory.CreateSession(host, port);
                Logger.LogInfo("[AtlyssAP] Session created");
                string password = string.IsNullOrWhiteSpace(cfgPassword.Value) ? null : cfgPassword.Value;
                LoginResult login = _session.TryConnectAndLogin(
                    "ATLYSS",
                    cfgSlot.Value,
                    ItemsHandlingFlags.AllItems,
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

                try
                {
                    LoginSuccessful loginSuccess = login as LoginSuccessful;
                    if (loginSuccess != null && loginSuccess.SlotData != null)
                    {
                        var slotData = loginSuccess.SlotData;
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
                    "Your goal: <color=cyan>Reach Level 32</color>"
                };
                if (goalOption >= 0 && goalOption < goalMessages.Length)
                {
                    SendAPChatMessage(goalMessages[goalOption]);
                }

                ApplyAreaAccessMode();
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
                    }
                }
                connected = true;
                connecting = false;
                Logger.LogInfo("=== [AtlyssAP] Connected and ready! ===");
                Logger.LogInfo("[AtlyssAP] Automatic detection active - level-ups and quests will be tracked!");
                Logger.LogInfo("[AtlyssAP] Items will drop on the ground - pick them up!");
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
                    try { _session.Items.ItemReceived -= OnItemReceived; } catch {  }
                    try
                    {
                        if (_session.Socket != null)
                            _session.Socket.DisconnectAsync();
                    }
                    catch {  }
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
                _questDebugLogged = false;
                _itemDropQueue.Clear();
                _itemDropCooldown = 0f;
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
                    $"from <color=cyan>{fromPlayerName}</color>!"
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

                if (itemName == "Catacombs Portal")
                {
                    _catacombsPortalReceived = true;
                    UnlockCatacombsPortal();
                    CheckProgressiveUnlock();
                    return;
                }
                if (itemName == "Grove Portal")
                {
                    _grovePortalReceived = true;
                    if (areaAccessOption != 2)
                    {
                        UnlockGrovePortal();
                    }
                    else
                    {
                        CheckProgressiveUnlock();
                    }
                    return;
                }

                if (itemName.StartsWith("Crowns"))
                {
                    int amount = GetCurrencyAmount(itemName);
                    GiveCurrency(amount);
                    return;
                }

                if (ItemNameMapping.TryGetValue(itemName, out string gameItemName))
                {
                    int quantity = DetermineItemQuantity(itemName);
                    DropItem(gameItemName, quantity);
                }
                else
                {
                    Logger.LogWarning($"[AtlyssAP] Unknown item type: {itemName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error handling item '{itemName}': {ex.Message}");
            }
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
        private void DropItem(string gameItemName, int quantity)
        {
            try
            {
                Logger.LogInfo($"[AtlyssAP] Queuing {quantity}x {gameItemName} to drop...");
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogError("[AtlyssAP] Player not found!");
                    return;
                }

                Vector3 playerPos = localPlayer.transform.position;

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
                    return;
                }
                Logger.LogInfo($"[AtlyssAP] DEBUG: Found ScriptableItem: {scriptableItem._itemName}");

                for (int i = 0; i < quantity; i++)
                {

                    float angle = (360f / quantity) * i * Mathf.Deg2Rad;
                    float radius = 2f;
                    Vector3 spawnPos = playerPos + new Vector3(
                        Mathf.Cos(angle) * radius,
                        1.5f,
                        Mathf.Sin(angle) * radius
                    );

                    _itemDropQueue.Enqueue(new PendingItemDrop
                    {
                        ItemName = itemName,
                        Position = spawnPos,
                        ScriptableItem = scriptableItem
                    });
                }
                Logger.LogInfo($"[AtlyssAP] Queued {quantity}x {itemName}! Will drop one every {ITEM_DROP_DELAY}s");
                Logger.LogInfo($"[AtlyssAP] Queue now has {_itemDropQueue.Count} items total");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to queue item '{gameItemName}': {ex.Message}");
                Logger.LogError($"[AtlyssAP] Stack: {ex.StackTrace}");
            }
        }

        private void UnlockCatacombsPortal()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogError("[AtlyssAP] Cannot unlock portal - Player not found!");
                    return;
                }

                string catacombsMapName = "_dungeon_catacombs";

                if (localPlayer._waypointAttunements.Contains(catacombsMapName))
                {
                    Logger.LogInfo("[AtlyssAP] Catacombs portal already unlocked");
                    return;
                }

                localPlayer._waypointAttunements.Add(catacombsMapName);

                if (WorldPortalManager._current != null)
                {
                    WorldPortalManager._current.Refresh_ZoneEntries();
                }
                Logger.LogInfo("[AtlyssAP] Catacombs Portal unlocked!");
                SendAPChatMessage("Portal unlocked: <color=cyan>Catacombs</color>");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to unlock Catacombs portal: {ex.Message}");
            }
        }

        private void UnlockGrovePortal()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogError("[AtlyssAP] Cannot unlock portal - Player not found!");
                    return;
                }

                string groveMapName = "_dungeon_crescentGrove";

                if (localPlayer._waypointAttunements.Contains(groveMapName))
                {
                    Logger.LogInfo("[AtlyssAP] Grove portal already unlocked");
                    return;
                }

                localPlayer._waypointAttunements.Add(groveMapName);

                if (WorldPortalManager._current != null)
                {
                    WorldPortalManager._current.Refresh_ZoneEntries();
                }
                Logger.LogInfo("[AtlyssAP] Grove Portal unlocked!");
                SendAPChatMessage("Portal unlocked: <color=cyan>The Grove</color>");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to unlock Grove portal: {ex.Message}");
            }
        }

        private void CheckProgressiveUnlock()
        {
            if (areaAccessOption != 2) return;
            if (_catacombsPortalReceived && _grovePortalReceived)
            {
                UnlockGrovePortal();
                SendAPChatMessage("<color=cyan>Both portals found - Grove unlocked!</color>");
            }
            else if (_grovePortalReceived && !_catacombsPortalReceived)
            {
                SendAPChatMessage("Grove portal found, but need <color=yellow>Catacombs portal</color> first!");
            }
        }

        private void LockAllPortals()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogError("[AtlyssAP] Cannot lock portals - Player not found!");
                    return;
                }
                string catacombsMapName = "_dungeon_catacombs";
                string groveMapName = "_dungeon_crescentGrove";

                int lockedCount = 0;

                if (localPlayer._waypointAttunements.Contains(catacombsMapName))
                {
                    localPlayer._waypointAttunements.Remove(catacombsMapName);
                    lockedCount++;
                }

                if (localPlayer._waypointAttunements.Contains(groveMapName))
                {
                    localPlayer._waypointAttunements.Remove(groveMapName);
                    lockedCount++;
                }

                if (WorldPortalManager._current != null)
                {
                    WorldPortalManager._current.Refresh_ZoneEntries();
                }
                Logger.LogInfo($"[AtlyssAP] Locked {lockedCount} portals for Archipelago mode");

                if (lockedCount > 0)
                {
                    SendAPChatMessage($"<color=orange>{lockedCount} portals locked</color> - find portal items to unlock!");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to lock portals: {ex.Message}");
            }
        }

        private void EnforcePortalLocks()
        {
            Player localPlayer = Player._mainPlayer;
            if (localPlayer == null) return;
            bool needsRefresh = false;

            if (!_catacombsPortalReceived && localPlayer._waypointAttunements.Contains("_dungeon_catacombs"))
            {
                localPlayer._waypointAttunements.Remove("_dungeon_catacombs");
                needsRefresh = true;
            }

            bool shouldLockGrove = false;

            if (areaAccessOption == 0)
            {

                shouldLockGrove = !_grovePortalReceived;
            }
            else if (areaAccessOption == 2)
            {

                shouldLockGrove = !_catacombsPortalReceived || !_grovePortalReceived;
            }
            if (shouldLockGrove && localPlayer._waypointAttunements.Contains("_dungeon_crescentGrove"))
            {
                localPlayer._waypointAttunements.Remove("_dungeon_crescentGrove");
                needsRefresh = true;
            }

            if (needsRefresh && WorldPortalManager._current != null)
            {
                WorldPortalManager._current.Refresh_ZoneEntries();
            }
        }
        private void ApplyAreaAccessMode()
        {
            if (areaAccessOption == 1)
            {
                Logger.LogInfo("[AtlyssAP] Area Access: Unlocked - Opening all areas");

                UnlockCatacombsPortal();
                UnlockGrovePortal();
                SendAPChatMessage("<color=cyan>All areas unlocked!</color>");
            }
            else if (areaAccessOption == 0)
            {
                Logger.LogInfo("[AtlyssAP] Area Access: Locked - Portals must be found");


                LockAllPortals();
            }
            else if (areaAccessOption == 2)
            {
                Logger.LogInfo("[AtlyssAP] Area Access: Progressive - Portals unlock sequentially");


                LockAllPortals();
            }
        }

        private void SendAPChatMessage(string message)
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                ChatBehaviour chat = localPlayer._chatBehaviour;
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

                _session.Socket.SendPacketAsync(new StatusUpdatePacket
                {
                    Status = ArchipelagoClientState.ClientGoal
                });
                SendAPChatMessage("<color=yellow>Release command sent!</color>");
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

                SendAPChatMessage("<color=yellow>Checking for new items...</color>");
                Logger.LogInfo("[AtlyssAP] Collect command executed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to execute collect: {ex.Message}");
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
                Logger.LogInfo($"[AtlyssAP] Hint requested for: {args}");

                SendAPChatMessage("<color=yellow>Hint system not yet fully implemented</color>");
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
            SendAPChatMessage("/collect - Check for new items");
            SendAPChatMessage("/hint [item] - Request hint (WIP)");
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
        private static void ParseServer(string raw, int fallbackPort, out string host, out int port)
        {
            host = (raw ?? "localhost").Trim();
            port = fallbackPort;
            int colonIndex = host.LastIndexOf(':');
            if (colonIndex > 0 && colonIndex < host.Length - 1)
            {
                string maybeHost = host.Substring(0, colonIndex);
                string maybePort = host.Substring(colonIndex + 1);
                if (int.TryParse(maybePort, out int parsedPort))
                {
                    host = maybeHost;
                    port = parsedPort;
                }
            }
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "localhost";
            }
        }
    }

    [HarmonyPatch(typeof(ChatBehaviour), "Send_ChatMessage")]
    public static class ChatBehaviourPatch
    {
        static bool Prefix(ChatBehaviour __instance, string _message)
        {
            try
            {

                if (!string.IsNullOrEmpty(_message) && _message.StartsWith("/"))
                {

                    string[] apCommands = { "/release", "/collect", "/hint", "/help", "/players", "/status" };

                    bool isAPCommand = false;
                    foreach (string cmd in apCommands)
                    {
                        if (_message.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))
                        {
                            isAPCommand = true;
                            break;
                        }
                    }
                    if (isAPCommand)
                    {

                        if (AtlyssArchipelagoPlugin.Instance != null)
                        {
                            AtlyssArchipelagoPlugin.Instance.HandleArchipelagoCommand(_message);
                        }

                        __instance._focusedInChat = false;

                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                if (AtlyssArchipelagoPlugin.StaticLogger != null)
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Chat patch error: {ex.Message}");
                }
                return true;
            }
        }
    }
}
