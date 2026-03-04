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

        // Shop location IDs are built at runtime from DataTables location name lookups
        // instead of hardcoded IDs, since the new Python uses index-based IDs
        private long[] _shopLocationIds;

        // Maps NPC GameObject name fragments to the shop's Python location name prefix
        // Format in Python: "Buy Item #N from <ShopName>"
        // NPC names are matched via Contains() so partial names work (e.g. "Sally" matches "_npc_Sally")
        // TODO: Craig, Torta, and Mad Statue NPC GameObject names need to be confirmed with dnSpy
        private static readonly Dictionary<string, string> MERCHANT_NPC_TO_SHOP = new Dictionary<string, string>
        {
            { "Sally", "Sally's Nook" },
            { "Skrit", "Skrit's Sikrit Market" },
            { "sallyWorker_frankie_01", "Frankie's Goods" },
            { "Craig", "Craig's Bazzar" },                   // TODO: confirm NPC name
            { "dyeMerchant", "Dye Merchant" },
            { "Tesh", "Tesh's Wares" },
            { "Nesh", "Nesh's Wares" },
            { "Rikko", "Rikko's Treasures" },
            { "Cotoo", "Cotoo's Treasures" },
            { "Ruka", "Ruka's Furnace" },
            { "fisher", "Torta's Fishing Shack" },           // TODO: confirm NPC name, might be "Torta" not "fisher"
            { "madStatue", "Mad Statue's Gift" },             // TODO: confirm NPC name
        };

        // Built at runtime: maps shop Python name to its location ID range
        private Dictionary<string, List<long>> _merchantLocationIds = new Dictionary<string, List<long>>();

        // UPDATED: Structured shop prices — each merchant's 5 items get these
        // prices shuffled randomly. Much more predictable than the old 15-3000 range.
        private static readonly int[] SHOP_PRICE_TIERS = new int[] { 200, 400, 600, 800, 1000 };

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

        // Builds shop location IDs from DataTables at runtime
        // Must be called after DataTables is loaded (during connection)
        public void InitializeShopLocations()
        {
            _merchantLocationIds.Clear();
            List<long> allIds = new List<long>();

            foreach (var kvp in MERCHANT_NPC_TO_SHOP)
            {
                string shopName = kvp.Value;
                List<long> ids = new List<long>();

                for (int i = 1; i <= 5; i++)
                {
                    string locationName = $"Buy Item #{i} from {shopName}";
                    if (AtlyssArchipelagoPlugin.AllLocationNameToId.TryGetValue(locationName, out long id))
                    {
                        ids.Add(id);
                        allIds.Add(id);
                    }
                    else
                    {
                        _logger.LogWarning($"[AtlyssAP] Shop location not found in DataTables: {locationName}");
                    }
                }

                _merchantLocationIds[shopName] = ids;
                _logger.LogInfo($"[AtlyssAP] Mapped {shopName}: {ids.Count} locations");
            }

            _shopLocationIds = allIds.ToArray();
            _logger.LogInfo($"[AtlyssAP] Initialized {_shopLocationIds.Length} shop location IDs across {_merchantLocationIds.Count} merchants");
        }

        public void SendLocationScouts(ArchipelagoSession session)
        {
            try
            {
                if (session == null)
                {
                    _logger.LogError("[AtlyssAP] Cannot scout - no session");
                    return;
                }

                // Build location IDs from DataTables if not already done
                if (_shopLocationIds == null || _shopLocationIds.Length == 0)
                {
                    InitializeShopLocations();
                }

                if (_shopLocationIds.Length == 0)
                {
                    _logger.LogError("[AtlyssAP] No shop location IDs found - cannot scout");
                    return;
                }

                _logger.LogInfo($"[AtlyssAP] Scouting {_shopLocationIds.Length} shop locations...");

                session.Locations.ScoutLocationsAsync(_shopLocationIds).ContinueWith(task =>
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

        /// <summary>
        /// Shuffle an array in-place using Fisher-Yates algorithm.
        /// </summary>
        private void ShuffleArray(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }

        private void ProcessScoutedLocations(ArchipelagoSession session, Dictionary<long, ScoutedItemInfo> scoutedLocations)
        {
            try
            {
                _scoutedShopItems.Clear();

                _logger.LogInfo($"[AtlyssAP] Processing {scoutedLocations.Count} scouted shop items");

                // Assign prices per merchant group.
                // Each merchant's 5 items get the 5 price tiers (200/400/600/800/1000)
                // shuffled randomly so pricing feels varied but stays affordable.
                foreach (var merchantKvp in _merchantLocationIds)
                {
                    string shopName = merchantKvp.Key;
                    List<long> locationIds = merchantKvp.Value;

                    // Shuffle price tiers for this merchant
                    int[] prices = (int[])SHOP_PRICE_TIERS.Clone();
                    ShuffleArray(prices);

                    for (int i = 0; i < locationIds.Count; i++)
                    {
                        long locationId = locationIds[i];

                        if (!scoutedLocations.TryGetValue(locationId, out ScoutedItemInfo itemInfo))
                        {
                            _logger.LogWarning($"[AtlyssAP] Missing scouted data for location {locationId}");
                            continue;
                        }

                        string itemName = session.Items.GetItemName(itemInfo.ItemId, itemInfo.ItemGame) ?? $"Item {itemInfo.ItemId}";
                        string playerName = session.Players.GetPlayerName(itemInfo.Player) ?? $"Player {itemInfo.Player}";

                        int price = i < prices.Length ? prices[i] : SHOP_PRICE_TIERS[0];

                        _scoutedShopItems[locationId] = new ShopAPItemInfo
                        {
                            ItemName = itemName,
                            FromPlayer = playerName,
                            LocationId = locationId,
                            Price = price,
                            SlotNumber = -1
                        };

                        _logger.LogInfo($"[AtlyssAP] Scouted {shopName} #{i + 1}: {itemName} from {playerName} ({price} crowns)");
                    }
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

                // Find which shop this NPC belongs to
                string shopName = null;
                foreach (var kvp in MERCHANT_NPC_TO_SHOP)
                {
                    if (npcName.Contains(kvp.Key))
                    {
                        shopName = kvp.Value;
                        break;
                    }
                }

                if (shopName == null)
                {
                    _logger.LogWarning($"[AtlyssAP] Unknown merchant NPC: '{npcName}' - skipping AP injection");
                    return;
                }

                if (!_merchantLocationIds.TryGetValue(shopName, out List<long> locationIds))
                {
                    _logger.LogWarning($"[AtlyssAP] No location IDs found for shop: {shopName}");
                    return;
                }

                _logger.LogInfo($"[AtlyssAP] Matched to merchant: {shopName} ({locationIds.Count} locations)");

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
                    _logger.LogWarning($"[AtlyssAP] Only found {availableSlots.Count} available slots for {shopName}");
                }

                int slotIndex = 0;
                foreach (long locationId in locationIds)
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
                        _logger.LogWarning($"[AtlyssAP] Ran out of slots for {shopName}");
                        break;
                    }

                    int assignedSlot = availableSlots[slotIndex];
                    shopItem.SlotNumber = assignedSlot;

                    // Show item name AND who it's from so players know exactly what they're buying
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

                _logger.LogInfo($"[AtlyssAP] Successfully injected {slotIndex} AP items into {shopName}!");
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