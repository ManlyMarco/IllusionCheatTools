using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration; // Required for ConfigEntry
using BepInEx.Logging;
using Character;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using UnityEngine;
using Newtonsoft.Json; // Required for JSON parsing

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static KeyValuePair<object, string>[] _openInInspectorButtons;
        private static Human _currentVisibleGirl;

        // Translation-related members
        private static Dictionary<string, string> _translations; // Current language translations
        private static string _currentLanguage = "en"; // Default to English
        private static string _pluginLocation; // Plugin installation path
        private static readonly Dictionary<string, string> CachedTranslations = new(); // Cache for translations
        private static Dictionary<string, string> SupportedLanguages; // List of supported languages
        internal static ConfigEntry<string> SelectedLanguage; // Config entry for language persistence

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location; // Store plugin path

            var config = instance.Config;
            SelectedLanguage = config.Bind("General", "Language", "en", "Selected UI language");
            _currentLanguage = SelectedLanguage.Value;

            // Load supported languages and current language
            LoadSupportedLanguages();
            LoadLanguage(_currentLanguage);

            // Update language when config changes
            SelectedLanguage.SettingChanged += (sender, args) =>
            {
                _currentLanguage = SelectedLanguage.Value;
                LoadLanguage(_currentLanguage);
            };

            CheatToolsWindow.OnShown += _ =>
            {
                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(Manager.HSceneManager._instance, "Manager.HSceneManager.instance"),
                    new KeyValuePair<object, string>(HC.Scene.ADVScene._instance, "ADVScene.instance"),
                    new KeyValuePair<object, string>((object)Manager.Game._instance ?? typeof(Manager.Game), "Manager.Game"),
                    new KeyValuePair<object, string>(Manager.Game.SaveData, "Manager.Game.SaveData"),
                    new KeyValuePair<object, string>(typeof(Manager.Config), "Manager.Config"),
                    new KeyValuePair<object, string>((object)Manager.Scene._instance ?? typeof(Manager.Scene), "Manager.Scene"),
                    new KeyValuePair<object, string>((object)Manager.Sound._instance ?? typeof(Manager.Sound), "Manager.Sound"),
                    new KeyValuePair<object, string>(typeof(Manager.GameSystem), "Manager.GameSystem"),
                    new KeyValuePair<object, string>(typeof(Manager.Map), "Manager.Map")
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => H.HSceneFlagCtrl._instance != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => H.HSceneFlagCtrl._instance != null && Manager.HSceneManager._instance != null, DrawGirlCheatMenu, T("UI.UnableToEditCharacterStats")));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Manager.Game.SaveData != null, DrawGlobalUnlocks, null));

            // Add language selector to the window
            CheatToolsWindow.OnGUI += () => DrawLanguageSelector();
        }

        #region Translation System

        // Load supported languages from languages.json
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

        // Load language file for the specified language code
        private static void LoadLanguage(string langCode)
        {
            string langFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), $"lang_{langCode}.json");
            if (File.Exists(langFilePath))
            {
                try
                {
                    string json = File.ReadAllText(langFilePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    CheatToolsPlugin.Logger.LogInfo($"Loaded language: {langCode}");
                }
                catch (Exception ex)
                {
                    CheatToolsPlugin.Logger.LogError($"Failed to load {langFilePath}: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning($"Language file {langFilePath} not found.");
                _translations = new Dictionary<string, string>();
            }
            CachedTranslations.Clear(); // Clear cache on language change
        }

        // Translation function with caching and fallback
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
                string fallbackPath = Path.Combine(Path.GetDirectoryName(_pluginLocation), "lang_en.json");
                if (File.Exists(fallbackPath))
                {
                    try
                    {
                        string json = File.ReadAllText(fallbackPath);
                        var fallbackTranslations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (fallbackTranslations.TryGetValue(key, out value))
                        {
                            CachedTranslations[key] = value;
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        CheatToolsPlugin.Logger.LogError($"Failed to load fallback (en): {ex.Message}");
                    }
                }
            }

            CheatToolsPlugin.Logger.LogWarning($"Translation missing for '{key}' in '{_currentLanguage}'.");
            return key.Split('.').Last(); // Fallback to last part of key
        }

        // Draw language selector UI
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

        private static void DrawGlobalUnlocks(CheatToolsWindow obj)
        {
            GUILayout.Label(T("UI.DangerZone"));
            if (GUILayout.Button(T("UI.GetAllAchievements"), GUILayout.ExpandWidth(true)))
            {
                var achievementKeys = new List<int>();
                foreach (var key in Manager.Game.SaveData.Achievement.Keys)
                    achievementKeys.Add(key);
                foreach (var key in achievementKeys)
                    HC.SaveData.SaveData.UnlockAchievement(key);
            }
            if (GUILayout.Button(T("UI.UnlockAllPerks"), GUILayout.ExpandWidth(true)))
            {
                var achievementKeys = new List<int>();
                foreach (var key in Manager.Game.SaveData.AchievementExchange.Keys)
                    achievementKeys.Add(key);
                foreach (var key in achievementKeys)
                    HC.SaveData.SaveData.UnlockAchievementExchange(key);
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            var hScene = H.HSceneFlagCtrl._instance;
            GUILayout.Label(T("UI.HSceneControls"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.MaleGauge") + ": " + hScene.Feel_m.ToString("F2"), GUILayout.Width(150));
                hScene.Feel_m = GUILayout.HorizontalSlider(hScene.Feel_m, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.FemaleGauge") + ": " + hScene.Feel_f.ToString("F2"), GUILayout.Width(150));
                hScene.Feel_f = GUILayout.HorizontalSlider(hScene.Feel_f, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.PainGauge") + ": " + hScene.FeelPain.ToString("F2"), GUILayout.Width(150));
                hScene.FeelPain = GUILayout.HorizontalSlider(hScene.FeelPain, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.SpankGauge") + ": " + hScene.FeelSpnking.ToString("F2"), GUILayout.Width(150));
                hScene.FeelSpnking = GUILayout.HorizontalSlider(hScene.FeelSpnking, 0, 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(T("UI.OpenHSceneFlagsInInspector")))
                Inspector.Instance.Push(new InstanceStackEntry(hScene, T("UI.HSceneFlagCtrl")), true);
        }

        internal static string GetHeroineName(Human heroine)
        {
            return !string.IsNullOrEmpty(heroine.fileParam?.fullname) ? heroine.fileParam.fullname : heroine.name;
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.CharacterStatusEditor"));
            var visibleGirls = Manager.HSceneManager._instance.Females;

            for (var i = 0; i < visibleGirls.Count; i++)
            {
                var girl = visibleGirls[i];
                if (girl == null) continue;
                if (GUILayout.Button(string.Format(T("UI.SelectHeroine"), i, GetHeroineName(girl))))
                    _currentVisibleGirl = girl;
            }

            GUILayout.Space(6);

            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label(T("UI.SelectCharacterToEditStats"));
        }

        private static void DrawSingleGirlCheats(Human currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label(string.Format(T("UI.SelectedHeroineName"), GetHeroineName(currentAdvGirl)));
                GUILayout.Space(6);

                var gi = currentAdvGirl.fileGameInfo;
                if (gi != null)
                {
                    void DrawSingleStateBtn(ChaFileDefine.State state)
                    {
                        if (GUILayout.Button(T($"Game.State.{state}")))
                        {
                            gi.nowState = state;
                            gi.calcState = state;
                            gi.nowDrawState = state;
                            gi.Favor = state == ChaFileDefine.State.Favor ? 100 : Mathf.Min(gi.Favor, 90);
                            gi.Enjoyment = state == ChaFileDefine.State.Enjoyment ? 100 : Mathf.Min(gi.Enjoyment, 90);
                            gi.Aversion = state == ChaFileDefine.State.Aversion ? 100 : Mathf.Min(gi.Aversion, 90);
                            gi.Slavery = state == ChaFileDefine.State.Slavery ? 100 : Mathf.Min(gi.Slavery, 90);
                            gi.Broken = state == ChaFileDefine.State.Broken ? 100 : Mathf.Min(gi.Broken, 90);
                            gi.Dependence = state == ChaFileDefine.State.Dependence ? 100 : Mathf.Min(gi.Dependence, 90);
                        }
                    }

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(T("UI.CurrentState") + ": " + T($"Game.State.{gi.nowState}"));
                        GUILayout.FlexibleSpace();
                        DrawSingleStateBtn(ChaFileDefine.State.Blank);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        DrawSingleStateBtn(ChaFileDefine.State.Favor);
                        DrawSingleStateBtn(ChaFileDefine.State.Enjoyment);
                        DrawSingleStateBtn(ChaFileDefine.State.Aversion);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        DrawSingleStateBtn(ChaFileDefine.State.Slavery);
                        DrawSingleStateBtn(ChaFileDefine.State.Broken);
                        DrawSingleStateBtn(ChaFileDefine.State.Dependence);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(6);
                    GUILayout.Label(T("UI.Statistics"));

                    void ShowSingleSlider(string name, Action<int> set, Func<int> get)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            var status = get();
                            GUILayout.Label(T($"UI.{name}") + ": " + status, GUILayout.Width(120));
                            var newStatus = Mathf.RoundToInt(GUILayout.HorizontalSlider(status, 0, 100));
                            if (newStatus != status)
                                set(newStatus);
                        }
                        GUILayout.EndHorizontal();
                    }

                    void ShowSingleTextfield(string name, Action<int> set, Func<int> get)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label(T($"UI.{name}") + ": ", GUILayout.Width(120));
                            GUI.changed = false;
                            var status = get();
                            var textField = GUILayout.TextField(status.ToString());
                            if (GUI.changed && int.TryParse(textField, out var newStatus) && newStatus != status)
                                set(newStatus);
                            GUI.changed = false;
                        }
                        GUILayout.EndHorizontal();
                    }

                    ShowSingleSlider("Favor", i => gi.Favor = i, () => gi.Favor);
                    ShowSingleSlider("Enjoyment", i => gi.Enjoyment = i, () => gi.Enjoyment);
                    ShowSingleSlider("Aversion", i => gi.Aversion = i, () => gi.Aversion);
                    ShowSingleSlider("Slavery", i => gi.Slavery = i, () => gi.Slavery);
                    ShowSingleSlider("Broken", i => gi.Broken = i, () => gi.Broken);
                    ShowSingleSlider("Dependence", i => gi.Dependence = i, () => gi.Dependence);
                    ShowSingleSlider("Dirty", i => gi.Dirty = i, () => gi.Dirty);
                    ShowSingleSlider("Tiredness", i => gi.Tiredness = i, () => gi.Tiredness);
                    ShowSingleSlider("Toilet", i => gi.Toilet = i, () => gi.Toilet);
                    ShowSingleSlider("Libido", i => gi.Libido = i, () => gi.Libido);
                    ShowSingleSlider("Alertness", i => gi.alertness = i, () => gi.alertness);
                    ShowSingleTextfield("HCount", i => { gi.hCount = i; if (i == 0) gi.firstHFlag = true; }, () => gi.hCount);

                    if (GUILayout.Button(T("UI.ViewMoreStatsAndFlags")))
                        Inspector.Instance.Push(new InstanceStackEntry(gi, string.Format(T("UI.Heroine"), GetHeroineName(currentAdvGirl))), true);
                }

                GUILayout.Space(6);

                if (GUILayout.Button(T("UI.NavigateToHeroineGameObject")))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, T("UI.HeroineNoBodyAssigned"));
                }

                if (GUILayout.Button(T("UI.OpenHeroineInInspector")))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, string.Format(T("UI.Heroine"), GetHeroineName(currentAdvGirl))), true);
            }
            GUILayout.EndVertical();
        }
    }
}
