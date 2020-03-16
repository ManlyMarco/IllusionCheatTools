using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using UnityEngine;
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

        private ConfigEntry<KeyboardShortcut> _showCheatWindow;

        internal static new ManualLogSource Logger;

        private IEnumerator Start()
        {
            Logger = base.Logger;
            _showCheatWindow = Config.Bind("General", "Open cheat window", new KeyboardShortcut(KeyCode.Pause));

            // Wait for runtime editor to init
            yield return null;

            _runtimeUnityEditorCore = (RuntimeUnityEditorCore)typeof(RuntimeUnityEditorCore).GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null, null);

            if (_runtimeUnityEditorCore == null)
            {
                Logger.Log(LogLevel.Error, "Could not get the instance of RuntimeUnityEditorCore, aborting");
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
            if (_showCheatWindow.Value.IsDown())
            {
                if (_cheatWindow == null)
                    _cheatWindow = new CheatWindow(_runtimeUnityEditorCore);

                _cheatWindow.Show = !_cheatWindow.Show;
            }

            if (_cheatWindow == null) return;
            _cheatWindow.Update();
        }
    }
}
