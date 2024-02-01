using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using CheatTools;
using RuntimeUnityEditor.Core;
using Shared;
using UnityEngine;

namespace CheatTools;

[BepInPlugin(Metadata.GUID, "Cheat Tools", Version)]
[BepInDependency(RuntimeUnityEditorCore.GUID, RuntimeUnityEditorCore.Version)]
public class CheatToolsPlugin : BasePlugin
{
    public const string Version = Metadata.Version;

    internal static new ManualLogSource Logger;

    public CheatToolsPlugin()
    {
        Logger = base.Log;
    }

    public override void Load()
    {
        var runtimeUnityEditorCore = RuntimeUnityEditorCore.Instance;
        if (runtimeUnityEditorCore == null)
        {
            Logger.Log(LogLevel.Error | LogLevel.Message, "Failed to get RuntimeUnityEditor! Make sure you don't have multiple versions of it installed!");
            return;
        }

        CheatToolsWindowInit.InitializeCheats();

        runtimeUnityEditorCore.AddFeature(new CheatToolsWindow(runtimeUnityEditorCore));
    }
}