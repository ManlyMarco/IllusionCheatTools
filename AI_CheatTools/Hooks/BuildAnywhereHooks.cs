using AIProject;
using HarmonyLib;
using Housing;
using UnityEngine;

namespace CheatTools
{
    /// <summary>
    /// Based on a cheat script by unknown
    /// </summary>
    internal static class BuildAnywhereHooks
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
                        _hInstance = Harmony.CreateAndPatchAll(typeof(BuildAnywhereHooks));
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
        [HarmonyPatch(typeof(GuideManager), nameof(GuideManager.GridArea), MethodType.Getter)]
        public static bool GetGridArea(ref Vector3 __result)
        {
            __result = new Vector3(15000f, 15000f, 15000f);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(VirtualCameraController), nameof(VirtualCameraController.Update))]
        public static bool GetUpdate(ref VirtualCameraController __instance)
        {
            __instance.isLimitPos = false;
            __instance.isLimitDir = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GuideRotation), nameof(GuideRotation.Round))]
        public static bool CustomRot(ref float __result, float _value)
        {
            var flag = _value < 0f;
            __result = Mathf.RoundToInt(Mathf.Abs(_value) / 15f) * 15f * (!flag ? 1 : -1);
            return false;
        }
    }
}
