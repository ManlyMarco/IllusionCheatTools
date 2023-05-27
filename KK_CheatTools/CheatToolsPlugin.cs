using BepInEx;
using BepInEx.Configuration;
using Manager;
using RuntimeUnityEditor.Core.Inspector;
using UnityEngine.AI;

namespace CheatTools
{
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> UnlockAllPositions;
        internal static ConfigEntry<bool> UnlockAllPositionsIndiscriminately;

        private void Awake()
        {
            UnlockAllPositions = Config.Bind("Cheats", "Unlock all H positions", false, "Reload the H scene to see changes.");
            UnlockAllPositions.SettingChanged += (sender, args) => UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;
            UnlockPositionsHooks.Enabled = UnlockAllPositions.Value;

            UnlockAllPositionsIndiscriminately = Config.Bind("Cheats", "Unlock invalid H positions as well", false, "This will unlock all positions even if they should not be possible.\nWARNING: Can result in bugs and even game crashes in some cases.\nReload the H scene to see changes.");
            UnlockAllPositionsIndiscriminately.SettingChanged += (sender, args) => UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;
            UnlockPositionsHooks.UnlockAll = UnlockAllPositionsIndiscriminately.Value;

            ToStringConverter.AddConverter<SaveData.Heroine>(heroine => !string.IsNullOrEmpty(heroine.Name) ? heroine.Name : heroine.nickname);
            ToStringConverter.AddConverter<SaveData.CharaData.Params.Data>(d => $"[{d.key} | {d.value}]");

            NoclipFeature.InitializeNoclip(this, () =>
            {
                if (!Game.IsInstance()) return null;
                var player = Game.Instance.Player;
                if (player == null) return null;
                var playerTransform = player.transform;
                if (playerTransform == null) return null;
                return playerTransform.GetComponent<NavMeshAgent>();
            });

            CheatToolsWindowInit.InitializeCheats();
        }
    }
}
