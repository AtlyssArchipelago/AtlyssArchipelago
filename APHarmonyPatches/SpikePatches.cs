using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static AtlyssArchipelagoWIP.AtlyssArchipelagoPlugin;

namespace AtlyssArchipelagoWIP
{
    // SPIKE STORAGE PATCHES
    // Redirects the game's file I/O for item banks to AP-specific bank files.
    // UPDATED: Now checks IsAPSessionActive() in addition to connected, so
    // storage persists across game restarts without needing to reconnect first.


    public class SpikePatch
    {
        /// <summary>
        /// Returns true if file redirects should be active.
        /// Either currently connected OR a previous AP session's data exists.
        /// </summary>
        private static bool ShouldRedirect()
        {
            // If currently connected, always redirect
            if (AtlyssArchipelagoPlugin.Instance != null && AtlyssArchipelagoPlugin.Instance.connected)
                return true;

            // If a previous AP session exists (marker file), redirect so saved
            // items persist across game restarts before reconnecting
            if (ArchipelagoSpikeStorage.IsAPSessionActive())
                return true;

            return false;
        }

        [HarmonyPatch(typeof(File), nameof(File.ReadAllText), new Type[] { typeof(string) })]
        // Redirects Spike's bank loading code to load custom Archipelago item banks.
        // UPDATED: Uses ShouldRedirect() instead of just checking connected
        public static class File_ReadAllText_Patch
        {
            static bool Prefix(ref string path, ref string __result)
            {
                try
                {
                    if (!ShouldRedirect())
                    {
                        return true;
                    }
                    if (path.EndsWith("atl_itemBank") && !path.Contains("_ap"))
                    {
                        string apMasterPath = ArchipelagoSpikeStorage.GetAPMasterBankPath();
                        if (File.Exists(apMasterPath))
                        {
                            __result = File.ReadAllText(apMasterPath);
                            StaticLogger?.LogInfo("[AtlyssAP] Redirected Spike load: MASTER bank -> AP MASTER bank");
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
                                    StaticLogger?.LogInfo($"[AtlyssAP] Redirected Spike load: bank {i} -> AP bank {i}");
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
                    StaticLogger?.LogError($"[AtlyssAP] Error in File.ReadAllText patch: {ex.Message}");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(File), nameof(File.WriteAllText), new Type[] { typeof(string), typeof(string) })]
        // Redirects Spike's bank saving code to save to custom Archipelago item banks.
        // UPDATED: Uses ShouldRedirect() instead of just checking connected
        public static class File_WriteAllText_Patch
        {
            static bool Prefix(ref string path, string contents)
            {
                try
                {
                    if (!ShouldRedirect())
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
                        StaticLogger?.LogInfo("[AtlyssAP] Redirected Spike save: MASTER bank -> AP MASTER bank");
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
                                StaticLogger?.LogInfo($"[AtlyssAP] Redirected Spike save: bank {i} -> AP bank {i}");
                                return false;
                            }
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    StaticLogger?.LogError($"[AtlyssAP] Error in File.WriteAllText patch: {ex.Message}");
                    return true;
                }
            }
        }

        // NEW: Safety patch on Create_StorageEntry to prevent IndexOutOfRangeException.
        // If an item has a slot number that exceeds the game's UI entry array, skip it
        // instead of crashing. This handles any edge cases where stored data has bad slots.
        [HarmonyPatch(typeof(ItemStorageManager), "Create_StorageEntry")]
        public static class CreateStorageEntry_SafetyPatch
        {
            static bool Prefix(ItemStorageManager __instance, ItemData _itemData, ScriptableItem _scriptItem, int _index, int _slotNumber)
            {
                try
                {
                    // Get the storage entries array via reflection to check bounds
                    var entriesField = typeof(ItemStorageManager).GetField("_currentStorageEntries",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (entriesField != null)
                    {
                        var entries = entriesField.GetValue(__instance) as Array;
                        if (entries != null)
                        {
                            if (_slotNumber < 0 || _slotNumber >= entries.Length)
                            {
                                StaticLogger?.LogWarning(
                                    $"[AtlyssAP] Skipping storage entry: slot {_slotNumber} out of bounds " +
                                    $"(max {entries.Length}) for item '{_itemData?._itemName}'"
                                );
                                return false; // Skip this entry, don't crash
                            }
                        }
                    }

                    return true; // Proceed normally
                }
                catch (Exception ex)
                {
                    StaticLogger?.LogError($"[AtlyssAP] Create_StorageEntry safety check error: {ex.Message}");
                    return true; // On reflection failure, let original code run
                }
            }
        }

        public static void InitializeAPStorage()
        {
            try
            {
                // UPDATED: Set session marker so storage persists across restarts
                ArchipelagoSpikeStorage.SetAPSessionActive();

                if (!ArchipelagoSpikeStorage.AreAPBanksInitialized())
                {
                    ArchipelagoSpikeStorage.InitializeAPBanks();
                    StaticLogger?.LogInfo("[AtlyssAP] Initialized AP Spike storage (separate from vanilla)");
                }
                else
                {
                    int itemCount = ArchipelagoSpikeStorage.GetTotalItemCount();
                    StaticLogger?.LogInfo($"[AtlyssAP] AP Spike storage loaded ({itemCount} items stored)");
                }
            }
            catch (Exception ex)
            {
                StaticLogger?.LogError($"[AtlyssAP] Failed to initialize AP storage: {ex.Message}");
            }
        }
    }
}