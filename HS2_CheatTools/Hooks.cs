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
    internal static class Hooks
    {
        private static Timer _cardSaveTimer;
        private static int _lastEntryNo;

        [HarmonyPatch(typeof(LobbyParameterUI), "SetParameter", typeof(ChaFileControl), typeof(int), typeof(int))]
        [HarmonyPostfix]
        public static void CharSelected(int _entryNo)
        {
            _cardSaveTimer?.Stop();
            var lsmInstance = Singleton<LobbySceneManager>.Instance;
            if (lsmInstance == null)
            {
                CheatToolsWindow._currentVisibleGirl = null;
                CheatToolsWindow._onGirlStatsChanged = null;
            }
            else
            {
                _lastEntryNo = _entryNo;
                var heroine = lsmInstance.heroines[_entryNo];
                CheatToolsWindow._currentVisibleGirl = heroine;
                CheatToolsWindow._onGirlStatsChanged = h =>
                {
                    if (lsmInstance == null)
                        CheatToolsWindow._onGirlStatsChanged = null;

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
                        Console.WriteLine("wtf " + new StackTrace());
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

        [HarmonyPatch(typeof(LobbyParameterUI), "SetParameter", typeof(GameCharaFileInfo), typeof(int), typeof(int))]
        [HarmonyPostfix]
        public static void CharSelected2(int _entryNo)
        {
            CharSelected(_entryNo);
        }
    }
}