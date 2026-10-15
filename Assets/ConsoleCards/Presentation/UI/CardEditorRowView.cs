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
            select = selectAction;
            selectedFill.color = row.IsSelected ? selectedColor : normalColor;
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
    }
}
