using Actor;
using AIChara;
using BepInEx;
using RuntimeUnityEditor.Core.Inspector;

namespace CheatTools
{
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ToStringConverter.AddConverter<Heroine>(CheatToolsWindowInit.GetHeroineName);
            ToStringConverter.AddConverter<ChaFile>(d => $"ChaFile - {d.charaFileName ?? "Unknown"} ({d.parameter?.fullname ?? "Unknown"})");
            ToStringConverter.AddConverter<ChaControl>(d => $"{d} - {d.chaFile?.parameter?.fullname ?? d.chaFile?.charaFileName ?? "Unknown"}");

            CheatToolsWindowInit.InitializeCheats();
        }
    }
}
