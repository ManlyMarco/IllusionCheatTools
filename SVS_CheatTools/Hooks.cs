using System;
using HarmonyLib;
using Pathfinding;
using Random = Il2CppSystem.Random;

namespace CheatTools;

internal static class Hooks
{
    #region Speedhack

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AIBase), nameof(AIBase.FixedUpdate))]
    private static void MovementSpeedOverride(AIBase __instance)
    {
        if (SpeedMode == SpeedModes.Normal || !__instance) return;

        var rai = __instance.TryCast<SV.SVRichAI>();
        if (rai == null) return;

        switch (SpeedMode)
        {
            case SpeedModes.ReturnToNormal:
                rai.accelType = 0;
                rai.slowSpeed = 1;
                rai.maxSpeed = 6;
                rai.acceleration = 6;
                SpeedMode = SpeedModes.Normal;
                break;

            case SpeedModes.Fast:
                rai.accelType = 1;
                rai.slowSpeed = 10;
                rai.maxSpeed = 10;
                rai.acceleration = 10;
                break;
            case SpeedModes.Sanic:
                rai.accelType = 1;
                rai.slowSpeed = 100;
                rai.maxSpeed = 100;
                rai.acceleration = 100;
                break;

            case SpeedModes.Normal:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public enum SpeedModes
    {
        ReturnToNormal = -1,
        Normal = 0,
        Fast,
        Sanic
    }

    public static SpeedModes SpeedMode;

    #endregion

    #region RNG manip

    public static bool RiggedRng;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ProbabilityCalculation), nameof(ProbabilityCalculation.Detect), typeof(int))]
    [HarmonyPatch(typeof(ProbabilityCalculation), nameof(ProbabilityCalculation.Detect), typeof(float))]
    private static void ProbabilityCalculation_Detect_Override(ref bool __result)
    {
        if (RiggedRng) __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Random), nameof(Random.Next), typeof(int))]
    [HarmonyPatch(typeof(Random), nameof(Random.Next), typeof(int), typeof(int))]
    private static void Random_Next_Override(int maxValue, ref int __result)
    {
        if (RiggedRng) __result = maxValue - 1;
    }

    #endregion
}
