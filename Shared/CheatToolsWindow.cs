using System;
using System.Collections.Generic;
using System.Reflection;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using UnityEngine;

namespace CheatTools
{
    public class CheatToolsWindow
    {
        private const int ScreenOffset = 20;

        public readonly RuntimeUnityEditorCore Editor;

        private readonly string _mainWindowTitle;
        private Vector2 _cheatsScrollPos;
        private Rect _cheatWindowRect;
        private Rect _screenRect;
        private bool _show;

        public static readonly List<CheatEntry> Cheats = new List<CheatEntry>();

        public CheatToolsWindow(RuntimeUnityEditorCore editor)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _mainWindowTitle = "Cheat Tools " + Assembly.GetExecutingAssembly().GetName().Version;

            Cheats.Add(new CheatEntry(window => true, DrawTimeControls, null));
        }

        private void DrawTimeControls(CheatToolsWindow window)
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
                GUILayout.Label((int)Math.Round(Time.timeScale * 100) + "%", GUILayout.Width(35));
                Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0, 5, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false))) Time.timeScale = 1;
            }
            GUILayout.EndHorizontal();
        }

        public bool Show
        {
            get => _show;
            set
            {
                _show = value;
                Editor.Show = value;

                if (value)
                {
                    SetWindowSizes();
                    OnShown(this);
                }
            }
        }

        public static Action<CheatToolsWindow> OnShown { get; set; }

        private void CheatWindowContents(int id)
        {
            _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
            {
                foreach (var cheatEntry in Cheats)
                {
                    var success = cheatEntry.Condition(this);
                    if (!success && cheatEntry.FailString == null) continue;

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        if (success)
                            cheatEntry.DrawCheats(this);
                        else
                            GUILayout.Label(cheatEntry.FailString);
                    }
                    GUILayout.EndVertical();
                }
            }
            GUILayout.EndScrollView();

            _cheatWindowRect = RuntimeUnityEditor.Core.Utils.IMGUIUtils.DragOrResize(id, _cheatWindowRect);
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

            const int cheatWindowHeight = 500;
            _cheatWindowRect = new Rect(_screenRect.xMin, _screenRect.yMax - cheatWindowHeight, 270, cheatWindowHeight);
        }
    }

    public class CheatEntry
    {
        public Func<CheatToolsWindow, bool> Condition;
        public string FailString;
        public Action<CheatToolsWindow> DrawCheats;

        public CheatEntry(Func<CheatToolsWindow, bool> condition, Action<CheatToolsWindow> drawCheats, string failString)
        {
            Condition = condition;
            DrawCheats = drawCheats;
            FailString = failString;
        }

        public static CheatEntry CreateOpenInInspectorButtons(Func<KeyValuePair<object, string>[]> func)
        {
            void DrawOpenInInspectorButtons(CheatToolsWindow window)
            {
                GUILayout.Label("Open in inspector");
                foreach (var obj in func())
                {
                    if (obj.Key == null) continue;
                    if (GUILayout.Button(obj.Value))
                    {
                        if (obj.Key is Type t)
                            window.Editor.Inspector.Push(new StaticStackEntry(t, obj.Value), true);
                        else if (obj.Key is Func<object> f)
                            window.Editor.Inspector.Push(new InstanceStackEntry(f(), obj.Value), true);
                        else
                            window.Editor.Inspector.Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                    }
                }
            }
            return new CheatEntry(w => func() != null, DrawOpenInInspectorButtons, null);
        }
    }
}
