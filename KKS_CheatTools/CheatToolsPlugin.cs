using BepInEx;
using BepInEx.Configuration;
using Manager;
using UnityEngine.AI;

namespace CheatTools
{
    public partial class CheatToolsPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            NoclipFeature.InitializeNoclip(this, () =>
            {
                if (Game.Player == null) return null;
                if (Game.Player.transform == null) return null;
                return Game.Player.transform.GetComponent<NavMeshAgent>();
            });

            CheatToolsWindowInit.InitializeCheats();
        }
    }
}
