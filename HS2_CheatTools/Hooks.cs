using System;
using System.Diagnostics;
using System.Timers;
using AIChara;
using BepInEx;
using GameLoadCharaFileSystem;
using HarmonyLib;
using HS2;
using Manager;

namespace CheatTools
{
    public static partial class CheatToolsWindowInit
    {
        private static class Hooks
        {
            private static Timer _cardSaveTimer;
            private static int _lastEntryNo;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(LobbyParameterUI), nameof(LobbyParameterUI.SetParameter), typeof(ChaFileControl), typeof(int), typeof(int))]
            public static void CharSelected(int _entryNo)
            {
                _cardSaveTimer?.Stop();
                var lsmInstance = Singleton<LobbySceneManager>.Instance;
                if (lsmInstance == null)
                {
                    _currentVisibleGirl = null;
                    _onGirlStatsChanged = null;
                }
                else
                {
                    _lastEntryNo = _entryNo;
                    var heroine = lsmInstance.heroines[_entryNo];
                    _currentVisibleGirl = heroine;
                    _onGirlStatsChanged = h =>
                    {
                        // todo rate limiting
                        if (h == heroine)
                        {
                            if (_cardSaveTimer == null)
                            {
                                _cardSaveTimer = new Timer(4000);
                                _cardSaveTimer.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                                _cardSaveTimer.Elapsed += (sender, args) => ApplyParameters(_lastEntryNo);
                            }

                            _cardSaveTimer.Start();
                        }
                        else
                        {
                            CheatToolsPlugin.Logger.LogWarning("wtf " + new StackTrace());
                        }
                    };
                }
            }

            private static void ApplyParameters(int charaEntryNo)
            {
                var lsmInstance = Singleton<LobbySceneManager>.Instance;
                if (lsmInstance == null) return;
                var heroine = lsmInstance.heroines[charaEntryNo];
                if (heroine == null) return;
                GlobalHS2Calc.CalcState(heroine.chaFile.gameinfo2, heroine.personality);
                heroine.chaFile.SaveCharaFile(heroine.chaFile.charaFileName, byte.MaxValue, false);
                lsmInstance.ParameterUI.SetParameter(heroine.chaFile, -1, charaEntryNo);
                //todo have as an extra button?
                lsmInstance.SetCharaAnimationAndPosition();
                var scrollCtrl = lsmInstance.SelectUI.scrollCtrl;
                if (scrollCtrl.selectInfo != null)
                {
                    scrollCtrl.selectInfo.info.state = heroine.chaFile.gameinfo2.nowState;
                    scrollCtrl.RefreshShown();
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(LobbyParameterUI), nameof(LobbyParameterUI.SetParameter), typeof(GameCharaFileInfo), typeof(int), typeof(int))]
            public static void CharSelected2(int _entryNo)
            {
                CharSelected(_entryNo);
            }
        }
    }
}
