using System.Collections;
using System.ComponentModel;
using BepInEx;
using Harmony;
using RuntimeUnityEditor.Core;
using UnityEngine;
using Logger = BepInEx.Logger;
using LogLevel = BepInEx.Logging.LogLevel;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID)]
    public class CheatTools : BaseUnityPlugin
    {
        public const string Version = "2.6";

        private CheatWindow _cheatWindow;
        private RuntimeUnityEditorCore _runtimeUnityEditorCore;

        [DisplayName("Show trainer and debug windows")]
        public SavedKeyboardShortcut ShowCheatWindow { get; }

        public CheatTools()
        {
            ShowCheatWindow = new SavedKeyboardShortcut(nameof(ShowCheatWindow), this, new KeyboardShortcut(KeyCode.Pause));
        }

        private IEnumerator Start()
        {
            // Wait for runtime editor to init
            yield return null;

            _runtimeUnityEditorCore = (RuntimeUnityEditorCore)AccessTools.Property(typeof(RuntimeUnityEditorCore), "Instance").GetValue(null, null);

            if (_runtimeUnityEditorCore == null)
            {
                Logger.Log(LogLevel.Error, "[CheatTools] Could not get the instance of RuntimeUnityEditorCore, aborting");
                enabled = false;
                yield break;
            }

            // Disable the default hotkey since we'll be controlling the show state manually
            _runtimeUnityEditorCore.ShowHotkey = KeyCode.None;
        }

        protected void OnGUI()
        {
            _cheatWindow?.DisplayCheatWindow();
        }

        protected void Update()
        {
            if (ShowCheatWindow.IsDown())
            {
                if (_cheatWindow == null)
                    _cheatWindow = new CheatWindow(_runtimeUnityEditorCore);

                _cheatWindow.Show = !_cheatWindow.Show;
            }
        }
    }
}
