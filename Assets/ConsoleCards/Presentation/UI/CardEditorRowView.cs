using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One card in the card editor's list: thumbnail, name and what it is; click to edit it (L2-2).</summary>
    public sealed class CardEditorRowView : ReusableUiView
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image selectedFill;
        [SerializeField] private RawImage thumbnail;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.953f, 0.769f, 1f);
        // Boards 10 and 11 (owner, 2026-10-30): the chosen row has a cyan edge and bar. Optional.
        [SerializeField] private GameObject selectedBar;
        [SerializeField] private Outline[] edges = Array.Empty<Outline>();
        [SerializeField] private Color edgeColor = new Color(0.259f, 0.271f, 0.278f, 1f);
        [SerializeField] private Color selectedEdgeColor = new Color(0.004f, 0.969f, 0.976f, 1f);

        /// <summary>The row's card (filters in the Card studio read it).</summary>
        public CardEditorRowModel Row { get; private set; }

        private string cardId;
        private Action<string> select;

        // DECKTREE-1 (owner, 2026-11-02: "the parent should be the game, and under it the child things"): a child row's
        // words move right by this much per level. The authored places are read once.
        private const float IndentPerLevel = 22f;
        private bool placesRead;
        private float nameX;
        private float detailX;

        // DECKTREE-2 (owner, 2026-11-02: "update the UI so that it looks like a tree"): the tree's lines, drawn in the
        // row's left margin: a trunk down from a parent, and for each child a line in from the trunk (├ or └).
        private const float TrunkOffset = 16f;  // the trunk sits this far left of the words of a top row
        private const float LineWidth = 2f;
        private const float RowGap = 6f;        // how far a line reaches past the row, across the gap to the next
        private static readonly Color TreeLine = new Color(0.38f, 0.42f, 0.44f, 1f);
        private static readonly Color TreeNode = new Color(0.004f, 0.969f, 0.976f, 1f);
        private float wordsLeft;
        private RectTransform trunkUp;
        private RectTransform trunkDown;
        private RectTransform branch;
        private RectTransform node;

        public void Bind(CardEditorRowModel row, Action<string> selectAction)
        {
            RequireAcquired();
            if (selectButton == null || selectedFill == null || thumbnail == null || nameLabel == null || detailLabel == null)
            {
                throw new InvalidOperationException("CardEditorRowView requires its authored references.");
            }

            cardId = row.Id;
            Row = row;
            select = selectAction;
            selectedFill.color = row.IsSelected ? selectedColor : normalColor;
            if (selectedBar != null) selectedBar.SetActive(row.IsSelected);
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i] != null) edges[i].effectColor = row.IsSelected ? selectedEdgeColor : edgeColor;
            }
            ShowThumbnail(true);
            thumbnail.texture = row.Thumbnail;
            thumbnail.color = row.Thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            nameLabel.text = row.Name;
            detailLabel.text = row.Detail;
            Indent(row.Depth);
            DrawTree(row);
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(HandleSelect);
        }

        public override void Unbind()
        {
            if (selectButton != null) selectButton.onClick.RemoveAllListeners();
            if (thumbnail != null) thumbnail.texture = null;
            select = null;
        }

        private void HandleSelect() => select?.Invoke(cardId);

        private void DrawTree(CardEditorRowModel row)
        {
            bool child = row.Depth > 0;
            bool parent = row.HasChildren;
            if (!child && !parent && trunkUp == null) return; // a plain list never makes the lines
            RectTransform words = nameLabel.rectTransform.parent as RectTransform;
            if (words == null) return;
            float trunkX = wordsLeft - TrunkOffset;
            // A child: the trunk from the row above, down to here (└) or on past (├), and a line in to the words.
            Line(ref trunkUp, words, "TreeUp", child, trunkX, child && row.IsLastChild ? 0.5f : 0f, 1f, RowGap, child && !row.IsLastChild ? -RowGap : 0f, TreeLine);
            float branchLength = (row.Depth * IndentPerLevel) + TrunkOffset - 6f;
            LineAcross(ref branch, words, "TreeIn", child, trunkX, branchLength);
            // A parent: a small node, and the trunk from it down to its first child.
            Line(ref trunkDown, words, "TreeDown", parent, trunkX, 0f, 0.5f, 0f, -RowGap, TreeLine);
            Node(words, parent, trunkX);
        }

        // A thin line down the row at x, from yMin to yMax of the row (0 bottom, 1 top), reaching past it by the offsets.
        private static void Line(ref RectTransform line, RectTransform parent, string name, bool shown, float x,
            float yMin, float yMax, float pastTop, float pastBottom, Color colour)
        {
            if (!shown)
            {
                if (line != null && line.gameObject.activeSelf) line.gameObject.SetActive(false);
                return;
            }

            if (line == null) line = MakeImage(parent, name, colour);
            line.anchorMin = new Vector2(0f, yMin);
            line.anchorMax = new Vector2(0f, yMax);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.offsetMin = new Vector2(x - (LineWidth * 0.5f), pastBottom);
            line.offsetMax = new Vector2(x + (LineWidth * 0.5f), pastTop);
            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
        }

        private static void LineAcross(ref RectTransform line, RectTransform parent, string name, bool shown, float x, float length)
        {
            if (!shown)
            {
                if (line != null && line.gameObject.activeSelf) line.gameObject.SetActive(false);
                return;
            }

            if (line == null) line = MakeImage(parent, name, TreeLine);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(0f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(length, LineWidth);
            line.anchoredPosition = new Vector2(x - (LineWidth * 0.5f), 0f);
            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
        }

        private void Node(RectTransform parent, bool shown, float x)
        {
            if (!shown)
            {
                if (node != null && node.gameObject.activeSelf) node.gameObject.SetActive(false);
                return;
            }

            if (node == null) node = MakeImage(parent, "TreeNode", TreeNode);
            node.anchorMin = new Vector2(0f, 0.5f);
            node.anchorMax = new Vector2(0f, 0.5f);
            node.pivot = new Vector2(0.5f, 0.5f);
            node.sizeDelta = new Vector2(8f, 8f);
            node.anchoredPosition = new Vector2(x, 0f);
            if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
        }

        private static RectTransform MakeImage(RectTransform parent, string name, Color colour)
        {
            GameObject made = new GameObject(name, typeof(RectTransform));
            made.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)made.transform;
            rect.SetParent(parent, false);
            Image image = made.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            LayoutElement ignore = made.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true; // never moved by a layout group in the row
            return rect;
        }

        private void Indent(int depth)
        {
            RectTransform name = nameLabel.rectTransform;
            RectTransform detail = detailLabel.rectTransform;
            if (!placesRead)
            {
                placesRead = true;
                nameX = name.anchoredPosition.x;
                detailX = detail.anchoredPosition.x;
                RectTransform words = name.parent as RectTransform;
                float width = words != null ? words.rect.width : 0f;
                wordsLeft = (name.anchorMin.x * width) + name.offsetMin.x; // the words' left edge in their parent
            }

            float shift = depth * IndentPerLevel;
            name.anchoredPosition = new Vector2(nameX + shift, name.anchoredPosition.y);
            detail.anchoredPosition = new Vector2(detailX + shift, detail.anchoredPosition.y);
        }

        /// <summary>Board 11: the deck builder's deck list has no pictures.</summary>
        public void ShowThumbnail(bool show)
        {
            if (thumbnail != null && thumbnail.gameObject.activeSelf != show) thumbnail.gameObject.SetActive(show);
        }
    }
}
