using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActionGame;
using HarmonyLib;
using Illusion.Component;
using Illusion.Game;
using Manager;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;
using Object = UnityEngine.Object;

namespace CheatTools
{
    public class CheatToolsWindow
    {
        private const int ScreenOffset = 20;
        private readonly string[] _hExpNames = { "First time", "Inexperienced", "Experienced", "Perverted" };

        private readonly RuntimeUnityEditorCore _editor;

        private readonly string _mainWindowTitle;
        private Vector2 _cheatsScrollPos;
        private Rect _cheatWindowRect;
        private Rect _screenRect;
        private bool _show;

        private SaveData.Heroine _currentVisibleGirl;
        private bool _showSelectHeroineList;

        private HFlag _hFlag;
        private TalkScene _talkScene;
        private HSprite _hSprite;
        private Studio.Studio _studioInstance;
        private Manager.Sound _soundInstance;
        private Communication _communicationInstance;
        private Scene _sceneInstance;
        private Game _gameMgr;

        private readonly Func<object> _funcGetHeroines;
        private readonly Func<object> _funcGetRootGos;
        private TriggerEnterExitEvent _playerEnterExitTrigger;

        public CheatToolsWindow(RuntimeUnityEditorCore editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            ToStringConverter.AddConverter<SaveData.Heroine>(heroine => !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.nickname);
            ToStringConverter.AddConverter<SaveData.CharaData.Params.Data>(d => $"[{d.key} | {d.value}]");

            _mainWindowTitle = "Cheat Tools " + Assembly.GetExecutingAssembly().GetName().Version;

            _funcGetHeroines = () => _gameMgr.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x));
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

                _hFlag = Object.FindObjectOfType<HFlag>();
                _talkScene = Object.FindObjectOfType<TalkScene>();
                _hSprite = Object.FindObjectOfType<HSprite>();
                _studioInstance = Studio.Studio.Instance;
                _soundInstance = Manager.Sound.Instance;
                _communicationInstance = Communication.Instance;
                _sceneInstance = Scene.Instance;
                _gameMgr = Game.Instance;
            }
        }

        private void CheatWindowContents(int id)
        {
            _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
            {
                if (_studioInstance == null)
                    DrawPlayerCheats();

                if (_hFlag != null)
                    DrawHSceneCheats(_hFlag);

                if (_hSprite != null)
                {
                    if (GUILayout.Button("Force quit H scene"))
                    {
                        Utils.Sound.Play(SystemSE.cancel);
                        _hSprite.flags.click = HFlag.ClickKind.end;
                        _hSprite.flags.isHSceneEnd = true;
                        _hSprite.flags.numEnd = 0;
                    }
                }

                if (_gameMgr != null)
                    DrawGirlCheatMenu();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
                    GUILayout.Label((int)Math.Round(Time.timeScale * 100) + "%", GUILayout.Width(35));
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
                            new KeyValuePair<object, string>(_gameMgr != null && _gameMgr.HeroineList.Count > 0 ? _funcGetHeroines : null, "Heroine list"),
                            new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                            new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                            new KeyValuePair<object, string>(_communicationInstance, "Manager.Communication.Instance"),
                            new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                            new KeyValuePair<object, string>(_hFlag, "HFlag"),
                            new KeyValuePair<object, string>(_talkScene, "TalkScene"),
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

                if (_gameMgr != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Global unlocks (might need a reload)");

                        CheatToolsPlugin.UnlockAllPositions.Value = GUILayout.Toggle(CheatToolsPlugin.UnlockAllPositions.Value, "Unlock all H positions");

                        if (GUILayout.Button("Obtain all H positions"))
                        {
                            // Vanilla positions don't seem to go above 60, modded positions are above 1000 usually
                            // 8 buckets might change in the future if game is updated with more h modes, check HSceneProc.lstAnimInfo for how many are needed
                            for (int i = 0; i < 10; i++)
                                _gameMgr.glSaveData.playHList[i] = new HashSet<int>(Enumerable.Range(0, 9999));
                        }

                        if (GUILayout.Button("Unlock all wedding personalities"))
                        {
                            foreach (var personalityId in Singleton<Voice>.Instance.voiceInfoList.Select(x => x.No).Where(x => x >= 0))
                                _gameMgr.weddingData.personality.Add(personalityId);
                        }

                        /* Doesn't work, need a list of items to put into glSaveData.clubContents from somewhere 
                        if (GUILayout.Button("Unlock all free H toys/extras"))
                        {
                            var go = new GameObject("CheatTools Temp");
                            var handCtrl = go.AddComponent<HandCtrl>();
                            var dicItem = (Dictionary<int, HandCtrl.AibuItem>)Traverse.Create(typeof(HandCtrl)).Field("dicItem").GetValue(handCtrl);
                            _gameMgr.glSaveData.clubContents[0] = new HashSet<int>(dicItem.Select(x => x.Value.saveID).Where(x => x >= 0));
                            go.Destroy();
                        }*/
                    }
                    GUILayout.EndVertical();
                }
            }
            GUILayout.EndScrollView();

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

        private void DrawGirlCheatMenu()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Girl stats");

                if (!_showSelectHeroineList)
                {
                    var visibleGirls = GetCurrentVisibleGirls();

                    for (var index = 0; index < visibleGirls.Length; index++)
                    {
                        var girl = visibleGirls[index];
                        if (GUILayout.Button($"Select current #{index} - {girl.Name}"))
                            _currentVisibleGirl = girl;
                    }

                    var anyHeroines = _gameMgr.HeroineList != null && _gameMgr.HeroineList.Count > 0;
                    if (anyHeroines)
                    {
                        if (GUILayout.Button("Select from heroine list"))
                            _showSelectHeroineList = true;
                    }

                    if (_currentVisibleGirl != null)
                    {
                        GUILayout.Space(6);
                        DrawHeroineCheats(_currentVisibleGirl);
                    }
                    else
                    {
                        GUILayout.Label("Select a girl to access her stats");
                    }

                    if (anyHeroines)
                    {
                        GUILayout.BeginVertical(GUI.skin.box);
                        {
                            GUILayout.Label("These affect ALL heroines");
                            if (GUILayout.Button("Make everyone friendly"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                {
                                    h.favor = 100;
                                    h.anger = 0;
                                    h.isAnger = false;
                                }
                            }
                            if (GUILayout.Button("Make everyone lovers"))
                            {
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
                            }
                            if (GUILayout.Button("Make everyone virgins"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                    MakeVirgin(h);
                            }
                            if (GUILayout.Button("Make everyone inexperienced"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                    MakeInexperienced(h);
                            }
                            if (GUILayout.Button("Make everyone experienced"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                    MakeExperienced(h);
                            }
                            if (GUILayout.Button("Make everyone perverted"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                    MakeHorny(h);
                            }
                            if (GUILayout.Button("Clear everyone's desires"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                {
                                    for (int i = 0; i < 31; i++)
                                        Game.Instance.actScene.actCtrl.SetDesire(i, h, 0);
                                }
                            }
                            if (GUILayout.Button("Everyone desires masturbation"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                    Game.Instance.actScene.actCtrl.SetDesire(4, h, 100);
                            }
                            if (GUILayout.Button("Everyone desires lesbian"))
                            {
                                foreach (var h in Game.Instance.HeroineList)
                                {
                                    Game.Instance.actScene.actCtrl.SetDesire(26, h, 100);
                                    Game.Instance.actScene.actCtrl.SetDesire(27, h, 100);
                                }
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
                        GUILayout.Label("Select one of the heroines to continue");

                        for (var index = 0; index < _gameMgr.HeroineList.Count; index++)
                        {
                            var heroine = _gameMgr.HeroineList[index];
                            if (GUILayout.Button($"Select #{index} - {heroine.Name}"))
                            {
                                _currentVisibleGirl = heroine;
                                _showSelectHeroineList = false;
                            }
                        }
                    }
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawHeroineCheats(SaveData.Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical();
            {
                GUILayout.Label("Selected girl name: " + currentAdvGirl.Name);

                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Favor: " + currentAdvGirl.favor, GUILayout.Width(60));
                        currentAdvGirl.favor = (int)GUILayout.HorizontalSlider(currentAdvGirl.favor, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Lewd: " + currentAdvGirl.lewdness, GUILayout.Width(60));
                        currentAdvGirl.lewdness = (int)GUILayout.HorizontalSlider(currentAdvGirl.lewdness, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Anger: " + currentAdvGirl.anger, GUILayout.Width(60));
                        currentAdvGirl.anger = (int)GUILayout.HorizontalSlider(currentAdvGirl.anger, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Intimacy: " + currentAdvGirl.intimacy, GUILayout.Width(60));
                        currentAdvGirl.intimacy = (int)GUILayout.HorizontalSlider(currentAdvGirl.intimacy, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                GUILayout.Space(4);

                GUILayout.Label("Sex experience: " + GetHExpText(currentAdvGirl));
                GUILayout.Label("Set to: (changes multiple stats)");
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Virgin"))
                        MakeVirgin(currentAdvGirl);
                    if (GUILayout.Button("Inexp"))
                        MakeInexperienced(currentAdvGirl);
                    if (GUILayout.Button("Exp"))
                        MakeExperienced(currentAdvGirl);
                    if (GUILayout.Button("Horny"))
                        MakeHorny(currentAdvGirl);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                GUILayout.Label("Set all touch experience");
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("0%"))
                        SetGirlHExp(currentAdvGirl, 0f);
                    if (GUILayout.Button("50%"))
                        SetGirlHExp(currentAdvGirl, 50f);
                    if (GUILayout.Button("100%"))
                        SetGirlHExp(currentAdvGirl, 100f);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(4);

                if (GUILayout.Button("Reset conversation time"))
                    currentAdvGirl.talkTime = currentAdvGirl.talkTimeMax;

                if (_gameMgr.actScene != null && _gameMgr.actScene.actCtrl != null)
                {
                    var desires = Enumerable.Range(0, 31).Select(i => new { i, desire = _gameMgr.actScene.actCtrl.GetDesire(i, currentAdvGirl) });
                    var sorted = desires.Where(x => x.desire > 10).OrderByDescending(x => x.desire).Select(x => x.i.ToString()).Take(8).ToArray();
                    var desireText = sorted.Length == 0 ? "Has no desires" : "Desires: " + string.Join(", ", sorted);
                    GUILayout.Label(desireText);

                    if (GUILayout.Button("Clear all desires"))
                    {
                        for (int i = 0; i < 31; i++) Game.Instance.actScene.actCtrl.SetDesire(i, currentAdvGirl, 0);
                    }

                    var wantsMast = _gameMgr.actScene.actCtrl.GetDesire(4, currentAdvGirl) > 80;
                    if (wantsMast)
                    {
                        GUILayout.Label("Desires to masturbate");
                    }
                    else
                    {
                        if (GUILayout.Button("Make desire to masturbate"))
                            Game.Instance.actScene.actCtrl.SetDesire(4, currentAdvGirl, 100);
                    }
                    var wantsLes = _gameMgr.actScene.actCtrl.GetDesire(26, currentAdvGirl) > 80;
                    if (wantsLes)
                    {
                        GUILayout.Label("Desires to lesbian");
                    }
                    else
                    {
                        if (GUILayout.Button("Make desire to lesbian"))
                        {
                            Game.Instance.actScene.actCtrl.SetDesire(26, currentAdvGirl, 100);
                            Game.Instance.actScene.actCtrl.SetDesire(27, currentAdvGirl, 100);
                        }
                    }
                    GUILayout.Label("Clear desires first to prioritize");
                }

                GUILayout.Space(4);

                // 危険日 is risky, 安全日 is safe. Only change when user clicks to avoid messing with the value unnecessarily
                GUI.changed = false;
                var isDangerousDay = GUILayout.Toggle(HFlag.GetMenstruation(currentAdvGirl.MenstruationDay) == HFlag.MenstruationType.危険日, "Is on a risky day");
                if (GUI.changed)
                    HFlag.SetMenstruation(currentAdvGirl, isDangerousDay ? HFlag.MenstruationType.危険日 : HFlag.MenstruationType.安全日);

                currentAdvGirl.isVirgin = GUILayout.Toggle(currentAdvGirl.isVirgin, "isVirgin");
                currentAdvGirl.isAnalVirgin = GUILayout.Toggle(currentAdvGirl.isAnalVirgin, "isAnalVirgin");

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Sex count: ", GUILayout.ExpandWidth(false));
                    GUI.changed = false;
                    var newCount = GUILayout.TextField(currentAdvGirl.hCount.ToString(), GUILayout.ExpandWidth(true));
                    if (GUI.changed && int.TryParse(newCount, out var newCountInt))
                        currentAdvGirl.hCount = Mathf.Max(newCountInt, 0);
                }
                GUILayout.EndHorizontal();

                currentAdvGirl.isAnger = GUILayout.Toggle(currentAdvGirl.isAnger, "Is angry");
                currentAdvGirl.isDate = GUILayout.Toggle(currentAdvGirl.isDate, "Date promised");
                //currentAdvGirl.isFirstGirlfriend = GUILayout.Toggle(currentAdvGirl.isFirstGirlfriend, "isFirstGirlfriend");
                currentAdvGirl.isGirlfriend = GUILayout.Toggle(currentAdvGirl.isGirlfriend, "isGirlfriend");

                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, "Won't refuse kiss");
                currentAdvGirl.denial.massage = GUILayout.Toggle(currentAdvGirl.denial.massage, "Won't refuse strong massage");
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, "Won't refuse anal");
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, "Won't refuse vibrator");
                currentAdvGirl.denial.notCondom = GUILayout.Toggle(currentAdvGirl.denial.notCondom, "Insert w/o condom OK");

                if (GUILayout.Button("Navigate to heroine's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        _editor.TreeViewer.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Heroine has no body assigned");
                }

                if (GUILayout.Button("Open Heroine in inspector"))
                {
                    _editor.Inspector.Push(new InstanceStackEntry(currentAdvGirl, "Heroine " + currentAdvGirl.Name), true);
                }
            }
            GUILayout.EndVertical();
        }

        private static void MakeHorny(SaveData.Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = Mathf.Max(1, currentAdvGirl.hCount);
            currentAdvGirl.isVirgin = false;
            SetGirlHExp(currentAdvGirl, 100f);
            currentAdvGirl.lewdness = 100;
        }

        private static void MakeExperienced(SaveData.Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = Mathf.Max(1, currentAdvGirl.hCount);
            currentAdvGirl.isVirgin = false;
            SetGirlHExp(currentAdvGirl, 100f);
            currentAdvGirl.lewdness = Mathf.Min(99, currentAdvGirl.lewdness);
        }

        private static void MakeInexperienced(SaveData.Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = Mathf.Max(1, currentAdvGirl.hCount);
            currentAdvGirl.isVirgin = false;
            currentAdvGirl.countKokanH = 50;
            SetGirlHExp(currentAdvGirl, 0);
        }

        private static void MakeVirgin(SaveData.Heroine currentAdvGirl)
        {
            currentAdvGirl.hCount = 0;
            currentAdvGirl.isVirgin = true;
            SetGirlHExp(currentAdvGirl, 0);
        }

        private static void SetGirlHExp(SaveData.Heroine girl, float amount)
        {
            girl.houshiExp = amount;
            girl.countKokanH = amount;
            girl.countAnalH = amount;
            for (var i = 0; i < girl.hAreaExps.Length; i++)
                girl.hAreaExps[i] = amount;
            for (var i = 0; i < girl.massageExps.Length; i++)
                girl.massageExps[i] = amount;
        }

        private void DrawPlayerCheats()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                if (_gameMgr == null || _gameMgr.saveData.isOpening)
                {
                    GUILayout.Label("Start the game to see player cheats");
                }
                else
                {
                    GUILayout.Label("Player stats");

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("STR: " + _gameMgr.Player.physical, GUILayout.Width(60));
                        _gameMgr.Player.physical = (int)GUILayout.HorizontalSlider(_gameMgr.Player.physical, 0, 100);
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("INT: " + _gameMgr.Player.intellect, GUILayout.Width(60));
                            _gameMgr.Player.intellect = (int)GUILayout.HorizontalSlider(_gameMgr.Player.intellect, 0, 100);
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("H: " + _gameMgr.Player.hentai, GUILayout.Width(60));
                            _gameMgr.Player.hentai = (int)GUILayout.HorizontalSlider(_gameMgr.Player.hentai, 0, 100);
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
                                    {
                                        typeof(Cycle)
                                            .GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)
                                            ?.SetValue(cycle, newVal);
                                    }
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

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Academy Name: ", GUILayout.ExpandWidth(false));
                        _gameMgr.saveData.accademyName = GUILayout.TextField(_gameMgr.saveData.accademyName, GUILayout.ExpandWidth(true));
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Player Name: ", GUILayout.ExpandWidth(false));
                        _gameMgr.Player.parameter.lastname = GUILayout.TextField(_gameMgr.Player.parameter.lastname);
                        _gameMgr.Player.parameter.firstname = GUILayout.TextField(_gameMgr.Player.parameter.firstname);
                    }
                    GUILayout.EndHorizontal();

                    if (GUILayout.Button("Add 10000 club points (+1 level)"))
                        _gameMgr.saveData.clubReport.comAdd += 10000;

                    if (GUILayout.Button("Stop shame reactions in bathrooms"))
                    {
                        var actionMap = Object.FindObjectOfType<ActionMap>();
                        if (actionMap != null)
                        {
                            foreach (var param in actionMap.infoDic.Values)
                            {
                                if (param.isWarning)
                                {
                                    param.isWarning = false;
                                    CheatToolsPlugin.Logger.Log(LogLevel.Message, "Disabling shame reactions on map: " + param.MapName);
                                }
                            }
                        }
                    }

                    GUI.changed = false;
                    var playerIsNoticeable = _playerEnterExitTrigger == null || _playerEnterExitTrigger.enabled;
                    playerIsNoticeable = !GUILayout.Toggle(!playerIsNoticeable, "Make player unnoticeable");
                    if (GUI.changed)
                    {
                        var actionMap = Object.FindObjectOfType<ActionScene>();
                        if (actionMap != null)
                        {
                            _playerEnterExitTrigger = Traverse.Create(actionMap.Player)
                                .Field<TriggerEnterExitEvent>("noticeArea").Value;
                            _playerEnterExitTrigger.enabled = playerIsNoticeable;
                        }
                    }

                    CheatToolsPlugin.NoclipMode = GUILayout.Toggle(CheatToolsPlugin.NoclipMode, "Enable player noclip");

                    if (GUILayout.Button("Open player data in inspector"))
                    {
                        _editor.Inspector.Push(new InstanceStackEntry(_gameMgr.saveData.player, "Player data"), true);
                    }
                }
            }
            GUILayout.EndVertical();
        }

        private SaveData.Heroine[] GetCurrentVisibleGirls()
        {
            if (_talkScene != null)
            {
                var result = _talkScene.targetHeroine;
                if (result != null) return new[] { result };
            }

            if (_hFlag != null)
            {
                var hHeroines = _hFlag.lstHeroine;
                if (hHeroines != null && hHeroines.Count > 0) return hHeroines.ToArray();
            }

            if (Game.IsInstance() &&
                Game.Instance.actScene != null &&
                Game.Instance.actScene.AdvScene != null)
            {
                var advScene = Game.Instance.actScene.AdvScene;
                if (advScene.Scenario != null && advScene.Scenario.currentHeroine != null)
                    return new[] { advScene.Scenario.currentHeroine };
                if (advScene.nowScene is TalkScene s && s.targetHeroine != null)
                    return new[] { s.targetHeroine };
            }

            return new SaveData.Heroine[0];
        }

        private string GetHExpText(SaveData.Heroine currentAdvGirl)
        {
            return _hExpNames[(int)currentAdvGirl.HExperience];
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

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            const int cheatWindowHeight = 410;
            _cheatWindowRect = new Rect(_screenRect.xMin, _screenRect.yMax - cheatWindowHeight, 270, cheatWindowHeight);
        }
    }
}
