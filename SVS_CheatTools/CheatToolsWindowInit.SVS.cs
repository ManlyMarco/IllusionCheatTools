using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Pathfinding;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using SaveData;
using SV;
using UnityEngine;
using UnityEngine.UI;
using Random = Il2CppSystem.Random;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static ImguiComboBoxSimple _belongingsDropdown;
        private static ImguiComboBoxSimple _individualityDropdown;
        private static KeyValuePair<object, string>[] _openInInspectorButtons;
        private static Actor _currentVisibleChara;

        public static void Initialize(CheatToolsPlugin instance)
        {
            CheatToolsWindow.OnShown += _ =>
            {
                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>((object)SV.H.HScene._instance ?? SV.H.HScene._instance, "SV.H.HScene"),
                    new KeyValuePair<object, string>(ADV.ADVManager._instance, "ADV.ADVManager"),
                    new KeyValuePair<object, string>((object)Manager.Game._instance ?? typeof(Manager.Game), "Manager.Game"),
                    new KeyValuePair<object, string>(Manager.Game.saveData, "Manager.Game.saveData"),
                    new KeyValuePair<object, string>(typeof(Manager.Config), "Manager.Config"),
                    new KeyValuePair<object, string>((object)Manager.Scene._instance ?? typeof(Manager.Scene), "Manager.Scene"),
                    new KeyValuePair<object, string>((object)Manager.Sound._instance ?? typeof(Manager.Sound), "Manager.Sound"),
                    new KeyValuePair<object, string>(typeof(Manager.GameSystem), "Manager.GameSystem"),
                    new KeyValuePair<object, string>((object)Manager.MapManager._instance ?? typeof(Manager.MapManager), "Manager.MapManager"),
                    new KeyValuePair<object, string>((object)Manager.SimulationManager._instance ?? typeof(Manager.SimulationManager), "Manager.SimulationManager"),
                    new KeyValuePair<object, string>((object)Manager.TalkManager._instance ?? typeof(Manager.TalkManager), "Manager.TalkManager"),
                };

                if (_belongingsDropdown == null)
                {
                    _belongingsDropdown = new ImguiComboBoxSimple(Manager.Game.BelongingsInfoTable.AsManagedEnumerable().OrderBy(x => x.Key).Select(x => new GUIContent(x.Value)).ToArray());
                    for (var i = 0; i < _belongingsDropdown.Contents.Length; i++)
                    {
                        var iCopy = i;
                        TranslationHelper.TranslateAsync(_belongingsDropdown.Contents[iCopy].text, s => _belongingsDropdown.Contents[iCopy].text = s);
                    }
                }
                if (_individualityDropdown == null)
                {
                    var guiContents = Manager.Game.IndividualityInfoTable.AsManagedEnumerable().ToDictionary(x => x.Value.ID, x => new GUIContent(x.Value.Name, null, x.Value.Information)).OrderBy(x => x.Key).ToList();
                    _individualityDropdown = new ImguiComboBoxSimple(guiContents.Select(x => x.Value).ToArray());
                    _individualityDropdown.ContentsIndexes = guiContents.Select(x => x.Key).ToArray();
                    for (var i = 0; i < _individualityDropdown.Contents.Length; i++)
                    {
                        var iCopy = i;
                        TranslationHelper.TranslateAsync(_individualityDropdown.Contents[iCopy].text, s => _individualityDropdown.Contents[iCopy].text = s);
                        TranslationHelper.TranslateAsync(_individualityDropdown.Contents[iCopy].tooltip, s => _individualityDropdown.Contents[iCopy].tooltip = s);
                    }
                }
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => SV.H.HScene.Active(), DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => Manager.Game.saveData.WorldTime > 0, DrawGeneralCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => Manager.Game.Charas.Count > 0, DrawGirlCheatMenu, "Unable to edit character stats on this screen or there are no characters. Load a saved game or start a new game and add characters to the roster."));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            Harmony.CreateAndPatchAll(typeof(Hooks));

            //todo cleaner way to update dropdowns
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => true, _ =>
            {
                _belongingsDropdown?.DrawDropdownIfOpen();
                _individualityDropdown?.DrawDropdownIfOpen();
            }, ""));
        }

        private static string GetCharaName(Actor chara)
        {
            var fullname = chara?.charFile?.Parameter?.fullname;
            return !string.IsNullOrEmpty(fullname) ? fullname : chara?.chaCtrl?.name ?? chara?.ToString();
        }

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            var hScene = SV.H.HScene._instance;

            GUILayout.Label("H scene controls");

            GUILayout.BeginHorizontal();
            var mainGauge = hScene.GaugeController?.MainUI;
            if (mainGauge != null)
            {
                GUILayout.Label("Main Gauge: " + mainGauge.GaugeValue.ToString("F1"), GUILayout.Width(150));
                mainGauge.GaugeValue = GUILayout.HorizontalSlider(mainGauge.GaugeValue, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var subGauge = hScene.GaugeController?.SubUI;
            if (subGauge != null)
            {
                GUILayout.Label("Sub Gauge: " + subGauge.GaugeValue.ToString("F1"), GUILayout.Width(150));
                subGauge.GaugeValue = GUILayout.HorizontalSlider(subGauge.GaugeValue, 0, 100);
            }
            GUILayout.EndHorizontal();

            //foreach (var hActor in hScene.Actors)
            //{
            //    if (hActor?.Actor == null) continue;
            //
            //    GUILayout.Label($"#{hActor.Index} - {GetCharaName(hActor.Actor)}");
            //
            // todo editing siru array doesn't cause updates
            //    for (int i = 0; i < hActor._siruLv.Length; i++)
            //    {
            //        GUILayout.BeginHorizontal();
            //        GUILayout.Label($"{(ChaFileDefine.SiruParts)i}: lv{hActor._siruLv[i]}", GUILayout.Width(150));
            //        hActor._siruLv[i] = (byte)GUILayout.HorizontalSlider(hActor._siruLv[i], 0, 6);
            //        GUILayout.EndHorizontal();
            //    }
            //}

            if (GUILayout.Button("Open HScene in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(hScene, "SV.H.HScene"), true);
        }

        private static void DrawGeneralCheats(CheatToolsWindow obj)
        {
            Hooks.RiggedRng = GUILayout.Toggle(Hooks.RiggedRng, new GUIContent("Rigged RNG (success if above 0%)", null, "All actions with at least 1% chance will always succeed.\nWARNING: This will affect RNG across the game. NPCs will (probably) always succeed with their actions which will skew the simulation heavily. Some events might never happen or keep repeating until this is turned off."));

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Walking speed");

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

            GUI.enabled = ADV.ADVManager._instance?.IsADV == true;
            if (GUILayout.Button(new GUIContent("Force Unlock visible talk options", null, "Un-gray and make clickable all currently visible buttons in the talk menu. Mostly for use with the blackmail menu. If the chance is 0% you still won't be able to succeed at the action.")))
            {
                var commandUi = UnityEngine.Object.FindObjectOfType<SV.CommandUI>();
                // For some reason buttons are found and set as interactable, but if they are in a hidden menu they revert to inactive when unhidden
                foreach (var btn in commandUi.GetComponentsInChildren<Button>(true))
                    btn.interactable = true;
            }
            GUI.enabled = true;
        }

        private static IEnumerable<KeyValuePair<int, Actor>> GetVisibleCharas()
        {
            if (SV.H.HScene.Active())
                return SV.H.HScene._instance.Actors.Select(x => new KeyValuePair<int, Actor>(x.Index, x.Actor));

            var talkManager = Manager.TalkManager._instance;
            if (talkManager != null && ADV.ADVManager._instance?.IsADV == true)
            {
                return new List<KeyValuePair<int, Actor>>
                {
                    new(0,talkManager.PlayerHi),
                    new(1,talkManager.Npc1),
                    new(2,talkManager.Npc2),
                    new(3,talkManager.Npc3),
                    new(4,talkManager.Npc4),
                };
            }

            return Manager.Game.saveData.Charas.AsManagedEnumerable();
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("Character status editor");

            foreach (var chara in GetVisibleCharas())
            {
                if (chara.Value == null) continue;
                if (GUILayout.Button($"Select #{chara.Key} - {GetCharaName(chara.Value)}"))
                    _currentVisibleChara = chara.Value;
            }

            GUILayout.Space(6);

            if (_currentVisibleChara != null)
                DrawSingleCharaCheats(_currentVisibleChara, cheatToolsWindow);
            else
                GUILayout.Label("Select a character to edit their stats");
        }

        private static void DrawSingleCharaCheats(Actor currentAdvChara, CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Selected chara name: " + GetCharaName(currentAdvChara));
                GUILayout.Space(6);

                var gameParam = currentAdvChara.charFile.GameParameter;
                if (gameParam != null)
                {
                    void DrawOne<T>(string name, Func<T> get, Action<T> set)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUI.changed = false;
                            var oldValue = get();
                            GUILayout.Label(name + ": ");
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
                    void DrawByte(string name, Func<byte> get, Action<byte> set)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUI.changed = false;
                            GUILayout.Label(name + ": ");
                            GUILayout.FlexibleSpace();
                            var oldValue = get();
                            var result = GUILayout.TextField(oldValue.ToString(), GUILayout.Width(50));
                            if (GUI.changed && byte.TryParse(result, out var newValue) && !newValue.Equals(oldValue)) set(newValue);
                        }
                        GUILayout.EndHorizontal();
                    }
                    void DrawNums(string name, byte count, Func<byte> get, Action<byte> set)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUI.changed = false;
                            GUILayout.Label(name + ": ");
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
                    void DrawBool(string name, Func<bool> get, Action<bool> set)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUI.changed = false;
                            var result = GUILayout.Toggle(get(), name);
                            if (GUI.changed) set(result);
                        }
                        GUILayout.EndHorizontal();
                    }

                    gameParam.job = (byte)GUILayout.SelectionGrid(gameParam.job, new[] { "None", "Lifeguard", "Cafe", "Shrine" }, 1);
                    DrawByte("Gayness", () => gameParam.sexualTarget, b => gameParam.sexualTarget = b);
                    DrawNums(nameof(gameParam.lvChastity), 5, () => gameParam.lvChastity, b => gameParam.lvChastity = b);
                    DrawNums(nameof(gameParam.lvSociability), 5, () => gameParam.lvSociability, b => gameParam.lvSociability = b);
                    DrawNums(nameof(gameParam.lvTalk), 5, () => gameParam.lvTalk, b => gameParam.lvTalk = b);
                    DrawNums(nameof(gameParam.lvStudy), 5, () => gameParam.lvStudy, b => gameParam.lvStudy = b);
                    DrawNums(nameof(gameParam.lvLiving), 5, () => gameParam.lvLiving, b => gameParam.lvLiving = b);
                    DrawNums(nameof(gameParam.lvPhysical), 5, () => gameParam.lvPhysical, b => gameParam.lvPhysical = b);
                    DrawNums("Fighting style", 3, () => gameParam.lvDefeat, b => gameParam.lvDefeat = b);

                    DrawBool(nameof(gameParam.isVirgin), () => gameParam.isVirgin, b => gameParam.isVirgin = b);
                    DrawBool(nameof(gameParam.isAnalVirgin), () => gameParam.isAnalVirgin, b => gameParam.isAnalVirgin = b);
                    DrawBool(nameof(gameParam.isMaleVirgin), () => gameParam.isMaleVirgin, b => gameParam.isMaleVirgin = b);
                    DrawBool(nameof(gameParam.isMaleAnalVirgin), () => gameParam.isMaleAnalVirgin, b => gameParam.isMaleAnalVirgin = b);

                    if (_belongingsDropdown != null)
                    {
                        GUILayout.Space(6);

                        GUILayout.BeginVertical(GUI.skin.box);

                        GUILayout.Label("Items owned:");
                        var targetArr = gameParam.belongings;
                        foreach (var gameParameterBelonging in targetArr)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                if (gameParameterBelonging >= 0 && gameParameterBelonging < _belongingsDropdown.Contents.Length)
                                    GUILayout.Label(_belongingsDropdown.Contents[gameParameterBelonging]);
                                else
                                    GUILayout.Label("Unknown item ID " + gameParameterBelonging);

                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("X", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                                {
                                    gameParam.belongings = new Il2CppStructArray<int>(targetArr.Where(x => x != gameParameterBelonging).ToArray());
                                }

                            }
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.BeginHorizontal();
                        {
                            _belongingsDropdown.Show((int)cheatToolsWindow.WindowRect.bottom - 30);
                            if (GUILayout.Button("GIVE", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            {
                                if (!gameParam.belongings.Contains(_belongingsDropdown.Index))
                                    gameParam.belongings = new Il2CppStructArray<int>(targetArr.AddItem(_belongingsDropdown.Index).ToArray());
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.EndVertical();
                    }

                    if (_individualityDropdown != null)
                    {
                        GUILayout.Space(6);

                        GUILayout.BeginVertical(GUI.skin.box);

                        GUILayout.Label("Traits:");
                        var targetArr = gameParam.individuality.answer;
                        foreach (var traitId in targetArr)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                var index = Array.IndexOf(_individualityDropdown.ContentsIndexes, traitId);
                                if (index >= 0)
                                {
                                    GUILayout.Label(_individualityDropdown.Contents[index]);
                                }
                                else
                                    GUILayout.Label("Unknown trait ID " + traitId);

                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("X", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                                {
                                    gameParam.individuality.Set(traitId, false);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.BeginHorizontal();
                        {
                            _individualityDropdown.Show((int)cheatToolsWindow.WindowRect.bottom - 30);
                            if (GUILayout.Button(new GUIContent("ADD", null, "If you add more than 2 traits they will work in-game, but will be removed after you save/load the game or the character."), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            {
                                var selectedTraitIndex = _individualityDropdown.ContentsIndexes[_individualityDropdown.Index];
                                if (gameParam.individuality.answer.Contains(-1))
                                    gameParam.individuality.Set(selectedTraitIndex, true);
                                else if (!gameParam.individuality.answer.Contains(selectedTraitIndex))
                                    gameParam.individuality.answer = gameParam.individuality.answer.AddItem(selectedTraitIndex).ToArray();
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.EndVertical();
                    }

                    //todo currentAdvChara.charFile.GameParameter.preferenceH

                    if (GUILayout.Button("Inspect GameParameter"))
                        Inspector.Instance.Push(new InstanceStackEntry(gameParam, "GameParameter " + GetCharaName(currentAdvChara)), true);
                }


                var charasGameParam = currentAdvChara.charasGameParam;
                if (charasGameParam != null)
                {
                    GUILayout.Space(6);

                    //todo charasGameParam.menstruations
                    //todo charasGameParam.sensitivity
                    //charasGameParam.sensitivity.tableFavorabiliry

                    if (GUILayout.Button("Inspect charasGameParam"))
                        Inspector.Instance.Push(new InstanceStackEntry(charasGameParam, "charasGameParam " + GetCharaName(currentAdvChara)), true);
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Navigate to Character's GameObject"))
                {
                    if (currentAdvChara.transform)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvChara.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, "Character has no body assigned");
                }

                if (GUILayout.Button("Open Character in inspector"))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvChara, "Actor " + GetCharaName(currentAdvChara)), true);

                //if (GUILayout.Button("Inspect extended data"))
                //{
                //    Inspector.Instance.Push(new InstanceStackEntry(ExtensibleSaveFormat.ExtendedSave.GetAllExtendedData(currentAdvChara.chaFile), "ExtData for " + currentAdvChara.Name), true);
                //}
            }
            GUILayout.EndVertical();
        }

        private static class Hooks
        {
            #region Speedhack

            [HarmonyPostfix]
            [HarmonyPatch(typeof(AIBase), nameof(AIBase.FixedUpdate))]
            private static void MovementSpeedOverride(AIBase __instance)
            {
                if (SpeedMode == SpeedModes.Normal || !__instance) return;

                var rai = __instance.TryCast<SVRichAI>();
                if (rai == null) return;

                switch (SpeedMode)
                {
                    case SpeedModes.ReturnToNormal:
                        rai.accelType = 0;
                        rai.slowSpeed = 1;
                        rai.maxSpeed = 6;
                        rai.acceleration = 6;
                        SpeedMode = SpeedModes.Normal;
                        break;

                    case SpeedModes.Fast:
                        rai.accelType = 1;
                        rai.slowSpeed = 10;
                        rai.maxSpeed = 10;
                        rai.acceleration = 10;
                        break;
                    case SpeedModes.Sanic:
                        rai.accelType = 1;
                        rai.slowSpeed = 100;
                        rai.maxSpeed = 100;
                        rai.acceleration = 100;
                        break;

                    case SpeedModes.Normal:
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public enum SpeedModes
            {
                ReturnToNormal = -1,
                Normal = 0,
                Fast,
                Sanic
            }

            public static SpeedModes SpeedMode;

            #endregion

            #region RNG manip

            public static bool RiggedRng = false;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ProbabilityCalculation), nameof(ProbabilityCalculation.Detect), typeof(int))]
            [HarmonyPatch(typeof(ProbabilityCalculation), nameof(ProbabilityCalculation.Detect), typeof(float))]
            private static void ProbabilityCalculation_Detect_Override(ref bool __result)
            {
                if (RiggedRng) __result = true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Random), nameof(Random.Next), typeof(int))]
            [HarmonyPatch(typeof(Random), nameof(Random.Next), typeof(int), typeof(int))]
            private static void Random_Next_Override(int maxValue, ref int __result)
            {
                if (RiggedRng) __result = maxValue - 1;
            }

            #endregion
        }
    }
}
