using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Character;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using IllusionMods;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using SaveData;
using SaveData.Extension;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;

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
        private static Actor _currentVisibleChara, _currentVisibleCharaMain;
        private static bool InsideADV => ADV.ADVManager._instance?.IsADV == true;
        private static bool InsideH => SV.H.HScene.Active();

        // 翻译相关成员
        private static Dictionary<string, string> _translations;
        private static string _currentLanguage = "en";
        private static string _pluginLocation;
        private static readonly Dictionary<string, string> CachedTranslations = new();
        private static Dictionary<string, string> SupportedLanguages;
        internal static ConfigEntry<string> SelectedLanguage;

        public static void Initialize(CheatToolsPlugin instance)
        {
            _pluginLocation = instance.Info.Location;

            var config = instance.Config;
            SelectedLanguage = config.Bind("General", "Language", "en", T("UI.ConfigLanguageDesc"));
            _currentLanguage = SelectedLanguage.Value;

            LoadSupportedLanguages();
            LoadLanguage(_currentLanguage);

            SelectedLanguage.SettingChanged += (sender, args) =>
            {
                _currentLanguage = SelectedLanguage.Value;
                LoadLanguage(_currentLanguage);
            };

            CheatToolsWindow.OnShown += window =>
            {
                _openInInspectorButtons = new[]
                {
                    new KeyValuePair<object, string>((object)SV.H.HScene._instance ?? SV.H.HScene._instance, T("UI.SVHScene")),
                    new KeyValuePair<object, string>(ADV.ADVManager._instance, T("UI.ADVManager")),
                    new KeyValuePair<object, string>((object)Manager.Game._instance ?? typeof(Manager.Game), T("UI.ManagerGame")),
                    new KeyValuePair<object, string>(Manager.Game.saveData, T("UI.ManagerGameSaveData")),
                    new KeyValuePair<object, string>(typeof(Manager.Config), T("UI.ManagerConfig")),
                    new KeyValuePair<object, string>((object)Manager.Scene._instance ?? typeof(Manager.Scene), T("UI.ManagerScene")),
                    new KeyValuePair<object, string>((object)Manager.Sound._instance ?? typeof(Manager.Sound), T("UI.ManagerSound")),
                    new KeyValuePair<object, string>(typeof(Manager.GameSystem), T("UI.ManagerGameSystem")),
                    new KeyValuePair<object, string>((object)Manager.MapManager._instance ?? typeof(Manager.MapManager), T("UI.ManagerMapManager")),
                    new KeyValuePair<object, string>((object)Manager.SimulationManager._instance ?? typeof(Manager.SimulationManager), T("UI.ManagerSimulationManager")),
                    new KeyValuePair<object, string>((object)Manager.TalkManager._instance ?? typeof(Manager.TalkManager), T("UI.ManagerTalkManager")),
                };

                if (_belongingsDropdown == null)
                {
                    _belongingsDropdown = new ImguiComboBoxSimple(Manager.Game.BelongingsInfoTable.AsManagedEnumerable().OrderBy(x => x.Key).Select(x => new GUIContent(x.Value)).ToArray());
                    for (var i = 0; i < _belongingsDropdown.Contents.Length; i++)
                    {
                        var iCopy = i;
                        TranslationHelper.TranslateAsync(_belongingsDropdown.Contents[iCopy].text, s => _belongingsDropdown.Contents[iCopy].text = s);
                    }
                    window.ComboBoxesToDisplay.Add(_belongingsDropdown);
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
                    window.ComboBoxesToDisplay.Add(_traitsDropdown);
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
                    }
                    window.ComboBoxesToDisplay.Add(_hPreferenceDropdown);
                }
                window.ComboBoxesToDisplay.Add(_otherCharaDropdown);
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => InsideH, DrawHSceneCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => InsideADV, DrawAdvCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => Manager.Game.saveData.WorldTime > 0, DrawGeneralCheats, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(_ => Manager.Game.Charas.Count > 0, DrawGirlCheatMenu, T("UI.NoCharactersToEdit")));
            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

            Harmony.CreateAndPatchAll(typeof(Hooks));
        }

        #region 翻译系统

        private static void LoadSupportedLanguages()
        {
            string languagesFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), "languages.json");
            if (File.Exists(languagesFilePath))
            {
                try
                {
                    string json = File.ReadAllText(languagesFilePath);
                    var languages = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, string>>>>(json);
                    SupportedLanguages = languages["supportedLanguages"].ToDictionary(l => l["code"], l => l["name"]);
                }
                catch (Exception ex)
                {
                    CheatToolsPlugin.Logger.LogError($"无法加载 languages.json: {ex.Message}");
                    SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "zh_CN", "中文" } };
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning("未找到 languages.json，使用默认语言。");
                SupportedLanguages = new Dictionary<string, string> { { "en", "English" }, { "zh_CN", "中文" } };
            }
        }

        private static void LoadLanguage(string langCode)
        {
            string langFilePath = Path.Combine(Path.GetDirectoryName(_pluginLocation), $"lang_{langCode}.json");
            if (File.Exists(langFilePath))
            {
                try
                {
                    string json = File.ReadAllText(langFilePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    CheatToolsPlugin.Logger.LogInfo($"成功加载语言: {langCode}");
                }
                catch (Exception ex)
                {
                    CheatToolsPlugin.Logger.LogError($"加载 {langFilePath} 失败: {ex.Message}");
                    _translations = new Dictionary<string, string>();
                }
            }
            else
            {
                CheatToolsPlugin.Logger.LogWarning($"未找到语言文件 {langFilePath}。");
                _translations = new Dictionary<string, string>();
            }
            CachedTranslations.Clear();
        }

        private static string T(string key)
        {
            if (CachedTranslations.TryGetValue(key, out string value))
                return value;

            if (_translations != null && _translations.TryGetValue(key, out value))
            {
                CachedTranslations[key] = value;
                return value;
            }

            if (_currentLanguage != "en")
            {
                string fallbackPath = Path.Combine(Path.GetDirectoryName(_pluginLocation), "lang_en.json");
                if (File.Exists(fallbackPath))
                {
                    try
                    {
                        string json = File.ReadAllText(fallbackPath);
                        var fallbackTranslations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (fallbackTranslations.TryGetValue(key, out value))
                        {
                            CachedTranslations[key] = value;
                            return value;
                        }
                    }
                    catch (Exception ex)
                    {
                        CheatToolsPlugin.Logger.LogError($"无法加载回退语言 (en): {ex.Message}");
                    }
                }
            }

            CheatToolsPlugin.Logger.LogWarning($"语言 '{_currentLanguage}' 中缺少翻译键 '{key}'。");
            return key.Split('.').Last();
        }

        private static void DrawLanguageSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("UI.Language") + ": ", GUILayout.Width(100));
            foreach (var lang in SupportedLanguages)
            {
                if (GUILayout.Button(lang.Value))
                {
                    if (_currentLanguage != lang.Key)
                    {
                        _currentLanguage = lang.Key;
                        SelectedLanguage.Value = _currentLanguage;
                        LoadLanguage(_currentLanguage);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        #endregion

        private static void DrawHSceneCheats(CheatToolsWindow cheatToolsWindow)
        {
            var hScene = SV.H.HScene._instance;

            GUILayout.Label(T("UI.HSceneControls"));

            foreach (var actor in hScene.Actors)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(string.Format(T("UI.ActorGauge"), actor.Name, actor.GaugeValue.ToString("N1")), GUILayout.Width(150));
                    GUI.changed = false;
                    var newValue = GUILayout.HorizontalSlider(actor.GaugeValue, 0, 100);
                    if (GUI.changed)
                        actor.SetGaugeValue(newValue);
                }
                GUILayout.EndHorizontal();
            }

            DrawBackgroundHideToggles();

            if (GUILayout.Button(T("UI.OpenHSceneInInspector")))
                Inspector.Instance.Push(new InstanceStackEntry(hScene, T("UI.SVHScene")), true);
        }

        private static GameObject _bgPanel, _bgDownFrame, _bgUpFrame;
        private static void DrawAdvCheats(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.ADVSceneControls"));

            if (GUILayout.Button(new GUIContent(T("UI.ForceUnlockTalkOptions"), null, T("UI.ForceUnlockTalkOptionsTooltip"))))
            {
                var commandUi = UnityEngine.Object.FindObjectOfType<SV.CommandUI>();
                foreach (var btn in commandUi.GetComponentsInChildren<Button>(true))
                    btn.interactable = true;
            }

            DrawBackgroundHideToggles();
        }

        private static void DrawBackgroundHideToggles()
        {
            if (!_bgPanel)
            {
                var bgCmp = Manager.Game.Instance.transform.GetComponentInChildren<SV.HighPolyBackGroundFrame>();
                var tr = bgCmp.animFrame.transform;
                _bgPanel = tr.Find("Panel").gameObject;
                _bgDownFrame = tr.Find("DownFrame").gameObject;
                _bgUpFrame = tr.Find("UpFrame").gameObject;
            }

            var prevActive = _bgDownFrame.activeSelf;
            var newActive = GUILayout.Toggle(prevActive, T("UI.ShowBackgroundFrame"));
            if (prevActive != newActive)
            {
                _bgDownFrame.active = newActive;
                _bgUpFrame.active = newActive;
            }

            prevActive = _bgPanel.activeSelf;
            newActive = GUILayout.Toggle(prevActive, T("UI.ShowBackgroundBlur"));
            if (prevActive != newActive)
                _bgPanel.active = newActive;
        }

        private static void DrawGeneralCheats(CheatToolsWindow cheatToolsWindow)
        {
            Hooks.RiggedRng = GUILayout.Toggle(Hooks.RiggedRng, new GUIContent(T("UI.RiggedRNG"), null, T("UI.RiggedRNGTooltip")));

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(T("UI.WalkingSpeed"));

                var normal = Hooks.SpeedMode == Hooks.SpeedModes.Normal || Hooks.SpeedMode == Hooks.SpeedModes.ReturnToNormal;
                var newNormal = GUILayout.Toggle(normal, T("UI.SpeedNormal"));
                if (!normal && newNormal)
                    Hooks.SpeedMode = Hooks.SpeedModes.ReturnToNormal;
                if (GUILayout.Toggle(Hooks.SpeedMode == Hooks.SpeedModes.Fast, T("UI.SpeedFast")))
                    Hooks.SpeedMode = Hooks.SpeedModes.Fast;
                if (GUILayout.Toggle(Hooks.SpeedMode == Hooks.SpeedModes.Sanic, T("UI.SpeedSanic")))
                    Hooks.SpeedMode = Hooks.SpeedModes.Sanic;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                Hooks.InterruptBlock = GUILayout.Toggle(Hooks.InterruptBlock, new GUIContent(T("UI.BlockInterrupts"), null, T("UI.BlockInterruptsTooltip")));
                Hooks.InterruptBlockAllow3P = GUILayout.Toggle(Hooks.InterruptBlockAllow3P, new GUIContent(T("UI.Except3P"), null, T("UI.Except3PTooltip")));
                Hooks.InterruptBlockAllowNonPlayer = GUILayout.Toggle(Hooks.InterruptBlockAllowNonPlayer, new GUIContent(T("UI.OnlyPlayer"), null, T("UI.OnlyPlayerTooltip")));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUI.enabled = !ReferenceEquals(SV.GameChara.PlayerAI, null);
            if (GUILayout.Button(T("UI.UnlimitedTimeLimit")))
                SV.GameChara.PlayerAI!.charaData.charasGameParam.baseParameter.NowStamina = 100000;
            GUI.enabled = true;

            DrawUtils.DrawNums(T("UI.Weekday"), 7, () => (byte)Manager.Game.saveData.Week, b => Manager.Game.saveData.Week = b);

            DrawUtils.DrawInt(T("UI.DayCount"), () => Manager.Game.saveData.Day, i => Manager.Game.saveData.Day = i, T("UI.DayCountTooltip"));
        }

        private static void DrawGirlCheatMenu(CheatToolsWindow cheatToolsWindow)
        {
            GUILayout.Label(T("UI.CharacterStatusEditor"));

            foreach (var chara in GameUtilities.GetCurrentActors(false))
            {
                var main = chara.Value.FindMainActorInstance();
                var isCopy = !ReferenceEquals(main.Value, chara.Value);
                if (GUILayout.Button(string.Format(T("UI.SelectCharacter"), chara.Key, chara.Value.GetCharaName(true), isCopy ? T("UI.Copy") : "")))
                {
                    _currentVisibleChara = chara.Value;
                    _currentVisibleCharaMain = isCopy ? main.Value : null;
                }
            }

            GUILayout.Space(6);

            try
            {
                if (_currentVisibleChara != null)
                    DrawSingleCharaCheats(_currentVisibleChara, _currentVisibleCharaMain, cheatToolsWindow);
                else
                    GUILayout.Label(T("UI.SelectCharacterToEdit"));
            }
            catch (Exception e)
            {
                CheatToolsPlugin.Logger.LogError(e);
                _currentVisibleChara = null;
            }
        }

        private static void DrawSingleCharaCheats(Actor currentAdvChara, Actor mainChara, CheatToolsWindow cheatToolsWindow)
        {
            var comboboxMaxY = (int)cheatToolsWindow.WindowRect.bottom - 30;
            var isCopy = mainChara != null;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(T("UI.Selected"), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(currentAdvChara.GetCharaName(true), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(T("UI.Close"), IMGUIUtils.LayoutOptionsExpandWidthFalse)) _currentVisibleChara = null;
                }
                GUILayout.EndHorizontal();

                if (isCopy)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(new GUIContent(T("UI.CharacterIsCopy"), null, T("UI.CharacterIsCopyTooltip")), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(T("UI.OpenMain")))
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
                    GUILayout.Label(T("UI.InGameStats"));

                    DrawUtils.DrawSlider(T("UI.Stamina"), 0, 1000, () => charasGameParam.baseParameter.Stamina, val => charasGameParam.baseParameter.Stamina = val);
                    DrawUtils.DrawSlider(T("UI.NowStamina"), 0, charasGameParam.baseParameter.Stamina + 100, () => charasGameParam.baseParameter.NowStamina, val => charasGameParam.baseParameter.NowStamina = val, T("UI.NowStaminaTooltip"));
                    DrawUtils.DrawSlider(T("UI.Conversation"), 0, 1000, () => charasGameParam.baseParameter.Conversation, val => charasGameParam.baseParameter.Conversation = val);
                    DrawUtils.DrawSlider(T("UI.Study"), 0, 1000, () => charasGameParam.baseParameter.Study, val => charasGameParam.baseParameter.Study = val);
                    DrawUtils.DrawSlider(T("UI.Living"), 0, 1000, () => charasGameParam.baseParameter.Living, val => charasGameParam.baseParameter.Living = val);
                    DrawUtils.DrawSlider(T("UI.Job"), 0, 1000, () => charasGameParam.baseParameter.Job, val => charasGameParam.baseParameter.Job = val, T("UI.JobTooltip"));

                    GUILayout.Space(6);

                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        var menstruationsLength = charasGameParam.menstruations.Length;
                        var currentDayIndex = Manager.Game.saveData.Day % menstruationsLength;

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label(T("UI.Menstruation"));

                            GUI.color = currentAdvChara.IsMenstruation(ActorExtensionH.Menstruation.Normal) ? Color.green : Color.white;
                            if (GUILayout.Button(T("UI.MenstruationNormal"))) SetMenstruationForDay(currentDayIndex, 0);
                            GUI.color = currentAdvChara.IsMenstruation(ActorExtensionH.Menstruation.Safe) ? Color.green : Color.white;
                            if (GUILayout.Button(T("UI.MenstruationSafe"))) SetMenstruationForDay(currentDayIndex, 1);
                            GUI.color = currentAdvChara.IsMenstruation(ActorExtensionH.Menstruation.Danger) ? Color.green : Color.white;
                            if (GUILayout.Button(T("UI.MenstruationDanger"))) SetMenstruationForDay(currentDayIndex, 2);
                            GUI.color = Color.white;
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label($"{menstruationsLength / 7}-weekly", GUILayout.Width(80));
                            var mensUiItems = new GUIContent[] { new(T("UI.MenstruationN")), new(T("UI.MenstruationS")), new(T("UI.MenstruationD")) };
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
                                    GUILayout.Label(T("UI.Schedule"), GUILayout.Width(80));
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
                    if (isCopy)
                    {
                        GUILayout.Label(T("UI.CannotEditRelationshipsCopy"));
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(T("UI.EditRelationshipWith"));

                        var targets = Manager.Game.saveData.Charas.AsManagedEnumerable().Select(x => x.Value).Where(x => x != null && !x.Equals(currentAdvChara)).ToArray();

                        _otherCharaListIndex = Math.Clamp(_otherCharaListIndex, -1, targets.Length - 1);

                        GUI.changed = false;
                        var result = GUILayout.Toggle(_otherCharaListIndex == -1, T("UI.Everyone"));
                        if (GUI.changed)
                            _otherCharaListIndex = result ? -1 : 0;

                        GUILayout.EndHorizontal();

                        if (_otherCharaListIndex >= 0)
                        {
                            _otherCharaListIndex = _otherCharaDropdown.Show(_otherCharaListIndex, targets.Select(x => new GUIContent(x.GetCharaName(true))).ToArray(), comboboxMaxY);
                            targets = new[] { targets[_otherCharaListIndex] };
                        }

                        if (targets.Length == 1)
                        {
                            var targetChara = targets[0];
                            var targetCharaId = targetChara.TryGetActorId();
                            var to = currentAdvChara.charasGameParam.baseParameter.GetHAffinity(targetCharaId);
                            var currentCharaId = currentAdvChara.TryGetActorId();
                            var targetBaseParameter = targetChara.charasGameParam.baseParameter;
                            var fro = targetBaseParameter.GetHAffinity(currentCharaId);

                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(T("UI.HAffinity"));
                                GUILayout.FlexibleSpace();
                                GUILayout.Label(string.Format(T("UI.HAffinityTo"), to.LV, to.Point), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                                if (GUILayout.Button("+1")) currentAdvChara.charasGameParam.baseParameter.AddHAffinity(targetCharaId, 20);
                                if (GUILayout.Button("0")) currentAdvChara.charasGameParam.baseParameter.RemoveHAffinity(targetCharaId);
                                GUILayout.FlexibleSpace();
                                GUILayout.Label(string.Format(T("UI.HAffinityFro"), fro.LV, fro.Point), IMGUIUtils.LayoutOptionsExpandWidthFalse);
                                if (GUILayout.Button("+1")) targetBaseParameter.AddHAffinity(currentCharaId, 20);
                                if (GUILayout.Button("0")) targetBaseParameter.RemoveHAffinity(currentCharaId);
                            }
                            GUILayout.EndHorizontal();
                        }
                        else if (targets.Length > 1)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Label(T("UI.HAffinityEveryone"));
                                if (GUILayout.Button(T("UI.MaxLevel")))
                                {
                                    var targetIds = targets.Select(x => x.TryGetActorId()).ToArray();
                                    foreach (var targetId in targetIds) currentAdvChara.charasGameParam.baseParameter.AddHAffinity(targetId, 100);

                                    var currentCharaId = currentAdvChara.TryGetActorId();
                                    foreach (var target in targets) target.charasGameParam.baseParameter.AddHAffinity(currentCharaId, 100);
                                }
                                if (GUILayout.Button(T("UI.SetToZero")))
                                {
                                    var targetIds = targets.Select(x => x.TryGetActorId()).ToArray();
                                    foreach (var targetId in targetIds) currentAdvChara.charasGameParam.baseParameter.RemoveHAffinity(targetId);

                                    var currentCharaId = currentAdvChara.TryGetActorId();
                                    foreach (var target in targets) target.charasGameParam.baseParameter.RemoveHAffinity(currentCharaId);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.Label(T("UI.RelationshipWarning"));

                        DrawSingleRankEditor(SensitivityKind.Love, currentAdvChara, targets);
                        DrawSingleRankEditor(SensitivityKind.Friend, currentAdvChara, targets);
                        DrawSingleRankEditor(SensitivityKind.Distant, currentAdvChara, targets);
                        DrawSingleRankEditor(SensitivityKind.Dislike, currentAdvChara, targets);
                    }
                    GUILayout.EndVertical();
                }

                var gameParam = currentAdvChara.charFile.GameParameter;
                if (gameParam != null)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        GUILayout.Label(T("UI.CardStats"));

                        DrawUtils.DrawStrings(T("UI.Job"), new[] { T("UI.JobNone"), T("UI.JobLifeguard"), T("UI.JobCafe"), T("UI.JobShrine") }, () => gameParam.job, b => gameParam.job = b);
                        DrawUtils.DrawNums(T("UI.Gayness"), 5, () => gameParam.sexualTarget, b => gameParam.sexualTarget = b);
                        DrawUtils.DrawNums(T("UI.LvChastity"), 5, () => gameParam.lvChastity, b => gameParam.lvChastity = b);
                        DrawUtils.DrawNums(T("UI.LvSociability"), 5, () => gameParam.lvSociability, b => gameParam.lvSociability = b);
                        DrawUtils.DrawNums(T("UI.LvTalk"), 5, () => gameParam.lvTalk, b => gameParam.lvTalk = b);
                        DrawUtils.DrawNums(T("UI.LvStudy"), 5, () => gameParam.lvStudy, b => gameParam.lvStudy = b);
                        DrawUtils.DrawNums(T("UI.LvLiving"), 5, () => gameParam.lvLiving, b => gameParam.lvLiving = b);
                        DrawUtils.DrawNums(T("UI.LvPhysical"), 5, () => gameParam.lvPhysical, b => gameParam.lvPhysical = b);
                        DrawUtils.DrawNums(T("UI.FightingStyle"), 3, () => gameParam.lvDefeat, b => gameParam.lvDefeat = b);

                        DrawUtils.DrawBool(T("UI.IsVirgin"), () => gameParam.isVirgin, b => gameParam.isVirgin = b);
                        DrawUtils.DrawBool(T("UI.IsAnalVirgin"), () => gameParam.isAnalVirgin, b => gameParam.isAnalVirgin = b);
                        DrawUtils.DrawBool(T("UI.IsMaleVirgin"), () => gameParam.isMaleVirgin, b => gameParam.isMaleVirgin = b);
                        DrawUtils.DrawBool(T("UI.IsMaleAnalVirgin"), () => gameParam.isMaleAnalVirgin, b => gameParam.isMaleAnalVirgin = b);
                    }
                    GUILayout.EndVertical();

                    GUILayout.Space(6);

                    DrawBelongingsPicker(gameParam, comboboxMaxY);
                    DrawTargetAnswersPicker(_hPreferenceDropdown, T("UI.HPreference"), gameParam, sv => sv.preferenceH, comboboxMaxY);
                    DrawTargetAnswersPicker(_traitsDropdown, T("UI.Trait"), gameParam, sv => sv.individuality, comboboxMaxY);
                }

                if (gameParam != null && GUILayout.Button(T("UI.InspectGameParameter")))
                    Inspector.Instance.Push(new InstanceStackEntry(gameParam, string.Format(T("UI.GameParam"), currentAdvChara.GetCharaName(true))), true);

                if (charasGameParam != null && GUILayout.Button(T("UI.InspectCharaGameParam")))
                    Inspector.Instance.Push(new InstanceStackEntry(charasGameParam, string.Format(T("UI.CharaGameParam"), currentAdvChara.GetCharaName(true))), true);

                if (GUILayout.Button(T("UI.NavigateToCharacterGameObject")))
                {
                    if (currentAdvChara.transform)
                        ObjectTreeViewer.Instance.SelectAndShowObject(currentAdvChara.transform);
                    else
                        CheatToolsPlugin.Logger.Log(LogLevel.Warning | LogLevel.Message, T("UI.CharacterNoBodyAssigned"));
                }

                if (GUILayout.Button(T("UI.OpenCharacterInInspector")))
                    Inspector.Instance.Push(new InstanceStackEntry(currentAdvChara, string.Format(T("UI.Actor"), currentAdvChara.GetCharaName(true))), true);
            }
            GUILayout.EndVertical();
        }

        private static void DrawBelongingsPicker(HumanDataGameParameter_SV gameParam, int comboboxMaxY)
        {
            if (_belongingsDropdown == null) return;

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label(T("UI.ItemsOwned"));
            var targetArr = gameParam.belongings;
            foreach (var gameParameterBelonging in targetArr)
            {
                GUILayout.BeginHorizontal();
                {
                    if (gameParameterBelonging >= 0 && gameParameterBelonging < _belongingsDropdown.Contents.Length)
                        GUILayout.Label(_belongingsDropdown.Contents[gameParameterBelonging]);
                    else
                        GUILayout.Label(string.Format(T("UI.UnknownItemId"), gameParameterBelonging));

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
                if (GUILayout.Button(T("UI.Give"), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    if (!gameParam.belongings.Contains(_belongingsDropdown.Index))
                        gameParam.belongings = new Il2CppStructArray<int>(targetArr.AddItem(_belongingsDropdown.Index).ToArray());
                }
                if (GUILayout.Button(new GUIContent(T("UI.ToAll"), null, T("UI.ToAllTooltip")), IMGUIUtils.LayoutOptionsExpandWidthFalse))
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
                        GUILayout.Label(string.Format(T("UI.UnknownId"), name, traitId));

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

                if (GUILayout.Button(new GUIContent(T("UI.Add"), null, T("UI.AddTooltip")), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    SetAnswer(answerBase, selectedTraitIndex);
                }
                if (GUILayout.Button(new GUIContent(T("UI.ToAll"), null, T("UI.ToAllTooltip")), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    foreach (var chara in Manager.Game.Charas.AsManagedEnumerable().Select(x => x.Value))
                        SetAnswer(targetAnswers(chara.charFile.GameParameter), selectedTraitIndex);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(new GUIContent(T("UI.ClearAll"), null, T("UI.ClearAllTooltip")), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    foreach (var chara in Manager.Game.Charas.AsManagedEnumerable().Select(x => x.Value))
                        targetAnswers(chara.charFile.GameParameter).answer = new Il2CppStructArray<int>(new[] { -1, -1 });
                }
                if (GUILayout.Button(new GUIContent(T("UI.TrimAllToTwo"), null, T("UI.TrimAllToTwoTooltip")), IMGUIUtils.LayoutOptionsExpandWidthFalse))
                {
                    foreach (var chara in Manager.Game.Charas.AsManagedEnumerable().Select(x => x.Value))
                    {
                        var answers = targetAnswers(chara.charFile.GameParameter);
                        var old = answers.answer;
                        answers.answer = new Il2CppStructArray<int>(2);
                        answers.answer[0] = old.Length >= 1 ? old[0] : -1;
                        answers.answer[1] = old.Length >= 2 ? old[1] : -1;
                    }
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
                GUILayout.Label(T($"UI.SensitivityKind.{kind}") + ":", GUILayout.Width(45));

                GUILayout.Label(new GUIContent(T("UI.To"), null, T("UI.ToTooltip")));

                if (affectedCharas.Count == 1)
                {
                    var rank = targetCharaSensitivity.tableFavorabiliry[affectedCharas[0].TryGetActorId()].ranks[(int)kind];
                    GUILayout.Label(((int)rank).ToString());
                }

                if (GUILayout.Button("+1")) OnOutgoing(1);
                if (GUILayout.Button("-1")) OnOutgoing(-1);

                GUILayout.Label(new GUIContent(T("UI.From"), null, T("UI.FromTooltip")));

                if (affectedCharas.Count == 1)
                {
                    var rank = affectedCharas[0].charasGameParam.sensitivity.tableFavorabiliry[targetChara.TryGetActorId()].ranks[(int)kind];
                    GUILayout.Label(((int)rank).ToString());
                }

                if (GUILayout.Button("+1")) OnIncoming(1);
                if (GUILayout.Button("-1")) OnIncoming(-1);
            }
            GUILayout.EndHorizontal();

            void OnOutgoing(int amount)
            {
                var targetIds = affectedCharas.Select(actor => actor.TryGetActorId()).ToArray();
                foreach (var tabkvp in targetCharaSensitivity.tableFavorabiliry)
                {
                    if (targetIds.Contains(tabkvp.Key))
                    {
                        ChangeRank(tabkvp.Value, kind, amount);
                    }
                }
            }

            void OnIncoming(int amount)
            {
                var ourId = targetChara.TryGetActorId();
                foreach (var charaKvp in affectedCharas)
                {
                    var otherSensitivity = charaKvp.charasGameParam.sensitivity;
                    var favorabiliryInfo = otherSensitivity.tableFavorabiliry[ourId];
                    ChangeRank(favorabiliryInfo, kind, amount);
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
