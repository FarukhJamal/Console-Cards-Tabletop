using System;
using System.IO;
using ConsoleCards.Presentation.Feedback;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Editor.Feedback
{
    /// <summary>
    /// Board 18 "Board feedback" (owner, 2026-10-30): the marks on the game board in one family, flat on the tiles.
    /// Draws its pictures (Art/Feedback/Fb2*.png), makes its materials and Final/World prefabs (Fb2_*), and points
    /// Resources/BoardFeedbackSet at them with colours by meaning. The pictures are drawn again on every run (they are
    /// this builder's); materials and prefabs are kept (delete one to have it made again). The older markers stay in
    /// the project untouched.
    /// </summary>
    public static class BoardFeedbackV2Builder
    {
        private const string TexturePath = "Assets/ConsoleCards/Content/Art/Feedback/";
        private const string MaterialPath = "Assets/ConsoleCards/Content/Materials/Feedback/";
        private const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/";
        private const string SetPath = "Assets/ConsoleCards/Content/Resources/BoardFeedbackSet.asset";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

        // A tile (a floor card) in world units, as the older markers; pictures for it are 256 x 348.
        private const float TileWidth = 0.72f;
        private const float TileDepth = 0.98f;
        private const int TileW = 256;
        private const int TileH = 348;
        private const float Inset = 6f;
        private const float Corner = 18f;

        [MenuItem("Console Cards/Feedback/Board Feedback v2 (board 18)")]
        public static void Build()
        {
            Directory.CreateDirectory(TexturePath);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(PrefabPath);

            Paint("Fb2Fill", TileW, TileH, (x, y) =>
            {
                float d = TileDistance(x, y);
                if (d > 0.5f) return 0f;
                return Mathf.Clamp01(0.5f - d) * (0.28f + (0.6f * Mathf.Exp(d / 28f)));
            });
            // Lines are bold enough to read on a tile seen from a seat (a tile is ~30 px on a 1080p screen).
            Paint("Fb2Frame", TileW, TileH, (x, y) => Band(TileDistance(x, y) + 7f, 7f));
            Paint("Fb2Corners", TileW, TileH, (x, y) =>
            {
                const float length = 66f;
                bool nearX = x < Inset + length || x > TileW - Inset - length;
                bool nearY = y < Inset + length || y > TileH - Inset - length;
                return nearX && nearY ? Band(TileDistance(x, y) + 10f, 10f) : 0f;
            });
            Paint("Fb2Stripe", 256, 24, (x, y) => (Mathf.Repeat((x + y) / 10f, 2f) < 1f ? 1f : 0.4f));
            Paint("Fb2Bang", 96, 96, (x, y) =>
            {
                float box = RoundRect(x, y, 48f, 48f, 44f, 44f, 14f);
                float mark = Mathf.Min(Segment(x, y, 48f, 70f, 48f, 40f) - 6f, Circle(x, y, 48f, 25f, 7f));
                return Mathf.Clamp01(0.5f - box) * Mathf.Clamp01(mark + 0.5f);
            });
            Paint("Fb2Ban", 128, 128, (x, y) =>
            {
                float ring = Mathf.Abs(Circle(x, y, 64f, 64f, 48f)) - 8f;
                float slash = Segment(x, y, 30f, 98f, 98f, 30f) - 8f;
                return Mathf.Clamp01(0.5f - Mathf.Min(ring, slash));
            });
            Paint("Fb2Crack", TileW, TileH, (x, y) =>
            {
                float d = Mathf.Min(
                    Mathf.Min(Segment(x, y, 40f, 320f, 100f, 230f), Segment(x, y, 100f, 230f, 84f, 186f)),
                    Mathf.Min(Mathf.Min(Segment(x, y, 84f, 186f, 140f, 110f), Segment(x, y, 140f, 110f, 218f, 70f)),
                        Mathf.Min(Segment(x, y, 100f, 230f, 186f, 250f), Segment(x, y, 186f, 250f, 214f, 300f))));
                return TileDistance(x, y) < 0f ? Mathf.Clamp01(6f - d) * 0.9f : 0f;
            });
            Paint("Fb2Dot", 64, 64, (x, y) =>
            {
                float d = Circle(x, y, 32f, 32f, 14f);
                return Mathf.Clamp01(0.5f - d) * 0.75f + (Mathf.Clamp01(1f - ((d + 14f) / 30f)) * 0.25f);
            });
            Paint("Fb2Sweep", TileW, TileH, (x, y) =>
            {
                if (TileDistance(x, y) > 0f) return 0f;
                float u = x / (float)TileW;
                float v = y / (float)TileH;
                float s = (u - v) / 0.16f;
                return Mathf.Exp(-(s * s)) * 0.85f;
            });
            Paint("Fb2Chevron", 128, 128, (x, y) =>
            {
                float d = Mathf.Min(Segment(x, y, 24f, 44f, 64f, 100f), Segment(x, y, 64f, 100f, 104f, 44f));
                return Mathf.Clamp01(13f - d);
            });

            Material fill = MaterialFor("Fb2Fill");
            Material frame = MaterialFor("Fb2Frame");
            Material corners = MaterialFor("Fb2Corners");
            Material stripe = MaterialFor("Fb2Stripe");
            Material bang = MaterialFor("Fb2Bang");
            Material ban = MaterialFor("Fb2Ban");
            Material crack = MaterialFor("Fb2Crack");
            Material dot = MaterialFor("Fb2Dot");
            Material sweep = MaterialFor("Fb2Sweep");
            Material chevron = MaterialFor("Fb2Chevron");
            Material pathLine = MaterialFor("Fb2PathLine", false);
            Vector2 tile = new Vector2(TileWidth, TileDepth);

            // 1 Turn piece: a glow in the seat colour inside the tile, breathing.
            BoardFeedbackMarker turn = Marker("Fb2_TurnPiece", false, m =>
            {
                Flat("Glow", m.transform, fill, tile, 0f, Vector2.zero);
                Flat("Frame", m.transform, frame, tile, 0.002f, Vector2.zero);
            }, 3.9f, 0f, 0.4f);
            // 2 Target: a teal tint with four corner brackets.
            BoardFeedbackMarker target = Marker("Fb2_Target", false, m =>
            {
                Flat("Tint", m.transform, fill, tile, 0f, Vector2.zero);
                Flat("Corners", m.transform, corners, tile, 0.002f, Vector2.zero);
            }, 3.9f, 0.015f, 0.35f);
            // 4 Reach: a faint dot in the middle of the tile.
            BoardFeedbackMarker reach = Marker("Fb2_Reach", false, m =>
                Flat("Dot", m.transform, dot, new Vector2(0.2f, 0.2f), 0f, Vector2.zero), 2.5f, 0f, 0.25f);
            // 5 Danger: a frame with a striped far edge and a "!" in the corner, flat on the tile.
            BoardFeedbackMarker danger = Marker("Fb2_Danger", false, m =>
            {
                Flat("Frame", m.transform, frame, tile, 0f, Vector2.zero);
                Flat("Stripe", m.transform, stripe, new Vector2(TileWidth - 0.05f, 0.07f), 0.002f, new Vector2(0f, (TileDepth * 0.5f) - 0.06f));
                Flat("Bang", m.transform, bang, new Vector2(0.16f, 0.16f), 0.004f, new Vector2((TileWidth * 0.5f) - 0.12f, (TileDepth * 0.5f) - 0.2f));
            }, 3.9f, 0f, 0.3f);
            // 6 Disabled: a grey tint with a crossed-out ring.
            BoardFeedbackMarker disabled = Marker("Fb2_Disabled", false, m =>
            {
                Flat("Tint", m.transform, fill, tile, 0f, Vector2.zero);
                Flat("Ban", m.transform, ban, new Vector2(0.3f, 0.3f), 0.003f, Vector2.zero);
            }, 0f, 0f, 0f);
            // 7 Event: a strong glow, a frame and cracks across the tile.
            BoardFeedbackMarker eventMark = Marker("Fb2_Event", false, m =>
            {
                Flat("Glow", m.transform, fill, tile * 1.06f, 0f, Vector2.zero);
                Flat("Frame", m.transform, frame, tile, 0.002f, Vector2.zero);
                Flat("Crack", m.transform, crack, tile, 0.004f, Vector2.zero);
            }, 5f, 0.03f, 0.35f);
            // 8 Flip: one light sweep across the tile.
            BoardFeedbackMarker flip = Marker("Fb2_Flip", true, m =>
                Flat("Sweep", m.transform, sweep, tile, 0f, Vector2.zero), 0f, 0f, 0f, 0.35f, 1.02f);
            // 9 Hover: a thin frame (white by meaning).
            BoardFeedbackMarker hover = Marker("Fb2_Hover", false, m =>
                Flat("Frame", m.transform, frame, tile, 0f, Vector2.zero), 0f, 0f, 0f);
            // 10 Landing: a faint fill and a frame where the carried piece will snap.
            BoardFeedbackMarker landing = Marker("Fb2_Landing", false, m =>
            {
                Flat("Shadow", m.transform, fill, tile, 0f, Vector2.zero);
                Flat("Frame", m.transform, frame, tile, 0.002f, Vector2.zero);
            }, 3.9f, 0f, 0.3f);
            // Selection: corner brackets around each selected piece.
            BoardFeedbackMarker selected = Marker("Fb2_Selection", false, m =>
                Flat("Corners", m.transform, corners, tile * 1.04f, 0f, Vector2.zero), 0f, 0f, 0f);
            // 3 Path: a solid line with a chevron at the end.
            BoardFeedbackArrow path = Path(pathLine, chevron);

            BoardFeedbackSet set = AssetDatabase.LoadAssetAtPath<BoardFeedbackSet>(SetPath);
            if (set == null)
            {
                Debug.LogWarning($"[Board Feedback v2] No {SetPath}: run Create Missing Board Feedback Prefabs first.");
                return;
            }

            SerializedObject so = new SerializedObject(set);
            so.FindProperty("activePawnRing").objectReferenceValue = turn;
            so.FindProperty("moveTarget").objectReferenceValue = target;
            so.FindProperty("reachTile").objectReferenceValue = reach;
            so.FindProperty("trapWarning").objectReferenceValue = danger;
            so.FindProperty("disabledMark").objectReferenceValue = disabled;
            so.FindProperty("collapseTarget").objectReferenceValue = eventMark;
            so.FindProperty("flipFlash").objectReferenceValue = flip;
            so.FindProperty("hoverOutline").objectReferenceValue = hover;
            so.FindProperty("landingGhost").objectReferenceValue = landing;
            so.FindProperty("selectionOutline").objectReferenceValue = selected;
            so.FindProperty("moveArrow").objectReferenceValue = path;
            // A danger mark lies on its tile now (it used to stand above it and hide pieces).
            so.FindProperty("trapWarningLift").floatValue = so.FindProperty("tileMarkerLift").floatValue;
            so.FindProperty("coloursByMeaning").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[Board Feedback v2] Board 18 marks made and set on BoardFeedbackSet (colours by meaning).");
        }

        // ---------- pictures ----------

        private static void Paint(string name, int width, int height, Func<float, float, float> alpha)
        {
            string path = TexturePath + name + ".png";
            {
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                        pixels[(y * width) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        // Signed distance to the tile's rounded outline (negative inside), in pixels.
        private static float TileDistance(float x, float y) =>
            RoundRect(x, y, TileW * 0.5f, TileH * 0.5f, (TileW * 0.5f) - Inset, (TileH * 0.5f) - Inset, Corner);

        private static float RoundRect(float x, float y, float cx, float cy, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x - cx) - (halfW - radius);
            float qy = Mathf.Abs(y - cy) - (halfH - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static float Circle(float x, float y, float cx, float cy, float radius) =>
            new Vector2(x - cx, y - cy).magnitude - radius;

        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            Vector2 p = new Vector2(x - ax, y - ay);
            Vector2 b = new Vector2(bx - ax, by - ay);
            float k = Mathf.Clamp01(Vector2.Dot(p, b) / Mathf.Max(1e-4f, b.sqrMagnitude));
            return (p - (b * k)).magnitude;
        }

        // A line of the given half width along a distance field, with a soft pixel edge.
        private static float Band(float distance, float halfWidth) => Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(distance));

        // ---------- materials and prefabs ----------

        private static Material MaterialFor(string name, bool textured = true)
        {
            string path = MaterialPath + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{ShaderName}' is missing.");
            Material material = new Material(shader) { name = name };
            if (textured) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + name + ".png"));
            material.SetColor("_BaseColor", Color.white);
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

        private static BoardFeedbackMarker Marker(string name, bool oneShot, Action<GameObject> build, float pulseSpeed,
            float pulseScale, float pulseAlpha, float oneShotSeconds = 0.6f, float grow = 1f)
        {
            string path = PrefabPath + name + ".prefab";
            BoardFeedbackMarker existing = AssetDatabase.LoadAssetAtPath<BoardFeedbackMarker>(path);
            if (existing != null) return existing;
            GameObject root = new GameObject(name);
            try
            {
                build(root);
                BoardFeedbackMarker marker = root.AddComponent<BoardFeedbackMarker>();
                SerializedObject so = new SerializedObject(marker);
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                SerializedProperty list = so.FindProperty("renderers");
                list.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                so.FindProperty("pulseSpeed").floatValue = pulseSpeed;
                so.FindProperty("pulseScale").floatValue = pulseScale;
                so.FindProperty("pulseAlpha").floatValue = pulseAlpha;
                so.FindProperty("bobHeight").floatValue = 0f;
                so.FindProperty("spinDegreesPerSecond").floatValue = 0f;
                so.FindProperty("oneShot").boolValue = oneShot;
                so.FindProperty("oneShotSeconds").floatValue = oneShotSeconds;
                so.FindProperty("oneShotGrow").floatValue = grow;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BoardFeedbackMarker>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A quad lying flat (its picture faces up; the picture's top is the far side), moved across the tile.
        private static Transform Flat(string name, Transform parent, Material material, Vector2 size, float lift, Vector2 offset)
        {
            GameObject quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(offset.x, lift, offset.y);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        private static BoardFeedbackArrow Path(Material line, Material head)
        {
            string path = PrefabPath + "Fb2_Path.prefab";
            BoardFeedbackArrow existing = AssetDatabase.LoadAssetAtPath<BoardFeedbackArrow>(path);
            if (existing != null) return existing;
            GameObject root = new GameObject("Fb2_Path");
            try
            {
                LineRenderer renderer = root.AddComponent<LineRenderer>();
                renderer.sharedMaterial = line;
                renderer.textureMode = LineTextureMode.Stretch;
                renderer.widthMultiplier = 0.06f;
                renderer.numCapVertices = 4;
                renderer.useWorldSpace = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.alignment = LineAlignment.View;
                Transform chevron = Flat("Head", root.transform, head, new Vector2(0.3f, 0.3f), 0f, Vector2.zero);
                BoardFeedbackArrow arrow = root.AddComponent<BoardFeedbackArrow>();
                SerializedObject so = new SerializedObject(arrow);
                so.FindProperty("line").objectReferenceValue = renderer;
                so.FindProperty("head").objectReferenceValue = chevron;
                so.FindProperty("headRenderer").objectReferenceValue = chevron.GetComponent<Renderer>();
                so.FindProperty("arcHeight").floatValue = 0.15f;
                so.FindProperty("dashScrollSpeed").floatValue = 0f;
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
