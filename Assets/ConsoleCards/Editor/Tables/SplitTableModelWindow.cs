using ConsoleCards.Presentation.TableSurface;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Tables
{
    /// <summary>
    /// Console Cards > Table > Split Table Model… (doc 30, B8; T1b): pick a table in the scene, name it, and split
    /// it into playing area, sides and lower frame. The model itself is never changed.
    /// </summary>
    public sealed class SplitTableModelWindow : EditorWindow
    {
        private GameObject source;
        private string displayName = "Classic";
        private float sidesDepth;
        private string lastReport = string.Empty;
        private Vector2 scroll;

        [MenuItem("Console Cards/Table/Split Table Model…")]
        public static void Open()
        {
            SplitTableModelWindow window = GetWindow<SplitTableModelWindow>(true, "Split Table Model");
            window.minSize = new Vector2(460f, 360f);
            if (Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
            {
                window.source = Selection.activeGameObject;
            }

            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Drag a table from the Hierarchy. It is split into PlayingArea (where pieces land), Sides (the rim) "
                + "and LowerFrame (legs and base), saved as a new prefab in Content/Tables and added to the Table menu. "
                + "The model itself is not changed. Pieces are sorted, not cut: a model whose top, rim and legs are one "
                + "fused surface needs Blender.",
                MessageType.Info);
            source = (GameObject)EditorGUILayout.ObjectField("Table in the scene", source, typeof(GameObject), true);
            displayName = EditorGUILayout.TextField("Name in the Table menu", displayName);
            sidesDepth = EditorGUILayout.FloatField(
                new GUIContent("Sides reach below top", "World units below the playing area that still count as Sides. 0: 12 % of the table's height."),
                Mathf.Max(0f, sidesDepth));

            using (new EditorGUI.DisabledScope(source == null))
            {
                if (GUILayout.Button("Split", GUILayout.Height(28f)))
                {
                    TableModel model = TableModelSplitter.Split(
                        new TableModelSplitter.Settings { Source = source, DisplayName = displayName, SidesDepth = sidesDepth },
                        out lastReport);
                    if (model != null)
                    {
                        lastReport += "\n" + TableModelChecker.Check(model, out _);
                        Selection.activeObject = model.gameObject;
                        EditorGUIUtility.PingObject(model.gameObject);
                    }

                    Debug.Log($"[Table] Split Table Model:\n{lastReport}");
                }
            }

            if (!string.IsNullOrEmpty(lastReport))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.HelpBox(lastReport, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
