using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActionGame;
using BepInEx;
using BepInEx.Logging;
using Manager;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", "1.6")]
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