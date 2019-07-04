using System.ComponentModel;
using BepInEx;
using RuntimeUnityEditor.Bepin4;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditor.Core.RuntimeUnityEditorCore.GUID)]
    public class CheatTools : BaseUnityPlugin
    {
        public const string Version = "2.4";

        private CheatWindow _cheatWindow;

        [DisplayName("Show trainer and debug windows")]
        public SavedKeyboardShortcut ShowCheatWindow { get; }

        public CheatTools()
        {
            ShowCheatWindow = new SavedKeyboardShortcut(nameof(ShowCheatWindow), this, new KeyboardShortcut(KeyCode.Pause));
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
            if (ShowCheatWindow.IsDown())
            {
                if (_cheatWindow == null)
                    _cheatWindow = new CheatWindow(RuntimeUnityEditor4.Instance);

                _cheatWindow.Show = !_cheatWindow.Show;
            }
        }
    }
}
