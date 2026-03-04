using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using System;
using UnityEngine;
using static AtlyssArchipelagoWIP.AtlyssArchipelagoPlugin;

namespace AtlyssArchipelagoWIP
{
    [HarmonyPatch(typeof(Player), "Player_OnDeath")]
    // Sends a DeathLink when the player dies, but only when DeathLink is on.
    // Also prevents incoming DeathLinks from sending another one out.
    class PlayerDeathPatch
    {
        static void Postfix(Player __instance)
        {
            AtlyssArchipelagoPlugin basePlugin = AtlyssArchipelagoPlugin.Instance;
            if (basePlugin._dlService == null || !basePlugin.cfgDeathlink.Value)
            {
                return;
            }
            if (reactingToDeathLink > 0) // we're dying because of a deathlink, don't send another one.
            {// for some random reason, Atlyss calls Player_OnDeath twice when the player dies.
                reactingToDeathLink--;
                return;
            }
            DeathLink dlToSend = new DeathLink(__instance._nickname, $"{__instance._nickname} was defeated.");
            basePlugin._dlService.SendDeathLink(dlToSend);
        }
    }

    [HarmonyPatch(typeof(ChatBehaviour), "Send_ChatMessage")]
    // Allows the sending of Archipelago commands in Atlyss chat to be forwarded to the Archipelago server.
    public static class ChatBehaviourPatch
    {
        static bool Prefix(ChatBehaviour __instance, string _message)
        {
            try
            {
                if (!string.IsNullOrEmpty(_message) && _message.StartsWith("/"))
                {
                    string[] apCommands = { "/release", "/collect", "/hint", "/help", "/players", "/status" };
                    bool isAPCommand = false;
                    foreach (string cmd in apCommands)
                    {
                        if (_message.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))
                        {
                            isAPCommand = true;
                            break;
                        }
                    }
                    if (isAPCommand)
                    {
                        if (AtlyssArchipelagoPlugin.Instance != null)
                        {
                            AtlyssArchipelagoPlugin.Instance.HandleArchipelagoCommand(_message);
                        }
                        __instance._focusedInChat = false;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                StaticLogger?.LogError($"[AtlyssAP] Chat patch error: {ex.Message}");
                return true;
            }
        }
    }

    // Angela "Rude!" achievement trigger patch
    // The actual mechanism: Angela bends over at bookshelf (misc01 anim), enabling
    // _specialHitbox which has a FriendlyNPC_Hitbox component. When the player's
    // weapon collider enters it, FriendlyNPC_Hitbox.OnTriggerEnter fires.
    // The _achievementTag on Angela's hitbox is "ATLYSS_ACHIEVEMENT_11".
    // We hook this to send the AP location check for "Rude!".
    [HarmonyPatch(typeof(FriendlyNPC_Hitbox), "OnTriggerEnter")]
    public static class AngelaRudePatch
    {
        static void Postfix(FriendlyNPC_Hitbox __instance)
        {
            try
            {
                if (AtlyssArchipelagoPlugin.Instance == null || !AtlyssArchipelagoPlugin.Instance.connected)
                    return;

                var tagField = AccessTools.Field(typeof(FriendlyNPC_Hitbox), "_achievementTag");
                if (tagField == null)
                    return;

                string tag = (string)tagField.GetValue(__instance);
                if (tag != "ATLYSS_ACHIEVEMENT_11")
                    return;

                StaticLogger?.LogInfo("[AtlyssAP] Angela 'Rude!' hitbox triggered!");
                AtlyssArchipelagoPlugin.Instance.SendCheckByName("Rude!");
                AtlyssArchipelagoPlugin.Instance.SendAPChatMessage(
                    "Found <color=yellow>Rude!</color>!"
                );
            }
            catch (Exception ex)
            {
                StaticLogger?.LogError($"[AtlyssAP] Angela patch error: {ex.Message}");
            }
        }
    }
}