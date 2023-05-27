using BepInEx;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using Shared;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin(Metadata.GUID, "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, RuntimeUnityEditorCore.Version)]
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        public const string Version = Metadata.Version;

        internal static new ManualLogSource Logger;

        public CheatToolsPlugin()
        {
            Logger = base.Logger;
        }

        private void Start()
        {
            var runtimeUnityEditorCore = RuntimeUnityEditorCore.Instance;
            if (runtimeUnityEditorCore == null)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error | BepInEx.Logging.LogLevel.Message, "Failed to get RuntimeUnityEditor! Make sure you don't have multiple versions of it installed!");
                enabled = false;
                return;
            }

            runtimeUnityEditorCore.AddFeature(new CheatToolsWindow(runtimeUnityEditorCore));

            if (runtimeUnityEditorCore.ShowHotkey == KeyCode.None)
            {
                // Previous versions of cheat tools set this to none, need to restore a sane value
                runtimeUnityEditorCore.ShowHotkey = KeyCode.Pause;
            }
        }
    }
}
