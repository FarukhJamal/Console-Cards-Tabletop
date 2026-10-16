using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>One card in the deck editor: its picture, name and kind, and its copies in the selected deck.</summary>
    public readonly struct DeckEditorCardRowModel
    {
        public DeckEditorCardRowModel(string cardId, string name, string detail, Texture thumbnail, int copies)
        {
            CardId = cardId ?? string.Empty;
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            Thumbnail = thumbnail;
            Copies = copies;
        }

        public string CardId { get; }
        public string Name { get; }
        public string Detail { get; }
        public Texture Thumbnail { get; }
        public int Copies { get; }
    }

    /// <summary>What the deck editor shows (doc 27 List 2, L2-3).</summary>
    public sealed class DeckEditorModel
    {
        public string Title = "DECK EDITOR";
        public string GameName = string.Empty;
        public bool CanChangeGame;
        public IReadOnlyList<CardEditorRowModel> Decks = Array.Empty<CardEditorRowModel>();
        public string DeckName = string.Empty;
        public bool CanRename;
        public bool CanDelete;
        public string Summary = string.Empty;
        public string FilterText = string.Empty;
        public IReadOnlyList<DeckEditorCardRowModel> Cards = Array.Empty<DeckEditorCardRowModel>();
        public bool HasChanges;
        public string Status = string.Empty;
    }

    /// <summary>The deck editor's actions, supplied by the game.</summary>
    public sealed class DeckEditorActions
    {
        public Action<int> CycleGame;
        public Action<string> SelectDeck;
        public Action NewDeck;
        public Action CopyDeck;
        public Action<string> RenameDeck;
        public Action DeleteDeck;
        public Action ToggleFilter;
        public Action<string, int> ChangeCopies;
        public Action Undo;
        public Action Save;
        public Action Close;
    }

    /// <summary>
    /// Deck editor (doc 27 List 2, L2-3): pick a game; its decks (the game's own deck, then yours); and the selected
    /// deck's cards with copies. Decks are made, copied, renamed and deleted here. An authored Final UI prefab; the
    /// deck list uses the card editor's row prefab and the cards use the deck editor's card row. Changes are kept
    /// until Save.
    /// </summary>
    public sealed class DeckEditorPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;

        [Header("Game")]
        [SerializeField] private Button previousGameButton;
        [SerializeField] private Button nextGameButton;
        [SerializeField] private Text gameLabel;

        [Header("Decks")]
        [SerializeField] private RectTransform deckRowsRoot;
        [SerializeField] private ScrollRect deckScroll;
        [SerializeField] private InputField deckNameInput;
        [SerializeField] private Button newDeckButton;
        [SerializeField] private Button copyDeckButton;
        [SerializeField] private Button renameDeckButton;
        [SerializeField] private Button deleteDeckButton;

        [Header("Cards in the deck")]
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Button filterButton;
        [SerializeField] private Text filterLabel;
        [SerializeField] private RectTransform cardRowsRoot;
        [SerializeField] private ScrollRect cardScroll;

        [Header("Footer")]
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button saveButton;

        private readonly List<CardEditorRowView> deckRowViews = new List<CardEditorRowView>();
        private readonly List<DeckEditorCardRowView> cardRowViews = new List<DeckEditorCardRowView>();
        private IRuntimeUiService uiService;
        private DeckEditorActions actions;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || titleLabel == null || previousGameButton == null
                || nextGameButton == null || gameLabel == null || deckRowsRoot == null || deckScroll == null
                || deckNameInput == null || newDeckButton == null || copyDeckButton == null || renameDeckButton == null
                || deleteDeckButton == null || summaryLabel == null || filterButton == null || filterLabel == null
                || cardRowsRoot == null || cardScroll == null || statusLabel == null || undoButton == null
                || saveButton == null || uiService == null)
            {
                throw new InvalidOperationException("DeckEditorPanelView requires its authored references.");
            }
        }

        /// <summary>Shows the panel; call again with a new model to refresh it (keeps both lists' scroll places).</summary>
        public void Bind(DeckEditorModel model, DeckEditorActions panelActions)
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
            deckNameInput.text = model.DeckName;
            deckNameInput.interactable = model.CanRename;
            renameDeckButton.interactable = model.CanRename;
            deleteDeckButton.interactable = model.CanDelete;
            summaryLabel.text = model.Summary;
            filterLabel.text = model.FilterText;
            undoButton.interactable = model.HasChanges;
            statusLabel.text = model.Status;

            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            previousGameButton.onClick.AddListener(HandlePreviousGame);
            nextGameButton.onClick.AddListener(HandleNextGame);
            newDeckButton.onClick.AddListener(HandleNewDeck);
            copyDeckButton.onClick.AddListener(HandleCopyDeck);
            renameDeckButton.onClick.AddListener(HandleRename);
            deleteDeckButton.onClick.AddListener(HandleDelete);
            filterButton.onClick.AddListener(HandleFilter);
            undoButton.onClick.AddListener(HandleUndo);
            saveButton.onClick.AddListener(HandleSave);

            float deckPlace = deckScroll.verticalNormalizedPosition;
            float cardPlace = cardScroll.verticalNormalizedPosition;
            bool keepDecks = deckRowViews.Count == model.Decks.Count && deckRowViews.Count > 0;
            bool keepCards = cardRowViews.Count == model.Cards.Count && cardRowViews.Count > 0;
            ReleaseRows();
            for (int i = 0; i < model.Decks.Count; i++)
            {
                CardEditorRowView row = uiService.AcquirePooled<CardEditorRowView>(UiPrefabIds.CardEditorRow, deckRowsRoot);
                row.Bind(model.Decks[i], HandleSelectDeck);
                row.Show();
                deckRowViews.Add(row);
            }

            for (int i = 0; i < model.Cards.Count; i++)
            {
                DeckEditorCardRowView row =
                    uiService.AcquirePooled<DeckEditorCardRowView>(UiPrefabIds.DeckEditorCardRow, cardRowsRoot);
                row.Bind(model.Cards[i], HandleCopies);
                row.Show();
                cardRowViews.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            deckScroll.verticalNormalizedPosition = keepDecks ? deckPlace : 1f;
            cardScroll.verticalNormalizedPosition = keepCards ? cardPlace : 1f;
        }

        public override void Unbind()
        {
            RemoveListeners();
            ReleaseRows();
            actions = null;
        }

        private void ReleaseRows()
        {
            for (int i = 0; i < deckRowViews.Count; i++)
            {
                if (deckRowViews[i] != null && uiService != null) uiService.Release(deckRowViews[i]);
            }

            for (int i = 0; i < cardRowViews.Count; i++)
            {
                if (cardRowViews[i] != null && uiService != null) uiService.Release(cardRowViews[i]);
            }

            deckRowViews.Clear();
            cardRowViews.Clear();
        }

        private void RemoveListeners()
        {
            Clear(dismissOverlayButton);
            Clear(closeButton);
            Clear(previousGameButton);
            Clear(nextGameButton);
            Clear(newDeckButton);
            Clear(copyDeckButton);
            Clear(renameDeckButton);
            Clear(deleteDeckButton);
            Clear(filterButton);
            Clear(undoButton);
            Clear(saveButton);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleClose() => actions?.Close?.Invoke();
        private void HandlePreviousGame() => actions?.CycleGame?.Invoke(-1);
        private void HandleNextGame() => actions?.CycleGame?.Invoke(1);
        private void HandleSelectDeck(string deckId) => actions?.SelectDeck?.Invoke(deckId);
        private void HandleNewDeck() => actions?.NewDeck?.Invoke();
        private void HandleCopyDeck() => actions?.CopyDeck?.Invoke();
        private void HandleRename() => actions?.RenameDeck?.Invoke(deckNameInput.text);
        private void HandleDelete() => actions?.DeleteDeck?.Invoke();
        private void HandleFilter() => actions?.ToggleFilter?.Invoke();
        private void HandleCopies(string cardId, int step) => actions?.ChangeCopies?.Invoke(cardId, step);
        private void HandleUndo() => actions?.Undo?.Invoke();
        private void HandleSave() => actions?.Save?.Invoke();
    }
}
