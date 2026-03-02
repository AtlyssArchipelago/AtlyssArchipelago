using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // UPDATED: Patch GameManager.Locate_Item with HYBRID ICON SYSTEM
    // - Items from ATLYSS game -> Show real item's icon (Regen Vial, Iron Sword, etc.)
    // - Items from other Archipelago games -> Show custom Archipelago icon (loaded from embedded resource)
    // This gives visual clarity for ATLYSS items while showing a unified icon for other games
    // FIXED: Changed parameter name from "_itemName" to "_tag" to match actual GameManager.Locate_Item signature
    // FIXED: Use existing item as dummy instead of trying to create abstract class instance (ScriptableItem is abstract)
    [HarmonyPatch(typeof(GameManager), "Locate_Item")]
    public static class LocateItemPatch
    {
        // REMOVED: No longer using singleton _customAPItem since each item needs unique name
        // private static ScriptableItem _customAPItem = null;
        private static ScriptableItem _fallbackDummyItem = null;
        private static Sprite _customAPSprite = null;

        // NEW: Cache for AP item -> real item mapping to avoid repeated lookups
        private static Dictionary<string, ScriptableItem> _apItemCache = new Dictionary<string, ScriptableItem>();

        static void Postfix(string _tag, ref ScriptableItem __result)
        {
            try
            {
                // If the item was found normally, don't interfere
                if (__result != null)
                    return;

                // Check if this is an AP item
                if (string.IsNullOrEmpty(_tag) || !_tag.StartsWith("[AP]"))
                    return;

                // Check cache first
                if (_apItemCache.TryGetValue(_tag, out ScriptableItem cachedItem))
                {
                    __result = cachedItem;
                    return;
                }

                // Extract the actual item name from "[AP] ItemName (PlayerName)" format
                string itemName = ExtractItemName(_tag);

                if (string.IsNullOrEmpty(itemName))
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogWarning($"[AtlyssAP] Could not extract item name from: {_tag}");
                    // UPDATED: Pass fallback name for items with extraction issues
                    __result = GetCustomAPItem("Unknown Item");
                    _apItemCache[_tag] = __result;
                    return;
                }

                // Try to find the real ATLYSS item
                ScriptableItem realItem = FindRealItem(itemName);

                if (realItem != null)
                {
                    // Found ATLYSS item - use its real icon
                    _apItemCache[_tag] = realItem;
                    __result = realItem;
                    AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Using real item '{realItem._itemName}' icon for: {_tag}");
                }
                else
                {
                    // Not an ATLYSS item - use custom AP icon with the actual item name
                    // UPDATED: Pass actual item name so non-ATLYSS items show their real names (e.g., "Roll Fragment")
                    __result = GetCustomAPItem(itemName);
                    _apItemCache[_tag] = __result;
                    AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Using custom AP icon for non-ATLYSS item: {_tag}");
                }
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Locate_Item patch error: {ex.Message}");
                __result = GetCustomAPItem("Unknown Item");
            }
        }

        // NEW: Extract the item name from "[AP] ItemName (PlayerName)" format
        public static string ExtractItemName(string apItemName)
        {
            // Remove "[AP] " prefix
            string withoutPrefix = apItemName.Replace("[AP] ", "");

            // Find the opening parenthesis to get just the item name
            int parenIndex = withoutPrefix.LastIndexOf(" (");
            if (parenIndex > 0)
            {
                return withoutPrefix.Substring(0, parenIndex);
            }

            return withoutPrefix;
        }

        // NEW: Find the real ScriptableItem for a given item name (ATLYSS items only)
        private static ScriptableItem FindRealItem(string itemName)
        {
            // Check if this item exists in our ItemNameMapping
            if (!AtlyssArchipelagoPlugin.ItemNameMapping.TryGetValue(itemName, out string gameItemName))
            {
                // Not in our mapping - must be from another game
                return null;
            }

            // Parse the game item name to get the actual item name
            // Format examples:
            // "(lv-0) STATUSCONSUMABLE_Regen Vial"
            // "(lv-6) WEAPON_Iron Sword (Sword, Strength)"
            // "TRADEITEM_Dense Ingot"

            string actualItemName = gameItemName;

            // Extract name after last underscore
            int lastUnderscoreIndex = gameItemName.LastIndexOf('_');
            if (lastUnderscoreIndex >= 0 && lastUnderscoreIndex < gameItemName.Length - 1)
            {
                actualItemName = gameItemName.Substring(lastUnderscoreIndex + 1);
            }

            // Try direct lookup first
            ScriptableItem item = GameManager._current.Locate_Item(actualItemName);
            if (item != null)
                return item;

            // If name has parentheses like "Iron Sword (Sword, Strength)", try without them
            if (actualItemName.Contains(" (") && actualItemName.EndsWith(")"))
            {
                int parenIndex = actualItemName.LastIndexOf(" (");
                string nameWithoutParen = actualItemName.Substring(0, parenIndex);
                item = GameManager._current.Locate_Item(nameWithoutParen);
                if (item != null)
                    return item;
            }

            // Try without spaces
            string noSpaces = actualItemName.Replace(" ", "");
            item = GameManager._current.Locate_Item(noSpaces);
            if (item != null)
                return item;

            return null;
        }

        // NEW: Get or create custom AP ScriptableItem with custom Archipelago icon
        // This is used for items from other Archipelago games (not ATLYSS items)
        // FIXED: Use Object.Instantiate to clone base item instead of CreateInstance (ScriptableItem is abstract)
        // UPDATED: Accept item name parameter to display actual item names from other games
        // Each call creates a new instance with unique name - caching happens at _apItemCache level
        private static ScriptableItem GetCustomAPItem(string itemName = "Archipelago Item")
        {
            // UPDATED: No longer using singleton _customAPItem since each item needs unique name
            // The caching is handled at the _apItemCache level in Postfix()
            // This allows each non-ATLYSS item to display its actual name (e.g., "Roll Fragment", "Bonus Point")

            // Get a base item to clone
            ScriptableItem baseItem = GetFallbackDummy();

            if (baseItem != null)
            {
                // FIXED: Clone the base item using Instantiate instead of CreateInstance
                // ScriptableItem is abstract and cannot be instantiated with CreateInstance<T>()
                // Instantiate() clones an existing object which works even for abstract classes
                ScriptableItem customItem = UnityEngine.Object.Instantiate(baseItem);

                // UPDATED: Set the actual item name from other games instead of generic "Archipelago Item"
                // This makes items display as "Roll Fragment", "Bonus Point", "Category Sixes", etc.
                // instead of showing "Bunbag" (the base item) or "Archipelago Item" (generic fallback)
                customItem._itemName = itemName;

                // Load and apply custom icon
                Sprite customSprite = LoadCustomAPSprite();
                if (customSprite != null)
                {
                    // Try to set the icon using reflection since we don't know the exact field name
                    TrySetItemIcon(customItem, customSprite);

                    // Only log success message on first sprite load (when _customAPSprite was null)
                    // This avoids spamming the log with "Loaded custom Archipelago icon successfully!" for every item
                    if (_customAPSprite == null)
                    {
                        AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Loaded custom Archipelago icon successfully!");
                    }
                    _customAPSprite = customSprite;
                }
                else
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogWarning($"[AtlyssAP] Failed to load custom icon, using base item icon");
                }

                return customItem;
            }
            else
            {
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Could not create custom AP item - no base item available");
                return null;
            }
        }

        // NEW: Load custom Archipelago sprite from embedded resource
        private static Sprite LoadCustomAPSprite()
        {
            if (_customAPSprite != null)
                return _customAPSprite;

            try
            {
                // Get the assembly where the icon is embedded
                Assembly assembly = Assembly.GetExecutingAssembly();

                // Try to find the resource (case-sensitive)
                string[] resourceNames = assembly.GetManifestResourceNames();
                string foundResource = null;

                foreach (string name in resourceNames)
                {
                    if (name.EndsWith("Archipelago.png-256x256_q95.png"))
                    {
                        foundResource = name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(foundResource))
                {
                    AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Could not find Archipelago.png-256x256_q95.png in embedded resources");
                    AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Available resources: {string.Join(", ", resourceNames)}");
                    return null;
                }

                // Load the image bytes from embedded resource
                using (Stream stream = assembly.GetManifestResourceStream(foundResource))
                {
                    if (stream == null)
                    {
                        AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Failed to open embedded resource stream");
                        return null;
                    }

                    byte[] imageData = new byte[stream.Length];
                    stream.Read(imageData, 0, imageData.Length);

                    // Create texture from image data
                    Texture2D texture = new Texture2D(2, 2); // Size will be overwritten by LoadImage
                    if (!texture.LoadImage(imageData))
                    {
                        AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Failed to load image data into texture");
                        return null;
                    }

                    // Create sprite from texture
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), // Pivot at center
                        100.0f // Pixels per unit
                    );

                    AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Successfully loaded custom AP sprite: {texture.width}x{texture.height}");
                    return sprite;
                }
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Error loading custom sprite: {ex.Message}");
                return null;
            }
        }

        // NEW: Try to set the item icon using reflection (since we don't know the exact field name)
        private static void TrySetItemIcon(ScriptableItem item, Sprite sprite)
        {
            try
            {
                // Common field names for item icons in Unity games
                string[] possibleFieldNames = { "_icon", "_itemIcon", "_sprite", "_itemSprite", "icon", "sprite" };

                Type itemType = item.GetType();

                foreach (string fieldName in possibleFieldNames)
                {
                    FieldInfo field = itemType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(Sprite))
                    {
                        field.SetValue(item, sprite);
                        AtlyssArchipelagoPlugin.StaticLogger.LogInfo($"[AtlyssAP] Set item icon using field: {fieldName}");
                        return;
                    }
                }

                AtlyssArchipelagoPlugin.StaticLogger.LogWarning($"[AtlyssAP] Could not find icon field on ScriptableItem");
            }
            catch (Exception ex)
            {
                AtlyssArchipelagoPlugin.StaticLogger.LogError($"[AtlyssAP] Error setting item icon: {ex.Message}");
            }
        }

        // Get fallback dummy item (Bunbag) for when custom icon isn't loaded yet
        private static ScriptableItem GetFallbackDummy()
        {
            if (_fallbackDummyItem == null)
            {
                _fallbackDummyItem = GameManager._current.Locate_Item("Bunbag");

                if (_fallbackDummyItem == null)
                    _fallbackDummyItem = GameManager._current.Locate_Item("Wood Sword");

                if (_fallbackDummyItem == null)
                    _fallbackDummyItem = GameManager._current.Locate_Item("Leather Top");
            }

            return _fallbackDummyItem;
        }
    }
}