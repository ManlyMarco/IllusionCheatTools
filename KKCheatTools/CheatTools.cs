using BepInEx;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    public class CheatTools : BaseUnityPlugin
    {
        public const string Version = "2.2";
        
        private CheatWindow _cheatWindow;

        protected void Start()
        {
            _cheatWindow = new CheatWindow();
        }

        protected void OnGUI()
        {
            _cheatWindow.DisplayCheatWindow();
        }

        protected void Update()
        {
            _cheatWindow.OnUpdate();

            if (Input.GetKeyDown(KeyCode.F12))
            {
                _cheatWindow.Show = !_cheatWindow.Show;
            }
        }
    }
}
