using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration; // 用于 ConfigEntry
using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using sv08;
using UnityEngine;
using Newtonsoft.Json; // 用于 JSON 解析

namespace CheatTools
{
    public static partial class CheatToolsWindowInit
    {
        private static KeyValuePair<object, string>[] _openInInspectorButtons;
        private static bool _teleportUnlock;
        private static bool _posTweakForce;
        private static bool _posTweakUnlimited;
        private static float _posTweakDistance = 0.1f;

        // 翻译相关成员
        private static Dictionary<string, string> _translations; // 当前语言的键值对
        private static string _currentLanguage = "en"; // 默认英文
        private static string _pluginLocation; // 插件安装路径
        private static readonly Dictionary<string, string> CachedTranslations = new(); // 翻译缓存
        private static Dictionary<string, string> SupportedLanguages; // 支持的语言列表
        internal static ConfigEntry<string> SelectedLanguage; // 语言选择配置项

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location; // 存储插件路径

            var config = instance.Config;
            SelectedLanguage = config.Bind("General", "Language", "en", "选择的 UI 语言");
            _currentLanguage = SelectedLanguage.Value;

            // 加载支持的语言和当前语言
            LoadSupportedLanguages();
            LoadLanguage(_currentLanguage);

            // 语言配置变更时更新
            SelectedLanguage.SettingChanged += (sender, args) =>
            {
                _currentLanguage = SelectedLanguage.Value;
                LoadLanguage(_currentLanguage);
            };

            CheatToolsWindow.OnShown += _ =>
            {
                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>((Func<object>)(() => Game.Instance), "Game.Instance"),
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Application.productName.Contains("VR"), DrawMoveTools, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Game.Instance?.PlayerStatus != null, DrawPlayerUnlocks, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Game.Instance?.GameStatus != null, DrawGlobalUnlocks, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            Harmony.CreateAndPatchAll(typeof(Hooks));

            // 在窗口顶部添加语言选择器
            CheatToolsWindow.OnGUI += () => DrawLanguageSelector();
        }

        #region 翻译系统

        // 从 languages.json 加载支持的语言
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
                    CheatToolsPlugin.Logger.LogError($"无法加载 languages.json: {ex.Message}");
                    SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "zh_CN", "中文" } };
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning("未找到 languages.json，使用默认语言。");
                SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "zh_CN", "中文" } };
            }
        }

        // 加载指定语言的翻译文件
        private static void LoadLanguage(string langCode)
        {
            string langFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), $"lang_{langCode}.json");
            if (File.Exists(langFilePath))
            {
                try
                {
                    string json = File.ReadAllText(langFilePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    CheatToolsPlugin.Logger.LogInfo($"成功加载语言文件: {langFilePath}");
                }
                catch (JsonException ex)
                {
                    CheatToolsPlugin.Logger.LogError($"解析 {langFilePath} 时出错: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
                catch (IOException ex)
                {
                    CheatToolsPlugin.Logger.LogError($"读取 {langFilePath} 时出错: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning($"未找到语言文件 {langFilePath}。");
                _translations = new Dictionary<string, string>();
            }
            CachedTranslations.Clear(); // 语言切换时清空缓存
        }

        // 翻译函数，支持缓存和回退机制
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
                        CheatToolsPlugin.Logger.LogError($"无法加载回退语言 (en): {ex.Message}");
                    }
                }
            }

            CheatToolsPlugin.Logger.LogWarning($"语言 '{_currentLanguage}' 中缺少翻译键 '{key}'。");
            return key.Split('.').Last(); // 回退到键的最后部分
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

        #endregion

        private static void DrawMoveTools(CheatToolsWindow window)
        {
            GUILayout.Label(T("UI.VRMoveTools"));

            _posTweakForce = GUILayout.Toggle(_posTweakForce, T("UI.AlwaysAllowThumbstickMove"));
            _posTweakUnlimited = GUILayout.Toggle(_posTweakUnlimited, T("UI.UnlimitedThumbstickMove"));

            GUILayout.Label(string.Format(T("UI.ThumbstickMoveDistance"), _posTweakDistance.ToString("N2")));
            _posTweakDistance = GUILayout.HorizontalSlider(_posTweakDistance, 0.1f, 2f);

            _teleportUnlock = GUILayout.Toggle(_teleportUnlock, T("UI.UnlockTeleportToolDistance"));
        }

        private static void DrawPlayerUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label(T("UI.PlayerUnlocks"));

            var playerStatus = Game.Instance.PlayerStatus;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.SakurakoFavor"), playerStatus.favor_Sakurako), GUILayout.Width(120));
                playerStatus.favor_Sakurako = (int)GUILayout.HorizontalSlider(playerStatus.favor_Sakurako, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.UmekoFavor"), playerStatus.favor_Umeko), GUILayout.Width(120));
                playerStatus.favor_Umeko = (int)GUILayout.HorizontalSlider(playerStatus.favor_Umeko, 0, 100); // 修复：改为 favor_Umeko
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(T("UI.OpenAdvancedOptions")))
                Inspector.Instance.Push(new InstanceStackEntry(playerStatus, T("UI.PlayerStatus")), true);
        }

        private static void DrawGlobalUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label(T("UI.GlobalUnlocks"));

            var gameStatus = Game.Instance.GameStatus;

            if (GUILayout.Button(T("UI.UnlockAllChapters")))
            {
                var chapterClear = gameStatus.Chapter_Clear;
                for (var i = 0; i < chapterClear.Length; i++)
                    chapterClear[i] = true;
            }

            if (GUILayout.Button(T("UI.UnlockAllClothes")))
            {
                var partsUnlock = gameStatus.Parts_Unlock;
                var partsNew = gameStatus.Parts_New;
                for (var i = 0; i < partsUnlock.Length; i++)
                {
                    partsNew[i] = !partsUnlock[i];
                    partsUnlock[i] = true;
                }
            }

            if (GUILayout.Button(T("UI.MarkAllEndingsAndGamesAsSeen")))
            {
                void EnsureNonzeroCount(List<int> list)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] == 0)
                            list[i] = 1;
                    }
                }

                EnsureNonzeroCount(gameStatus.listEndingCount);
                EnsureNonzeroCount(gameStatus.listMiniGameClearCount);

                var tvf = Traverse.Create(gameStatus).Field("listMiniGamePlayCount");
                if (tvf.FieldExists())
                    EnsureNonzeroCount(tvf.GetValue<List<int>>());

                gameStatus.SetSystemFlag(ID_SFlag.SFlag_End, true);
            }
        }
    }
}
