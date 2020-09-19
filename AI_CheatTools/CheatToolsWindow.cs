using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIChara;
using AIProject;
using AIProject.Definitions;
using AIProject.SaveData;
using HarmonyLib;
using Manager;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Map = Manager.Map;
using Resources = Manager.Resources;

namespace CheatTools
{
    public class CheatToolsWindow
    {
        private const int ScreenOffset = 20;

        private readonly RuntimeUnityEditorCore _editor;

        private readonly string _mainWindowTitle;
        private Vector2 _cheatsScrollPos;
        private Rect _cheatWindowRect;
        private Rect _screenRect;
        private bool _show;

        private AgentActor _currentVisibleGirl;

        private Studio.Studio _studioInstance;
        private Manager.Sound _soundInstance;
        private Scene _sceneInstance;
        private Game _gameMgr;
        private Resources _resources;
        private Map _map;
        private HSceneFlagCtrl _hScene;
        private string _gameTimeText;

        private readonly Func<object> _funcGetHeroines;
        private readonly Func<object> _funcGetRootGos;

        public CheatToolsWindow(RuntimeUnityEditorCore editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            ToStringConverter.AddConverter<AgentActor>(heroine => !string.IsNullOrEmpty(heroine.CharaName) ? heroine.CharaName : heroine.name);
            ToStringConverter.AddConverter<AgentData>(d => $"AgentData - {d.CharaFileName} | {d.NowCoordinateFileName}");
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

            _mainWindowTitle = "Cheat Tools " + Assembly.GetExecutingAssembly().GetName().Version;

            _funcGetHeroines = () => _map.AgentTable.Values.Select(x => new ReadonlyCacheEntry(x.CharaName, x));
            _funcGetRootGos = EditorUtilities.GetRootGoScanner;
        }

        public bool Show
        {
            get => _show;
            set
            {
                _show = value;
                _editor.Show = value;

                if (value)
                    SetWindowSizes();

                _studioInstance = Studio.Studio.IsInstance() ? Studio.Studio.Instance : null;
                _soundInstance = Manager.Sound.Instance;
                _sceneInstance = Scene.Instance;
                _gameMgr = Game.IsInstance() ? Game.Instance : null;
                _resources = Resources.Instance;
                _map = Map.IsInstance() ? Map.Instance : null;
                _hScene = HSceneFlagCtrl.IsInstance() ? HSceneFlagCtrl.Instance : null;

                _gameTimeText = null;
            }
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            const int cheatWindowHeight = 410;
            _cheatWindowRect = new Rect(_screenRect.xMin, _screenRect.yMax - cheatWindowHeight, 270, cheatWindowHeight);
        }

        public void DisplayCheatWindow()
        {
            if (!Show) return;

            var skinBack = GUI.skin;
            GUI.skin = InterfaceMaker.CustomSkin;

            _cheatWindowRect = GUILayout.Window(591, _cheatWindowRect, CheatWindowContents, _mainWindowTitle);

            InterfaceMaker.EatInputInRect(_cheatWindowRect);
            GUI.skin = skinBack;
        }

        private void CheatWindowContents(int id)
        {
            _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
            {
                DrawPlayerCheats();

                DrawEnviroControls();

                DrawHSceneCheats();

                DrawGirlCheatMenu();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label("Open in inspector");
                    foreach (var obj in new[]
                    {
                            new KeyValuePair<object, string>(_map != null && _map.AgentTable.Count > 0 ? _funcGetHeroines : null, "Heroine list"),
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
                            new KeyValuePair<object, string>(_funcGetRootGos, "Root Objects")
                    })
                    {
                        if (obj.Key == null) continue;
                        if (GUILayout.Button(obj.Value))
                        {
                            if (obj.Key is Type t)
                                _editor.Inspector.Push(new StaticStackEntry(t, obj.Value), true);
                            else if (obj.Key is Func<object> f)
                                _editor.Inspector.Push(new InstanceStackEntry(f(), obj.Value), true);
                            else
                                _editor.Inspector.Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                        }
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void DrawPlayerCheats()
        {
            if (_map != null && _map.Player == null) return;
            var playerData = _map.Player.PlayerData;
            if (playerData == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("General / Player");

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
                CheatToolsPlugin.NoclipMode = GUILayout.Toggle(CheatToolsPlugin.NoclipMode, "Enable player noclip");

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
                                var tr = Traverse.Create(dp.MapDefines);
                                tr.Field("_itemSlotMax").SetValue(99999);
                                tr.Field("_itemStackUpperLimit").SetValue(99999);
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
                        _editor.TreeViewer.SelectAndShowObject(_map.Player.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message,
                            "Player has no body assigned");
                }

                if (GUILayout.Button("Open Player in inspector"))
                    _editor.Inspector.Push(new InstanceStackEntry(_map.Player, "Player"), true);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawEnviroControls()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                if (_map != null)
                {
                    var weatherSim = _map.Simulator;
                    if (weatherSim != null)
                    {
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
                }

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
                    GUILayout.Label((int)Math.Round(Time.timeScale * 100) + "%", GUILayout.Width(35));
                    Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0, 5, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                        Time.timeScale = 1;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawHSceneCheats()
        {
            if (_hScene == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
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
                    _editor.Inspector.Push(new InstanceStackEntry(_hScene, "HSceneFlagCtrl"), true);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawGirlCheatMenu()
        {
            if (_map == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
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
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawSingleGirlCheats(AgentActor currentAdvGirl)
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
                        _editor.TreeViewer.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message, "Actor has no body assigned");
                }

                if (GUILayout.Button("Open Actor in inspector"))
                    _editor.Inspector.Push(new InstanceStackEntry(currentAdvGirl, "Actor " + currentAdvGirl.CharaName), true);
            }
            GUILayout.EndVertical();
        }
    }
}
