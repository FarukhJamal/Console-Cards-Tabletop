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
        // PLACE-1: how many players the table is dealt for (games that let players choose).
        public bool ShowPlayers;
        public string PlayersText = string.Empty;
        public bool CanChangePlayers;
        // MODE-1 / DIR-1 (owner, 2026-10-22): the win mode and the turn direction (games that let players choose).
        public bool ShowWinMode;
        public string WinModeText = string.Empty;
        public bool ShowTurns;
        public string TurnsText = string.Empty;
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

        // UI-2 front end (owner, 2026-10-24: the Milanote user journey).
        /// <summary>The screen to open on (null: stay on the screen shown).</summary>
        public FrontEndPage? OpenAt;
        public bool IsEmptyTable;
        public IReadOnlyList<ProfileRowModel> Profiles = Array.Empty<ProfileRowModel>();
        public Texture AvatarPicture;
        public string AvatarName = string.Empty;
        public bool CanChangeAvatar;
        public int AvatarIndex;
        public int AvatarCount;
        public int PlayerCount;
        public IReadOnlyList<string> PlayerNames = Array.Empty<string>();
        public IReadOnlyList<Color> PlayerColors = Array.Empty<Color>();
        public IReadOnlyList<string> PlayerColorNames = Array.Empty<string>();
        // Owner, 2026-10-25: Game details as in the reference (IN THE BOX list, pictures, three feature tiles).
        public IReadOnlyList<FrontEndBoxItem> BoxItems = Array.Empty<FrontEndBoxItem>();
        public IReadOnlyList<Texture> Gallery = Array.Empty<Texture>();
        public IReadOnlyList<FrontEndFeature> Features = Array.Empty<FrontEndFeature>();
        /// <summary>The chosen deck's short name (the Review grid); <see cref="Deck"/> is the full line.</summary>
        public string DeckName = string.Empty;
        /// <summary>The chosen deck's name and size, under DECK SET on the Setup step.</summary>
        public string DeckDetail = string.Empty;
        public bool CanChangeColours;
        /// <summary>MP-2: the Lobby and Room screens (null: online play is not in this build's front end).</summary>
        public OnlineScreenModel Online;
        // Owner, 2026-10-29: the main menu's SETTINGS screen (the value shown between each row's < >).
        public string LightText = string.Empty;
        public string ColoursText = string.Empty;
        public string QualityText = string.Empty;
        public string RoomSoundText = string.Empty;
        /// <summary>CREATE › YOUR GAMES (board 4): the games this profile made, newest first.</summary>
        public IReadOnlyList<FrontEndMyGame> MyGames = Array.Empty<FrontEndMyGame>();

        // Boards 1–3 (Home, Library, Game sheet).
        /// <summary>Home's CONTINUE card: a game table is behind the front end (CONTINUE goes back to it).</summary>
        public bool ContinueInProgress;
        /// <summary>Home's card shows at all (the table in progress, or the game played last with PLAY AGAIN).</summary>
        public bool ContinueShown;
        public string ContinueName = string.Empty;
        public string ContinueLine = string.Empty;
        public string ContinueSubLine = string.Empty;
        public Texture ContinuePicture;
        /// <summary>The Library's tiles (the games, then yours; not the Empty Table, which is SANDBOX).</summary>
        public IReadOnlyList<FrontEndLibraryTile> LibraryTiles = Array.Empty<FrontEndLibraryTile>();
        /// <summary>The Game sheet's words: a short line about the game and its first rules.</summary>
        public string Tagline = string.Empty;
        public IReadOnlyList<string> HowToPlay = Array.Empty<string>();

        // PROFILE-2 (boards 19 and 19b): the players on this computer and the one playing now.
        /// <summary>Every profile, the one playing now first.</summary>
        public IReadOnlyList<FrontEndProfile> ProfileCards = Array.Empty<FrontEndProfile>();
        /// <summary>The seat colours a player can pick, and the one picked (-1: none yet).</summary>
        public IReadOnlyList<Color> ProfileColours = Array.Empty<Color>();
        public int ProfileColour = -1;
        /// <summary>Every avatar picture, in the set's order (<see cref="AvatarIndex"/> is the one picked).</summary>
        public IReadOnlyList<Texture> AvatarChoices = Array.Empty<Texture>();
        public IReadOnlyList<string> AvatarChoiceNames = Array.Empty<string>();
        public string StatGames = "0";
        public string StatWins = "0";
        public string StatLastGame = "-";
        public string StatYourGames = "0";
        public bool CanDeleteProfile;
        /// <summary>DELETE PLAYER was pressed once: the next press removes the player.</summary>
        public bool ProfileDeleteArmed;
        /// <summary>The last change to the profile was saved (the SAVED tick).</summary>
        public bool ProfileSaved = true;
    }

    /// <summary>PROFILE-2: one player on this computer (board 19's list, board 19b's switch list).</summary>
    public readonly struct FrontEndProfile
    {
        public FrontEndProfile(string id, string name, string line, Texture avatar, Color colour, bool active)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Line = line ?? string.Empty;
            Avatar = avatar;
            Colour = colour;
            Active = active;
        }

        public string Id { get; }
        public string Name { get; }
        public string Line { get; }
        public Texture Avatar { get; }
        public Color Colour { get; }
        public bool Active { get; }
    }

    /// <summary>A game on the Library (board 2): its tag (2–4 PLAYERS, YOUR GAME), a line, and whether it was played last.</summary>
    public readonly struct FrontEndLibraryTile
    {
        public FrontEndLibraryTile(string id, string name, string line, string tag, bool mine, Texture picture, bool chosen)
            : this(id, name, line, tag, mine, picture, chosen, false)
        {
        }

        /// <param name="comingSoon">Board 2: a game that is not out yet (COMING SOON, dimmed, not clickable).</param>
        public FrontEndLibraryTile(string id, string name, string line, string tag, bool mine, Texture picture, bool chosen,
            bool comingSoon)
        {
            ComingSoon = comingSoon;
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Line = line ?? string.Empty;
            Tag = tag ?? string.Empty;
            Mine = mine;
            Picture = picture;
            Chosen = chosen;
        }

        public string Id { get; }
        public string Name { get; }
        public string Line { get; }
        public string Tag { get; }
        public bool Mine { get; }
        public Texture Picture { get; }
        public bool Chosen { get; }
        public bool ComingSoon { get; }
    }

    /// <summary>One line of IN THE BOX on Game details. <see cref="Icon"/> names a picture in the screen's IconSet.</summary>
    public readonly struct FrontEndBoxItem
    {
        public FrontEndBoxItem(string icon, string label, int count)
        {
            Icon = icon ?? string.Empty;
            Label = label ?? string.Empty;
            Count = count;
        }

        public string Icon { get; }
        public string Label { get; }
        public int Count { get; }
    }

    /// <summary>One feature tile under the pictures on Game details (icon from the IconSet, a title, a line).</summary>
    public readonly struct FrontEndFeature
    {
        public FrontEndFeature(string icon, string title, string line)
        {
            Icon = icon ?? string.Empty;
            Title = title ?? string.Empty;
            Line = line ?? string.Empty;
        }

        public string Icon { get; }
        public string Title { get; }
        public string Line { get; }
    }

    /// <summary>One game you made, on CREATE › YOUR GAMES: its name and "2–4 players · changed 3 days ago".</summary>
    public readonly struct FrontEndMyGame
    {
        public FrontEndMyGame(string id, string name, string detail, bool deleteArmed)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            DeleteArmed = deleteArmed;
        }

        public string Id { get; }
        public string Name { get; }
        public string Detail { get; }
        /// <summary>DELETE was pressed once: the next press deletes.</summary>
        public bool DeleteArmed { get; }
    }

    /// <summary>The game selection menu's actions, supplied by the game.</summary>
    public sealed class GameSelectActions
    {
        public Action<string> SelectGame;
        public Action<int> CycleMode;
        public Action<int> CyclePlayers;
        public Action<int> CycleWinMode;
        public Action<int> CycleTurns;
        public Action<int> CycleDeck;
        public Action OpenProfile;
        public Action OpenCardArt;
        public Action OpenCardEditor;
        public Action OpenDeckEditor;
        public Action Start;
        public Action Delete;
        public Action Close;

        // UI-2 front end.
        public Action<string> PlayAs;
        public Action<int> CycleAvatar;
        public Action Customize;
        public Action Sandbox;
        public Action Exit;
        /// <summary>The pawn arrows on the Players step: seat index, -1 or +1.</summary>
        public Action<int, int> CycleSeatColour;
        /// <summary>CREATE > TABLE: the table's shape, size and edge (Table tools).</summary>
        public Action OpenTableTools;

        // MP-2: online play (PLAY ONLINE, the Lobby and the Room).
        public Action OpenOnline;
        /// <summary>The room's name and whether it is private.</summary>
        public Action<string, bool> CreateRoom;
        /// <summary>A room's code (from the list or typed).</summary>
        public Action<string> JoinRoom;
        public Action LeaveRoom;
        public Action ToggleReady;
        public Action<int> CycleOnlineColour;
        public Action<int> CycleOnlineSeat;

        // Owner, 2026-10-29: the main menu's SETTINGS screen, -1 or +1.
        public Action<int> CycleLight;
        public Action<int> CycleColours;
        public Action<int> CycleQuality;
        public Action<int> CycleRoomSound;
        /// <summary>Board 6: the main menu's SETTINGS opens the full Settings page (false: it is missing).</summary>
        public Func<bool> OpenBoardSettings;
        // CREATE (board 4): MAKE A GAME, and PLAY / EDIT / Copy / Rename / Delete for a game you made (by its id).
        public Action MakeGame;
        public Action<string> PlayMyGame;
        public Action<string> EditMyGame;
        public Action<string> CopyMyGame;
        public Action<string, string> RenameMyGame;
        public Action<string> DeleteMyGame;
        /// <summary>Home (board 1): PLAY AGAIN starts the game played last with its last settings.</summary>
        public Action PlayAgain;

        // PROFILE-2 (board 19): the player playing now.
        public Action<string> RenameProfile;
        public Action<int> PickProfileColour;
        public Action<int> PickAvatar;
        public Action NewProfile;
        public Action DeleteProfile;
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
        [Tooltip("Choose how many players (PLACE-1). Added by Create Missing Gameplay UI Prefabs.")]
        [SerializeField] private GameObject playersRow;
        [SerializeField] private Button previousPlayersButton;
        [SerializeField] private Button nextPlayersButton;
        [SerializeField] private Text playersLabel;
        [Tooltip("Choose the win mode (MODE-1). Added by Create Missing Gameplay UI Prefabs.")]
        [SerializeField] private GameObject winModeRow;
        [SerializeField] private Button previousWinModeButton;
        [SerializeField] private Button nextWinModeButton;
        [SerializeField] private Text winModeLabel;
        [Tooltip("Choose the turn direction (DIR-1). Added by Create Missing Gameplay UI Prefabs.")]
        [SerializeField] private GameObject turnsRow;
        [SerializeField] private Button previousTurnsButton;
        [SerializeField] private Button nextTurnsButton;
        [SerializeField] private Text turnsLabel;
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
            if (playersRow != null) playersRow.SetActive(model.ShowPlayers);
            if (playersLabel != null) playersLabel.text = model.PlayersText;
            if (previousPlayersButton != null) previousPlayersButton.interactable = model.CanChangePlayers;
            if (nextPlayersButton != null) nextPlayersButton.interactable = model.CanChangePlayers;
            if (winModeRow != null) winModeRow.SetActive(model.ShowWinMode);
            if (winModeLabel != null) winModeLabel.text = model.WinModeText;
            if (turnsRow != null) turnsRow.SetActive(model.ShowTurns);
            if (turnsLabel != null) turnsLabel.text = model.TurnsText;
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
            if (previousPlayersButton != null) previousPlayersButton.onClick.AddListener(HandlePreviousPlayers);
            if (nextPlayersButton != null) nextPlayersButton.onClick.AddListener(HandleNextPlayers);
            if (previousWinModeButton != null) previousWinModeButton.onClick.AddListener(HandlePreviousWinMode);
            if (nextWinModeButton != null) nextWinModeButton.onClick.AddListener(HandleNextWinMode);
            if (previousTurnsButton != null) previousTurnsButton.onClick.AddListener(HandlePreviousTurns);
            if (nextTurnsButton != null) nextTurnsButton.onClick.AddListener(HandleNextTurns);
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
            Clear(previousPlayersButton);
            Clear(nextPlayersButton);
            Clear(previousWinModeButton);
            Clear(nextWinModeButton);
            Clear(previousTurnsButton);
            Clear(nextTurnsButton);
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
        private void HandlePreviousPlayers() => actions?.CyclePlayers?.Invoke(-1);
        private void HandleNextPlayers() => actions?.CyclePlayers?.Invoke(1);
        private void HandlePreviousWinMode() => actions?.CycleWinMode?.Invoke(-1);
        private void HandleNextWinMode() => actions?.CycleWinMode?.Invoke(1);
        private void HandlePreviousTurns() => actions?.CycleTurns?.Invoke(-1);
        private void HandleNextTurns() => actions?.CycleTurns?.Invoke(1);
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
