using AIProject.MiniGames.Fishing;
using BepInEx.Harmony;
using HarmonyLib;

namespace CheatTools
{
    public partial class CheatToolsWindow
    {
        /// <summary>
        /// Based on a cheat script by ghorsington
        /// </summary>
        private static class FishingHackHooks
        {
            private static Harmony _hInstance;

            public static bool Enabled
            {
                get => _hInstance != null;
                set
                {
                    if (value != Enabled)
                    {
                        if (value)
                            _hInstance = HarmonyWrapper.PatchAll(typeof(FishingHackHooks));
                        else
                        {
                            _hInstance.UnpatchAll(_hInstance.Id);
                            _hInstance = null;
                        }
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(FishingManager), "CheckArrowInCircle")]
            public static bool CheckPossible(FishingManager __instance, ref float ___fishHeartPoint)
            {
                ___fishHeartPoint = 0f;
                __instance.scene = FishingManager.FishingScene.Success;
                AccessTools.Method(typeof(FishingManager), "SceneToSuccess").Invoke(__instance, null);
                return false;
            }
        }
    }
}