using System;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace CheatTools;

/// <summary>
/// GUILayout.SelectionGrid is broken, doesn't show anything
/// </summary>
internal static class DrawUtils
{
    private static readonly GUILayoutOption[] _LayoutOptionsName = { GUILayout.Width(80) };
    public static void DrawOne<T>(string name, Func<T> get, Action<T> set)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            var oldValue = get();
            GUILayout.Label(name + ": ", _LayoutOptionsName);
            GUILayout.FlexibleSpace();
            var result = GUILayout.TextField(oldValue.ToString(), GUILayout.Width(50));
            if (GUI.changed)
            {
                var newValue = (T)Convert.ChangeType(result, typeof(T));
                if (!newValue.Equals(oldValue))
                    set(newValue);
            }
        }
        GUILayout.EndHorizontal();
    }
    public static void DrawByte(string name, Func<byte> get, Action<byte> set)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            GUILayout.Label(name + ": ", _LayoutOptionsName);
            GUILayout.FlexibleSpace();
            var oldValue = get();
            var result = GUILayout.TextField(oldValue.ToString(), GUILayout.Width(50));
            if (GUI.changed && byte.TryParse(result, out var newValue) && !newValue.Equals(oldValue)) set(newValue);
        }
        GUILayout.EndHorizontal();
    }
    public static void DrawInt(string name, Func<int> get, Action<int> set, string tooltip = null)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            GUILayout.Label(new GUIContent(name + ": ", null, tooltip), _LayoutOptionsName);
            GUILayout.FlexibleSpace();
            var oldValue = get();
            var result = GUILayout.TextField(oldValue.ToString(), GUILayout.Width(50));
            if (GUI.changed && int.TryParse(result, out var newValue) && !newValue.Equals(oldValue)) set(newValue);
        }
        GUILayout.EndHorizontal();
    }
    public static void DrawNums(string name, byte count, Func<byte> get, Action<byte> set)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            GUILayout.Label(name + ": ", _LayoutOptionsName);
            GUILayout.FlexibleSpace();
            var oldValue = get();

            for (byte i = 0; i < count; i++)
            {
                if (oldValue == i) GUI.color = Color.green;
                if (GUILayout.Button((i + 1).ToString(), IMGUIUtils.LayoutOptionsExpandWidthFalse)) set(i);
                GUI.color = Color.white;
            }
        }
        GUILayout.EndHorizontal();
    }
    public static void DrawStrings(string name, string[] strings, Func<byte> get, Action<byte> set)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            GUILayout.Label(name + ": ", _LayoutOptionsName);
            GUILayout.FlexibleSpace();
            var oldValue = get();

            for (byte i = 0; i < strings.Length; i++)
            {
                if (oldValue == i) GUI.color = Color.green;
                if (GUILayout.Button(strings[i], IMGUIUtils.LayoutOptionsExpandWidthFalse)) set(i);
                GUI.color = Color.white;
            }
        }
        GUILayout.EndHorizontal();
    }
    public static void DrawBool(string name, Func<bool> get, Action<bool> set)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            var result = GUILayout.Toggle(get(), name);
            if (GUI.changed) set(result);
        }
        GUILayout.EndHorizontal();
    }

    public static void DrawSlider(string name, int min, int max, Func<int> get, Action<int> set, string tooltip = null)
        => DrawSlider(name, (float)min, (float)max, () => (float)get(), f => set(Mathf.RoundToInt(f)), tooltip);

    public static void DrawSlider(string name, float min, float max, Func<float> get, Action<float> set, string tooltip = null)
    {
        GUILayout.BeginHorizontal();
        {
            GUI.changed = false;
            GUILayout.Label(new GUIContent(name + ": ", null, tooltip), _LayoutOptionsName);
            GUILayout.Space(4);

            var oldValue = get();

            var newValue = GUILayout.HorizontalSlider(oldValue, min, max, IMGUIUtils.LayoutOptionsExpandWidthTrue);
            if (GUI.changed)
            {
                GUI.changed = false;
                set(newValue);
                oldValue = newValue;
            }

            GUILayout.Space(4);

            var result = GUILayout.TextField(oldValue.ToString(), GUILayout.Width(50));
            if (GUI.changed && float.TryParse(result, out newValue) && !newValue.Equals(oldValue)) set(newValue);
        }
        GUILayout.EndHorizontal();
    }
}
