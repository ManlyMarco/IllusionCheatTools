using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", "1.2")]
    public partial class CheatTools : BaseUnityPlugin
    {
        private const int InspectorTypeWidth = 170, InspectorNameWidth = 200;
        private const int ScreenOffset = 20;
        private readonly string[] _hExpNames = { "First time", "Inexperienced", "Used to", "Perverted" };

        private Rect _screenRect;
        private bool _showGui;
        private bool _userHasHitReturn;
        private Rect _windowRect, _windowRect2;
        private Vector2 _inspectorScrollPos, _cheatsScrollPos;
        private string _mainWindowTitle;

        private readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();
        private readonly List<ICacheEntry> _fieldCache = new List<ICacheEntry>();

        private readonly Stack<InspectorStackEntry> _inspectorStack = new Stack<InspectorStackEntry>();
        private InspectorStackEntry _nextToPush;
        private object _currentlyEditingTag;
        private string _currentlyEditingText;

        private Manager.Game _gameMgr;
        private SaveData.Heroine _currentVisibleGirl;

        private static void DrawSeparator()
        {
            GUILayout.Space(5);
        }

        private static string ExtractText(object value)
        {
            switch (value)
            {
                case string str:
                    return str;
                case Transform _:
                    return value.ToString();
                case SaveData.Heroine heroine:
                    return heroine.Name;
                case ICollection collection:
                    return $"Count = {collection.Count}";
                case IEnumerable _:
                    return "IS ENUMERABLE";
                default:
                    return value?.ToString() ?? "NULL";
            }
        }

        private void CacheFields(object o)
        {
            try
            {
                _fieldCache.Clear();
                if (o != null)
                {
                    if (o is IEnumerable enumerable)
                    {
                        _fieldCache.AddRange(enumerable.Cast<object>().Select((x, y) => new ListCacheEntry(x, y)).Cast<ICacheEntry>());
                    }
                    else
                    {
                        var type = o.GetType();
                        _fieldCache.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => new FieldCacheEntry(o, f)).Cast<ICacheEntry>());
                        _fieldCache.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(p => new PropertyCacheEntry(o, p)).Cast<ICacheEntry>());
                    }
                }

                _inspectorScrollPos = Vector2.zero;
            }
            catch (Exception ex)
            {
                BepInLogger.Log("Inspector crash: " + ex);
            }
        }

        private Boolean CanCovert(string value, Type type)
        {
            if (_canCovertCache.ContainsKey(type))
                return _canCovertCache[type];

            try
            {
                var _ = Convert.ChangeType(value, type);
                _canCovertCache[type] = true;
                return true;
            }
            catch
            {
                _canCovertCache[type] = false;
                return false;
            }
        }

        private void CheatWindow(int id)
        {
            try
            {
                var hFlag = FindObjectOfType<HFlag>();
                var talkScene = FindObjectOfType<TalkScene>();

                _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
                {
                    if (!_gameMgr.saveData.isOpening)
                    {
                        DrawPlayerCheats();
                    }
                    else
                    {
                        GUILayout.Label("Start the game to see player cheats");
                    }

                    DrawSeparator();

                    if (hFlag != null)
                    {
                        DrawHSceneCheats(hFlag);
                    }

                    DrawSeparator();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Current girl stats");

                        _currentVisibleGirl = GetCurrentAdvHeroine() ?? GetCurrentTalkSceneHeroine(talkScene) ?? GetCurrentHflagHeroine(hFlag) ?? _currentVisibleGirl;

                        if (_currentVisibleGirl != null)
                        {
                            DrawCurrentHeroineCheats(_currentVisibleGirl);
                        }
                        else
                        {
                            GUILayout.Label("Talk to a girl to access her stats");
                        }
                    }
                    GUILayout.EndVertical();

                    DrawSeparator();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Open in inspector");
                        foreach (var obj in new[]
                        {
                            new KeyValuePair<object, string>(_gameMgr?.HeroineList, "Heroine list"),
                            new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                            new KeyValuePair<object, string>(Manager.Scene.Instance, "Manager.Scene.Instance"),
                            new KeyValuePair<object, string>(Manager.Communication.Instance, "Manager.Communication.Instance"),
                            new KeyValuePair<object, string>(Manager.Sound.Instance, "Manager.Sound.Instance"),
                            new KeyValuePair<object, string>(hFlag, "HFlag"),
                            new KeyValuePair<object, string>(talkScene, "TalkScene"),
                        })
                        {
                            if (obj.Key == null) continue;
                            if (GUILayout.Button(obj.Value))
                            {
                                InspectorClear();
                                InspectorPush(new InspectorStackEntry(obj.Key, obj.Value));
                            }
                        }
                        GUILayout.EndVertical();
                    }

                    DrawSeparator();

                    GUILayout.Label("Created by MarC0 @ HongFire");
                }
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                BepInLogger.Log("CheatWindow crash: " + ex.Message);
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
                {
                    currentAdvGirl.talkTime = currentAdvGirl.talkTimeMax;
                }

                currentAdvGirl.isVirgin = GUILayout.Toggle(currentAdvGirl.isVirgin, "isVirgin");
                currentAdvGirl.isAnalVirgin = GUILayout.Toggle(currentAdvGirl.isAnalVirgin, "isAnalVirgin");
                currentAdvGirl.isAnger = GUILayout.Toggle(currentAdvGirl.isAnger, "Is angry");
                currentAdvGirl.isDate = GUILayout.Toggle(currentAdvGirl.isDate, "Date promised");
                currentAdvGirl.isFirstGirlfriend = GUILayout.Toggle(currentAdvGirl.isFirstGirlfriend, "isFirstGirlfriend");
                currentAdvGirl.isGirlfriend = GUILayout.Toggle(currentAdvGirl.isGirlfriend, "isGirlfriend");

                currentAdvGirl.denial.notCondom = GUILayout.Toggle(currentAdvGirl.denial.notCondom, "Denies no condom");
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, "Denies anal");
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, "Denies vibrator");
                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, "Denies kiss");
                currentAdvGirl.denial.massage = GUILayout.Toggle(currentAdvGirl.denial.massage, "Denies strong massage");

                if (GUILayout.Button("Open current girl in inspector"))
                {
                    InspectorClear();
                    InspectorPush(new InspectorStackEntry(currentAdvGirl, "Heroine " + currentAdvGirl.Name));
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

        private void DrawEditableValue(ICacheEntry field, object value)
        {
            var isBeingEdited = _currentlyEditingTag == field;
            var text = isBeingEdited ? _currentlyEditingText : ExtractText(value);
            var result = GUILayout.TextField(text, GUILayout.ExpandWidth(true));

            if (!Equals(text, result) || isBeingEdited)
            {
                if (_userHasHitReturn)
                {
                    _currentlyEditingTag = null;
                    _userHasHitReturn = false;
                    try
                    {
                        var converted = Convert.ChangeType(result, field.Type());
                        if (!Equals(converted, value))
                            field.Set(converted);
                    }
                    catch (Exception ex)
                    {
                        BepInLogger.Log("Failed to set value - " + ex.Message);
                    }
                }
                else
                {
                    _currentlyEditingText = result;
                    _currentlyEditingTag = field;
                }
            }
        }

        private void DrawPlayerCheats()
        {
            GUILayout.BeginVertical(GUI.skin.box);
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

                    var cycle = FindObjectsOfType<ActionGame.Cycle>().FirstOrDefault();
                    if (cycle != null)
                    {
                        if (cycle.timerVisible)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label("Time: " + cycle.timer.ToString("N1"), GUILayout.Width(65));
                                var newVal = GUILayout.HorizontalSlider(cycle.timer, 0, ActionGame.Cycle.TIME_LIMIT);
                                if (Math.Abs(newVal - cycle.timer) > 0.09)
                                {
                                    typeof(ActionGame.Cycle)
                                        .GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)?
                                        .SetValue(cycle, newVal);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Day of the week: " + cycle.nowWeek);
                            if (GUILayout.Button("Next"))
                            {
                                cycle.Change(cycle.nowWeek.Next());
                            }
                        }
                        GUILayout.EndHorizontal();

                    }
                }

                if (GUILayout.Button("Add 10000 club points (+1 level)"))
                {
                    _gameMgr.saveData.clubReport.comAdd += 10000;
                }

                if (GUILayout.Button("Open player data in inspector"))
                {
                    InspectorClear();
                    InspectorPush(new InspectorStackEntry(_gameMgr.saveData.player, "Player data"));
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawValue(object value)
        {
            GUILayout.TextArea(ExtractText(value), GUI.skin.label, GUILayout.ExpandWidth(true));
        }

        private void DrawVariableName(ICacheEntry field)
        {
            GUILayout.TextArea(field.Name(), GUI.skin.label, GUILayout.Width(InspectorNameWidth), GUILayout.MaxWidth(InspectorNameWidth));
        }

        private void DrawVariableNameEnterButton(ICacheEntry field, object value)
        {
            if (GUILayout.Button(field.Name(), GUILayout.Width(InspectorNameWidth), GUILayout.MaxWidth(InspectorNameWidth)))
            {
                if (value != null)
                {
                    _nextToPush = new InspectorStackEntry(value, field.Name());
                }
            }
        }

        private SaveData.Heroine GetCurrentAdvHeroine()
        {
            try
            {
                var nowScene = _gameMgr.actScene?.AdvScene?.nowScene;
                var currentAdvGirl = nowScene?.GetType().GetField("m_TargetHeroine", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(nowScene) as SaveData.Heroine;
                return currentAdvGirl;
            }
            catch { return null; }
        }

        private SaveData.Heroine GetCurrentHflagHeroine(HFlag hFlag)
        {
            return hFlag?.lstHeroine?.FirstOrDefault();
        }

        private SaveData.Heroine GetCurrentTalkSceneHeroine(TalkScene talkScene)
        {
            return talkScene?.targetHeroine;
        }

        private string GetHExpText(SaveData.Heroine currentAdvGirl)
        {
            return _hExpNames[(int)currentAdvGirl.HExperience];
        }

        private void InspectorClear()
        {
            _inspectorStack.Clear();
            CacheFields(null);
        }

        private void InspectorPop()
        {
            _inspectorStack.Pop();
            CacheFields(_inspectorStack.Peek().Instance);
        }

        private void InspectorPush(InspectorStackEntry o)
        {
            _inspectorStack.Push(o);
            CacheFields(o.Instance);
        }

        private void InspectorWindow(int id)
        {
            try
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Exit the inspector"))
                        {
                            InspectorClear();
                        }
                        GUILayout.Label("To go deeper click on the variable names. Click on the list below to go back. To edit variables type in a new value and press enter.");
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    foreach (var item in _inspectorStack.Reverse().ToArray())
                    {
                        if (GUILayout.Button(item.Name, GUILayout.ExpandWidth(false)))
                        {
                            while (_inspectorStack.Peek() != item)
                                InspectorPop();

                            return;
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Value type", GUI.skin.box, GUILayout.Width(InspectorTypeWidth));
                            GUILayout.Label("Variable name", GUI.skin.box, GUILayout.Width(InspectorNameWidth));
                            GUILayout.Label("Value", GUI.skin.box, GUILayout.ExpandWidth(true));
                        }
                        GUILayout.EndHorizontal();

                        _inspectorScrollPos = GUILayout.BeginScrollView(_inspectorScrollPos);
                        {
                            GUILayout.BeginVertical();

                            foreach (var field in _fieldCache)
                            {
                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Label(field.TypeName(), GUILayout.Width(InspectorTypeWidth), GUILayout.MaxWidth(InspectorTypeWidth));

                                    var value = field.Get();

                                    if (field.Type().IsPrimitive)
                                        DrawVariableName(field);
                                    else
                                        DrawVariableNameEnterButton(field, value);

                                    if (CanCovert(ExtractText(value), field.Type()) && field.CanSet())
                                        DrawEditableValue(field, value);
                                    else
                                        DrawValue(value);
                                }
                                GUILayout.EndHorizontal();
                            }
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndScrollView();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                BepInLogger.Log("Inspector crash: " + ex.Message);
            }

            GUI.DragWindow();
        }

        public void OnGUI()
        {
            if (!_showGui) return;

            var e = Event.current;
            if (e.keyCode == KeyCode.Return) _userHasHitReturn = true;

            _windowRect = GUILayout.Window(591, _windowRect, CheatWindow, _mainWindowTitle);

            if (_inspectorStack.Count != 0)
                _windowRect2 = GUILayout.Window(592, _windowRect2, InspectorWindow, "Inspector");
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            if (_windowRect.IsDefault())
                _windowRect = new Rect(_screenRect.width - 50 - 270, 100, 270, 380);

            if (_windowRect2.IsDefault())
            {
                const int width = 800;
                const int height = 600;
                _windowRect2 = new Rect(_screenRect.width / 2 - width / 2, _screenRect.height / 2 - height / 2, width, height);
            }
        }

        public void Start()
        {
            _gameMgr = Manager.Game.Instance;

            _mainWindowTitle = "Cheat Tools" + Assembly.GetExecutingAssembly().GetName().Version;
        }

        public void Update()
        {
            if (_nextToPush != null)
            {
                InspectorPush(_nextToPush);

                _nextToPush = null;
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                _showGui = !_showGui;
                SetWindowSizes();
            }
        }
    }
}