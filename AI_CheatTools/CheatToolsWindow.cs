using System;
using System.Collections.Generic;
using System.Linq;
using AIProject;
using AIProject.Definitions;
using AIProject.SaveData;
using Manager;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Map = Manager.Map;
using Resources = Manager.Resources;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static AgentActor _currentVisibleGirl;

        private static Studio.Studio _studioInstance;
        private static Manager.Sound _soundInstance;
        private static Scene _sceneInstance;
        private static Game _gameMgr;
        private static Resources _resources;
        private static Map _map;
        private static HSceneFlagCtrl _hScene;
        private static string _gameTimeText;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        public static void Initialize()
        {
            CheatToolsWindow.OnShown += _ =>
            {
                _studioInstance = Studio.Studio.IsInstance() ? Studio.Studio.Instance : null;
                _soundInstance = Manager.Sound.Instance;
                _sceneInstance = Scene.Instance;
                _gameMgr = Game.IsInstance() ? Game.Instance : null;
                _resources = Resources.Instance;
                _map = Map.IsInstance() ? Map.Instance : null;
                _hScene = HSceneFlagCtrl.IsInstance() ? HSceneFlagCtrl.Instance : null;

                _gameTimeText = null;

                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(_map != null && _map.AgentTable.Count > 0 ? (Func<object>)(() => _map.AgentTable.Values.Select(x => new ReadonlyCacheEntry(x.CharaName, x))) : null, "Heroine list"),
                    new KeyValuePair<object, string>(Manager.ADV.IsInstance() ? Manager.ADV.Instance : null, "Manager.ADV.Instance"),
                    new KeyValuePair<object, string>(AnimalManager.IsInstance() ? AnimalManager.Instance : null, "Manager.AnimalManager.Instance"),
                    new KeyValuePair<object, string>(_map, "Manager.Map.Instance"),
                    new KeyValuePair<object, string>(Character.IsInstance() ? Character.Instance : null, "Manager.Character.Instance"),
                    new KeyValuePair<object, string>(Config.IsInstance() ? Config.Instance : null, "Manager.Config.Instance"),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(Manager.Housing.IsInstance() ? Manager.Housing.Instance : null, "Manager.Housing.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                    new KeyValuePair<object, string>((Func<object>)EditorUtilities.GetRootGoScanner, "Root Objects")
                };
                ;
            };


            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null && _map.Player != null && _map.Player.PlayerData != null, DrawPlayerCheats, "Start the game to see player cheats"));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null && _map.Simulator != null, DrawEnviroControls, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hScene != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null, DrawGirlCheatMenu, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
        }

        private static void DrawPlayerCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("General / Player");
            var playerData = _map.Player.PlayerData;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Fishing skill lvl: " + playerData.FishingSkill.Level, GUILayout.Width(150));
                if (GUILayout.Button("+500 exp")) playerData.FishingSkill.AddExperience(500);
            }
            GUILayout.EndHorizontal();

            if (_resources != null)
            {
                var mp = _resources.MerchantProfile;
                if (mp != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var shanLvl = mp.SpendMoneyBorder.Count(x => playerData.SpendMoney >= x) + 1;
                        GUILayout.Label("Shan heart lvl: " + shanLvl, GUILayout.Width(150));
                        if (GUILayout.Button("1")) playerData.SpendMoney = 0;
                        if (GUILayout.Button("2")) playerData.SpendMoney = mp.SpendMoneyBorder[0];
                        if (GUILayout.Button("3")) playerData.SpendMoney = mp.SpendMoneyBorder[1];
                    }
                    GUILayout.EndHorizontal();
                }
            }

            FishingHackHooks.Enabled = GUILayout.Toggle(FishingHackHooks.Enabled, "Enable instant fishing");
            UnlockCraftingHooks.Enabled = GUILayout.Toggle(UnlockCraftingHooks.Enabled, "Enable free crafting");
            NoclipFeature.NoclipMode = GUILayout.Toggle(NoclipFeature.NoclipMode, "Enable player noclip");

            CheatToolsPlugin.BuildAnywhere.Value = GUILayout.Toggle(CheatToolsPlugin.BuildAnywhere.Value, "Allow building anywhere");
            CheatToolsPlugin.BuildOverlap.Value = GUILayout.Toggle(CheatToolsPlugin.BuildOverlap.Value, "Allow building items to overlap");

            if (_resources != null)
            {
                var dp = _resources.DefinePack;
                if (dp != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Warning: These can't be turned off!");
                        if (dp.MapDefines.ItemSlotMax >= 99999 && playerData.InventorySlotMax >= 99999)
                            GUI.enabled = false;
                        if (GUILayout.Button("Unlimited inventory slots"))
                        {
                            dp.MapDefines._itemSlotMax = 99999;
                            dp.MapDefines._itemStackUpperLimit = 99999;
                            playerData.InventorySlotMax = 99999;
                        }
                        GUI.enabled = true;

                        if (playerData.ItemList.Count == 0)
                            GUI.enabled = false;
                        if (GUILayout.Button("Clear player inventory"))
                        {
                            playerData.ItemList.Clear();
                            //MapUIContainer.AddNotify();
                            CheatToolsPlugin.Logger.LogMessage("Your inventory has been cleared.");
                        }
                        GUI.enabled = true;

                        GUILayout.BeginHorizontal();
                        {
                            var add1 = GUILayout.Button("Get +1 of all items");
                            var add99 = GUILayout.Button("+99");
                            if (add1 || add99)
                            {
                                if (_resources.GameInfo != null)
                                {
                                    var addAmount = add1 ? 1 : 99;
                                    foreach (var category in _resources.GameInfo.GetItemCategories())
                                    {
                                        foreach (var stuffItemInfo in _resources.GameInfo.GetItemTable(category).Values)
                                        {
                                            var it = playerData.ItemList.Find(item => item.CategoryID == stuffItemInfo.CategoryID && item.ID == stuffItemInfo.ID);
                                            if (it != null) it.Count += addAmount;
                                            else playerData.ItemList.Add(new StuffItem(stuffItemInfo.CategoryID, stuffItemInfo.ID, addAmount));
                                        }
                                    }

                                    CheatToolsPlugin.Logger.LogMessage(addAmount + " of all items have been added to your inventory");
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
            }

            if (GUILayout.Button("Navigate to Player's GameObject"))
            {
                if (_map.Player.transform != null)
                    ObjectTreeViewer.Instance.SelectAndShowObject(_map.Player.transform);
                else
                    CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message,
                        "Player has no body assigned");
            }

            if (GUILayout.Button("Open Player in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(_map.Player, "Player"), true);
        }

        private static void DrawEnviroControls(CheatToolsWindow cheatToolsWindow)
        {
            var weatherSim = _map.Simulator;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Weather: " + weatherSim.Weather, GUILayout.Width(120));

                if (weatherSim.Weather == Weather.Clear) GUI.enabled = false;
                if (GUILayout.Button("Clear")) weatherSim.RefreshWeather(Weather.Clear, true);
                GUI.enabled = true;

                if (GUILayout.Button("Next")) weatherSim.RefreshWeather(weatherSim.Weather.Next(), true);
            }
            GUILayout.EndHorizontal();

            if (weatherSim.EnvironmentProfile != null)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label($"Temperature: {weatherSim.TemperatureValue:F0}C", GUILayout.Width(120));
                    weatherSim.TemperatureValue = GUILayout.HorizontalSlider(weatherSim.TemperatureValue,
                        weatherSim.EnvironmentProfile.TemperatureBorder.MinDegree,
                        weatherSim.EnvironmentProfile.TemperatureBorder.MaxDegree);
                }
                GUILayout.EndHorizontal();
            }

            if (weatherSim.EnviroSky != null && weatherSim.EnviroSky.GameTime != null)
            {
                GUILayout.BeginHorizontal();
                {
                    var gameTime = weatherSim.EnviroSky.GameTime;
                    //var dt = DateTime.MinValue.AddHours(gameTime.Hours).AddMinutes(gameTime.Minutes).AddSeconds(gameTime.Seconds);
                    GUILayout.Label("Game time:", GUILayout.Width(120));
                    var timeText = _gameTimeText ?? $"{gameTime.Hours:00}:{gameTime.Minutes:00}:{gameTime.Seconds:00}";
                    var newTimeText = GUILayout.TextField(timeText, GUILayout.ExpandWidth(true));
                    if (timeText != newTimeText)
                    {
                        try
                        {
                            var parts = newTimeText.Split(':');
                            weatherSim.EnviroSky.SetTime(gameTime.Years, gameTime.Days, int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                            _gameTimeText = null;
                        }
                        catch
                        {
                            // Let user keep editing if the parsing fails
                            _gameTimeText = newTimeText;
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("H scene controls");

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Male Gauge: " + _hScene.feel_m.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_m = GUILayout.HorizontalSlider(_hScene.feel_m, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Female Gauge: " + _hScene.feel_f.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_f = GUILayout.HorizontalSlider(_hScene.feel_f, 0, 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Open HScene Flags in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(_hScene, "HSceneFlagCtrl"), true);
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("Heroines");

            var visibleGirls = _map.AgentTable.Values;

            foreach (var girl in visibleGirls)
            {
                if (GUILayout.Button($"Select #{girl.ID} - {girl.CharaName ?? girl.name}"))
                    _currentVisibleGirl = girl;
            }

            GUILayout.Space(6);

            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label("Select a heroine to access her stats");
        }

        private static void DrawSingleGirlCheats(AgentActor currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Selected heroine name: " + (currentAdvGirl.CharaName ?? currentAdvGirl.name));
                GUILayout.Space(6);

                if (currentAdvGirl.ChaControl != null && currentAdvGirl.ChaControl.fileGameInfo != null)
                {
                    GUILayout.Label("Status");

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label($"Love phase: {currentAdvGirl.ChaControl.fileGameInfo.phase + 1} / 4");
                        if (GUILayout.Button("-1")) currentAdvGirl.SetPhase(Mathf.Max(0, currentAdvGirl.ChaControl.fileGameInfo.phase - 1));
                        if (GUILayout.Button("+1")) currentAdvGirl.SetPhase(Mathf.Min(3, currentAdvGirl.ChaControl.fileGameInfo.phase + 1));
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        var sickness = AIProject.Definitions.Sickness.TagTable.FirstOrDefault(x => x.Value == currentAdvGirl.AgentData.SickState.ID).Key;
                        GUILayout.Label($"Sickness: {sickness ?? "None"}", GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Heal", GUILayout.ExpandWidth(false)) && currentAdvGirl.AgentData.SickState.ID > -1)
                        {
                            currentAdvGirl.HealSickBySleep();
                            currentAdvGirl.AgentData.SickState.OverwritableID = -1;
                            currentAdvGirl.AgentData.WeaknessMotivation = 0;
                        }
                    }
                    GUILayout.EndHorizontal();

                    foreach (Status.Type statusValue in Enum.GetValues(typeof(Status.Type)))
                    {
                        if (currentAdvGirl.AgentData.StatsTable.ContainsKey((int)statusValue))
                        {
                            GUILayout.BeginHorizontal();
                            {
                                var status = Mathf.RoundToInt(currentAdvGirl.AgentData.StatsTable[(int)statusValue]);
                                GUILayout.Label(statusValue + ": " + status, GUILayout.Width(120));
                                var newStatus = Mathf.RoundToInt(GUILayout.HorizontalSlider(status, 0, (int)statusValue == 5 ? 150 : 100));
                                if (newStatus != status)
                                    currentAdvGirl.AgentData.StatsTable[(int)statusValue] = newStatus;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.Space(6);

                    GUILayout.Label("Flavor skills");
                    foreach (FlavorSkill.Type typeValue in Enum.GetValues(typeof(FlavorSkill.Type)))
                    {
                        if (currentAdvGirl.ChaControl.fileGameInfo.flavorState.ContainsKey((int)typeValue))
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(typeValue + ": ", GUILayout.Width(120));
                                GUI.changed = false;
                                var flavorSkill = currentAdvGirl.GetFlavorSkill(typeValue);
                                var textField = GUILayout.TextField(flavorSkill.ToString());
                                if (GUI.changed && int.TryParse(textField, out var newSkill) && newSkill != flavorSkill)
                                    currentAdvGirl.SetFlavorSkill(typeValue, newSkill);
                                GUI.changed = false;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.Space(6);
                }

                if (currentAdvGirl.AgentData.TalkMotivation >= currentAdvGirl.AgentData.StatsTable[5])
                    GUI.enabled = false;
                if (GUILayout.Button("Reset talk time"))
                {
                    currentAdvGirl.AgentData.TalkMotivation = currentAdvGirl.AgentData.StatsTable[5];
                    currentAdvGirl.AgentData.WeaknessMotivation = 0;
                }
                GUI.enabled = true;

                GUILayout.Space(6);

                if (GUILayout.Button("Navigate to Actor's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message, "Actor has no body assigned");
                }

                if (GUILayout.Button("Open Actor in inspector"))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, "Actor " + currentAdvGirl.CharaName), true);

                if (GUILayout.Button("Inspect extended data"))
                {
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.ChaControl?.chaFile), "ExtData for " + currentAdvGirl.CharaName), true);
                }
            }
            GUILayout.EndVertical();
        }
    }
}
