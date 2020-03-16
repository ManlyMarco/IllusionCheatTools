using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using UnityEngine;
using UnityEngine.AI;
using LogLevel = BepInEx.Logging.LogLevel;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, "2.0")]
    public class CheatTools : BaseUnityPlugin
    {
        public const string Version = "2.7";

        private CheatWindow _cheatWindow;
        private RuntimeUnityEditorCore _runtimeUnityEditorCore;

        private ConfigEntry<KeyboardShortcut> _showCheatWindow;
        private ConfigEntry<KeyboardShortcut> _noclip;

        internal static new ManualLogSource Logger;

        private static bool _noclipMode;
        internal static bool NoclipMode
        {
            get => _noclipMode;
            set
            {
                if (!Manager.Game.IsInstance())
                {
                    _noclipMode = false;
                }
                else if (_noclipMode != value)
                {
                    Manager.Game.Instance.Player.transform.GetComponent<NavMeshAgent>().enabled = !value;
                    _noclipMode = value;
                }
            }
        }

        private IEnumerator Start()
        {
            Logger = base.Logger;
            _showCheatWindow = Config.Bind("Hotkeys", "Toggle cheat window", new KeyboardShortcut(KeyCode.Pause));
            _noclip = Config.Bind("Hotkeys", "Toggle player noclip", KeyboardShortcut.Empty);

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

        private void OnGUI()
        {
            _cheatWindow?.DisplayCheatWindow();
        }

        private void Update()
        {
            if (_showCheatWindow.Value.IsDown())
            {
                if (_cheatWindow == null)
                    _cheatWindow = new CheatWindow(_runtimeUnityEditorCore);

                _cheatWindow.Show = !_cheatWindow.Show;
            }
            else if (_noclip.Value.IsDown())
            {
                NoclipMode = !NoclipMode;
            }

            if (NoclipMode)
            {
                if (!Manager.Game.IsInstance())
                {
                    NoclipMode = false;
                    return;
                }

                var _gameMgr = Manager.Game.Instance;
                if (_gameMgr.Player == null || _gameMgr.Player.transform == null ||
                _gameMgr.Player.transform.GetComponent<NavMeshAgent>()?.enabled != false)
                {
                    NoclipMode = false;
                    return;
                }

                if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
                {
                    float moveSpeed = Input.GetKey(KeyCode.LeftShift) ? 0.5f : 0.05f;
                    _gameMgr.Player.transform.Translate(moveSpeed * new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")), Camera.main.transform);
                }

                if (Input.GetAxis("Mouse ScrollWheel") != 0)
                {
                    float scrollSpeed = Input.GetKey(KeyCode.LeftShift) ? 10f : 1f;
                    _gameMgr.Player.transform.position += scrollSpeed * new Vector3(0, -Input.GetAxis("Mouse ScrollWheel"), 0);
                }


                Vector3 eulerAngles = _gameMgr.Player.transform.rotation.eulerAngles;
                eulerAngles.y = Camera.main.transform.rotation.eulerAngles.y;
                _gameMgr.Player.transform.rotation = Quaternion.Euler(eulerAngles);
            }
        }
    }
}
