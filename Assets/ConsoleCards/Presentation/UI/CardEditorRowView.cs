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

        /// <summary>Board 11: the deck builder's deck list has no pictures.</summary>
        public void ShowThumbnail(bool show)
        {
            if (thumbnail != null && thumbnail.gameObject.activeSelf != show) thumbnail.gameObject.SetActive(show);
        }
    }
}
