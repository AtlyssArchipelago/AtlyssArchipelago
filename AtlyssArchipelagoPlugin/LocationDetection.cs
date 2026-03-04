using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtlyssArchipelagoWIP
{
    // Location detection: polling for level-ups, quest completions, skill levels, and sending checks.
    public partial class AtlyssArchipelagoPlugin
    {
        private void PollForLevelChanges()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                PlayerStats stats = localPlayer.GetComponent<PlayerStats>();
                if (stats == null) return;
                int currentLevel = stats.Network_currentLevel;

                if (currentLevel != _lastLevel)
                {
                    Logger.LogInfo($"[AtlyssAP] Level changed: {_lastLevel} -> {currentLevel}");

                    if (currentLevel >= 2 && currentLevel <= 32 && currentLevel % 2 == 0)
                    {
                        string locationName = $"Reach Level {currentLevel}";
                        SendCheckByName(locationName);

                        SendAPChatMessage(
                            $"Found <color=yellow>{locationName}</color>! " +
                            $"Sent item to another player!"
                        );
                        Logger.LogInfo($"[AtlyssAP] {locationName} milestone!");
                    }
                    _lastLevel = currentLevel;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error polling level: {ex.Message}");
            }
        }

        // Poll for fishing and mining level changes
        // Partner's locations start at Lv. 1: "Fishing Lv. 1" through "Fishing Lv. 10"
        private void PollForSkillLevelChanges()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;

                // Get PlayerStats component to access profession data
                PlayerStats playerStats = localPlayer._pStats;
                if (playerStats == null) return;

                // Search through professions SyncList to find fishing and mining by name
                for (int i = 0; i < playerStats._syncProfessions.Count; i++)
                {
                    ProfessionStruct profession = playerStats._syncProfessions[i];

                    // Get the ScriptableProfession to access the profession name
                    ScriptableProfession scriptableProfession = null;
                    if (GameManager._current != null && GameManager._current._statLogics != null)
                    {
                        if (i < GameManager._current._statLogics._scriptableProfessions.Length)
                        {
                            scriptableProfession = GameManager._current._statLogics._scriptableProfessions[i];
                        }
                    }

                    if (scriptableProfession == null)
                        continue;

                    // Check if this is the Fishing profession
                    if (scriptableProfession._professionName == "Fishing")
                    {
                        int currentFishingLevel = profession._professionLvl;
                        if (currentFishingLevel > _previousFishingLevel)
                        {
                            for (int level = _previousFishingLevel + 1; level <= currentFishingLevel; level++)
                            {
                                string locationName = $"Fishing Lv. {level}";
                                SendCheckByName(locationName);
                                SendAPChatMessage(
                                    $"Found <color=yellow>{locationName}</color>! " +
                                    $"Sent item to another player!"
                                );
                                Logger.LogInfo($"[AtlyssAP] {locationName} reached!");
                            }
                            _previousFishingLevel = currentFishingLevel;
                        }
                    }

                    // Check if this is the Mining profession
                    if (scriptableProfession._professionName == "Mining")
                    {
                        int currentMiningLevel = profession._professionLvl;
                        if (currentMiningLevel > _previousMiningLevel)
                        {
                            for (int level = _previousMiningLevel + 1; level <= currentMiningLevel; level++)
                            {
                                string locationName = $"Mining Lv. {level}";
                                SendCheckByName(locationName);
                                SendAPChatMessage(
                                    $"Found <color=yellow>{locationName}</color>! " +
                                    $"Sent item to another player!"
                                );
                                Logger.LogInfo($"[AtlyssAP] {locationName} reached!");
                            }
                            _previousMiningLevel = currentMiningLevel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error checking skill levels: {ex.Message}");
            }
        }

        private void PollForQuestCompletions()
        {
            try
            {
                Player localPlayer = Player._mainPlayer;
                if (localPlayer == null) return;
                PlayerQuesting questing = localPlayer.GetComponent<PlayerQuesting>();
                if (questing == null)
                {
                    return;
                }
                if (questing._finishedQuests == null)
                {
                    return;
                }

                if (!_questDebugLogged && questing._finishedQuests.Count > 0)
                {
                    Logger.LogInfo($"[AtlyssAP] DEBUG: Found {questing._finishedQuests.Count} completed quests");
                    foreach (var quest in questing._finishedQuests.Keys)
                    {
                        Logger.LogInfo($"[AtlyssAP] DEBUG: Completed quest: '{quest}'");
                    }
                    _questDebugLogged = true;
                }

                foreach (var kvp in AllQuestToLocation)
                {
                    string questName = kvp.Key;
                    long locationId = kvp.Value;

                    if (questing._finishedQuests.ContainsKey(questName) && !_completedQuests.Contains(questName))
                    {
                        SendCheckById(locationId);
                        _completedQuests.Add(questName);

                        SendAPChatMessage(
                            $"Found <color=yellow>{questName}</color>! " +
                            $"Sent item to another player!"
                        );
                        Logger.LogInfo($"[AtlyssAP] Quest completed: {questName}");

                        // Victory detection based on goal option from slot data
                        // AP handles completion server-side, this is just a cosmetic message
                        string[] goalBossQuests = {
                            "Gatling Galius",       // goal 0-5 individual bosses have various quests
                            "The Gall of Galius"    // goal 3 (Galius) and goal 6 (all bosses)
                        };
                        // Show a generic victory hint when major boss quests complete
                        if (questName == "The Gall of Galius" || questName == "Gatling Galius")
                        {
                            SendAPChatMessage(
                                $"<color=gold>VICTORY!</color> " +
                                $"<color=yellow>You may have completed your goal!</color>"
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error polling quests: {ex.Message}");
            }
        }

        // Changed from private to public so HarmonyPatches can send checks (e.g. Angela trigger)
        public void SendCheckById(long locationId)
        {
            if (!connected || _session == null)
            {
                Logger.LogError("[AtlyssAP] Not connected; cannot send check.");
                return;
            }
            if (_reportedChecks.Contains(locationId))
            {
                return;
            }
            try
            {
                if (_session != null && _session.Socket != null && _session.Socket.Connected)
                {
                    _session.Locations.CompleteLocationChecks(locationId);
                    _reportedChecks.Add(locationId);
                    Logger.LogInfo($"[AtlyssAP] Sent check ID: {locationId}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Failed to send check: {ex.Message}");
            }
        }

        // Sends a location check by name using the AllLocationNameToId dictionary from DataTables
        public void SendCheckByName(string locationName)
        {
            if (AllLocationNameToId.TryGetValue(locationName, out long locationId))
            {
                SendCheckById(locationId);
            }
            else
            {
                Logger.LogWarning($"[AtlyssAP] Location not found in DataTables: '{locationName}'");
            }
        }
    }
}