using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // Handles Shop Sanity: scouts AP locations, injects items into shops, detects purchases
    public class ArchipelagoShopSanity
    {
        private readonly AtlyssArchipelagoPlugin _plugin;
        private readonly ManualLogSource _logger;

        // Shop location IDs (591300-591304)
        private const long SHOP_PURCHASE_BASE = 591300;
        private static readonly long[] SHOP_LOCATION_IDS = new long[]
        {
            SHOP_PURCHASE_BASE + 0, // 591300
            SHOP_PURCHASE_BASE + 1, // 591301
            SHOP_PURCHASE_BASE + 2, // 591302
            SHOP_PURCHASE_BASE + 3, // 591303
            SHOP_PURCHASE_BASE + 4  // 591304
        };

        // Random price tiers for AP items (15 to 5000 crowns)
        private static readonly int[] SHOP_PRICE_TIERS = { 15, 500, 1500, 3500, 5000 };

        // Data structure for scouted shop items
        private class ShopAPItemInfo
        {
            public string ItemName;
            public string FromPlayer;
            public long LocationId;
            public int Price;
            public int SlotNumber;
        }

        // State tracking
        private Dictionary<long, ShopAPItemInfo> _scoutedShopItems = new Dictionary<long, ShopAPItemInfo>();
        private HashSet<long> _purchasedShopItems = new HashSet<long>();
        private bool _shopItemsInitialized = false;

        public ArchipelagoShopSanity(AtlyssArchipelagoPlugin plugin, ManualLogSource logger)
        {
            _plugin = plugin;
            _logger = logger;
        }

        public bool IsInitialized => _shopItemsInitialized;

        // Send LocationScouts packet to AP server to peek at shop item names
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

                // Async scout request
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

        // Process scouted data and store item info with random prices
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

                    // Get item name and player name
                    string itemName = session.Items.GetItemName(itemInfo.ItemId, itemInfo.ItemGame) ?? $"Item {itemInfo.ItemId}";
                    string playerName = session.Players.GetPlayerName(itemInfo.Player) ?? $"Player {itemInfo.Player}";

                    // Assign random price tier
                    int price = SHOP_PRICE_TIERS[UnityEngine.Random.Range(0, SHOP_PRICE_TIERS.Length)];

                    _scoutedShopItems[locationId] = new ShopAPItemInfo
                    {
                        ItemName = itemName,
                        FromPlayer = playerName,
                        LocationId = locationId,
                        Price = price,
                        SlotNumber = -1 // Assigned when injecting into shop
                    };

                    _logger.LogInfo($"[AtlyssAP] Scouted shop #{index + 1}: {itemName} from {playerName} ({price} crowns)");
                    index++;
                }

                _shopItemsInitialized = true;
                _logger.LogInfo($"[AtlyssAP] Shop items initialized! Ready to inject into shops.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error processing scouted locations: {ex.Message}");
            }
        }

        // Inject AP items into shop's vendor items
        public void InjectAPShopItems(NetNPC npc)
        {
            try
            {
                // Find which shop slots are already used
                HashSet<int> usedSlots = new HashSet<int>();

                foreach (var vendorItem in npc._vendorItems.Values)
                {
                    usedSlots.Add(vendorItem._itemData._slotNumber);
                }

                _logger.LogInfo($"[AtlyssAP] Shop has {usedSlots.Count} used slots");

                // Find next available slots for AP items
                List<int> availableSlots = new List<int>();
                for (int slot = 0; availableSlots.Count < SHOP_LOCATION_IDS.Length && slot < 100; slot++)
                {
                    if (!usedSlots.Contains(slot))
                    {
                        availableSlots.Add(slot);
                    }
                }

                if (availableSlots.Count < SHOP_LOCATION_IDS.Length)
                {
                    _logger.LogWarning($"[AtlyssAP] Only found {availableSlots.Count} available slots, need {SHOP_LOCATION_IDS.Length}");
                }

                // Create and inject each AP item
                int slotIndex = 0;
                foreach (var shopItem in _scoutedShopItems.Values)
                {
                    // Skip already purchased items
                    if (_purchasedShopItems.Contains(shopItem.LocationId))
                        continue;

                    if (slotIndex >= availableSlots.Count)
                    {
                        _logger.LogWarning("[AtlyssAP] Ran out of available shop slots!");
                        break;
                    }

                    int assignedSlot = availableSlots[slotIndex];
                    shopItem.SlotNumber = assignedSlot;

                    string displayName = $"[AP] {shopItem.ItemName}";

                    // Create ItemData for shop entry
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

                    // Create ShopkeepItemStruct with custom price
                    ShopkeepItemStruct apShopItem = new ShopkeepItemStruct
                    {
                        _itemName = displayName,
                        _dedicatedValue = shopItem.Price,
                        _useDedicatedValue = true, // Use our custom price
                        _itemData = itemData,
                        _stockQuantity = 1,
                        _removeAtEmptyStock = true, // Remove after purchase
                        _isbuybackItem = false,
                        _isGambleItem = false,
                        _equipModifierID = 0,
                        _specialItemCostName = string.Empty,
                        _specialItemCostQuantity = 0,
                        _gambleValue = 0
                    };

                    // Add to vendor items dictionary
                    if (!npc._vendorItems.ContainsKey(displayName))
                    {
                        npc._vendorItems.Add(displayName, apShopItem);
                        _logger.LogInfo($"[AtlyssAP] Injected: {displayName} in slot {assignedSlot} for {shopItem.Price} crowns");
                    }

                    slotIndex++;
                }

                _logger.LogInfo($"[AtlyssAP] Successfully injected {slotIndex} AP items into shop!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error injecting shop items: {ex.Message}");
            }
        }

        // Poll player inventory for AP item purchases
        public void PollForPurchases(ArchipelagoSession session)
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;

                PlayerInventory inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory == null) return;

                foreach (var shopItem in _scoutedShopItems.Values)
                {
                    if (_purchasedShopItems.Contains(shopItem.LocationId))
                        continue;

                    string displayName = $"[AP] {shopItem.ItemName}";

                    // Check if player has this AP item in inventory
                    if (PlayerHasItem(inventory, displayName))
                    {
                        // Remove placeholder item from inventory
                        RemoveItemFromInventory(inventory, displayName);

                        // Send location check to AP server
                        session.Locations.CompleteLocationChecks(shopItem.LocationId);
                        _purchasedShopItems.Add(shopItem.LocationId);

                        _plugin.SendAPChatMessage($"<color=yellow>Purchased {shopItem.ItemName}!</color> Check Spike storage!");
                        _logger.LogInfo($"[AtlyssAP] Shop purchase detected: {displayName} (Location {shopItem.LocationId})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[AtlyssAP] Error polling shop purchases: {ex.Message}");
            }
        }

        // Reset shop state on disconnect
        public void Reset()
        {
            _scoutedShopItems.Clear();
            _purchasedShopItems.Clear();
            _shopItemsInitialized = false;
            _logger.LogInfo("[AtlyssAP] Shop sanity state reset");
        }

        // Check if player has item in their inventory
        private bool PlayerHasItem(PlayerInventory inventory, string itemName)
        {
            for (int i = 0; i < inventory._heldItems.Count; i++)
            {
                if (inventory._heldItems[i]._itemName == itemName)
                {
                    return true;
                }
            }
            return false;
        }

        // Remove item from player inventory 
        private void RemoveItemFromInventory(PlayerInventory inventory, string itemName)
        {
            for (int i = 0; i < inventory._heldItems.Count; i++)
            {
                if (inventory._heldItems[i]._itemName == itemName)
                {
                    inventory.Remove_Item(inventory._heldItems[i], 0);
                    _logger.LogInfo($"[AtlyssAP] Removed {itemName} from inventory");
                    return;
                }
            }
        }
    }
}