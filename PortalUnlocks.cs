using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static AtlyssArchipelagoWIP.AtlyssArchipelagoPlugin;

namespace AtlyssArchipelagoWIP
{
    public class PortalUnlocks : MonoBehaviour
    {
        private AtlyssArchipelagoPlugin basePlugin;
        private struct PortalData // What the portal's scene data was before we overwrote it. Used to restore data later.
        {
            public string portalCaption; // equates to GameObject.Portal.ScenePortalData._portalCaptionTitle
            public string spawnID; // equates to GameObject.Portal.ScenePortalData._spawnPointTag
            public string sceneName; // equates to GameObject.Portal.ScenePortalData._subScene
        }
        private List<string> lockedScenes = new List<string>();
        private Dictionary<PortalData, PortalData> PortalDataToLockedData = new Dictionary<PortalData, PortalData>() // a list of every basegame portal, and what data it should have when locked.
        {   
            // Here's a formatting example:
          /*{new PortalData
                {                         // basegame portal data
                    portalCaption = ,     // the title that displays onscreen in vanilla
                    spawnID = ,           // the internal name of the spawnpoint the portal takes you to in vanilla
                    sceneName =           // the internal name of the scene the portal loads in vanilla
                },new PortalData          
                {                         // locked by AP portal data
                    portalCaption = ,     // the original location title, appended by "(Locked!)"
                    spawnID = ,           // left empty, so the player does not move
                    sceneName =           // the name of the currently loaded scene, to make sure the portal doesn't actually teleport the player
                }
            }*/
            {new PortalData // The portal in Sanctum that leads to Outer Sanctum
                {
                    portalCaption = "Outer Sanctum",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                },new PortalData
                {
                    portalCaption = "Outer Sanctum (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_sanctum.unity"
                }
            },
            {new PortalData // The portal in Sanctum that leads to the Sanctum PvP arena (note, will only be active in multiplayer servers)
                {
                    portalCaption = "Sanctum Arena",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_sanctumArena.unity"
                },new PortalData
                {
                    portalCaption = "Sanctum Arena (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_sanctum.unity"
                }
            },
            {new PortalData // The portal in Outer Sanctum that leads to Sanctum
                {
                    portalCaption = "Sanctum",
                    spawnID = "gatePoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_sanctum.unity"
                },new PortalData
                {
                    portalCaption = "Sanctum (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                }
            },
            {new PortalData // The portal in Outer Sanctum that leads to Effold Terrace
                {
                    portalCaption = "Effold Terrace",
                    spawnID = "startPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_effoldTerrace.unity"
                },new PortalData
                {
                    portalCaption = "Effold Terrace (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                }
            },
            {new PortalData // The portal in Outer Sanctum that leads to Arcwood Pass
                {
                    portalCaption = "Arcwood Pass",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity"
                },new PortalData
                {
                    portalCaption = "Arcwood Pass (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                }
            },
            {new PortalData // The portal in Outer Sanctum that leads to Tull Valley
                {
                    portalCaption = "Tull Valley",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulValley.unity"
                },new PortalData
                {
                    portalCaption = "Tull Valley (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                }
            },
            {new PortalData // The portal in Arcwood Pass that leads to Outer Sanctum
                {
                    portalCaption = "Outer Sanctum",
                    spawnID = "arcwoodSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                },new PortalData
                {
                    portalCaption = "Outer Sanctum (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity"
                }
            },
            {new PortalData // The portal in Arcwood Pass that leads to Crescent Road
                {
                    portalCaption = "Crescent Road",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity"
                },new PortalData
                {
                    portalCaption = "Crescent Road (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity"
                }
            },
            {new PortalData // The portal in Arcwood Pass that leads to the Executioner's Tomb (will only spawn in multiplayer)
                {
                    portalCaption = "Executioner's Tomb",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_catacombsArena.unity"
                },new PortalData
                {
                    portalCaption = "Executioner's Tomb (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity"
                }
            },
            {new PortalData // The portal in Arcwood Pass that leads to the Catacombs
                {
                    portalCaption = "Sanctum Catacombs",
                    spawnID = "", // this is left blank in vanilla so the game can spawn the player randomly in the dungeon
                    sceneName = "Assets/Scenes/map_dungeon00_sanctumCatacombs.unity"
                },new PortalData
                {
                    portalCaption = "Sanctum Catacombs (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity"
                }
            },
            {new PortalData // The portal in Crescent Road that leads to Arcwood Pass
                {
                    portalCaption = "Arcwood Pass",
                    spawnID = "keepSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity"
                },new PortalData
                {
                    portalCaption = "Arcwood Pass (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity"
                }
            },
            {new PortalData // The portal in Crescent Road that leads to Luvora Garden
                {
                    portalCaption = "Luvora Garden",
                    spawnID = "startPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_luvoraGarden.unity"
                },new PortalData
                {
                    portalCaption = "Luvora Garden (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity"
                }
            },
            {new PortalData // The portal in Crescent Road that leads to Crescent Keep
                {
                    portalCaption = "Crescent Keep",
                    spawnID = "startPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity"
                },new PortalData
                {
                    portalCaption = "Crescent Keep (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity"
                }
            },
            {new PortalData // The portal in Effold Terrace that leads to Outer Sanctum
                {
                    portalCaption = "Outer Sanctum",
                    spawnID = "terraceSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                },new PortalData
                {
                    portalCaption = "Outer Sanctum (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_effoldTerrace.unity"
                }
            },
            {new PortalData // The portal in Tull Valley that leads to Outer Sanctum
                {
                    portalCaption = "Outer Sanctum",
                    spawnID = "tullValleyPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_outerSanctum.unity"
                },new PortalData
                {
                    portalCaption = "Outer Sanctum (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulValley.unity"
                }
            },
            {new PortalData // The portal in Tull Valley that leads to the Tull Enclave
                {
                    portalCaption = "Tull Enclave",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulEnclave.unity"
                },new PortalData
                {
                    portalCaption = "Tull Enclave (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulValley.unity"
                }
            },
            {new PortalData // The portal in Tull Enclave that leads to Tull Valley
                {
                    portalCaption = "Tull Valley",
                    spawnID = "enclavePoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulValley.unity"
                },new PortalData
                {
                    portalCaption = "Tull Valley (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulEnclave.unity"
                }
            },
            {new PortalData // The portal in Tull Enclave that leads to Bularr Fortress
                {
                    portalCaption = "Bularr Fortress",
                    spawnID = "startPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_bularFortress.unity"
                },new PortalData
                {
                    portalCaption = "Bularr Fortress (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulEnclave.unity"
                }
            },
            {new PortalData // The portal in Bularr Fortress that leads to Tull Enclave
                {
                    portalCaption = "Tull Enclave",
                    spawnID = "fortSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_tuulEnclave.unity"
                },new PortalData
                {
                    portalCaption = "Tull Enclave (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_bularFortress.unity"
                }
            },
            {new PortalData // The portal in Luvora Garden that leads to Crescent Road
                {
                    portalCaption = "Crescent Road",
                    spawnID = "gardenPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity"
                },new PortalData
                {
                    portalCaption = "Crescent Road (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_luvoraGarden.unity"
                }
            },
            {new PortalData // The portal in Crescent Keep that leads to Crescent Road
                {
                    portalCaption = "Crescent Road",
                    spawnID = "keepSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentRoad.unity"
                },new PortalData
                {
                    portalCaption = "Crescent Road (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity"
                }
            },
            {new PortalData // The portal in Crescent Keep that leads to Gate of the Moon
                {
                    portalCaption = "Gate of the Moon",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity"
                },new PortalData
                {
                    portalCaption = "Gate of the Moon (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity"
                }
            },
            {new PortalData // The portal in Crescent Keep that leads to Crescent Grove
                {
                    portalCaption = "Crescent Grove",
                    spawnID = "startPoint",
                    sceneName = "Assets/Scenes/map_dungeon01_crescentGrove.unity"
                },new PortalData
                {
                    portalCaption = "Crescent Grove (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity"
                }
            },
            {new PortalData // The portal in Gate of the Moon that leads to Crescent Keep
                {
                    portalCaption = "Crescent Keep",
                    spawnID = "moonGateSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_crescentKeep.unity"
                },new PortalData
                {
                    portalCaption = "Crescent Keep (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity"
                }
            },
            {new PortalData // The portal in Gate of the Moon that leads to Wall of the Stars
                {
                    portalCaption = "Wall of the Stars",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_wallOfTheStars.unity"
                },new PortalData
                {
                    portalCaption = "Wall of the Stars (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity"
                }
            },
            {new PortalData // The portal in Gate of the Moon that leads to Redwoud
                {
                    portalCaption = "Redwoud",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/map_zone00_redwoud.unity"
                },new PortalData
                {
                    portalCaption = "Redwoud (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity"
                }
            },
            {new PortalData // The portal in Gate of the Moon that leads to Elwood Tree
                {
                    portalCaption = "Elwood Tree",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/map_zone00_elwoodTree.unity"
                },new PortalData
                {
                    portalCaption = "Elwood Tree (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity"
                }
            },
            {new PortalData // The portal in Wall of the Stars that leads to Gate of the Moon
                {
                    portalCaption = "Gate of the Moon",
                    spawnID = "starWallSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_gateOfTheMoon.unity"
                },new PortalData
                {
                    portalCaption = "Gate of the Moon (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_wallOfTheStars.unity"
                }
            },
            {new PortalData // The portal in Wall of the Stars that leads to the Trial of the Stars
                {
                    portalCaption = "Trial of the Stars",
                    spawnID = "spawnPoint",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_trialOfTheStars.unity"
                },new PortalData
                {
                    portalCaption = "Trial of the Stars (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_wallOfTheStars.unity"
                }
            },
            {new PortalData // The portal in the Trial of the Stars that leads to Wall of the Stars
                {           // Note that there are two of this portal. It is technically impossible to distinguish between the two with this method, but they both get locked anyway.
                    portalCaption = "Wall of the Stars",
                    spawnID = "trialSpawn",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_wallOfTheStars.unity"
                },new PortalData
                {
                    portalCaption = "Wall of the Stars (Locked!)",
                    spawnID = "",
                    sceneName = "Assets/Scenes/00_zone_forest/_zone00_trialOfTheStars.unity"
                }
            }
        };

        private void Awake()
        {
            basePlugin = AtlyssArchipelagoPlugin.Instance;
            SceneManager.sceneLoaded += OnSceneLoaded; // when a scene is loaded, run EnforcePortalLocks. due to limitations with c#, this has to be through a helper function
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m) // we don't actually care about loadscenemode, but it needs to be here
        {
            StartCoroutine(EnforcePortalLocks(s));
        }

        public void CheckProgressiveUnlock()
        {
            if (basePlugin.areaAccessOption != 2) return;
            if (basePlugin._catacombsPortalReceived && basePlugin._grovePortalReceived)
            {
                UnblockAccessToScene("Assets/Scenes/map_dungeon01_crescentGrove.unity");
                basePlugin.SendAPChatMessage("<color=cyan>Both portals found - Grove unlocked!</color>");
            }
            else if (basePlugin._grovePortalReceived && !basePlugin._catacombsPortalReceived)
            {
                basePlugin.SendAPChatMessage("Grove portal found, but need <color=yellow>Catacombs portal</color> first!");
            }
        }

        private IEnumerator EnforcePortalLocks(Scene newScene)
        {
            if (!basePlugin.connected || newScene.name == "map_dungeon00_sanctumCatacombs" || newScene.name == "map_dungeon01_crescentGrove")
            {
                yield break; // we're not connected, so we don't know what to lock.
                // >>> IDEA: Could possibly default to all areas locked to avoid breaking logic? <<<
            }
            yield return new WaitUntil(() => newScene.isLoaded); // wait for the scene to finish loading, just in case
            yield return new WaitForSecondsRealtime(2); // plus a little to avoid race conditions
            GameObject portalContainer = null;
            foreach (var o in newScene.GetRootGameObjects()) // get everything at scene root
            {
                if (o.name == "_PORTALS" || o.name == "_PORTAL") // some scenes have it plural, some don't. no idea why
                {
                    portalContainer = o;
                    StaticLogger.LogMessage($"[AtlyssAP] Portals for scene {newScene.name} located.");
                }
            }
            if (portalContainer == null) // if we searched everything and didn't find it...
            {
                StaticLogger.LogError($"[AtlyssAP] Portals for scene {newScene.name} were unable to be found!");
                yield break; // ... stop the coroutine and log the scene name.
            }
            for (int i = 0; i < portalContainer.transform.childCount; i++) // for each portal as a child of the _PORTALS holder...
            {
                Transform portal = portalContainer.transform.GetChild(i); // ...grab it...
                var portalData = portal.GetComponent<Portal>()._scenePortal; // ...fetch its scene data...
                if (lockedScenes.Contains(portalData._subScene)) // ...and check if it's one of the locked scenes
                {
                    PortalData dataOfPortalToLock = new PortalData // format the current portal's data into the PortalData format
                    {
                        portalCaption = portalData._portalCaptionTitle,
                        spawnID = portalData._spawnPointTag,
                        sceneName = portalData._subScene
                    };
                    if (PortalDataToLockedData.TryGetValue(dataOfPortalToLock, out PortalData lockedData)) // check the portal dictionary for it
                    {
                        portalData._portalCaptionTitle = lockedData.portalCaption; // then set all values accordingly
                        portalData._spawnPointTag = lockedData.spawnID;
                        portalData._subScene = lockedData.sceneName;
                    }
                    else // it wasn't in the dictionary for some reason.
                    {
                        StaticLogger.LogError($"[AtlyssAP] The portal to {portalData._portalCaptionTitle} wasn't found in the dictionary!");
                    }
                    
                }
            }
        }

        public void ApplyAreaAccessMode()
        {
            if (basePlugin.areaAccessOption == 1)
            {
                StaticLogger.LogInfo("[AtlyssAP] Area Access: Unlocked - Opening all areas");
                lockedScenes.Clear();
                basePlugin.SendAPChatMessage("<color=cyan>All areas unlocked!</color>");
            }
            else if (basePlugin.areaAccessOption == 0)
            {
                StaticLogger.LogInfo("[AtlyssAP] Area Access: Locked - Portals must be found");
                BlockAccessToScene("Assets/Scenes/map_dungeon00_sanctumCatacombs.unity");
                BlockAccessToScene("Assets/Scenes/map_dungeon01_crescentGrove.unity");
            }
            else if (basePlugin.areaAccessOption == 2)
            {
                StaticLogger.LogInfo("[AtlyssAP] Area Access: Progressive - Portals unlock sequentially");
                BlockAccessToScene("Assets/Scenes/map_dungeon00_sanctumCatacombs.unity");
                BlockAccessToScene("Assets/Scenes/map_dungeon01_crescentGrove.unity");
            }
        }
        public void BlockAccessToScene(string sceneName) // this must be the location of the scene in the files (ex: Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity)
        {
            lockedScenes.Add(sceneName);
            StaticLogger.LogInfo($"[AtlyssAP] {sceneName} has been locked by Archipelago");
        }

        public void UnblockAccessToScene(string sceneName) // this must be the location of the scene in the files (ex: Assets/Scenes/00_zone_forest/_zone00_arcwoodPass.unity)
        {
            lockedScenes.Remove(sceneName);
            StaticLogger.LogInfo($"[AtlyssAP] {sceneName} is no longer being locked by Archipelago");
        }
    }
}