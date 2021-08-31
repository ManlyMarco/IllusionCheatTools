using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RuntimeUnityEditor.Bepin5;
using RuntimeUnityEditor.Core;
using Shared;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin(Metadata.GUID, "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, "2.0")]
    [HelpURL("https://github.com/ManlyMarco/IllusionCheatTools")]
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        public const string Version = Metadata.Version;

        private CheatToolsWindow _cheatWindow;
        private RuntimeUnityEditorCore _runtimeUnityEditorCore;

        private ConfigEntry<KeyboardShortcut> _showCheatWindow;

        internal static new ManualLogSource Logger;

        private IEnumerator Start()
        {
            Logger = base.Logger;
            _showCheatWindow = Config.Bind("Hotkeys", "Toggle cheat window", new KeyboardShortcut(KeyCode.Pause));
            
            // Wait for runtime editor to init
            yield return null;

            _runtimeUnityEditorCore = RuntimeUnityEditor5.Instance;

            if (_runtimeUnityEditorCore == null)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error | BepInEx.Logging.LogLevel.Message, "Failed to get RuntimeUnityEditor! Make sure you don't have multiple versions of it installed!");
                enabled = false;
                yield break;
            }

            // Disable the default hotkey since we'll be controlling the show state manually
            _runtimeUnityEditorCore.ShowHotkey = KeyCode.None;
        }

        private void OnGUI()
        {
            _cheatWindow?.DisplayCheatWindow();
        }

        private void Update()
        {
            if (_showCheatWindow.Value.IsDown())
            {
                if (_cheatWindow == null)
                    _cheatWindow = new CheatToolsWindow(_runtimeUnityEditorCore);

                _cheatWindow.Show = !_cheatWindow.Show;
            }
        }
    }
}
