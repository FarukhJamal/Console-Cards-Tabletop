using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeInteractionGuide : MonoBehaviour
    {
        [SerializeField] private bool showGuide = true;
        [SerializeField] private Rect guideArea = new Rect(16f, 16f, 360f, 300f);
        [SerializeField] private string title = "Console Cards Prototype";
        [SerializeField] private string[] guideLines =
        {
            "Click Card, Pawn, or Token: select",
            "Click empty table: clear selection",
            "Drag selected object: preview move",
            "Release drag: accept movement",
            "Esc: cancel / rollback",
            "Mouse wheel + selection: rotate 15 degrees",
            "Mouse wheel + no selection: camera zoom",
            "Scroll during drag: no zoom or rotation",
            "F + selected Card: flip face",
            "F + Pawn or Token: rejected, no visible change",
            "WASD / Arrow keys: camera pan",
            "Middle mouse drag: camera pan",
        };

        private void OnGUI()
        {
            if (!showGuide)
            {
                return;
            }

            GUILayout.BeginArea(guideArea, GUI.skin.box);
            GUILayout.Label(title, GUI.skin.label);
            GUILayout.Space(4f);

            for (int i = 0; i < guideLines.Length; i++)
            {
                GUILayout.Label(guideLines[i], GUI.skin.label);
            }

            GUILayout.EndArea();
        }
    }
}
