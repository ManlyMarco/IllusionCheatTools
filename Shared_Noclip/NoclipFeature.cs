using System;
using System.Collections;
using BepInEx.Configuration;
using KKAPI.Studio;
using UnityEngine;
using UnityEngine.AI;

namespace CheatTools
{
    internal static class NoclipFeature
    {
        private static ConfigEntry<KeyboardShortcut> _noclip;
        private static Func<NavMeshAgent> _getPlayerNavMeshAgent;
        private static bool _noclipMode;

        public static void InitializeNoclip(CheatToolsPlugin instance, Func<NavMeshAgent> getPlayerNavMeshAgent)
        {
            if (StudioAPI.InsideStudio) return;
            
            _getPlayerNavMeshAgent = getPlayerNavMeshAgent ?? throw new ArgumentNullException(nameof(getPlayerNavMeshAgent));

            _noclip = instance.Config.Bind("Hotkeys", "Toggle player noclip", KeyboardShortcut.Empty);
            instance.StartCoroutine(NoclipCo());
        }

        private static IEnumerator NoclipCo()
        {
            while (true)
            {
                yield return null;

                if (_noclip.Value.IsDown())
                {
                    NoclipMode = !NoclipMode;
                }

                if (NoclipMode)
                {
                    var navMeshAgent = _getPlayerNavMeshAgent();
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
                    var navMeshAgent = _getPlayerNavMeshAgent();
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

        private static void RunNoclip(Transform playerTransform)
        {
            if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
            {
                var moveSpeed = Input.GetKey(KeyCode.LeftShift) ? 0.5f : 0.05f;
                playerTransform.Translate(
                    moveSpeed * new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")),
                    Camera.main.transform);
            }

            if (Input.GetAxis("Mouse ScrollWheel") != 0)
            {
                var scrollSpeed = Input.GetKey(KeyCode.LeftShift) ? 10f : 1f;
                playerTransform.position += scrollSpeed * new Vector3(0, -Input.GetAxis("Mouse ScrollWheel"), 0);
            }

            var eulerAngles = playerTransform.rotation.eulerAngles;
            eulerAngles.y = Camera.main.transform.rotation.eulerAngles.y;
            playerTransform.rotation = Quaternion.Euler(eulerAngles);
        }
    }
}
