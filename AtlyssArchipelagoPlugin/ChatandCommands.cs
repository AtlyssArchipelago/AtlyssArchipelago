using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using BepInEx;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // Chat, commands, and DeathLink: sending in-game messages, handling /commands, processing server packets, death events.
    public partial class AtlyssArchipelagoPlugin
    {
        public void SendAPChatMessage(string message)
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                ChatBehaviour chat = localPlayer._chatBehaviour;
                // FIX #4: Check chat is not null BEFORE calling SetValue on it.
                // Previously maxOnscreenMessages.SetValue(chat, 50) ran before the null check,
                // causing NullReferenceException when chat wasn't ready during early item receives.
                if (chat == null) return;
                maxOnscreenMessages.SetValue(chat, 50);

                chat.Init_GameLogicMessage(
                    $"<color=#00ff00>[Archipelago]</color> {message}"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to send chat message: {ex.Message}");
            }
        }

        public void HandleArchipelagoCommand(string message)
        {
            if (!connected || _session == null)
            {
                SendAPChatMessage("<color=red>Not connected to Archipelago!</color>");
                return;
            }

            string command = message.TrimStart('/').Trim();
            string[] parts = command.Split(new[] { ' ' }, 2);
            string cmd = parts[0].ToLower();
            string args = parts.Length > 1 ? parts[1] : "";
            Logger.LogInfo($"[AtlyssAP] Command received: {cmd} {args}");
            switch (cmd)
            {
                case "release":
                    HandleReleaseCommand();
                    break;
                case "collect":
                    HandleCollectCommand();
                    break;
                case "hint":
                    HandleHintCommand(args);
                    break;
                case "help":
                    HandleHelpCommand();
                    break;
                case "players":
                    HandlePlayersCommand();
                    break;
                case "status":
                    HandleStatusCommand();
                    break;
                default:
                    try
                    {
                        _session.Socket.SendPacket(new SayPacket { Text = message });
                        Logger.LogInfo($"[AtlyssAP] Sent command to server: {message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[AtlyssAP] Failed to send command: {ex.Message}");
                        SendAPChatMessage($"<color=red>Unknown command: /{cmd}</color>");
                    }
                    break;
            }
        }

        private void HandleReleaseCommand()
        {
            try
            {
                _session.Say("!release");
                SendAPChatMessage("<color=yellow>Release requested!</color>");
                Logger.LogInfo("[AtlyssAP] Release command executed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to execute release: {ex.Message}");
                SendAPChatMessage("<color=red>Failed to execute release</color>");
            }
        }

        private void HandleCollectCommand()
        {
            try
            {
                _session.Say("!collect");
                SendAPChatMessage("<color=yellow>Collect requested!</color>");
                Logger.LogInfo("[AtlyssAP] Collect command executed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to execute collect: {ex.Message}");
                SendAPChatMessage("<color=red>Failed to execute collect</color>");
            }
        }

        private void HandleHintCommand(string args)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    SendAPChatMessage("<color=yellow>Usage: /hint [item name]</color>");
                    return;
                }

                SendAPChatMessage($"<color=yellow>Requesting hint for: {args}</color>");
                _session.Say($"!hint {args}");
                Logger.LogInfo($"[AtlyssAP] Hint requested for: {args}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to request hint: {ex.Message}");
            }
        }

        private void HandleHelpCommand()
        {
            SendAPChatMessage("<color=yellow>Archipelago Commands:</color>");
            SendAPChatMessage("/release - Release remaining items");
            SendAPChatMessage("/collect - Collect items from others");
            SendAPChatMessage("/hint [item] - Request hint");
            SendAPChatMessage("/players - List connected players");
            SendAPChatMessage("/status - Show completion status");
            SendAPChatMessage("/help - Show this message");
        }

        private void HandlePlayersCommand()
        {
            try
            {
                var players = _session.Players.AllPlayers;
                int playerCount = 0;
                foreach (var p in players)
                {
                    playerCount++;
                }
                SendAPChatMessage($"<color=yellow>Connected Players ({playerCount}):</color>");
                foreach (var player in players)
                {
                    string name = player.Name ?? $"Player {player.Slot}";
                    string game = player.Game ?? "Unknown";
                    SendAPChatMessage($"- {name} ({game})");
                }
                Logger.LogInfo($"[AtlyssAP] Listed {playerCount} players");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to list players: {ex.Message}");
                SendAPChatMessage("<color=red>Failed to get player list</color>");
            }
        }

        private void HandleStatusCommand()
        {
            try
            {
                int checkedCount = _reportedChecks.Count;
                int totalCount = AllLocationNameToId.Count;
                int totalQuests = AllQuestToLocation.Count;
                float percentage = totalCount > 0 ? (float)checkedCount / totalCount * 100f : 0f;
                SendAPChatMessage($"<color=yellow>Progress: {checkedCount}/{totalCount} ({percentage:F1}%)</color>");
                SendAPChatMessage($"Level milestones: {_lastLevel}/32");
                SendAPChatMessage($"Quest completions: {_completedQuests.Count}/{totalQuests}");
                Logger.LogInfo($"[AtlyssAP] Status: {checkedCount}/{totalCount} locations checked");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to show status: {ex.Message}");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            if (packet is PrintJsonPacket && !(packet is ItemPrintJsonPacket) && !(packet is HintPrintJsonPacket))
            {
                SendAPChatMessage($"{packet.ToJObject().SelectToken("data[0].text")?.Value<string>()}");
            }
            else if (packet is HintPrintJsonPacket)
            {
                string combinedMessage;
                if (packet.ToJObject()["receiving"].ToObject<int>() == _session.Players.ActivePlayer.Slot) // if the receiving player is us...
                {
                    var NetItem = packet.ToJObject()["item"].ToObject<NetworkItem>(); // Get the item data from the hint
                    var playerSending = _session.Players.GetPlayerInfo(NetItem.Player).Alias; // Find the finding player's slot alias
                    var itemReceiving = _session.Items.GetItemName(NetItem.Item); // Get the item's name
                    var findingLocation = _session.Locations.GetLocationNameFromId(NetItem.Location, _session.Players.GetPlayerInfo(NetItem.Player).Game); // Get the location's name
                    combinedMessage = $"Your <color=yellow>{itemReceiving}</color> is at <color=yellow>{findingLocation}</color> in <color=#00FFFF>{playerSending}'s</color> world.";
                    SendAPChatMessage(combinedMessage);
                }
                else if (packet.ToJObject()["item"].ToObject<NetworkItem>().Player == _session.Players.ActivePlayer.Slot) // if the sending player is us...
                {
                    var NetItem = packet.ToJObject()["item"].ToObject<NetworkItem>(); // Get the item data
                    var playerReceiving = _session.Players.GetPlayerInfo(NetItem.Player).Alias; // Find the receiving player's slot alias
                    var itemSending = _session.Items.GetItemName(NetItem.Item, _session.Players.GetPlayerInfo(NetItem.Player).Game); // Get the item's name
                    var findingLocation = _session.Locations.GetLocationNameFromId(NetItem.Location); // Get the location's name
                    combinedMessage = $"<color=#00FFFF>{playerReceiving}'s</color> <color=yellow>{itemSending}</color> is at your <color=yellow>{findingLocation}</color>";
                    SendAPChatMessage(combinedMessage);
                }
                else // ap should prevent this, but it's better to be prepared
                {
                    Logger.LogWarning($"[AtlyssAP] Received a hint for a different player. Ignoring.");
                }
            }
        }

        // -- DeathLink --

        private void OnDeathLinkReceived(DeathLink dl)
        {
            reactingToDeathLink = 2;
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null)
                {
                    Logger.LogWarning("[AtlyssAP] Player not found for DeathLink! Possibly on Main Menu?");
                    reactingToDeathLink = 0;
                    return;
                }
                localPlayer._playerZoneType = ZoneType.Field; // if this is set to `Safe`, as it is in Sanctum for example, the player cannot die from anything.
                localPlayer._statusEntity.Subtract_Health(10000);
                Logger.LogMessage("[AtlyssAP] DeathLink Received!");
                string DeathLinkMessage;
                if (dl.Cause.IsNullOrWhiteSpace())
                {
                    DeathLinkMessage = $"Killed by {dl.Source} on Archipelago.";
                }
                else
                {
                    DeathLinkMessage = $"{dl.Cause}.";
                }
                try
                {
                    GameObject.Find("_GameUI_InGame").GetComponent<ErrorPromptTextManager>().Init_ErrorPrompt(DeathLinkMessage); // this is that large red text in the top middle of the screen
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[AtlyssAP] Failed to display DeathLink message: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to process DeathLink: {ex.Message}");
                reactingToDeathLink = 0;
            }
        }

        public void ToggleDeathLink(bool enabled)
        {
            if (_session == null || _dlService == null || !_session.Socket.Connected)
            {
                return;
            }
            if (enabled)
            {
                _dlService.EnableDeathLink();
            }
            else
            {
                _dlService.DisableDeathLink();
            }
        }
    }
}