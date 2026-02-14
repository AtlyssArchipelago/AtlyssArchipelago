using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    public class ArchipelagoShopSanity
    {
        private readonly AtlyssArchipelagoPlugin _plugin;
        private readonly ManualLogSource _logger;

        private static readonly long[] SHOP_LOCATION_IDS = new long[]
        {
            591300, 591301, 591302, 591303, 591304,
            591305, 591306, 591307, 591308, 591309,
            591310, 591311, 591312, 591313, 591314,
            591315, 591316, 591317, 591318, 591319,
            591320, 591321, 591322, 591323, 591324,
            591325, 591326, 591327, 591328, 591329,
            591330, 591331, 591332, 591333, 591334,
            591335, 591336, 591337, 591338, 591339,
            591340, 591341, 591342, 591343, 591344,
            591345, 591346, 591347, 591348, 591349
        };

        // UPDATED: Fixed merchant names to match actual GameObject names in-game
        // fisher changed from "Fisher" to "fisher" (lowercase)
        // dyeMerchant changed from "Dye Merchant" to "dyeMerchant" (camelCase)
        // sallyWorker_frankie_01 changed from "Frankie" to "sallyWorker_frankie_01" (full GameObject name)
        private static readonly Dictionary<string, (long start, long end)> MERCHANT_LOCATION_RANGES = new Dictionary<string, (long, long)>
        {
            { "Sally", (591300, 591304) },                      // _npc_Sally
            { "Skrit", (591305, 591309) },                      // _npc_Skrit
            { "sallyWorker_frankie_01", (591310, 591314) },     // _npc_sallyWorker_frankie_01 - FIXED: full name
            { "Ruka", (591315, 591319) },                       // _npc_Ruka
            { "fisher", (591320, 591324) },                     // _npc_fisher - FIXED: lowercase
            { "dyeMerchant", (591325, 591329) },                // _npc_dyeMerchant - FIXED: camelCase
            { "Tesh", (591330, 591334) },                       // _npc_Tesh
            { "Nesh", (591335, 591339) },                       // _npc_Nesh
            { "Cotoo", (591340, 591344) },                      // _npc_Cotoo
            { "Rikko", (591345, 591349) }                       // _npc_Rikko
        };

        // UPDATED: Changed from fixed tiers to a random range of 15-3000 crowns
        // to reduce grinding needed for shop purchases
        private const int SHOP_PRICE_MIN = 15;
        private const int SHOP_PRICE_MAX = 3000;

        private class ShopAPItemInfo
        {
            public string ItemName;
            public string FromPlayer;
            public long LocationId;
            public int Price;
            public int SlotNumber;
        }

        private Dictionary<long, ShopAPItemInfo> _scoutedShopItems = new Dictionary<long, ShopAPItemInfo>();
        private HashSet<long> _purchasedShopItems = new HashSet<long>();
        private bool _shopItemsInitialized = false;

        public ArchipelagoShopSanity(AtlyssArchipelagoPlugin plugin, ManualLogSource logger)
        {
            _plugin = plugin;
            _logger = logger;
        }

        public bool IsInitialized => _shopItemsInitialized;

        public void SendLocationScouts(ArchipelagoSession session)
        {
            try
            {
                if (session == null)
                {
                    _logger.LogError("[AtlyssAP] Cannot scout - no session");
                    return;
                }

                _logger.LogInfo($"[AtlyssAP] Scouting {SHOP_LOCATION_IDS.Length} shop locations...");

                session.Locations.ScoutLocationsAsync(SHOP_LOCATION_IDS).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        _logger.LogError($"[AtlyssAP] Scout failed: {task.Exception?.GetBaseException().Message}");
                        return;
                    }

                    if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        ProcessScoutedLocations(session, task.Result);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error sending shop scouts: {ex.Message}");
            }
        }

        private void ProcessScoutedLocations(ArchipelagoSession session, Dictionary<long, ScoutedItemInfo> scoutedLocations)
        {
            try
            {
                _scoutedShopItems.Clear();

                _logger.LogInfo($"[AtlyssAP] Processing {scoutedLocations.Count} scouted shop items");

                int index = 0;
                foreach (var kvp in scoutedLocations)
                {
                    long locationId = kvp.Key;
                    ScoutedItemInfo itemInfo = kvp.Value;

                    string itemName = session.Items.GetItemName(itemInfo.ItemId, itemInfo.ItemGame) ?? $"Item {itemInfo.ItemId}";
                    string playerName = session.Players.GetPlayerName(itemInfo.Player) ?? $"Player {itemInfo.Player}";

                    int price = UnityEngine.Random.Range(SHOP_PRICE_MIN, SHOP_PRICE_MAX + 1);

                    _scoutedShopItems[locationId] = new ShopAPItemInfo
                    {
                        ItemName = itemName,
                        FromPlayer = playerName,
                        LocationId = locationId,
                        Price = price,
                        SlotNumber = -1
                    };

                    _logger.LogInfo($"[AtlyssAP] Scouted shop #{index + 1}: {itemName} from {playerName} ({price} crowns)");
                    index++;
                }

                _shopItemsInitialized = true;
                _logger.LogInfo($"[AtlyssAP] Shop items initialized! {_scoutedShopItems.Count} items ready.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error processing scouted locations: {ex.Message}");
            }
        }

        public void InjectAPShopItems(NetNPC npc)
        {
            try
            {
                string npcName = npc.gameObject.name;

                _logger.LogInfo($"[AtlyssAP] Found NPC GameObject: '{npcName}'");

                string merchantName = null;
                (long start, long end) range = (0, 0);

                foreach (var kvp in MERCHANT_LOCATION_RANGES)
                {
                    if (npcName.Contains(kvp.Key))
                    {
                        merchantName = kvp.Key;
                        range = kvp.Value;
                        break;
                    }
                }

                if (merchantName == null)
                {
                    _logger.LogWarning($"[AtlyssAP] Unknown merchant NPC: '{npcName}' - skipping AP injection");
                    return;
                }

                _logger.LogInfo($"[AtlyssAP] Matched to merchant: {merchantName} (locations {range.start}-{range.end})");

                HashSet<int> usedSlots = new HashSet<int>();
                foreach (var vendorItem in npc._vendorItems.Values)
                {
                    usedSlots.Add(vendorItem._itemData._slotNumber);
                }

                List<int> availableSlots = new List<int>();
                for (int slot = 0; availableSlots.Count < 5 && slot < 100; slot++)
                {
                    if (!usedSlots.Contains(slot))
                    {
                        availableSlots.Add(slot);
                    }
                }

                if (availableSlots.Count < 5)
                {
                    _logger.LogWarning($"[AtlyssAP] Only found {availableSlots.Count} available slots for {merchantName}");
                }

                int slotIndex = 0;
                for (long locationId = range.start; locationId <= range.end; locationId++)
                {
                    if (!_scoutedShopItems.TryGetValue(locationId, out var shopItem))
                    {
                        _logger.LogWarning($"[AtlyssAP] Missing scouted data for location {locationId}");
                        continue;
                    }

                    if (_purchasedShopItems.Contains(locationId))
                        continue;

                    if (slotIndex >= availableSlots.Count)
                    {
                        _logger.LogWarning($"[AtlyssAP] Ran out of slots for {merchantName}");
                        break;
                    }

                    int assignedSlot = availableSlots[slotIndex];
                    shopItem.SlotNumber = assignedSlot;

                    // UPDATED: Show item name AND who it's from so players know exactly what they're buying
                    // Format: "[AP] ItemName (PlayerName)"
                    string displayName = $"[AP] {shopItem.ItemName} ({shopItem.FromPlayer})";

                    ItemData itemData = new ItemData
                    {
                        _itemName = displayName,
                        _quantity = 0,
                        _maxQuantity = 1,
                        _slotNumber = assignedSlot,
                        _modifierID = 0,
                        _isEquipped = false,
                        _isAltWeapon = false
                    };

                    ShopkeepItemStruct apShopItem = new ShopkeepItemStruct
                    {
                        _itemName = displayName,
                        _dedicatedValue = shopItem.Price,
                        _useDedicatedValue = true,
                        _itemData = itemData,
                        _stockQuantity = 1,
                        _removeAtEmptyStock = true,
                        _isbuybackItem = false,
                        _isGambleItem = false,
                        _equipModifierID = 0,
                        _specialItemCostName = string.Empty,
                        _specialItemCostQuantity = 0,
                        _gambleValue = 0
                    };

                    if (!npc._vendorItems.ContainsKey(displayName))
                    {
                        npc._vendorItems.Add(displayName, apShopItem);
                        _logger.LogInfo($"[AtlyssAP] Injected: {displayName} in slot {assignedSlot} for {shopItem.Price} crowns");
                    }

                    slotIndex++;
                }

                _logger.LogInfo($"[AtlyssAP] Successfully injected {slotIndex} AP items into {merchantName}!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error injecting shop items: {ex.Message}");
            }
        }

        // NEW: Handle AP item purchase (called by Harmony patch when player buys AP item)
        // This replaces the old polling system with immediate purchase detection and location check sending
        // Extracts the actual item name from the display format "[AP] ItemName (PlayerName)"
        public void HandleAPItemPurchase(string displayName, string key)
        {
            try
            {
                // Extract actual item name from "[AP] ItemName (PlayerName)" format. Use already existing helper function
                string itemName = LocateItemPatch.ExtractItemName(displayName);

                _logger.LogInfo($"[AtlyssAP] Looking for purchased item: {itemName}");

                // FIX: Find the FIRST UNPURCHASED shop item matching this name
                // Old code matched by name only without checking purchase state, so duplicate items
                // (e.g. 7x Progressive Portal across different shops) always found the first copy
                // which was already bought, causing "already purchased" errors for every subsequent buy.
                // Now we also check !_purchasedShopItems.Contains() to skip already-bought locations.
                ShopAPItemInfo purchasedItem = null;
                foreach (var shopItem in _scoutedShopItems.Values)
                {
                    if (shopItem.ItemName == itemName && !_purchasedShopItems.Contains(shopItem.LocationId))
                    {
                        purchasedItem = shopItem;
                        break;
                    }
                }

                if (purchasedItem == null)
                {
                    _logger.LogWarning($"[AtlyssAP] No unpurchased AP item found for: {itemName} (all copies may already be bought)");
                    return;
                }

                // Send location check to server
                if (_plugin._session != null && _plugin._session.Socket.Connected)
                {
                    _plugin._session.Locations.CompleteLocationChecks(purchasedItem.LocationId);
                    _purchasedShopItems.Add(purchasedItem.LocationId);

                    _plugin.SendAPChatMessage($"<color=yellow>Purchased {itemName}!</color> Item sent to Spike storage!");
                    _logger.LogInfo($"[AtlyssAP] Shop purchase completed: {displayName} (Location {purchasedItem.LocationId})");
                }
                else
                {
                    _logger.LogError("[AtlyssAP] Not connected to Archipelago server!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error handling AP purchase: {ex.Message}");
            }
        }

        public void Reset()
        {
            _scoutedShopItems.Clear();
            _purchasedShopItems.Clear();
            _shopItemsInitialized = false;
            _logger.LogInfo("[AtlyssAP] Shop sanity state reset");
        }
    }
}