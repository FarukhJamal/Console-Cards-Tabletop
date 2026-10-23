using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Catalog;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The card front's own layers (ICON-1, FRAME-1; doc 31 PL-ICONS, PL-FRAMES), drawn by the platform over the card's
    /// picture: the game's <b>frame</b> (border with a clear middle), a <b>tab</b> sticking out past the edge at every
    /// side or corner that has an icon, and the shared <b>symbol</b> in each tab. Everything comes from the game's
    /// <see cref="CardFrameStyle"/> and the card's frame variant and icons, so a new frame picture, other colours or
    /// other symbols change every card without code. One child mesh per card (4 parts: frame, side tabs, corner tabs,
    /// symbols) with shared materials; meshes are shared by cards with the same icons and frame. Nothing per frame.
    /// Without a frame style (or a variant with the frame off) the icons sit inside the card as small badges.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const string CardIconsObjectName = CardVisualReferences.IconObjectName;
        // Badges without a frame: size and inset from the edge, as parts of the card's shorter side.
        private const float CardIconSizePart = 0.17f;
        private const float CardIconInsetPart = 0.05f;
        // Heights above the face: words 0.0001, then frame, tabs and symbols; all under the next card in a pile.
        private const float CardFrameLift = 0.00012f;
        private const float CardTabLift = 0.0002f;
        private const float CardSymbolLift = 0.00028f;
        // BUILD-1: the alpha-clipped material the frame, tabs and symbols copy. It lives in Content/Resources so a
        // player build keeps the shader's alpha-clip variant (a keyword switched on only from code is stripped).
        private const string CardFaceLayerResourcePath = "CardFrames/CardFaceLayer";
        private const int CardFacePartCount = 4;

        private readonly Dictionary<string, Mesh> cardIconMeshes = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        private readonly Dictionary<(Texture, Color), Material> cardFaceLayerMaterials =
            new Dictionary<(Texture, Color), Material>();
        private readonly Dictionary<(CardFrameStyle, int, Texture), Material[]> cardFaceMaterialSets =
            new Dictionary<(CardFrameStyle, int, Texture), Material[]>();
        private Material cardFaceLayerBase;
        private readonly Vector3[] cardIconCorners = new Vector3[8];

        private readonly List<Vector3> faceVertices = new List<Vector3>(64);
        private readonly List<Vector2> faceUvs = new List<Vector2>(64);
        private readonly List<int>[] faceTriangles =
        {
            new List<int>(6), new List<int>(24), new List<int>(24), new List<int>(48),
        };

        // Shows the card's frame, tabs and icons, or hides them. Called whenever the card's look is set. A null
        // definition (a floor tile, an avatar) has no frame.
        internal void ApplyCardIcons(CardVisualReferences references, CardDefinition definition, ObjectDefinitionId definitionId)
        {
            if (references == null || references.CardView == null || references.CardView.FaceUpVisualRoot == null)
            {
                return;
            }

            Transform faceRoot = references.CardView.FaceUpVisualRoot.transform;
            Transform existing = faceRoot.Find(CardIconsObjectName);
            CardFrameStyle style = definition != null ? CardFrameStyleOf(definitionId) : null;
            CardFrameVariant variant = style != null ? style.Variant(definition.FrameVariant) : null;
            bool framed = variant != null && variant.ShowFrame;
            string icons = definition != null ? definition.EdgeSymbolsText : string.Empty;
            Material[] materials = definition != null && (framed || icons.Length > 0)
                ? CardFaceMaterials(references.FaceUpRenderer, style, variant, framed)
                : null;
            if (materials == null || !TryGetCardIconFrame(references, faceRoot, out Vector2 half, out float top))
            {
                if (existing != null && existing.gameObject.activeSelf) existing.gameObject.SetActive(false);
                references.SetIconRenderer(null);
                return;
            }

            Mesh mesh = CardFaceMesh(definition, icons, framed ? style : null, half, top);
            GameObject iconObject = existing != null ? existing.gameObject : CreateCardIconObject(faceRoot);
            MeshFilter filter = iconObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = iconObject.GetComponent<MeshRenderer>();
            if (!ReferenceEquals(filter.sharedMesh, mesh)) filter.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            if (!iconObject.activeSelf) iconObject.SetActive(true);
            references.SetIconRenderer(renderer);
        }

        // The frame style of the game a card belongs to: this table's game (its own and your cards), else the game
        // box that has it, else the platform's default.
        internal CardFrameStyle CardFrameStyleOf(ObjectDefinitionId definitionId)
        {
            if (!definitionId.IsEmpty)
            {
                GameDefinition tableGame = TableGame;
                if (tableGame != null
                    && ((tableContent != null && tableContent.TryGetCard(definitionId, out _))
                        || tableGame.TryGetCard(definitionId, out _)))
                {
                    return tableGame.CardFrameStyle;
                }

                IReadOnlyList<ComponentCatalog> boxes = componentLibrary != null ? componentLibrary.GameBoxes : null;
                for (int i = 0; boxes != null && i < boxes.Count; i++)
                {
                    GameDefinition game = boxes[i] != null ? boxes[i].Game : null;
                    if (game != null && game.TryGetCard(definitionId, out _)) return game.CardFrameStyle;
                }
            }

            return CardFrameStyle.Default;
        }

        private static GameObject CreateCardIconObject(Transform faceRoot)
        {
            GameObject iconObject = new GameObject(CardIconsObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            iconObject.layer = faceRoot.gameObject.layer;
            Transform iconTransform = iconObject.transform;
            iconTransform.SetParent(faceRoot, false);
            iconTransform.localPosition = Vector3.zero;
            iconTransform.localRotation = Quaternion.identity;
            iconTransform.localScale = Vector3.one;
            MeshRenderer renderer = iconObject.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return iconObject;
        }

        // The card front in face-root space: half width (x) and half length (z) of the face plate, and the height of
        // its top surface (the supplied card model's top when it is higher).
        private bool TryGetCardIconFrame(
            CardVisualReferences references,
            Transform faceRoot,
            out Vector2 half,
            out float top)
        {
            half = Vector2.zero;
            top = float.NegativeInfinity;
            Renderer plate = references.FaceUpRenderer;
            MeshFilter plateFilter = plate != null ? plate.GetComponent<MeshFilter>() : null;
            if (plateFilter == null || plateFilter.sharedMesh == null)
            {
                return false;
            }

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            AddCardIconBounds(plateFilter, faceRoot, ref min, ref max);
            half = new Vector2(Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)), Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)));
            top = max.y;

            IReadOnlyList<Renderer> supplied = references.SuppliedModelRenderers;
            for (int i = 0; i < supplied.Count; i++)
            {
                Renderer model = supplied[i];
                if (model == null || !model.transform.IsChildOf(faceRoot)) continue;
                MeshFilter modelFilter = model.GetComponent<MeshFilter>();
                if (modelFilter == null || modelFilter.sharedMesh == null) continue;
                Vector3 modelMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 modelMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                AddCardIconBounds(modelFilter, faceRoot, ref modelMin, ref modelMax);
                top = Mathf.Max(top, modelMax.y);
            }

            return half.x > 0.0001f && half.y > 0.0001f && !float.IsInfinity(top);
        }

        private void AddCardIconBounds(MeshFilter filter, Transform faceRoot, ref Vector3 min, ref Vector3 max)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            int n = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        cardIconCorners[n++] = new Vector3(c.x + (x * e.x), c.y + (y * e.y), c.z + (z * e.z));
                    }
                }
            }

            Transform meshTransform = filter.transform;
            for (int i = 0; i < cardIconCorners.Length; i++)
            {
                Vector3 local = faceRoot.InverseTransformPoint(meshTransform.TransformPoint(cardIconCorners[i]));
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
        }

        // ---- the mesh: part 0 frame, 1 side tabs, 2 corner tabs, 3 symbols ----

        // Shared by every card with the same icons, frame sizes and face size. A null style: badges only.
        private Mesh CardFaceMesh(CardDefinition definition, string icons, CardFrameStyle style, Vector2 half, float top)
        {
            string key = style == null
                ? $"{icons}|badges|{half.x:F3}|{half.y:F3}|{top:F4}"
                : $"{icons}|{style.SideTabWidth:F3},{style.SideTabHeight:F3},{style.SideTabOutside:F3},"
                    + $"{style.CornerTabSize:F3},{style.CornerTabOutside:F3},{style.SymbolSize:F3}|{half.x:F3}|{half.y:F3}|{top:F4}";
            if (cardIconMeshes.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            faceVertices.Clear();
            faceUvs.Clear();
            for (int i = 0; i < faceTriangles.Length; i++) faceTriangles[i].Clear();
            float shorter = Mathf.Min(half.x, half.y) * 2f;
            if (style != null)
            {
                // The frame over the whole front; the picture's top is the card's top (+z).
                AddFaceQuad(0, new Vector3(-half.x, top + CardFrameLift, -half.y), new Vector3(2f * half.x, 0f, 0f),
                    new Vector3(0f, 0f, 2f * half.y), new Rect(0f, 0f, 1f, 1f));
            }

            for (int place = 0; place < CardDefinition.EdgeIconCount; place++)
            {
                ConsoleSymbol symbol = definition.EdgeSymbol((CardIconPlace)place);
                if (symbol == ConsoleSymbol.None) continue;
                Vector2 symbolCentre;
                float symbolSize;
                if (style != null)
                {
                    symbolCentre = AddCardTab((CardIconPlace)place, style, half, shorter, top + CardTabLift);
                    symbolSize = shorter * style.SymbolSize;
                }
                else
                {
                    symbolSize = shorter * CardIconSizePart;
                    float inset = (shorter * CardIconInsetPart) + (symbolSize * 0.5f);
                    symbolCentre = BadgeCentre((CardIconPlace)place, half.x - inset, half.y - inset);
                }

                // Symbols stay upright (their top toward the card's top), whatever side their tab is on.
                float h = symbolSize * 0.5f;
                AddFaceQuad(3, new Vector3(symbolCentre.x - h, top + CardSymbolLift, symbolCentre.y - h),
                    new Vector3(symbolSize, 0f, 0f), new Vector3(0f, 0f, symbolSize), CardIconAtlas.Cell(symbol));
            }

            Mesh mesh = new Mesh { name = "Card face " + icons };
            mesh.subMeshCount = CardFacePartCount;
            mesh.SetVertices(faceVertices);
            mesh.SetUVs(0, faceUvs);
            for (int part = 0; part < CardFacePartCount; part++) mesh.SetTriangles(faceTriangles[part], part);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cardIconMeshes[key] = mesh;
            return mesh;
        }

        // A tab at an icon place, sticking out past the edge or corner by the style's amount. The side tab picture
        // points up (base at its bottom); the corner tab picture is for the top right corner; both are turned (never
        // mirrored) for the other places. Returns the tab's middle, where its symbol goes.
        private Vector2 AddCardTab(CardIconPlace place, CardFrameStyle style, Vector2 half, float shorter, float y)
        {
            int side = (int)place;
            if (side < 4)
            {
                // Outward direction d and the direction along the edge t (d turned a quarter clockwise seen from above).
                Vector2 d = side == 0 ? new Vector2(0f, 1f) : side == 1 ? new Vector2(1f, 0f)
                    : side == 2 ? new Vector2(0f, -1f) : new Vector2(-1f, 0f);
                Vector2 t = new Vector2(d.y, -d.x);
                float edge = Mathf.Abs(d.x) > 0.5f ? half.x : half.y;
                float width = shorter * style.SideTabWidth;
                float height = shorter * style.SideTabHeight;
                float inside = (1f - style.SideTabOutside) * height;
                Vector2 origin = (d * (edge - inside)) - (t * (width * 0.5f));
                AddFaceQuad(1, new Vector3(origin.x, y, origin.y), new Vector3(t.x * width, 0f, t.y * width),
                    new Vector3(d.x * height, 0f, d.y * height), new Rect(0f, 0f, 1f, 1f));
                return d * (edge - inside + (height * 0.5f));
            }

            // Corners: quarter turns clockwise from the top right one (TopRight, BottomRight, BottomLeft, TopLeft).
            int turns = side - 4;
            Vector2 corner = new Vector2(turns == 0 || turns == 1 ? half.x : -half.x, turns == 0 || turns == 3 ? half.y : -half.y);
            float size = shorter * style.CornerTabSize;
            float back = (1f - style.CornerTabOutside) * size;
            Vector2 u = Turn(new Vector2(1f, 0f), turns);
            Vector2 v = Turn(new Vector2(0f, 1f), turns);
            Vector2 start = corner + Turn(new Vector2(-back, -back), turns);
            AddFaceQuad(2, new Vector3(start.x, y, start.y), new Vector3(u.x * size, 0f, u.y * size),
                new Vector3(v.x * size, 0f, v.y * size), new Rect(0f, 0f, 1f, 1f));
            float middle = (size * 0.5f) - back;
            return corner + Turn(new Vector2(middle, middle), turns);
        }

        // A vector turned clockwise (seen from above) by quarter turns: (x, z) -> (z, -x).
        private static Vector2 Turn(Vector2 value, int quarterTurns)
        {
            for (int i = 0; i < quarterTurns; i++) value = new Vector2(value.y, -value.x);
            return value;
        }

        // One flat quad facing up: origin is the picture's bottom left, across its bottom edge, up its left edge.
        private void AddFaceQuad(int part, Vector3 origin, Vector3 across, Vector3 up, Rect uv)
        {
            int first = faceVertices.Count;
            faceVertices.Add(origin);
            faceVertices.Add(origin + up);
            faceVertices.Add(origin + up + across);
            faceVertices.Add(origin + across);
            faceUvs.Add(new Vector2(uv.xMin, uv.yMin));
            faceUvs.Add(new Vector2(uv.xMin, uv.yMax));
            faceUvs.Add(new Vector2(uv.xMax, uv.yMax));
            faceUvs.Add(new Vector2(uv.xMax, uv.yMin));
            List<int> triangles = faceTriangles[part];
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private static Vector2 BadgeCentre(CardIconPlace place, float right, float up)
        {
            switch (place)
            {
                case CardIconPlace.Top: return new Vector2(0f, up);
                case CardIconPlace.Right: return new Vector2(right, 0f);
                case CardIconPlace.Bottom: return new Vector2(0f, -up);
                case CardIconPlace.Left: return new Vector2(-right, 0f);
                case CardIconPlace.TopRight: return new Vector2(right, up);
                case CardIconPlace.BottomRight: return new Vector2(right, -up);
                case CardIconPlace.BottomLeft: return new Vector2(-right, -up);
                default: return new Vector2(-right, up);
            }
        }

        // ---- materials ----

        // The 4 materials of a card front for one style variant: frame, side tabs, corner tabs (each the variant's
        // colour) and symbols (the style's symbol picture or the shared one). Null when nothing can be drawn.
        private Material[] CardFaceMaterials(Renderer faceRenderer, CardFrameStyle style, CardFrameVariant variant, bool framed)
        {
            Texture2D symbols = style != null && style.SymbolAtlas != null ? style.SymbolAtlas : CardIconAtlas.Texture;
            int variantIndex = framed ? Mathf.Max(0, style.IndexOf(variant.Id)) : -1;
            CardFrameStyle key = framed ? style : null;
            if (cardFaceMaterialSets.TryGetValue((key, variantIndex, symbols), out Material[] cached) && MaterialsAlive(cached))
            {
                return cached;
            }

            Material symbolMaterial = CardFaceLayerMaterial(faceRenderer, symbols, Color.white);
            if (symbolMaterial == null && !framed)
            {
                return null;
            }

            Material[] set = new Material[CardFacePartCount];
            set[0] = framed ? CardFaceLayerMaterial(faceRenderer, style.FrameTextureOf(variant), variant.FrameColor) : null;
            set[1] = framed ? CardFaceLayerMaterial(faceRenderer, style.SideTabTexture, variant.TabColor) : null;
            set[2] = framed ? CardFaceLayerMaterial(faceRenderer, style.CornerTabTexture, variant.TabColor) : null;
            set[3] = symbolMaterial;
            // A missing picture draws nothing for its part (an empty part costs nothing).
            Material any = set[0] ?? set[1] ?? set[2] ?? set[3];
            if (any == null)
            {
                return null;
            }

            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null) set[i] = CardFaceLayerMaterial(faceRenderer, null, Color.clear);
            }

            cardFaceMaterialSets[(key, variantIndex, symbols)] = set;
            return set;
        }

        private static bool MaterialsAlive(Material[] set)
        {
            if (set == null) return false;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] == null) return false;
            }

            return true;
        }

        // The card face's own material with a picture and tint, cut out where the picture is clear. A null picture
        // gives a fully cut-out material (used for parts that have no picture). One per picture and colour.
        private Material CardFaceLayerMaterial(Renderer faceRenderer, Texture texture, Color color)
        {
            if (texture == null && color != Color.clear)
            {
                return null;
            }

            if (cardFaceLayerMaterials.TryGetValue((texture, color), out Material cached) && cached != null)
            {
                return cached;
            }

            if (cardFaceLayerBase == null)
            {
                Material shipped = Resources.Load<Material>(CardFaceLayerResourcePath);
                cardFaceLayerBase = shipped != null
                    ? shipped
                    : faceRenderer != null ? CardArtBaseMaterial(faceRenderer.sharedMaterial) : null;
            }

            if (cardFaceLayerBase == null)
            {
                return null;
            }

            Material material = new Material(cardFaceLayerBase)
            {
                name = texture != null ? $"Card Face Layer ({texture.name})" : "Card Face Layer (none)",
            };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            cardFaceLayerMaterials[(texture, color)] = material;
            return material;
        }

        private void ReleaseCardIconAssets()
        {
            foreach (Mesh mesh in cardIconMeshes.Values)
            {
                if (mesh != null) Destroy(mesh);
            }

            cardIconMeshes.Clear();
            foreach (Material material in cardFaceLayerMaterials.Values)
            {
                if (material != null) Destroy(material);
            }

            cardFaceLayerMaterials.Clear();
            cardFaceMaterialSets.Clear();
            cardFaceLayerBase = null;
        }
    }
}
