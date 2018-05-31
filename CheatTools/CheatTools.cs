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
        private readonly int inspectorTypeWidth = 170, inspectorNameWidth = 200;
        private readonly int screenOffset = 20;
        private Dictionary<Type, bool> CanCovertCache = new Dictionary<Type, bool>();
        private object currentlyEditingTag;
        private string currentlyEditingText;
        private List<ICacheEntry> fieldCache = new List<ICacheEntry>();
        private Manager.Game gameMgr;
        private string[] hExpNames = { "First time", "Inexperienced", "Used to", "Perverted" };
        private Vector2 inspectorScrollPos, cheatsScrollPos;
        private Stack<InspectorStackEntry> inspectorStack = new Stack<InspectorStackEntry>();
        private string mainWindowTitle;
        private InspectorStackEntry nextToPush;
        private Rect screenRect;
        private bool showGui = false;
        private bool userHasHitReturn;
        private Rect windowRect, windowRect2;

        private static void DrawSeparator()
        {
            GUILayout.Space(5);
        }

        private static string ExtractText(object value)
        {
            if (value is string str) return str;

            if (value is ICollection collection) return $"Count = {collection.Count}";

            if (value is IEnumerable enumerable) return "IS ENUMERABLE";

            if (value is SaveData.Heroine heroine) return heroine.Name;

            return value?.ToString() ?? "NULL";
        }

        private void CacheFields(object o)
        {
            try
            {
                fieldCache.Clear();
                if (o != null)
                {
                    if (o is IEnumerable enumerable)
                    {
                        fieldCache.AddRange(enumerable.Cast<object>().Select((x, y) => new ListCacheEntry(x, y)).Cast<ICacheEntry>());
                    }
                    else
                    {
                        Type type = o.GetType();
                        fieldCache.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(f => new FieldCacheEntry(o, f)).Cast<ICacheEntry>());
                        fieldCache.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(p => new PropertyCacheEntry(o, p)).Cast<ICacheEntry>());
                    }
                }

                inspectorScrollPos = Vector2.zero;
            }
            catch (Exception ex)
            {
                BepInLogger.Log("Inspector crash: " + ex.ToString());
            }
        }

        private Boolean CanCovert(string value, Type type)
        {
            if (CanCovertCache.ContainsKey(type))
                return CanCovertCache[type];

            try
            {
                var obj = Convert.ChangeType(value, type);
                CanCovertCache[type] = true;
                return true;
            }
            catch
            {
                CanCovertCache[type] = false;
                return false;
            }
        }

        private void CheatWindow(int id)
        {
            try
            {
                // add club rating cheat (maybe dont change directly,
                // add points as normal bonus and have player go write a report)

                // todo add time slider?

                cheatsScrollPos = GUILayout.BeginScrollView(cheatsScrollPos);
                {
                    if (!gameMgr.saveData.isOpening)
                    {
                        DrawPlayerCheats();
                    }
                    else
                    {
                        GUILayout.Label("Start the game to see player cheats");
                    }

                    DrawSeparator();

                    if (GUILayout.Button("Open game state in inspector"))
                    {
                        InspectorClear();
                        InspectorPush(new InspectorStackEntry(gameMgr, "Manager.Game.Instance"));
                    }

                    DrawSeparator();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Current girl stats");

                        var currentAdvGirl = GetCurrentAdvHeroine();

                        if (currentAdvGirl != null)
                        {
                            DrawCurrentHeroineCheats(currentAdvGirl);
                        }
                        else
                        {
                            GUILayout.Label("Talk to a girl to access her stats");
                        }
                    }
                    GUILayout.EndVertical();

                    DrawSeparator();

                    if (GUILayout.Button("Open heroine list in inspector"))
                    {
                        InspectorClear();
                        InspectorPush(new InspectorStackEntry(gameMgr.HeroineList, "Heroine list"));
                    }

                    DrawSeparator();
                    GUILayout.Label("Created by MarC0 @ HongFire");
                }
                GUILayout.EndScrollView();

                //TODO guibutton to enable/disable full editor
            }
            catch (Exception ex)
            {
                BepInLogger.Log("CheatWindow crash: " + ex.Message);
            }

            GUI.DragWindow();
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

                if (GUILayout.Button("Set all H experience to 99%"))
                {
                    currentAdvGirl.houshiExp = 99f;
                    for (int i = 0; i < currentAdvGirl.hAreaExps.Length; i++)
                        currentAdvGirl.hAreaExps[i] = 99f;
                }

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

        private void DrawEditableValue(ICacheEntry field, object value)
        {
            bool isBeingEdited = currentlyEditingTag == field;
            string text = isBeingEdited ? currentlyEditingText : ExtractText(value);
            var result = GUILayout.TextField(text, GUILayout.ExpandWidth(true));

            if (!Equals(text, result) || isBeingEdited)
            {
                if (userHasHitReturn)
                {
                    currentlyEditingTag = null;
                    userHasHitReturn = false;
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
                    currentlyEditingText = result;
                    currentlyEditingTag = field;
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
                    GUILayout.Label("STR: " + gameMgr.Player.physical, GUILayout.Width(60));
                    gameMgr.Player.physical = (int)GUILayout.HorizontalSlider(gameMgr.Player.physical, 0, 100);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("INT: " + gameMgr.Player.intellect, GUILayout.Width(60));
                        gameMgr.Player.intellect = (int)GUILayout.HorizontalSlider(gameMgr.Player.intellect, 0, 100);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("H: " + gameMgr.Player.hentai, GUILayout.Width(60));
                        gameMgr.Player.hentai = (int)GUILayout.HorizontalSlider(gameMgr.Player.hentai, 0, 100);
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
                                if(Math.Abs(newVal - cycle.timer) > 0.09)
                                {
                                    typeof(ActionGame.Cycle)
                                        .GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)
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
                    gameMgr.saveData.clubReport.comAdd += 10000;
                }

                if (GUILayout.Button("Open player data in inspector"))
                {
                    InspectorClear();
                    InspectorPush(new InspectorStackEntry(gameMgr.saveData.player, "Player data"));
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
            GUILayout.TextArea(field.Name(), GUI.skin.label, GUILayout.Width(inspectorNameWidth), GUILayout.MaxWidth(inspectorNameWidth));
        }

        private void DrawVariableNameEnterButton(ICacheEntry field, object value)
        {
            if (GUILayout.Button(field.Name(), GUILayout.Width(inspectorNameWidth), GUILayout.MaxWidth(inspectorNameWidth)))
            {
                if (value != null)
                {
                    nextToPush = new InspectorStackEntry(value, field.Name());
                }
            }
        }

        private SaveData.Heroine GetCurrentAdvHeroine()
        {
            try
            {
                MonoBehaviour nowScene = gameMgr.actScene?.AdvScene?.nowScene;
                SaveData.Heroine currentAdvGirl = nowScene?.GetType().GetField("m_TargetHeroine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(nowScene) as SaveData.Heroine;
                return currentAdvGirl;
            }
            catch { return null; }
        }

        private string GetHExpText(SaveData.Heroine currentAdvGirl)
        {
            return hExpNames[(int)currentAdvGirl.HExperience];
        }

        private void InspectorClear()
        {
            inspectorStack.Clear();
            CacheFields(null);
        }

        private void InspectorPop()
        {
            inspectorStack.Pop();
            CacheFields(inspectorStack.Peek().Instance);
        }

        private void InspectorPush(InspectorStackEntry o)
        {
            inspectorStack.Push(o);
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
                    foreach (var item in inspectorStack.Reverse().ToArray())
                    {
                        if (GUILayout.Button(item.Name, GUILayout.ExpandWidth(false)))
                        {
                            while (inspectorStack.Peek() != item)
                                InspectorPop();

                            return;
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Value type", GUI.skin.box, GUILayout.Width(inspectorTypeWidth));
                            GUILayout.Label("Variable name", GUI.skin.box, GUILayout.Width(inspectorNameWidth));
                            GUILayout.Label("Value", GUI.skin.box, GUILayout.ExpandWidth(true));
                        }
                        GUILayout.EndHorizontal();

                        inspectorScrollPos = GUILayout.BeginScrollView(inspectorScrollPos);
                        {
                            GUILayout.BeginVertical();

                            foreach (var field in fieldCache)
                            {
                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Label(field.TypeName(), GUILayout.Width(inspectorTypeWidth), GUILayout.MaxWidth(inspectorTypeWidth));

                                    object value = field.Get();

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

        private void OnGUI()
        {
            if (!showGui) return;

            Event e = Event.current;
            if (e.keyCode == KeyCode.Return) userHasHitReturn = true;

            windowRect = GUILayout.Window(591, windowRect, CheatWindow, mainWindowTitle);

            if (inspectorStack.Count != 0)
                windowRect2 = GUILayout.Window(592, windowRect2, InspectorWindow, "Inspector");
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            screenRect = new Rect(screenOffset, screenOffset, w - screenOffset * 2, h - screenOffset * 2);

            if (windowRect.IsDefault())
                windowRect = new Rect(screenRect.width - 50 - 270, 100, 270, 380);

            if (windowRect2.IsDefault())
                windowRect2 = new Rect(screenRect.width / 2 - 800 / 2, screenRect.height / 2 - 600 / 2, 800, 600);
        }

        private void Start()
        {
            gameMgr = Manager.Game.Instance;

            mainWindowTitle = "Cheat Tools" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void Update()
        {
            if (nextToPush != null)
            {
                InspectorPush(nextToPush);

                nextToPush = null;
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                showGui = !showGui;
                SetWindowSizes();
            }

            if (!showGui) return;
        }
    }
}