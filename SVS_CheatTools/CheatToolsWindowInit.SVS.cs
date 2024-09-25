using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Character;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using SaveData;
using SaveData.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static ImguiComboBoxSimple _belongingsDropdown;
        private static ImguiComboBoxSimple _traitsDropdown;
        private static ImguiComboBoxSimple _hPreferenceDropdown;
        private static int _otherCharaListIndex;
        private static ImguiComboBox _otherCharaDropdown = new();
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
                if (_traitsDropdown == null)
                {
                    var guiContents = Manager.Game.IndividualityInfoTable.AsManagedEnumerable().ToDictionary(x => x.Value.ID, x => new GUIContent(x.Value.Name, null, x.Value.Information)).OrderBy(x => x.Key).ToList();
                    _traitsDropdown = new ImguiComboBoxSimple(guiContents.Select(x => x.Value).ToArray());
                    _traitsDropdown.ContentsIndexes = guiContents.Select(x => x.Key).ToArray();
                    for (var i = 0; i < _traitsDropdown.Contents.Length; i++)
                    {
                        var iCopy = i;
                        TranslationHelper.TranslateAsync(_traitsDropdown.Contents[iCopy].text, s => _traitsDropdown.Contents[iCopy].text = s);
                        TranslationHelper.TranslateAsync(_traitsDropdown.Contents[iCopy].tooltip, s => _traitsDropdown.Contents[iCopy].tooltip = s);
                    }
                }

                if (_hPreferenceDropdown == null)
                {
                    var guiContents = Manager.Game.PreferenceHInfoTable.AsManagedEnumerable().ToDictionary(x => x.Key, x => new GUIContent(x.Value)).OrderBy(x => x.Key).ToList();
                    _hPreferenceDropdown = new ImguiComboBoxSimple(guiContents.Select(x => x.Value).ToArray());
                    _hPreferenceDropdown.ContentsIndexes = guiContents.Select(x => x.Key).ToArray();
                    for (var i = 0; i < _hPreferenceDropdown.Contents.Length; i++)
                    {
                        var iCopy = i;
                        TranslationHelper.TranslateAsync(_hPreferenceDropdown.Contents[iCopy].text, s => _hPreferenceDropdown.Contents[iCopy].text = s);
                        //TranslationHelper.TranslateAsync(_hPreferenceDropdown.Contents[iCopy].tooltip, s => _hPreferenceDropdown.Contents[iCopy].tooltip = s);
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
                _traitsDropdown?.DrawDropdownIfOpen();
                _hPreferenceDropdown?.DrawDropdownIfOpen();
                _otherCharaDropdown?.DrawDropdownIfOpen();
            }, ""));
        }

        private static string GetCharaName(this Actor chara)
        {
            var fullname = chara?.charFile?.Parameter?.fullname;
            return !string.IsNullOrEmpty(fullname) ? fullname : chara?.chaCtrl?.name ?? chara?.ToString();
        }
        private static int GetActorId(this Actor currentAdvChara)
        {
            return Manager.Game.Charas.AsManagedEnumerable().Single(x => x.Value.Equals(currentAdvChara)).Key;
        }

        private static IEnumerable<KeyValuePair<int, Actor>> GetVisibleCharas()
        {
            if (SV.H.HScene.Active())
            {
                // HScene.Actors contains copies of the actors. Couldn't find a better way to get the originals
                return SV.H.HScene._instance.Actors.Select(GetMainActorInstance).Where(x => x.Value != null);
            }

            var talkManager = Manager.TalkManager._instance;
            if (talkManager != null && ADV.ADVManager._instance?.IsADV == true)
            {
                return new List<KeyValuePair<int, Actor>>
                {
                    // PlayerHi and Npc1-4 contain copies of the Actors
                    GetMainActorInstance(talkManager.PlayerHi),
                    GetMainActorInstance(talkManager.Npc1),
                    GetMainActorInstance(talkManager.Npc2),
                    GetMainActorInstance(talkManager.Npc3),
                    GetMainActorInstance(talkManager.Npc4),
                }.Where(x => x.Value != null);
            }

            return Manager.Game.saveData.Charas.AsManagedEnumerable().Where(x => x.Value != null);
        }

        private static KeyValuePair<int, Actor> GetMainActorInstance(this SV.H.HActor x) => x?.Actor.GetMainActorInstance() ?? default;
        private static KeyValuePair<int, Actor> GetMainActorInstance(this Actor x) => x == null ? default : Manager.Game.Charas.AsManagedEnumerable().FirstOrDefault(y => x.charFile.About.dataID == y.Value.charFile.About.dataID);

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            var hScene = SV.H.HScene._instance;

            GUILayout.Label("H scene controls");

            foreach (var actor in hScene.Actors)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(actor.Name + " Gauge: " + actor.GaugeValue.ToString("N1"), GUILayout.Width(150));
                    GUI.changed = false;
                    var newValue = GUILayout.HorizontalSlider(actor.GaugeValue, 0, 100);
                    if (GUI.changed)
                        actor.SetGaugeValue(newValue);

                    // todo editing siru array doesn't cause updates
                    //    for (int i = 0; i < hActor._siruLv.Length; i++)
                    //    {
                    //        GUILayout.BeginHorizontal();
                    //        GUILayout.Label($"{(ChaFileDefine.SiruParts)i}: lv{hActor._siruLv[i]}", GUILayout.Width(150));
                    //        hActor._siruLv[i] = (byte)GUILayout.HorizontalSlider(hActor._siruLv[i], 0, 6);
                    //        GUILayout.EndHorizontal();
                    //    }
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Open HScene in inspector"))
                Inspector.Instance.Push(new InstanceStackEntry(hScene, "SV.H.HScene"), true);
        }

        private static void DrawGeneralCheats(CheatToolsWindow obj)
        {
            Hooks.RiggedRng = GUILayout.Toggle(Hooks.RiggedRng, new GUIContent("Rigged RNG (success if above 0%)", null, "All actions with at least 1% chance will always succeed. Must be activated BEFORE talking to a character.\nWARNING: This will affect RNG across the game. NPCs will (probably) always succeed with their actions which will skew the simulation heavily. Some events might never happen or keep repeating until this is turned off."));

            GUILayout.Space(5);

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

            GUILayout.BeginHorizontal();
            {
                Hooks.InterruptBlock = GUILayout.Toggle(Hooks.InterruptBlock, new GUIContent("Block interrupts", null, "Prevent NPCs from interrupting interactions of 2 other characters. This does not prevent NPCs from talking to idle characters."));
                Hooks.InterruptBlockAllow3P = GUILayout.Toggle(Hooks.InterruptBlockAllow3P, new GUIContent("except 3P", null, "Do not block NPCs interrupting to ask for a threesome."));
                Hooks.InterruptBlockAllowNonPlayer = GUILayout.Toggle(Hooks.InterruptBlockAllowNonPlayer, new GUIContent("only player", null, "Only block interrupts if player controls one of the involved characters."));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUI.enabled = ADV.ADVManager._instance?.IsADV == true;
            if (GUILayout.Button(new GUIContent("Force Unlock visible talk options", null, "Un-gray and make clickable all currently visible buttons in the talk menu. Mostly for use with the blackmail menu. If the chance is 0% you still won't be able to succeed at the action.")))
            {
                var commandUi = UnityEngine.Object.FindObjectOfType<SV.CommandUI>();
                // For some reason buttons are found and set as interactable, but if they are in a hidden menu they revert to inactive when unhidden
                foreach (var btn in commandUi.GetComponentsInChildren<Button>(true))
                    btn.interactable = true;
            }
            GUI.enabled = true;


            DrawUtils.DrawNums("Weekday", 7, () => (byte)Manager.Game.saveData.Week, b => Manager.Game.saveData.Week = b);

            DrawUtils.DrawInt("Day count", () => Manager.Game.saveData.Day, i => Manager.Game.saveData.Day = i, "Total number of days passed in-game. Used for calculating menstruation status and probably other things.");

            //GUILayout.BeginHorizontal();
            //{
            //    GUILayout.Label("asd: ");
            //}
            //GUILayout.EndHorizontal();
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label("Character status editor");

            foreach (var chara in GetVisibleCharas())
            {
                if (GUILayout.Button($"Select #{chara.Key} - {GetCharaName(chara.Value)}"))
                    _currentVisibleChara = chara.Value;
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

        private static void DrawSingleCharaCheats(Actor currentAdvChara, CheatToolsWindow cheatToolsWindow)
        {
            var comboboxMaxY = (int)cheatToolsWindow.WindowRect.bottom - 30;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Selected:", IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(GetCharaName(currentAdvChara), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", IMGUIUtils.LayoutOptionsExpandWidthFalse)) _currentVisibleChara = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);

                var charasGameParam = currentAdvChara.charasGameParam;
                if (charasGameParam != null)
                {
                    var baseParameter = currentAdvChara.charasGameParam.baseParameter;
                    if (baseParameter != null)
                    {
                        GUILayout.Label("In-game stats");

                        DrawUtils.DrawSlider(nameof(baseParameter.NowStamina), 0, baseParameter.Stamina + 100, () => baseParameter.NowStamina, val => baseParameter.NowStamina = val,
                                             "If character is controlled by player this is used for determining how long until the period ends. Initial value is equal to 'Stamina + 100'.");
                        DrawUtils.DrawSlider(nameof(baseParameter.Stamina), 0, 1000, () => baseParameter.Stamina, val => baseParameter.Stamina = val);
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

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        // DarkSoldier27: Ok I figure it out:
                        // 0:LOVE
                        // 1:FRIEND
                        // 2:INDIFFERENT
                        // 3:DISLIKE
                        // values go from 0 to 30, reaching 30 increase a favorability point in longSensitivityCounts <- this is what determined their status, the max value for this one is also 30
                        // and yeah reaching below 0 reduce a point

                        GUILayout.BeginHorizontal();

                        GUILayout.Label("Edit relationship with: ");

                        var targets = Manager.Game.saveData.Charas.AsManagedEnumerable().Select(x => x.Value).Where(x => x != null && !x.Equals(currentAdvChara)).ToArray();

                        _otherCharaListIndex = Math.Clamp(_otherCharaListIndex, -1, targets.Length - 1);

                        GUI.changed = false;
                        var result = GUILayout.Toggle(_otherCharaListIndex == -1, "Everyone");
                        if (GUI.changed)
                            _otherCharaListIndex = result ? -1 : 0;

                        GUILayout.EndHorizontal();

                        if (_otherCharaListIndex >= 0)
                        {
                            _otherCharaListIndex = _otherCharaDropdown.Show(_otherCharaListIndex, targets.Select(x => new GUIContent(GetCharaName(x))).ToArray(), comboboxMaxY);
                            targets = new[] { targets[_otherCharaListIndex] };
                        }

                        GUILayout.Label("WARNING: BETA, settings may be reset by the game randomly. Save-Load the game after editing for best chance to make it work.");

                        DrawSingleRankEditor(SensitivityKind.Love, currentAdvChara, targets);
                        DrawSingleRankEditor(SensitivityKind.Friend, currentAdvChara, targets);
                        DrawSingleRankEditor(SensitivityKind.Distant, currentAdvChara, targets);
                        DrawSingleRankEditor(SensitivityKind.Dislike, currentAdvChara, targets);
                    }
                    GUILayout.EndVertical();

                    //todo charasGameParam.sensitivity, same deal as relationships with tables and stocks

                    GUILayout.Space(6);
                }

                var gameParam = currentAdvChara.charFile.GameParameter;
                if (gameParam != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label("Card stats (same as in maker)");

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

                    GUILayout.Space(6);

                    DrawBelongingsPicker(gameParam, comboboxMaxY);
                    DrawTargetAnswersPicker(_hPreferenceDropdown, "H Preference", gameParam, sv => sv.preferenceH, comboboxMaxY);
                    DrawTargetAnswersPicker(_traitsDropdown, "Trait", gameParam, sv => sv.individuality, comboboxMaxY);
                }

                if (gameParam != null && GUILayout.Button("Inspect GameParameter"))
                    Inspector.Instance.Push(new InstanceStackEntry(gameParam, "GameParam " + GetCharaName(currentAdvChara)), true);

                if (charasGameParam != null && GUILayout.Button("Inspect CharactersGameParameter"))
                    Inspector.Instance.Push(new InstanceStackEntry(charasGameParam, "CharaGameParam " + GetCharaName(currentAdvChara)), true);

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

        private static void DrawBelongingsPicker(HumanDataGameParameter_SV gameParam, int comboboxMaxY)
        {
            if (_belongingsDropdown == null) return;

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
                _belongingsDropdown.Show(comboboxMaxY);
                if (GUILayout.Button("GIVE", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    if (!gameParam.belongings.Contains(_belongingsDropdown.Index))
                        gameParam.belongings = new Il2CppStructArray<int>(targetArr.AddItem(_belongingsDropdown.Index).ToArray());
                }
                if (GUILayout.Button(new GUIContent("TO ALL", null, "Give this item to ALL characters if they don't already have it, including you."), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    foreach (var chara in Manager.Game.Charas.AsManagedEnumerable().Select(x => x.Value))
                    {
                        if (!chara.charFile.GameParameter.belongings.Contains(_belongingsDropdown.Index))
                            chara.charFile.GameParameter.belongings = new Il2CppStructArray<int>(chara.charFile.GameParameter.belongings.AddItem(_belongingsDropdown.Index).ToArray());
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private static void DrawTargetAnswersPicker(ImguiComboBoxSimple combobox, string name, HumanDataGameParameter_SV currentCharaData, Func<HumanDataGameParameter_SV, HumanDataGameParameter_SV.AnswerBase> targetAnswers, int comboboxMaxY)
        {
            if (combobox == null) return;

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label(name + ":");
            var answerBase = targetAnswers(currentCharaData);
            var answerArr = answerBase.answer;
            foreach (var traitId in answerArr)
            {
                GUILayout.BeginHorizontal();
                {
                    var index = Array.IndexOf(combobox.ContentsIndexes, traitId);
                    if (index >= 0)
                    {
                        GUILayout.Label(combobox.Contents[index]);
                    }
                    else
                        GUILayout.Label($"Unknown {name} ID {traitId}");

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("X", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                    {
                        answerBase.Set(traitId, false);
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            {
                combobox.Show(comboboxMaxY);
                var selectedTraitIndex = combobox.ContentsIndexes[combobox.Index];

                if (GUILayout.Button(new GUIContent("ADD", null, "If you add more than 2 entries they will work in-game, but will be removed after you save/load the game or the character."), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    SetAnswer(answerBase, selectedTraitIndex);
                }
                if (GUILayout.Button(new GUIContent("TO ALL", null, "Add this entry to ALL characters, including you."), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    foreach (var chara in Manager.Game.Charas.AsManagedEnumerable().Select(x => x.Value))
                        SetAnswer(targetAnswers(chara.charFile.GameParameter), selectedTraitIndex);
                }
            }
            GUILayout.EndHorizontal();

            void SetAnswer(HumanDataGameParameter_SV.AnswerBase individuality, int id)
            {
                if (individuality.answer.Contains(-1))
                    individuality.Set(id, true);
                else if (!individuality.answer.Contains(id))
                    individuality.answer = individuality.answer.AddItem(id).ToArray();
            }

            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private static void DrawSingleRankEditor(SensitivityKind kind, Actor targetChara, IList<Actor> affectedCharas)
        {
            var targetCharaSensitivity = targetChara.charasGameParam.sensitivity;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(kind + ":", GUILayout.Width(45));

                GUILayout.Label(new GUIContent("to", null, "Current character's feelings towards the target character selected in the dropdown above.\nRanks: 0 - Low, 1 - Medium, 2 - High, 3 - Max"));

                if (affectedCharas.Count == 1)
                {
                    var rank = targetCharaSensitivity.tableFavorabiliry[GetActorId(affectedCharas[0])].ranks[(int)kind];
                    GUILayout.Label(((int)rank).ToString());
                }

                if (GUILayout.Button("+1")) OnOutgoing(1);
                if (GUILayout.Button("-1")) OnOutgoing(-1);

                GUILayout.Label(new GUIContent("from", null, "The target character's feelings towards current character."));

                if (affectedCharas.Count == 1)
                {
                    var rank = affectedCharas[0].charasGameParam.sensitivity.tableFavorabiliry[GetActorId(targetChara)].ranks[(int)kind];
                    GUILayout.Label(((int)rank).ToString());
                }

                if (GUILayout.Button("+1")) OnIncoming(1);
                if (GUILayout.Button("-1")) OnIncoming(-1);
            }
            GUILayout.EndHorizontal();
            return;

            void OnOutgoing(int amount)
            {
                var targetIds = affectedCharas.Select(GetActorId).ToArray();
                foreach (var tabkvp in targetCharaSensitivity.tableFavorabiliry)
                {
                    if (targetIds.Contains(tabkvp.Key))
                    {
                        ChangeRank(tabkvp.Value, kind, amount);

                        // All of these overwrite everything we just changed
                        // todo: need to find some way to update relationship status across the game without having to save/load
                        //targetCharaSensitivity.CalcFavorState(tabkvp.Value);
                        //targetCharaSensitivity.LongStockCalc(tabkvp.Value);
                        //targetCharaSensitivity.CalcHighvFavorability();
                    }
                }
            }
            void OnIncoming(int amount)
            {
                var ourId = GetActorId(targetChara);
                foreach (var charaKvp in affectedCharas)
                {
                    var otherSensitivity = charaKvp.charasGameParam.sensitivity;
                    var favorabiliryInfo = otherSensitivity.tableFavorabiliry[ourId];
                    ChangeRank(favorabiliryInfo, kind, amount);

                    //otherSensitivity.CalcFavorState(favorabiliryInfo);
                    //otherSensitivity.LongStockCalc(favorabiliryInfo);
                    //otherSensitivity.CalcHighvFavorability();
                }
            }
            void ChangeRank(SensitivityParameter.FavorabiliryInfo favorabiliryInfo, SensitivityKind sensitivityKind, int amount)
            {
                var newRank = (SensitivityParameter.Rank)Mathf.Clamp((int)(favorabiliryInfo.ranks[(int)sensitivityKind] + amount), 0, (int)SensitivityParameter.Rank.MAX);
                favorabiliryInfo.ranks[(int)sensitivityKind] = newRank;

                favorabiliryInfo.longSensitivityCounts[(int)sensitivityKind] = 10 * (int)newRank;
            }
        }
    }

    internal enum SensitivityKind
    {
        Love = 0,
        Friend = 1,
        Distant = 2,
        Dislike = 3
    }
}
