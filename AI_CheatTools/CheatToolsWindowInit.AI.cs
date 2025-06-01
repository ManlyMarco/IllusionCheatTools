using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
        private static Dictionary<string, string> _translations; // 直接存储当前语言的键值对
        private static string _currentLanguage = "zh_CN"; // 默认中文语言代码
        private static string _pluginLocation; // 用于存储插件的安装路径

        internal static ConfigEntry<bool> BuildAnywhere;
        internal static ConfigEntry<bool> BuildOverlap;

        // 翻译函数：从当前加载的翻译字典中查找键名对应的文本
        private static string T(string key)
        {
            if (_translations != null && _translations.ContainsKey(key))
                return _translations[key];
            return key; // 如果找不到翻译，回退到键名本身
        }

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location; // 存储插件的安装路径

            // 初始加载默认语言文件 (例如 lang_zh_CN.json)
            LoadLanguage(_currentLanguage);

            var config = instance.Config;

            BuildAnywhere = config.Bind("Cheats", "Allow building anywhere", false);
            BuildAnywhere.SettingChanged += (sender, args) => BuildAnywhereHooks.Enabled = BuildAnywhere.Value;
            BuildAnywhereHooks.Enabled = BuildAnywhere.Value;

            BuildOverlap = config.Bind("Cheats", "Allow building overlap", false);
            BuildOverlap.SettingChanged += (sender, args) => BuildOverlapHooks.Enabled = BuildOverlap.Value;
            BuildOverlapHooks.Enabled = BuildOverlap.Value;

            NoclipFeature.InitializeNoclip(instance);

            // ToStringConverter 转换器通常不需要翻译，它们用于调试信息
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

                // 使用 T() 函数翻译按钮文本
                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(_map != null && _map.AgentTable.Count > 0 ? (Func<object>)(() => _map.AgentTable.Values.Select(x => new ReadonlyCacheEntry(x.CharaName, x))) : null, T("Heroine list")),
                    new KeyValuePair<object, string>(Manager.ADV.IsInstance() ? Manager.ADV.Instance : null, T("Manager.ADV.Instance")),
                    new KeyValuePair<object, string>(AnimalManager.IsInstance() ? AnimalManager.Instance : null, T("Manager.AnimalManager.Instance")),
                    new KeyValuePair<object, string>(_map, T("Manager.Map.Instance")),
                    new KeyValuePair<object, string>(Character.IsInstance() ? Character.Instance : null, T("Manager.Character.Instance")),
                    new KeyValuePair<object, string>(Manager.Config.IsInstance() ? Manager.Config.Instance : null, T("Manager.Config.Instance")),
                    new KeyValuePair<object, string>(_gameMgr, T("Manager.Game.Instance")),
                    new KeyValuePair<object, string>(Manager.Housing.IsInstance() ? Manager.Housing.Instance : null, T("Manager.Housing.Instance")),
                    new KeyValuePair<object, string>(_sceneInstance, T("Manager.Scene.Instance")),
                    new KeyValuePair<object, string>(_soundInstance, T("Manager.Sound.Instance")),
                    new KeyValuePair<object, string>(_studioInstance, T("Studio.Instance")),
                };
            };

            // 使用 T() 函数翻译作弊菜单项的描述
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null && _map.Player != null && _map.Player.PlayerData != null, DrawPlayerCheats, T("Start the game to see player cheats")));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null && _map.Simulator != null, DrawEnviroControls, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hScene != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _map != null, DrawGirlCheatMenu, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
        }

        // 新增的加载语言文件方法
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
                catch (Exception ex)
                {
                    CheatToolsPlugin.Logger.LogError($"Failed to load language file {langFilePath}: {ex.Message}. Falling back to default strings.");
                    _translations = new Dictionary<string, string>(); // 加载失败时清空翻译，回退到键名
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning($"Language file {langFilePath} not found. Using default strings.");
                _translations = new Dictionary<string, string>(); // 文件不存在时清空翻译
            }
        }

        // 绘制语言选择器
        private static void DrawLanguageSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Language") + ": ", GUILayout.Width(100)); // “Language”本身也需要翻译
            if (GUILayout.Button(T("Chinese"))) // 按钮文本翻译
            {
                if (_currentLanguage != "zh_CN")
                {
                    _currentLanguage = "zh_CN";
                    LoadLanguage(_currentLanguage); // 重新加载语言文件
                }
            }
            if (GUILayout.Button(T("English"))) // 按钮文本翻译
            {
                if (_currentLanguage != "en")
                {
                    _currentLanguage = "en";
                    LoadLanguage(_currentLanguage); // 重新加载语言文件
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawPlayerCheats(CheatToolsWindow cheatToolsWindow)
        {
            DrawLanguageSelector(); // 在每个作弊菜单的顶部添加语言选择器
            GUILayout.Label(T("General / Player"));
            var playerData = _map.Player.PlayerData;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("Fishing skill lvl: ") + playerData.FishingSkill.Level, GUILayout.Width(150));
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
                        GUILayout.Label(T("Shan heart lvl: ") + shanLvl, GUILayout.Width(150));
                        if (GUILayout.Button("1")) playerData.SpendMoney = 0;
                        if (GUILayout.Button("2")) playerData.SpendMoney = mp.SpendMoneyBorder[0];
                        if (GUILayout.Button("3")) playerData.SpendMoney = mp.SpendMoneyBorder[1];
                    }
                    GUILayout.EndHorizontal();
                }
            }

            FishingHackHooks.Enabled = GUILayout.Toggle(FishingHackHooks.Enabled, T("Enable instant fishing"));
            UnlockCraftingHooks.Enabled = GUILayout.Toggle(UnlockCraftingHooks.Enabled, T("Enable free crafting"));
            NoclipFeature.NoclipMode = GUILayout.Toggle(NoclipFeature.NoclipMode, T("Enable player noclip"));

            BuildAnywhere.Value = GUILayout.Toggle(BuildAnywhere.Value, T("Allow building anywhere"));
            BuildOverlap.Value = GUILayout.Toggle(BuildOverlap.Value, T("Allow building items to overlap"));

            if (_resources != null)
            {
                var dp = _resources.DefinePack;
                if (dp != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label(T("Warning: These can't be turned off!"));
                        if (dp.MapDefines.ItemSlotMax >= 99999 && playerData.InventorySlotMax >= 99999)
                            GUI.enabled = false;
                        if (GUILayout.Button(T("Unlimited inventory slots")))
                        {
                            dp.MapDefines._itemSlotMax = 99999;
                            dp.MapDefines._itemStackUpperLimit = 99999;
                            playerData.InventorySlotMax = 99999;
                        }
                        GUI.enabled = true;

                        if (playerData.ItemList.Count == 0)
                            GUI.enabled = false;
                        if (GUILayout.Button(T("Clear player inventory")))
                        {
                            playerData.ItemList.Clear();
                            CheatToolsPlugin.Logger.LogMessage(T("Your inventory has been cleared."));
                        }
                        GUI.enabled = true;

                        GUILayout.BeginHorizontal();
                        {
                            var add1 = GUILayout.Button(T("Get +1 of all items"));
                            var add99 = GUILayout.Button(T("+99"));
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

                                    // 使用 string.Format 进行更灵活的文本拼接
                                    CheatToolsPlugin.Logger.LogMessage(string.Format(T("{0} items have been added to your inventory"), addAmount));
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
            }

            if (GUILayout.Button(T("Navigate to Player's GameObject")))
            {
                if (_map.Player.transform != null)
                    ObjectTreeViewer.Instance.SelectAndShowObject(_map.Player.transform);
                else
                    CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message,
                        T("Player has no body assigned"));
            }

            if (GUILayout.Button(T("Open Player in inspector")))
                Inspector.Instance.Push(new InstanceStackEntry(_map.Player, T("Player")), true); // 翻译 Inspector 的标题
        }

        private static void DrawEnviroControls(CheatToolsWindow cheatToolsWindow)
        {
            var weatherSim = _map.Simulator;

            GUILayout.BeginHorizontal();
            {
                // 翻译天气类型，例如 "Weather.Clear" 对应 "晴朗"
                GUILayout.Label(T("Weather: ") + T("Weather." + weatherSim.Weather.ToString()), GUILayout.Width(120));

                if (weatherSim.Weather == Weather.Clear) GUI.enabled = false;
                if (GUILayout.Button(T("Clear"))) weatherSim.RefreshWeather(Weather.Clear, true);
                GUI.enabled = true;

                if (GUILayout.Button(T("Next"))) weatherSim.RefreshWeather(weatherSim.Weather.Next(), true);
            }
            GUILayout.EndHorizontal();

            if (weatherSim.EnvironmentProfile != null)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(T("Temperature: ") + $"{weatherSim.TemperatureValue:F0}C", GUILayout.Width(120));
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
                    GUILayout.Label(T("Game time:"), GUILayout.Width(120));
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
            GUILayout.Label(T("H scene controls"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("Male Gauge: ") + _hScene.feel_m.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_m = GUILayout.HorizontalSlider(_hScene.feel_m, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("Female Gauge: ") + _hScene.feel_f.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_f = GUILayout.HorizontalSlider(_hScene.feel_f, 0, 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(T("Open HScene Flags in inspector")))
                Inspector.Instance.Push(new InstanceStackEntry(_hScene, T("HSceneFlagCtrl")), true); // 翻译 Inspector 的标题
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("Heroines"));

            var visibleGirls = _map.AgentTable.Values;

            foreach (var girl in visibleGirls)
            {
                if (GUILayout.Button($"{T("Select")} #{girl.ID} - {girl.CharaName ?? girl.name}")) // 翻译 "Select"
                    _currentVisibleGirl = girl;
            }

            GUILayout.Space(6);

            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label(T("Select a heroine to access her stats"));
        }

        private static void DrawSingleGirlCheats(AgentActor currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label(T("Selected heroine name: ") + (currentAdvGirl.CharaName ?? currentAdvGirl.name));
                GUILayout.Space(6);

                if (currentAdvGirl.ChaControl != null && currentAdvGirl.ChaControl.fileGameInfo != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label(T("Status"));

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label(T("Love phase: ") + $"{currentAdvGirl.ChaControl.fileGameInfo.phase + 1} / 4");
                            if (GUILayout.Button("-1")) currentAdvGirl.SetPhase(Mathf.Max(0, currentAdvGirl.ChaControl.fileGameInfo.phase - 1));
                            if (GUILayout.Button("+1")) currentAdvGirl.SetPhase(Mathf.Min(3, currentAdvGirl.ChaControl.fileGameInfo.phase + 1));
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        {
                            var sickness = AIProject.Definitions.Sickness.TagTable.FirstOrDefault(x => x.Value == currentAdvGirl.AgentData.SickState.ID).Key;
                            // 翻译疾病名称，如果 sickness 为 null，则显示 "None" 的翻译
                            GUILayout.Label(T("Sickness: ") + (sickness != null ? T(sickness) : T("None")), GUILayout.ExpandWidth(true));
                            if (GUILayout.Button(T("Heal"), GUILayout.ExpandWidth(false)) && currentAdvGirl.AgentData.SickState.ID > -1)
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
                                    var status = Mathf.RoundToInt(currentAdvGirl.AgentData.StatsTable[(int)statusValue));
                                    // 翻译 Status.Type 的枚举名称
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
                        _expandDesires = GUILayout.Toggle(_expandDesires, T("Desires & Motivations"));
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
                                        // 翻译 Desire.Type 的枚举名称
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
                        _expandSkills = GUILayout.Toggle(_expandSkills, T("Flavor skills"));
                        if (_expandSkills)
                        {
                            foreach (FlavorSkill.Type typeValue in Enum.GetValues(typeof(FlavorSkill.Type)))
                            {
                                if (currentAdvGirl.ChaControl.fileGameInfo.flavorState.ContainsKey((int)typeValue))
                                {
                                    GUILayout.BeginHorizontal();
                                    {
                                        // 翻译 FlavorSkill.Type 的枚举名称
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
                if (GUILayout.Button(T("Reset talk time")))
                {
                    currentAdvGirl.AgentData.TalkMotivation = currentAdvGirl.AgentData.StatsTable[5];
                    currentAdvGirl.AgentData.WeaknessMotivation = 0;
                }
                GUI.enabled = true;

                GUILayout.Space(6);

                if (GUILayout.Button(T("Navigate to Actor's GameObject")))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message, T("Actor has no body assigned"));
                }

                if (GUILayout.Button(T("Open Actor in inspector")))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, T("Actor ") + currentAdvGirl.CharaName), true); // 翻译 Inspector 的标题

                if (GUILayout.Button(T("Inspect extended data")))
                {
                    // 翻译 Inspector 的标题
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.ChaControl?.chaFile), T("ExtData for ") + currentAdvGirl.CharaName), true);
                }
            }
            GUILayout.EndVertical();
        }
    }
}
