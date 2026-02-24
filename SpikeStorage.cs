using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    public class ArchipelagoSpikeStorage
    {
        private const int NUM_BANKS = 7;

        // FIXED: Reduced from 100 to 40. The game's storage UI uses the same
        // grid size for ALL bank tabs (master and numbered). Items at slot 40+
        // crash Create_StorageEntry with IndexOutOfRangeException because the
        // UI entry array only has 40 elements.
        private const int MASTER_BANK_SLOTS = 40;
        private const int NUMBERED_BANK_SLOTS = 40;

        // Total capacity: 40 (master) + 7*40 (numbered) = 320 slots
        // More than enough for AP items

        [Serializable]
        public class ItemBankData
        {
            public List<ItemData> _heldItemStorage = new List<ItemData>();
        }

        // ================================================================
        // SESSION PERSISTENCE
        // A marker file that tracks whether an AP session is active.
        // This allows the file redirect patches to work BEFORE reconnecting
        // so items persist across game restarts.
        // ================================================================

        private static string GetSessionMarkerPath()
        {
            string gameDataPath = Path.Combine(UnityEngine.Application.dataPath, "profileCollections");
            return Path.Combine(gameDataPath, "ap_session_active");
        }

        /// <summary>
        /// Returns true if an AP session was previously active (marker file exists).
        /// Used by file redirect patches to load AP banks even before reconnecting.
        /// </summary>
        public static bool IsAPSessionActive()
        {
            return File.Exists(GetSessionMarkerPath());
        }

        /// <summary>
        /// Called when connecting to AP server. Creates the marker file.
        /// </summary>
        public static void SetAPSessionActive()
        {
            try
            {
                string path = GetSessionMarkerPath();
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, DateTime.UtcNow.ToString("o"));
                AtlyssArchipelagoPlugin.StaticLogger?.LogInfo("[AtlyssAP] AP session marker set (storage will persist across restarts)");
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to set session marker: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when starting a NEW AP game/seed. Removes the marker and clears banks.
        /// </summary>
        public static void ClearAPSession()
        {
            try
            {
                string path = GetSessionMarkerPath();
                if (File.Exists(path))
                    File.Delete(path);
                ClearAllAPBanks();
                AtlyssArchipelagoPlugin.StaticLogger?.LogInfo("[AtlyssAP] AP session cleared (marker removed, banks wiped)");
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to clear session: {ex.Message}");
            }
        }

        // ================================================================
        // BANK PATHS
        // ================================================================

        public static string GetAPMasterBankPath()
        {
            string gameDataPath = Path.Combine(UnityEngine.Application.dataPath, "profileCollections");
            return Path.Combine(gameDataPath, "atl_itemBank_ap");
        }

        public static string GetAPBankPath(int bankNumber)
        {
            if (bankNumber < 1 || bankNumber > NUM_BANKS)
            {
                throw new ArgumentException($"Bank number must be between 1 and {NUM_BANKS}");
            }

            string gameDataPath = Path.Combine(UnityEngine.Application.dataPath, "profileCollections");
            return Path.Combine(gameDataPath, $"atl_itemBank_ap_{bankNumber:D2}");
        }

        // ================================================================
        // BANK INITIALIZATION
        // ================================================================

        public static void InitializeAPBanks()
        {
            try
            {
                string masterPath = GetAPMasterBankPath();
                string masterDir = Path.GetDirectoryName(masterPath);

                if (!Directory.Exists(masterDir))
                {
                    Directory.CreateDirectory(masterDir);
                }

                if (!File.Exists(masterPath))
                {
                    ItemBankData emptyBank = new ItemBankData();
                    string json = JsonConvert.SerializeObject(emptyBank, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(masterPath, json);

                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo("[AtlyssAP] Created AP master item bank");
                }

                for (int i = 1; i <= NUM_BANKS; i++)
                {
                    string apBankPath = GetAPBankPath(i);

                    if (!File.Exists(apBankPath))
                    {
                        ItemBankData emptyBank = new ItemBankData();
                        string json = JsonConvert.SerializeObject(emptyBank, Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(apBankPath, json);

                        AtlyssArchipelagoPlugin.StaticLogger?.LogInfo($"[AtlyssAP] Created AP item bank {i}");
                    }
                }

                AtlyssArchipelagoPlugin.StaticLogger?.LogInfo($"[AtlyssAP] Archipelago item banks initialized");
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to initialize AP banks: {ex.Message}");
            }
        }

        // ================================================================
        // BANK VALIDATION
        // Fixes items with overlapping or out-of-bounds slot numbers.
        // Called every time a bank is loaded to prevent IndexOutOfRangeException.
        // ================================================================

        /// <summary>
        /// Validate and fix slot numbers in a bank. Reassigns any slots that are
        /// out-of-bounds or overlapping. Returns true if any fixes were made.
        /// </summary>
        private static bool ValidateBank(ItemBankData bank, int maxSlots)
        {
            if (bank == null || bank._heldItemStorage == null || bank._heldItemStorage.Count == 0)
                return false;

            bool modified = false;
            HashSet<int> usedSlots = new HashSet<int>();

            foreach (var item in bank._heldItemStorage)
            {
                bool needsReassign = false;

                // Check out-of-bounds
                if (item._slotNumber < 0 || item._slotNumber >= maxSlots)
                {
                    needsReassign = true;
                }
                // Check overlap (duplicate slot number)
                else if (usedSlots.Contains(item._slotNumber))
                {
                    needsReassign = true;
                }

                if (needsReassign)
                {
                    // Find next open slot
                    int newSlot = 0;
                    while (usedSlots.Contains(newSlot) && newSlot < maxSlots)
                    {
                        newSlot++;
                    }

                    if (newSlot < maxSlots)
                    {
                        AtlyssArchipelagoPlugin.StaticLogger?.LogWarning(
                            $"[AtlyssAP] Fixing item '{item._itemName}' slot {item._slotNumber} -> {newSlot} (was out-of-bounds or overlapping)"
                        );
                        item._slotNumber = newSlot;
                        modified = true;
                    }
                    else
                    {
                        // Bank is full — this item can't fit. Remove it.
                        // (This shouldn't happen normally since we cap items per bank)
                        AtlyssArchipelagoPlugin.StaticLogger?.LogWarning(
                            $"[AtlyssAP] Bank full, cannot reassign item '{item._itemName}' — will be dropped"
                        );
                    }
                }

                usedSlots.Add(item._slotNumber);
            }

            // Remove items that couldn't be assigned valid slots
            bank._heldItemStorage.RemoveAll(item => item._slotNumber < 0 || item._slotNumber >= maxSlots);

            return modified;
        }

        // ================================================================
        // BANK LOAD / SAVE
        // ================================================================

        public static ItemBankData LoadAPBank(int bankNumber)
        {
            try
            {
                string path = GetAPBankPath(bankNumber);

                if (!File.Exists(path))
                {
                    return new ItemBankData();
                }

                string json = File.ReadAllText(path);
                ItemBankData bank = JsonConvert.DeserializeObject<ItemBankData>(json);
                bank = bank ?? new ItemBankData();

                // Validate on load — fix any bad slot numbers
                if (ValidateBank(bank, NUMBERED_BANK_SLOTS))
                {
                    SaveAPBank(bankNumber, bank);
                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo($"[AtlyssAP] Fixed slot numbers in AP bank {bankNumber}");
                }

                return bank;
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to load AP bank {bankNumber}: {ex.Message}");
                return new ItemBankData();
            }
        }

        public static void SaveAPBank(int bankNumber, ItemBankData data)
        {
            try
            {
                string path = GetAPBankPath(bankNumber);
                string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to save AP bank {bankNumber}: {ex.Message}");
            }
        }

        public static ItemBankData LoadAPMasterBank()
        {
            try
            {
                string path = GetAPMasterBankPath();

                if (!File.Exists(path))
                {
                    return new ItemBankData();
                }

                string json = File.ReadAllText(path);
                ItemBankData bank = JsonConvert.DeserializeObject<ItemBankData>(json);
                bank = bank ?? new ItemBankData();

                // Validate on load — fix any bad slot numbers
                if (ValidateBank(bank, MASTER_BANK_SLOTS))
                {
                    SaveAPMasterBank(bank);
                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo("[AtlyssAP] Fixed slot numbers in AP master bank");
                }

                return bank;
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to load AP master bank: {ex.Message}");
                return new ItemBankData();
            }
        }

        public static void SaveAPMasterBank(ItemBankData data)
        {
            try
            {
                string path = GetAPMasterBankPath();
                string json = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to save AP master bank: {ex.Message}");
            }
        }

        // ================================================================
        // ADD ITEM TO STORAGE
        // ================================================================

        public static bool AddItemToAPSpike(ItemData itemToAdd)
        {
            try
            {
                // Strategy:
                // 1. Try to stack with existing items across ALL banks (if stackable)
                // 2. If not stackable or no stack space, find first open slot across ALL banks
                // Master bank first, then numbered banks 1-7.

                bool isStackable = itemToAdd._maxQuantity > 1;

                // Step 1: Try stacking across all banks
                if (isStackable)
                {
                    // Try master bank stacking
                    ItemBankData masterBank = LoadAPMasterBank();
                    foreach (var existingItem in masterBank._heldItemStorage)
                    {
                        if (existingItem._itemName == itemToAdd._itemName &&
                            existingItem._quantity < existingItem._maxQuantity)
                        {
                            int spaceAvailable = existingItem._maxQuantity - existingItem._quantity;
                            int amountToAdd = Math.Min(spaceAvailable, itemToAdd._quantity);
                            existingItem._quantity += amountToAdd;
                            itemToAdd._quantity -= amountToAdd;
                            SaveAPMasterBank(masterBank);

                            if (itemToAdd._quantity == 0)
                            {
                                AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                                    $"[AtlyssAP] Added {itemToAdd._itemName} to AP Spike MASTER bank (stacked)"
                                );
                                return true;
                            }
                        }
                    }

                    // Try numbered bank stacking
                    for (int bankNum = 1; bankNum <= NUM_BANKS; bankNum++)
                    {
                        ItemBankData bank = LoadAPBank(bankNum);
                        foreach (var existingItem in bank._heldItemStorage)
                        {
                            if (existingItem._itemName == itemToAdd._itemName &&
                                existingItem._quantity < existingItem._maxQuantity)
                            {
                                int spaceAvailable = existingItem._maxQuantity - existingItem._quantity;
                                int amountToAdd = Math.Min(spaceAvailable, itemToAdd._quantity);
                                existingItem._quantity += amountToAdd;
                                itemToAdd._quantity -= amountToAdd;
                                SaveAPBank(bankNum, bank);

                                if (itemToAdd._quantity == 0)
                                {
                                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                                        $"[AtlyssAP] Added {itemToAdd._itemName} to AP Spike bank {bankNum} (stacked)"
                                    );
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Step 2: Find first open slot across all banks
                // Try master bank
                {
                    ItemBankData masterBank = LoadAPMasterBank();
                    int nextSlot = FindNextOpenSlot(masterBank, MASTER_BANK_SLOTS);
                    if (nextSlot >= 0)
                    {
                        itemToAdd._slotNumber = nextSlot;
                        itemToAdd._isEquipped = false;
                        masterBank._heldItemStorage.Add(itemToAdd);
                        SaveAPMasterBank(masterBank);

                        AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                            $"[AtlyssAP] Added {itemToAdd._itemName} to AP Spike MASTER bank slot {nextSlot}"
                        );
                        return true;
                    }
                }

                // Try numbered banks
                for (int bankNum = 1; bankNum <= NUM_BANKS; bankNum++)
                {
                    ItemBankData bank = LoadAPBank(bankNum);
                    int nextSlot = FindNextOpenSlot(bank, NUMBERED_BANK_SLOTS);
                    if (nextSlot >= 0)
                    {
                        itemToAdd._slotNumber = nextSlot;
                        itemToAdd._isEquipped = false;
                        bank._heldItemStorage.Add(itemToAdd);
                        SaveAPBank(bankNum, bank);

                        AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                            $"[AtlyssAP] Added {itemToAdd._itemName} to AP Spike bank {bankNum} slot {nextSlot}"
                        );
                        return true;
                    }
                }

                AtlyssArchipelagoPlugin.StaticLogger?.LogWarning(
                    $"[AtlyssAP] All AP Spike banks are full! Cannot add {itemToAdd._itemName}"
                );
                return false;
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError(
                    $"[AtlyssAP] Failed to add item to AP Spike: {ex.Message}"
                );
                return false;
            }
        }

        private static int FindNextOpenSlot(ItemBankData bank, int maxSlots)
        {
            HashSet<int> usedSlots = new HashSet<int>();
            foreach (var item in bank._heldItemStorage)
            {
                usedSlots.Add(item._slotNumber);
            }

            int nextSlot = 0;
            while (usedSlots.Contains(nextSlot))
            {
                nextSlot++;
            }

            return nextSlot < maxSlots ? nextSlot : -1;
        }

        // ================================================================
        // UTILITY
        // ================================================================

        public static int GetTotalItemCount()
        {
            int count = 0;
            ItemBankData masterBank = LoadAPMasterBank();
            count += masterBank._heldItemStorage.Count;

            for (int i = 1; i <= NUM_BANKS; i++)
            {
                ItemBankData bank = LoadAPBank(i);
                count += bank._heldItemStorage.Count;
            }
            return count;
        }

        public static bool AreAPBanksInitialized()
        {
            if (!File.Exists(GetAPMasterBankPath()))
            {
                return false;
            }

            for (int i = 1; i <= NUM_BANKS; i++)
            {
                if (!File.Exists(GetAPBankPath(i)))
                {
                    return false;
                }
            }
            return true;
        }

        public static void ClearAllAPBanks()
        {
            try
            {
                ItemBankData emptyBank = new ItemBankData();
                SaveAPMasterBank(emptyBank);

                for (int i = 1; i <= NUM_BANKS; i++)
                {
                    SaveAPBank(i, emptyBank);
                }

                AtlyssArchipelagoPlugin.StaticLogger?.LogInfo("[AtlyssAP] Cleared all AP item banks (master + numbered)");
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError($"[AtlyssAP] Failed to clear AP banks: {ex.Message}");
            }
        }
    }
}