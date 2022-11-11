using System;
using System.Collections.Generic;
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

namespace CheatTools
{
    public static partial class CheatToolsWindowInit
    {
        internal static Heroine _currentVisibleGirl;
        internal static Action<Heroine> _onGirlStatsChanged;

        private static Studio.Studio _studioInstance;
        private static Manager.Sound _soundInstance;
        private static Scene _sceneInstance;
        private static Game _gameMgr;
        private static BaseMap _baseMap;
        private static HSceneFlagCtrl _hScene;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        internal static string GetHeroineName(Heroine heroine)
        {
            return !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.ChaName;
        }

        public static void InitializeCheats()
        {
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
                        _gameMgr != null && _gameMgr.heroineList.Count > 0 ? (Func<object>)(() => _gameMgr.heroineList.Select(x => new ReadonlyCacheEntry(GetHeroineName(x), x))) : null, "Heroine list"),
                    new KeyValuePair<object, string>(ADVManager.IsInstance() ? ADVManager.Instance : null, "Manager.ADVManager.Instance"),
                    new KeyValuePair<object, string>(_baseMap, "Manager.BaseMap.instance"),
                    new KeyValuePair<object, string>(Character.IsInstance() ? Character.Instance : null, "Manager.Character.Instance"),
                    new KeyValuePair<object, string>(typeof(Manager.Config), "Manager.Config"),
                    new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                    new KeyValuePair<object, string>(GameSystem.IsInstance() ? GameSystem.Instance : null, "Manager.GameSystem.Instance"),
                    new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.instance"),
                    new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.instance"),
                    new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                    new KeyValuePair<object, string>((Func<object>)EditorUtilities.GetRootGoScanner, "Root Objects")
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _hScene != null, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _baseMap != null && (_hScene != null || Singleton<LobbySceneManager>.IsInstance()), DrawGirlCheatMenu, "Unable to edit character stats on this screen. Start an H scene or enter the lobby."));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => _studioInstance == null && _gameMgr != null && _gameMgr.saveData != null, DrawGlobalUnlocks, null));

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Hooks));
        }

        private static void DrawGlobalUnlocks(CheatToolsWindow obj)
        {
            GUILayout.Label("Danger zone! These cheats are permanent and can't be undone without resetting the save.");

            if (GUILayout.Button("Get all achievements", GUILayout.ExpandWidth(true)))
            {
                foreach (var achievementKey in _gameMgr.saveData.achievement.Keys.ToList())
                    SaveData.SetAchievementAchieve(achievementKey);
            }
            if (GUILayout.Button("Unlock all perks", GUILayout.ExpandWidth(true)))
            {
                foreach (var achievementKey in _gameMgr.saveData.achievementExchange.Keys.ToList())
                    SaveData.SetAchievementExchangeRelease(achievementKey);
            }
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("H scene controls");

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Male Gauge: " + _hScene.feel_m.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_m = GUILayout.HorizontalSlider(_hScene.feel_m, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Female Gauge: " + _hScene.feel_f.ToString("F2"), GUILayout.Width(150));
                _hScene.feel_f = GUILayout.HorizontalSlider(_hScene.feel_f, 0, 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Pain Gauge: " + _hScene.feelPain.ToString("F2"), GUILayout.Width(150));
                _hScene.feelPain = GUILayout.HorizontalSlider(_hScene.feelPain, 0, 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Open HScene Flags in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(_hScene, "HSceneFlagCtrl"), true);
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("Character status editor");

            if (!Singleton<LobbySceneManager>.IsInstance())
            {
                var visibleGirls = _gameMgr.heroineList;

                for (var i = 0; i < visibleGirls.Count; i++)
                {
                    var girl = visibleGirls[i];
                    if (GUILayout.Button($"Select #{i} - {GetHeroineName(girl)}"))
                        _currentVisibleGirl = girl;
                }

                GUILayout.Space(6);
            }

            if (_currentVisibleGirl != null)
                DrawSingleGirlCheats(_currentVisibleGirl);
            else
                GUILayout.Label("Select a character to edit their stats");
        }

        private static void DrawSingleGirlCheats(Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Selected heroine name: " + (GetHeroineName(currentAdvGirl)));
                GUILayout.Space(6);

                if (currentAdvGirl.chaCtrl != null && currentAdvGirl.chaCtrl.fileGameInfo2 != null)
                {
                    var anyChanges = false;
                    var gi2 = currentAdvGirl.gameinfo2;

                    void DrawSingleStateBtn(ChaFileDefine.State state)
                    {
                        if (GUILayout.Button(state.ToString()))
                        {
                            gi2.nowState = state; gi2.calcState = state; gi2.nowDrawState = state;
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
                        GUILayout.Label("Current state: " + gi2.nowState);
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

                    ShowSingleSlider(nameof(gi2.Favor), i => gi2.Favor = i, () => gi2.Favor);
                    ShowSingleSlider(nameof(gi2.Enjoyment), i => gi2.Enjoyment = i, () => gi2.Enjoyment);
                    ShowSingleSlider(nameof(gi2.Aversion), i => gi2.Aversion = i, () => gi2.Aversion);
                    ShowSingleSlider(nameof(gi2.Slavery), i => gi2.Slavery = i, () => gi2.Slavery);
                    ShowSingleSlider(nameof(gi2.Broken), i => gi2.Broken = i, () => gi2.Broken);
                    ShowSingleSlider(nameof(gi2.Dependence), i => gi2.Dependence = i, () => gi2.Dependence);
                    ShowSingleSlider(nameof(gi2.Dirty), i => gi2.Dirty = i, () => gi2.Dirty);
                    ShowSingleSlider(nameof(gi2.Tiredness), i => gi2.Tiredness = i, () => gi2.Tiredness);
                    ShowSingleSlider(nameof(gi2.Toilet), i => gi2.Toilet = i, () => gi2.Toilet);
                    ShowSingleSlider(nameof(gi2.Libido), i => gi2.Libido = i, () => gi2.Libido);

                    ShowSingleTextfield(nameof(gi2.hCount), i => { gi2.hCount = i; if (i == 0) gi2.firstHFlag = true; }, () => gi2.hCount);

                    if (anyChanges)
                        _onGirlStatsChanged(_currentVisibleGirl);

                    if (GUILayout.Button("View more stats and flags"))
                        Inspector.Instance.Push(new InstanceStackEntry(gi2, "Heroine " + GetHeroineName(currentAdvGirl)), true);
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

                if (GUILayout.Button("Inspect extended data"))
                {
                    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvGirl.chaFile), "ExtData for " + currentAdvGirl.Name), true);
                }
            }
            GUILayout.EndVertical();
        }
    }
}
