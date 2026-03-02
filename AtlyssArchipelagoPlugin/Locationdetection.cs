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
                        long locationId = REACH_LEVEL_2 + ((currentLevel - 2) / 2);
                        if (!_reportedChecks.Contains(locationId))
                        {
                            SendCheckById(locationId);

                            SendAPChatMessage(
                                $"Found <color=yellow>Reach Level {currentLevel}</color>! " +
                                $"Sent item to another player!"
                            );
                            Logger.LogInfo($"[AtlyssAP] Reached Level {currentLevel} milestone!");
                        }
                    }
                    _lastLevel = currentLevel;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[AtlyssAP] Error polling level: {ex.Message}");
            }
        }

        // NEW: Poll for fishing and mining level changes - Added to track profession skill levels
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
                            // Send checks for all levels between previous and current
                            for (int level = _previousFishingLevel + 1; level <= currentFishingLevel; level++)
                            {
                                if (FishingLevelLocations.TryGetValue(level, out long locationId))
                                {
                                    SendCheckById(locationId);
                                    SendAPChatMessage(
                                        $"Found <color=yellow>Fishing Level {level}</color>! " +
                                        $"Sent item to another player!"
                                    );
                                    Logger.LogInfo($"[AtlyssAP] Fishing level {level} reached!");
                                }
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
                            // Send checks for all levels between previous and current
                            for (int level = _previousMiningLevel + 1; level <= currentMiningLevel; level++)
                            {
                                if (MiningLevelLocations.TryGetValue(level, out long locationId))
                                {
                                    SendCheckById(locationId);
                                    SendAPChatMessage(
                                        $"Found <color=yellow>Mining Level {level}</color>! " +
                                        $"Sent item to another player!"
                                    );
                                    Logger.LogInfo($"[AtlyssAP] Mining level {level} reached!");
                                }
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
                        if (questName == "Gatling Galius")
                        {
                            SendAPChatMessage(
                                $"<color=gold>VICTORY!</color> " +
                                $"<color=yellow>You completed your goal!</color>"
                            );
                            Logger.LogInfo($"[AtlyssAP] VICTORY! Defeated final boss Galius!");
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
    }
}