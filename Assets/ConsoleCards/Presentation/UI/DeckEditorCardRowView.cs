using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One card in the deck editor (L2-3): picture, name and kind, and − copies + in the selected deck.</summary>
    public sealed class DeckEditorCardRowView : ReusableUiView
    {
        [SerializeField] private Image fill;
        [SerializeField] private RawImage thumbnail;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Button decrementButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private Text copiesLabel;
        [Tooltip("Row colour for a card that is in the deck (at least one copy).")]
        [SerializeField] private Color inDeckColor = new Color(1f, 0.953f, 0.769f, 1f);
        [SerializeField] private Color outOfDeckColor = Color.white;

        private string cardId;
        private Action<string, int> changeCopies;

        public void Bind(DeckEditorCardRowModel row, Action<string, int> copiesAction)
        {
            RequireAcquired();
            if (fill == null || thumbnail == null || nameLabel == null || detailLabel == null || decrementButton == null
                || incrementButton == null || copiesLabel == null)
            {
                throw new InvalidOperationException("DeckEditorCardRowView requires its authored references.");
            }

            cardId = row.CardId;
            changeCopies = copiesAction;
            fill.color = row.Copies > 0 ? inDeckColor : outOfDeckColor;
            thumbnail.texture = row.Thumbnail;
            thumbnail.color = row.Thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            nameLabel.text = row.Name;
            detailLabel.text = row.Detail;
            copiesLabel.text = row.Copies.ToString();
            decrementButton.interactable = row.Copies > 0;
            decrementButton.onClick.RemoveAllListeners();
            incrementButton.onClick.RemoveAllListeners();
            decrementButton.onClick.AddListener(HandleFewer);
            incrementButton.onClick.AddListener(HandleMore);
        }

        public override void Unbind()
        {
            if (decrementButton != null) decrementButton.onClick.RemoveAllListeners();
            if (incrementButton != null) incrementButton.onClick.RemoveAllListeners();
            if (thumbnail != null) thumbnail.texture = null;
            changeCopies = null;
        }

        private void HandleFewer() => changeCopies?.Invoke(cardId, -1);
        private void HandleMore() => changeCopies?.Invoke(cardId, 1);
    }
}
