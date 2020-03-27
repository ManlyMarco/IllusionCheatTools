using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using RuntimeUnityEditor.Core;

namespace AI_CheatTools
{
    [BepInPlugin("CheatTools", "Cheat Tools", Version)]
    [BepInDependency(RuntimeUnityEditorCore.GUID, "2.0")]
    public class CheatToolsPlugin : BaseUnityPlugin
    {
        public const string Version = "2.7";

    }
}
