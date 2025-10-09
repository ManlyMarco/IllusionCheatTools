using System;
using System.Collections.Generic;
using System.Linq;
using AC.Scene;
using AC.Scene.Explore;
using AC.Scene.Explore.Communication;
using AC.User;
using BepInEx.Logging;
using Character;
using H;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ILLGAMES.ADV;
using IllusionMods;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static int _otherCharaListIndex;
        private static ImguiComboBox _otherCharaDropdown = new();
        private static KeyValuePair<object, string>[] _openInInspectorButtons;
        private static Actor _currentVisibleChara, _currentVisibleCharaMain;
        // Only true when dialog box is open
        private static bool ADVOpen => ADVCore._instance && ADVCore._instance.isActiveAndEnabled;

        // TODO faster way to get this?
        private static ExploreScene ExploreSceneInstance => _exploreSceneInstance ? _exploreSceneInstance : _exploreSceneInstance = GameObject.FindObjectOfType<ExploreScene>();
        private static ExploreScene _exploreSceneInstance;

        private static bool InsideCommunication
        {
            get
            {
                var exploreScene = ExploreSceneInstance;
                return exploreScene != null && exploreScene.CommunicationUI != null && exploreScene.CommunicationUI.isActiveAndEnabled && exploreScene.CommunicationUI._targets.Count > 0 && !InsideH;
            }
        }

        private static SaveData CurrentSaveData => Manager.Game.Instance?.SaveData;

        private static bool InsideH => HScene.IsActive();

        public static void Initialize(CheatToolsPlugin instance)
        {
            CheatToolsWindow.OnShown += window =>
            {
                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>(HScene._instance ? HScene.Instance : typeof(HScene), "H.HScene"),
                    new KeyValuePair<object, string>(ExploreSceneInstance, "AC.Scene.ExploreScene"),
                    new KeyValuePair<object, string>(CurrentSaveData, "SaveData"),
                    new KeyValuePair<object, string>(typeof(Manager.Config), "Manager.Config"),
                    new KeyValuePair<object, string>(Manager.Game._instance ? Manager.Game.Instance : typeof(Manager.Game), "Manager.Game"),
                    new KeyValuePair<object, string>((object)Manager.Scene._instance ?? typeof(Manager.Scene), "Manager.Scene"),
                    new KeyValuePair<object, string>((object)Manager.Sound._instance ?? typeof(Manager.Sound), "Manager.Sound"),
                    new KeyValuePair<object, string>(typeof(Manager.GameSystem), "Manager.GameSystem"),
                };

                window.ComboBoxesToDisplay.Add(_otherCharaDropdown);
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => InsideH, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => InsideCommunication, DrawAdvCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => ExploreSceneInstance, DrawExploreCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => CurrentSaveData != null, DrawSavedataCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => CurrentSaveData?.NPCDataList?.Sum(x => x.Count) > 0, DrawGirlCheatMenu, "Unable to edit character stats on this screen or there are no characters. Load a saved game or start a new game and add characters to the roster."));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            Harmony.CreateAndPatchAll(typeof(Hooks));
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            var hScene = H.HScene._instance;

            GUILayout.Label("H scene controls");

            var hflag = hScene.CtrlFlag;
            DrawUtils.DrawSlider("Guage rise rate", 0.001f, 0.5f, () => hflag.SpeedGuageRate, f => hflag.SpeedGuageRate = f, "Speed at which the pleasure guages get increased");
            DrawUtils.DrawSlider("Guage fall rate", 0.001f, 0.5f, () => hflag.GuageDecreaseRate, f => hflag.GuageDecreaseRate = f, "Speed at which the pleasure guages decrease (only happens in rare cases)");

            var hGauge = hScene.Sprite.GaugeUI;
            DrawUtils.DrawSlider("Male guage", 0f, 1f, () => hGauge._gaugeM.Value, f => hGauge._gaugeM.Value = f);
            DrawUtils.DrawSlider("Female guage", 0f, 1f, () => hGauge._gaugeF.Value, f => hGauge._gaugeF.Value = f);

            if (GUILayout.Button("Open HScene in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(hScene, "H.HScene"), true);
        }

        private static void DrawAdvCheats(CheatToolsWindow cheatToolsWindow)
        {
            var commUi = ExploreSceneInstance.CommunicationUI;

            GUILayout.Label("ADV scene controls");

            // TODO

            DrawUtils.DrawBool("Show letterbox", () => commUi._objLetterBox.activeSelf, b => commUi._objLetterBox.SetActive(b));
        }


        private static void DrawExploreCheats(CheatToolsWindow cheatToolsWindow)
        {
            var expScene = ExploreSceneInstance;
            var cycle = expScene.SaveData.Cycle;

            Hooks.RiggedRng = GUILayout.Toggle(Hooks.RiggedRng, new GUIContent("Rigged RNG (success if above 0%)", null, "All actions with at least 1% chance will always succeed. Must be activated BEFORE talking to a character.\nWARNING: This will affect RNG across the game. NPCs will (probably) always succeed with their actions which will skew the simulation heavily. Some events might never happen or keep repeating until this is turned off."));

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            {
                DrawUtils.DrawSlider("Elapsed time", 0, expScene._propertyData.Explore.LengthTimeZone, () => cycle.ElapsedTime, f => cycle.ElapsedTime = f);

                if (GUILayout.Button("Unlimited time limit (until game restart)"))
                {
                    expScene._propertyData.Explore._lengthTimeZone = 100000;
                    cycle.ElapsedTime = 0;
                }

                GUILayout.Label("Walking speed:");

                var normal = Hooks.SpeedMode == Hooks.SpeedModes.Normal || Hooks.SpeedMode == Hooks.SpeedModes.ReturnToNormal;
                var newNormal = GUILayout.Toggle(normal, "Normal");
                if (!normal && newNormal)
                    Hooks.SpeedMode = Hooks.SpeedModes.ReturnToNormal;
                if (GUILayout.Toggle(Hooks.SpeedMode == Hooks.SpeedModes.Fast, "Fast"))
                    Hooks.SpeedMode = Hooks.SpeedModes.Fast;
                if (GUILayout.Toggle(Hooks.SpeedMode == Hooks.SpeedModes.Sanic, "Sanic"))
                    Hooks.SpeedMode = Hooks.SpeedModes.Sanic;
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawSavedataCheats(CheatToolsWindow cheatToolsWindow)
        {
            var savedata = CurrentSaveData;
            GUILayout.BeginVertical(GUI.skin.box);
            {
                var cycle = savedata.Cycle;

                DrawUtils.DrawNums("Day of week (Sunday is 1)", 7, () => (byte)cycle.DayOfWeek, b => cycle.DayOfWeek = b);
                DrawUtils.DrawInt("Day count", () => cycle.ElapsedDay, i => cycle.ElapsedDay = i, "Total number of days passed in-game.");
                DrawUtils.DrawInt("Week count", () => cycle.ElapsedWeek, i => cycle.ElapsedWeek = i, "Total number of weeks passed in-game. Used to calculate when festivals happen.");

                var isFestivalWeek = cycle.IsFestivalWeek();
                GUILayout.Label($"IsFestivalWeek={isFestivalWeek}  IsShoppingWeek={cycle.IsShoppingWeek()}");

                var prevEnabled = GUI.enabled;
                if (isFestivalWeek && cycle.DayOfWeek == 6)
                    GUI.enabled = false;

                if (GUILayout.Button("Skip to next festival"))
                {
                    while (!cycle.IsFestivalWeek())
                        cycle.ElapsedWeek++;

                    cycle.DayOfWeek = 6;

                    if (ExploreSceneInstance?.isActiveAndEnabled == true)
                        ExploreSceneInstance.ChangeNextCycle(new Il2CppSystem.Nullable<TimeZones>(TimeZones.Return));
                }

                GUI.enabled = prevEnabled;
            }
            GUILayout.EndVertical();

            var playerData = savedata.PlayerData;
            if (playerData != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                for (var i = 0; i < playerData.Tastes.Length; i++)
                {
                    var thisIndex = i;
                    DrawUtils.DrawSlider("Taste " + (i + 1), 0, 20, () => (int)playerData.Tastes[thisIndex], (int val) =>
                    {
                        playerData.Tastes[thisIndex] = (byte)val;
                        if (InsideCommunication)
                            ExploreSceneInstance.CommunicationUI.RefreshTasteGraph();
                    });
                }
                GUILayout.EndVertical();
            }
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            var npcList = CurrentSaveData.NPCDataList.SelectMany(x => x).Where(x => x != null).ToList();

            GUILayout.Label("Character status editor");

            // TODO

            //foreach (var chara in GameUtilities.GetCurrentActors(false))
            //{
            //    var main = chara.Value.FindMainActorInstance();
            //    var isCopy = !ReferenceEquals(main.Value, chara.Value);
            //    if (GUILayout.Button($"Select #{chara.Key} - {chara.Value.GetCharaName(true)}{(isCopy ? " (Copy)" : "")}"))
            //    {
            //        _currentVisibleChara = chara.Value;
            //        _currentVisibleCharaMain = isCopy ? main.Value : null;
            //    }
            //}

            //GUILayout.Space(6);

            //try
            //{
            //    if (_currentVisibleChara != null)
            //        DrawSingleCharaCheats(_currentVisibleChara, _currentVisibleCharaMain, cheatToolsWindow);
            //    else
            //        GUILayout.Label("Select a character to edit their stats");
            //}
            //catch (Exception e)
            //{
            //    CheatToolsPlugin.Logger.LogError(e);
            //    _currentVisibleChara = null;
            //}
        }

        /*private static void DrawSingleCharaCheats(Actor currentAdvChara, Actor mainChara, CheatToolsWindow cheatToolsWindow)
        {
            var comboboxMaxY = (int)cheatToolsWindow.WindowRect.bottom - 30;
            var isCopy = mainChara != null;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Selected:", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(currentAdvChara.GetCharaName(true), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", IMGUIUtils.LayoutOptionsExpandWidthFalse)) _currentVisibleChara = null;
                }
                GUILayout.EndHorizontal();

                if (isCopy)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(new GUIContent("!! This character is a copy !!", null, "All changes made to this characters will be lost after the current scene finishes.\n\n" +
                                                                                               "If you want to make permanent changes, open the main instance of this character and do your changes there.\n" +
                                                                                               "You will have to exit and re-enter current scene to propagate the changes to the copied character)."), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Open main"))
                        {
                            _currentVisibleChara = mainChara;
                            _currentVisibleCharaMain = null;
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(6);

                var charasGameParam = currentAdvChara.charasGameParam;
                if (charasGameParam != null)
                {
                    var baseParameter = currentAdvChara.charasGameParam.baseParameter;

                    {
                        GUILayout.Label("In-game stats (changed through gameplay)");

                        DrawUtils.DrawSlider(nameof(baseParameter.Stamina), 0, 1000, () => baseParameter.Stamina, val => baseParameter.Stamina = val);
                        DrawUtils.DrawSlider(nameof(baseParameter.NowStamina), 0, baseParameter.Stamina + 100, () => baseParameter.NowStamina, val => baseParameter.NowStamina = val,
                                             "When character is controlled by player this field is used for determining how long until the period ends. NPCs don't use it.\nInitial value is equal to 'Stamina + 100'.");
                        DrawUtils.DrawSlider(nameof(baseParameter.Conversation), 0, 1000, () => baseParameter.Conversation, val => baseParameter.Conversation = val);
                        DrawUtils.DrawSlider(nameof(baseParameter.Study), 0, 1000, () => baseParameter.Study, val => baseParameter.Study = val);
                        DrawUtils.DrawSlider(nameof(baseParameter.Living), 0, 1000, () => baseParameter.Living, val => baseParameter.Living = val);
                        DrawUtils.DrawSlider(nameof(baseParameter.Job), 0, 1000, () => baseParameter.Job, val => baseParameter.Job = val, "Doesn't seem to work, changes get overwritten.");

                        GUILayout.Space(6);
                    }

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        var menstruationsLength = charasGameParam.menstruations.Length;
                        var currentDayIndex = Manager.Game.saveData.Day % menstruationsLength;

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Menstruation: ");

                            GUI.color = currentAdvChara.IsMenstruation(ActorExtensionH.Menstruation.Normal) ? Color.green : Color.white;
                            if (GUILayout.Button("Normal")) SetMenstruationForDay(currentDayIndex, 0);
                            GUI.color = currentAdvChara.IsMenstruation(ActorExtensionH.Menstruation.Safe) ? Color.green : Color.white;
                            if (GUILayout.Button("Safe")) SetMenstruationForDay(currentDayIndex, 1);
                            GUI.color = currentAdvChara.IsMenstruation(ActorExtensionH.Menstruation.Danger) ? Color.green : Color.white;
                            if (GUILayout.Button("Danger")) SetMenstruationForDay(currentDayIndex, 2);
                            GUI.color = Color.white;
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label(menstruationsLength / 7 + "-weekly", GUILayout.Width(80));
                            var mensUiItems = new GUIContent[] { new("N"), new("S"), new("D") };
                            for (var i = 0; i < menstruationsLength; i++)
                            {
                                var mens = charasGameParam.menstruations[i];
                                GUI.color = currentDayIndex == i ? Color.green : Color.white;
                                if (GUILayout.Button(mensUiItems[mens]))
                                    SetMenstruationForDay(i, (mens + 1) % 3);

                                if (i == 6)
                                {
                                    GUI.color = Color.white;
                                    GUILayout.EndHorizontal();
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label("schedule:", GUILayout.Width(80));
                                }
                            }
                            GUI.color = Color.white;
                        }
                        GUILayout.EndHorizontal();

                        void SetMenstruationForDay(int index, int newMens) => charasGameParam.menstruations[index] = newMens;
                    }
                    GUILayout.EndVertical();

                    GUILayout.Space(6);
                }

                var gameParam = currentAdvChara.id.GameParameter;
                if (gameParam != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Card stats (same as in the chara maker)");

                        DrawUtils.DrawStrings("Job", new[] { "None", "Lifeguard", "Cafe", "Shrine" }, () => gameParam.job, b => gameParam.job = b);
                        DrawUtils.DrawNums("Gayness", 5, () => gameParam.sexualTarget, b => gameParam.sexualTarget = b);
                        DrawUtils.DrawNums(nameof(gameParam.lvChastity), 5, () => gameParam.lvChastity, b => gameParam.lvChastity = b);
                        DrawUtils.DrawNums(nameof(gameParam.lvSociability), 5, () => gameParam.lvSociability, b => gameParam.lvSociability = b);
                        DrawUtils.DrawNums(nameof(gameParam.lvTalk), 5, () => gameParam.lvTalk, b => gameParam.lvTalk = b);
                        DrawUtils.DrawNums(nameof(gameParam.lvStudy), 5, () => gameParam.lvStudy, b => gameParam.lvStudy = b);
                        DrawUtils.DrawNums(nameof(gameParam.lvLiving), 5, () => gameParam.lvLiving, b => gameParam.lvLiving = b);
                        DrawUtils.DrawNums(nameof(gameParam.lvPhysical), 5, () => gameParam.lvPhysical, b => gameParam.lvPhysical = b);
                        DrawUtils.DrawNums("Fighting style", 3, () => gameParam.lvDefeat, b => gameParam.lvDefeat = b);

                        DrawUtils.DrawBool(nameof(gameParam.isVirgin), () => gameParam.isVirgin, b => gameParam.isVirgin = b);
                        DrawUtils.DrawBool(nameof(gameParam.isAnalVirgin), () => gameParam.isAnalVirgin, b => gameParam.isAnalVirgin = b);
                        DrawUtils.DrawBool(nameof(gameParam.isMaleVirgin), () => gameParam.isMaleVirgin, b => gameParam.isMaleVirgin = b);
                        DrawUtils.DrawBool(nameof(gameParam.isMaleAnalVirgin), () => gameParam.isMaleAnalVirgin, b => gameParam.isMaleAnalVirgin = b);
                    }
                    GUILayout.EndVertical();
                }

                if (gameParam != null && GUILayout.Button("Inspect GameParameter"))
                    Inspector.Instance.Push(new InstanceStackEntry(gameParam, "GameParam " + currentAdvChara.GetCharaName(true)), true);

                if (charasGameParam != null && GUILayout.Button("Inspect CharactersGameParameter"))
                    Inspector.Instance.Push(new InstanceStackEntry(charasGameParam, "CharaGameParam " + currentAdvChara.GetCharaName(true)), true);

                if (GUILayout.Button("Navigate to Character's GameObject"))
                {
                    if (currentAdvChara.transform)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvChara.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Character has no body assigned");
                }

                if (GUILayout.Button("Open Character in inspector"))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvChara, "Actor " + currentAdvChara.GetCharaName(true)), true);

                //if (GUILayout.Button("Inspect extended data"))
                //{
                //    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvChara.chaFile), "ExtData for " + currentAdvChara.Name), true);
                //}
            }
            GUILayout.EndVertical();
        }*/

    }
}
