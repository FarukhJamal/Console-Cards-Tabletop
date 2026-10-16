using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.TableSurface;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConsoleCards.Editor.Tables
{
    /// <summary>
    /// Console Cards > Table > Use Table Model As Scene Table (doc 30, B8; T1b): puts a table model in the scene as
    /// the base table, where the current table's playing area is. Its playing area becomes the placement surface and
    /// the layout frame (same units, so games land in the same places); other surfaces under the old table (the game
    /// board) move to the new table; the old table is switched off, not deleted. Undo puts everything back.
    /// Uses the table model selected in the Project window, or the "classic" model from the Table menu list.
    /// </summary>
    public static class TableSceneSetup
    {
        private const string Title = "Use Table Model As Scene Table";

        [MenuItem("Console Cards/Table/Use Table Model As Scene Table")]
        public static void UseAsSceneTable()
        {
            TableModel prefab = PickPrefab();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(Title, "Select a table model prefab in the Project window, or split the scene's table first (Split Table Model…, named Classic).", "OK");
                return;
            }

            if (prefab.PlayingArea == null || prefab.PlayingArea.GetComponent<MeshFilter>() == null)
            {
                EditorUtility.DisplayDialog(Title, $"'{prefab.DisplayName}' has no playing area. Run Check Table Model on it.", "OK");
                return;
            }

            PhysicalTabletopSurface origin = FindOrigin();
            if (origin == null)
            {
                EditorUtility.DisplayDialog(Title, "No active table surface in the open scene is marked as the Template Layout Origin.", "OK");
                return;
            }

            Physics.SyncTransforms();
            SerializedObject originData = new SerializedObject(origin);
            Transform frame = originData.FindProperty("templateLayoutFrame").objectReferenceValue as Transform;
            if (frame == null) frame = origin.transform;
            float localUnits = originData.FindProperty("localUnitsPerTableUnit").floatValue;
            float worldPerUnit = frame.TransformVector(Vector3.right * localUnits).magnitude;
            Collider collider = origin.GetComponent<Collider>();
            if (collider == null || worldPerUnit <= 0f)
            {
                EditorUtility.DisplayDialog(Title, "The current table surface has no collider or no usable frame.", "OK");
                return;
            }

            Bounds bounds = collider.bounds;
            Vector3 up = frame.up.normalized;
            float reach = bounds.extents.magnitude + 1f;
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            if (collider.Raycast(new Ray(bounds.center + (up * reach), -up), out RaycastHit hit, reach * 2f)) top = hit.point;
            Vector3 forward = Vector3.ProjectOnPlane(frame.forward, up);
            Quaternion facing = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward.normalized, up) : Quaternion.identity;

            TableModel oldModel = origin.GetComponentInParent<TableModel>();
            Transform oldRoot = oldModel != null ? oldModel.transform : frame;
            if (oldModel != null && PrefabUtility.GetCorrespondingObjectFromSource(oldModel) == prefab)
            {
                EditorUtility.DisplayDialog(Title, $"'{prefab.DisplayName}' is already the scene's table.", "OK");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, origin.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, Title);
            instance.name = $"Base Table ({prefab.DisplayName})";
            instance.transform.SetParent(oldRoot.parent, false);
            instance.transform.SetPositionAndRotation(top, facing);
            Vector3 parentScale = instance.transform.parent != null ? instance.transform.parent.lossyScale : Vector3.one;
            instance.transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

            TableModel model = instance.GetComponent<TableModel>();
            GameObject area = model.PlayingArea.gameObject;
            MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(area);
            meshCollider.sharedMesh = model.ColliderMesh != null ? model.ColliderMesh : area.GetComponent<MeshFilter>().sharedMesh;
            PhysicalTabletopSurface surface = Undo.AddComponent<PhysicalTabletopSurface>(area);
            SerializedObject surfaceData = new SerializedObject(surface);
            surfaceData.FindProperty("surfaceCollider").objectReferenceValue = meshCollider;
            surfaceData.FindProperty("templateLayoutOrigin").boolValue = true;
            surfaceData.FindProperty("templateLayoutFrame").objectReferenceValue = instance.transform;
            surfaceData.FindProperty("localUnitsPerTableUnit").floatValue = worldPerUnit;
            surfaceData.ApplyModifiedProperties();

            // Other surfaces under the old table (the game board) move to the new one, keeping where they are.
            int moved = 0;
            foreach (PhysicalTabletopSurface other in oldRoot.GetComponentsInChildren<PhysicalTabletopSurface>(true))
            {
                if (other == origin || other.transform.IsChildOf(origin.transform)) continue;
                bool nested = false;
                for (Transform parent = other.transform.parent; parent != null && parent != oldRoot; parent = parent.parent)
                {
                    if (parent.GetComponent<PhysicalTabletopSurface>() != null) nested = true;
                }

                if (nested) continue; // it moves with the surface above it
                Undo.SetTransformParent(other.transform, instance.transform, Title);
                moved++;
            }

            Undo.RecordObject(oldRoot.gameObject, Title);
            oldRoot.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
            EditorUtility.DisplayDialog(
                Title,
                $"'{prefab.DisplayName}' is now the scene's table.\n\n"
                + $"• The old table '{oldRoot.name}' is switched off (not deleted); delete it once you are happy.\n"
                + (moved > 0 ? $"• {moved} other surface(s) (such as the game board) moved to the new table.\n" : string.Empty)
                + "• Save the scene (Ctrl+S). Edit > Undo puts everything back.",
                "OK");
        }

        private static TableModel PickPrefab()
        {
            GameObject selected = Selection.activeObject as GameObject;
            if (selected != null && EditorUtility.IsPersistent(selected))
            {
                TableModel chosen = selected.GetComponent<TableModel>();
                if (chosen != null) return chosen;
            }

            TableModelLibrary library = AssetDatabase.LoadAssetAtPath<TableModelLibrary>(TableModelSplitter.LibraryPath);
            return library != null ? library.Find("classic") : null;
        }

        private static PhysicalTabletopSurface FindOrigin()
        {
            foreach (PhysicalTabletopSurface surface in Object.FindObjectsByType<PhysicalTabletopSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (EditorUtility.IsPersistent(surface)) continue;
                if (new SerializedObject(surface).FindProperty("templateLayoutOrigin").boolValue) return surface;
            }

            return null;
        }
    }
}
