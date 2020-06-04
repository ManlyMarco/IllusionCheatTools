using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Actor;
using AIChara;
using Manager;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using LogLevel = BepInEx.Logging.LogLevel;

namespace CheatTools
{
    public class CheatToolsWindow
    {
        private const int ScreenOffset = 20;

        private readonly RuntimeUnityEditorCore _editor;

        private readonly string _mainWindowTitle;
        private Vector2 _cheatsScrollPos;
        private Rect _cheatWindowRect;
        private Rect _screenRect;
        private bool _show;

        private Heroine _currentVisibleGirl;

        private Studio.Studio _studioInstance;
        private Manager.Sound _soundInstance;
        private Scene _sceneInstance;
        private Game _gameMgr;
        private BaseMap _baseMap;
        private HSceneFlagCtrl _hScene;

        public CheatToolsWindow(RuntimeUnityEditorCore editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            ToStringConverter.AddConverter<Heroine>(GetHeroineName);
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

            _mainWindowTitle = "Cheat Tools " + Assembly.GetExecutingAssembly().GetName().Version;
        }

        private static string GetHeroineName(Heroine heroine)
        {
            return !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.ChaName;
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

                _studioInstance = Studio.Studio.IsInstance() ? Studio.Studio.Instance : null;
                _soundInstance = Manager.Sound.instance;
                _sceneInstance = Scene.instance;
                _gameMgr = Game.IsInstance() ? Game.Instance : null;
                _baseMap = BaseMap.instance;
                _hScene = HSceneFlagCtrl.IsInstance() ? HSceneFlagCtrl.Instance : null;
            }
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            const int cheatWindowHeight = 410;
            _cheatWindowRect = new Rect(_screenRect.xMin, _screenRect.yMax - cheatWindowHeight, 270, cheatWindowHeight);
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

        private void CheatWindowContents(int id)
        {
            _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
            {
                //DrawPlayerCheats();

                //DrawEnviroControls();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
                    GUILayout.Label((int)Math.Round(Time.timeScale * 100) + "%", GUILayout.Width(35));
                    Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0, 5, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                        Time.timeScale = 1;
                }
                GUILayout.EndHorizontal();

                DrawHSceneCheats();

                DrawGirlCheatMenu();

                //_gameMgr.saveData.achievementAchieve

                if (_studioInstance == null && _gameMgr?.saveData != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
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
                    GUILayout.EndVertical();
                }

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label("Open in inspector");
                    foreach (var obj in new[]
                    {
                            new KeyValuePair<object, string>(_gameMgr?.heroineList.Count > 0 ? _gameMgr.heroineList.Select(x => new ReadonlyCacheEntry(GetHeroineName(x), x)) : null, "Heroine list"),
                            new KeyValuePair<object, string>(ADVManager.IsInstance() ? ADVManager.Instance : null, "Manager.ADVManager.Instance"),
                            new KeyValuePair<object, string>(_baseMap, "Manager.BaseMap.instance"),
                            new KeyValuePair<object, string>(Character.IsInstance() ? Character.Instance : null, "Manager.Character.Instance"),
                            new KeyValuePair<object, string>(typeof(Manager.Config), "Manager.Config"),
                            new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                            new KeyValuePair<object, string>(GameSystem.IsInstance() ? GameSystem.Instance : null, "Manager.GameSystem.Instance"),
                            new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.instance"),
                            new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.instance"),
                            new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                            new KeyValuePair<object, string>(EditorUtilities.GetRootGoScanner(), "Root Objects")
                        })
                    {
                        if (obj.Key == null) continue;
                        if (GUILayout.Button(obj.Value))
                        {
                            if (obj.Key is Type t)
                                _editor.Inspector.Push(new StaticStackEntry(t, obj.Value), true);
                            else
                                _editor.Inspector.Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                        }
                    }
                }
                GUILayout.EndVertical();

                if (GUILayout.Button("Clear AssetBundle Cache"))
                {
                    foreach (var pair in AssetBundleManager.ManifestBundlePack)
                    {
                        foreach (var bundle in new Dictionary<string, LoadedAssetBundle>(pair.Value.LoadedAssetBundles))
                            AssetBundleManager.UnloadAssetBundle(bundle.Key, true, pair.Key);
                    }
                }
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void DrawHSceneCheats()
        {
            if (_hScene == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
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
                    _editor.Inspector.Push(new InstanceStackEntry(_hScene, "HSceneFlagCtrl"), true);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawGirlCheatMenu()
        {
            if (_baseMap == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Heroines");

                var visibleGirls = _gameMgr.heroineList;

                for (var i = 0; i < visibleGirls.Count; i++)
                {
                    var girl = visibleGirls[i];
                    if (GUILayout.Button($"Select #{i} - {GetHeroineName(girl)}"))
                        _currentVisibleGirl = girl;
                }

                GUILayout.Space(6);

                if (_currentVisibleGirl != null)
                    DrawSingleGirlCheats(_currentVisibleGirl);
                else
                    GUILayout.Label("Select a heroine to access her stats");
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawSingleGirlCheats(Heroine currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Selected heroine name: " + (GetHeroineName(currentAdvGirl)));
                GUILayout.Space(6);

                if (currentAdvGirl.chaCtrl != null && currentAdvGirl.chaCtrl.fileGameInfo2 != null)
                {
                    var gi2 = currentAdvGirl.gameinfo2;

                    void DrawSingleStateBtn(ChaFileDefine.State state)
                    {
                        if (GUILayout.Button(state.ToString()))
                        {
                            gi2.nowState = state; gi2.calcState = state; gi2.nowDrawState = state;
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
                                set(newStatus);
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
                                set(newStatus);
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

                    if (GUILayout.Button("View more stats and flags"))
                        _editor.Inspector.Push(new InstanceStackEntry(gi2, "Heroine " + GetHeroineName(currentAdvGirl)), true);
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Navigate to Heroine's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        _editor.TreeViewer.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Heroine has no body assigned");
                }

                if (GUILayout.Button("Open Heroine in inspector"))
                    _editor.Inspector.Push(new InstanceStackEntry(currentAdvGirl, "Heroine " + GetHeroineName(currentAdvGirl)), true);
            }
            GUILayout.EndVertical();
        }
    }
}
