using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", "1.6")]
    public partial class CheatTools : BaseUnityPlugin
    {
        private const int InspectorTypeWidth = 170, InspectorNameWidth = 240;
        private int _inspectorValueWidth;
        private const int ScreenOffset = 20;
        private readonly string[] _hExpNames = { "First time", "Inexperienced", "Experienced", "Perverted" };

        private Rect _screenRect;
        private bool _showGui;
        private bool _userHasHitReturn;
        private Rect _windowRect, _windowRect2;
        private Vector2 _inspectorScrollPos, _cheatsScrollPos, _inspectorStackScrollPos;
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
            if (value == null) return "NULL";
            switch (value)
            {
                case string str:
                    return str;
                case Transform t:
                    return t.name;
                case GameObject o:
                    return o.name;
                case SaveData.Heroine heroine:
                    return heroine.Name;
                case SaveData.CharaData.Params.Data d:
                    return $"[{d.key} | {d.value}]";
                case ICollection collection:
                    return $"Count = {collection.Count}";
                case IEnumerable _:
                    return "IS ENUMERABLE";
                default:
                    {
                        var valueType = value.GetType();
                        try
                        {
                            if (valueType.IsGenericType)
                            {
                                var baseType = valueType.GetGenericTypeDefinition();
                                if (baseType == typeof(KeyValuePair<,>))
                                {
                                    //var argTypes = baseType.GetGenericArguments();
                                    var kvpKey = valueType.GetProperty("Key")?.GetValue(value, null);
                                    var kvpValue = valueType.GetProperty("Value")?.GetValue(value, null);
                                    return $"[{ExtractText(kvpKey)} | {ExtractText(kvpValue)}]";
                                }
                            }

                            return value.ToString();
                        }
                        catch
                        {
                            return valueType.Name;
                        }
                    }
            }
        }

        private void CacheFields(object o)
        {
            try
            {
                _fieldCache.Clear();
                if (o != null)
                {
                    /*if (o is Transform t)
                    {
                        _fieldCache
                    }*/

                    if (!(o is Transform) && !(o is string) && o is IEnumerable enumerable)
                    {
                        _fieldCache.AddRange(enumerable.Cast<object>().Select((x, y) => x is ICacheEntry ? x : new ListCacheEntry(x, y)).Cast<ICacheEntry>());
                    }
                    else
                    {
                        var type = o.GetType();
                        if (type == typeof(string))
                            _fieldCache.Add(new ReadonlyCacheEntry("this", o));

                        _fieldCache.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                            .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                            .Select(f => new FieldCacheEntry(o, f)).Cast<ICacheEntry>());
                        _fieldCache.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                            .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                            .Select(p => new PropertyCacheEntry(o, p)).Cast<ICacheEntry>());

                        _fieldCache.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                            .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                            .Select(f => new FieldCacheEntry(null, f)).Cast<ICacheEntry>());
                        _fieldCache.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                            .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                            .Select(p => new PropertyCacheEntry(null, p)).Cast<ICacheEntry>());

                        _fieldCache.AddRange(CacheMethods(o, type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)));
                        _fieldCache.AddRange(CacheMethods(null, type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)));
                    }
                }

                _inspectorScrollPos = Vector2.zero;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "Inspector CacheFields crash: " + ex);
            }
        }

        private static IEnumerable<ICacheEntry> CacheMethods(object o, MethodInfo[] typesToCheck)
        {
            var cacheItems = typesToCheck
                .Where(x => !x.IsConstructor && !x.IsSpecialName && x.ReturnType != typeof(void) && x.GetParameters().Length == 0)
                .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Where(x => x.Name != "MemberwiseClone" && x.Name != "obj_address") // Instant game crash
                .Select(m =>
                {
                    if (m.ContainsGenericParameters)
                        try
                        {
                            return m.MakeGenericMethod(typeof(UnityEngine.Object));
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    return m;
                }).Where(x => x != null)
                .Select(m => new MethodCacheEntry(o, m)).Cast<ICacheEntry>();
            return cacheItems;
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
                    if (_gameMgr != null && !_gameMgr.saveData.isOpening)
                    {
                        DrawPlayerCheats();
                    }
                    else
                    {
                        GUILayout.Label("Start the game to see player cheats");
                    }
                    
                    if (hFlag != null)
                    {
                        DrawHSceneCheats(hFlag);
                    }

                    //Now can quit first time H scene
                    var hSprite = FindObjectOfType<HSprite>();
                    if (hSprite != null)
                    {
                        if (GUILayout.Button("Quit H scene (alpha)"))
                        {
                            hSprite.btnEnd.onClick.Invoke();
                        }
                    }
                    
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
                            new KeyValuePair<object, string>(_gameMgr?.HeroineList.Select(x=>new ReadonlyCacheEntry(x.ChaName, x)), "Heroine list"),
                            new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                            new KeyValuePair<object, string>(Manager.Scene.Instance, "Manager.Scene.Instance"),
                            new KeyValuePair<object, string>(Manager.Communication.Instance, "Manager.Communication.Instance"),
                            new KeyValuePair<object, string>(Manager.Sound.Instance, "Manager.Sound.Instance"),
                            new KeyValuePair<object, string>(hFlag, "HFlag"),
                            new KeyValuePair<object, string>(talkScene, "TalkScene"),
                            new KeyValuePair<object, string>(Studio.Studio.Instance, "Studio.Instance"),
                            new KeyValuePair<object, string>(_gameMgr.transform.root.GetComponents<object>(), "Game Root"),
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
                Logger.Log(LogLevel.Error, "[CheatTools] CheatWindow crash: " + ex.Message);
            }

            GUI.DragWindow();
        }

        private static IEnumerable<ReadonlyCacheEntry> GetTransformScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Transforms...");

            var trt = typeof(Transform);
            var types = GetAllComponentTypes().Where(x => trt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, false))
                yield return component;
        }

        private static IEnumerable<ReadonlyCacheEntry> GetMonoBehaviourScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for MonoBehaviours...");

            var mbt = typeof(MonoBehaviour);
            var types = GetAllComponentTypes().Where(x => mbt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, true))
                yield return component;
        }

        private static IEnumerable<ReadonlyCacheEntry> GetComponentScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Components...");

            var mbt = typeof(MonoBehaviour);
            var trt = typeof(Transform);
            var allComps = GetAllComponentTypes().ToList();
            var types = allComps.Where(x => !mbt.IsAssignableFrom(x) && !trt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, true))
                yield return component;
        }

        private static IEnumerable<ReadonlyCacheEntry> ScanComponentTypes(IEnumerable<Type> types, bool noTransfroms)
        {
            var allObjects = from type in types
                             let components = FindObjectsOfType(type).OfType<Component>()
                             from component in components
                             where !(noTransfroms && component is Transform)
                             select component;

            string GetTransformPath(Transform tr)
            {
                if (tr.parent != null)
                    return GetTransformPath(tr.parent) + "/" + tr.name;
                return tr.name;
            }

            foreach (var obj in allObjects.Distinct())
                yield return new ReadonlyCacheEntry($"{GetTransformPath(obj.transform)} ({obj.GetType().Name})", obj);
        }


        private static IEnumerable<Type> GetAllComponentTypes()
        {
            var compType = typeof(Component);
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                {
                    try
                    {
                        return x.GetTypes();
                    }
                    catch (SystemException)
                    {
                        return Enumerable.Empty<Type>();
                    }
                })
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .Where(compType.IsAssignableFrom);
        }

        private static IEnumerable<ReadonlyCacheEntry> GetInstanceClassScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for class instances...");

            var query = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                {
                    try
                    {
                        return x.GetTypes();
                    }
                    catch (SystemException)
                    {
                        return Enumerable.Empty<Type>();
                    }
                })
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters);

            foreach (var type in query)
            {
                object obj = null;
                try
                {
                    obj = type.GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                        ?.GetValue(null, null);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Debug, ex.ToString());
                }
                if (obj != null)
                    yield return new ReadonlyCacheEntry(type.Name + ".Instance", obj);
            }
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

                currentAdvGirl.denial.kiss = GUILayout.Toggle(currentAdvGirl.denial.kiss, "Won't refuse kiss");
                currentAdvGirl.denial.massage = GUILayout.Toggle(currentAdvGirl.denial.massage, "Won't refuse strong massage");
                currentAdvGirl.denial.anal = GUILayout.Toggle(currentAdvGirl.denial.anal, "Won't refuse anal");
                currentAdvGirl.denial.aibu = GUILayout.Toggle(currentAdvGirl.denial.aibu, "Won't refuse vibrator");
                currentAdvGirl.denial.notCondom = GUILayout.Toggle(currentAdvGirl.denial.notCondom, "Insert w/o condom OK");

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

        private void DrawEditableValue(ICacheEntry field, object value, params GUILayoutOption[] layoutParams)
        {
            var isBeingEdited = _currentlyEditingTag == field;
            var text = isBeingEdited ? _currentlyEditingText : ExtractText(value);
            var result = GUILayout.TextField(text, layoutParams);

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
                            field.SetValue(converted);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "Failed to set value - " + ex.Message);
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

        private void DrawValue(object value, params GUILayoutOption[] layoutParams)
        {
            GUILayout.TextArea(ExtractText(value), GUI.skin.label, layoutParams);
        }

        private void DrawVariableName(ICacheEntry field)
        {
            GUILayout.TextArea(field.Name(), GUI.skin.label, GUILayout.Width(InspectorNameWidth), GUILayout.MaxWidth(InspectorNameWidth));
        }

        private void DrawVariableNameEnterButton(ICacheEntry field)
        {
            if (GUILayout.Button(field.Name(), _alignedButtonStyle, GUILayout.Width(InspectorNameWidth), GUILayout.MaxWidth(InspectorNameWidth)))
            {
                var val = field.EnterValue();
                if (val != null)
                {
                    _nextToPush = new InspectorStackEntry(val, field.Name());
                }
            }
        }

        private SaveData.Heroine GetCurrentAdvHeroine()
        {
            try
            {
                var nowScene = _gameMgr?.actScene?.AdvScene?.nowScene;
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
                        if (GUILayout.Button("Close Inspector"))
                        {
                            InspectorClear();
                        }
                        GUILayout.Label("Find");
                        foreach (var obj in new[]
                          {
                            new KeyValuePair<object, string>(GetInstanceClassScanner().OrderBy(x=>x.Name()), "Instances"),
                            new KeyValuePair<object, string>(GetComponentScanner().OrderBy(x=>x.Name()), "Components"),
                            new KeyValuePair<object, string>(GetMonoBehaviourScanner().OrderBy(x=>x.Name()), "MonoBehaviours"),
                            new KeyValuePair<object, string>(GetTransformScanner().OrderBy(x=>x.Name()), "Transforms"),
//                            new KeyValuePair<object, string>(GetTypeScanner(_inspectorStack.Peek().GetType()).OrderBy(x=>x.Name()), _inspectorStack.Peek().GetType().ToString()+"s"),
                        })
                        {
                            if (obj.Key == null) continue;
                            if (GUILayout.Button(obj.Value))
                            {
                                InspectorClear();
                                InspectorPush(new InspectorStackEntry(obj.Key, obj.Value));
                            }
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Label("To go deeper click on the variable names. Click on the list below to go back. To edit variables type in a new value and press enter.");

                    _inspectorStackScrollPos = GUILayout.BeginScrollView(_inspectorStackScrollPos, true, false,
                        GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none, GUILayout.Height(46));
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
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
                    }
                    GUILayout.EndScrollView();

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
                            bool widthCalculated = false;
                            foreach (var entry in _fieldCache)
                            {
                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Label(entry.TypeName(), GUILayout.Width(InspectorTypeWidth), GUILayout.MaxWidth(InspectorTypeWidth));

                                    var value = entry.GetValue();

                                    if (entry.CanEnterValue())
                                        DrawVariableNameEnterButton(entry);
                                    else
                                        DrawVariableName(entry);

                                    if (_fieldCache.Count < 200)
                                    {
                                        var widthParam = widthCalculated
                                            ? GUILayout.Width(_inspectorValueWidth)
                                            : GUILayout.ExpandWidth(true);

                                        if (entry.CanSetValue() && CanCovert(ExtractText(value), entry.Type()))
                                            DrawEditableValue(entry, value, widthParam);
                                        else
                                            DrawValue(value, widthParam);

                                        // Calculate width only once
                                        if (!widthCalculated && Event.current.type == EventType.Repaint)
                                        {
                                            _inspectorValueWidth = (int)GUILayoutUtility.GetLastRect().width;
                                            widthCalculated = true;
                                        }
                                    }
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
                Logger.Log(LogLevel.Error, "[CheatTools] Inspector crash: " + ex);
                InspectorClear();
            }

            GUI.DragWindow();
        }

        private object GetTypeScanner(Type type)
        {
            throw new NotImplementedException();
        }

        protected void OnGUI()
        {
            if (!_showGui) return;

            if (_alignedButtonStyle == null)
                _alignedButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };

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

        protected void Start()
        {
            _gameMgr = Manager.Game.Instance;

            _mainWindowTitle = "Cheat Tools" + Assembly.GetExecutingAssembly().GetName().Version;
        }

        private GUIStyle _alignedButtonStyle;

        protected void Update()
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
