using AIChara;
using AIProject;
using AIProject.SaveData;
using BepInEx;
using BepInEx.Configuration;
using Manager;
using RuntimeUnityEditor.Core.Inspector;
using UnityEngine.AI;

namespace CheatTools
{
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> BuildAnywhere;
        internal static ConfigEntry<bool> BuildOverlap;

        private void Awake()
        {
            BuildAnywhere = Config.Bind("Cheats", "Allow building anywhere", false);
            BuildAnywhere.SettingChanged += (sender, args) => BuildAnywhereHooks.Enabled = BuildAnywhere.Value;
            BuildAnywhereHooks.Enabled = BuildAnywhere.Value;

            BuildOverlap = Config.Bind("Cheats", "Allow building overlap", false);
            BuildOverlap.SettingChanged += (sender, args) => BuildOverlapHooks.Enabled = BuildOverlap.Value;
            BuildOverlapHooks.Enabled = BuildOverlap.Value;

            NoclipFeature.InitializeNoclip(this, () =>
            {
                if (!Map.IsInstance()) return null;
                if (Map.Instance.Player == null) return null;
                if (Map.Instance.Player.Controller == null) return null;
                return Map.Instance.Player.Controller.GetComponent<NavMeshAgent>();
            });

            ToStringConverter.AddConverter<AgentActor>(heroine => !string.IsNullOrEmpty(heroine.CharaName) ? heroine.CharaName : heroine.name);
            ToStringConverter.AddConverter<AgentData>(d => $"AgentData - {d.CharaFileName} | {d.NowCoordinateFileName}");
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

            CheatToolsWindowInit.Initialize();
        }
    }
}
