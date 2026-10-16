using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One profile in the profile panel.</summary>
    public readonly struct ProfileRowModel
    {
        public ProfileRowModel(string id, string name, string detail, bool isActive)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            IsActive = isActive;
        }

        public string Id { get; }
        public string Name { get; }
        public string Detail { get; }
        public bool IsActive { get; }
    }

    /// <summary>What the profile panel shows (doc 28, P2).</summary>
    public sealed class ProfilePanelModel
    {
        public ProfilePanelModel(string activeName, IReadOnlyList<ProfileRowModel> rows, string status)
        {
            ActiveName = activeName ?? string.Empty;
            Rows = rows ?? Array.Empty<ProfileRowModel>();
            Status = status ?? string.Empty;
        }

        public string ActiveName { get; }
        public IReadOnlyList<ProfileRowModel> Rows { get; }
        public string Status { get; }
    }

    /// <summary>The profile panel's actions, supplied by the game.</summary>
    public sealed class ProfilePanelActions
    {
        public Action<string> PlayAs;
        public Action<string> Create;
        public Action<string> Rename;
        public Action OpenFolder;
        public Action Close;
    }

    /// <summary>
    /// Player profile panel (doc 28, P2): who is playing, every profile on this computer with Play as, and a name box
    /// to make a new profile or rename this one. An authored Final UI prefab; rows are pooled instances of the
    /// authored row prefab.
    /// </summary>
    public sealed class ProfilePanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text activeNameLabel;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private InputField nameInput;
        [SerializeField] private Button createButton;
        [SerializeField] private Button renameButton;
        [Tooltip("Optional. Shown only when the game offers it (not in play; the profiles folder is a Unity menu).")]
        [SerializeField] private Button openFolderButton;
        [SerializeField] private Text statusLabel;

        private readonly List<ProfileRowView> rowViews = new List<ProfileRowView>();
        private IRuntimeUiService uiService;
        private ProfilePanelActions actions;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || activeNameLabel == null || rowsRoot == null
                || scroll == null || nameInput == null || createButton == null || renameButton == null
                || statusLabel == null || uiService == null)
            {
                throw new InvalidOperationException("ProfilePanelView requires its authored references.");
            }
        }

        /// <summary>Shows the panel; call again with a new model to refresh it. Typed text is kept unless cleared.</summary>
        public void Bind(ProfilePanelModel model, ProfilePanelActions panelActions, bool clearName)
        {
            RequireAcquired();
            ValidateReferences();
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();
            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            createButton.onClick.AddListener(HandleCreate);
            renameButton.onClick.AddListener(HandleRename);
            if (openFolderButton != null)
            {
                openFolderButton.gameObject.SetActive(actions.OpenFolder != null);
                openFolderButton.onClick.AddListener(HandleOpenFolder);
            }

            activeNameLabel.text = model.ActiveName;
            statusLabel.text = model.Status;
            if (clearName) nameInput.text = string.Empty;

            ReleaseRows();
            for (int i = 0; i < model.Rows.Count; i++)
            {
                ProfileRowView row = uiService.AcquirePooled<ProfileRowView>(UiPrefabIds.ProfileRow, rowsRoot);
                row.Bind(model.Rows[i], HandlePlayAs);
                row.Show();
                rowViews.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
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
            Clear(createButton);
            Clear(renameButton);
            Clear(openFolderButton);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleClose() => actions?.Close?.Invoke();
        private void HandleCreate() => actions?.Create?.Invoke(nameInput.text);
        private void HandleRename() => actions?.Rename?.Invoke(nameInput.text);
        private void HandleOpenFolder() => actions?.OpenFolder?.Invoke();
        private void HandlePlayAs(string id) => actions?.PlayAs?.Invoke(id);
    }
}
