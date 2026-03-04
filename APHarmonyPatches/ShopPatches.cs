using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // Shop Sanity patch - Injects AP items into merchant shops
    // Each merchant gets their own unique 5 AP items
    [HarmonyPatch(typeof(NetNPC), "Init_ShopkeepListing")]
    public static class ShopInventoryPatch
    {
        static void Postfix(NetNPC __instance)
        {
            try
            {
                // Only inject if connected and shop sanity is initialized
                if (!AtlyssArchipelagoPlugin.Instance.connected)
                    return;
                if (AtlyssArchipelagoPlugin.Instance._shopSanity == null)
                    return;
                if (!AtlyssArchipelagoPlugin.Instance._shopSanity.IsInitialized)
                    return;

                // FIXED: Use GameObject name to identify which merchant this is
                string npcName = __instance.gameObject.name;

                // Merchant NPC GameObjects that get AP items injected
                // TODO: Craig, Torta/fisher, and Mad Statue NPC names need to be confirmed
                string[] apMerchants = new string[]
                {
                    "_npc_Sally",
                    "_npc_Skrit",
                    "_npc_sallyWorker_frankie_01",
                    "_npc_Craig",                       // TODO: confirm NPC name
                    "_npc_dyeMerchant",
                    "_npc_Tesh",
                    "_npc_Nesh",
                    "_npc_Rikko",
                    "_npc_Cotoo",
                    "_npc_Ruka",
                    "_npc_fisher",                      // TODO: confirm — might be "_npc_Torta"
                    "_npc_madStatue",                   // TODO: confirm NPC name
                };

                bool isAPMerchant = false;
                foreach (string merchant in apMerchants)
                {
                    if (npcName == merchant)
                    {
                        isAPMerchant = true;
                        break;
                    }
                }

                if (!isAPMerchant)
                {
                    // Not one of the AP merchants - don't inject
                    return;
                }

                AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] AP merchant shop opened: {npcName} - injecting items");
                AtlyssArchipelagoPlugin.Instance._shopSanity.InjectAPShopItems(__instance);

                // FIXED: Force the ShopkeepManager to rebuild its display listing.
                // Without this, the shop UI was built from the original vendor items
                // before our AP items were injected. Items only appeared after buying
                // something (which triggered a re-render). Now we explicitly re-trigger
                // the listing rebuild so all AP items show immediately.
                try
                {
                    var shopManager = ShopkeepManager._current;
                    if (shopManager != null)
                    {
                        // Try the direct method first
                        var beginMethod = typeof(ShopkeepManager).GetMethod("Begin_ShopkeepListing",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (beginMethod != null)
                        {
                            // Check parameter count — might take the NPC as argument, or no args
                            var parameters = beginMethod.GetParameters();
                            if (parameters.Length == 1)
                            {
                                beginMethod.Invoke(shopManager, new object[] { __instance });
                                AtlyssArchipelagoPlugin.StaticLogger.LogInfo("[AtlyssAP] Refreshed shop display via Begin_ShopkeepListing(npc)");
                            }
                            else if (parameters.Length == 0)
                            {
                                beginMethod.Invoke(shopManager, null);
                                AtlyssArchipelagoPlugin.StaticLogger.LogInfo("[AtlyssAP] Refreshed shop display via Begin_ShopkeepListing()");
                            }
                        }
                        else
                        {
                            // Fallback: try other common method names
                            string[] refreshMethods = { "Refresh_ShopListing", "Init_ShopListing",
                                                        "Update_ShopListing", "Build_ShopListing",
                                                        "Begin_ShopListing", "Set_ShopListing" };
                            foreach (string methodName in refreshMethods)
                            {
                                var method = typeof(ShopkeepManager).GetMethod(methodName,
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (method != null)
                                {
                                    var p = method.GetParameters();
                                    if (p.Length == 1 && p[0].ParameterType == typeof(NetNPC))
                                    {
                                        method.Invoke(shopManager, new object[] { __instance });
                                        AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Refreshed shop display via {methodName}(npc)");
                                        break;
                                    }
                                    else if (p.Length == 0)
                                    {
                                        method.Invoke(shopManager, null);
                                        AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Refreshed shop display via {methodName}()");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception refreshEx)
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogWarning($"[AtlyssAP] Could not refresh shop display: {refreshEx.Message}");
                    // Non-fatal — items are still in vendor data, just might need a re-open to show
                }
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Shop patch error: {ex.Message}");
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Stack trace: {ex.StackTrace}");
            }
        }
    }

    // NEW: Patch shop purchase to intercept AP item purchases
    // When player buys an AP item, we send the location check immediately instead of giving them the dummy item
    [HarmonyPatch(typeof(ShopkeepManager), "Init_PurchaseItem")]
    public static class ShopPurchasePatch
    {
        static bool Prefix(ScriptableItem _scriptableItem, int _quantity, string _key, ShopListDataEntry _setEntry)
        {
            try
            {
                // Only intercept if connected and shop sanity is active
                if (!AtlyssArchipelagoPlugin.Instance.connected)
                    return true;
                if (AtlyssArchipelagoPlugin.Instance._shopSanity == null)
                    return true;
                if (!AtlyssArchipelagoPlugin.Instance._shopSanity.IsInitialized)
                    return true;

                // Check if this is an AP item purchase (name starts with "[AP]")
                string itemName = _setEntry._shopStruct._itemName;
                if (!itemName.StartsWith("[AP]"))
                    return true; // Not an AP item, run normal purchase logic

                AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Intercepting purchase of: {itemName}");

                // Get player components
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogError("[AtlyssAP] Player not found for purchase!");
                    return false;
                }

                PlayerInventory inventory = localPlayer.GetComponent<PlayerInventory>();
                if (inventory == null)
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogError("[AtlyssAP] PlayerInventory not found!");
                    return false;
                }

                // Check if player can afford it
                int price = _setEntry._shopStruct._dedicatedValue;
                if (_setEntry._shopStruct._useDedicatedValue)
                {
                    price = _setEntry._shopStruct._dedicatedValue;
                }
                else if (_scriptableItem != null)
                {
                    price = _scriptableItem._vendorCost;
                }

                if (inventory._heldCurrency < price)
                {
                    ErrorPromptTextManager.current.Init_ErrorPrompt("Not enough Crowns");
                    AtlyssArchipelagoPlugin.StaticLogger.LogInfo("[AtlyssAP] Player cannot afford AP item");
                    return false; // Block purchase
                }

                // Deduct currency
                inventory.Network_heldCurrency -= price;
                AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Deducted {price} crowns from player");

                // Play purchase sound
                inventory.Play_PurchaseSound();

                // Handle the AP purchase through shop sanity system
                AtlyssArchipelagoPlugin.Instance._shopSanity.HandleAPItemPurchase(itemName, _key);

                // Block normal purchase logic (return false prevents dummy item from being given)
                return false;
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Shop purchase patch error: {ex.Message}");
                return true; // On error, allow normal purchase
            }
        }
    }
}