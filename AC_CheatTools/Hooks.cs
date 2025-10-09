using System;
using AC.Scene.Explore;
using HarmonyLib;
using ILLGAMES.Unity;

namespace CheatTools;

internal static class Hooks
{
    #region Speedhack

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.UpdateLocomotionSpeed))]
    private static void MovementSpeedOverride(Actor __instance)
    {
        if (SpeedMode == SpeedModes.Normal || !__instance.Transform) return;

        switch (SpeedMode)
        {
            case SpeedModes.ReturnToNormal:
                __instance.Agent.speed = 3;
                __instance.Agent.acceleration = 12;
                SpeedMode = SpeedModes.Normal;
                break;

            case SpeedModes.Fast:
                __instance.Agent.speed = 7;
                __instance.Agent.acceleration = 20;
                break;
            case SpeedModes.Sanic:
                __instance.Agent.speed = 100;
                __instance.Agent.acceleration = 400;
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
    [HarmonyPatch(typeof(Utils.ProbabilityCalclator), nameof(Utils.ProbabilityCalclator.DetectFromPercent), typeof(int))]
    private static void ProbabilityCalculation_Detect_Override(ref bool __result)
    {
        if (RiggedRng) __result = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Il2CppSystem.Random), nameof(Il2CppSystem.Random.Next), typeof(int))]
    [HarmonyPatch(typeof(Il2CppSystem.Random), nameof(Il2CppSystem.Random.Next), typeof(int), typeof(int))]
    private static void Random_Next_Override(int maxValue, ref int __result)
    {
        if (RiggedRng) __result = maxValue - 1;
    }

    #endregion
}
