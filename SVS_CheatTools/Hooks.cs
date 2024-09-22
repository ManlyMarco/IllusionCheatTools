using System;
using HarmonyLib;
using Pathfinding;

namespace CheatTools;

internal static class Hooks
{
    #region No interruptions

    public static bool InterruptBlock;
    public static bool InterruptBlockAllow3P = true;
    public static bool InterruptBlockAllowNonPlayer = true;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SV.ReactionManager), nameof(SV.ReactionManager.CheckIntervention))]
    private static void ReactionManager_CheckIntervention_Postfix(SV.ReactionManager __instance,
                                                                  SV.Chara.AI ai, SV.Chara.AI aiTarg1, SV.Chara.AI aiTarg2,
                                                                  SV.SimulationDefine.CommandNo setCommandNo,
                                                                  ref bool risSetAction)
    {
        if (!InterruptBlock) return;

        if (InterruptBlockAllow3P && (setCommandNo == SV.SimulationDefine.CommandNo.LetsHave3P || setCommandNo == SV.SimulationDefine.CommandNo.WantA3P)) return;

        if (InterruptBlockAllowNonPlayer)
        {
            var includesPc = ai._charaData.IsPC || aiTarg1?._charaData.IsPC == true || aiTarg2?._charaData.IsPC == true;
            if (!includesPc) return;
        }

#if DEBUG
        Console.WriteLine($"ai={ai._charaData.Name} aiTarg1={aiTarg1._charaData.Name} aiTarg2={aiTarg2._charaData.Name} setCommandNo={setCommandNo} risSetAction={risSetAction}");
#endif
        // Cancel the intervention
        ai._charaData.charasGameParam.commandNo = (int)SV.SimulationDefine.CommandNo.None;
        risSetAction = false;
    }

    #endregion

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
    [HarmonyPatch(typeof(Il2CppSystem.Random), nameof(Il2CppSystem.Random.Next), typeof(int))]
    [HarmonyPatch(typeof(Il2CppSystem.Random), nameof(Il2CppSystem.Random.Next), typeof(int), typeof(int))]
    private static void Random_Next_Override(int maxValue, ref int __result)
    {
        if (RiggedRng) __result = maxValue - 1;
    }

    #endregion
}
