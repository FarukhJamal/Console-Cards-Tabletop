using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.ConsoleMat
{
    /// <summary>
    /// Makes the catalog Console mat double-sided (doc 22 §14): adds the missing layout entries (four corner
    /// slots, the back's A/B/X/Y card slots and D-pad die spots, and the edge symbols), the matching slots and
    /// anchors on the Console prefab, and a stand-in mat with the front and back pictures that turns over.
    /// Only what is missing is added; nothing that exists is overwritten. Run it once before Play.
    /// </summary>
    public static class ConsoleMatUpgradeTool
    {
        public const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Real/ConsoleMat.prefab";
        public const string FrontTexturePath = "Assets/ConsoleCards/Content/Art/Console/ConsoleMatFront.png";
        public const string BackTexturePath = "Assets/ConsoleCards/Content/Art/Console/ConsoleMatBack.png";
        public const string FrontMaterialPath = "Assets/ConsoleCards/Content/Materials/Console/ConsoleMatFront.mat";
        public const string BackMaterialPath = "Assets/ConsoleCards/Content/Materials/Console/ConsoleMatBack.mat";

        private const string FacesName = "MatFaces";

        // 1.0 keeps the corner slots of neighbouring seats in a four-seat ring apart (Trap Floor's seat check needs
        // 0.15). The first version used 1.2; such entries are corrected once.
        private const float CornerSize = 1.0f;
        private const float FirstCornerSize = 1.2f;
        private const float FaceGap = 0.002f;

        // Positions are console-local (origin at the mat centre, +x right, +z toward the top of the picture),
        // measured from the front and back pictures. Back slots are given as seen with the back facing up.
        private static readonly SlotEntry[] RequiredEntries =
        {
            new SlotEntry("CornerTL", ConsoleLayoutSlotKind.Card, "Corners", -3.05f, 1.85f, CornerSize, CornerSize, ConsoleMatSide.Front, ConsoleSymbol.Circle),
            new SlotEntry("CornerTR", ConsoleLayoutSlotKind.Card, "Corners", 3.05f, 1.85f, CornerSize, CornerSize, ConsoleMatSide.Front, ConsoleSymbol.Triangle),
            new SlotEntry("CornerBL", ConsoleLayoutSlotKind.Card, "Corners", -3.05f, -1.85f, CornerSize, CornerSize, ConsoleMatSide.Front, ConsoleSymbol.Plus),
            new SlotEntry("CornerBR", ConsoleLayoutSlotKind.Card, "Corners", 3.05f, -1.85f, CornerSize, CornerSize, ConsoleMatSide.Front, ConsoleSymbol.Minus),
            new SlotEntry("BtnB", ConsoleLayoutSlotKind.Card, "Buttons", 1.605f, 1.473f, 1.4f, 1.4f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("BtnA", ConsoleLayoutSlotKind.Card, "Buttons", 0.327f, 0f, 1.4f, 1.4f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("BtnY", ConsoleLayoutSlotKind.Card, "Buttons", 2.883f, 0f, 1.4f, 1.4f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("BtnX", ConsoleLayoutSlotKind.Card, "Buttons", 1.605f, -1.459f, 1.4f, 1.4f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("PadUp", ConsoleLayoutSlotKind.Die, "DPad", -1.611f, 0.628f, 0.6f, 0.6f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("PadLeft", ConsoleLayoutSlotKind.Die, "DPad", -2.235f, 0f, 0.6f, 0.6f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("PadRight", ConsoleLayoutSlotKind.Die, "DPad", -0.99f, 0f, 0.6f, 0.6f, ConsoleMatSide.Back, ConsoleSymbol.None),
            new SlotEntry("PadDown", ConsoleLayoutSlotKind.Die, "DPad", -1.611f, -0.664f, 0.6f, 0.6f, ConsoleMatSide.Back, ConsoleSymbol.None),
        };

        // Symbols of the slots the mat already had (top edge squares, bottom edge diamonds, star left, cross right).
        private static readonly Dictionary<string, ConsoleSymbol> ExistingSymbols = new Dictionary<string, ConsoleSymbol>
        {
            { "TopL", ConsoleSymbol.Square },
            { "TopC", ConsoleSymbol.Square },
            { "TopR", ConsoleSymbol.Square },
            { "BotL", ConsoleSymbol.Diamond },
            { "BotC", ConsoleSymbol.Diamond },
            { "BotR", ConsoleSymbol.Diamond },
            { "RailL", ConsoleSymbol.Star },
            { "RailR", ConsoleSymbol.Cross },
        };

        [MenuItem("Console Cards/Console/Make Console Mat Double-Sided")]
        public static void MakeDoubleSided()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            ConsoleLayoutBinding binding = prefab != null ? prefab.GetComponent<ConsoleLayoutBinding>() : null;
            if (binding == null || binding.Layout == null)
            {
                Debug.LogWarning($"[Console] {PrefabPath} with a Console layout was not found.");
                return;
            }

            ConsoleLayoutDefinition layout = binding.Layout;
            int addedEntries = AddMissingLayoutEntries(layout, out int symbolsSet);
            int correctedCorners = CorrectCornerSizes(layout);
            ConsoleLayoutData layoutData = layout.ToData();

            Material front = EnsureMaterial(FrontMaterialPath, FrontTexturePath);
            Material back = EnsureMaterial(BackMaterialPath, BackTexturePath);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            int addedSlots;
            bool addedFaces;
            int syncedSlots;
            try
            {
                addedSlots = AddMissingSlots(root, layoutData);
                syncedSlots = SyncSlotsToLayout(root, layoutData, false);
                addedFaces = AddFaces(root, layoutData, front, back);
                if (addedSlots > 0 || syncedSlots > 0 || addedFaces)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Console] Double-sided mat: {addedEntries} layout entries added, {symbolsSet} edge symbols set, "
                + $"{correctedCorners} corner sizes corrected, {addedSlots} slots added and {syncedSlots} moved to match the layout, {(addedFaces ? "front and back pictures added" : "mat pictures already there")}."
                + (front == null || back == null ? " The front or back picture is missing; the mat shows without it." : string.Empty));
        }

        private static int AddMissingLayoutEntries(ConsoleLayoutDefinition layout, out int symbolsSet)
        {
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            HashSet<string> keys = new HashSet<string>();
            int maxOrdinal = 0;
            symbolsSet = 0;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                string key = slot.FindPropertyRelative("key").stringValue;
                keys.Add(key);
                maxOrdinal = Mathf.Max(maxOrdinal, slot.FindPropertyRelative("ordinal").intValue);
                SerializedProperty symbol = slot.FindPropertyRelative("symbol");
                if (ExistingSymbols.TryGetValue(key, out ConsoleSymbol wanted) && symbol.intValue == (int)ConsoleSymbol.None)
                {
                    symbol.intValue = (int)wanted;
                    symbolsSet++;
                }
            }

            int added = 0;
            foreach (SlotEntry entry in RequiredEntries)
            {
                if (keys.Contains(entry.Key)) continue;
                slots.arraySize++;
                SerializedProperty slot = slots.GetArrayElementAtIndex(slots.arraySize - 1);
                slot.FindPropertyRelative("ordinal").intValue = ++maxOrdinal;
                slot.FindPropertyRelative("key").stringValue = entry.Key;
                slot.FindPropertyRelative("kind").intValue = (int)entry.Kind;
                slot.FindPropertyRelative("group").stringValue = entry.Group;
                slot.FindPropertyRelative("x").floatValue = entry.X;
                slot.FindPropertyRelative("z").floatValue = entry.Z;
                slot.FindPropertyRelative("footprintWidth").floatValue = entry.Width;
                slot.FindPropertyRelative("footprintDepth").floatValue = entry.Depth;
                slot.FindPropertyRelative("defaultCapacity").intValue = 0;
                slot.FindPropertyRelative("marker").boolValue = false;
                slot.FindPropertyRelative("defaultOrientation").intValue = entry.Kind == ConsoleLayoutSlotKind.Card
                    ? (int)ConsoleSlotOrientation.Portrait
                    : (int)ConsoleSlotOrientation.None;
                slot.FindPropertyRelative("side").intValue = (int)entry.Side;
                slot.FindPropertyRelative("symbol").intValue = (int)entry.Symbol;
                added++;
            }

            if (added > 0 || symbolsSet > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(layout);
            }

            return added;
        }

        private static int CorrectCornerSizes(ConsoleLayoutDefinition layout)
        {
            SerializedObject so = new SerializedObject(layout);
            SerializedProperty slots = so.FindProperty("slots");
            int corrected = 0;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                if (!slot.FindPropertyRelative("key").stringValue.StartsWith("Corner")) continue;
                SerializedProperty width = slot.FindPropertyRelative("footprintWidth");
                SerializedProperty depth = slot.FindPropertyRelative("footprintDepth");
                if (!Mathf.Approximately(width.floatValue, FirstCornerSize) || !Mathf.Approximately(depth.floatValue, FirstCornerSize)) continue;
                width.floatValue = CornerSize;
                depth.floatValue = CornerSize;
                corrected++;
            }

            if (corrected > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(layout);
            }

            return corrected;
        }

        /// <summary>
        /// Puts every Console slot and its anchor where the layout says, with the layout's footprint (collider and
        /// feedback plates). The layout asset is the one source; the prefab follows it. Returns how many slots
        /// changed. With recordUndo, the change can be undone in the Editor (used by the Scene view editor).
        /// </summary>
        public static int SyncSlotsToLayout(GameObject root, ConsoleLayoutData layout, bool recordUndo)
        {
            ConsoleView view = root.GetComponent<ConsoleView>();
            if (view == null) return 0;
            List<ConsoleSlotView> slotViews = new List<ConsoleSlotView>(root.GetComponentsInChildren<ConsoleSlotView>(true));
            slotViews.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            IReadOnlyList<ConsoleLayoutSlotData> cardSlots = layout.CardSlots;
            IReadOnlyList<Transform> anchors = view.SlotAnchors;
            int changed = 0;
            for (int i = 0; i < slotViews.Count && i < cardSlots.Count; i++)
            {
                ConsoleLayoutSlotData data = cardSlots[i];
                Transform slot = slotViews[i].transform;
                Vector3 slotPosition = new Vector3(data.X, slot.localPosition.y, data.Z);
                BoxCollider collider = slot.GetComponent<BoxCollider>();
                bool slotMoved = (slot.localPosition - slotPosition).sqrMagnitude > 0.000001f;
                bool resized = collider != null
                    && (Mathf.Abs(collider.size.x - data.FootprintWidth) > 0.0005f || Mathf.Abs(collider.size.z - data.FootprintDepth) > 0.0005f);
                Transform anchor = i < anchors.Count ? anchors[i] : null;
                Vector3 anchorPosition = anchor != null ? new Vector3(data.X, anchor.localPosition.y, data.Z) : Vector3.zero;
                bool anchorMoved = anchor != null && (anchor.localPosition - anchorPosition).sqrMagnitude > 0.000001f;
                if (!slotMoved && !resized && !anchorMoved) continue;

                if (recordUndo)
                {
                    Undo.RecordObject(slot, "Move Console slot");
                    if (collider != null) Undo.RecordObject(collider, "Resize Console slot");
                    if (anchor != null) Undo.RecordObject(anchor, "Move Console slot");
                    foreach (Transform child in slot) Undo.RecordObject(child, "Resize Console slot");
                }

                slot.localPosition = slotPosition;
                if (anchor != null) anchor.localPosition = anchorPosition;
                ShapeSlot(slot, data);
                changed++;
            }

            return changed;
        }

        // One slot (a Console Slot prefab instance, like the existing ones) and one anchor per layout Card slot.
        private static int AddMissingSlots(GameObject root, ConsoleLayoutData layout)
        {
            ConsoleView view = root.GetComponent<ConsoleView>();
            List<ConsoleSlotView> slotViews = new List<ConsoleSlotView>(root.GetComponentsInChildren<ConsoleSlotView>(true));
            slotViews.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            IReadOnlyList<ConsoleLayoutSlotData> cardSlots = layout.CardSlots;
            if (view == null || slotViews.Count == 0 || slotViews.Count >= cardSlots.Count) return 0;

            GameObject slotSource = PrefabUtility.GetCorrespondingObjectFromSource(slotViews[0].gameObject);
            if (slotSource == null)
            {
                Debug.LogWarning("[Console] The existing Console slots are not prefab instances; no slots were added.");
                return 0;
            }

            SerializedObject viewSo = new SerializedObject(view);
            SerializedProperty anchors = viewSo.FindProperty("slotAnchors");
            Transform anchorParent = anchors.arraySize > 0 && anchors.GetArrayElementAtIndex(0).objectReferenceValue is Transform first
                ? first.parent
                : root.transform;
            int sibling = slotViews[slotViews.Count - 1].transform.GetSiblingIndex();
            int added = 0;
            for (int i = slotViews.Count; i < cardSlots.Count; i++)
            {
                ConsoleLayoutSlotData data = cardSlots[i];
                GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(slotSource, root.transform);
                slot.name = "Slot_" + data.Key;
                slot.transform.localPosition = new Vector3(data.X, 0.01f, data.Z);
                slot.transform.localRotation = Quaternion.identity;
                slot.transform.SetSiblingIndex(++sibling);
                ShapeSlot(slot.transform, data);

                GameObject anchor = new GameObject("Anchor_" + data.Key);
                anchor.layer = root.layer;
                anchor.transform.SetParent(anchorParent, false);
                anchor.transform.localPosition = new Vector3(data.X, 0f, data.Z);
                anchors.arraySize++;
                anchors.GetArrayElementAtIndex(anchors.arraySize - 1).objectReferenceValue = anchor.transform;
                added++;
            }

            viewSo.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        // Matches the existing slots: no bezel or recess, the target and feedback plates sized to the footprint.
        private static void ShapeSlot(Transform slot, ConsoleLayoutSlotData data)
        {
            SetActive(slot, "OuterBezel", false);
            SetActive(slot, "Recess", false);
            ScaleFlat(slot, "ValidTargetFeedbackRoot", data.FootprintWidth, data.FootprintDepth);
            ScaleFlat(slot, "SourceTargetFeedbackRoot", data.FootprintWidth, data.FootprintDepth);
            ScaleFlat(slot, "InvalidTargetFeedbackRoot", data.FootprintWidth, data.FootprintDepth);
            BoxCollider collider = slot.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.size = new Vector3(data.FootprintWidth, 0.02f, data.FootprintDepth);
                collider.center = new Vector3(0f, 0.01f, 0f);
            }
        }

        private static void SetActive(Transform slot, string child, bool active)
        {
            Transform found = slot.Find(child);
            if (found != null) found.gameObject.SetActive(active);
        }

        private static void ScaleFlat(Transform slot, string child, float width, float depth)
        {
            Transform found = slot.Find(child);
            if (found != null) found.localScale = new Vector3(width, found.localScale.y, depth);
        }

        // The stand-in double-sided mat: two pictures back to back on a pivot that turns over. The old mat model
        // stays (its colliders still hold pieces up) but is no longer drawn. Replace it with the new model later.
        private static bool AddFaces(GameObject root, ConsoleLayoutData layout, Material front, Material back)
        {
            if (root.GetComponent<ConsoleMatFaces>() != null || root.transform.Find(FacesName) != null) return false;

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            for (int c = 0; c < root.transform.childCount; c++)
            {
                Transform child = root.transform.GetChild(c);
                if (child.GetComponent<ConsoleSlotView>() != null || child.name == "SlotAnchors") continue;
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds local = ToLocal(root.transform, renderer.bounds);
                    if (hasBounds) bounds.Encapsulate(local);
                    else
                    {
                        bounds = local;
                        hasBounds = true;
                    }

                    renderer.enabled = false;
                }
            }

            float top = hasBounds ? bounds.max.y : 0.02f;
            float bottom = hasBounds ? bounds.min.y : 0f;
            float half = Mathf.Max(0.001f, (top - bottom) * 0.5f) + FaceGap;

            GameObject pivot = new GameObject(FacesName);
            pivot.layer = root.layer;
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            pivot.transform.SetSiblingIndex(1);
            CreateFace(pivot.transform, "Front", new Vector3(0f, half, 0f), Quaternion.Euler(90f, 0f, 0f), layout, front);
            CreateFace(pivot.transform, "Back", new Vector3(0f, -half, 0f), Quaternion.Euler(270f, 0f, 0f), layout, back);

            ConsoleMatFaces faces = root.AddComponent<ConsoleMatFaces>();
            SerializedObject so = new SerializedObject(faces);
            so.FindProperty("turnPivot").objectReferenceValue = pivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void CreateFace(Transform pivot, string name, Vector3 position, Quaternion rotation, ConsoleLayoutData layout, Material material)
        {
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = name;
            face.layer = pivot.gameObject.layer;
            Collider collider = face.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            face.transform.SetParent(pivot, false);
            face.transform.localPosition = position;
            face.transform.localRotation = rotation;
            face.transform.localScale = new Vector3(layout.MatWidth, layout.MatDepth, 1f);
            MeshRenderer renderer = face.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Bounds ToLocal(Transform root, Bounds world)
        {
            Vector3 min = world.min;
            Vector3 max = world.max;
            Bounds local = new Bounds(root.InverseTransformPoint(min), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) != 0 ? max.x : min.x, (i & 2) != 0 ? max.y : min.y, (i & 4) != 0 ? max.z : min.z);
                local.Encapsulate(root.InverseTransformPoint(corner));
            }

            return local;
        }

        // A lit, alpha-clipped material for one mat picture (the cut corners are transparent). Never overwritten.
        private static Material EnsureMaterial(string materialPath, string texturePath)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null) return existing;

            Texture2D texture = PrepareTexture(texturePath);
            if (texture == null) return null;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Console] The URP Lit shader was not found; the mat pictures were not made.");
                return null;
            }

            Material material = new Material(shader) { name = Path.GetFileNameWithoutExtension(materialPath) };
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Smoothness", 0.15f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }

        private static Texture2D PrepareTexture(string texturePath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Console] {texturePath} was not found.");
                return null;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        private readonly struct SlotEntry
        {
            public SlotEntry(string key, ConsoleLayoutSlotKind kind, string group, float x, float z, float width, float depth, ConsoleMatSide side, ConsoleSymbol symbol)
            {
                Key = key;
                Kind = kind;
                Group = group;
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
                Side = side;
                Symbol = symbol;
            }

            public string Key { get; }
            public ConsoleLayoutSlotKind Kind { get; }
            public string Group { get; }
            public float X { get; }
            public float Z { get; }
            public float Width { get; }
            public float Depth { get; }
            public ConsoleMatSide Side { get; }
            public ConsoleSymbol Symbol { get; }
        }
    }
}
