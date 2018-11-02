using System.Collections.Generic;
using Harmony;
using Manager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CheatTools
{
    /// <summary>
    /// Allows blocking of mouse clicks in the main game and studio
    /// </summary>
    internal static class CursorBlocker
    {
        private static bool _disableCameraControls;

        private static bool _hooksInstalled;
        //private static List<string> _sceneNameOverride;

        public static bool DisableCameraControls
        {
            get => _disableCameraControls;
            set
            {
                if (!_hooksInstalled)
                {
                    _hooksInstalled = true;
                    InstallHooks();
                }

                _disableCameraControls = value;

                var hSceneProc = Object.FindObjectOfType<HSceneProc>();
                if (hSceneProc != null) hSceneProc.enabled = !value;
            }
        }

        private static void InstallHooks()
        {
            if (Application.productName == "CharaStudio")
            {
                var oldCondition = Studio.Studio.Instance.cameraCtrl.noCtrlCondition;
                Studio.Studio.Instance.cameraCtrl.noCtrlCondition = () => DisableCameraControls || oldCondition();
            }
            else
            {
                HarmonyInstance.Create("CursorBlockerHooks").PatchAll(typeof(CursorBlocker));
                //_sceneNameOverride = new List<string> {"CursorBlocker"};
            }
        }

        /*[HarmonyPrefix]
        [HarmonyPatch(typeof(Scene))]
        [HarmonyPatch("NowSceneNames", PropertyMethod.Getter)]
        public static bool NowSceneNamesOverride(Scene __instance, ref List<string> __result)
        {
            if (DisableCameraControls)
            {
                __result = _sceneNameOverride;
                return false;
            }
            return true;
        }*/

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CameraControl_Ver2), "LateUpdate")]
        public static bool CameraControl_LateUpdateOverride(CameraControl_Ver2 __instance)
        {
            return !DisableCameraControls;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GlobalMethod), "IsCameraMoveFlag", 
            new[] {typeof(CameraControl_Ver2)})]
        public static bool GlobalMethod_IsCameraMoveFlagOverride(ref bool __result, CameraControl_Ver2 _ctrl)
        {
            if (_ctrl != null && !DisableCameraControls)
            {
                var noCtrlCondition = _ctrl.NoCtrlCondition;

                if (noCtrlCondition != null && !noCtrlCondition())
                    __result = true;
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GraphicRaycaster), "Raycast",
            new[] {typeof(PointerEventData), typeof(List<RaycastResult>)})]
        public static bool SetGameCanvasInputsEnabled()
        {
            return !DisableCameraControls;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GraphicRaycaster), "Raycast",
            new[] {typeof(Canvas), typeof(Camera), typeof(Vector2), typeof(List<Graphic>)})]
        public static bool SetGameCanvasInputsEnabled2()
        {
            return !DisableCameraControls;
        }
    }
}