using System.Collections.Generic;
using System.Text;
using ConsoleCards.Presentation.TableSurface;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Tables
{
    /// <summary>
    /// Console Cards > Table > Check Table Model (doc 30, B8; T1b): checks a table model prefab against the contract
    /// and says in plain words what to fix. Problems stop the model working; notes are advice.
    /// </summary>
    public static class TableModelChecker
    {
        // Today's classic playing area (world units): games are laid out for it.
        private static readonly Vector2 ClassicArea = new Vector2(24.7f, 17.1f);
        private const int TriangleBudget = 20000;

        [MenuItem("Console Cards/Table/Check Table Model")]
        public static void CheckSelected()
        {
            GameObject selected = Selection.activeGameObject;
            TableModel model = selected != null ? selected.GetComponentInParent<TableModel>() : null;
            if (model == null)
            {
                EditorUtility.DisplayDialog("Check Table Model", "Select a table model prefab (it has a TableModel component on its root).", "OK");
                return;
            }

            string report = Check(model, out int problems);
            Debug.Log($"[Table] Check Table Model '{model.name}':\n{report}", model);
            EditorUtility.DisplayDialog("Check Table Model", report, "OK");
            if (problems == 0) EnsureInLibrary(model);
        }

        /// <summary>The check's findings as text; <paramref name="problems"/> counts the ones that stop the model working.</summary>
        public static string Check(TableModel model, out int problems)
        {
            List<string> found = new List<string>();
            List<string> notes = new List<string>();
            Transform root = model.transform;

            if (string.IsNullOrWhiteSpace(model.ModelId)) found.Add("Give the model an id (TableModel > Model Id).");
            if (model.PlayingArea == null) found.Add($"There is no {TableModel.PlayingAreaName} part.");
            if (model.Sides == null) notes.Add($"There is no {TableModel.SidesName} part (fine for a table without a rim).");
            if (model.LowerFrame == null) notes.Add($"There is no {TableModel.LowerFrameName} part (the table will float).");
            if (root.localScale != Vector3.one || root.localRotation != Quaternion.identity)
                notes.Add("Set the root's rotation to 0 and scale to 1; the game places the root itself.");

            int triangles = 0;
            foreach (MeshRenderer part in new[] { model.PlayingArea, model.Sides, model.LowerFrame })
            {
                if (part == null) continue;
                MeshFilter filter = part.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    found.Add($"{part.name} has no mesh.");
                    continue;
                }

                triangles += CountTriangles(mesh);
                if (part.transform.parent != root) notes.Add($"{part.name} should be a direct child of the root.");
                if (part.transform.localScale != Vector3.one || part.transform.localRotation != Quaternion.identity)
                    notes.Add($"{part.name}: set rotation to 0 and scale to 1 (bake them into the mesh).");
                if (part.sharedMaterials.Length > 1)
                    notes.Add($"{part.name} has {part.sharedMaterials.Length} materials; one per part lets the player change its texture in one go.");
            }

            if (triangles > TriangleBudget) notes.Add($"{triangles} triangles; the budget is about {TriangleBudget}.");
            if (model.PlayingArea != null) CheckPlayingArea(model, found, notes);

            problems = found.Count;
            StringBuilder report = new StringBuilder();
            report.AppendLine(found.Count == 0 ? $"'{model.DisplayName}' can be used as a table." : $"'{model.DisplayName}' cannot be used yet:");
            foreach (string line in found) report.AppendLine("• " + line);
            if (notes.Count > 0) report.AppendLine("Notes:");
            foreach (string line in notes) report.AppendLine("• " + line);
            report.AppendLine($"{triangles} triangles in all.");
            return report.ToString();
        }

        private static void CheckPlayingArea(TableModel model, List<string> found, List<string> notes)
        {
            MeshFilter filter = model.PlayingArea.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return;
            if (model.PlayingArea.GetComponents<Collider>().Length > 0)
                found.Add($"Remove the colliders from {TableModel.PlayingAreaName}; the game adds its own (or give a simpler mesh as Collider Mesh).");

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length == 0 && mesh.vertexCount > 0)
            {
                notes.Add($"{mesh.name} cannot be read here; tick Read/Write on its model file to check it fully.");
                return;
            }

            if (vertices.Length == 0 || triangles.Length < 3)
            {
                found.Add($"{TableModel.PlayingAreaName} has an empty mesh.");
                return;
            }

            Matrix4x4 toRoot = model.transform.worldToLocalMatrix * model.PlayingArea.transform.localToWorldMatrix;
            float flat = 0f;
            float total = 0f;
            Bounds bounds = new Bounds(toRoot.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = toRoot.MultiplyPoint3x4(vertices[triangles[t]]);
                Vector3 b = toRoot.MultiplyPoint3x4(vertices[triangles[t + 1]]);
                Vector3 c = toRoot.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = 0.5f * cross.magnitude;
                total += area;
                if (area > 0f && cross.normalized.y >= 0.95f) flat += area;
                bounds.Encapsulate(a);
                bounds.Encapsulate(b);
                bounds.Encapsulate(c);
            }

            if (total <= 0f)
            {
                found.Add($"{TableModel.PlayingAreaName} has no area.");
                return;
            }

            if (flat / total < 0.9f)
                found.Add($"{TableModel.PlayingAreaName} is not flat and facing up ({100f * flat / total:0}% of it is). Move bumps and walls to Sides.");
            if (Mathf.Abs(bounds.max.y) > 0.05f)
                notes.Add($"The top of {TableModel.PlayingAreaName} is {bounds.max.y:0.###} from the root; it should be at the root's height (0).");
            if (Mathf.Abs(bounds.center.x) > 0.05f || Mathf.Abs(bounds.center.z) > 0.05f)
                notes.Add($"The root is not at the centre of {TableModel.PlayingAreaName} (off by {bounds.center.x:0.##}, {bounds.center.z:0.##}); game layouts are centred on the root.");
            if (bounds.size.x + 0.05f < ClassicArea.x || bounds.size.z + 0.05f < ClassicArea.y)
                notes.Add($"The playing area is {bounds.size.x:0.#} × {bounds.size.z:0.#}; games are laid out for about {ClassicArea.x:0.#} × {ClassicArea.y:0.#}, so some may not fit.");

            List<Vector2> uvs = new List<Vector2>();
            mesh.GetUVs(0, uvs);
            if (uvs.Count == 0) notes.Add($"{TableModel.PlayingAreaName} has no texture coordinates; textures will not show on it.");
        }

        private static int CountTriangles(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
            return (int)count;
        }

        // A model that passes and lives in the Project is offered for the Table menu.
        private static void EnsureInLibrary(TableModel model)
        {
            if (!EditorUtility.IsPersistent(model)) return;
            TableModelLibrary library = AssetDatabase.LoadAssetAtPath<TableModelLibrary>(TableModelSplitter.LibraryPath);
            if (library != null && library.Find(model.ModelId) == model) return;
            if (!EditorUtility.DisplayDialog("Check Table Model", $"Add '{model.DisplayName}' to the Table menu?", "Add", "Not now")) return;
            if (library == null)
            {
                TableModelSplitter.EnsureFolder("Assets/ConsoleCards/Content/Resources");
                library = ScriptableObject.CreateInstance<TableModelLibrary>();
                AssetDatabase.CreateAsset(library, TableModelSplitter.LibraryPath);
            }

            library.AddOrReplace(model);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
