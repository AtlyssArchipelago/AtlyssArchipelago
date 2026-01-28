using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
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
            
            string saveDirectory = UnityEngine.Application.persistentDataPath;
            return Path.Combine(saveDirectory, $"atl_itemBank_ap_{bankNumber:D2}");
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
                if (TryAddToMasterBank(itemToAdd))
                {
                    return true;
                }
                
                for (int bankNum = 1; bankNum <= NUM_BANKS; bankNum++)
                {
                    ItemBankData bank = LoadAPBank(bankNum);
                    
                    if (itemToAdd._quantity < itemToAdd._maxQuantity)
                    {
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
                    
                    int nextSlot = 0;
                    HashSet<int> usedSlots = new HashSet<int>();
                    foreach (var item in bank._heldItemStorage)
                    {
                        usedSlots.Add(item._slotNumber);
                    }
                    
                    while (usedSlots.Contains(nextSlot))
                    {
                        nextSlot++;
                    }
                    
                    if (nextSlot < 40)
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

        private static bool TryAddToMasterBank(ItemData itemToAdd)
        {
            try
            {
                ItemBankData masterBank = LoadAPMasterBank();
                
                if (itemToAdd._quantity < itemToAdd._maxQuantity)
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

    public class SpikePatch
    {
        [HarmonyPatch(typeof(File), nameof(File.ReadAllText), new Type[] { typeof(string) })]
        public static class File_ReadAllText_Patch
        {
            static bool Prefix(ref string path, ref string __result)
            {
                try
                {
                    if (AtlyssArchipelagoPlugin.Instance == null || !AtlyssArchipelagoPlugin.Instance.connected)
                    {
                        return true;
                    }

                    if (path.EndsWith("atl_itemBank") && !path.Contains("_ap"))
                    {
                        string apMasterPath = ArchipelagoSpikeStorage.GetAPMasterBankPath();
                        
                        if (File.Exists(apMasterPath))
                        {
                            __result = File.ReadAllText(apMasterPath);
                            AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                                "[AtlyssAP] Redirected Spike load: MASTER bank -> AP MASTER bank"
                            );
                            return false;
                        }
                        else
                        {
                            __result = "{ \"_heldItemStorage\": [] }";
                            return false;
                        }
                    }

                    if (path.Contains("atl_itemBank_") && !path.Contains("_ap_"))
                    {
                        string fileName = Path.GetFileName(path);
                        
                        for (int i = 1; i <= 7; i++)
                        {
                            if (fileName == $"atl_itemBank_{i:D2}")
                            {
                                string apPath = ArchipelagoSpikeStorage.GetAPBankPath(i);
                                
                                if (File.Exists(apPath))
                                {
                                    __result = File.ReadAllText(apPath);
                                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                                        $"[AtlyssAP] Redirected Spike load: bank {i} -> AP bank {i}"
                                    );
                                    return false;
                                }
                                else
                                {
                                    __result = "{ \"_heldItemStorage\": [] }";
                                    return false;
                                }
                            }
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    AtlyssArchipelagoPlugin.StaticLogger?.LogError(
                        $"[AtlyssAP] Error in File.ReadAllText patch: {ex.Message}"
                    );
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(File), nameof(File.WriteAllText), new Type[] { typeof(string), typeof(string) })]
        public static class File_WriteAllText_Patch
        {
            static bool Prefix(ref string path, string contents)
            {
                try
                {
                    if (AtlyssArchipelagoPlugin.Instance == null || !AtlyssArchipelagoPlugin.Instance.connected)
                    {
                        return true;
                    }

                    if (path.EndsWith("atl_itemBank") && !path.Contains("_ap"))
                    {
                        string apMasterPath = ArchipelagoSpikeStorage.GetAPMasterBankPath();
                        string dir = Path.GetDirectoryName(apMasterPath);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        
                        File.WriteAllText(apMasterPath, contents);
                        
                        AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                            "[AtlyssAP] Redirected Spike save: MASTER bank -> AP MASTER bank"
                        );
                        
                        return false;
                    }

                    if (path.Contains("atl_itemBank_") && !path.Contains("_ap_"))
                    {
                        string fileName = Path.GetFileName(path);
                        
                        for (int i = 1; i <= 7; i++)
                        {
                            if (fileName == $"atl_itemBank_{i:D2}")
                            {
                                string apPath = ArchipelagoSpikeStorage.GetAPBankPath(i);
                                File.WriteAllText(apPath, contents);
                                
                                AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                                    $"[AtlyssAP] Redirected Spike save: bank {i} -> AP bank {i}"
                                );
                                
                                return false;
                            }
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    AtlyssArchipelagoPlugin.StaticLogger?.LogError(
                        $"[AtlyssAP] Error in File.WriteAllText patch: {ex.Message}"
                    );
                    return true;
                }
            }
        }

        public static void InitializeAPStorage()
        {
            try
            {
                if (!ArchipelagoSpikeStorage.AreAPBanksInitialized())
                {
                    ArchipelagoSpikeStorage.InitializeAPBanks();
                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                        "[AtlyssAP] Initialized AP Spike storage (separate from vanilla)"
                    );
                }
                else
                {
                    int itemCount = ArchipelagoSpikeStorage.GetTotalItemCount();
                    AtlyssArchipelagoPlugin.StaticLogger?.LogInfo(
                        $"[AtlyssAP] AP Spike storage loaded ({itemCount} items stored)"
                    );
                }
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger?.LogError(
                    $"[AtlyssAP] Failed to initialize AP storage: {ex.Message}"
                );
            }
        }
    }
}
