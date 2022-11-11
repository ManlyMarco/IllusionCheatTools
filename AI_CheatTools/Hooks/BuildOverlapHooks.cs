using HarmonyLib;
using Housing;

namespace CheatTools
{
    /// <summary>
    /// Based on a cheat script by ghorsington
    /// </summary>
    internal static class BuildOverlapHooks
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
                        _hInstance = Harmony.CreateAndPatchAll(typeof(BuildOverlapHooks));
                    else
                    {
                        _hInstance.UnpatchSelf();
                        _hInstance = null;
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftInfo), "IsOverlapNow", MethodType.Getter)]
        [HarmonyPatch(typeof(OCItem), "IsOverlapNow", MethodType.Getter)]
        [HarmonyPatch(typeof(OCFolder), "IsOverlapNow", MethodType.Getter)]
        [HarmonyPatch(typeof(ObjectCtrl), "IsOverlapNow", MethodType.Getter)]
        public static bool GetIsOverlapNow(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}