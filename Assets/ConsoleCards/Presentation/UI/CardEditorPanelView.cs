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

    /// <summary>One deck in the selected card's "In decks" list: the game's deck or one of yours, with copies.</summary>
    public readonly struct CardEditorDeckRowModel
    {
        public CardEditorDeckRowModel(string deckId, string name, string detail, int copies)
        {
            DeckId = deckId ?? string.Empty;
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            Copies = copies;
        }

        public string DeckId { get; }
        public string Name { get; }
        public string Detail { get; }
        public int Copies { get; }
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

    /// <summary>
    /// What the card editor shows (doc 27 List 2, L2-2): the chosen game and its deck, the card list, and the selected
    /// card with its preview and the decks it is in.
    /// </summary>
    public sealed class CardEditorModel
    {
        public string Title = "CARD EDITOR";
        public string GameName = string.Empty;
        public bool CanChangeGame;
        public string DeckSummary = string.Empty;
        public IReadOnlyList<CardEditorRowModel> Rows = Array.Empty<CardEditorRowModel>();
        public string Heading = string.Empty;
        public string IdText = string.Empty;
        public string Code = string.Empty;
        public bool CodeEditable;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Cost = string.Empty;
        public string WorksLike = string.Empty;
        public bool CanChangeWorksLike;
        public IReadOnlyList<CardEditorDeckRowModel> Decks = Array.Empty<CardEditorDeckRowModel>();
        public Texture Front;
        public Texture Back;
        public bool CanChoosePictures;
        public bool CanClearFront;
        public bool CanClearBack;
        public bool CanReset;
        public string ResetText = "Reset";
        public bool CanDelete;
        public string Status = string.Empty;
        /// <summary>The last change was written (auto-save): the header shows its SAVED tick.</summary>
        public bool Saved;
        /// <summary>Card studio: the card plays large (avatar size); pictures of another shape are fitted, not cropped.</summary>
        public bool Large;
        public bool FitPicture;
        public bool CanDuplicate;
        public bool CanImport;

        /// <summary>
        /// The card's 8 icon places (ICON-1), in Top, Right, Bottom, Left, Top right, Bottom right, Bottom left, Top left
        /// order: one picture holds every icon and each place shows its cell of it. Null atlas: no icon buttons.
        /// </summary>
        public Texture IconAtlas;
        public Rect[] IconCells = Array.Empty<Rect>();
        public bool CanChangeIcons;

        /// <summary>
        /// The card's frame (FRAME-1): the game's frame picture and tab pictures with the chosen variant's colours,
        /// and which icon places have a tab (same order as <see cref="IconCells"/>). Null frame: no frame shown.
        /// </summary>
        public Texture FrameTexture;
        public Color FrameColor = Color.white;
        public Texture SideTabTexture;
        public Texture CornerTabTexture;
        public Color TabColor = Color.white;
        public bool[] TabShown = Array.Empty<bool>();
        public string FrameName = string.Empty;
        public bool CanChangeFrame;
    }

    /// <summary>The card editor's actions, supplied by the game. Each carries what is typed now.</summary>
    public sealed class CardEditorActions
    {
        public Action<int, CardEditorTyped> CycleGame;
        public Action<string> Select;
        public Action<CardEditorTyped> NewCard;
        public Action<int, CardEditorTyped> CycleWorksLike;
        public Action<string, CardEditorTyped> ToggleDeck;
        public Action<string, int, CardEditorTyped> ChangeDeckCopies;
        public Action<CardEditorTyped> NewDeck;
        public Action<bool, CardEditorTyped> ChoosePicture;
        public Action<bool, CardEditorTyped> ClearPicture;
        /// <summary>An icon place was clicked (0-7, the order of <see cref="CardEditorModel.IconCells"/>): next icon.</summary>
        public Action<int, CardEditorTyped> CycleIcon;
        /// <summary>The frame variant picker: -1 previous, +1 next (FRAME-1).</summary>
        public Action<int, CardEditorTyped> CycleFrame;
        public Action<CardEditorTyped> Save;
        /// <summary>Auto-save: writes the card as typed (no panel refresh); true when it was saved.</summary>
        public Func<CardEditorTyped, bool> AutoSave;
        /// <summary>Card studio: STANDARD (false) or LARGE · AVATARS (true).</summary>
        public Action<bool, CardEditorTyped> SetSize;
        /// <summary>Card studio: CROP TO FILL (false) or FIT · ADD BORDER (true).</summary>
        public Action<bool, CardEditorTyped> SetFit;
        public Action<CardEditorTyped> Duplicate;
        public Action<CardEditorTyped> ImportFolder;
        /// <summary>UNDO: back to the card as it was when it was chosen.</summary>
        public Action Undo;
        public Action Reset;
        public Action Delete;
        public Action Close;
    }

    /// <summary>
    /// Card editor (doc 27 List 2, L2-2): pick a game; its deck (every card with its copies and the total); the
    /// selected card's words, cost, the card it works like, its pictures with a live preview, and the decks it is in
    /// (the game's deck and yours) with copies in each. An authored Final UI prefab; list rows are pooled instances
    /// of authored row prefabs. Typing updates the preview here only; nothing is saved until Save.
    /// </summary>
    public sealed class CardEditorPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;

        [Header("Game")]
        [SerializeField] private Button previousGameButton;
        [SerializeField] private Button nextGameButton;
        [SerializeField] private Text gameLabel;
        [SerializeField] private Text deckSummaryLabel;

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
        [Tooltip("Optional (ICON-1): 8 buttons on the front preview, in Top, Right, Bottom, Left, Top right, Bottom right, "
            + "Bottom left, Top left order. A click shows the next icon at that place.")]
        [SerializeField] private Button[] iconButtons = Array.Empty<Button>();
        [SerializeField] private RawImage[] iconImages = Array.Empty<RawImage>();
        [Tooltip("Optional (FRAME-1): the frame over the front preview, and a tab under each icon place (same order).")]
        [SerializeField] private RawImage framePreview;
        [SerializeField] private RawImage[] tabImages = Array.Empty<RawImage>();
        [SerializeField] private Button framePrevious;
        [SerializeField] private Button frameNext;
        [SerializeField] private Text frameLabel;

        [Header("Fields")]
        [SerializeField] private Text headingLabel;
        [SerializeField] private Text idLabel;
        [SerializeField] private InputField codeInput;
        [SerializeField] private InputField nameInput;
        [SerializeField] private InputField descriptionInput;
        [SerializeField] private InputField costInput;
        [SerializeField] private Button worksLikePrevious;
        [SerializeField] private Button worksLikeNext;
        [SerializeField] private Text worksLikeLabel;
        [SerializeField] private Button chooseFrontButton;
        [SerializeField] private Button clearFrontButton;
        [SerializeField] private Button chooseBackButton;
        [SerializeField] private Button clearBackButton;

        [Header("In decks")]
        [SerializeField] private RectTransform deckRowsRoot;
        [SerializeField] private Button newDeckButton;

        [Header("Footer")]
        [SerializeField] private Text statusLabel;
        [Tooltip("Auto-save: the SAVED tick in the header, shown after each change is written.")]
        [SerializeField] private GameObject savedTick;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Text resetLabel;
        [SerializeField] private Button saveButton;

        // Board 10 "Card studio" (owner, 2026-10-30: "cards plus art will be merged"). All optional: a panel made before
        // board 10 has none of them and works as before.
        [Header("Card studio (board 10)")]
        [Tooltip("Find a card: hides the rows whose name does not have the words.")]
        [SerializeField] private InputField searchInput;
        [Tooltip("ALL · YOURS · NO PICTURE · WRONG SHAPE")]
        [SerializeField] private Button[] filterButtons = Array.Empty<Button>();
        [SerializeField] private Image[] filterFills = Array.Empty<Image>();
        [SerializeField] private Text[] filterLabels = Array.Empty<Text>();
        [SerializeField] private Color filterOn = new Color(0.910f, 0.925f, 0.929f, 1f);
        [SerializeField] private Color filterOff = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color filterOnWords = new Color(0.063f, 0.075f, 0.078f, 1f);
        [SerializeField] private Color filterOffWords = new Color(0.718f, 0.753f, 0.765f, 1f);
        [Tooltip("Under each picture: ✓ (or ⚠) its name · width × height.")]
        [SerializeField] private Text frontFileLabel;
        [SerializeField] private Text backFileLabel;
        [Tooltip("PICTURES · GAME DATA · IN DECKS: the buttons and the page each shows.")]
        [SerializeField] private Button[] tabButtons = Array.Empty<Button>();
        [SerializeField] private Image[] tabFills = Array.Empty<Image>();
        [SerializeField] private GameObject[] tabPages = Array.Empty<GameObject>();
        [SerializeField] private Color tabOn = new Color(0.110f, 0.235f, 0.239f, 1f);
        [SerializeField] private Color tabOff = new Color(0f, 0f, 0f, 0f);
        [Tooltip("The yellow box: shown when the front picture is not the card's shape or is below its sharp size.")]
        [SerializeField] private GameObject shapeWarning;
        [SerializeField] private Text shapeWarningLabel;
        [SerializeField] private Button templateButton;
        [Tooltip("UNDO: back to the card as it was last saved.")]
        [SerializeField] private Button undoButton;
        [Tooltip("DONE: saves the card and closes the studio.")]
        [SerializeField] private Button doneButton;
        [Tooltip("The card's shape (width : height), its best size and its smallest sharp size, in pixels.")]
        [SerializeField] private Vector2 bestSize = new Vector2(1500f, 2100f);
        [SerializeField] private Vector2 sharpSize = new Vector2(750f, 1050f);
        [Tooltip("CARD SIZE: STANDARD, LARGE · AVATARS.")]
        [SerializeField] private Button[] sizeButtons = Array.Empty<Button>();
        [SerializeField] private Image[] sizeFills = Array.Empty<Image>();
        [Tooltip("WHEN A PICTURE IS THE WRONG SHAPE: CROP TO FILL, FIT · ADD BORDER.")]
        [SerializeField] private Button[] fitButtons = Array.Empty<Button>();
        [SerializeField] private Image[] fitFills = Array.Empty<Image>();
        [Tooltip("The size rows' values: best size, smallest sharp size (they change with LARGE).")]
        [SerializeField] private Text bestSizeLabel;
        [SerializeField] private Text sharpSizeLabel;
        [SerializeField] private Text fitNoteLabel;
        [SerializeField] private Button duplicateButton;
        [SerializeField] private Button importButton;
        [Tooltip("FRONT + BACK · ON THE TABLE · IN A CONSOLE: the buttons and the page each shows.")]
        [SerializeField] private Button[] previewButtons = Array.Empty<Button>();
        [SerializeField] private Image[] previewFills = Array.Empty<Image>();
        [SerializeField] private GameObject[] previewPages = Array.Empty<GameObject>();
        [SerializeField] private RawImage tableFront;
        [SerializeField] private RawImage tableBack;
        [SerializeField] private RawImage consoleCard;
        [Tooltip("The cards on the table and Console pages, scaled up for a large card.")]
        [SerializeField] private RectTransform tableCards;
        [SerializeField] private RectTransform consoleCardHolder;

        private static readonly System.Text.StringBuilder FileWords = new System.Text.StringBuilder(64);
        private int filter;
        private int tab;
        private string selectedId = string.Empty;
        private Texture shownFront;
        private int previewPage;
        private bool shownLarge;

        private readonly List<CardEditorRowView> rowViews = new List<CardEditorRowView>();
        private readonly List<CardEditorDeckRowView> deckRowViews = new List<CardEditorDeckRowView>();
        private IRuntimeUiService uiService;
        private CardEditorActions actions;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || titleLabel == null || previousGameButton == null
                || nextGameButton == null || gameLabel == null || deckSummaryLabel == null || newCardButton == null
                || rowsRoot == null || scroll == null || frontPreview == null || frontBlankName == null
                || backPreview == null || previewName == null || previewCost == null || previewDescription == null
                || headingLabel == null || idLabel == null || codeInput == null || nameInput == null
                || descriptionInput == null || costInput == null || worksLikePrevious == null || worksLikeNext == null
                || worksLikeLabel == null || chooseFrontButton == null || clearFrontButton == null
                || chooseBackButton == null || clearBackButton == null || deckRowsRoot == null || newDeckButton == null
                || statusLabel == null || deleteButton == null || resetButton == null || resetLabel == null
                || saveButton == null || uiService == null)
            {
                throw new InvalidOperationException(
                    "CardEditorPanelView requires its authored references. A CardEditorPanel made before the game picker "
                    + "and In decks list: delete it and run Create Missing Gameplay UI Prefabs again.");
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
            gameLabel.text = model.GameName;
            previousGameButton.interactable = model.CanChangeGame;
            nextGameButton.interactable = model.CanChangeGame;
            deckSummaryLabel.text = model.DeckSummary;
            headingLabel.text = model.Heading;
            idLabel.text = model.IdText;
            codeInput.text = model.Code;
            codeInput.interactable = model.CodeEditable;
            nameInput.text = model.Name;
            descriptionInput.text = model.Description;
            costInput.text = model.Cost;
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
            ShowSaved(model.Saved);
            BindIcons(model);
            BindFrame(model);

            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            previousGameButton.onClick.AddListener(HandlePreviousGame);
            nextGameButton.onClick.AddListener(HandleNextGame);
            newCardButton.onClick.AddListener(HandleNewCard);
            worksLikePrevious.onClick.AddListener(HandlePreviousWorksLike);
            worksLikeNext.onClick.AddListener(HandleNextWorksLike);
            chooseFrontButton.onClick.AddListener(HandleChooseFront);
            clearFrontButton.onClick.AddListener(HandleClearFront);
            chooseBackButton.onClick.AddListener(HandleChooseBack);
            clearBackButton.onClick.AddListener(HandleClearBack);
            newDeckButton.onClick.AddListener(HandleNewDeck);
            deleteButton.onClick.AddListener(HandleDelete);
            resetButton.onClick.AddListener(HandleReset);
            saveButton.onClick.AddListener(HandleSave);
            if (framePrevious != null) framePrevious.onClick.AddListener(HandlePreviousFrame);
            if (frameNext != null) frameNext.onClick.AddListener(HandleNextFrame);
            for (int i = 0; iconButtons != null && i < iconButtons.Length; i++)
            {
                if (iconButtons[i] == null) continue;
                int place = i;
                iconButtons[i].onClick.AddListener(() => HandleIcon(place));
            }

            nameInput.onValueChanged.AddListener(HandleTyping);
            descriptionInput.onValueChanged.AddListener(HandleTyping);
            costInput.onValueChanged.AddListener(HandleTyping);
            codeInput.onEndEdit.AddListener(HandleTypingDone);
            nameInput.onEndEdit.AddListener(HandleTypingDone);
            descriptionInput.onEndEdit.AddListener(HandleTypingDone);
            costInput.onEndEdit.AddListener(HandleTypingDone);

            float scrollPlace = scroll.verticalNormalizedPosition;
            bool keepPlace = rowViews.Count == model.Rows.Count && rowViews.Count > 0;
            ReleaseRows();
            for (int i = 0; i < model.Rows.Count; i++)
            {
                CardEditorRowView row = uiService.AcquirePooled<CardEditorRowView>(UiPrefabIds.CardEditorRow, rowsRoot);
                row.Bind(model.Rows[i], HandleSelect);
                row.Show();
                rowViews.Add(row);
            }

            for (int i = 0; i < model.Decks.Count; i++)
            {
                CardEditorDeckRowView deckRow =
                    uiService.AcquirePooled<CardEditorDeckRowView>(UiPrefabIds.CardEditorDeckRow, deckRowsRoot);
                deckRow.Bind(model.Decks[i], HandleToggleDeck, HandleDeckCopies);
                deckRow.Show();
                deckRowViews.Add(deckRow);
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = keepPlace ? scrollPlace : 1f;
            BindStudio(model);
        }

        // ---------- board 10 ----------

        private void BindStudio(CardEditorModel model)
        {
            selectedId = string.Empty;
            for (int i = 0; i < model.Rows.Count; i++)
            {
                if (model.Rows[i].IsSelected) selectedId = model.Rows[i].Id;
            }

            shownFront = model.Front;
            if (searchInput != null)
            {
                searchInput.onValueChanged.RemoveAllListeners();
                searchInput.onValueChanged.AddListener(_ => ApplyFilter());
            }

            for (int i = 0; i < filterButtons.Length; i++)
            {
                if (filterButtons[i] == null) continue;
                int which = i;
                filterButtons[i].onClick.RemoveAllListeners();
                filterButtons[i].onClick.AddListener(() =>
                {
                    filter = which;
                    ApplyFilter();
                });
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int which = i;
                tabButtons[i].onClick.RemoveAllListeners();
                tabButtons[i].onClick.AddListener(() => ShowTab(which));
            }

            if (filterLabels.Length > 0 && filterLabels[0] != null) filterLabels[0].text = "ALL " + model.Rows.Count;
            if (templateButton != null)
            {
                templateButton.onClick.RemoveAllListeners();
                templateButton.onClick.AddListener(SaveTemplate);
            }

            if (undoButton != null)
            {
                undoButton.onClick.RemoveAllListeners();
                undoButton.onClick.AddListener(() =>
                {
                    CardEditorActions now = actions;
                    if (now?.Undo != null) now.Undo();
                    else if (selectedId.Length > 0) now?.Select?.Invoke(selectedId);
                });
            }

            BindStudioParts(model);

            if (doneButton != null)
            {
                doneButton.onClick.RemoveAllListeners();
                doneButton.onClick.AddListener(() =>
                {
                    CardEditorActions now = actions;
                    now?.Save?.Invoke(Typed());
                    now?.Close?.Invoke();
                });
            }

            if (frontFileLabel != null) titleLabel.text = "CARD STUDIO";
            if (frontFileLabel != null)
            {
                // Board 10: a picture is never stretched; one of another shape is cropped to fill the card.
                CropToFill(frontPreview, model.Front);
                CropToFill(backPreview, model.Back);
                frontPreview.color = model.Front != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                backPreview.color = model.Back != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (frontFileLabel != null) frontFileLabel.text = FileLine(model.Front, true);
            if (backFileLabel != null) backFileLabel.text = FileLine(model.Back, false);
            ShowWarning(model.Front);
            ShowTab(tab);
            ApplyFilter();
        }

        // Board 10's switches, DUPLICATE, IMPORT and the three preview pages. A panel made before them has none.
        private void BindStudioParts(CardEditorModel model)
        {
            shownLarge = model.Large;
            for (int i = 0; i < sizeButtons.Length; i++)
            {
                if (sizeButtons[i] == null) continue;
                bool large = i == 1;
                sizeButtons[i].onClick.RemoveAllListeners();
                sizeButtons[i].onClick.AddListener(() => actions?.SetSize?.Invoke(large, Typed()));
                if (i < sizeFills.Length && sizeFills[i] != null) sizeFills[i].color = large == model.Large ? tabOn : tabOff;
            }

            for (int i = 0; i < fitButtons.Length; i++)
            {
                if (fitButtons[i] == null) continue;
                bool fit = i == 1;
                fitButtons[i].onClick.RemoveAllListeners();
                fitButtons[i].onClick.AddListener(() => actions?.SetFit?.Invoke(fit, Typed()));
                if (i < fitFills.Length && fitFills[i] != null) fitFills[i].color = fit == model.FitPicture ? tabOn : tabOff;
            }

            // A large card has the same shape; its pictures want 1.4 times the pixels.
            float scale = model.Large ? 1.4f : 1f;
            if (bestSizeLabel != null) bestSizeLabel.text = $"{Mathf.RoundToInt(bestSize.x * scale)} × {Mathf.RoundToInt(bestSize.y * scale)} px";
            if (sharpSizeLabel != null) sharpSizeLabel.text = $"{Mathf.RoundToInt(sharpSize.x * scale)} × {Mathf.RoundToInt(sharpSize.y * scale)} px";
            if (fitNoteLabel != null)
            {
                fitNoteLabel.text = model.FitPicture
                    ? "Pictures are never stretched: one of another shape is fitted whole, with a dark border."
                    : "Pictures are never stretched: one of another shape is cropped to fill the card (the middle is kept).";
            }

            if (duplicateButton != null)
            {
                duplicateButton.onClick.RemoveAllListeners();
                duplicateButton.onClick.AddListener(() => actions?.Duplicate?.Invoke(Typed()));
                duplicateButton.interactable = model.CanDuplicate;
            }

            if (importButton != null)
            {
                importButton.onClick.RemoveAllListeners();
                importButton.onClick.AddListener(() => actions?.ImportFolder?.Invoke(Typed()));
                importButton.interactable = model.CanImport;
            }

            for (int i = 0; i < previewButtons.Length; i++)
            {
                if (previewButtons[i] == null) continue;
                int which = i;
                previewButtons[i].onClick.RemoveAllListeners();
                previewButtons[i].onClick.AddListener(() => ShowPreviewPage(which));
            }

            ShowPicture(tableFront, model.Front);
            ShowPicture(tableBack, model.Back);
            ShowPicture(consoleCard, model.Front != null ? model.Front : model.Back);
            ShowPreviewPage(previewPage);
        }

        private void ShowPicture(RawImage image, Texture picture)
        {
            if (image == null) return;
            image.texture = picture;
            CropToFill(image, picture);
            image.color = picture != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        }

        private void ShowPreviewPage(int which)
        {
            if (previewPages.Length == 0) return;
            previewPage = Mathf.Clamp(which, 0, previewPages.Length - 1);
            for (int i = 0; i < previewPages.Length; i++)
            {
                if (previewPages[i] != null && previewPages[i].activeSelf != (i == previewPage)) previewPages[i].SetActive(i == previewPage);
                if (i < previewFills.Length && previewFills[i] != null) previewFills[i].color = i == previewPage ? tabOn : tabOff;
            }

            Vector3 size = shownLarge ? new Vector3(1.3f, 1.3f, 1f) : Vector3.one;
            if (tableCards != null) tableCards.localScale = size;
            if (consoleCardHolder != null) consoleCardHolder.localScale = size;
        }

        private void ShowTab(int which)
        {
            if (tabPages.Length == 0) return;
            tab = Mathf.Clamp(which, 0, tabPages.Length - 1);
            for (int i = 0; i < tabPages.Length; i++)
            {
                if (tabPages[i] != null && tabPages[i].activeSelf != (i == tab)) tabPages[i].SetActive(i == tab);
                if (i < tabFills.Length && tabFills[i] != null) tabFills[i].color = i == tab ? tabOn : tabOff;
            }
        }

        // ALL, YOURS (your cards and new ones), NO PICTURE, ⚠ SHAPE (a picture that is not the card's shape).
        private void ApplyFilter()
        {
            string find = searchInput != null ? searchInput.text.Trim() : string.Empty;
            for (int i = 0; i < rowViews.Count; i++)
            {
                CardEditorRowView view = rowViews[i];
                if (view == null) continue;
                CardEditorRowModel row = view.Row;
                bool shown = find.Length == 0 || row.Name.IndexOf(find, StringComparison.OrdinalIgnoreCase) >= 0;
                if (shown && filter == 1) shown = row.Detail.Contains("Your card") || row.Detail.StartsWith("New card", StringComparison.Ordinal);
                if (shown && filter == 2) shown = row.Thumbnail == null;
                if (shown && filter == 3) shown = row.Thumbnail != null && !RightShape(row.Thumbnail);
                if (view.gameObject.activeSelf != shown) view.gameObject.SetActive(shown);
            }

            for (int i = 0; i < filterFills.Length; i++)
            {
                if (filterFills[i] != null) filterFills[i].color = i == filter ? filterOn : filterOff;
                if (i < filterLabels.Length && filterLabels[i] != null) filterLabels[i].color = i == filter ? filterOnWords : filterOffWords;
            }
        }

        private void CropToFill(RawImage image, Texture picture)
        {
            if (image == null) return;
            if (picture == null || picture.height <= 0 || bestSize.y <= 0f)
            {
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            float card = bestSize.x / bestSize.y;
            float shape = picture.width / (float)picture.height;
            image.uvRect = shape > card
                ? new Rect((1f - (card / shape)) * 0.5f, 0f, card / shape, 1f)
                : new Rect(0f, (1f - (shape / card)) * 0.5f, 1f, shape / card);
        }

        private bool RightShape(Texture picture)
        {
            if (picture == null || picture.height <= 0 || bestSize.y <= 0f) return true;
            float shape = picture.width / (float)picture.height;
            return Mathf.Abs(shape - (bestSize.x / bestSize.y)) <= 0.02f;
        }

        private string FileLine(Texture picture, bool front)
        {
            if (picture == null) return front ? "No front picture yet" : "The deck's back";
            FileWords.Clear();
            // Oxanium has no tick or warning sign: the words say it.
            FileWords.Append(RightShape(picture) ? "<color=#2FD46B>OK</color> · " : "<color=#FFB21E>WRONG SHAPE</color> · ");
            string name = picture.name ?? string.Empty;
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            // A picture of yours is stored under its card's key (…/front): say so instead of the key.
            if (name == "front" || name == "back") name = "your picture";
            if (name.Length > 22) name = name.Substring(0, 21) + "…";
            if (name.Length > 0) FileWords.Append(name).Append(" · ");
            FileWords.Append(picture.width).Append(" × ").Append(picture.height);
            return FileWords.ToString();
        }

        private void ShowWarning(Texture front)
        {
            if (shapeWarning == null) return;
            bool wrongShape = front != null && !RightShape(front);
            bool soft = front != null && (front.width < sharpSize.x || front.height < sharpSize.y);
            shapeWarning.SetActive(wrongShape || soft);
            if (shapeWarningLabel == null || front == null) return;
            shapeWarningLabel.text = wrongShape && soft
                ? $"This {front.width} × {front.height} picture is not the card's shape and is below the sharp size: it is cropped to fit and looks soft up close."
                : wrongShape
                    ? $"This {front.width} × {front.height} picture is not the card's shape ({bestSize.x:0} × {bestSize.y:0}): it is cropped to fit, never stretched."
                    : $"This {front.width} × {front.height} picture is below the sharp size ({sharpSize.x:0} × {sharpSize.y:0}): it looks soft up close.";
        }

        // GET THE TEMPLATE PNG: a card-sized picture with the safe area marked, saved where the game keeps your files.
        private void SaveTemplate()
        {
            int width = Mathf.RoundToInt(bestSize.x);
            int height = Mathf.RoundToInt(bestSize.y);
            Texture2D template = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            Color paper = new Color(0.17f, 0.19f, 0.20f, 1f);
            Color line = new Color(0.024f, 0.937f, 0.941f, 1f);
            int inset = Mathf.RoundToInt(width * 0.05f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool onSafe = (x == inset || x == width - inset || y == inset || y == height - inset)
                        && x >= inset && x <= width - inset && y >= inset && y <= height - inset;
                    pixels[(y * width) + x] = onSafe ? line : paper;
                }
            }

            template.SetPixels(pixels);
            template.Apply();
            string path = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "CardTemplate.png");
            try
            {
                System.IO.File.WriteAllBytes(path, template.EncodeToPNG());
                statusLabel.text = "Template saved: " + path;
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                statusLabel.text = "The template could not be saved: " + exception.Message;
            }
            finally
            {
                Destroy(template);
            }
        }

        public override void Unbind()
        {
            RemoveListeners();
            ReleaseRows();
            if (frontPreview != null) frontPreview.texture = null;
            if (backPreview != null) backPreview.texture = null;
            for (int i = 0; iconImages != null && i < iconImages.Length; i++)
            {
                if (iconImages[i] != null) iconImages[i].texture = null;
            }

            for (int i = 0; tabImages != null && i < tabImages.Length; i++)
            {
                if (tabImages[i] != null) tabImages[i].texture = null;
            }

            if (framePreview != null) framePreview.texture = null;

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

        // The icon places on the front preview: each shows its cell of the icon picture (the faint ring when empty).
        // A panel made before ICON-1 has no icon buttons; it works without them.
        private void BindIcons(CardEditorModel model)
        {
            bool show = model.IconAtlas != null && model.IconCells != null;
            for (int i = 0; iconButtons != null && i < iconButtons.Length; i++)
            {
                Button button = iconButtons[i];
                if (button == null) continue;
                bool placeShown = show && i < model.IconCells.Length;
                if (button.gameObject.activeSelf != placeShown) button.gameObject.SetActive(placeShown);
                button.interactable = model.CanChangeIcons;
                RawImage image = iconImages != null && i < iconImages.Length ? iconImages[i] : null;
                if (image == null) continue;
                image.texture = placeShown ? model.IconAtlas : null;
                if (placeShown) image.uvRect = model.IconCells[i];
            }
        }

        // The frame over the front preview and a tab under each icon place that has an icon (FRAME-1). A panel made
        // before FRAME-1 has none of these; it works without them.
        private void BindFrame(CardEditorModel model)
        {
            if (framePreview != null)
            {
                bool shown = model.FrameTexture != null;
                if (framePreview.gameObject.activeSelf != shown) framePreview.gameObject.SetActive(shown);
                framePreview.texture = model.FrameTexture;
                framePreview.color = model.FrameColor;
            }

            for (int i = 0; tabImages != null && i < tabImages.Length; i++)
            {
                RawImage tab = tabImages[i];
                if (tab == null) continue;
                Texture picture = i < 4 ? model.SideTabTexture : model.CornerTabTexture;
                bool shown = picture != null && model.TabShown != null && i < model.TabShown.Length && model.TabShown[i];
                if (tab.gameObject.activeSelf != shown) tab.gameObject.SetActive(shown);
                tab.texture = shown ? picture : null;
                tab.color = model.TabColor;
            }

            if (frameLabel != null) frameLabel.text = model.FrameName;
            if (framePrevious != null) framePrevious.interactable = model.CanChangeFrame;
            if (frameNext != null) frameNext.interactable = model.CanChangeFrame;
        }

        private void ReleaseRows()
        {
            for (int i = 0; i < rowViews.Count; i++)
            {
                if (rowViews[i] != null && uiService != null) uiService.Release(rowViews[i]);
            }

            for (int i = 0; i < deckRowViews.Count; i++)
            {
                if (deckRowViews[i] != null && uiService != null) uiService.Release(deckRowViews[i]);
            }

            rowViews.Clear();
            deckRowViews.Clear();
        }

        private void RemoveListeners()
        {
            Clear(dismissOverlayButton);
            Clear(closeButton);
            Clear(previousGameButton);
            Clear(nextGameButton);
            Clear(newCardButton);
            Clear(worksLikePrevious);
            Clear(worksLikeNext);
            Clear(chooseFrontButton);
            Clear(clearFrontButton);
            Clear(chooseBackButton);
            Clear(clearBackButton);
            Clear(newDeckButton);
            Clear(deleteButton);
            Clear(resetButton);
            Clear(saveButton);
            for (int i = 0; iconButtons != null && i < iconButtons.Length; i++) Clear(iconButtons[i]);
            Clear(framePrevious);
            Clear(frameNext);
            if (nameInput != null) nameInput.onValueChanged.RemoveAllListeners();
            if (descriptionInput != null) descriptionInput.onValueChanged.RemoveAllListeners();
            if (costInput != null) costInput.onValueChanged.RemoveAllListeners();
            if (codeInput != null) codeInput.onEndEdit.RemoveAllListeners();
            if (nameInput != null) nameInput.onEndEdit.RemoveAllListeners();
            if (descriptionInput != null) descriptionInput.onEndEdit.RemoveAllListeners();
            if (costInput != null) costInput.onEndEdit.RemoveAllListeners();
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleTyping(string _)
        {
            RefreshPreviewWords();
            ShowSaved(false);
        }

        // Auto-save: leaving a field writes the card as typed.
        private void HandleTypingDone(string _)
        {
            CardEditorActions now = actions;
            if (now?.AutoSave != null) ShowSaved(now.AutoSave(Typed()));
        }

        private void ShowSaved(bool saved)
        {
            if (savedTick != null && savedTick.activeSelf != saved) savedTick.SetActive(saved);
        }

        // Leaving the studio or choosing another card first saves what is typed.
        private void HandleClose()
        {
            CardEditorActions now = actions;
            now?.AutoSave?.Invoke(Typed());
            now?.Close?.Invoke();
        }

        private void HandlePreviousGame() => actions?.CycleGame?.Invoke(-1, Typed());
        private void HandleNextGame() => actions?.CycleGame?.Invoke(1, Typed());
        private void HandleSelect(string id)
        {
            CardEditorActions now = actions;
            now?.AutoSave?.Invoke(Typed());
            now?.Select?.Invoke(id);
        }

        private void HandleNewCard() => actions?.NewCard?.Invoke(Typed());
        private void HandlePreviousWorksLike() => actions?.CycleWorksLike?.Invoke(-1, Typed());
        private void HandleNextWorksLike() => actions?.CycleWorksLike?.Invoke(1, Typed());
        private void HandleToggleDeck(string deckId) => actions?.ToggleDeck?.Invoke(deckId, Typed());
        private void HandleDeckCopies(string deckId, int step) => actions?.ChangeDeckCopies?.Invoke(deckId, step, Typed());
        private void HandleNewDeck() => actions?.NewDeck?.Invoke(Typed());
        private void HandleChooseFront() => actions?.ChoosePicture?.Invoke(true, Typed());
        private void HandleClearFront() => actions?.ClearPicture?.Invoke(true, Typed());
        private void HandleChooseBack() => actions?.ChoosePicture?.Invoke(false, Typed());
        private void HandleClearBack() => actions?.ClearPicture?.Invoke(false, Typed());
        private void HandleSave() => actions?.Save?.Invoke(Typed());
        private void HandleReset() => actions?.Reset?.Invoke();
        private void HandleDelete() => actions?.Delete?.Invoke();
        private void HandleIcon(int place) => actions?.CycleIcon?.Invoke(place, Typed());
        private void HandlePreviousFrame() => actions?.CycleFrame?.Invoke(-1, Typed());
        private void HandleNextFrame() => actions?.CycleFrame?.Invoke(1, Typed());
    }
}
