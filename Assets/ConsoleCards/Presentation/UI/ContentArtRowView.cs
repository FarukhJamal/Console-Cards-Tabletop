using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One picture in the Card Art panel: thumbnail, name, where the picture comes from, and actions.</summary>
    public sealed class ContentArtRowView : ReusableUiView
    {
        [SerializeField] private RawImage thumbnail;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text sourceLabel;
        [SerializeField] private Button chooseButton;
        [SerializeField] private Button resetButton;

        private string key;
        private Action<string> choose;
        private Action<string> reset;

        public void Bind(ContentArtRowModel row, bool canChoose, Action<string> chooseAction, Action<string> resetAction)
        {
            RequireAcquired();
            if (thumbnail == null || nameLabel == null || sourceLabel == null || chooseButton == null || resetButton == null)
            {
                throw new InvalidOperationException("ContentArtRowView requires its authored references.");
            }

            key = row.Key;
            choose = chooseAction;
            reset = resetAction;
            thumbnail.texture = row.Thumbnail;
            thumbnail.color = row.Thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            nameLabel.text = row.Label;
            sourceLabel.text = row.Source;
            chooseButton.interactable = canChoose;
            resetButton.gameObject.SetActive(row.CanReset);
            chooseButton.onClick.RemoveAllListeners();
            resetButton.onClick.RemoveAllListeners();
            chooseButton.onClick.AddListener(HandleChoose);
            resetButton.onClick.AddListener(HandleReset);
        }

        public override void Unbind()
        {
            if (chooseButton != null) chooseButton.onClick.RemoveAllListeners();
            if (resetButton != null) resetButton.onClick.RemoveAllListeners();
            if (thumbnail != null) thumbnail.texture = null;
            choose = null;
            reset = null;
        }

        private void HandleChoose() => choose?.Invoke(key);
        private void HandleReset() => reset?.Invoke(key);
    }
}
