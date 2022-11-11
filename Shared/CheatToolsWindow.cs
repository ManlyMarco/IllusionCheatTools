using System;
using System.Collections.Generic;
using System.Reflection;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace CheatTools
{
    public sealed class CheatToolsWindow : Window<CheatToolsWindow>
    {
        public readonly RuntimeUnityEditorCore Editor;

        private Vector2 _cheatsScrollPos;

        public static readonly List<CheatEntry> Cheats = new List<CheatEntry>();

        protected override void Initialize(InitSettings initSettings)
        {
            OnInitialize?.Invoke(this);
            Enabled = true;
        }

        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);
            if (visible)
                OnShown?.Invoke(this);
        }

        protected override Rect GetDefaultWindowRect(Rect screenRect)
        {
            const int cheatWindowHeight = 500;
            return new Rect(screenRect.xMin, screenRect.yMax - cheatWindowHeight, 270, cheatWindowHeight);
        }

        public CheatToolsWindow(RuntimeUnityEditorCore editor)
        {
            Editor = editor ?? throw new ArgumentNullException(nameof(editor));
            Title = "Cheat Tools " + Assembly.GetExecutingAssembly().GetName().Version;
            DisplayName = "Cheat Tools";

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

        public static event Action<CheatToolsWindow> OnInitialize;
        public static event Action<CheatToolsWindow> OnShown;

        protected override void DrawContents()
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
                            Inspector.Instance.Push(new StaticStackEntry(t, obj.Value), true);
                        else if (obj.Key is Func<object> f)
                            Inspector.Instance.Push(new InstanceStackEntry(f(), obj.Value), true);
                        else
                            Inspector.Instance.Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                    }
                }
            }
            return new CheatEntry(w => func() != null, DrawOpenInInspectorButtons, null);
        }
    }
}
