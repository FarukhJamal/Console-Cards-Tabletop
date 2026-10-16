using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Composition;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Editor
{
    [CustomEditor(typeof(TabletopComposition))]
    public sealed class TabletopCompositionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Camera camera = serializedObject.FindProperty("targetCamera").objectReferenceValue as Camera;
            if (camera != null && camera.gameObject.scene.IsValid())
            {
                int count = PhysicalTabletopSurfaces.CountUsableSurfaces(camera.gameObject.scene.GetPhysicsScene());
                EditorGUILayout.HelpBox(count == 0 ? PhysicalTabletopSurfaces.MissingSurfaceMessage
                    : $"Physical placement: {count} usable registered surface(s). Table/Board models need no placement references here.",
                    count == 0 ? MessageType.Error : MessageType.Info);
            }
            DrawDefaultInspector();
        }
    }
}
