using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// One deck in the card editor's "In decks" list (L2-2): a tick box (in the deck or not), the deck's name and size,
    /// and the card's copies in that deck with − and +.
    /// </summary>
    public sealed class CardEditorDeckRowView : ReusableUiView
    {
        [SerializeField] private Button tickButton;
        [SerializeField] private Image tickFill;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Button decrementButton;
        [SerializeField] private Button incrementButton;
        [SerializeField] private Text copiesLabel;
        [SerializeField] private Color inColor = new Color(0.122f, 0.478f, 0.4f, 1f);
        [SerializeField] private Color outColor = Color.white;

        private string deckId;
        private Action<string> toggle;
        private Action<string, int> changeCopies;

        public void Bind(CardEditorDeckRowModel row, Action<string> toggleAction, Action<string, int> copiesAction)
        {
            RequireAcquired();
            if (tickButton == null || tickFill == null || nameLabel == null || detailLabel == null
                || decrementButton == null || incrementButton == null || copiesLabel == null)
            {
                throw new InvalidOperationException("CardEditorDeckRowView requires its authored references.");
            }

            deckId = row.DeckId;
            toggle = toggleAction;
            changeCopies = copiesAction;
            tickFill.color = row.Copies > 0 ? inColor : outColor;
            nameLabel.text = row.Name;
            detailLabel.text = row.Detail;
            copiesLabel.text = row.Copies.ToString();
            decrementButton.interactable = row.Copies > 0;
            RemoveListeners();
            tickButton.onClick.AddListener(HandleToggle);
            decrementButton.onClick.AddListener(HandleFewer);
            incrementButton.onClick.AddListener(HandleMore);
        }

        public override void Unbind()
        {
            RemoveListeners();
            toggle = null;
            changeCopies = null;
        }

        private void RemoveListeners()
        {
            if (tickButton != null) tickButton.onClick.RemoveAllListeners();
            if (decrementButton != null) decrementButton.onClick.RemoveAllListeners();
            if (incrementButton != null) incrementButton.onClick.RemoveAllListeners();
        }

        private void HandleToggle() => toggle?.Invoke(deckId);
        private void HandleFewer() => changeCopies?.Invoke(deckId, -1);
        private void HandleMore() => changeCopies?.Invoke(deckId, 1);
    }
}
