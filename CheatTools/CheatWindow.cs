using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActionGame;
using BepInEx.Logging;
using Manager;
using UnityEngine;
using Logger = BepInEx.Logger;
using Object = UnityEngine.Object;

namespace CheatTools
{
    public class CheatWindow
    {
        private const int ScreenOffset = 20;
        private readonly string[] _hExpNames = {"First time", "Inexperienced", "Experienced", "Perverted"};

        private readonly Inspector _inspector = new Inspector();

        private Vector2 _cheatsScrollPos;
        private Rect _cheatWindowRect;
        private Rect _screenRect;
        private readonly string _mainWindowTitle;
        private bool _show;

        private SaveData.Heroine _currentVisibleGirl;
        private Game _gameMgr;
        private string _typeNameToSearchBox = "Specify type name to search";

        public CheatWindow()
        {
            _mainWindowTitle = "Cheat Tools" + Assembly.GetExecutingAssembly().GetName().Version;
        }

        public Game GameMgr => _gameMgr ?? (_gameMgr = Game.Instance);

        public bool Show
        {
            get => _show;
            set
            {
                _show = value;
                if (value)
                    SetWindowSizes();
            }
        }

        private void CheatWindowContents(int id)
        {
            try
            {
                var hFlag = Object.FindObjectOfType<HFlag>();
                var talkScene = Object.FindObjectOfType<TalkScene>();

                _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
                {
                    if (GameMgr != null && !GameMgr.saveData.isOpening)
                        DrawPlayerCheats();
                    else
                        GUILayout.Label("Start the game to see player cheats");

                    if (hFlag != null)
                        DrawHSceneCheats(hFlag);

                    //Now can quit first time H scene
                    var hSprite = Object.FindObjectOfType<HSprite>();
                    if (hSprite != null)
                        if (GUILayout.Button("Quit H scene (alpha)"))
                            hSprite.btnEnd.onClick.Invoke();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Current girl stats");

                        _currentVisibleGirl = GetCurrentAdvHeroine() ?? GetCurrentTalkSceneHeroine(talkScene) ??
                                              GetCurrentHflagHeroine(hFlag) ?? _currentVisibleGirl;

                        if (_currentVisibleGirl != null)
                            DrawCurrentHeroineCheats(_currentVisibleGirl);
                        else
                            GUILayout.Label("Talk to a girl to access her stats");
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
                        GUILayout.Label((int) Math.Round(Time.timeScale * 100) + "%", GUILayout.Width(35));
                        Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0, 5, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                            Time.timeScale = 1;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Open in inspector");
                        foreach (var obj in new[]
                        {
                            new KeyValuePair<object, string>(
                                GameMgr?.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x)),
                                "Heroine list"),
                            new KeyValuePair<object, string>(GameMgr, "Manager.Game.Instance"),
                            new KeyValuePair<object, string>(Scene.Instance, "Manager.Scene.Instance"),
                            new KeyValuePair<object, string>(Communication.Instance, "Manager.Communication.Instance"),
                            new KeyValuePair<object, string>(Manager.Sound.Instance, "Manager.Sound.Instance"),
                            new KeyValuePair<object, string>(hFlag, "HFlag"),
                            new KeyValuePair<object, string>(talkScene, "TalkScene"),
                            new KeyValuePair<object, string>(Studio.Studio.Instance, "Studio.Instance"),
                            new KeyValuePair<object, string>(Utilities.GetRootGoScanner(), "Root Objects")
                        })
                        {
                            if (obj.Key == null) continue;
                            if (GUILayout.Button(obj.Value))
                            {
                                _inspector.InspectorClear();
                                _inspector.InspectorPush(new InspectorStackEntry(obj.Key, obj.Value));
                            }
                        }

                        GUILayout.Space(8);

                        _typeNameToSearchBox = GUILayout.TextField(_typeNameToSearchBox, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Find objects of this type"))
                        {
                            if (string.IsNullOrEmpty(_typeNameToSearchBox))
                            {
                                _typeNameToSearchBox = "Specify type name to search";
                            }
                            else
                            {
                                var matchedTypes = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(x => x.GetTypes())
                                    .Where(x => x.GetFriendlyName().Equals(_typeNameToSearchBox, StringComparison.OrdinalIgnoreCase));

                                var objects = new List<Object>();
                                foreach (var matchedType in matchedTypes)
                                    objects.AddRange(Object.FindObjectsOfType(matchedType) ?? Enumerable.Empty<Object>());

                                _inspector.InspectorClear();
                                _inspector.InspectorPush(new InspectorStackEntry(objects.AsEnumerable(), "Objects of type " + _typeNameToSearchBox));
                            }
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "[CheatTools] CheatWindow crash: " + ex.Message);
            }

            GUI.DragWindow();
        }

        private static void DrawHSceneCheats(HFlag hFlag)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("H scene controls");

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Male Gauge: " + hFlag.gaugeMale.ToString("N1"), GUILayout.Width(150));
                    hFlag.gaugeMale = GUILayout.HorizontalSlider(hFlag.gaugeMale, 0, 100);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Female Gauge: " + hFlag.gaugeFemale.ToString("N1"), GUILayout.Width(150));
                    hFlag.gaugeFemale = GUILayout.HorizontalSlider(hFlag.gaugeFemale, 0, 100);
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawCurrentHeroineCheats(SaveData.Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical();
            {
                GUILayout.Label("Name: " + currentAdvGirl.Name);

                GUILayout.Label("Sex experience: " + GetHExpText(currentAdvGirl));

                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Favor: " + currentAdvGirl.favor, GUILayout.Width(60));
                        currentAdvGirl.favor = (int) GUILayout.HorizontalSlider(currentAdvGirl.favor, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Lewd: " + currentAdvGirl.lewdness, GUILayout.Width(60));
                        currentAdvGirl.lewdness = (int) GUILayout.HorizontalSlider(currentAdvGirl.lewdness, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Anger: " + currentAdvGirl.anger, GUILayout.Width(60));
                        currentAdvGirl.anger = (int) GUILayout.HorizontalSlider(currentAdvGirl.anger, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Set all H experience to");
                    if (GUILayout.Button("0%"))
                        SetGirlHExp(currentAdvGirl, 0f);
                    if (GUILayout.Button("50%"))
                        SetGirlHExp(currentAdvGirl, 50f);
                    if (GUILayout.Button("99%"))
                        SetGirlHExp(currentAdvGirl, 99f);
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Reset conversation time"))
                    currentAdvGirl.talkTime = currentAdvGirl.talkTimeMax;

                currentAdvGirl.isVirgin = GUILayout.Toggle(currentAdvGirl.isVirgin, "isVirgin");
                currentAdvGirl.isAnalVirgin = GUILayout.Toggle(currentAdvGirl.isAnalVirgin, "isAnalVirgin");
                currentAdvGirl.isAnger = GUILayout.Toggle(currentAdvGirl.isAnger, "Is angry");
                currentAdvGirl.isDate = GUILayout.Toggle(currentAdvGirl.isDate, "Date promised");
                //currentAdvGirl.isFirstGirlfriend = GUILayout.Toggle(currentAdvGirl.isFirstGirlfriend, "isFirstGirlfriend");
                currentAdvGirl.isGirlfriend = GUILayout.Toggle(currentAdvGirl.isGirlfriend, "isGirlfriend");

                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, "Won't refuse kiss");
                currentAdvGirl.denial.massage =
                    GUILayout.Toggle(currentAdvGirl.denial.massage, "Won't refuse strong massage");
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, "Won't refuse anal");
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, "Won't refuse vibrator");
                currentAdvGirl.denial.notCondom =
                    GUILayout.Toggle(currentAdvGirl.denial.notCondom, "Insert w/o condom OK");

                if (GUILayout.Button("Open current girl in inspector"))
                {
                    _inspector.InspectorClear();
                    _inspector.InspectorPush(new InspectorStackEntry(currentAdvGirl, "Heroine " + currentAdvGirl.Name));
                }
            }
            GUILayout.EndVertical();
        }

        private static void SetGirlHExp(SaveData.Heroine girl, float amount)
        {
            girl.houshiExp = amount;
            for (var i = 0; i < girl.hAreaExps.Length; i++)
                girl.hAreaExps[i] = amount;
            for (var i = 0; i < girl.massageExps.Length; i++)
                girl.massageExps[i] = amount;
        }

        private void DrawPlayerCheats()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Player stats");

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("STR: " + GameMgr.Player.physical, GUILayout.Width(60));
                    GameMgr.Player.physical = (int) GUILayout.HorizontalSlider(GameMgr.Player.physical, 0, 100);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("INT: " + GameMgr.Player.intellect, GUILayout.Width(60));
                        GameMgr.Player.intellect = (int) GUILayout.HorizontalSlider(GameMgr.Player.intellect, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("H: " + GameMgr.Player.hentai, GUILayout.Width(60));
                        GameMgr.Player.hentai = (int) GUILayout.HorizontalSlider(GameMgr.Player.hentai, 0, 100);
                    }
                    GUILayout.EndHorizontal();

                    var cycle = Object.FindObjectsOfType<Cycle>().FirstOrDefault();
                    if (cycle != null)
                    {
                        if (cycle.timerVisible)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label("Time: " + cycle.timer.ToString("N1"), GUILayout.Width(65));
                                var newVal = GUILayout.HorizontalSlider(cycle.timer, 0, Cycle.TIME_LIMIT);
                                if (Math.Abs(newVal - cycle.timer) > 0.09)
                                    typeof(Cycle)
                                        .GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)?
                                        .SetValue(cycle, newVal);
                            }
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Day of the week: " + cycle.nowWeek);
                            if (GUILayout.Button("Next"))
                                cycle.Change(cycle.nowWeek.Next());
                        }
                        GUILayout.EndHorizontal();
                    }
                }

                if (GUILayout.Button("Add 10000 club points (+1 level)"))
                    GameMgr.saveData.clubReport.comAdd += 10000;

                if (GUILayout.Button("Open player data in inspector"))
                {
                    _inspector.InspectorClear();
                    _inspector.InspectorPush(new InspectorStackEntry(GameMgr.saveData.player, "Player data"));
                }
            }
            GUILayout.EndVertical();
        }

        private SaveData.Heroine GetCurrentAdvHeroine()
        {
            try
            {
                var nowScene = GameMgr?.actScene?.AdvScene?.nowScene;
                var currentAdvGirl =
                    nowScene?.GetType().GetField("m_TargetHeroine", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(nowScene) as SaveData.Heroine;
                return currentAdvGirl;
            }
            catch
            {
                return null;
            }
        }

        private static SaveData.Heroine GetCurrentHflagHeroine(HFlag hFlag)
        {
            return hFlag?.lstHeroine?.FirstOrDefault();
        }

        private static SaveData.Heroine GetCurrentTalkSceneHeroine(TalkScene talkScene)
        {
            return talkScene?.targetHeroine;
        }

        private string GetHExpText(SaveData.Heroine currentAdvGirl)
        {
            return _hExpNames[(int) currentAdvGirl.HExperience];
        }

        public void DisplayCheatWindow()
        {
            if (!Show) return;

            _cheatWindowRect = GUILayout.Window(591, _cheatWindowRect, CheatWindowContents, _mainWindowTitle);

            _inspector.DisplayInspector();
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            InitializeWindowSize(_screenRect);
        }

        private void InitializeWindowSize(Rect screenRect)
        {
            if (_cheatWindowRect.IsDefault())
                _cheatWindowRect = new Rect(screenRect.width - 50 - 270, 100, 270, 380);

            _inspector.InitializeInspectorWindowSize(_screenRect);
        }

        public void OnUpdate()
        {
            _inspector.InspectorUpdate();
        }
    }
}