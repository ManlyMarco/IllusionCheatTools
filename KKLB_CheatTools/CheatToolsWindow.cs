using System;
using System.Collections.Generic;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using sv08;
using UnityEngine;

namespace CheatTools
{
    public static class CheatToolsWindowInit
    {
        private static KeyValuePair<object, string>[] _openInInspectorButtons;

        public static void InitializeCheats()
        {
            CheatToolsWindow.OnShown += _ =>
            {
                _openInInspectorButtons = new[]
                {
                  //new KeyValuePair<object, string>(_gameMgr != null && _gameMgr.HeroineList.Count > 0 ? (Func<object>)(() => _gameMgr.HeroineList.Select(x => new ReadonlyCacheEntry(x.ChaName, x))) : null, "Heroine //list"),
                  //new KeyValuePair<object, string>(_gameMgr, "Manager.Game.Instance"),
                  //new KeyValuePair<object, string>(_sceneInstance, "Manager.Scene.Instance"),
                  //new KeyValuePair<object, string>(_communicationInstance, "Manager.Communication.Instance"),
                  //new KeyValuePair<object, string>(_soundInstance, "Manager.Sound.Instance"),
                  //new KeyValuePair<object, string>(_hFlag, "HFlag"),
                  //new KeyValuePair<object, string>(_talkScene, "TalkScene"),
                  //new KeyValuePair<object, string>(_studioInstance, "Studio.Instance"),
                    new KeyValuePair<object, string>((Func<object>)(() => Game.Instance) , "Game.Instance"),
                    new KeyValuePair<object, string>((Func<object>)EditorUtilities.GetRootGoScanner, "Root Objects")
                };
            };

            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Game.Instance?.PlayerStatus != null, DrawPlayerUnlocks, null));
            CheatToolsWindow.Cheats.Add(new CheatEntry(w => Game.Instance?.GameStatus != null, DrawGlobalUnlocks, null));

            CheatToolsWindow.Cheats.Add(CheatEntry.CreateOpenInInspectorButtons(() => _openInInspectorButtons));

        }

        private static void DrawPlayerUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label("Player unlocks (current playthrough)");

            var playerStatus = Game.Instance.PlayerStatus;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Sakurako Favor: " + playerStatus.favor_Sakurako, GUILayout.Width(120));
                playerStatus.favor_Sakurako = (int)GUILayout.HorizontalSlider(playerStatus.favor_Sakurako, 0, 100);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Umeko Favor: " + playerStatus.favor_Umeko, GUILayout.Width(120));
                playerStatus.favor_Sakurako = (int)GUILayout.HorizontalSlider(playerStatus.favor_Sakurako, 0, 100);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Open advanced options"))
            {
                Inspector.Instance.Push(new InstanceStackEntry(playerStatus, "PlayerStatus"), true);
            }
        }

        private static void DrawGlobalUnlocks(CheatToolsWindow window)
        {
            GUILayout.Label("Global unlocks (might need a game restart, permanent)");

            var gameStatus = Game.Instance.GameStatus;

            if (GUILayout.Button("Unlock all chapters"))
            {
                var chapterClear = gameStatus.Chapter_Clear;
                for (int i = 0; i < chapterClear.Length; i++)
                    chapterClear[i] = true;
            }

            if (GUILayout.Button("Unlock all clothes"))
            {
                var partsUnlock = gameStatus.Parts_Unlock;
                var partsNew = gameStatus.Parts_New;
                for (int i = 0; i < partsUnlock.Length; i++)
                {
                    partsNew[i] = !partsUnlock[i];
                    partsUnlock[i] = true;
                }
            }
            if (GUILayout.Button("Mark all endings and games as seen"))
            {
                void EnsureNonzeroCount(List<int> list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == 0)
                            list[i] = 1;
                    }
                }
                EnsureNonzeroCount(gameStatus.listEndingCount);
                EnsureNonzeroCount(gameStatus.listMiniGamePlayCount);
                EnsureNonzeroCount(gameStatus.listMiniGameClearCount);

                gameStatus.SetSystemFlag(ID_SFlag.SFlag_End, true);
            }
        }
    }
}
