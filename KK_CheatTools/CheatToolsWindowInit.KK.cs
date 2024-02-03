﻿using System;
using System.Collections.Generic;
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

        public static void Initialize(CheatToolsPlugin instance)
        {
            var config = instance.Config;

            UnlockAllPositions = config.Bind("Cheats", "Unlock all H positions", false, "Reload the H scene to see changes.");
            UnlockAllPositions.SettingChanged += (sender, args) => UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;
            UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;

            UnlockAllPositionsIndiscriminately = config.Bind("Cheats", "Unlock invalid H positions as well", false, "This will unlock all positions even if they should not be possible.\nWARNING: Can result in bugs and even game crashes in some cases.\nReload the H scene to see changes.");
            UnlockAllPositionsIndiscriminately.SettingChanged += (sender, args) => UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;
            UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;

            ToStringConverter.AddConverter<SaveData.Heroine>(heroine => !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.nickname);
            ToStringConverter.AddConverter<SaveData.CharaData.Params.Data>(d => $"[{d.key} | {d.value}]");

            NoclipFeature.InitializeNoclip(instance, () =>
            {
                if (!Game.IsInstance()) return null;
                var player = Game.Instance.Player;
                if (player == null) return null;
                var playerTransform = player.transform;
                if (playerTransform == null) return null;
                return playerTransform.GetComponent<NavMeshAgent>();
            });

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
                    new KeyValuePair<object, string>(_gameMgr != null && _gameMgr.HeroineList.Count > 0 ? (Func<object>)(() => _gameMgr.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x))) : null, "Heroine list"),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                    new KeyValuePair<object, string>(_communicationInstance, "Manager.Communication.Instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                    new KeyValuePair<object, string>(_hFlag, "HFlag"),
                    new KeyValuePair<object, string>(_talkScene, "TalkScene"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                    new KeyValuePair<object, string>((Func<object>)EditorUtilities.GetRootGoScanner, "Root Objects")
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _studioInstance == null && _gameMgr != null && !_gameMgr.saveData.isOpening, DrawPlayerCheats, "Start the game to see player cheats"));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hFlag != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGirlCheatMenu, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _gameMgr != null, DrawGlobalUnlocks, null));
        }

        private static void DrawGlobalUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label("Global unlocks (might need a reload)");

            UnlockAllPositions.Value = GUILayout.Toggle(UnlockAllPositions.Value, "Unlock all H positions");

            if (GUILayout.Button("Obtain all H positions"))
            {
                // Vanilla positions don't seem to go above 60, modded positions are above 1000 usually
                // 8 buckets might change in the future if game is updated with more h modes, check HSceneProc.lstAnimInfo for how many are needed
                for (var i = 0; i < 10; i++)
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

            if (_hSprite != null && GUILayout.Button("Force quit H scene"))
            {
                Utils.Sound.Play(SystemSE.cancel);
                _hSprite.flags.click = HFlag.ClickKind.end;
                _hSprite.flags.isHSceneEnd = true;
                _hSprite.flags.numEnd = 0;
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

                        if (GUILayout.Button("Make everyone club members"))
                            foreach (var h in Game.Instance.HeroineList)
                            {
                                if (!h.isTeacher)
                                    h.isStaff = true;
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
                                for (var i = 0; i < 31; i++)
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

        private static void DrawHeroineCheats(SaveData.Heroine currentAdvGirl)
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

                GUILayout.Space(8);

                if (GUILayout.Button("Reset conversation time"))
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
                        for (var i = 0; i < 31; i++)
                            actCtrl.SetDesire(i, currentAdvGirl, 0);
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

                GUI.changed = false;
                var newVal = GUILayout.Toggle(currentAdvGirl.talkEvent.Contains(0) || currentAdvGirl.talkEvent.Contains(1), "Had first meeting");
                if (GUI.changed)
                {
                    if (newVal)
                    {
                        currentAdvGirl.talkEvent.Add(0);
                        currentAdvGirl.talkEvent.Add(1);
                    }
                    else
                    {
                        currentAdvGirl.talkEvent.Remove(0);
                        currentAdvGirl.talkEvent.Remove(1);
                    }
                }

                GUI.changed = false;
                newVal = GUILayout.Toggle(currentAdvGirl.talkEvent.Contains(2), "Is a friend");
                if (GUI.changed)
                {
                    if (newVal)
                        currentAdvGirl.talkEvent.Add(2);
                    else
                        currentAdvGirl.talkEvent.Remove(2);
                }

                currentAdvGirl.isGirlfriend = GUILayout.Toggle(currentAdvGirl.isGirlfriend, "Is a girlfriend");
                currentAdvGirl.isStaff = GUILayout.Toggle(currentAdvGirl.isStaff, "Is a club member");

                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, "Won't refuse kiss");
                currentAdvGirl.denial.massage = GUILayout.Toggle(currentAdvGirl.denial.massage, "Won't refuse strong massage");
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, "Won't refuse anal");
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, "Won't refuse vibrator");
                currentAdvGirl.denial.notCondom = GUILayout.Toggle(currentAdvGirl.denial.notCondom, "Insert w/o condom OK");

                if (_gameMgr?.actScene != null && currentAdvGirl.transform != null && GUILayout.Button("Follow me"))
                {
                    var npc = currentAdvGirl.transform.GetComponent<NPC>();
                    if (npc) _gameMgr.actScene.Player.ChaserSet(npc);
                    else CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Could not make heroine follow - NPC component not found");
                }

                if (GUILayout.Button("Navigate to heroine's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Heroine has no body assigned");
                }

                if (GUILayout.Button("Open Heroine in inspector"))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, "Heroine " + currentAdvGirl.Name), true);

                if (GUILayout.Button("Inspect extended data"))
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.charFile), "ExtData for " + currentAdvGirl.Name), true);
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

        private static void DrawPlayerCheats(CheatToolsWindow cheatToolsWindow)
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
                _gameMgr.saveData.accademyName =
                    GUILayout.TextField(_gameMgr.saveData.accademyName, GUILayout.ExpandWidth(true));
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
                            CheatToolsPlugin.Logger.Log(LogLevel.Message,
                                                        "Disabling shame reactions on map: " + param.MapName);
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
                Inspector.Instance.Push(new InstanceStackEntry(_gameMgr.saveData.player, "Player data"), true);
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

        private static string GetHExpText(SaveData.Heroine currentAdvGirl)
        {
            return ((HExperienceKindEng)currentAdvGirl.HExperience).ToString();
        }
    }
}
