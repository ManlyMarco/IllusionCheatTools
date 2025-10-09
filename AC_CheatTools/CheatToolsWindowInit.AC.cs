using System.Collections.Generic;
using System.Linq;
using AC.Scene;
using AC.User;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using H;
using HarmonyLib;
using Il2CppSystem.Threading;
using ILLGAMES.ADV;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Array = System.Array;
using Exception = System.Exception;
using Math = System.Math;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static KeyValuePair<object, string>[] _openInInspectorButtons;
        private static NPCData _currentVisibleChara;

        // Only true when dialog box is open
        private static bool ADVOpen => ADVCore._instance && ADVCore._instance.isActiveAndEnabled;
        private static SaveData CurrentSaveData => Manager.Game.Instance?.SaveData;
        private static bool InsideH => HScene.IsActive();
        // TODO faster way to get this?
        private static ExploreScene ExploreSceneInstance => _exploreSceneInstance ? _exploreSceneInstance : _exploreSceneInstance = UnityEngine.Object.FindObjectOfType<ExploreScene>();
        private static ExploreScene _exploreSceneInstance;

        private static bool InsideCommunication
        {
            get
            {
                var exploreScene = ExploreSceneInstance;
                return exploreScene != null && exploreScene.CommunicationUI != null && exploreScene.CommunicationUI.isActiveAndEnabled && exploreScene.CommunicationUI._targets.Count > 0 && !InsideH;
            }
        }

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

            DrawUtils.DrawSlider("Elapsed time", 0, expScene._propertyData.Explore.LengthTimeZone, () => cycle.ElapsedTime, f => cycle.ElapsedTime = f);

            if (GUILayout.Button("Unlimited time limit (until game restart)"))
            {
                expScene._propertyData.Explore._lengthTimeZone = 100000;
                cycle.ElapsedTime = 0;
            }

            GUILayout.BeginHorizontal();
            {
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

            if (!InsideCommunication && !InsideH)
            {
                GUI.color = Color.red;
                if (GUILayout.Button(new GUIContent("I. AM. MAGNETIC.", null, "Warning: Likely to softlock the game, save first! You may be able to un-softlock by skipping the current period.")))
                {
                    ExploreSceneInstance.Player._state.Release();

                    foreach (var npc in ExploreSceneInstance.NPCList)
                    {
                        // BUG: Can softlock in first person mode with no controls enabled other than wasd and right click
                        ExploreSceneInstance.CallNPC(npc)
                                            .ContinueWith((Il2CppSystem.Action)(() =>
                                            {
                                                // TODO find a way to ensure player is not softlocked, this doesn't really help
                                                ExploreSceneInstance._cycleUI.Visible = true;
                                            }));
                    }
                }
                GUI.color = Color.white;
            }
        }

        private static void DrawSavedataCheats(CheatToolsWindow cheatToolsWindow)
        {
            var savedata = CurrentSaveData;
            GUILayout.BeginVertical(GUI.skin.box);
            {
                var cycle = savedata.Cycle;

                DrawUtils.DrawNums("Day of week", 7, () => (byte)cycle.DayOfWeek, b => cycle.DayOfWeek = b, "Sunday is day 1");
                DrawUtils.DrawInt("Day count", () => cycle.ElapsedDay, i => cycle.ElapsedDay = i, "Total number of days passed in-game.");
                DrawUtils.DrawInt("Week count", () => cycle.ElapsedWeek, i => cycle.ElapsedWeek = i, "Total number of weeks passed in-game. Used to calculate when festivals happen.");

                var isFestivalWeek = cycle.IsFestivalWeek();
                GUILayout.Label($"IsFestivalWeek={isFestivalWeek}  IsShoppingWeek={cycle.IsShoppingWeek()}");

                // Skip to Sunday buttons
                {
                    var prevEnabled = GUI.enabled;
                    if (cycle.DayOfWeek is 6 or 0)
                        GUI.enabled = false;

                    void JumpToSunday(bool festival)
                    {
                        if (ExploreSceneInstance?.isActiveAndEnabled == true)
                        {
                            savedata.ChangeDay(DaysOfWeek.Saturday);
                            if (festival) while (!cycle.IsFestivalWeek()) cycle.ElapsedWeek++;
                            ExploreSceneInstance.ChangeNextCycle(new Il2CppSystem.Nullable<TimeZones>(TimeZones.Return), true, CancellationToken.None);
                        }
                        else
                        {
                            savedata.ChangeDay(DaysOfWeek.Sunday);
                            if (festival) while (!cycle.IsFestivalWeek()) cycle.ElapsedWeek++;
                        }
                    }

                    if (GUILayout.Button("Skip to Sunday")) JumpToSunday(false);

                    GUI.enabled = prevEnabled;
                    if (isFestivalWeek && cycle.DayOfWeek is 6 or 0)
                        GUI.enabled = false;

                    if (GUILayout.Button("Skip to next festival")) JumpToSunday(true);

                    GUI.enabled = prevEnabled;
                }
            }
            GUILayout.EndVertical();

            var playerData = savedata.PlayerData;
            if (playerData != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);

                // Do not use .Tastes because it always returns 80 for some reason
                for (var i = 0; i < playerData._tastes.Length; i++)
                {
                    var thisIndex = i;
                    DrawUtils.DrawSlider("Taste " + (i + 1), 0, 20, () => playerData._tastes[thisIndex], val =>
                    {
                        playerData._tastes[thisIndex] = (byte)val;
                        if (InsideCommunication)
                            ExploreSceneInstance.CommunicationUI.RefreshTasteGraph();
                    });
                }

                if (GUILayout.Button("Inspect SaveData.PlayerData"))
                    Inspector.Instance.Push(new InstanceStackEntry(playerData, "PlayerData"), true);

                GUILayout.EndVertical();
            }
        }

        private static NPCData[] GetCurrentActors()
        {
            if (CurrentSaveData?.NPCDataList == null) return Array.Empty<NPCData>();

            // todo faster?
            var allNpcs = CurrentSaveData.NPCDataList.SelectMany(x => x).Where(x => x?.NPCInstance?.BaseData != null).ToDictionary(x => x.NPCInstance.BaseData, x => x);
            if (InsideH)
            {
                return HScene.Instance._hActorAll.Where(x => x?.ActorData != null).Select(x =>
                {
                    allNpcs.TryGetValue(x.ActorData, out var npcd);
                    return npcd;
                }).Where(x => x != null).ToArray();
            }

            if (InsideCommunication)
            {
                return ExploreSceneInstance.CommunicationUI._targets.AsManagedEnumerable().Where(x => x?.BaseData != null).Select(x =>
                {
                    allNpcs.TryGetValue(x.BaseData, out var npcd);
                    return npcd;
                }).Where(x => x != null).ToArray();
            }

            //return allNpcs.Values.ToArray();
            return Array.Empty<NPCData>();
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            var npcList = CurrentSaveData.NPCDataList.SelectMany(x => x).Where(x => x != null).ToList();

            GUILayout.Label("Character status editor");

            // TODO

            foreach (var chara in GetCurrentActors())
            {
                if (GUILayout.Button($"Select {chara.HumanData.GetCharaName(true) ?? chara.CharaFileName}"))
                {
                    _currentVisibleChara = chara;
                }
            }

            GUILayout.Space(6);

            try
            {
                if (_currentVisibleChara != null)
                    DrawSingleCharaCheats(_currentVisibleChara, cheatToolsWindow);
                else
                    GUILayout.Label("Select a character to edit their stats");
            }
            catch (Exception e)
            {
                CheatToolsPlugin.Logger.LogError(e);
                _currentVisibleChara = null;
            }
        }

        private static void DrawSingleCharaCheats(NPCData currentChara, CheatToolsWindow cheatToolsWindow)
        {
            var charaName = currentChara.HumanData.GetCharaName(true);

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Selected:", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(charaName, IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", IMGUIUtils.LayoutOptionsExpandWidthFalse)) _currentVisibleChara = null;
                }
                GUILayout.EndHorizontal();

                void UpdateUiIfNeeded(bool isLoveTalk = false)
                {
                    if (InsideCommunication)
                        ExploreSceneInstance.CommunicationUI.UpdateParameter(isLoveTalk);
                }

                DrawUtils.DrawNums("Relation lv", 4, () => currentChara.RelationValue, b =>
                {
                    currentChara.RelationValue = b;
                    currentChara.FavorValue = Math.Max(currentChara.FavorValue, currentChara.RelationValue * 100);
                    UpdateUiIfNeeded(true);
                }, "1 - unknown, 2 - friend, 3 - bestie, 4 - lover.\nWarning: May not update immediately, save/load the game in case of issues.");

                DrawUtils.DrawSlider(nameof(currentChara.Favor), 0, 100, () => currentChara.FavorValue - currentChara.RelationValue * 100, i =>
                {
                    currentChara.FavorValue = i + currentChara.RelationValue * 100;
                    UpdateUiIfNeeded();
                });

                GUILayout.Space(5);

                GUILayout.Label($"MoodLevel={currentChara.MoodLevel}  IntimacyRank={currentChara.IntimacyRank}  LewdnessState={currentChara.LewdnessState}");
                DrawUtils.DrawSlider(nameof(currentChara.Mood), 0, 100, () => currentChara.Mood, i =>
                {
                    currentChara.Mood = i;
                    UpdateUiIfNeeded();
                });
                DrawUtils.DrawSlider(nameof(currentChara.Intimacy), 0, 100, () => currentChara.Intimacy, i =>
                {
                    currentChara.Intimacy = i;
                    UpdateUiIfNeeded();
                });
                DrawUtils.DrawSlider(nameof(currentChara.Lewdness), 0, 100, () => currentChara.LewdnessValue, i =>
                {
                    currentChara.LewdnessValue = i;
                    UpdateUiIfNeeded();
                });

                GUILayout.Space(5);

                DrawUtils.DrawSlider(nameof(currentChara.Sexperience), 0, 100, () => currentChara.Sexperience, i =>
                {
                    currentChara.Sexperience = i;
                    UpdateUiIfNeeded();
                }, "Experience bar seen at the end of H scenes.");
                DrawUtils.DrawBool(nameof(currentChara.IsVirgin), () => currentChara.IsVirgin, b => currentChara.IsVirgin = b);
                DrawUtils.DrawBool(nameof(currentChara.IsAnalVirgin), () => currentChara.IsAnalVirgin, b => currentChara.IsAnalVirgin = b);
                DrawUtils.DrawInt(nameof(currentChara.HCount), () => currentChara.HCountValue, b =>
                {
                    var change = b - currentChara.HCountValue;
                    if (change > 0)
                    {
                        for (; change > 0; change--)
                            currentChara.AddHCount();
                    }
                    else
                    {
                        currentChara.HCountValue = b;
                    }
                });

                GUILayout.Space(5);

                if (!InsideCommunication && !InsideH)
                {
                    GUI.color = Color.red;
                    if (GUILayout.Button(new GUIContent("Call", null, "Warning: May softlock the game in some cases, save before using!")))
                        ExploreSceneInstance.CallNPC(currentChara.NPCInstance);
                    GUI.color = Color.white;
                }
#if DEBUG
                if (GUILayout.Button("DEBUG: try update UI"))
                {
                    ExploreSceneInstance.CommunicationUI.UpdateParameter(true);
                    ExploreSceneInstance.CommunicationUI.UpdateCameraAngle();
                    ExploreSceneInstance.CommunicationUI.UpdateTasteGraph();
                    ExploreSceneInstance.CommunicationUI.RefreshTasteGraph();
                }
#endif
                GUILayout.Space(5);

                if (GUILayout.Button("Open Character in inspector"))
                    Inspector.Instance.Push(new InstanceStackEntry(currentChara, "NPCData " + charaName), true);

                if (GUILayout.Button("Navigate to Character's GameObject"))
                {
                    if (currentChara.NPCInstance?.Transform)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentChara.NPCInstance.Transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Character has no body assigned");
                }
            }
            GUILayout.EndVertical();
        }
    }
}
