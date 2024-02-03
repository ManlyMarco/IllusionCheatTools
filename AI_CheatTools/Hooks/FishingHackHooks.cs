using AIProject.MiniGames.Fishing;
using HarmonyLib;

namespace CheatTools
{
    /// <summary>
    /// Based on a cheat script by ghorsington
    /// </summary>
    internal static class FishingHackHooks
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
                    {
                        _hInstance = Harmony.CreateAndPatchAll(typeof(FishingHackHooks));
                    }
                    else
                    {
                        _hInstance.UnpatchSelf();
                        _hInstance = null;
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FishingManager), nameof(FishingManager.CheckArrowInCircle))]
        public static bool CheckPossible(FishingManager __instance, ref float ___fishHeartPoint)
        {
            ___fishHeartPoint = 0f;
            __instance.scene = FishingManager.FishingScene.Success;
            AccessTools.Method(typeof(FishingManager), nameof(FishingManager.SceneToSuccess)).Invoke(__instance, null);
            return false;
        }
    }
}
