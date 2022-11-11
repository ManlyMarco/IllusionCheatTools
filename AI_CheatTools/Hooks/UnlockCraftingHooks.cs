using System.Collections.Generic;
using AIProject;
using AIProject.UI;
using AIProject.UI.Viewer;
using HarmonyLib;

namespace CheatTools
{
    /// <summary>
    /// Based on a cheat script by ghorsington
    /// </summary>
    internal static class UnlockCraftingHooks
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
                        _hInstance = Harmony.CreateAndPatchAll(typeof(UnlockCraftingHooks));
                        _hInstance.Patch(
                            typeof(Manager.Housing.LoadInfo).GetConstructor(new[] { typeof(int), typeof(List<string>) }),
                            postfix: new HarmonyMethod(typeof(UnlockCraftingHooks), nameof(LoadInfoCtor)));
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
        [HarmonyPatch(typeof(CraftUI), "Possible")]
        public static bool CheckPossible(ref RecipeDataInfo[] __result, RecipeDataInfo[] info)
        {
            __result = info;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftViewer), "Possible")]
        public static bool CheckPossible(ref CraftItemNodeUI.Possible __result)
        {
            __result = new CraftItemNodeUI.Possible(false, true);
            return false;
        }

        // How many of required items are available
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RecipeItemNodeUI), "ItemCount", MethodType.Getter)]
        public static bool GetCraftItemUIItemCount(ref int __result)
        {
            __result = 9999;
            return false;
        }

        private static void LoadInfoCtor(Manager.Housing.LoadInfo __instance)
        {
            __instance.requiredMaterials = new Manager.Housing.RequiredMaterial[0];
        }
    }
}