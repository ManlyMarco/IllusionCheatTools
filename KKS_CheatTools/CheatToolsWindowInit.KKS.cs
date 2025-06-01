using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using SaveData;
using UnityEngine;
using UnityEngine.AI;
using LogLevel = BepInEx.Logging.LogLevel;
using Object = UnityEngine.Object;
using Newtonsoft.Json;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static Heroine _currentVisibleGirl;
        private static bool _showSelectHeroineList;

        private static HFlag _hFlag;
        private static TalkScene _talkScene;
        private static HSprite _hSprite;
        private static Studio.Studio _studioInstance;
        private static Manager.Sound _soundInstance;
        private static Scene _sceneInstance;
        private static Game _gameMgr;

        private static TriggerEnterExitEvent _playerEnterExitTrigger;
        private static string _setdesireId;
        private static string _setdesireValue;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        private static readonly string[] _prayerNames =
        {
            "Nothing", "Topic drop bonus", "Find more topics?", "Safe topic bonus",
            "Girls want to talk", "Extra oil", "Confession bonus", "Find good topics next day",
            "Lewd topic bonus", "Lover visit at evening", "Girls want to H you", "Ask for sex bonus"
        };

        private static readonly int[] _prayerIds = { 0, 1, 2, 3, 4, 5, 6, 7, 1000, 1001, 1002, 1003 };

        private static readonly string[] _relationNames = { "Casual", "Friend", "Lover", "Bonded" };

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

            NoclipFeature.InitializeNoclip(instance);

            CheatToolsWindow.OnShown += _ =>
            {
                _hFlag = Object.FindObjectOfType<HFlag>();
                _talkScene = Object.FindObjectOfType<TalkScene>();
                _hSprite = Object.FindObjectOfType<HSprite>();
                _studioInstance = Studio.Studio.Instance;
                _soundInstance = Manager.Sound.instance;
                _sceneInstance = Scene.instance;
                _gameMgr = Game.instance;

                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(_gameMgr != null && Game.HeroineList.Count > 0 ? (Func<object>)(() => Game.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x))) : null, T("UI.HeroineList")),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.instance"),
                    new KeyValuePair<object, string>(_hFlag, "HFlag"),
                    new KeyValuePair<object, string>(_talkScene, "TalkScene"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _studioInstance == null && Game.saveData != null && !Game.saveData.isOpening, DrawPlayerCheats, T("UI.StartGameToSeePlayerCheats")));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hFlag != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGirlCheatMenu, null));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGlobalUnlocks, null));

            CheatToolsWindow.OnGUI += () => DrawLanguageSelector();
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
            if (GUILayout.Button(T("UI.ObtainAllHPositions")))
            {
                for (var i = 0; i < 10; i++)
                    Game.globalData.playHList[i] = new HashSet<int>(Enumerable.Range(0, 9999));
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.HSceneControls"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.MaleGauge") + ": " + _hFlag.gaugeMale.ToString("N1"), GUILayout.Width(150));
                _hFlag.gaugeMale = GUILayout.HorizontalSlider(_hFlag.gaugeMale, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.FemaleGauge") + ": " + _hFlag.gaugeFemale.ToString("N1"), GUILayout.Width(150));
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

                var anyHeroines = Game.HeroineList != null && Game.HeroineList.Count > 0;
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
                        if (GUILayout.Button(T("UI.MakeEveryoneFriends")))
                            foreach (var h in Game.HeroineList)
                            {
                                h.favor = 100;
                                h.isGirlfriend = false;
                            }
                        if (GUILayout.Button(T("UI.MakeEveryoneLovers")))
                            foreach (var h in Game.HeroineList)
                            {
                                h.favor = 75;
                                h.isGirlfriend = true;
                                h.confessed = true;
                            }
                        if (GUILayout.Button(T("UI.MakeEveryoneFullLovers")))
                            foreach (var h in Game.HeroineList)
                            {
                                h.favor = 150;
                                h.isGirlfriend = true;
                                h.confessed = true;
                            }
                        if (GUILayout.Button(T("UI.MakeEveryoneLewd")))
                            foreach (var h in Game.HeroineList) h.lewdness = 100;
                        if (GUILayout.Button(T("UI.MakeEveryoneVirgins")))
                            foreach (var h in Game.HeroineList) MakeVirgin(h);
                        if (GUILayout.Button(T("UI.MakeEveryoneInexperienced")))
                            foreach (var h in Game.HeroineList) MakeInexperienced(h);
                        if (GUILayout.Button(T("UI.MakeEveryoneExperienced")))
                            foreach (var h in Game.HeroineList) MakeExperienced(h);
                        if (GUILayout.Button(T("UI.MakeEveryonePerverted")))
                            foreach (var h in Game.HeroineList) MakeHorny(h);
                        if (GUILayout.Button(T("UI.ClearEveryonesDesires")))
                            foreach (var h in Game.HeroineList)
                                for (var i = 0; i < 31; i++)
                                    ActionScene.instance.actCtrl.SetDesire(i, h, 0);
                        if (GUILayout.Button(T("UI.EveryoneDesiresMasturbation")))
                            foreach (var h in Game.HeroineList)
                                ActionScene.instance.actCtrl.SetDesire(4, h, 100);
                        if (GUILayout.Button(T("UI.EveryoneDesiresLesbian")))
                            foreach (var h in Game.HeroineList)
                            {
                                ActionScene.instance.actCtrl.SetDesire(26, h, 100);
                                ActionScene.instance.actCtrl.SetDesire(27, h, 100);
                            }
                    }
                    GUILayout.EndVertical();
                }
            }
            else
            {
                if (Game.HeroineList == null || Game.HeroineList.Count == 0)
                {
                    _showSelectHeroineList = false;
                }
                else
                {
                    GUILayout.Label(T("UI.SelectOneOfTheHeroines"));
                    for (var index = 0; index < Game.HeroineList.Count; index++)
                    {
                        var heroine = Game.HeroineList[index];
                        if (GUILayout.Button(string.Format(T("UI.SelectHeroine"), index, heroine.Name)))
                        {
                            _currentVisibleGirl = heroine;
                            _showSelectHeroineList = false;
                        }
                    }
                }
            }
        }

        private static void DrawHeroineCheats(Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(T("UI.SelectedGirlName"));
                    GUILayout.Label(currentAdvGirl.Name);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(T("UI.RelationshipLevel"));
                    GUILayout.Label(T($"UI.Relation.{_relationNames[GetRelationSafe(currentAdvGirl)]}"));
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(string.Format(T("UI.Favor"), currentAdvGirl.favor), GUILayout.Width(70));
                    currentAdvGirl.favor = (int)GUILayout.HorizontalSlider(currentAdvGirl.favor, 0, currentAdvGirl.isGirlfriend ? 150 : 100);
                }
                GUILayout.EndHorizontal();

                currentAdvGirl.isFriend = GUILayout.Toggle(currentAdvGirl.isFriend, T("UI.IsAFriend"));
                currentAdvGirl.isGirlfriend = GUILayout.Toggle(currentAdvGirl.isGirlfriend, T("UI.IsAGirlfriend"));
                currentAdvGirl.confessed = GUILayout.Toggle(currentAdvGirl.confessed, T("UI.Confessed"));
                currentAdvGirl.isLunch = GUILayout.Toggle(currentAdvGirl.isLunch, T("UI.HadFirstLunch"));
                currentAdvGirl.isDayH = GUILayout.Toggle(currentAdvGirl.isDayH, T("UI.HadHToday"));

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(T("UI.DesireToVisit"), GUILayout.ExpandWidth(false));
                    GUI.changed = false;
                    var newCount = GUILayout.TextField(currentAdvGirl.visitDesire.ToString(), GUILayout.ExpandWidth(true));
                    if (GUI.changed && int.TryParse(newCount, out var newCountInt))
                        currentAdvGirl.visitDesire = Mathf.Max(newCountInt, 0);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(8);

                if (GUILayout.Button(T("UI.ResetConversationTime")))
                    currentAdvGirl.talkTime = currentAdvGirl.talkTimeMax;

                if (ActionScene.instance != null && currentAdvGirl.transform != null && GUILayout.Button(T("UI.FollowMe")))
                {
                    var npc = currentAdvGirl.transform.GetComponent<NPC>();
                    if (npc) ActionScene.instance.Player.ChaserSet(npc);
                    else CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, T("UI.NPCComponentNotFound"));
                }

                if (ActionScene.initialized && ActionScene.instance != null)
                {
                    var actCtrl = ActionScene.instance.actCtrl;
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
                            GUILayout.Label($"{(int)desire.id} {desire.id}");
                            GUILayout.FlexibleSpace();
                            GUILayout.Label($"{desire.value}%");
                            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                                actCtrl.SetDesire((int)desire.id, currentAdvGirl, 0);
                        }
                        GUILayout.EndHorizontal();
                    }
                    if (!any) GUILayout.Label(T("UI.HasNoDesires"));

                    if (GUILayout.Button(T("UI.ClearAllDesires")))
                        for (var i = 0; i < 31; i++)
                            actCtrl.SetDesire(i, currentAdvGirl, 0);

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(T("UI.SetDesire"), GUILayout.ExpandWidth(false));
                        _setdesireId = GUILayout.TextField(_setdesireId ?? "");
                        GUILayout.Label(T("UI.ToValue"), GUILayout.ExpandWidth(false));
                        _setdesireValue = GUILayout.TextField(_setdesireValue ?? "");
                        if (GUILayout.Button("OK", GUILayout.ExpandWidth(false)))
                        {
                            try
                            {
                                actCtrl.SetDesire((int)Enum.Parse(typeof(DesireEng), _setdesireId), currentAdvGirl, int.Parse(_setdesireValue));
                            }
                            catch (Exception e)
                            {
                                CheatToolsPlugin.Logger.LogMessage(T("UI.InvalidDesireInput") + e.Message);
                            }
                        }
                    }
                    GUILayout.EndHorizontal();

                    var wantsMast = actCtrl.GetDesire(4, currentAdvGirl) > 80;
                    if (!wantsMast && GUILayout.Button(T("UI.MakeDesireToMasturbate")))
                        actCtrl.SetDesire(4, currentAdvGirl, 100);

                    var wantsLes = actCtrl.GetDesire(26, currentAdvGirl) > 80;
                    if (!wantsLes && GUILayout.Button(T("UI.MakeDesireToLesbian")))
                    {
                        actCtrl.SetDesire(26, currentAdvGirl, 100);
                        actCtrl.SetDesire(27, currentAdvGirl, 100);
                    }
                }

                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(string.Format(T("UI.Lewd"), currentAdvGirl.lewdness), GUILayout.Width(70));
                    currentAdvGirl.lewdness = (int)GUILayout.HorizontalSlider(currentAdvGirl.lewdness, 0, 100);
                }
                GUILayout.EndHorizontal();

                GUI.changed = false;
                var isDangerousDay = GUILayout.Toggle(HFlag.GetMenstruation(currentAdvGirl.MenstruationDay) == HFlag.MenstruationType.危険日, T("UI.IsOnARiskyDay"));
                if (GUI.changed)
                    HFlag.SetMenstruation(currentAdvGirl, isDangerousDay ? HFlag.MenstruationType.危険日 : HFlag.MenstruationType.安全日);

                currentAdvGirl.isVirgin = GUILayout.Toggle(currentAdvGirl.isVirgin, T("UI.IsVirgin"));
                currentAdvGirl.isAnalVirgin = GUILayout.Toggle(currentAdvGirl.isAnalVirgin, T("UI.IsAnalVirgin"));

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(T("UI.SexCount"), GUILayout.ExpandWidth(false));
                    GUI.changed = false;
                    var newCount = GUILayout.TextField(currentAdvGirl.hCount.ToString(), GUILayout.ExpandWidth(true));
                    if (GUI.changed && int.TryParse(newCount, out var newCountInt))
                        currentAdvGirl.hCount = Mathf.Max(newCountInt, 0);
                }
                GUILayout.EndHorizontal();

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

                GUILayout.Space(4);

                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, T("UI.WontRefuseKiss"));
                currentAdvGirl.denial.massage = GUILayout.Toggle(currentAdvGirl.denial.massage, T("UI.WontRefuseMassage"));
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, T("UI.WontRefuseAnal"));
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, T("UI.WontRefuseVibrator"));
                currentAdvGirl.denial.notCondom = GUILayout.Toggle(currentAdvGirl.denial.notCondom, T("UI.InsertWithoutCondomOK"));

                GUILayout.Space(4);

                if (GUILayout.Button(T("UI.NavigateToHeroineGameObject")))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, T("UI.HeroineNoBodyAssigned"));
                }

                if (GUILayout.Button(T("UI.OpenHeroineInInspector")))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, string.Format(T("UI.Heroine"), currentAdvGirl.Name)), true);

                if (GUILayout.Button(T("UI.InspectExtendedData")))
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.charFile), string.Format(T("UI.ExtDataFor"), currentAdvGirl.Name)), true);
            }
            GUILayout.EndVertical();
        }

        private static void DrawPlayerCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.PlayerStats"));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.STR"), Game.Player.physical), GUILayout.Width(60));
                Game.Player.physical = (int)GUILayout.HorizontalSlider(Game.Player.physical, 0, 100);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.INT"), Game.Player.intellect), GUILayout.Width(60));
                Game.Player.intellect = (int)GUILayout.HorizontalSlider(Game.Player.intellect, 0, 100);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(string.Format(T("UI.H"), Game.Player.hentai), GUILayout.Width(60));
                Game.Player.hentai = (int)GUILayout.HorizontalSlider(Game.Player.hentai, 0, 100);
            }
            GUILayout.EndHorizontal();

            var cycle = Object.FindObjectsOfType<Cycle>().FirstOrDefault();
            if (cycle != null)
            {
                if (cycle.timerVisible)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(string.Format(T("UI.Time"), cycle.timer.ToString("N1")), GUILayout.Width(65));
                        var newVal = GUILayout.HorizontalSlider(cycle.timer, 0, Cycle.TIME_LIMIT);
                        if (Math.Abs(newVal - cycle.timer) > 0.09)
                            cycle._timer = newVal;
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(string.Format(T("UI.DayOfTheWeek"), T($"UI.Week.{cycle.nowWeek}")));
                    if (GUILayout.Button(T("UI.Next")))
                        cycle.Change(cycle.nowWeek.Next());
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.PlayerName"), GUILayout.ExpandWidth(false));
                Game.Player.parameter.lastname = GUILayout.TextField(Game.Player.parameter.lastname);
                Game.Player.parameter.firstname = GUILayout.TextField(Game.Player.parameter.firstname);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(T("UI.AddKoikatsuPoints")))
                Game.saveData.player.koikatsuPoint += 100;

            if (GUILayout.Button(T("UI.StopShameReactions")))
            {
                var actionMap = Object.FindObjectOfType<ActionMap>();
                if (actionMap != null)
                    foreach (var param in actionMap.infoDic.Values)
                        if (param.isWarning)
                        {
                            param.isWarning = false;
                            CheatToolsPlugin.Logger.Log(LogLevel.Message, string.Format(T("UI.DisablingShameReactions"), param.MapName));
                        }
            }

            GUI.changed = false;
            var playerIsNoticeable = _playerEnterExitTrigger == null || _playerEnterExitTrigger.enabled;
            playerIsNoticeable = !GUILayout.Toggle(!playerIsNoticeable, T("UI.MakePlayerUnnoticeable"));
            if (GUI.changed)
            {
                var actionMap = Object.FindObjectOfType<ActionScene>();
                if (actionMap != null)
                {
                    _playerEnterExitTrigger = actionMap.Player.noticeArea;
                    _playerEnterExitTrigger.enabled = playerIsNoticeable;
                }
            }

            NoclipFeature.NoclipMode = GUILayout.Toggle(NoclipFeature.NoclipMode, T("UI.EnablePlayerNoclip"));

            if (GUILayout.Button(T("UI.OpenPlayerDataInInspector")))
                Inspector.Instance.Push(new InstanceStackEntry(Game.saveData.player, T("UI.PlayerData")), true);

            GUILayout.BeginVertical(GUI.skin.box);
            {
                var currentPrayer = Game.saveData.prayedResult;
                var prayerIndex = Array.IndexOf(_prayerIds, currentPrayer);
                var prayerName = prayerIndex >= 0 ? T($"UI.Prayer.{_prayerNames[prayerIndex]}") : T("UI.Unknown");

                GUILayout.Label(string.Format(T("UI.PrayerBonus"), prayerName));

                GUI.changed = false;
                var result = GUILayout.SelectionGrid(prayerIndex, _prayerNames.Select(n => T($"UI.Prayer.{n}")).ToArray(), 1);
                if (GUI.changed)
                    Game.saveData.prayedResult = _prayerIds[result];
            }
            GUILayout.EndVertical();
        }

        private static void MakeHorny(Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = Mathf.Max(1, currentAdvGirl.hCount);
            currentAdvGirl.isVirgin = false;
            SetGirlHExp(currentAdvGirl, 100f);
            currentAdvGirl.lewdness = 100;
        }

        private static void MakeExperienced(Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = Mathf.Max(1, currentAdvGirl.hCount);
            currentAdvGirl.isVirgin = false;
            SetGirlHExp(currentAdvGirl, 100f);
            currentAdvGirl.lewdness = Mathf.Min(99, currentAdvGirl.lewdness);
        }

        private static void MakeInexperienced(Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = Mathf.Max(1, currentAdvGirl.hCount);
            currentAdvGirl.isVirgin = false;
            currentAdvGirl.countKokanH = 50;
            SetGirlHExp(currentAdvGirl, 0);
        }

        private static void MakeVirgin(Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = 0;
            currentAdvGirl.isVirgin = true;
            SetGirlHExp(currentAdvGirl, 0);
        }

        private static void SetGirlHExp(Heroine girl, float amount)
        {
            girl.houshiExp = amount;
            girl.countKokanH = amount;
            girl.countAnalH = amount;
            for (var i = 0; i < girl.hAreaExps.Length; i++)
                girl.hAreaExps[i] = amount;
            for (var i = 0; i < girl.massageExps.Length; i++)
                girl.massageExps[i] = amount;
            girl.hExp = amount;
        }

        private static Heroine[] GetCurrentVisibleGirls()
        {
            if (_talkScene != null && _talkScene.targetHeroine != null)
                return new[] { _talkScene.targetHeroine };

            if (_hFlag != null && _hFlag.lstHeroine != null && _hFlag.lstHeroine.Count > 0)
                return _hFlag.lstHeroine.ToArray();

            if (Game.initialized && ActionScene.initialized && ActionScene.instance.advScene != null)
            {
                var advScene = ActionScene.instance.advScene;
                if (advScene.Scenario != null && advScene.Scenario.currentHeroine != null)
                    return new[] { advScene.Scenario.currentHeroine };
                if (advScene.nowScene is TalkScene s && s.targetHeroine != null)
                    return new[] { s.targetHeroine };
            }

            return Array.Empty<Heroine>();
        }

        private static string GetHExpText(Heroine currentAdvGirl)
        {
            return ((HExperienceKindEng)currentAdvGirl.HExperience).ToString();
        }

        private static int GetRelationSafe(Heroine heroine)
        {
            if (heroine.isGirlfriend)
                return heroine.favor >= 150 ? 3 : 2;
            return heroine.isFriend ? 1 : 0;
        }
    }
}
