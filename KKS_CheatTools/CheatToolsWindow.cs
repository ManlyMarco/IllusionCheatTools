using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActionGame;
using ActionGame.Chara;
using ADV;
using Illusion.Component;
using Illusion.Game;
using Manager;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;
using Object = UnityEngine.Object;

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
        private static Scene _sceneInstance;
        private static Game _gameMgr;

        private static TriggerEnterExitEvent _playerEnterExitTrigger;
        private static string _setdesireId;
        private static string _setdesireValue;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        private static readonly string[] _prayerNames = new[]
        {
            "Nothing",
            "Topic drop bonus",
            "Find more topics?",
            "Safe topic bonus",
            "Girls want to talk",
            "Extra oil",
            "Confession bonus",
            "Find good topics next day",
            "Lewd topic bonus",
            "Lover visit at evening",
            "Girls want to H you",
            "Ask for sex bonus",
        };

        private static readonly int[] _prayerIds = new[]
        {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            1000,
            1001,
            1002,
            1003,
        };

        private static readonly string[] _relationNames = new[]
        {
            "Casual",
            "Friend",
            "Lover",
            "Bonded",
        };

        public static void InitializeCheats()
        {
            CheatToolsWindow.OnShown = window =>
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
                    new KeyValuePair<object, string>(_gameMgr != null && Game.HeroineList.Count > 0 ? (Func<object>) (() => Game.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x))) : null, "Heroine list"),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.instance"),
                    new KeyValuePair<object, string>(_hFlag, "HFlag"),
                    new KeyValuePair<object, string>(_talkScene, "TalkScene"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                    new KeyValuePair<object, string>((Func<object>) EditorUtilities.GetRootGoScanner, "Root Objects")
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _studioInstance == null && Game.saveData != null && !Game.saveData.isOpening, DrawPlayerCheats, "Start the game to see player cheats"));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hFlag != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGirlCheatMenu, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGlobalUnlocks, null));
        }

        private static void DrawGlobalUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label("Global unlocks (might need a reload)");

            // todo needs testing
            if (GUILayout.Button("Obtain all H positions"))
            {
                // Vanilla positions don't seem to go above 60, modded positions are above 1000 usually
                // 8 buckets might change in the future if game is updated with more h modes, check HSceneProc.lstAnimInfo for how many are needed
                for (int i = 0; i < 10; i++) Game.globalData.playHList[i] = new HashSet<int>(Enumerable.Range(0, 9999));
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("H scene controls");

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Male Gauge: " + _hFlag.gaugeMale.ToString("N1"), GUILayout.Width(150));
                _hFlag.gaugeMale = GUILayout.HorizontalSlider(_hFlag.gaugeMale, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Female Gauge: " + _hFlag.gaugeFemale.ToString("N1"), GUILayout.Width(150));
                _hFlag.gaugeFemale = GUILayout.HorizontalSlider(_hFlag.gaugeFemale, 0, 100);
            }

            GUILayout.EndHorizontal();

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
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
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

                var anyHeroines = Game.HeroineList != null && Game.HeroineList.Count > 0;
                if (anyHeroines)
                {
                    if (GUILayout.Button("Select from heroine list"))
                        _showSelectHeroineList = true;
                }

                if (_currentVisibleGirl != null)
                {
                    GUILayout.Space(6);
                    DrawHeroineCheats(_currentVisibleGirl, cheatToolsWindow);
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
                        if (GUILayout.Button("Make everyone friends"))
                        {
                            foreach (var h in Game.HeroineList)
                            {
                                h.favor = 100;
                                h.isGirlfriend = false;
                            }
                        }
                        if (GUILayout.Button("Make everyone lovers"))
                        {
                            foreach (var h in Game.HeroineList)
                            {
                                h.favor = 75;
                                h.isGirlfriend = true;
                                h.confessed = true;
                            }
                        }
                        if (GUILayout.Button("Make everyone full lovers"))
                        {
                            foreach (var h in Game.HeroineList)
                            {
                                h.favor = 150;
                                h.isGirlfriend = true;
                                h.confessed = true;
                            }
                        }
                        if (GUILayout.Button("Make everyone lewd"))
                        {
                            foreach (var h in Game.HeroineList)
                            {
                                h.lewdness = 100;
                            }
                        }
                        if (GUILayout.Button("Make everyone virgins"))
                        {
                            foreach (var h in Game.HeroineList)
                                MakeVirgin(h);
                        }
                        if (GUILayout.Button("Make everyone inexperienced"))
                        {
                            foreach (var h in Game.HeroineList)
                                MakeInexperienced(h);
                        }
                        if (GUILayout.Button("Make everyone experienced"))
                        {
                            foreach (var h in Game.HeroineList)
                                MakeExperienced(h);
                        }
                        if (GUILayout.Button("Make everyone perverted"))
                        {
                            foreach (var h in Game.HeroineList)
                                MakeHorny(h);
                        }
                        //todo check if desires are the same as in kk
                        if (GUILayout.Button("Clear everyone's desires"))
                        {
                            foreach (var h in Game.HeroineList)
                            {
                                for (int i = 0; i < 31; i++)
                                    ActionScene.instance.actCtrl.SetDesire(i, h, 0);
                            }
                        }
                        if (GUILayout.Button("Everyone desires masturbation"))
                        {
                            foreach (var h in Game.HeroineList)
                                ActionScene.instance.actCtrl.SetDesire(4, h, 100);
                        }
                        if (GUILayout.Button("Everyone desires lesbian"))
                        {
                            foreach (var h in Game.HeroineList)
                            {
                                ActionScene.instance.actCtrl.SetDesire(26, h, 100);
                                ActionScene.instance.actCtrl.SetDesire(27, h, 100);
                            }
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
                    GUILayout.Label("Select one of the heroines to continue");

                    for (var index = 0; index < Game.HeroineList.Count; index++)
                    {
                        var heroine = Game.HeroineList[index];
                        if (GUILayout.Button($"Select #{index} - {heroine.Name}"))
                        {
                            _currentVisibleGirl = heroine;
                            _showSelectHeroineList = false;
                        }
                    }
                }
            }
        }

        private static void DrawHeroineCheats(SaveData.Heroine currentAdvGirl, CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Selected girl name: ");
                    GUILayout.Label(currentAdvGirl.Name);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Relationship level: ");
                    GUILayout.Label(_relationNames[currentAdvGirl.relation]);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Favor: " + currentAdvGirl.favor, GUILayout.Width(70));
                    currentAdvGirl.favor = (int)GUILayout.HorizontalSlider(currentAdvGirl.favor, 0, currentAdvGirl.isGirlfriend ? 150 : 100);
                }
                GUILayout.EndHorizontal();

                currentAdvGirl.isFriend = GUILayout.Toggle(currentAdvGirl.isFriend, "Is a friend");
                currentAdvGirl.isGirlfriend = GUILayout.Toggle(currentAdvGirl.isGirlfriend, "Is a girlfriend");

                currentAdvGirl.confessed = GUILayout.Toggle(currentAdvGirl.confessed, "Confessed");
                currentAdvGirl.isLunch = GUILayout.Toggle(currentAdvGirl.isLunch, "Had first lunch");
                currentAdvGirl.isDayH = GUILayout.Toggle(currentAdvGirl.isDayH, "Had H today (won't visit)");

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Desire to visit: ", GUILayout.ExpandWidth(false));
                    GUI.changed = false;
                    var newCount = GUILayout.TextField(currentAdvGirl.visitDesire.ToString(), GUILayout.ExpandWidth(true));
                    if (GUI.changed && int.TryParse(newCount, out var newCountInt))
                        currentAdvGirl.visitDesire = Mathf.Max(newCountInt, 0);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(8);

                if (GUILayout.Button("Reset conversation time"))
                    currentAdvGirl.talkTime = currentAdvGirl.talkTimeMax;

                if (ActionScene.instance != null && currentAdvGirl.transform != null && GUILayout.Button("Follow me"))
                {
                    var npc = currentAdvGirl.transform.GetComponent<NPC>();
                    if (npc) ActionScene.instance.Player.ChaserSet(npc);
                    else CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Could not make heroine follow - NPC component not found");
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
                            GUILayout.Label("Desires (and their strengths):\n");
                            any = true;
                        }
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label((int)desire.id + " " + desire.id);
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(desire.value + "%");
                            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                                actCtrl.SetDesire((int)desire.id, currentAdvGirl, 0);
                        }
                        GUILayout.EndHorizontal();
                    }
                    if (!any) GUILayout.Label("Has no desires");

                    if (GUILayout.Button("Clear all desires"))
                    {
                        for (int i = 0; i < 31; i++) actCtrl.SetDesire(i, currentAdvGirl, 0);
                    }

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Set desire ", GUILayout.ExpandWidth(false));
                        _setdesireId = GUILayout.TextField(_setdesireId ?? "");
                        GUILayout.Label(" to value ", GUILayout.ExpandWidth(false));
                        _setdesireValue = GUILayout.TextField(_setdesireValue ?? "");
                        if (GUILayout.Button("OK", GUILayout.ExpandWidth(false)))
                        {
                            try
                            {
                                actCtrl.SetDesire((int)Enum.Parse(typeof(DesireEng), _setdesireId), currentAdvGirl, int.Parse(_setdesireValue));
                            }
                            catch (Exception e)
                            {
                                CheatToolsPlugin.Logger.LogMessage("Invalid desire ID (0-30) or value (0-100) - " + e.Message);
                            }
                        }
                    }
                    GUILayout.EndHorizontal();

                    var wantsMast = actCtrl.GetDesire(4, currentAdvGirl) > 80;
                    if (!wantsMast)
                    {
                        if (GUILayout.Button("Make desire to masturbate"))
                            actCtrl.SetDesire(4, currentAdvGirl, 100);
                    }

                    var wantsLes = actCtrl.GetDesire(26, currentAdvGirl) > 80;
                    if (!wantsLes)
                    {
                        if (GUILayout.Button("Make desire to lesbian"))
                        {
                            actCtrl.SetDesire(26, currentAdvGirl, 100);
                            actCtrl.SetDesire(27, currentAdvGirl, 100);
                        }
                    }
                }

                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Lewd: " + currentAdvGirl.lewdness, GUILayout.Width(70));
                    currentAdvGirl.lewdness = (int)GUILayout.HorizontalSlider(currentAdvGirl.lewdness, 0, 100);
                }
                GUILayout.EndHorizontal();

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

                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, "Won't refuse kiss");
                currentAdvGirl.denial.massage = GUILayout.Toggle(currentAdvGirl.denial.massage, "Won't refuse strong massage");
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, "Won't refuse anal");
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, "Won't refuse vibrator");
                currentAdvGirl.denial.notCondom = GUILayout.Toggle(currentAdvGirl.denial.notCondom, "Insert w/o condom OK");

                GUILayout.Space(4);

                if (GUILayout.Button("Navigate to heroine's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        cheatToolsWindow.Editor.TreeViewer.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Heroine has no body assigned");
                }

                if (GUILayout.Button("Open Heroine in inspector"))
                {
                    cheatToolsWindow.Editor.Inspector.Push(new InstanceStackEntry(currentAdvGirl, "Heroine " + currentAdvGirl.Name), true);
                }

                if (GUILayout.Button("Inspect extended data"))
                {
                    cheatToolsWindow.Editor.Inspector.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.charFile), "ExtData for " + currentAdvGirl.Name), true);
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
            
            // Global exp added in KKS
            girl.hExp = amount;
        }

        private static void DrawPlayerCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("Player stats");

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("STR: " + Game.Player.physical, GUILayout.Width(60));
                Game.Player.physical = (int)GUILayout.HorizontalSlider(Game.Player.physical, 0, 100);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("INT: " + Game.Player.intellect, GUILayout.Width(60));
                    Game.Player.intellect = (int)GUILayout.HorizontalSlider(Game.Player.intellect, 0, 100);
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("H: " + Game.Player.hentai, GUILayout.Width(60));
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
                            GUILayout.Label("Time: " + cycle.timer.ToString("N1"), GUILayout.Width(65));
                            var newVal = GUILayout.HorizontalSlider(cycle.timer, 0, Cycle.TIME_LIMIT);
                            if (Math.Abs(newVal - cycle.timer) > 0.09)
                            {
                                cycle._timer = newVal;
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
                GUILayout.Label("Player Name: ", GUILayout.ExpandWidth(false));
                Game.Player.parameter.lastname = GUILayout.TextField(Game.Player.parameter.lastname);
                Game.Player.parameter.firstname = GUILayout.TextField(Game.Player.parameter.firstname);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Add 100 Koikatsu points"))
                Game.saveData.player.koikatsuPoint += 100;

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
                    _playerEnterExitTrigger = actionMap.Player.noticeArea;
                    _playerEnterExitTrigger.enabled = playerIsNoticeable;
                }
            }

            NoclipFeature.NoclipMode = GUILayout.Toggle(NoclipFeature.NoclipMode, "Enable player noclip");

            if (GUILayout.Button("Open player data in inspector"))
            {
                cheatToolsWindow.Editor.Inspector.Push(new InstanceStackEntry(Game.saveData.player, "Player data"), true);
            }

            GUILayout.BeginVertical(GUI.skin.box);
            {
                var currentPrayer = Game.saveData.prayedResult;
                var prayerIndex = Array.IndexOf(_prayerIds, currentPrayer);
                var prayerName = prayerIndex >= 0 ? _prayerNames[prayerIndex] : "Unknown";

                GUILayout.Label("Prayer bonus: " + prayerName);

                GUI.changed = false;
                var result = GUILayout.SelectionGrid(prayerIndex, _prayerNames, 1);
                if (GUI.changed)
                    Game.saveData.prayedResult = _prayerIds[result];
            }
            GUILayout.EndVertical();
        }

        private static SaveData.Heroine[] GetCurrentVisibleGirls()
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

            if (Game.initialized &&
                ActionScene.initialized &&
                ActionScene.instance.advScene != null)
            {
                var advScene = ActionScene.instance.advScene;
                if (advScene.Scenario != null && advScene.Scenario.currentHeroine != null)
                    return new[] { advScene.Scenario.currentHeroine };
                if (advScene.nowScene is TalkScene s && s.targetHeroine != null)
                    return new[] { s.targetHeroine };
            }

            return new SaveData.Heroine[0];
        }

        private static string GetHExpText(SaveData.Heroine currentAdvGirl)
        {
            return ((HExperienceKindEng)currentAdvGirl.HExperience).ToString();
        }
    }
}
