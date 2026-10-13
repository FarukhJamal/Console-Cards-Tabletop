using System.IO;
using ConsoleCards.Presentation.Feedback;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Feedback
{
    /// <summary>
    /// Creates the board feedback prefabs (doc 25, F2) in Final/World, their materials, and the
    /// Resources/BoardFeedbackSet asset the game loads. Like the Final UI builders it only creates what is
    /// missing and never overwrites, so the prefabs, materials and set are yours to edit.
    /// </summary>
    public static class BoardFeedbackBuilder
    {
        private const string TexturePath = "Assets/ConsoleCards/Content/Art/Feedback/";
        private const string MaterialPath = "Assets/ConsoleCards/Content/Materials/Feedback/";
        private const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/";
        private const string SetPath = "Assets/ConsoleCards/Content/Resources/BoardFeedbackSet.asset";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

        [MenuItem("Console Cards/Feedback/Create Missing Board Feedback Prefabs (Final World)")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(PrefabPath);
            ConfigureTexture("Ring.png", TextureWrapMode.Clamp);
            ConfigureTexture("Glow.png", TextureWrapMode.Clamp);
            ConfigureTexture("TileOutline.png", TextureWrapMode.Clamp);
            ConfigureTexture("Warning.png", TextureWrapMode.Clamp);
            ConfigureTexture("ArrowHead.png", TextureWrapMode.Clamp);
            ConfigureTexture("Dash.png", TextureWrapMode.Repeat);

            Material ring = MaterialFor("Ring");
            Material glow = MaterialFor("Glow");
            Material outline = MaterialFor("TileOutline");
            Material warning = MaterialFor("Warning");
            Material head = MaterialFor("ArrowHead");
            Material dash = MaterialFor("Dash");

            BoardFeedbackMarker pawnRing = MarkerPrefab("ActivePawnRing", false, 0.6f, m =>
            {
                Flat("Ring", m.transform, ring, new Vector2(0.95f, 0.95f), 0f);
                return SetMarker(m, pulseSpeed: 4f, pulseScale: 0.08f, bobHeight: 0f, spin: 40f);
            });
            BoardFeedbackMarker trapWarning = MarkerPrefab("TrapWarning", false, 0.6f, m =>
            {
                Flat("Outline", m.transform, outline, new Vector2(0.72f, 0.98f), 0f);
                Flat("Mark", m.transform, warning, new Vector2(0.38f, 0.38f), 0.01f);
                return SetMarker(m, pulseSpeed: 7f, pulseScale: 0.06f, bobHeight: 0f, spin: 0f);
            });
            BoardFeedbackMarker moveTarget = MarkerPrefab("MoveTarget", false, 0.6f, m =>
            {
                Flat("Outline", m.transform, outline, new Vector2(0.72f, 0.98f), 0f);
                return SetMarker(m, pulseSpeed: 5f, pulseScale: 0.05f, bobHeight: 0f, spin: 0f);
            });
            BoardFeedbackMarker flipFlash = MarkerPrefab("FlipFlash", true, 0.5f, m =>
            {
                Flat("Glow", m.transform, glow, new Vector2(0.9f, 1.1f), 0f);
                return SetMarker(m, pulseSpeed: 0f, pulseScale: 0f, bobHeight: 0f, spin: 0f, grow: 1.6f);
            });
            BoardFeedbackMarker collapseBurst = MarkerPrefab("CollapseBurst", true, 0.8f, m =>
            {
                Flat("Ring", m.transform, ring, new Vector2(0.9f, 0.9f), 0f);
                Flat("Glow", m.transform, glow, new Vector2(0.8f, 0.8f), 0.005f);
                return SetMarker(m, pulseSpeed: 0f, pulseScale: 0f, bobHeight: 0f, spin: 0f, grow: 2.6f);
            });
            BoardFeedbackArrow arrow = ArrowPrefab(dash, head);

            if (!File.Exists(SetPath))
            {
                BoardFeedbackSet set = ScriptableObject.CreateInstance<BoardFeedbackSet>();
                AssetDatabase.CreateAsset(set, SetPath);
                SerializedObject so = new SerializedObject(set);
                so.FindProperty("activePawnRing").objectReferenceValue = pawnRing;
                so.FindProperty("trapWarning").objectReferenceValue = trapWarning;
                so.FindProperty("moveTarget").objectReferenceValue = moveTarget;
                so.FindProperty("flipFlash").objectReferenceValue = flipFlash;
                so.FindProperty("collapseBurst").objectReferenceValue = collapseBurst;
                so.FindProperty("moveArrow").objectReferenceValue = arrow;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.Log($"[Board Feedback] Kept {SetPath} (already exists).");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Board Feedback] Created any missing board feedback prefabs, materials and the feedback set.");
        }

        private static void ConfigureTexture(string file, TextureWrapMode wrap)
        {
            string path = TexturePath + file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Board Feedback] Missing texture {path}.");
                return;
            }

            if (importer.textureType == TextureImporterType.Default
                && importer.alphaIsTransparency
                && importer.wrapMode == wrap)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static Material MaterialFor(string name)
        {
            string path = MaterialPath + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new System.InvalidOperationException($"Shader '{ShaderName}' is missing.");
            Material material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + name + ".png"));
            material.SetColor("_BaseColor", Color.white);
            // Transparent, alpha-blended, no depth write, drawn after the table.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static BoardFeedbackMarker MarkerPrefab(
            string name,
            bool oneShot,
            float oneShotSeconds,
            System.Func<GameObject, BoardFeedbackMarker> build)
        {
            string path = PrefabPath + name + ".prefab";
            BoardFeedbackMarker existing = AssetDatabase.LoadAssetAtPath<BoardFeedbackMarker>(path);
            if (existing != null)
            {
                Debug.Log($"[Board Feedback] Kept {path} (already exists).");
                return existing;
            }

            GameObject root = new GameObject(name);
            try
            {
                BoardFeedbackMarker marker = build(root);
                SerializedObject so = new SerializedObject(marker);
                so.FindProperty("oneShot").boolValue = oneShot;
                so.FindProperty("oneShotSeconds").floatValue = oneShotSeconds;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BoardFeedbackMarker>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static BoardFeedbackMarker SetMarker(
            GameObject root,
            float pulseSpeed,
            float pulseScale,
            float bobHeight,
            float spin,
            float grow = 1.8f)
        {
            BoardFeedbackMarker marker = root.AddComponent<BoardFeedbackMarker>();
            SerializedObject so = new SerializedObject(marker);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            SerializedProperty list = so.FindProperty("renderers");
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.FindProperty("pulseSpeed").floatValue = pulseSpeed;
            so.FindProperty("pulseScale").floatValue = pulseScale;
            so.FindProperty("bobHeight").floatValue = bobHeight;
            so.FindProperty("spinDegreesPerSecond").floatValue = spin;
            so.FindProperty("oneShotGrow").floatValue = grow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        // A quad lying flat on the table (its picture faces up).
        private static Transform Flat(string name, Transform parent, Material material, Vector2 size, float lift)
        {
            GameObject quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, lift, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        private static BoardFeedbackArrow ArrowPrefab(Material dash, Material head)
        {
            string path = PrefabPath + "MoveArrow.prefab";
            BoardFeedbackArrow existing = AssetDatabase.LoadAssetAtPath<BoardFeedbackArrow>(path);
            if (existing != null)
            {
                Debug.Log($"[Board Feedback] Kept {path} (already exists).");
                return existing;
            }

            GameObject root = new GameObject("MoveArrow");
            try
            {
                LineRenderer line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = dash;
                line.textureMode = LineTextureMode.Tile;
                line.widthMultiplier = 0.12f;
                line.numCapVertices = 2;
                line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.alignment = LineAlignment.View;
                Transform headTransform = Flat("Head", root.transform, head, new Vector2(0.32f, 0.32f), 0f);
                BoardFeedbackArrow arrow = root.AddComponent<BoardFeedbackArrow>();
                SerializedObject so = new SerializedObject(arrow);
                so.FindProperty("line").objectReferenceValue = line;
                so.FindProperty("head").objectReferenceValue = headTransform;
                so.FindProperty("headRenderer").objectReferenceValue = headTransform.GetComponent<Renderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BoardFeedbackArrow>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
