using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logger;
using Object = UnityEngine.Object;

namespace CheatTools
{
    public class Inspector
    {
        private const int InspectorTypeWidth = 170;
        private const int InspectorNameWidth = 240;

        private readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();
        private readonly List<ICacheEntry> _fieldCache = new List<ICacheEntry>();
        private readonly Stack<InspectorStackEntry> _inspectorStack = new Stack<InspectorStackEntry>();

        private GUIStyle _alignedButtonStyle;
        private Rect _inspectorWindowRect;
        private Vector2 _inspectorScrollPos;
        private Vector2 _inspectorStackScrollPos;
        private int _inspectorValueWidth;

        private InspectorStackEntry _nextToPush;

        private object _currentlyEditingTag;
        private string _currentlyEditingText;
        private bool _userHasHitReturn;

        private void CacheFields(object objectToOpen)
        {
            _inspectorScrollPos = Vector2.zero;
            _fieldCache.Clear();

            if (objectToOpen == null) return;

            IEnumerable<ICacheEntry> CacheMethods(object instance, MethodInfo[] typesToCheck)
            {
                var cacheItems = typesToCheck
                    .Where(x => !x.IsConstructor && !x.IsSpecialName && x.ReturnType != typeof(void) &&
                                x.GetParameters().Length == 0)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Where(x => x.Name != "MemberwiseClone" && x.Name != "obj_address") // Instant game crash
                    .Select(m =>
                    {
                        if (m.ContainsGenericParameters)
                            try
                            {
                                return m.MakeGenericMethod(typeof(Object));
                            }
                            catch (Exception)
                            {
                                return null;
                            }
                        return m;
                    }).Where(x => x != null)
                    .Select(m => new MethodCacheEntry(instance, m)).Cast<ICacheEntry>();
                return cacheItems;
            }

            try
            {
                var type = objectToOpen.GetType();

                // If we somehow enter a string, this allows user to see what the string actually says
                if (type == typeof(string))
                    _fieldCache.Add(new ReadonlyCacheEntry("this", objectToOpen));
                else if (objectToOpen is Transform tr)
                    _fieldCache.Add(new ReadonlyCacheEntry("Child objects", tr.Cast<Transform>().ToArray()));
                else if (objectToOpen is GameObject ob && ob.transform != null)
                    _fieldCache.Add(new ReadonlyCacheEntry("Child objects", ob.transform.Cast<Transform>().ToArray()));
                else if (objectToOpen is IEnumerable enumerable)
                {
                    _fieldCache.AddRange(enumerable.Cast<object>()
                        .Select((x, y) => x is ICacheEntry ? x : new ListCacheEntry(x, y))
                        .Cast<ICacheEntry>());
                }

                // Instance members
                _fieldCache.AddRange(type
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                               BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(f => new FieldCacheEntry(objectToOpen, f)).Cast<ICacheEntry>());
                _fieldCache.AddRange(type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                   BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new PropertyCacheEntry(objectToOpen, p)).Cast<ICacheEntry>());

                // Static members
                _fieldCache.AddRange(type
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                               BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(f => new FieldCacheEntry(null, f)).Cast<ICacheEntry>());
                _fieldCache.AddRange(type
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                   BindingFlags.FlattenHierarchy)
                    .Where(f => !f.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    .Select(p => new PropertyCacheEntry(null, p)).Cast<ICacheEntry>());

                // Methods
                _fieldCache.AddRange(CacheMethods(objectToOpen,
                    type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                    BindingFlags.FlattenHierarchy)));
                _fieldCache.AddRange(CacheMethods(null,
                    type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                    BindingFlags.FlattenHierarchy)));
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "Inspector CacheFields crash: " + ex);
            }
        }

        private bool CanCovert(string value, Type type)
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

        private void DrawEditableValue(ICacheEntry field, object value, params GUILayoutOption[] layoutParams)
        {
            var isBeingEdited = _currentlyEditingTag == field;
            var text = isBeingEdited ? _currentlyEditingText : Utilities.ExtractText(value);
            var result = GUILayout.TextField(text, layoutParams);

            if (!Equals(text, result) || isBeingEdited)
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

        private void DrawValue(object value, params GUILayoutOption[] layoutParams)
        {
            GUILayout.TextArea(Utilities.ExtractText(value), GUI.skin.label, layoutParams);
        }

        private void DrawVariableName(ICacheEntry field)
        {
            GUILayout.TextArea(field.Name(), GUI.skin.label, GUILayout.Width(InspectorNameWidth),
                GUILayout.MaxWidth(InspectorNameWidth));
        }

        private void DrawVariableNameEnterButton(ICacheEntry field)
        {
            if (GUILayout.Button(field.Name(), _alignedButtonStyle, GUILayout.Width(InspectorNameWidth),
                GUILayout.MaxWidth(InspectorNameWidth)))
            {
                var val = field.EnterValue();
                if (val != null)
                    _nextToPush = new InspectorStackEntry(val, field.Name());
            }
        }

        public void InspectorClear()
        {
            _inspectorStack.Clear();
            CacheFields(null);
        }

        private void InspectorPop()
        {
            _inspectorStack.Pop();
            CacheFields(_inspectorStack.Peek().Instance);
        }

        public void InspectorPush(InspectorStackEntry o)
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
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            GUILayout.Label("Find:");
                            foreach (var obj in new[]
                            {
                                new KeyValuePair<object, string>(
                                    Utilities.GetInstanceClassScanner().OrderBy(x => x.Name()), "Instances"),
                                new KeyValuePair<object, string>(Utilities.GetComponentScanner().OrderBy(x => x.Name()),
                                    "Components"),
                                new KeyValuePair<object, string>(
                                    Utilities.GetMonoBehaviourScanner().OrderBy(x => x.Name()), "MonoBehaviours"),
                                new KeyValuePair<object, string>(Utilities.GetTransformScanner().OrderBy(x => x.Name()),
                                    "Transforms")
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

                        GUILayout.Space(13);

                        GUILayout.BeginHorizontal(GUI.skin.box);
                        {
                            if (GUILayout.Button("Help"))
                                InspectorPush(InspectorHelpObj.Create());
                            if (GUILayout.Button("Close"))
                                InspectorClear();
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndHorizontal();

                    _inspectorStackScrollPos = GUILayout.BeginScrollView(_inspectorStackScrollPos, true, false,
                        GUI.skin.horizontalScrollbar, GUIStyle.none, GUIStyle.none, GUILayout.Height(46));
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false),
                            GUILayout.ExpandHeight(false));
                        foreach (var item in _inspectorStack.Reverse().ToArray())
                            if (GUILayout.Button(item.Name, GUILayout.ExpandWidth(false)))
                            {
                                while (_inspectorStack.Peek() != item)
                                    InspectorPop();

                                return;
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
                            var widthCalculated = false;
                            foreach (var entry in _fieldCache)
                            {
                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Label(entry.TypeName(), GUILayout.Width(InspectorTypeWidth),
                                        GUILayout.MaxWidth(InspectorTypeWidth));

                                    var value = entry.GetValue();

                                    if (entry.CanEnterValue() || value is Exception)
                                        DrawVariableNameEnterButton(entry);
                                    else
                                        DrawVariableName(entry);

                                    if (_fieldCache.Count < 200)
                                    {
                                        var widthParam = widthCalculated
                                            ? GUILayout.Width(_inspectorValueWidth)
                                            : GUILayout.ExpandWidth(true);

                                        if (entry.CanSetValue() &&
                                            CanCovert(Utilities.ExtractText(value), entry.Type()))
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

        public void DisplayInspector()
        {
            if (_alignedButtonStyle == null)
            {
                _alignedButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };
            }

            if (Event.current.keyCode == KeyCode.Return) _userHasHitReturn = true;

            if (_inspectorStack.Count != 0)
            {
                Utilities.DrawSolidWindowBackground(_inspectorWindowRect);
                _inspectorWindowRect = GUILayout.Window(592, _inspectorWindowRect, InspectorWindow, "Inspector");
            }
        }

        public void UpdateWindowSize(Rect screenRect)
        {
            const int width = 800;
            //const int height = 600;
            //_inspectorWindowRect = new Rect(screenRect.width / 2 - width / 2, screenRect.height / 2 - height / 2, width, height);
            
            int height = (int)(screenRect.height / 3) * 2;

            _inspectorWindowRect = new Rect(screenRect.xMin + screenRect.width / 2 - width / 2 + 22, screenRect.yMin, width, height);
        }

        public void InspectorUpdate()
        {
            if (_nextToPush != null)
            {
                InspectorPush(_nextToPush);

                _nextToPush = null;
            }
        }
    }
}