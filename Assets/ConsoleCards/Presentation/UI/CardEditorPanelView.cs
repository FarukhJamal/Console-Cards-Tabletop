using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One card in the card editor's list.</summary>
    public readonly struct CardEditorRowModel
    {
        public CardEditorRowModel(string id, string name, string detail, Texture thumbnail, bool isSelected)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            Thumbnail = thumbnail;
            IsSelected = isSelected;
        }

        public string Id { get; }
        public string Name { get; }
        public string Detail { get; }
        public Texture Thumbnail { get; }
        public bool IsSelected { get; }
    }

    /// <summary>The words typed in the card editor, handed back with every action so nothing typed is lost.</summary>
    public readonly struct CardEditorTyped
    {
        public CardEditorTyped(string code, string name, string description, string cost)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Cost = cost ?? string.Empty;
        }

        public string Code { get; }
        public string Name { get; }
        public string Description { get; }
        public string Cost { get; }
    }

    /// <summary>What the card editor shows (doc 27 List 2, L2-2): the list, and the selected card with its preview.</summary>
    public sealed class CardEditorModel
    {
        public string Title = "CARD EDITOR";
        public IReadOnlyList<CardEditorRowModel> Rows = Array.Empty<CardEditorRowModel>();
        public string Heading = string.Empty;
        public string IdText = string.Empty;
        public string Code = string.Empty;
        public bool CodeEditable;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public int Copies;
        public string Cost = string.Empty;
        public string WorksLike = string.Empty;
        public bool CanChangeWorksLike;
        public Texture Front;
        public Texture Back;
        public bool CanChoosePictures;
        public bool CanClearFront;
        public bool CanClearBack;
        public bool CanReset;
        public string ResetText = "Reset";
        public bool CanDelete;
        public string Status = string.Empty;
    }

    /// <summary>The card editor's actions, supplied by the game. Each carries what is typed now.</summary>
    public sealed class CardEditorActions
    {
        public Action<string> Select;
        public Action<CardEditorTyped> NewCard;
        public Action<int, CardEditorTyped> ChangeCopies;
        public Action<int, CardEditorTyped> CycleWorksLike;
        public Action<bool, CardEditorTyped> ChoosePicture;
        public Action<bool, CardEditorTyped> ClearPicture;
        public Action<CardEditorTyped> Save;
        public Action Reset;
        public Action Delete;
        public Action Close;
    }

    /// <summary>
    /// Card editor (doc 27 List 2, L2-2): every card of the game with your new cards, and the selected card's
    /// words, cost, copies, the card it works like and its pictures, with a live preview of the card. An authored
    /// Final UI prefab; rows are pooled instances of the authored row prefab. Typing updates the preview here only;
    /// nothing is saved until Save.
    /// </summary>
    public sealed class CardEditorPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;

        [Header("Card list")]
        [SerializeField] private Button newCardButton;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private ScrollRect scroll;

        [Header("Preview")]
        [SerializeField] private RawImage frontPreview;
        [Tooltip("The card's name, shown on the blank base card while it has no front picture.")]
        [SerializeField] private Text frontBlankName;
        [SerializeField] private RawImage backPreview;
        [SerializeField] private Text previewName;
        [SerializeField] private Text previewCost;
        [SerializeField] private Text previewDescription;

        [Header("Fields")]
        [SerializeField] private Text headingLabel;
        [SerializeField] private Text idLabel;
        [SerializeField] private InputField codeInput;
        [SerializeField] private InputField nameInput;
        [SerializeField] private InputField descriptionInput;
        [SerializeField] private InputField costInput;
        [SerializeField] private Button copiesDecrement;
        [SerializeField] private Button copiesIncrement;
        [SerializeField] private Text copiesLabel;
        [SerializeField] private Button worksLikePrevious;
        [SerializeField] private Button worksLikeNext;
        [SerializeField] private Text worksLikeLabel;
        [SerializeField] private Button chooseFrontButton;
        [SerializeField] private Button clearFrontButton;
        [SerializeField] private Button chooseBackButton;
        [SerializeField] private Button clearBackButton;

        [Header("Footer")]
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text resetLabel;
        [SerializeField] private Button saveButton;

        private readonly List<CardEditorRowView> rowViews = new List<CardEditorRowView>();
        private IRuntimeUiService uiService;
        private CardEditorActions actions;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || titleLabel == null || newCardButton == null
                || rowsRoot == null || scroll == null || frontPreview == null || frontBlankName == null
                || backPreview == null || previewName == null || previewCost == null || previewDescription == null
                || headingLabel == null || idLabel == null || codeInput == null || nameInput == null
                || descriptionInput == null || costInput == null || copiesDecrement == null || copiesIncrement == null
                || copiesLabel == null || worksLikePrevious == null || worksLikeNext == null || worksLikeLabel == null
                || chooseFrontButton == null || clearFrontButton == null || chooseBackButton == null
                || clearBackButton == null || statusLabel == null || deleteButton == null || resetButton == null
                || resetLabel == null || saveButton == null || uiService == null)
            {
                throw new InvalidOperationException("CardEditorPanelView requires its authored references.");
            }
        }

        /// <summary>Shows the panel; call again with a new model to refresh it (keeps the list's scroll place).</summary>
        public void Bind(CardEditorModel model, CardEditorActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            if (model == null) throw new ArgumentNullException(nameof(model));
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();

            titleLabel.text = model.Title;
            headingLabel.text = model.Heading;
            idLabel.text = model.IdText;
            codeInput.text = model.Code;
            codeInput.interactable = model.CodeEditable;
            nameInput.text = model.Name;
            descriptionInput.text = model.Description;
            costInput.text = model.Cost;
            copiesLabel.text = model.Copies.ToString();
            worksLikeLabel.text = model.WorksLike;
            worksLikePrevious.interactable = model.CanChangeWorksLike;
            worksLikeNext.interactable = model.CanChangeWorksLike;
            frontPreview.texture = model.Front;
            backPreview.texture = model.Back;
            backPreview.color = model.Back != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            frontBlankName.gameObject.SetActive(model.Front == null);
            chooseFrontButton.interactable = model.CanChoosePictures;
            chooseBackButton.interactable = model.CanChoosePictures;
            clearFrontButton.interactable = model.CanClearFront;
            clearBackButton.interactable = model.CanClearBack;
            resetButton.interactable = model.CanReset;
            resetLabel.text = model.ResetText;
            deleteButton.gameObject.SetActive(model.CanDelete);
            statusLabel.text = model.Status;
            RefreshPreviewWords();

            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            newCardButton.onClick.AddListener(HandleNewCard);
            copiesDecrement.onClick.AddListener(HandleFewerCopies);
            copiesIncrement.onClick.AddListener(HandleMoreCopies);
            worksLikePrevious.onClick.AddListener(HandlePreviousWorksLike);
            worksLikeNext.onClick.AddListener(HandleNextWorksLike);
            chooseFrontButton.onClick.AddListener(HandleChooseFront);
            clearFrontButton.onClick.AddListener(HandleClearFront);
            chooseBackButton.onClick.AddListener(HandleChooseBack);
            clearBackButton.onClick.AddListener(HandleClearBack);
            deleteButton.onClick.AddListener(HandleDelete);
            resetButton.onClick.AddListener(HandleReset);
            saveButton.onClick.AddListener(HandleSave);
            nameInput.onValueChanged.AddListener(HandleTyping);
            descriptionInput.onValueChanged.AddListener(HandleTyping);
            costInput.onValueChanged.AddListener(HandleTyping);

            float scrollPlace = scroll.verticalNormalizedPosition;
            bool keepPlace = rowViews.Count == model.Rows.Count && rowViews.Count > 0;
            ReleaseRows();
            for (int i = 0; i < model.Rows.Count; i++)
            {
                CardEditorRowView row = uiService.AcquirePooled<CardEditorRowView>(PrototypeUiPrefabIds.CardEditorRow, rowsRoot);
                row.Bind(model.Rows[i], HandleSelect);
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
            if (frontPreview != null) frontPreview.texture = null;
            if (backPreview != null) backPreview.texture = null;
            actions = null;
        }

        private CardEditorTyped Typed() =>
            new CardEditorTyped(codeInput.text, nameInput.text, descriptionInput.text, costInput.text);

        // The words under the pictures follow what is typed, before anything is saved.
        private void RefreshPreviewWords()
        {
            string name = string.IsNullOrWhiteSpace(nameInput.text) ? "(no name)" : nameInput.text.Trim();
            previewName.text = name;
            frontBlankName.text = name;
            previewCost.text = string.IsNullOrWhiteSpace(costInput.text) ? "No cost" : "Cost: " + costInput.text.Trim();
            previewDescription.text = descriptionInput.text;
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
            Clear(newCardButton);
            Clear(copiesDecrement);
            Clear(copiesIncrement);
            Clear(worksLikePrevious);
            Clear(worksLikeNext);
            Clear(chooseFrontButton);
            Clear(clearFrontButton);
            Clear(chooseBackButton);
            Clear(clearBackButton);
            Clear(deleteButton);
            Clear(resetButton);
            Clear(saveButton);
            if (nameInput != null) nameInput.onValueChanged.RemoveAllListeners();
            if (descriptionInput != null) descriptionInput.onValueChanged.RemoveAllListeners();
            if (costInput != null) costInput.onValueChanged.RemoveAllListeners();
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleTyping(string _) => RefreshPreviewWords();
        private void HandleClose() => actions?.Close?.Invoke();
        private void HandleSelect(string id) => actions?.Select?.Invoke(id);
        private void HandleNewCard() => actions?.NewCard?.Invoke(Typed());
        private void HandleFewerCopies() => actions?.ChangeCopies?.Invoke(-1, Typed());
        private void HandleMoreCopies() => actions?.ChangeCopies?.Invoke(1, Typed());
        private void HandlePreviousWorksLike() => actions?.CycleWorksLike?.Invoke(-1, Typed());
        private void HandleNextWorksLike() => actions?.CycleWorksLike?.Invoke(1, Typed());
        private void HandleChooseFront() => actions?.ChoosePicture?.Invoke(true, Typed());
        private void HandleClearFront() => actions?.ClearPicture?.Invoke(true, Typed());
        private void HandleChooseBack() => actions?.ChoosePicture?.Invoke(false, Typed());
        private void HandleClearBack() => actions?.ClearPicture?.Invoke(false, Typed());
        private void HandleSave() => actions?.Save?.Invoke(Typed());
        private void HandleReset() => actions?.Reset?.Invoke();
        private void HandleDelete() => actions?.Delete?.Invoke();
    }
}
