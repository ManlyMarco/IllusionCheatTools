using BepInEx;

namespace CheatTools
{
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            CheatToolsWindowInit.InitializeCheats();
        }
    }
}
