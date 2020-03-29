using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIChara;
using AIProject;
using AIProject.Definitions;
using AIProject.SaveData;
using HarmonyLib;
using Manager;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Map = Manager.Map;
using Resources = Manager.Resources;

namespace CheatTools
{
    public partial class CheatToolsWindow
    {
        private const int ScreenOffset = 20;

        private readonly RuntimeUnityEditorCore _editor;

        private readonly string _mainWindowTitle;
        private Vector2 _cheatsScrollPos;
        private Rect _cheatWindowRect;
        private Rect _screenRect;
        private bool _show;

        private AgentActor _currentVisibleGirl;

        private Studio.Studio _studioInstance;
        private Manager.Sound _soundInstance;
        private Scene _sceneInstance;
        private Game _gameMgr;
        private Resources _resources;
        private Map _map;
        private HSceneFlagCtrl _hScene;

        public CheatToolsWindow(RuntimeUnityEditorCore editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            ToStringConverter.AddConverter<AgentActor>(heroine => !string.IsNullOrEmpty(heroine.CharaName) ? heroine.CharaName : heroine.name);
            ToStringConverter.AddConverter<AgentData>(d => $"AgentData - {d.CharaFileName} | {d.NowCoordinateFileName}");
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

            _mainWindowTitle = "Cheat Tools " + Assembly.GetExecutingAssembly().GetName().Version;
        }

        public bool Show
        {
            get => _show;
            set
            {
                _show = value;
                _editor.Show = value;

                if (value)
                    SetWindowSizes();

                _studioInstance = Studio.Studio.IsInstance() ? Studio.Studio.Instance : null;
                _soundInstance = Manager.Sound.Instance;
                _sceneInstance = Scene.Instance;
                _gameMgr = Game.IsInstance() ? Game.Instance : null;
                _resources = Resources.Instance;
                _map = Map.IsInstance() ? Map.Instance : null;
                _hScene = HSceneFlagCtrl.IsInstance() ? HSceneFlagCtrl.Instance : null;
            }
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            const int cheatWindowHeight = 410;
            _cheatWindowRect = new Rect(_screenRect.xMin, _screenRect.yMax - cheatWindowHeight, 270, cheatWindowHeight);
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

        private void CheatWindowContents(int id)
        {
            _cheatsScrollPos = GUILayout.BeginScrollView(_cheatsScrollPos);
            {
                DrawPlayerCheats();

                DrawHSceneCheats();
                
                //if (_hSprite != null)
                //{
                //    if (GUILayout.Button("Force quit H scene"))
                //        _hSprite.btnEnd.onClick.Invoke();
                //}

                DrawGirlCheatMenu();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Speed", GUILayout.ExpandWidth(false));
                    GUILayout.Label((int)Math.Round(Time.timeScale * 100) + "%", GUILayout.Width(35));
                    Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0, 5, GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                        Time.timeScale = 1;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label("Open in inspector");
                    foreach (var obj in new[]
                    {
                            new KeyValuePair<object, string>(_map?.AgentTable.Count > 0 ? _map.AgentTable.Values.Select(x => new ReadonlyCacheEntry(x.CharaName, x)) : null, "Heroine list"),
                            new KeyValuePair<object, string>(_map, "Manager.Map.Instance"),
                            new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                            new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                            new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                            new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                            new KeyValuePair<object, string>(EditorUtilities.GetRootGoScanner(), "Root Objects")
                        })
                    {
                        if (obj.Key == null) continue;
                        if (GUILayout.Button(obj.Value))
                        {
                            _editor.Inspector.Push(new InstanceStackEntry(obj.Key, obj.Value), true);
                        }
                    }
                }
                GUILayout.EndVertical();

                if (GUILayout.Button("Clear AssetBundle Cache"))
                {
                    foreach (var pair in AssetBundleManager.ManifestBundlePack)
                    {
                        foreach (var bundle in new Dictionary<string, LoadedAssetBundle>(pair.Value.LoadedAssetBundles))
                            AssetBundleManager.UnloadAssetBundle(bundle.Key, true, pair.Key);
                    }
                }
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void DrawPlayerCheats()
        {
            if (_map?.Player == null) return;
            var playerData = _map.Player.PlayerData;
            if (playerData == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Player stats");

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Fishing skill lvl: " + playerData.FishingSkill.Level, GUILayout.Width(150));
                    if (GUILayout.Button("+500 exp")) playerData.FishingSkill.AddExperience(500);
                }
                GUILayout.EndHorizontal();

                var mp = _resources?.MerchantProfile;
                if (mp != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var shanLvl = mp.SpendMoneyBorder.Count(x => playerData.SpendMoney >= x) + 1;
                        GUILayout.Label("Shan heart lvl: " + shanLvl, GUILayout.Width(150));
                        if (GUILayout.Button("1")) playerData.SpendMoney = 0;
                        if (GUILayout.Button("2")) playerData.SpendMoney = mp.SpendMoneyBorder[0];
                        if (GUILayout.Button("3")) playerData.SpendMoney = mp.SpendMoneyBorder[1];
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(6);

                var dp = _resources?.DefinePack;
                if (dp != null)
                {
                    if (dp.MapDefines.ItemSlotMax >= 99999 && playerData.InventorySlotMax >= 99999)
                        GUI.enabled = false;
                    if (GUILayout.Button("Unlimited inventory slots"))
                    {
                        var tr = Traverse.Create(dp.MapDefines);
                        tr.Field("_itemSlotMax").SetValue(99999);
                        tr.Field("_itemStackUpperLimit").SetValue(99999);
                        playerData.InventorySlotMax = 99999;
                    }
                    GUI.enabled = true;

                    if (playerData.ItemList.Count > 0 && GUILayout.Button("Clear player inventory"))
                    {
                        playerData.ItemList.Clear();
                        //MapUIContainer.AddNotify();
                        CheatToolsPlugin.Logger.LogMessage("Your inventory has been cleared.");
                    }

                    if (GUILayout.Button("Get all items (clears old)") && _resources?.GameInfo != null)
                    {
                        playerData.ItemList.Clear();

                        foreach (var category in _resources.GameInfo.GetItemCategories())
                            foreach (var stuffItemInfo in _resources.GameInfo.GetItemTable(category).Values)
                                playerData.ItemList.Add(new StuffItem(stuffItemInfo.CategoryID, stuffItemInfo.ID, 999));

                        CheatToolsPlugin.Logger.LogMessage("999 of all items have been added to your inventory");
                    }
                }

                FishingHackHooks.Enabled = GUILayout.Toggle(FishingHackHooks.Enabled, "Enable instant fishing");
                UnlockCraftingHooks.Enabled = GUILayout.Toggle(UnlockCraftingHooks.Enabled, "Enable free crafting");
                CheatToolsPlugin.NoclipMode = GUILayout.Toggle(CheatToolsPlugin.NoclipMode, "Enable player noclip");

                GUILayout.Space(6);

                if (GUILayout.Button("Navigate to Player's GameObject"))
                {
                    if (_map.Player.transform != null)
                        _editor.TreeViewer.SelectAndShowObject(_map.Player.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message,
                            "Player has no body assigned");
                }

                if (GUILayout.Button("Open Player in inspector"))
                    _editor.Inspector.Push(new InstanceStackEntry(_map.Player, "Player"), true);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawHSceneCheats()
        {
            if (_hScene == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("H scene controls");
        
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Male Gauge: " + _hScene.feel_m.ToString("F2"), GUILayout.Width(150));
                    _hScene.feel_m = GUILayout.HorizontalSlider(_hScene.feel_m, 0, 1);
                }
                GUILayout.EndHorizontal();
        
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Female Gauge: " + _hScene.feel_f.ToString("F2"), GUILayout.Width(150));
                    _hScene.feel_f = GUILayout.HorizontalSlider(_hScene.feel_f, 0, 1);
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Open HScene Flags in inspector"))
                    _editor.Inspector.Push(new InstanceStackEntry(_hScene, "HSceneFlagCtrl"), true);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawGirlCheatMenu()
        {
            if (_map == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Girl stats");

                var visibleGirls = _map.AgentTable.Values;

                foreach (var girl in visibleGirls)
                {
                    if (GUILayout.Button($"Select #{girl.ID} - {girl.CharaName ?? girl.name}"))
                        _currentVisibleGirl = girl;
                }

                GUILayout.Space(6);

                if (_currentVisibleGirl != null)
                    DrawSingleGirlCheats(_currentVisibleGirl);
                else
                    GUILayout.Label("Select a girl to access her stats");
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
        }

        private void DrawSingleGirlCheats(AgentActor currentAdvGirl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label("Selected girl name: " + (currentAdvGirl.CharaName ?? currentAdvGirl.name));
                GUILayout.Space(6);

                if (currentAdvGirl.ChaControl != null && currentAdvGirl.ChaControl.fileGameInfo != null)
                {
                    GUILayout.Label("Status");

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label($"Love phase: {currentAdvGirl.ChaControl.fileGameInfo.phase + 1} / 4");
                        if (GUILayout.Button("-1")) currentAdvGirl.SetPhase(Mathf.Max(0, currentAdvGirl.ChaControl.fileGameInfo.phase - 1));
                        if (GUILayout.Button("+1")) currentAdvGirl.SetPhase(Mathf.Min(3, currentAdvGirl.ChaControl.fileGameInfo.phase + 1));
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        var sickness = AIProject.Definitions.Sickness.TagTable.FirstOrDefault(x => x.Value == currentAdvGirl.AgentData.SickState.ID).Key;
                        GUILayout.Label($"Sickness: {sickness ?? "None"}", GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Heal", GUILayout.ExpandWidth(false)) && currentAdvGirl.AgentData.SickState.ID > -1)
                        {
                            currentAdvGirl.HealSickBySleep();
                            currentAdvGirl.AgentData.SickState.OverwritableID = -1;
                            currentAdvGirl.AgentData.WeaknessMotivation = 0;
                        }
                    }
                    GUILayout.EndHorizontal();

                    foreach (Status.Type statusValue in Enum.GetValues(typeof(Status.Type)))
                    {
                        if (currentAdvGirl.AgentData.StatsTable.ContainsKey((int)statusValue))
                        {
                            GUILayout.BeginHorizontal();
                            {
                                var status = Mathf.RoundToInt(currentAdvGirl.AgentData.StatsTable[(int)statusValue]);
                                GUILayout.Label(statusValue + ": " + status, GUILayout.Width(120));
                                var newStatus = Mathf.RoundToInt(GUILayout.HorizontalSlider(status, 0, (int)statusValue == 5 ? 150 : 100));
                                if (newStatus != status)
                                    currentAdvGirl.AgentData.StatsTable[(int)statusValue] = newStatus;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.Space(6);

                    GUILayout.Label("Flavor skills");
                    foreach (FlavorSkill.Type typeValue in Enum.GetValues(typeof(FlavorSkill.Type)))
                    {
                        if (currentAdvGirl.ChaControl.fileGameInfo.flavorState.ContainsKey((int)typeValue))
                        {
                            GUILayout.BeginHorizontal();
                            {
                                var flavorSkill = currentAdvGirl.GetFlavorSkill(typeValue);
                                GUILayout.Label(typeValue + ": " + flavorSkill, GUILayout.Width(120));
                                GUI.changed = false;
                                var textField = GUILayout.TextField(flavorSkill.ToString());
                                if (GUI.changed && int.TryParse(textField, out var newSkill) && newSkill != flavorSkill)
                                    currentAdvGirl.SetFlavorSkill(typeValue, newSkill);
                                GUI.changed = false;
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.Space(6);
                }

                if (GUILayout.Button("Reset talk time"))
                {
                    currentAdvGirl.AgentData.TalkMotivation = currentAdvGirl.AgentData.StatsTable[5];
                    currentAdvGirl.AgentData.WeaknessMotivation = 0;
                }

                GUILayout.Space(6);

                if (GUILayout.Button("Navigate to Actor's GameObject"))
                {
                    if (currentAdvGirl.transform != null)
                        _editor.TreeViewer.SelectAndShowObject(currentAdvGirl.transform);
                    else
                        CheatToolsPlugin.Logger.Log(BepInEx.Logging.LogLevel.Warning | BepInEx.Logging.LogLevel.Message, "Actor has no body assigned");
                }

                if (GUILayout.Button("Open Actor in inspector"))
                    _editor.Inspector.Push(new InstanceStackEntry(currentAdvGirl, "Actor " + currentAdvGirl.CharaName), true);
            }
            GUILayout.EndVertical();
        }
    }
}
