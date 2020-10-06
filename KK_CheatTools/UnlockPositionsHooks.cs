using System.Collections.Generic;
using System.Linq;
using BepInEx.Harmony;
using HarmonyLib;
using UnityEngine;

namespace CheatTools
{
    /// <summary>
    /// Originally by Keelhauled
    /// https://github.com/Keelhauled/KoikatuPlugins/blob/master/src/UnlockHPositions/Unlocker.cs
    /// </summary>
    internal static class UnlockPositionsHooks
    {
        private static bool _fixUi;
        private static Harmony _hInstance;

        public static bool Enabled
        {
            get => _hInstance != null;
            set
            {
                if (value != Enabled)
                {
                    if (value)
                        _hInstance = HarmonyWrapper.PatchAll(typeof(UnlockPositionsHooks));
                    else
                    {
                        _hInstance.UnpatchAll(_hInstance.Id);
                        _hInstance = null;
                    }
                }
            }
        }

        public static bool UnlockAll { get; set; }

        [HarmonyPrefix, HarmonyPatch(typeof(HSceneProc), "CreateListAnimationFileName")]
        public static bool HarmonyPatch_HSceneProc_CreateListAnimationFileName(HSceneProc __instance, ref bool _isAnimListCreate, ref int _list)
        {
            var traverse = Traverse.Create(__instance);

            _fixUi = false;
            var oneFem = __instance.flags.lstHeroine.Count == 1;
            var peeping = __instance.dataH.peepCategory?.FirstOrDefault() != 0;

            if (_isAnimListCreate)
                traverse.Method("CreateAllAnimationList").GetValue();

            var lstAnimInfo = traverse.Field("lstAnimInfo").GetValue<List<HSceneProc.AnimationListInfo>[]>();
            var lstUseAnimInfo = traverse.Field("lstUseAnimInfo").GetValue<List<HSceneProc.AnimationListInfo>[]>();

            for (int i = 0; i < lstAnimInfo.Length; i++)
            {
                lstUseAnimInfo[i] = new List<HSceneProc.AnimationListInfo>();
                if (_list == -1 || i == _list)
                {
                    for (int j = 0; j < lstAnimInfo[i].Count; j++)
                    {
                        if ((UnlockAll && oneFem && !peeping) || lstAnimInfo[i][j].lstCategory.Any(c => __instance.categorys.Contains(c.category)))
                        {
                            if (oneFem) _fixUi = true;
                            lstUseAnimInfo[i].Add(lstAnimInfo[i][j]);
                        }
                    }
                }
            }

            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSprite), "CreateMotionList")]
        public static void HarmonyPatch_HSprite_CreateMotionList(HSprite __instance, ref int _kind)
        {
            // todo move this list size fix to a fix plugin
            if (_fixUi && _kind == 2 && UnlockAll && __instance.menuActionSub.GetActive(5))
            {
                var go = __instance.menuAction.GetObject(_kind);
                var rectTransform = go.transform as RectTransform;
                go = __instance.menuActionSub.GetObject(5);
                var rectTransform2 = go.transform as RectTransform;
                var anchoredPosition = rectTransform2.anchoredPosition;
                anchoredPosition.y = rectTransform.anchoredPosition.y + 350f; // may cause issues with different resolutions, fuck it
                rectTransform2.anchoredPosition = anchoredPosition;
            }
        }
    }
}
