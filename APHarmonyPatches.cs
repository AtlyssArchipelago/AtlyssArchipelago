using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AtlyssArchipelagoWIP.AtlyssArchipelagoPlugin;

namespace AtlyssArchipelagoWIP
{
    [HarmonyPatch(typeof(SettingsManager), "Close_SettingsMenu")]
    // Updates the Archipelago DeathLink setting when the settings menu is closed, regardless of if the `Save` button was pressed.
    class SettingsMenuClosePatch
    {
        static void Prefix(SettingsManager __instance)
        {
            if (!GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_apSettingsTab"))
            {
                return;
            }
            AtlyssArchipelagoPlugin basePlugin = AtlyssArchipelagoPlugin.Instance;
            var apDeathLinkToggle = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_apSettingsTab/_backdrop_gameTab/Scroll View_gameTab/Viewport_gameTab/Content_gameTab/_cell_fadeGameFeed/Toggle_fadeGameFeed")
                .GetComponent<Toggle>();
            if (apDeathLinkToggle.isOn != basePlugin.cfgDeathlink.Value) // the toggle was changed. update the Archipelago server accordingly
            {
                basePlugin.ToggleDeathLink(apDeathLinkToggle.isOn);
            }
            basePlugin.cfgDeathlink.Value = apDeathLinkToggle.isOn;
        }
    }

    [HarmonyPatch(typeof(SettingsManager), "Open_SettingsMenu")]
    // Creates the new Archipelago settings menu when the menu is opened, if it doesn't already exist.
    class SettingsMenuOpenPatch
    {
        static void Prefix(SettingsManager __instance)
        {
            if (!GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_apSettingsTab") && SceneManager.GetSceneByName("01_rootScene").isLoaded)
            {
                bool uiNeedsDisable = false;
                if (!GameObject.Find("_GameUI_MainMenu")) // will only be missing it it's disabled
                {
                    uiNeedsDisable = true;
                    MainMenuManager._current.gameObject.transform.parent.gameObject.SetActive(true); // this one's a bit of a mess, let me explain
                    // [MainMenuManager._current] We get the current MainMenuManager, which is a child of the main menu gui.
                    // [.gameObject.transform.parent.gameObject] We get it's parent, which is the gui itself.
                    // [.SetActive(true)] activate the object, allowing the later GameObject.Find calls work.
                }
                AtlyssArchipelagoPlugin basePlugin = AtlyssArchipelagoPlugin.Instance;

                // The Archipelago menu doesn't exist. Create it.
                var apMenu = GameObject.Instantiate(GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_gameSettings")); // Copy an already existing tab
                apMenu.name = "_dolly_apSettingsTab"; // rename this object (i don't care to rename the children because we don't need to)
                apMenu.transform.SetParent(GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu").transform, false); // reparent it (copying removes the parent)
                var apMenuContent = apMenu.transform.Find("_backdrop_gameTab/Scroll View_gameTab/Viewport_gameTab/Content_gameTab"); // find the content
                apMenuContent.transform.Find("_header_gameSettings/Text").GetComponent<Text>().text = "Archipelago Settings"; // change the header name
                var apServerSetting = apMenuContent.transform.GetChild(1); // grab the settings we'll be changing
                var apSlotSetting = apMenuContent.transform.GetChild(2);
                var apPasswordSetting = apMenuContent.transform.GetChild(3);
                var apDeathLinkSetting = apMenuContent.transform.GetChild(21);
                apDeathLinkSetting.GetComponentInChildren<Text>().text = "DeathLink";
                apDeathLinkSetting.GetComponent<SettingsCell>()._setToggle = null; // disable basegame functionality of this toggle (we're adding our own)
                GameObject.Destroy(apServerSetting.transform.GetChild(1)); // remove the original buttons (we're replacing them)
                GameObject.Destroy(apSlotSetting.transform.GetChild(1));
                GameObject.Destroy(apPasswordSetting.transform.GetChild(1));
                apServerSetting.GetComponentInChildren<Text>().text = "Archipelago Server"; // change the setting display names
                apSlotSetting.GetComponentInChildren<Text>().text = "Slot Name";
                apPasswordSetting.GetComponentInChildren<Text>().text = "Password";
                apMenuContent.transform.Find("_header_nametagSettings/Text").GetComponent<Text>().text = "Press F5 at any time to connect!";
                for (int i = apMenuContent.childCount - 3; i >= 5; i--) // delete all other children, starting at the end
                {
                    GameObject child = apMenuContent.GetChild(i).gameObject;
                    GameObject.Destroy(child);
                }

                // Now, repopulate the settings menu with custom inputs
                // We copy an already working input field so we don't need to make three ourselves.
                var apServerInput = GameObject.Instantiate(GameObject.Find("_GameUI_MainMenu").transform.Find("_characterSelectMenu/Canvas_characterSelect/_dolly_characterManagement/_input_@nickname"));
                apServerInput.name = "Input_apServer";
                var apSlotInput = GameObject.Instantiate(GameObject.Find("_GameUI_MainMenu").transform.Find("_characterSelectMenu/Canvas_characterSelect/_dolly_characterManagement/_input_@nickname"));
                apSlotInput.name = "Input_apSlot";
                var apPasswordInput = GameObject.Instantiate(GameObject.Find("_GameUI_MainMenu").transform.Find("_characterSelectMenu/Canvas_characterSelect/_dolly_characterManagement/_input_@nickname"));
                apPasswordInput.name = "Input_apPassword";
                apServerInput.gameObject.SetActive(true); // these are inactive by default
                apSlotInput.gameObject.SetActive(true);
                apPasswordInput.gameObject.SetActive(true);
                GameObject.Destroy(apServerInput.transform.Find("_backdrop_globalNicknameTooltip")); // This is a tooltip that we don't need
                GameObject.Destroy(apSlotInput.transform.Find("_backdrop_globalNicknameTooltip"));
                GameObject.Destroy(apPasswordInput.transform.Find("_backdrop_globalNicknameTooltip"));
                apServerInput.transform.SetParent(apServerSetting.transform, false); // Parent it to each respective option
                apSlotInput.transform.SetParent(apSlotSetting.transform, false);
                apPasswordInput.transform.SetParent(apPasswordSetting.transform, false);
                apServerInput.transform.localPosition = new Vector3(180, 0, 0); // Align with the settings menu
                apSlotInput.transform.localPosition = new Vector3(180, 0, 0);
                apPasswordInput.transform.localPosition = new Vector3(180, 0, 0);

                // Autofill from the config files
                basePlugin.apServer = apServerInput.GetComponent<InputField>();
                basePlugin.apSlot = apSlotInput.GetComponent<InputField>();
                basePlugin.apPassword = apPasswordInput.GetComponent<InputField>();
                basePlugin.apDeathlink = apDeathLinkSetting.transform.Find("Toggle_fadeGameFeed").gameObject.GetComponent<Toggle>();
                ((Text)basePlugin.apServer.placeholder).text = "archipelago.gg:38281";
                ((Text)basePlugin.apSlot.placeholder).text = "ATLYSSPlayer";
                ((Text)basePlugin.apPassword.placeholder).text = "supersecret";
                basePlugin.apServer.text = basePlugin.cfgServer.Value;
                basePlugin.apSlot.text = basePlugin.cfgSlot.Value;
                basePlugin.apPassword.text = basePlugin.cfgPassword.Value;
                basePlugin.apDeathlink.isOn = basePlugin.cfgDeathlink.Value;

                // Now to create the Archipelago button in the settings menu
                var apSettingsTabButton = GameObject.Instantiate(GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_tabButtons/Button_gameTab"));
                apSettingsTabButton.name = "Button_apTab";
                apSettingsTabButton.GetComponentInChildren<Text>().text = "Archipelago";
                var settingsTabs = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_tabButtons");
                apSettingsTabButton.transform.SetParent(settingsTabs.transform, false);
                settingsTabs.GetComponent<HorizontalLayoutGroup>().childScaleWidth = true;
                settingsTabs.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
                settingsTabs.GetComponent<HorizontalLayoutGroup>().padding.right = 8; // make it symmetrical
                settingsTabs.SetActive(false);
                basePlugin.ReenableSettingsTabs(); // due to Monobehaviour shenanigans, we can't actually do this here.
                apSettingsTabButton.GetComponent<Button>().onClick.AddListener(() => __instance.Set_SettingMenuSelectionIndex(4)); // when the button is pressed, run the basegame function
                if (uiNeedsDisable)
                {
                    MainMenuManager._current.gameObject.transform.parent.gameObject.SetActive(false);
                }
            }
        }
    }

    [HarmonyPatch(typeof(SettingsManager), "Set_SettingMenuSelectionIndex")]
    // This patch detects if the settings menu is trying to access the fourth page index (not there in vanilla)
    // If it is, load a custom settings page made for Archipelago
    // Postfix, not Prefix, since the vanilla function already automatically closes all tabs if the index is invalid, making it easier for this patch.
    class SettingsMenuTabsPatch
    {
        static void Postfix(SettingsManager __instance, int _index)
        {
            if (!SceneManager.GetSceneByName("01_rootScene").isLoaded) // the game hasn't fully started yet
            {
                return;
            }
            if (_index == 4)
            {
                MenuElement apSettingsMenu = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_apSettingsTab").GetComponent<MenuElement>();
                RectTransform apMenuContent = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_apSettingsTab/_backdrop_gameTab/Scroll View_gameTab/Viewport_gameTab/Content_gameTab").GetComponent<RectTransform>();
                GameObject settingsTabHighlight = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_selectHighlight_tabButtons");
                apSettingsMenu.isEnabled = true;
                apMenuContent.anchoredPosition = Vector2.zero;
                settingsTabHighlight.transform.position = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_tabButtons/Button_apTab").transform.position;
            }
            else
            {
                MenuElement apSettingsMenu = GameObject.Find("_SettingsManager/Canvas_SettingsMenu/_dolly_settingsMenu/_dolly_apSettingsTab").GetComponent<MenuElement>();
                apSettingsMenu.isEnabled = false;
            }
        }
    }

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
            {// for some random reason, ATLYSS calls Player_OnDeath twice when the player dies.
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

    public class SpikePatch
    {
        [HarmonyPatch(typeof(File), nameof(File.ReadAllText), new Type[] { typeof(string) })]
        // Redirects Spike's bank loading code to load custom Archipelago item banks.
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

        public static void InitializeAPStorage()
        {
            try
            {
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