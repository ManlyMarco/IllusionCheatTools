using System;
using System.Collections;
using BepInEx.Configuration;
using KKAPI.Studio;
using Manager;
using UnityEngine.AI;

namespace CheatTools
{
    internal static class NoclipFeature
    {
        private static ConfigEntry<KeyboardShortcut> _noclip;
        private static bool _noclipMode;

        public static void InitializeNoclip(CheatToolsPlugin instance)
        {
            if (StudioAPI.InsideStudio) return;

            _noclip = instance.Config.Bind("Hotkeys", "Toggle player noclip", KeyboardShortcut.Empty);
            instance.StartCoroutine(NoclipCo());
        }

        private static IEnumerator NoclipCo()
        {
            while (true)
            {
                yield return null;

                if (_noclip.Value.IsDown())
                    NoclipMode = !NoclipMode;

                if (NoclipMode)
                {
                    var navMeshAgent = GetPlayerNavMeshAgent();
                    if (navMeshAgent == null || navMeshAgent.enabled)
                    {
                        NoclipMode = false;
                        continue;
                    }

                    RunNoclip(navMeshAgent.transform);
                }
            }
        }

        internal static bool NoclipMode
        {
            get => _noclipMode;
            set
            {
                if (_noclipMode != value)
                {
                    var navMeshAgent = GetPlayerNavMeshAgent();
                    if (navMeshAgent != null)
                    {
                        navMeshAgent.enabled = !value;
                        _noclipMode = value;
                        return;
                    }

                    _noclipMode = false;
                }
            }
        }

        private static NavMeshAgent GetPlayerNavMeshAgent()
        {
#if AI
            if (!Map.IsInstance()) return null;
            if (Map.Instance.Player == null) return null;
            if (Map.Instance.Player.Controller == null) return null;
            return Map.Instance.Player.Controller.GetComponent<NavMeshAgent>();
#elif KK
            if (!Game.IsInstance()) return null;
            var player = Game.Instance.Player;
            if (player == null) return null;
            var playerTransform = player.transform;
            if (playerTransform == null) return null;
            return playerTransform.GetComponent<NavMeshAgent>();
#elif KKS
            if (Game.Player == null) return null;
            if (Game.Player.transform == null) return null;
            return Game.Player.transform.GetComponent<NavMeshAgent>();
#endif
        }

        private static void RunNoclip(UnityEngine.Transform playerTransform)
        {
#if AI || HS2
            var x = Manager.Input.Instance.GetAxisRaw(ActionID.MoveHorizontal);
            var y = Manager.Input.Instance.GetAxisRaw(ActionID.MoveVertical);
#else
            var x = UnityEngine.Input.GetAxisRaw("Horizontal");
            var y = UnityEngine.Input.GetAxisRaw("Vertical");
#endif
            if (x != 0 || y != 0)
            {
                var moveSpeed = UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) ?
#if AI || HS2
                    5f : 0.5f;
#else
                    0.5f : 0.05f;
#endif
                playerTransform.Translate(moveSpeed * new UnityEngine.Vector3(x, 0, y), UnityEngine.Camera.main.transform);
            }

            var w = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (w != 0)
            {
                var scrollSpeed = UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) ?
#if AI || HS2
                    100f : 10f;
#else
                    10f : 1f;
#endif
                playerTransform.position += scrollSpeed * new UnityEngine.Vector3(0, -w, 0);
            }

            var eulerAngles = playerTransform.rotation.eulerAngles;
            eulerAngles.y = UnityEngine.Camera.main.transform.rotation.eulerAngles.y;
            playerTransform.rotation = UnityEngine.Quaternion.Euler(eulerAngles);
        }
    }
}
