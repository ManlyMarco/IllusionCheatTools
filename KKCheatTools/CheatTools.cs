using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Manager;
using RuntimeUnityEditor.Bepin5;
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

        private IEnumerator Start()
        {
            Logger = base.Logger;
            _showCheatWindow = Config.Bind("Hotkeys", "Toggle cheat window", new KeyboardShortcut(KeyCode.Pause));
            _noclip = Config.Bind("Hotkeys", "Toggle player noclip", KeyboardShortcut.Empty);

            // Wait for runtime editor to init
            yield return null;

            _runtimeUnityEditorCore = RuntimeUnityEditor5.Instance;

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
                if (Game.IsInstance())
                {
                    var player = Game.Instance.Player;
                    if (player != null)
                    {
                        var playerTransform = player.transform;
                        if (playerTransform != null && playerTransform.GetComponent<NavMeshAgent>()?.enabled == false)
                        {
                            RunNoclip(playerTransform);
                            return;
                        }
                    }
                }

                NoclipMode = false;
            }
        }

        private static bool _noclipMode;
        internal static bool NoclipMode
        {
            get => _noclipMode;
            set
            {
                if (_noclipMode != value)
                {
                    if (Game.IsInstance() && Game.Instance.Player != null && Game.Instance.Player.transform != null)
                    {
                        var navMeshAgent = Game.Instance.Player.transform.GetComponent<NavMeshAgent>();
                        if (navMeshAgent != null)
                        {
                            navMeshAgent.enabled = !value;
                            _noclipMode = value;
                            return;
                        }
                    }

                    _noclipMode = false;
                }
            }
        }

        private static void RunNoclip(Transform playerTransform)
        {
            if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
            {
                var moveSpeed = Input.GetKey(KeyCode.LeftShift) ? 0.5f : 0.05f;
                playerTransform.Translate(
                    moveSpeed * new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")),
                    Camera.main.transform);
            }

            if (Input.GetAxis("Mouse ScrollWheel") != 0)
            {
                var scrollSpeed = Input.GetKey(KeyCode.LeftShift) ? 10f : 1f;
                playerTransform.position += scrollSpeed * new Vector3(0, -Input.GetAxis("Mouse ScrollWheel"), 0);
            }


            var eulerAngles = playerTransform.rotation.eulerAngles;
            eulerAngles.y = Camera.main.transform.rotation.eulerAngles.y;
            playerTransform.rotation = Quaternion.Euler(eulerAngles);
        }
    }
}
