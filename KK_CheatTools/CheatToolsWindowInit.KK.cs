using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ActionGame;
using ActionGame.Chara;
using BepInEx.Configuration;
using Illusion.Component;
using Illusion.Game;
using Manager;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using UnityEngine.AI;
using LogLevel = BepInEx.Logging.LogLevel;
using Object = UnityEngine.Object;
using Newtonsoft.Json;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static SaveData.Heroine _currentVisibleGirl;
        private static bool _showSelectHeroineList;

        private static HFlag _hFlag;
        private static TalkScene _talkScene;
        private static HSprite _hSprite;
        private static Studio.Studio _studioInstance;
        private static Manager.Sound _soundInstance;
        private static Communication _communicationInstance;
        private static Scene _sceneInstance;
        private static Game _gameMgr;

        private static TriggerEnterExitEvent _playerEnterExitTrigger;
        private static string _setdesireId;
        private static string _setdesireValue;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        internal static ConfigEntry<bool> UnlockAllPositions;
        internal static ConfigEntry<bool> UnlockAllPositionsIndiscriminately;

        // 翻译相关成员
        private static Dictionary<string, string> _translations;
        private static string _currentLanguage = "en";
        private static string _pluginLocation;
        private static readonly Dictionary<string, string> CachedTranslations = new();
        private static Dictionary<string, string> SupportedLanguages;
        internal static ConfigEntry<string> SelectedLanguage;

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location;

            var config = instance.Config;
            SelectedLanguage = config.Bind("General", "Language", "en", "选择的 UI 语言");
            _currentLanguage = SelectedLanguage.Value;

            LoadSupportedLanguages();
            LoadLanguage(_currentLanguage);

            SelectedLanguage.SettingChanged += (sender, args) =>
            {
                _currentLanguage = SelectedLanguage.Value;
                LoadLanguage(_currentLanguage);
            };

            UnlockAllPositions = config.Bind("Cheats", "Unlock all H positions", false, T("UI.UnlockAllHPositionsDesc"));
            UnlockAllPositions.SettingChanged += (sender, args) => UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;
            UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;

            UnlockAllPositionsIndiscriminately = config.Bind("Cheats", "Unlock invalid H positions as well", false, T("UI.UnlockInvalidHPositionsDesc"));
            UnlockAllPositionsIndiscriminately.SettingChanged += (sender, args) => UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;
            UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;

            ToStringConverter.AddConverter<SaveData.Heroine>(heroine => !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.nickname);
            ToStringConverter.AddConverter<SaveData.CharaData.Params.Data>(d => $"[{d.key} | {d.value}]");

            NoclipFeature.InitializeNoclip(instance);

            CheatToolsWindow.OnShown += _ =>
            {
                _hFlag = Object.FindObjectOfType<HFlag>();
                _talkScene = Object.FindObjectOfType<TalkScene>();
                _hSprite = Object.FindObjectOfType<HSprite>();
                _studioInstance = Studio.Studio.Instance;
                _soundInstance = Manager.Sound.Instance;
                _communicationInstance = Communication.Instance;
                _sceneInstance = Scene.Instance;
                _gameMgr = Game.Instance;

                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(_gameMgr != null && _gameMgr.HeroineList.Count > 0 ? (Func<object>)(() => _gameMgr.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x))) : null, T("UI.HeroineList")),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                    new KeyValuePair<object, string>(_communicationInstance, "Manager.Communication.Instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                    new KeyValuePair<object, string>(_hFlag, "HFlag"),
                    new KeyValuePair<object, string>(_talkScene, "TalkScene"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _studioInstance == null && _gameMgr != null && !_gameMgr.saveData.isOpening, DrawPlayerCheats, T("UI.StartGameToSeePlayerCheats")));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hFlag != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGirlCheatMenu, null));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGlobalUnlocks, null));
        }

        #region 翻译系统

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

        private static void LoadLanguage(string langCode)
        {
            string langFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), $"lang_{langCode}.json");
            if (File.Exists(langFilePath))
            {
                try
                {
                    string json = File.ReadAllText(langFilePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    CheatToolsPlugin.Logger.LogInfo($"成功加载语言: {langCode}");
                }
                catch (Exception ex)
                {
                    CheatToolsPlugin.Logger.LogError($"加载 {langFilePath} 失败: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning($"未找到语言文件 {langFilePath}。");
                _translations = new Dictionary<string, string>();
            }
            CachedTranslations.Clear();
        }

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
                        CheatToolsPlugin.Logger.LogError($"无法加载回退语言 (en): {ex.Message}");
                    }
                }
            }

            CheatToolsPlugin.Logger.LogWarning($"语言 '{_currentLanguage}' 中缺少翻译键 '{key}'。");
            return key.Split('.').Last();
        }

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

        private static void DrawGlobalUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label(T("UI.GlobalUnlocks"));
            UnlockAllPositions.Value = GUILayout.Toggle(UnlockAllPositions.Value, T("UI.UnlockAllHPositions"));
            if (GUILayout.Button(T("UI.ObtainAllHPositions")))
                for (var i = 0; i < 10; i++)
                    _gameMgr.glSaveData.playHList[i] = new HashSet<int>(Enumerable.Range(0, 9999));
            if (GUILayout.Button(T("UI.UnlockAllWeddingPersonalities")))
                foreach (var personalityId in Singleton<Voice>.Instance.voiceInfoList.Select(x => x.No).Where(x => x >= 0))
                    _gameMgr.weddingData.personality.Add(personalityId);
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.HSceneControls"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.MaleGauge"), _hFlag.gaugeMale.ToString("N1")), GUILayout.Width(150));
                _hFlag.gaugeMale = GUILayout.HorizontalSlider(_hFlag.gaugeMale, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.FemaleGauge"), _hFlag.gaugeFemale.ToString("N1")), GUILayout.Width(150));
                _hFlag.gaugeFemale = GUILayout.HorizontalSlider(_hFlag.gaugeFemale, 0, 100);
            }
            GUILayout.EndHorizontal();

            if (_hSprite != null && GUILayout.Button(T("UI.ForceQuitHScene")))
            {
                Utils.Sound.Play(SystemSE.cancel);
                _hSprite.flags.click = HFlag.ClickKind.end;
                _hSprite.flags.isHSceneEnd = true;
                _hSprite.flags.numEnd = 0;
            }
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.GirlStats"));

            if (!_showSelectHeroineList)
            {
                var visibleGirls = GetCurrentVisibleGirls();

                for (var index = 0; index < visibleGirls.Length; index++)
                {
                    var girl = visibleGirls[index];
                    if (GUILayout.Button(string.Format(T("UI.SelectCurrentHeroine"), index, girl.Name)))
                        _currentVisibleGirl = girl;
                }

                var anyHeroines = _gameMgr.HeroineList != null && _gameMgr.HeroineList.Count > 0;
                if (anyHeroines && GUILayout.Button(T("UI.SelectFromHeroineList")))
                    _showSelectHeroineList = true;

                if (_currentVisibleGirl != null)
                {
                    GUILayout.Space(6);
                    DrawHeroineCheats(_currentVisibleGirl);
                }
                else
                {
                    GUILayout.Label(T("UI.SelectAGirlToAccessStats"));
                }

                if (anyHeroines)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label(T("UI.AffectAllHeroines"));
                        if (GUILayout.Button(T("UI.MakeEveryoneFriendly")))
                            foreach (var h in Game.Instance.HeroineList)
                            {
                                h.favor = 100;
                                h.anger = 0;
                                h.isAnger = false;
                            }
                        if (GUILayout.Button(T("UI.MakeEveryoneLovers")))
                            foreach (var h in Game.Instance.HeroineList)
                            {
                                h.anger = 0;
                                h.isAnger = false;
                                h.favor = 100;
                                h.lewdness = 100;
                                h.intimacy = 100;
                                h.isGirlfriend = true;
                                h.confessed = true;
                            }
                        if (GUILayout.Button(T("UI.MakeEveryoneClubMembers")))
                            foreach (var h in Game.Instance.HeroineList)
                                if (!h.isTeacher)
                                    h.isStaff = true;
                        if (GUILayout.Button(T("UI.MakeEveryoneVirgins")))
                            foreach (var h in Game.Instance.HeroineList) MakeVirgin(h);
                        if (GUILayout.Button(T("UI.MakeEveryoneInexperienced")))
                            foreach (var h in Game.Instance.HeroineList) MakeInexperienced(h);
                        if (GUILayout.Button(T("UI.MakeEveryoneExperienced")))
                            foreach (var h in Game.Instance.HeroineList) MakeExperienced(h);
                        if (GUILayout.Button(T("UI.MakeEveryonePerverted")))
                            foreach (var h in Game.Instance.HeroineList) MakeHorny(h);
                        if (GUILayout.Button(T("UI.ClearEveryonesDesires")))
                            foreach (var h in Game.Instance.HeroineList)
                                for (var i = 0; i < 31; i++)
                                    Game.Instance.actScene.actCtrl.SetDesire(i, h, 0);
                        if (GUILayout.Button(T("UI.EveryoneDesiresMasturbation")))
                            foreach (var h in Game.Instance.HeroineList)
                                Game.Instance.actScene.actCtrl.SetDesire(4, h, 100);
                        if (GUILayout.Button(T("UI.EveryoneDesiresLesbian")))
                            foreach (var h in Game.Instance.HeroineList)
                            {
                                Game.Instance.actScene.actCtrl.SetDesire(26, h, 100);
                                Game.Instance.actScene.actCtrl.SetDesire(27, h, 100);
                            }
                    }
                    GUILayout.EndVertical();
                }
            }
            else
            {
                if (_gameMgr.HeroineList == null || _gameMgr.HeroineList.Count == 0)
                {
                    _showSelectHeroineList = false;
                }
                else
                {
                    GUILayout.Label(T("UI.SelectOneOfTheHeroines"));
                    for (var index = 0; index < _gameMgr.HeroineList.Count; index++)
                    {
                        var heroine = _gameMgr.HeroineList[index];
                        if (GUILayout.Button(string.Format(T("UI.SelectHero from the heroine list")))
                            _currentVisibleGirl = heroine;
                            _showSelectHeroineList = false;
                    }
                }
            }
        }

        private static void DrawHeroineCheats(SaveData.Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical();
            {
                GUILayout.Label(string.Format(T("UI.SelectedGirlName"), currentAdvGirl.Name));

                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(string.Format(T("UI.Favor"), currentAdvGirl.favor), GUILayout.Width(60));
                        currentAdvGirl.favor = (int)GUILayout.HorizontalSlider(currentAdvGirl.favor, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(string.Format(T("UI.Lewd"), currentAdvGirl.lewdness), GUILayout.Width(60));
                        currentAdvGirl.lewdness = (int)GUILayout.HorizontalSlider(currentAdvGirl.lewdness, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(string.Format(T("UI.Anger"), currentAdvGirl.anger), GUILayout.Width(60));
                        currentAdvGirl.anger = (int)GUILayout.HorizontalSlider(currentAdvGirl.anger, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(string.Format(T("UI.Intimacy"), currentAdvGirl.intimacy), GUILayout.Width(60));
                        currentAdvGirl.intimacy = (int)GUILayout.HorizontalSlider(currentAdvGirl.intimacy, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.Space(4);

                GUILayout.Label(string.Format(T("UI.SexExperience"), T($"UI.HExp.{GetHExpText(currentAdvGirl)}")));
                GUILayout.Label(T("UI.SetTo"));
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(T("UI.Virgin"))) MakeVirgin(currentAdvGirl);
                    if (GUILayout.Button(T("UI.Inexp"))) MakeInexperienced(currentAdvGirl);
                    if (GUILayout.Button(T("UI.Exp"))) MakeExperienced(currentAdvGirl);
                    if (GUILayout.Button(T("UI.Horny"))) MakeHorny(currentAdvGirl);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                GUILayout.Label(T("UI.SetAllTouchExperience"));
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("0%")) SetGirlHExp(currentAdvGirl, 0f);
                    if (GUILayout.Button("50%")) SetGirlHExp(currentAdvGirl, 50f);
                    if (GUILayout.Button("100%")) SetGirlHExp(currentAdvGirl, 100f);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(8);

                if (GUILayout.Button(T("UI.ResetConversationTime")))
                    currentAdvGirl.talkTime = currentAdvGirl.talkTimeMax;

                var actCtrl = _gameMgr?.actScene?.actCtrl;
                if (actCtrl != null)
                {
                    var sortedDesires = Enum.GetValues(typeof(DesireEng)).Cast<DesireEng>()
                        .Select(i => new { id = i, value = actCtrl.GetDesire((int)i, currentAdvGirl) })
                        .Where(x => x.value > 5)
                        .OrderByDescending(x => x.value)
                        .Take(8);

                    var any = false;
                    foreach (var desire in sortedDesires)
                    {
                        if (!any)
                        {
                            GUILayout.Label(T("UI.DesiresAndStrengths"));
                            any = true;
                        }
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label($"{(int)desire.id} {T($"UI.Desire.{desire.id}")}");
                            GUILayout.FlexibleSpace();
                            GUILayout.Label($"{desire.value}%");
                            if (GUILayout
