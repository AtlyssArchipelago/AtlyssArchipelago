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

        [Serializable]
        public class ItemBankData
        {
            public List<ItemData> _heldItemStorage = new List<ItemData>();
        }

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
                return bank ?? new ItemBankData();
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
                return bank ?? new ItemBankData();
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

        public static bool AddItemToAPSpike(ItemData itemToAdd)
        {
            try
            {
                // CHANGED: Instead of filling the master bank first and only overflowing to
                // numbered banks when master is full, we now:
                // 1. Try to stack with existing items across ALL banks (if item is stackable)
                // 2. If not stackable or no stack space, find the first open slot across ALL banks
                // This distributes items more evenly and prevents the master tab from overflowing
                // while other tabs sit empty.

                bool isStackable = itemToAdd._maxQuantity > 1;

                // Step 1: Try stacking across all banks (master first, then 1-7)
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

                // Step 2: Find first open slot across all banks (master first, then 1-7)
                // Try master bank (100 slots)
                {
                    ItemBankData masterBank = LoadAPMasterBank();
                    int nextSlot = FindNextOpenSlot(masterBank, 100);
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

                // Try numbered banks (40 slots each)
                for (int bankNum = 1; bankNum <= NUM_BANKS; bankNum++)
                {
                    ItemBankData bank = LoadAPBank(bankNum);
                    int nextSlot = FindNextOpenSlot(bank, 40);
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

        // ADDED: Helper to find the next open slot in a bank, returns -1 if bank is full
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

        // NOTE: No longer called by AddItemToAPSpike (which now handles all banks itself),
        // but kept for potential external use. Fixed stackability check.
        private static bool TryAddToMasterBank(ItemData itemToAdd)
        {
            try
            {
                ItemBankData masterBank = LoadAPMasterBank();

                // FIXED: Only try stacking for items that are actually stackable.
                // Equipment has maxQuantity=1, so the old check (quantity < maxQuantity)
                // was 1 < 1 = false, which worked by accident. But for safety, we now
                // explicitly check maxQuantity > 1 to skip the stacking loop entirely
                // for non-stackable items like weapons and armor.
                bool isStackable = itemToAdd._maxQuantity > 1;
                if (isStackable)
                {
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
                }

                int nextSlot = 0;
                HashSet<int> usedSlots = new HashSet<int>();
                foreach (var item in masterBank._heldItemStorage)
                {
                    usedSlots.Add(item._slotNumber);
                }

                while (usedSlots.Contains(nextSlot))
                {
                    nextSlot++;
                }

                if (nextSlot < 100)
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

                return false;
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError(
                    $"[AtlyssAP] Failed to add to master bank: {ex.Message}"
                );
                return false;
            }
        }

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