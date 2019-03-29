using BepInEx;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditor.RuntimeUnityEditor.GUID)]
    public class CheatTools : BaseUnityPlugin
    {
        public const string Version = "2.3";
        
        private CheatWindow _cheatWindow;
        
        protected void OnGUI()
        {
            _cheatWindow?.DisplayCheatWindow();
        }

        protected void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12))
            {
                if(_cheatWindow == null)
                    _cheatWindow = new CheatWindow(GetComponent<RuntimeUnityEditor.RuntimeUnityEditor>());

                _cheatWindow.Show = !_cheatWindow.Show;
            }
        }
    }
}
