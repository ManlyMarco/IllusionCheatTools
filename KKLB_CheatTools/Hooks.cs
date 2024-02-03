using HarmonyLib;
using sv08;
using UnityEngine;

namespace CheatTools
{
    public static partial class CheatToolsWindowInit
    {
        private static class Hooks
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(LocomotionTeleport), "AimCollisionTest")]
            private static void AimHook(ref Vector3 end)
            {
                if (_teleportUnlock)
                    end = Vector3.positiveInfinity;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(CameraPosTweak), nameof(CameraPosTweak.Update))]
            private static void PosTweakHook(CameraPosTweak __instance)
            {
                if (_posTweakForce)
                    __instance.canPosTweak = true;

                if (_posTweakUnlimited)
                {
                    __instance.sayuuNum = 0;
                    __instance.zengoNum = 0;
                }

                __instance.distance = _posTweakDistance;
            }
        }
    }
}
