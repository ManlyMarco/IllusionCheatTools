using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Manager;
using RuntimeUnityEditor.Bepin5;
using RuntimeUnityEditor.Core;
using Shared;
using UnityEngine;
using UnityEngine.AI;
using LogLevel = BepInEx.Logging.LogLevel;

namespace CheatTools
{
    [BepInPlugin(Metadata.GUID, "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, "2.0")]
    [HelpURL("https://github.com/ManlyMarco/IllusionCheatTools")]
    public class CheatToolsPlugin : BaseUnityPlugin
    {
        public const string Version = Metadata.Version;

        private CheatToolsWindow _cheatWindow;
        private RuntimeUnityEditorCore _runtimeUnityEditorCore;

        private ConfigEntry<KeyboardShortcut> _showCheatWindow;
        private ConfigEntry<KeyboardShortcut> _noclip;

        internal static ConfigEntry<bool> UnlockAllPositions;
        internal static ConfigEntry<bool> UnlockAllPositionsIndiscriminately;

        internal static new ManualLogSource Logger;

        private IEnumerator Start()
        {
            Logger = base.Logger;
            _showCheatWindow = Config.Bind("Hotkeys", "Toggle cheat window", new KeyboardShortcut(KeyCode.Pause));
            _noclip = Config.Bind("Hotkeys", "Toggle player noclip", KeyboardShortcut.Empty);

            UnlockAllPositions = Config.Bind("Cheats", "Unlock all H positions", false, "Reload the H scene to see changes.");
            UnlockAllPositions.SettingChanged += (sender, args) => UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;
            UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;

            UnlockAllPositionsIndiscriminately = Config.Bind("Cheats", "Unlock invalid H positions as well", false, "This will unlock all positions even if they should not be possible.\nWARNING: Can result in bugs and even game crashes in some cases.\nReload the H scene to see changes.");
            UnlockAllPositionsIndiscriminately.SettingChanged += (sender, args) => UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;
            UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;

            // Wait for runtime editor to init
            yield return null;

            _runtimeUnityEditorCore = RuntimeUnityEditor5.Instance;

            if (_runtimeUnityEditorCore == null)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "Failed to get RuntimeUnityEditor! Make sure you don't have multiple versions of it installed!");
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
