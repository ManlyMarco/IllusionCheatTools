using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Actor;
using AIChara;
using Manager;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;
using Newtonsoft.Json; // Required for JSON parsing

namespace CheatTools
{
    public static partial class CheatToolsWindowInit
    {
        private static Heroine _currentVisibleGirl;
        private static Action<Heroine> _onGirlStatsChanged;

        private static Studio.Studio _studioInstance;
        private static Manager.Sound _soundInstance;
        private static Scene _sceneInstance;
        private static Game _gameMgr;
        private static BaseMap _baseMap;
        private static HSceneFlagCtrl _hScene;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        // Translation-related members
        private static Dictionary<string, string> _translations; // Stores current language key-value pairs
        private static string _currentLanguage = "en"; // Default to English
        private static string _pluginLocation; // Plugin installation path
        private static readonly Dictionary<string, string> CachedTranslations = new(); // Cache for translations

        // Supported languages list
        private static Dictionary<string, string> SupportedLanguages;

        internal static ConfigEntry<string> SelectedLanguage;

        private static string GetHeroineName(Heroine heroine)
        {
            return !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.ChaName;
        }

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location; // Store plugin path

            var config = instance.Config;

            SelectedLanguage = config.Bind("General", "Language", "en", "Selected UI language");
            _currentLanguage = SelectedLanguage.Value;

            // Load supported languages and current language
            LoadSupportedLanguages();
            LoadLanguage(_currentLanguage);

            SelectedLanguage.SettingChanged += (sender, args) =>
            {
                _currentLanguage = SelectedLanguage.Value;
                LoadLanguage(_currentLanguage);
            };

            ToStringConverter.AddConverter<Heroine>(GetHeroineName);
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

            CheatToolsWindow.OnShown += _ =>
            {
                _studioInstance = Studio.Studio.IsInstance() ? Studio.Studio.Instance : null;
                _soundInstance = Manager.Sound.instance;
                _sceneInstance = Scene.instance;
                _gameMgr = Game.IsInstance() ? Game.Instance : null;
                _baseMap = BaseMap.instance;
                _hScene = HSceneFlagCtrl.IsInstance() ? HSceneFlagCtrl.Instance : null;

                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(
                        _gameMgr != null && _gameMgr.heroineList.Count > 0 ? (Func<object>)(() => _gameMgr.heroineList.Select(x => new ReadonlyCacheEntry(GetHeroineName(x), x))) : null, T("UI.HeroineList")),
                    new KeyValuePair<object, string>(ADVManager.IsInstance() ? ADVManager.Instance : null, "Manager.ADVManager.Instance"),
                    new KeyValuePair<object, string>(_baseMap, "Manager.BaseMap.instance"),
                    new KeyValuePair<object, string>(Character.IsInstance() ? Character.Instance : null, "Manager.Character.Instance"),
                    new KeyValuePair<object, string>(typeof(Manager.Config), "Manager.Config"),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(GameSystem.IsInstance() ? GameSystem.Instance : null, "Manager.GameSystem.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.instance"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hScene != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _baseMap != null && (_hScene != null || Singleton<LobbySceneManager>.IsInstance()), DrawGirlCheatMenu, T("UI.UnableToEditCharacterStats")));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _studioInstance == null && _gameMgr != null && _gameMgr.saveData != null, DrawGlobalUnlocks, null));

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Hooks));

            // Add language selector to the window
            CheatToolsWindow.OnGUI += () => DrawLanguageSelector();
        }

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
            CachedTranslations.Clear(); // Clear cache when language changes
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
            return key.Split('.').Last(); // Return last part of the key as fallback
        }

        // Draw language selector at the top of the window
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

        private static void DrawGlobalUnlocks(CheatToolsWindow obj)
        {
            GUILayout.Label(T("UI.DangerZone"));
            if (GUILayout.Button(T("UI.GetAllAchievements"), GUILayout.ExpandWidth(true)))
            {
                foreach (var achievementKey in _gameMgr.saveData.achievement.Keys.ToList())
                    SaveData.SetAchievementAchieve(achievementKey);
            }
            if (GUILayout.Button(T("UI.UnlockAllPerks"), GUILayout.ExpandWidth(true)))
            {
                foreach (var achievementKey in _gameMgr.saveData.achievementExchange.Keys.ToList())
                    SaveData.SetAchievementExchangeRelease(achievementKey);
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
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.PainGauge") + ": " + _hScene.feelPain.ToString("F2"), GUILayout.Width(150));
                _hScene.feelPain = GUILayout.HorizontalSlider(_hScene.feelPain, 0, 1);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button(T("UI.OpenHSceneFlagsInInspector")))
                Inspector.Instance.Push(new InstanceStackEntry(_hScene, T("UI.HSceneFlagCtrl")), true);
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.CharacterStatusEditor"));
            if (!Singleton<LobbySceneManager>.IsInstance())
            {
                var visibleGirls = _gameMgr.heroineList;
                for (var i = 0; i < visibleGirls.Count; i++)
                {
                    var girl = visibleGirls[i];
                    if (GUILayout.Button(string.Format(T("UI.SelectHeroine"), i, GetHeroineName(girl))))
                        _currentVisibleGirl = girl;
                }
                GUILayout.Space(6);
            }
            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label(T("UI.SelectCharacterToEditStats"));
        }

        private static void DrawSingleGirlCheats(Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label(string.Format(T("UI.SelectedHeroineName"), GetHeroineName(currentAdvGirl)));
                GUILayout.Space(6);
                if (currentAdvGirl.chaCtrl != null && currentAdvGirl.chaCtrl.fileGameInfo2 != null)
                {
                    var anyChanges = false;
                    var gi2 = currentAdvGirl.gameinfo2;
                    void DrawSingleStateBtn(ChaFileDefine.State state)
                    {
                        if (GUILayout.Button(T($"Game.State.{state}")))
                        {
                            gi2.nowState = state;
                            gi2.calcState = state;
                            gi2.nowDrawState = state;
                            gi2.Favor = state == ChaFileDefine.State.Favor ? 100 : Mathf.Min(gi2.Favor, 90);
                            gi2.Enjoyment = state == ChaFileDefine.State.Enjoyment ? 100 : Mathf.Min(gi2.Enjoyment, 90);
                            gi2.Aversion = state == ChaFileDefine.State.Aversion ? 100 : Mathf.Min(gi2.Aversion, 90);
                            gi2.Slavery = state == ChaFileDefine.State.Slavery ? 100 : Mathf.Min(gi2.Slavery, 90);
                            gi2.Broken = state == ChaFileDefine.State.Broken ? 100 : Mathf.Min(gi2.Broken, 90);
                            gi2.Dependence = state == ChaFileDefine.State.Dependence ? 100 : Mathf.Min(gi2.Dependence, 90);
                            anyChanges = true;
                        }
                    }
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(T("UI.CurrentState") + ": " + T($"Game.State.{gi2.nowState}"));
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
                            {
                                set(newStatus);
                                anyChanges = true;
                            }
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
                            {
                                set(newStatus);
                                anyChanges = true;
                            }
                            GUI.changed = false;
                        }
                        GUILayout.EndHorizontal();
                    }
                    ShowSingleSlider("Favor", i => gi2.Favor = i, () => gi2.Favor);
                    ShowSingleSlider("Enjoyment", i => gi2.Enjoyment = i, () => gi2.Enjoyment);
                    ShowSingleSlider("Aversion", i => gi2.Aversion = i, () => gi2.Aversion);
                    ShowSingleSlider("Slavery", i => gi2.Slavery = i, () => gi2.Slavery);
                    ShowSingleSlider("Broken", i => gi2.Broken = i, () => gi2.Broken);
                    ShowSingleSlider("Dependence", i => gi2.Dependence = i, () => gi2.Dependence);
                    ShowSingleSlider("Dirty", i => gi2.Dirty = i, () => gi2.Dirty);
                    ShowSingleSlider("Tiredness", i => gi2.Tiredness = i, () => gi2.Tiredness);
                    ShowSingleSlider("Toilet", i => gi2.Toilet = i, () => gi2.Toilet);
                    ShowSingleSlider("Libido", i => gi2.Libido = i, () => gi2.Libido);
                    ShowSingleTextfield("HCount", i => { gi2.hCount = i; if (i == 0) gi2.firstHFlag = true; }, () => gi2.hCount);
                    if (anyChanges)
                        _onGirlStatsChanged(_currentVisibleGirl);
                    if (GUILayout.Button(T("UI.ViewMoreStatsAndFlags")))
                        Inspector.Instance.Push(new InstanceStackEntry(gi2, string.Format(T("UI.Heroine"), GetHeroineName(currentAdvGirl))), true);
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
                if (GUILayout.Button(T("UI.InspectExtendedData")))
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.chaFile), string.Format(T("UI.ExtDataFor"), currentAdvGirl.Name)), true);
            }
            GUILayout.EndVertical();
        }
    }
}
