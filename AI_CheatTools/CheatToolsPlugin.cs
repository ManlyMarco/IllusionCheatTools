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
using Input = UnityEngine.Input;

namespace CheatTools
{
    [BepInPlugin(Metadata.GUID, "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, "2.0")]
    public class CheatToolsPlugin : BaseUnityPlugin
    {
        public const string Version = Metadata.Version;

        private CheatToolsWindow _cheatToolsWindow;
        private RuntimeUnityEditorCore _runtimeUnityEditorCore;

        private ConfigEntry<KeyboardShortcut> _showCheatWindow;
        private ConfigEntry<KeyboardShortcut> _noclip;
        internal static ConfigEntry<bool> BuildAnywhere;
        internal static ConfigEntry<bool> BuildOverlap;

        internal static new ManualLogSource Logger;

        private IEnumerator Start()
        {
            Logger = base.Logger;
            _showCheatWindow = Config.Bind("Hotkeys", "Toggle cheat window", new KeyboardShortcut(KeyCode.Pause));
            _noclip = Config.Bind("Hotkeys", "Toggle player noclip", KeyboardShortcut.Empty);

            BuildAnywhere = Config.Bind("Cheats", "Allow building anywhere", false);
            BuildAnywhere.SettingChanged += (sender, args) => BuildAnywhereHooks.Enabled = BuildAnywhere.Value;
            BuildAnywhereHooks.Enabled = BuildAnywhere.Value;

            BuildOverlap = Config.Bind("Cheats", "Allow building overlap", false);
            BuildOverlap.SettingChanged += (sender, args) => BuildOverlapHooks.Enabled = BuildOverlap.Value;
            BuildOverlapHooks.Enabled = BuildOverlap.Value;

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
            _cheatToolsWindow?.DisplayCheatWindow();
        }

        private void Update()
        {
            if (_showCheatWindow.Value.IsDown())
            {
                if (_cheatToolsWindow == null)
                    _cheatToolsWindow = new CheatToolsWindow(_runtimeUnityEditorCore);

                _cheatToolsWindow.Show = !_cheatToolsWindow.Show;
            }
            else if (_noclip.Value.IsDown())
            {
                NoclipMode = !NoclipMode;
            }

            if (NoclipMode)
            {
                if (Map.IsInstance())
                {
                    var player = Map.Instance.Player;
                    if (player != null)
                    {
                        var playerTransform = player.Controller;
                        if (playerTransform != null && playerTransform.GetComponent<NavMeshAgent>()?.enabled == false)
                        {
                            RunNoclip(playerTransform.transform);
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
                    if (Map.IsInstance() && Map.Instance.Player != null && Map.Instance.Player.Controller != null)
                    {
                        var navMeshAgent = Map.Instance.Player.Controller.GetComponent<NavMeshAgent>();
                        if (navMeshAgent != null)
                        {
                            navMeshAgent.enabled = !value;
                            _noclipMode = value;
                            return;
                        }
                        else
                        {
                            Logger.LogWarning("No NavMeshAgent found!");
                        }
                    }

                    _noclipMode = false;
                }
            }
        }

        private static void RunNoclip(Transform playerTransform)
        {
            var x = Manager.Input.Instance.GetAxisRaw(ActionID.MoveHorizontal);
            var y = Manager.Input.Instance.GetAxisRaw(ActionID.MoveVertical);
            if (x != 0 || y != 0)
            {
                var moveSpeed = Input.GetKey(KeyCode.LeftShift) ? 5f : 0.5f;
                playerTransform.Translate(moveSpeed * new Vector3(x, 0, y), Camera.main.transform);
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
