using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // Item receiving: handling received items, creating game ItemData, giving currency.
    public partial class AtlyssArchipelagoPlugin
    {
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
    }
}