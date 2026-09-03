using ConsoleCards.Presentation.Interaction;
using UnityEditor;

namespace ConsoleCards.Editor
{
    [CustomEditor(typeof(PhysicalTabletopSurface))]
    [CanEditMultipleObjects]
    public sealed class PhysicalTabletopSurfaceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (UnityEngine.Object selected in targets)
            {
                PhysicalTabletopSurface surface = (PhysicalTabletopSurface)selected;
                bool usable = surface.TryGetCollider(out _, out string issue);
                EditorGUILayout.HelpBox(usable
                    ? "Valid authored placement surface. Resize the Collider with Edit Collider; its Transform/parent scale defines the usable area. No model or composition reference is needed."
                    : issue, usable ? MessageType.Info : MessageType.Warning);
            }
        }
    }
}
