using System;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsoleCards.Editor.ConsoleMat
{
    /// <summary>
    /// Scene view editor for the Console mat layout (doc 22 §14). Open the Console prefab, select its root and the
    /// slots of the chosen side are drawn on the mat: drag a slot's centre to move it, drag its corner dot to
    /// resize it, and the dashed outline shows where a card lies. Changes go to the layout asset (the one source);
    /// the prefab's slots, anchors, colliders and feedback plates follow it, so card positions follow too.
    /// </summary>
    [CustomEditor(typeof(ConsoleLayoutBinding))]
    public sealed class ConsoleLayoutSceneEditor : UnityEditor.Editor
    {
        private const float CardWidth = 1f;
        private const float CardDepth = 1.4f;
        private const float DrawHeight = 0.04f;

        private static bool editInSceneView = true;
        private static bool applyWhileEditing = true;
        private static bool showBack;
        private static int selectedSlot = -1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ConsoleLayoutBinding binding = (ConsoleLayoutBinding)target;
            ConsoleLayoutDefinition layout = binding.Layout;
            if (layout == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Console layout (Scene view)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open the Console prefab and select its root. In the Scene view, drag a slot's centre square to move it "
                + "and its corner dot to resize it. The dashed outline is where a card lies. Cyan: card slot; yellow: die spot.",
                MessageType.Info);
            editInSceneView = EditorGUILayout.Toggle("Edit in Scene view", editInSceneView);
            applyWhileEditing = EditorGUILayout.Toggle("Move prefab slots too", applyWhileEditing);
            int side = GUILayout.Toolbar(showBack ? 1 : 0, new[] { "Front side", "Back side" });
            if ((side == 1) != showBack)
            {
                showBack = side == 1;
                selectedSlot = -1;
                ShowMatSide(binding, showBack);
                SceneView.RepaintAll();
            }

            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            if (selectedSlot >= 0 && selectedSlot < slots.arraySize)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Selected slot", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(slots.GetArrayElementAtIndex(selectedSlot), true);
                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    if (applyWhileEditing) Apply(binding, false);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply layout to this Console"))
            {
                Apply(binding, true);
            }
        }

        private void OnDisable()
        {
            // Leave the mat front up; the side shown while editing is only a view.
            if (target is ConsoleLayoutBinding binding && binding != null && showBack)
            {
                ShowMatSide(binding, false);
            }
        }

        private void OnSceneGUI()
        {
            ConsoleLayoutBinding binding = (ConsoleLayoutBinding)target;
            if (!editInSceneView || binding == null || binding.Layout == null) return;

            ConsoleLayoutDefinition layout = binding.Layout;
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            bool changed = false;
            using (new Handles.DrawingScope(binding.transform.localToWorldMatrix))
            {
                for (int i = 0; i < slots.arraySize; i++)
                {
                    SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                    ConsoleLayoutSlotKind kind = (ConsoleLayoutSlotKind)slot.FindPropertyRelative("kind").intValue;
                    bool back = slot.FindPropertyRelative("side").intValue == (int)ConsoleMatSide.Back;
                    if (kind == ConsoleLayoutSlotKind.Cube || back != showBack) continue;
                    changed |= DrawSlot(slot, kind, i);
                }
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                if (applyWhileEditing) Apply(binding, false);
            }
        }

        private static bool DrawSlot(SerializedProperty slot, ConsoleLayoutSlotKind kind, int index)
        {
            SerializedProperty x = slot.FindPropertyRelative("x");
            SerializedProperty z = slot.FindPropertyRelative("z");
            SerializedProperty width = slot.FindPropertyRelative("footprintWidth");
            SerializedProperty depth = slot.FindPropertyRelative("footprintDepth");
            Vector3 centre = new Vector3(x.floatValue, DrawHeight, z.floatValue);
            float halfWidth = width.floatValue * 0.5f;
            float halfDepth = depth.floatValue * 0.5f;
            bool selected = index == selectedSlot;
            Color line = kind == ConsoleLayoutSlotKind.Die ? new Color(1f, 0.85f, 0.2f) : new Color(0.33f, 0.94f, 0.86f);
            if (selected) line = new Color(1f, 0.55f, 0.15f);
            Color fill = new Color(line.r, line.g, line.b, selected ? 0.25f : 0.12f);
            Handles.DrawSolidRectangleWithOutline(Rect(centre, halfWidth, halfDepth), fill, line);

            if (kind == ConsoleLayoutSlotKind.Card)
            {
                bool landscape = slot.FindPropertyRelative("defaultOrientation").intValue == (int)ConsoleSlotOrientation.Landscape;
                Vector3[] card = Rect(centre, (landscape ? CardDepth : CardWidth) * 0.5f, (landscape ? CardWidth : CardDepth) * 0.5f);
                Handles.color = new Color(1f, 1f, 1f, 0.7f);
                Handles.DrawDottedLines(new[] { card[0], card[1], card[1], card[2], card[2], card[3], card[3], card[0] }, 4f);
            }

            ConsoleSymbol symbol = (ConsoleSymbol)slot.FindPropertyRelative("symbol").intValue;
            string label = slot.FindPropertyRelative("key").stringValue + (symbol != ConsoleSymbol.None ? $" ({symbol})" : string.Empty);
            Handles.Label(centre + new Vector3(-halfWidth, 0f, halfDepth + 0.12f), label, EditorStyles.whiteBoldLabel);

            bool changed = false;
            Handles.color = line;
            float moveSize = HandleUtility.GetHandleSize(centre) * 0.07f;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider2D(centre, Vector3.up, Vector3.right, Vector3.forward, moveSize, Handles.RectangleHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                x.floatValue = Round(moved.x);
                z.floatValue = Round(moved.z);
                selectedSlot = index;
                changed = true;
            }

            Vector3 corner = centre + new Vector3(halfWidth, 0f, -halfDepth);
            float cornerSize = HandleUtility.GetHandleSize(corner) * 0.05f;
            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.Slider2D(corner, Vector3.up, Vector3.right, Vector3.forward, cornerSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                width.floatValue = Mathf.Max(0.1f, Round(Mathf.Abs(dragged.x - centre.x) * 2f));
                depth.floatValue = Mathf.Max(0.1f, Round(Mathf.Abs(dragged.z - centre.z) * 2f));
                selectedSlot = index;
                changed = true;
            }

            return changed;
        }

        private static Vector3[] Rect(Vector3 centre, float halfWidth, float halfDepth)
        {
            return new[]
            {
                centre + new Vector3(-halfWidth, 0f, -halfDepth),
                centre + new Vector3(-halfWidth, 0f, halfDepth),
                centre + new Vector3(halfWidth, 0f, halfDepth),
                centre + new Vector3(halfWidth, 0f, -halfDepth),
            };
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 1000f) / 1000f;
        }

        // Puts the prefab's slots, anchors and colliders where the layout says.
        private static void Apply(ConsoleLayoutBinding binding, bool report)
        {
            ConsoleLayoutData data;
            try
            {
                data = binding.Layout.ToData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Console] The layout is not valid yet: {exception.Message}");
                return;
            }

            if (!IsEditablePrefab(binding.gameObject))
            {
                if (report) Debug.LogWarning("[Console] Open the Console prefab (Prefab Mode) to apply the layout to its slots.");
                return;
            }

            int changed = ConsoleMatUpgradeTool.SyncSlotsToLayout(binding.gameObject, data, true);
            if (changed > 0)
            {
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
                else EditorUtility.SetDirty(binding.gameObject);
            }

            if (report) Debug.Log($"[Console] Layout applied: {changed} slots moved or resized.");
        }

        private static bool IsEditablePrefab(GameObject root)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            return (stage != null && stage.prefabContentsRoot == root) || PrefabUtility.IsPartOfPrefabAsset(root);
        }

        // Shows the chosen side of the mat picture while editing (a view only; the game sets the side itself).
        private static void ShowMatSide(ConsoleLayoutBinding binding, bool back)
        {
            ConsoleMatFaces faces = binding.GetComponent<ConsoleMatFaces>();
            if (faces == null || faces.TurnPivot == null) return;
            faces.TurnPivot.localRotation = back ? Quaternion.AngleAxis(180f, Vector3.right) : Quaternion.identity;
            SceneView.RepaintAll();
        }
    }
}
