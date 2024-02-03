using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using RuntimeUnityEditor.Core;
using Shared;

namespace CheatTools
{
    [BepInPlugin(GUID, DisplayName, Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, RuntimeUnityEditorCore.Version)]
    [BepInProcess("HoneyCome")]
    public class CheatToolsPlugin : BasePlugin
    {
        public const string DisplayName = Metadata.DisplayName;
        public const string GUID = Metadata.GUID;
        public const string Version = Metadata.Version;

        internal static new ManualLogSource Logger;

        public CheatToolsPlugin()
        {
            Logger = base.Log;
        }

        public override void Load()
        {
            CheatToolsWindowInit.Initialize(this);

            var runtimeUnityEditorCore = RuntimeUnityEditorCore.Instance;
            if (runtimeUnityEditorCore == null)
            {
                Logger.Log(LogLevel.Error | LogLevel.Message, "Failed to get RuntimeUnityEditor! Make sure you don't have multiple versions of it installed!");
                return;
            }

            runtimeUnityEditorCore.AddFeature(new CheatToolsWindow(runtimeUnityEditorCore));
        }
    }
}