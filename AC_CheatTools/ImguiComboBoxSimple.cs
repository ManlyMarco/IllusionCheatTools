using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace CheatTools
{
    /// <inheritdoc cref="ImguiComboBox"/>
    internal class ImguiComboBoxSimple : ImguiComboBox
    {
        public readonly GUIContent[] Contents;
        public int[] ContentsIndexes;
        public int Index;
        public ImguiComboBoxSimple(GUIContent[] contents) : base()
        {
            Contents = contents;
        }

        /// <inheritdoc cref="ImguiComboBox.Show(int,GUIContent[],int,UnityEngine.GUIStyle)"/>
        public void Show(int windowYmax = int.MaxValue, GUIStyle listStyle = null)
        {
            Index = base.Show(Index, Contents, windowYmax, listStyle);
        }
    }
}
