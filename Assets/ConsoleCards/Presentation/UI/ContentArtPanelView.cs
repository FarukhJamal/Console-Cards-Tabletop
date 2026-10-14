using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One picture row in the Card Art panel.</summary>
    public readonly struct ContentArtRowModel
    {
        public ContentArtRowModel(string key, string label, string source, Texture thumbnail, bool canReset)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            Source = source ?? string.Empty;
            Thumbnail = thumbnail;
            CanReset = canReset;
        }

        public string Key { get; }
        public string Label { get; }
        public string Source { get; }
        public Texture Thumbnail { get; }
        public bool CanReset { get; }
    }

    /// <summary>What the Card Art panel shows (doc 24, A3/A4).</summary>
    public sealed class ContentArtPanelModel
    {
        public ContentArtPanelModel(string packName, int packCount, string status, IReadOnlyList<ContentArtRowModel> rows,
            bool canChoosePictures)
        {
            PackName = packName ?? string.Empty;
            PackCount = packCount;
            Status = status ?? string.Empty;
            Rows = rows ?? Array.Empty<ContentArtRowModel>();
            CanChoosePictures = canChoosePictures;
        }

        public string PackName { get; }
        public int PackCount { get; }
        public string Status { get; }
        public IReadOnlyList<ContentArtRowModel> Rows { get; }
        public bool CanChoosePictures { get; }
    }

    /// <summary>The Card Art panel's actions, supplied by the game.</summary>
    public sealed class ContentArtPanelActions
    {
        public Action<int> CyclePack;
        public Action OpenFolder;
        public Action Reload;
        public Action<string> ChoosePicture;
        public Action<string> ResetPicture;
        public Action Close;
    }

    /// <summary>
    /// Card Art panel (doc 24, A3/A4): pick the active art pack and replace any card's picture with your own.
    /// An authored Final UI prefab; rows are pooled instances of the authored row prefab.
    /// </summary>
    public sealed class ContentArtPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text packLabel;
        [SerializeField] private Button previousPackButton;
        [SerializeField] private Button nextPackButton;
        [SerializeField] private Button openFolderButton;
        [SerializeField] private Button reloadButton;
        [SerializeField] private Text statusLabel;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private ScrollRect scroll;

        private readonly List<ContentArtRowView> rowViews = new List<ContentArtRowView>();
        private IRuntimeUiService uiService;
        private ContentArtPanelActions actions;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || packLabel == null || previousPackButton == null
                || nextPackButton == null || openFolderButton == null || reloadButton == null || statusLabel == null
                || rowsRoot == null || scroll == null || uiService == null)
            {
                throw new InvalidOperationException("ContentArtPanelView requires its authored references.");
            }
        }

        /// <summary>Shows the panel; call again with a new model to refresh it (keeps the scroll place).</summary>
        public void Bind(ContentArtPanelModel model, ContentArtPanelActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();
            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            previousPackButton.onClick.AddListener(HandlePreviousPack);
            nextPackButton.onClick.AddListener(HandleNextPack);
            openFolderButton.onClick.AddListener(HandleOpenFolder);
            reloadButton.onClick.AddListener(HandleReload);

            packLabel.text = model.PackCount > 0
                ? $"{model.PackName}   ({model.PackCount} pack{(model.PackCount == 1 ? string.Empty : "s")} found)"
                : $"{model.PackName}   (no packs yet)";
            previousPackButton.interactable = model.PackCount > 0;
            nextPackButton.interactable = model.PackCount > 0;
            statusLabel.text = model.Status;

            float scrollPlace = scroll.verticalNormalizedPosition;
            bool keepPlace = rowViews.Count == model.Rows.Count && rowViews.Count > 0;
            ReleaseRows();
            for (int i = 0; i < model.Rows.Count; i++)
            {
                ContentArtRowView row = uiService.AcquirePooled<ContentArtRowView>(PrototypeUiPrefabIds.ContentArtRow, rowsRoot);
                row.Bind(model.Rows[i], model.CanChoosePictures, HandleChoose, HandleReset);
                row.Show();
                rowViews.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = keepPlace ? scrollPlace : 1f;
        }

        public override void Unbind()
        {
            RemoveListeners();
            ReleaseRows();
            actions = null;
        }

        private void ReleaseRows()
        {
            for (int i = 0; i < rowViews.Count; i++)
            {
                if (rowViews[i] != null && uiService != null) uiService.Release(rowViews[i]);
            }

            rowViews.Clear();
        }

        private void RemoveListeners()
        {
            Clear(dismissOverlayButton);
            Clear(closeButton);
            Clear(previousPackButton);
            Clear(nextPackButton);
            Clear(openFolderButton);
            Clear(reloadButton);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleClose() => actions?.Close?.Invoke();
        private void HandlePreviousPack() => actions?.CyclePack?.Invoke(-1);
        private void HandleNextPack() => actions?.CyclePack?.Invoke(1);
        private void HandleOpenFolder() => actions?.OpenFolder?.Invoke();
        private void HandleReload() => actions?.Reload?.Invoke();
        private void HandleChoose(string key) => actions?.ChoosePicture?.Invoke(key);
        private void HandleReset(string key) => actions?.ResetPicture?.Invoke(key);
    }
}
