using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // Connection logic: connecting, disconnecting, session ID management.
    public partial class AtlyssArchipelagoPlugin
    {
        private bool IsNewSession(string sessionId)
        {
            try
            {
                string sessionPath = Path.Combine(Application.persistentDataPath, SESSION_FILE);
                if (!File.Exists(sessionPath))
                    return true;

                string savedId = File.ReadAllText(sessionPath);
                return savedId != sessionId;
            }
            catch
            {
                return true;
            }
        }

        private void SaveSessionId(string sessionId)
        {
            try
            {
                string sessionPath = Path.Combine(Application.persistentDataPath, SESSION_FILE);
                File.WriteAllText(sessionPath, sessionId);
                Logger.LogInfo($"Saved session ID: {sessionId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save session ID: {ex.Message}");
            }
        }

        private void TryConnect()
        {
            if (connected)
            {
                Logger.LogWarning("[AtlyssAP] Already connected.");
                return;
            }
            if (connecting)
            {
                Logger.LogWarning("[AtlyssAP] Connection in progress...");
                return;
            }
            connecting = true;
            try
            {
                // FIX #3: Guard against null InputFields during early scene loading.
                // When auto-connect fires before the settings menu has been created,
                // apServer/apSlot are still null, causing NullReferenceException.
                if (apServer == null || apSlot == null)
                {
                    Logger.LogWarning("[AtlyssAP] UI not ready yet (InputFields are null). Retrying later...");
                    connecting = false;
                    return;
                }

                Logger.LogInfo("[AtlyssAP] === CONNECTING TO ARCHIPELAGO ===");
                Logger.LogInfo($"[AtlyssAP] Server: {apServer.text}");
                Logger.LogInfo($"[AtlyssAP] Slot: {apSlot.text}");
                _session = ArchipelagoSessionFactory.CreateSession(apServer.text);
                _dlService = _session.CreateDeathLinkService();
                Logger.LogInfo("[AtlyssAP] Session and DeathLink service created");
                string password = string.IsNullOrWhiteSpace(apPassword.text) ? null : apPassword.text;
                LoginResult login = _session.TryConnectAndLogin(
                    "ATLYSS",
                    apSlot.text,
                    ItemsHandlingFlags.AllItems,
                    tags: apDeathlink ? new[] { "DeathLink" } : Array.Empty<string>(),
                    password: password,
                    requestSlotData: true
                );
                if (!login.Successful)
                {
                    LoginFailure failure = login as LoginFailure;
                    if (failure != null && failure.Errors != null && failure.Errors.Length > 0)
                    {
                        Logger.LogError($"[AtlyssAP] Login failed: {string.Join(", ", failure.Errors)}");
                    }
                    else
                    {
                        Logger.LogError("[AtlyssAP] Login failed.");
                    }
                    Disconnect();
                    connecting = false;
                    return;
                }
                Logger.LogInfo("[AtlyssAP] Login successful!");
                cfgServer.Value = apServer.text; // update the config with the new values the player entered (in case they are different)
                cfgSlot.Value = apSlot.text;
                cfgPassword.Value = apPassword.text;

                try
                {
                    LoginSuccessful loginSuccess = login as LoginSuccessful;
                    if (loginSuccess != null && loginSuccess.SlotData != null)
                    {
                        slotData = loginSuccess.SlotData;
                        if (slotData.ContainsKey("goal"))
                        {
                            goalOption = Convert.ToInt32(slotData["goal"]);
                            string[] goalNames = { "Slime Diva", "Lord Zuulneruda", "Colossus", "Galius", "Lord Kaluuz", "Valdur", "All Bosses", "All Quests", "Level 32" };
                            Logger.LogInfo($"[AtlyssAP] Goal: {goalNames[goalOption]}");
                        }
                        if (slotData.ContainsKey("random_portals"))
                        {
                            randomPortalsEnabled = Convert.ToInt32(slotData["random_portals"]) == 1;
                            Logger.LogInfo($"[AtlyssAP] Portal Mode: {(randomPortalsEnabled ? "Random Portals" : "Progressive Portals")}");
                        }
                        // CHANGED: Updated equipment slot data reading for Gated/Random system.
                        // Was: equipmentProgressionOption with "Progressive" vs "Random" logging.
                        // Now: equipmentGatingOption with "Gated" vs "Random" logging.
                        // The slot data key "equipment_progression" stays the same (matches Python options.py).
                        // Value 0 = Random (equipment placed anywhere), Value 1 = Gated (equipment restricted by location tier).
                        // This is purely informational on the C# side - gating is enforced during seed generation in Python.
                        if (slotData.ContainsKey("equipment_progression"))
                        {
                            equipmentGatingOption = Convert.ToInt32(slotData["equipment_progression"]);
                            Logger.LogInfo($"[AtlyssAP] Equipment: {(equipmentGatingOption == 1 ? "Gated" : "Random")}");
                        }
                        if (slotData.ContainsKey("shop_sanity"))
                        {
                            shopSanityEnabled = Convert.ToInt32(slotData["shop_sanity"]) == 1;
                            Logger.LogInfo($"[AtlyssAP] Shop Sanity: {(shopSanityEnabled ? "Enabled" : "Disabled")}");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("[AtlyssAP] Login successful but slot data is null - using default options");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AtlyssAP] Could not read slot data: {ex.Message}");
                }

                SendAPChatMessage("<color=yellow>Connected to multiworld!</color>");

                string[] goalMessages = {
                    "Your goal: <color=red>Defeat Slime Diva</color>",
                    "Your goal: <color=red>Defeat Lord Zuulneruda</color>",
                    "Your goal: <color=red>Defeat Colossus</color>",
                    "Your goal: <color=red>Defeat Galius</color>",
                    "Your goal: <color=red>Defeat Lord Kaluuz</color>",
                    "Your goal: <color=red>Defeat Valdur</color>",
                    "Your goal: <color=red>Defeat All Bosses</color>",
                    "Your goal: <color=yellow>Complete All Quests</color>",
                    "Your goal: <color=#00FFFF>Reach Level 32</color>"
                };
                if (goalOption >= 0 && goalOption < goalMessages.Length)
                {
                    SendAPChatMessage(goalMessages[goalOption]);
                }

                portalLocker.ApplyAreaAccessMode();

                // NEW: Send location scouts if shop sanity is enabled (scouts all 50 locations)
                if (shopSanityEnabled)
                {
                    _shopSanity.SendLocationScouts(_session);
                }

                string newSessionId = $"{apServer.text}_{apSlot.text}_{_session.RoomState.Seed}";
                if (IsNewSession(newSessionId))
                {
                    Logger.LogInfo("[AtlyssAP] New AP session detected - clearing storage");
                    ArchipelagoSpikeStorage.ClearAPSession();
                    SaveSessionId(newSessionId);
                }
                currentSessionId = newSessionId;

                SpikePatch.InitializeAPStorage();

                try
                {
                    _session.Socket.SendPacket(new GetDataPackagePacket { Games = new[] { "ATLYSS" } });
                    Logger.LogInfo("[AtlyssAP] Data package requested.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AtlyssAP] Data package warning: {ex.Message}");
                }
                try
                {
                    _session.Socket.SendPacket(new SyncPacket());
                    Logger.LogInfo("[AtlyssAP] Sync packet sent.");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[AtlyssAP] Sync packet warning: {ex.Message}");
                }
                _session.Items.ItemReceived += OnItemReceived;
                _session.Socket.PacketReceived += OnPacketReceived;
                _dlService.OnDeathLinkReceived += OnDeathLinkReceived;
                _session.Locations.CheckedLocationsUpdated += (locations) =>
                {
                    Logger.LogInfo($"[AtlyssAP] Server confirmed {locations.Count} checked location(s)");
                };

                Player localPlayer = Player._mainPlayer;
                if (localPlayer != null)
                {
                    PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        _lastLevel = stats.Network_currentLevel;
                        Logger.LogInfo($"[AtlyssAP] Starting at level {_lastLevel}");

                        // NEW: Reset tracked profession levels on connection
                        _previousFishingLevel = 1;
                        _previousMiningLevel = 1;
                    }
                }
                connected = true;
                connecting = false;
                Logger.LogInfo("=== [AtlyssAP] Connected and ready! ===");
                Logger.LogInfo("[AtlyssAP] Automatic detection active - level-ups and quests will be tracked!");
                Logger.LogInfo("[AtlyssAP] Items will be sent to Spike storage!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Connection failed: {ex.Message}");
                Logger.LogError($"[AtlyssAP] Stack: {ex.StackTrace}");
                Disconnect();
                connecting = false;
                connected = false;
            }
        }

        private void Disconnect()
        {
            try
            {
                if (_session != null)
                {
                    try { _session.Items.ItemReceived -= OnItemReceived; } catch { }
                    try
                    {
                        if (_session.Socket != null)
                            _session.Socket.DisconnectAsync();
                    }
                    catch { }
                }
            }
            finally
            {
                _session = null;
                connected = false;
                connecting = false;
                _reportedChecks.Clear();
                _completedQuests.Clear();
                _lastLevel = 0;
                // NEW: Reset profession level tracking on disconnect
                _previousFishingLevel = 1;
                _previousMiningLevel = 1;
                _questDebugLogged = false;

                // UPDATED: Reset all 11 portal tracking states
                foreach (var key in _portalItemsReceived.Keys.ToList())
                {
                    _portalItemsReceived[key] = false;
                }

                // Reset progressive counters
                progressivePortalCount = 0;
                // REMOVED: progressiveEquipmentTier reset - Progressive Equipment no longer exists.
                // Equipment is now distributed via Gated/Random item_rules during Python seed generation.

                // NEW: Reset shop sanity state
                _shopSanity.Reset();

                Logger.LogInfo("[AtlyssAP] Disconnected.");
            }
        }
    }
}