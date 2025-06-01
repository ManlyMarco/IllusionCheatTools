using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIChara;
using AIProject;
using AIProject.Definitions;
using AIProject.SaveData;
using BepInEx.Configuration;
using Manager;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json; // 引入 Newtonsoft.Json 命名空间
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
        private static bool _expandDesires, _expandSkills;

        // 翻译相关的成员
        private static Dictionary<string, string> _translations; // 存储当前语言的键值对
        private static string _currentLanguage = "en"; // 默认英文
        private static string _pluginLocation; // 插件安装路径
        private static readonly Dictionary<string, string> CachedTranslations = new(); // 缓存翻译

        // 支持的语言列表
        private static Dictionary<string, string> SupportedLanguages;

        internal static ConfigEntry<bool> BuildAnywhere;
        internal static ConfigEntry<bool> BuildOverlap;
        internal static ConfigEntry<string> SelectedLanguage;

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location; // 存储插件路径

            var config = instance.Config;

            BuildAnywhere = config.Bind("Cheats", "Allow building anywhere", false);
            BuildAnywhere.SettingChanged += (sender, args) => BuildAnywhereHooks.Enabled = BuildAnywhere.Value;
            BuildAnywhereHooks.Enabled = BuildAnywhere.Value;

            BuildOverlap = config.Bind("Cheats", "Allow building overlap", false);
            BuildOverlap.SettingChanged += (sender, args) => BuildOverlapHooks.Enabled = BuildOverlap.Value;
            BuildOverlapHooks.Enabled = BuildOverlap.Value;

            SelectedLanguage = config.Bind("General", "Language", "en", "Selected UI language");
            _currentLanguage = SelectedLanguage.Value;

            // 加载支持的语言列表
            LoadSupportedLanguages();
            LoadLanguage(_currentLanguage);

            SelectedLanguage.SettingChanged += (sender, args) =>
            {
                _currentLanguage = SelectedLanguage.Value;
                LoadLanguage(_currentLanguage);
            };

            NoclipFeature.InitializeNoclip(instance);

            ToStringConverter.AddConverter<AgentActor>(heroine => !string.IsNullOrEmpty(heroine.CharaName) ? heroine.CharaName : heroine.name);
            ToStringConverter.AddConverter<AgentData>(d => $"AgentData - {d.CharaFileName} | {d.NowCoordinateFileName}");
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

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
                    new KeyValuePair<object, string>(_map != null && _map.AgentTable.Count > 0 ? (Func<object>)(() => _map.AgentTable.Values.Select(x => new ReadonlyCacheEntry(x.CharaName, x))) : null, T("UI.HeroineList")),
                    new KeyValuePair<object, string>(Manager.ADV.IsInstance() ? Manager.ADV.Instance : null, "Manager.ADV.Instance"),
                    new KeyValuePair<object, string>(AnimalManager.IsInstance() ? AnimalManager.Instance : null, "Manager.AnimalManager.Instance"),
                    new KeyValuePair<object, string>(_map, "Manager.Map.Instance"),
                    new KeyValuePair<object, string>(Character.IsInstance() ? Character.Instance : null, "Manager.Character.Instance"),
                    new KeyValuePair<object, string>(Manager.Config.IsInstance() ? Manager.Config.Instance : null, "Manager.Config.Instance"),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(Manager.Housing.IsInstance() ? Manager.Housing.Instance : null, "Manager.Housing.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null && _map.Player != null && _map.Player.PlayerData != null, DrawPlayerCheats, T("UI.StartGameToSeePlayerCheats")));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null && _map.Simulator != null, DrawEnviroControls, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hScene != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null, DrawGirlCheatMenu, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            // 添加语言选择器到窗口顶部
            CheatToolsWindow.OnGUI += () => DrawLanguageSelector();
        }

        // 加载支持的语言列表
        private static void LoadSupportedLanguages()
        {
            string languagesFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), "languages.json");
            if (File.Exists(languagesFilePath))
            {
                try
                {
                    string json = File.ReadAllText(languagesFilePath);
                    var languages = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, string>>>>(json);
                    SupportedLanguages = languages["supportedLanguages"].ToDictionary(l => l["code"], l => l["name"]);
                }
                catch (Exception ex)
                {
                    CheatToolsPlugin.Logger.LogError($"Failed to load languages.json: {ex.Message}");
                    SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "zh_CN", "中文" } };
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning("languages.json not found, using default languages.");
                SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "zh_CN", "中文" } };
            }
        }

        // 加载语言文件
        private static void LoadLanguage(string langCode)
        {
            string langFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), $"lang_{langCode}.json");
            if (File.Exists(langFilePath))
            {
                try
                {
                    string json = File.ReadAllText(langFilePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    CheatToolsPlugin.Logger.LogInfo($"Successfully loaded language file: {langFilePath}");
                }
                catch (JsonException ex)
                {
                    CheatToolsPlugin.Logger.LogError($"JSON error in {langFilePath}: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
                catch (IOException ex)
                {
                    CheatToolsPlugin.Logger.LogError($"File read error for {langFilePath}: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning($"Language file {langFilePath} not found.");
                _translations = new Dictionary<string, string>();
            }
            CachedTranslations.Clear(); // 清空缓存
        }

        // 翻译函数
        private static string T(string key)
        {
            if (CachedTranslations.TryGetValue(key, out string value))
                return value;
            if (_translations != null && _translations.TryGetValue(key, out value))
            {
                CachedTranslations[key] = value;
                return value;
            }
            if (_currentLanguage != "en")
            {
                string langFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), "lang_en.json");
                if (File.Exists(langFilePath))
                {
                    try
                    {
                        string json = File.ReadAllText(langFilePath);
                        var fallbackTranslations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (fallbackTranslations.TryGetValue(key, out value))
                        {
                            CachedTranslations[key] = value;
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        CheatToolsPlugin.Logger.LogError($"Failed to load fallback language (en): {ex.Message}");
                    }
                }
            }
            CheatToolsPlugin.Logger.LogWarning($"Translation key '{key}' not found for language '{_currentLanguage}'.");
            return key.Split('.').Last(); // 返回键的最后部分
        }

        // 绘制语言选择器
        private static void DrawLanguageSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("UI.Language") + ": ", GUILayout.Width(100));
            foreach (var lang in SupportedLanguages)
            {
                if (GUILayout.Button(lang.Value))
                {
                    if (_currentLanguage != lang.Key)
                    {
                        _currentLanguage = lang.Key;
                        SelectedLanguage.Value = _currentLanguage;
                        LoadLanguage(_currentLanguage);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawPlayerCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.GeneralPlayer"));
            var playerData = _map.Player.PlayerData;

            // 钓鱼技能
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format(T("UI.FishingSkillLevel"), playerData.FishingSkill.Level), GUILayout.Width(150));
            if (GUILayout.Button("+500 exp")) playerData.FishingSkill.AddExperience(500);
            GUILayout.EndHorizontal();

            // 珊心等级
            if (_resources?.MerchantProfile != null)
            {
                var mp = _resources.MerchantProfile;
                var shanLvl = mp.SpendMoneyBorder.Count(x => playerData.SpendMoney >= x) + 1;
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format(T("UI.ShanHeartLevel"), shanLvl), GUILayout.Width(150));
                if (GUILayout.Button("1")) playerData.SpendMoney = 0;
                if (GUILayout.Button("2")) playerData.SpendMoney = mp.SpendMoneyBorder[0];
                if (GUILayout.Button("3")) playerData.SpendMoney = mp.SpendMoneyBorder[1];
                GUILayout.EndHorizontal();
            }

            // 切换选项
            FishingHackHooks.Enabled = GUILayout.Toggle(FishingHackHooks.Enabled, T("UI.EnableInstantFishing"));
            UnlockCraftingHooks.Enabled = GUILayout.Toggle(UnlockCraftingHooks.Enabled, T("UI.EnableFreeCrafting"));
            NoclipFeature.NoclipMode = GUILayout.Toggle(NoclipFeature.NoclipMode, T("UI.EnablePlayerNoclip"));
            BuildAnywhere.Value = GUILayout.Toggle(BuildAnywhere.Value, T("UI.AllowBuildingAnywhere"));
            BuildOverlap.Value = GUILayout.Toggle(BuildOverlap.Value, T("UI.AllowBuildingOverlap"));

            // 背包控制
            if (_resources?.DefinePack != null)
            {
                var dp = _resources.DefinePack;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(T("UI.WarningPermanent"));
                if (dp.MapDefines.ItemSlotMax >= 99999 && playerData.InventorySlotMax >= 99999)
                    GUI.enabled = false;
                if (GUILayout.Button(T("UI.UnlimitedInventorySlots")))
                {
                    dp.MapDefines._itemSlotMax = 99999;
                    dp.MapDefines._itemStackUpperLimit = 99999;
                    playerData.InventorySlotMax = 99999;
                }
                GUI.enabled = true;

                if (playerData.ItemList.Count == 0)
                    GUI.enabled = false;
                if (GUILayout.Button(T("UI.ClearInventory")))
                {
                    playerData.ItemList.Clear();
                    CheatToolsPlugin.Logger.LogMessage(T("UI.InventoryCleared"));
                }
                GUI.enabled = true;

                GUILayout.BeginHorizontal();
                var add1 = GUILayout.Button(T("UI.AddOneItem"));
                var add99 = GUILayout.Button(T("UI.AddNinetyNineItems"));
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
                        CheatToolsPlugin.Logger.LogMessage(string.Format(T("UI.ItemsAdded"), addAmount));
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            // 导航按钮
            if (GUILayout.Button(T("UI.NavigateToPlayer")))
            {
                if (_map.Player.transform != null)
                    ObjectTreeViewer.Instance.SelectAndShowObject(_map.Player.transform);
                else
                    CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message, T("UI.PlayerNoBodyAssigned"));
            }

            if (GUILayout.Button(T("UI.OpenPlayerInInspector")))
                Inspector.Instance.Push(new InstanceStackEntry(_map.Player, T("UI.Player")), true);
        }

        private static void DrawEnviroControls(CheatToolsWindow cheatToolsWindow)
        {
            var weatherSim = _map.Simulator;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.Weather") + ": " + T($"Game.Weather.{weatherSim.Weather}"), GUILayout.Width(120));

                if (weatherSim.Weather == Weather.Clear) GUI.enabled = false;
                if (GUILayout.Button(T("Game.Weather.Clear"))) weatherSim.RefreshWeather(Weather.Clear, true);
                GUI.enabled = true;

                if (GUILayout.Button(T("UI.Next"))) weatherSim.RefreshWeather(weatherSim.Weather.Next(), true);
            }
            GUILayout.EndHorizontal();

            if (weatherSim.EnvironmentProfile != null)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(string.Format(T("UI.Temperature"), weatherSim.TemperatureValue.ToString("F0")), GUILayout.Width(120));
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
                    GUILayout.Label(T("UI.GameTime") + ":", GUILayout.Width(120));
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
                            _gameTimeText = newTimeText;
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.HSceneControls"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.MaleGauge") + ": " + _hScene.feel_m.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_m = GUILayout.HorizontalSlider(_hScene.feel_m, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.FemaleGauge") + ": " + _hScene.feel_f.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_f = GUILayout.HorizontalSlider(_hScene.feel_f, 0, 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(T("UI.OpenHSceneFlagsInInspector")))
                Inspector.Instance.Push(new InstanceStackEntry(_hScene, T("UI.HSceneFlagCtrl")), true);
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.Heroines"));

            var visibleGirls = _map.AgentTable.Values;

            foreach (var girl in visibleGirls)
            {
                if (GUILayout.Button(string.Format(T("UI.SelectHeroine"), girl.ID, girl.CharaName ?? girl.name)))
                    _currentVisibleGirl = girl;
            }

            GUILayout.Space(6);

            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label(T("UI.SelectHeroineToAccessStats"));
        }

        private static void DrawSingleGirlCheats(AgentActor currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label(string.Format(T("UI.SelectedHeroineName"), currentAdvGirl.CharaName ?? currentAdvGirl.name));
                GUILayout.Space(6);

                if (currentAdvGirl.ChaControl != null && currentAdvGirl.ChaControl.fileGameInfo != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label(T("UI.Status"));

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label(string.Format(T("Game.Status.LovePhase"), currentAdvGirl.ChaControl.fileGameInfo.phase + 1));
                            if (GUILayout.Button("-1")) currentAdvGirl.SetPhase(Mathf.Max(0, currentAdvGirl.ChaControl.fileGameInfo.phase - 1));
                            if (GUILayout.Button("+1")) currentAdvGirl.SetPhase(Mathf.Min(3, currentAdvGirl.ChaControl.fileGameInfo.phase + 1));
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        {
                            var sickness = AIProject.Definitions.Sickness.TagTable.FirstOrDefault(x => x.Value == currentAdvGirl.AgentData.SickState.ID).Key;
                            GUILayout.Label(T("UI.Sickness") + ": " + (sickness != null ? T(sickness) : T("Game.Status.SicknessNone")), GUILayout.ExpandWidth(true));
                            if (GUILayout.Button(T("UI.Heal"), GUILayout.ExpandWidth(false)) && currentAdvGirl.AgentData.SickState.ID > -1)
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
                                    GUILayout.Label(T(statusValue.ToString()) + ": " + status, GUILayout.Width(120));
                                    var newStatus = Mathf.RoundToInt(GUILayout.HorizontalSlider(status, 0, (int)statusValue == 5 ? 150 : 100));
                                    if (newStatus != status)
                                        currentAdvGirl.AgentData.StatsTable[(int)statusValue] = newStatus;
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        _expandDesires = GUILayout.Toggle(_expandDesires, T("UI.DesiresMotivations"));
                        if (_expandDesires)
                        {
                            foreach (Desire.Type typeValue in Enum.GetValues(typeof(Desire.Type)))
                            {
                                var desireKey = Desire.GetDesireKey(typeValue);
                                var desire = currentAdvGirl.GetDesire(desireKey);
                                var motivation = currentAdvGirl.GetMotivation(desireKey);
                                if (desire.HasValue || motivation.HasValue)
                                {
                                    GUILayout.BeginHorizontal();
                                    {
                                        GUILayout.Label(T(typeValue.ToString()) + ": ", GUILayout.ExpandWidth(true));
                                        GUI.changed = false;

                                        if (desire.HasValue)
                                        {
                                            var textFieldDesire = GUILayout.TextField($"{desire.Value:F2}", GUILayout.Width(60));
                                            if (GUI.changed && float.TryParse(textFieldDesire, out var newDesire) && Mathf.Abs(newDesire - desire.Value) >= 0.01f)
                                                currentAdvGirl.SetDesire(desireKey, newDesire);
                                            GUI.changed = false;
                                        }
                                        else
                                        {
                                            GUILayout.Label("---", GUILayout.Width(60));
                                        }

                                        if (motivation.HasValue)
                                        {
                                            var textFieldMotivation = GUILayout.TextField($"{motivation.Value:F2}", GUILayout.Width(60));
                                            if (GUI.changed && float.TryParse(textFieldMotivation, out var newMotivation) && Mathf.Abs(newMotivation - motivation.Value) >= 0.01f)
                                                currentAdvGirl.SetMotivation(desireKey, newMotivation);
                                            GUI.changed = false;
                                        }
                                        else
                                        {
                                            GUILayout.Label("---", GUILayout.Width(60));
                                        }
                                    }
                                    GUILayout.EndHorizontal();
                                }
                            }
                        }
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        _expandSkills = GUILayout.Toggle(_expandSkills, T("UI.FlavorSkills"));
                        if (_expandSkills)
                        {
                            foreach (FlavorSkill.Type typeValue in Enum.GetValues(typeof(FlavorSkill.Type)))
                            {
                                if (currentAdvGirl.ChaControl.fileGameInfo.flavorState.ContainsKey((int)typeValue))
                                {
                                    GUILayout.BeginHorizontal();
                                    {
                                        GUILayout.Label(T(typeValue.ToString()) + ": ", GUILayout.Width(120));
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
                        }
                    }
                    GUILayout.EndVertical();
                }

                if (currentAdvGirl.AgentData.TalkMotivation >= currentAdvGirl.AgentData.StatsTable[5])
                    GUI.enabled = false;
                if (GUILayout.Button(T("UI.ResetTalkTime")))
                {
                    currentAdvGirl.AgentData.TalkMotivation = currentAdvGirl.AgentData.StatsTable[5];
                    currentAdvGirl.AgentData.WeaknessMotivation = 0;
                }
                GUI.enabled = true;

                GUILayout.Space(6);

                if (GUILayout.Button(T("UI.NavigateToActor")))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message, T("UI.ActorNoBodyAssigned"));
                }

                if (GUILayout.Button(T("UI.OpenActorInInspector")))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, string.Format(T("UI.Actor"), currentAdvGirl.CharaName)), true);

                if (GUILayout.Button(T("UI.InspectExtendedData")))
                {
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.ChaControl?.chaFile), string.Format(T("UI.ExtDataFor"), currentAdvGirl.CharaName)), true);
                }
            }
            GUILayout.EndVertical();
        }
    }
}
