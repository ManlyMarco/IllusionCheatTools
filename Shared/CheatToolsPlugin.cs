#if !HC && !SVS
using System;
using BepInEx;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using Shared;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin(GUID, DisplayName, Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, RuntimeUnityEditorCore.Version)]
#if !KKLB
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
#endif
    public sealed class CheatToolsPlugin : BaseUnityPlugin
    {
        public const string DisplayName = Metadata.DisplayName;
        public const string GUID = Metadata.GUID;
        public const string Version = Metadata.Version;

        internal static new ManualLogSource Logger;

        private bool _initialized;

        private void Awake()
        {
            Logger = base.Logger;

            try
            {
                CheatToolsWindowInit.Initialize(this);
                _initialized = true;
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to initialize: " + e);
                enabled = false;
            }
        }

        private void Start()
        {
            if(!_initialized) return;

            var runtimeUnityEditorCore = RuntimeUnityEditorCore.Instance;
            if (runtimeUnityEditorCore == null)
            {
                Logger.Log(BepInEx.Logging.LogLevel.Error | BepInEx.Logging.LogLevel.Message, "Failed to get RuntimeUnityEditor! Make sure you don't have multiple versions of it installed!");
                enabled = false;
                return;
            }

            runtimeUnityEditorCore.AddFeature(new CheatToolsWindow(runtimeUnityEditorCore));
        }
    }
}
#endif
