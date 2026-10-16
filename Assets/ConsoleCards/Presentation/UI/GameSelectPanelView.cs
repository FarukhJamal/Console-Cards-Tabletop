using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>What the game selection menu shows (doc 27 List 2, L2-4).</summary>
    public sealed class GameSelectModel
    {
        public string Title = "NEW TABLE";
        public IReadOnlyList<CardEditorRowModel> Games = Array.Empty<CardEditorRowModel>();
        public Texture Picture;
        public string GameName = string.Empty;
        public string Detail = string.Empty;
        public bool ShowMode;
        public string ModeName = string.Empty;
        public bool CanChangeMode;
        public string Rules = string.Empty;
        public string Deck = string.Empty;
        public bool CanChangeDeck;
        public string ProfileName = string.Empty;
        public bool CanOpenCardArt;
        public bool CanEditCards;
        public bool CanEditDecks;
        public bool CanStart;
        public bool CanDelete;
        public string DeleteText = "Delete game";
        public string StartText = "Start";
        public string Status = string.Empty;
    }

    /// <summary>The game selection menu's actions, supplied by the game.</summary>
    public sealed class GameSelectActions
    {
        public Action<string> SelectGame;
        public Action<int> CycleMode;
        public Action<int> CycleDeck;
        public Action OpenProfile;
        public Action OpenCardArt;
        public Action OpenCardEditor;
        public Action OpenDeckEditor;
        public Action Start;
        public Action Delete;
        public Action Close;
    }

    /// <summary>
    /// Game selection menu (doc 27 List 2, L2-4): Table menu > New table… opens it. Every game (the Empty Table, Trap
    /// Floor, and each game box in the library), the chosen game's picture, players, mode, rules in play and deck,
    /// who is playing, and the places to make the game yours (card art, card editor, deck editor). The deck line's
    /// &lt; &gt; choose the deck to play with (L2-5). Start replaces the table. An authored Final UI prefab; the game list
    /// uses the card editor's row prefab.
    /// </summary>
    public sealed class GameSelectPanelView : ReusableUiView
    {
        [SerializeField] private Button dismissOverlayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;

        [Header("Games")]
        [SerializeField] private RectTransform gameRowsRoot;
        [SerializeField] private ScrollRect gameScroll;

        [Header("The chosen game")]
        [SerializeField] private RawImage picture;
        [SerializeField] private Text gameNameLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private GameObject modeRow;
        [SerializeField] private Button previousModeButton;
        [SerializeField] private Button nextModeButton;
        [SerializeField] private Text modeLabel;
        [SerializeField] private Text rulesLabel;
        [SerializeField] private Text deckLabel;
        [Tooltip("Choose the deck to play with (L2-5). Added by Create Missing Gameplay UI Prefabs.")]
        [SerializeField] private Button previousDeckButton;
        [SerializeField] private Button nextDeckButton;

        [Header("Who and make it yours")]
        [SerializeField] private Text profileLabel;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button cardArtButton;
        [SerializeField] private Button cardEditorButton;
        [SerializeField] private Button deckEditorButton;

        [Header("Footer")]
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private Text startLabel;
        [Tooltip("Delete one of your games (doc 29). Added by Create Missing Gameplay UI Prefabs.")]
        [SerializeField] private Button deleteGameButton;
        [SerializeField] private Text deleteGameLabel;

        private readonly List<CardEditorRowView> gameRowViews = new List<CardEditorRowView>();
        private IRuntimeUiService uiService;
        private GameSelectActions actions;

        public void Initialize(IRuntimeUiService service)
        {
            uiService = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void ValidateReferences()
        {
            if (dismissOverlayButton == null || closeButton == null || titleLabel == null || gameRowsRoot == null
                || gameScroll == null || picture == null || gameNameLabel == null || detailLabel == null
                || modeRow == null || previousModeButton == null || nextModeButton == null || modeLabel == null
                || rulesLabel == null || deckLabel == null || profileLabel == null || profileButton == null
                || cardArtButton == null || cardEditorButton == null || deckEditorButton == null || statusLabel == null
                || startButton == null || startLabel == null || uiService == null)
            {
                throw new InvalidOperationException("GameSelectPanelView requires its authored references.");
            }
        }

        public void Bind(GameSelectModel model, GameSelectActions panelActions)
        {
            RequireAcquired();
            ValidateReferences();
            if (model == null) throw new ArgumentNullException(nameof(model));
            actions = panelActions ?? throw new ArgumentNullException(nameof(panelActions));
            RemoveListeners();

            titleLabel.text = model.Title;
            picture.texture = model.Picture;
            picture.color = model.Picture != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            gameNameLabel.text = model.GameName;
            detailLabel.text = model.Detail;
            modeRow.SetActive(model.ShowMode);
            modeLabel.text = model.ModeName;
            previousModeButton.interactable = model.CanChangeMode;
            nextModeButton.interactable = model.CanChangeMode;
            rulesLabel.text = model.Rules;
            deckLabel.text = model.Deck;
            if (previousDeckButton != null) previousDeckButton.interactable = model.CanChangeDeck;
            if (nextDeckButton != null) nextDeckButton.interactable = model.CanChangeDeck;
            profileLabel.text = model.ProfileName;
            cardArtButton.interactable = model.CanOpenCardArt;
            cardEditorButton.interactable = model.CanEditCards;
            deckEditorButton.interactable = model.CanEditDecks;
            startButton.interactable = model.CanStart;
            startLabel.text = model.StartText;
            if (deleteGameButton != null) deleteGameButton.gameObject.SetActive(model.CanDelete);
            if (deleteGameLabel != null) deleteGameLabel.text = model.DeleteText;
            statusLabel.text = model.Status;

            dismissOverlayButton.onClick.AddListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
            previousModeButton.onClick.AddListener(HandlePreviousMode);
            nextModeButton.onClick.AddListener(HandleNextMode);
            if (previousDeckButton != null) previousDeckButton.onClick.AddListener(HandlePreviousDeck);
            if (nextDeckButton != null) nextDeckButton.onClick.AddListener(HandleNextDeck);
            profileButton.onClick.AddListener(HandleProfile);
            cardArtButton.onClick.AddListener(HandleCardArt);
            cardEditorButton.onClick.AddListener(HandleCardEditor);
            deckEditorButton.onClick.AddListener(HandleDeckEditor);
            startButton.onClick.AddListener(HandleStart);
            if (deleteGameButton != null) deleteGameButton.onClick.AddListener(HandleDelete);

            float place = gameScroll.verticalNormalizedPosition;
            bool keepPlace = gameRowViews.Count == model.Games.Count && gameRowViews.Count > 0;
            ReleaseRows();
            for (int i = 0; i < model.Games.Count; i++)
            {
                CardEditorRowView row = uiService.AcquirePooled<CardEditorRowView>(UiPrefabIds.CardEditorRow, gameRowsRoot);
                row.Bind(model.Games[i], HandleSelectGame);
                row.Show();
                gameRowViews.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            gameScroll.verticalNormalizedPosition = keepPlace ? place : 1f;
        }

        public override void Unbind()
        {
            RemoveListeners();
            ReleaseRows();
            if (picture != null) picture.texture = null;
            actions = null;
        }

        private void ReleaseRows()
        {
            for (int i = 0; i < gameRowViews.Count; i++)
            {
                if (gameRowViews[i] != null && uiService != null) uiService.Release(gameRowViews[i]);
            }

            gameRowViews.Clear();
        }

        private void RemoveListeners()
        {
            Clear(dismissOverlayButton);
            Clear(closeButton);
            Clear(previousModeButton);
            Clear(nextModeButton);
            Clear(previousDeckButton);
            Clear(nextDeckButton);
            Clear(profileButton);
            Clear(cardArtButton);
            Clear(cardEditorButton);
            Clear(deckEditorButton);
            Clear(startButton);
            Clear(deleteGameButton);
        }

        private static void Clear(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void HandleClose() => actions?.Close?.Invoke();
        private void HandleSelectGame(string id) => actions?.SelectGame?.Invoke(id);
        private void HandlePreviousMode() => actions?.CycleMode?.Invoke(-1);
        private void HandleNextMode() => actions?.CycleMode?.Invoke(1);
        private void HandlePreviousDeck() => actions?.CycleDeck?.Invoke(-1);
        private void HandleNextDeck() => actions?.CycleDeck?.Invoke(1);
        private void HandleProfile() => actions?.OpenProfile?.Invoke();
        private void HandleCardArt() => actions?.OpenCardArt?.Invoke();
        private void HandleCardEditor() => actions?.OpenCardEditor?.Invoke();
        private void HandleDeckEditor() => actions?.OpenDeckEditor?.Invoke();
        private void HandleStart() => actions?.Start?.Invoke();
        private void HandleDelete() => actions?.Delete?.Invoke();
    }
}
