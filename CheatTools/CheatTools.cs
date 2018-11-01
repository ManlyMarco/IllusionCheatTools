using BepInEx;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", "1.9")]
    public class CheatTools : BaseUnityPlugin
    {
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
