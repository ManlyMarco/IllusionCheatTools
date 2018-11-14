using System.ComponentModel;
using BepInEx;
using UnityEngine;

namespace CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", "2.0")]
    public class CheatTools : BaseUnityPlugin
    {
        [DisplayName("Path to dnSpy.exe")]
        [Description("Full path to dnSpy that will enable integration with Inspector.\n\n" +
                     "When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.")]
        public ConfigWrapper<string> DnSpyPath { get; private set; }

        private CheatWindow _cheatWindow;

        protected void Start()
        {
            _cheatWindow = new CheatWindow();

            DnSpyPath = new ConfigWrapper<string>(nameof(DnSpyPath), this);
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;
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
