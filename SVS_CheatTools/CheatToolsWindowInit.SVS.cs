using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Character;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using UnityEngine;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static KeyValuePair<object, string>[] _openInInspectorButtons;
        private static Human _currentVisibleGirl;

        public static void Initialize(CheatToolsPlugin instance)
        {
            CheatToolsWindow.OnShown += _ =>
            {
                _openInInspectorButtons = new[]
                {
                    //new KeyValuePair<object, string>(_hScene, "HSceneFlagCtrl.instance"),
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
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => H.HSceneFlagCtrl._instance != null && Manager.HSceneManager._instance != null, DrawGirlCheatMenu, "Unable to edit character stats on this screen.\nYou have to start an H scene, edit the character, and finish the H scene to save changes."));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Manager.Game.SaveData != null, DrawGlobalUnlocks, null));

            //Harmony.CreateAndPatchAll(typeof(Hooks));
        }

        private static void DrawGlobalUnlocks(CheatToolsWindow obj)
        {
            GUILayout.Label("Danger zone! These cheats are permanent and can't be undone without resetting the save.");

            if (GUILayout.Button("Get all achievements", GUILayout.ExpandWidth(true)))
            {
                var achievementKeys = new List<int>();
                foreach (var achievementKey in Manager.Game.SaveData.Achievement.Keys)
                    achievementKeys.Add(achievementKey);

                foreach (var achievementKey in achievementKeys)
                    HC.SaveData.SaveData.UnlockAchievement(achievementKey);
            }

            if (GUILayout.Button("Unlock all perks", GUILayout.ExpandWidth(true)))
            {
                var achievementKeys = new List<int>();
                foreach (var achievementKey in Manager.Game.SaveData.AchievementExchange.Keys)
                    achievementKeys.Add(achievementKey);

                foreach (var achievementKey in achievementKeys)
                    HC.SaveData.SaveData.UnlockAchievementExchange(achievementKey);
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            var hScene = H.HSceneFlagCtrl._instance;

            GUILayout.Label("H scene controls");

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Male Gauge: " + hScene.Feel_m.ToString("F2"), GUILayout.Width(150));
                hScene.Feel_m = GUILayout.HorizontalSlider(hScene.Feel_m, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Female Gauge: " + hScene.Feel_f.ToString("F2"), GUILayout.Width(150));
                hScene.Feel_f = GUILayout.HorizontalSlider(hScene.Feel_f, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Pain Gauge: " + hScene.FeelPain.ToString("F2"), GUILayout.Width(150));
                hScene.FeelPain = GUILayout.HorizontalSlider(hScene.FeelPain, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Spank Gauge: " + hScene.FeelSpnking.ToString("F2"), GUILayout.Width(150));
                hScene.FeelSpnking = GUILayout.HorizontalSlider(hScene.FeelSpnking, 0, 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Open HScene Flags in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(hScene, "HSceneFlagCtrl"), true);
        }

        internal static string GetHeroineName(Human heroine)
        {
            return !string.IsNullOrEmpty(heroine.fileParam?.fullname) ? heroine.fileParam.fullname : heroine.name;
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("Character status editor");

            var visibleGirls = Manager.HSceneManager._instance.Females; //Character.Human._list;

            for (var i = 0; i < visibleGirls.Count; i++)
            {
                var girl = visibleGirls[i];
                if (girl == null) continue;
                if (GUILayout.Button($"Select #{i} - {GetHeroineName(girl)}"))
                    _currentVisibleGirl = girl;
            }

            GUILayout.Space(6);

            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label("Select a character to edit their stats");
        }

        private static void DrawSingleGirlCheats(Human currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Selected heroine name: " + GetHeroineName(currentAdvGirl));
                GUILayout.Space(6);

                var gi = currentAdvGirl.fileGameInfo;
                if (gi != null)
                {
                    var anyChanges = false;

                    void DrawSingleStateBtn(ChaFileDefine.State state)
                    {
                        if (GUILayout.Button(state.ToString()))
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
                            anyChanges = true;
                        }
                    }

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Current state: " + gi.nowState);
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

                    GUILayout.Label("Statistics:");

                    void ShowSingleSlider(string name, Action<int> set, Func<int> get)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            var status = get();
                            GUILayout.Label(name + ": " + status, GUILayout.Width(120));
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
                            GUILayout.Label(name + ": ", GUILayout.Width(120));
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

                    ShowSingleSlider(nameof(gi.Favor), i => gi.Favor = i, () => gi.Favor);
                    ShowSingleSlider(nameof(gi.Enjoyment), i => gi.Enjoyment = i, () => gi.Enjoyment);
                    ShowSingleSlider(nameof(gi.Aversion), i => gi.Aversion = i, () => gi.Aversion);
                    ShowSingleSlider(nameof(gi.Slavery), i => gi.Slavery = i, () => gi.Slavery);
                    ShowSingleSlider(nameof(gi.Broken), i => gi.Broken = i, () => gi.Broken);
                    ShowSingleSlider(nameof(gi.Dependence), i => gi.Dependence = i, () => gi.Dependence);
                    ShowSingleSlider(nameof(gi.Dirty), i => gi.Dirty = i, () => gi.Dirty);
                    ShowSingleSlider(nameof(gi.Tiredness), i => gi.Tiredness = i, () => gi.Tiredness);
                    ShowSingleSlider(nameof(gi.Toilet), i => gi.Toilet = i, () => gi.Toilet);
                    ShowSingleSlider(nameof(gi.Libido), i => gi.Libido = i, () => gi.Libido);

                    ShowSingleSlider(nameof(gi.alertness), i => gi.alertness = i, () => gi.alertness);

                    ShowSingleTextfield(nameof(gi.hCount), i => { gi.hCount = i; if (i == 0) gi.firstHFlag = true; }, () => gi.hCount);

                    //todo allow changing in lobby, needed for persisting the changes
                    // if (anyChanges)
                    //     _onGirlStatsChanged(_currentVisibleGirl);

                    if (GUILayout.Button("View more stats and flags"))
                        Inspector.Instance.Push(new InstanceStackEntry(gi, "Heroine " + GetHeroineName(currentAdvGirl)), true);
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Navigate to Heroine's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Heroine has no body assigned");
                }

                if (GUILayout.Button("Open Heroine in inspector"))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvGirl, "Heroine " + GetHeroineName(currentAdvGirl)), true);

                //if (GUILayout.Button("Inspect extended data"))
                //{
                //    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.chaFile), "ExtData for " + currentAdvGirl.Name), true);
                //}
            }
            GUILayout.EndVertical();
        }

        //private static class Hooks
        //{
        //}
    }
}
