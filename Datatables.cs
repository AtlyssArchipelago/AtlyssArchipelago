using System.Collections.Generic;

namespace AtlyssArchipelagoWIP
{
    // All data dictionaries: location IDs, quest mappings, item name mappings.
    // Separated from main plugin to keep data definitions in one place.
    public partial class AtlyssArchipelagoPlugin
    {
        private const long BASE_LOCATION_ID = 591000;
        private const long DEFEAT_SLIME_DIVA = BASE_LOCATION_ID + 1;
        private const long DEFEAT_LORD_ZUULNERUDA = BASE_LOCATION_ID + 2;
        private const long DEFEAT_GALIUS = BASE_LOCATION_ID + 3;
        private const long DEFEAT_COLOSSUS = BASE_LOCATION_ID + 4;
        private const long DEFEAT_LORD_KALUUZ = BASE_LOCATION_ID + 5;
        private const long DEFEAT_VALDUR = BASE_LOCATION_ID + 6;
        private const long REACH_LEVEL_2 = BASE_LOCATION_ID + 10;

        // Angela "Rude!" achievement trigger - hitting Angela's butt hitbox in Sanctum
        private const long IRRITATE_ANGELA = BASE_LOCATION_ID + 500;

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

            { 591500, "Irritate Angela" },
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
    }
}