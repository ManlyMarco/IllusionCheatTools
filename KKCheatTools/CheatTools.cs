using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RuntimeUnityEditor.Bepin4;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditor.Core.RuntimeUnityEditorCore.GUID)]
    public class CheatTools : BaseUnityPlugin
    {
        public const string Version = "2.6";

        private CheatWindow _cheatWindow;

        public ConfigEntry<KeyboardShortcut> ShowCheatWindow { get; }

        internal static new ManualLogSource Logger;

        public CheatTools()
        {
            Logger = base.Logger;

            ShowCheatWindow = Config.AddSetting("", "Show trainer window", new KeyboardShortcut(KeyCode.Pause));
        }

        private void Start()
        {
            // Disable the default hotkey since we'll be controlling the show state manually
            RuntimeUnityEditor4.Instance.ShowHotkey = KeyCode.None;
        }

        protected void OnGUI()
        {
            _cheatWindow?.DisplayCheatWindow();
        }

        protected void Update()
        {
            if (ShowCheatWindow.Value.IsDown())
            {
                if (_cheatWindow == null)
                    _cheatWindow = new CheatWindow(RuntimeUnityEditor4.Instance);

                _cheatWindow.Show = !_cheatWindow.Show;
            }
        }
    }
}
