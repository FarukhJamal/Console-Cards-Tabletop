using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>The front end's screens, in the order of the Milanote user journey.</summary>
    public enum FrontEndPage
    {
        Home = 0,
        Create = 1,
        Profile = 2,
        Library = 3,
        Details = 4,
        Setup = 5,
        Players = 6,
        Review = 7,

        // MP-2: online play (FE_Lobby, FE_Room). A front end made before them has neither; it works as before.
        Lobby = 8,
        Room = 9,

        // Owner, 2026-10-29: the main menu's SETTINGS (FE_Settings: light, colours, quality, room sound). Optional:
        // a front end made before it opens the player profile there, as before.
        Settings = 10,
    }

    /// <summary>
    /// UI-2 (owner, 2026-10-24: the Milanote user journey): the front end. Main menu (PLAY, CREATE, SANDBOX), Player
    /// profile (profiles and avatar), Game library, Game details, then three setup steps (Game setup, Players,
    /// Review) and START GAME. It shows the same model and actions as the New table… window
    /// (<see cref="GameSelectModel"/>), so choosing a game, a mode or a player count works exactly as before.
    /// An authored Final UI prefab (Console Cards > UI > Apply Kit Look builds it); the parts are found by name once,
    /// when the view is first used.
    /// </summary>
    public sealed partial class FrontEndView : ReusableUiView
    {
        public const int ProfileRowCount = 4;
        public const int PlayerRowCount = 8;
        public const int BoxRowCount = 6;
        public const int GalleryThumbCount = 3;
        public const int FeatureTileCount = 3;
        private const float ProfileRowTop = 296f;
        private const float ProfileRowPitch = 109f;

        [Header("Art (set by the builder)")]
        [SerializeField] private Sprite gameEntry;
        [SerializeField] private Sprite selectedGameEntry;
        [SerializeField] private Sprite profileEntry;
        [SerializeField] private Sprite selectedProfileEntry;
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private Sprite discSprite;
        [SerializeField] private Texture fallbackAvatar;
        [SerializeField] private Color activeTab = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color idleTab = new Color(0.91f, 0.925f, 0.929f, 1f);
        [SerializeField, HideInInspector] private int builtVersion;

        private readonly GameObject[] pages = new GameObject[Enum.GetNames(typeof(FrontEndPage)).Length];
        private readonly List<GameEntryParts> gameEntries = new List<GameEntryParts>();
        private readonly ProfileRowParts[] profileRows = new ProfileRowParts[ProfileRowCount];
        private readonly PlayerRowParts[] playerRows = new PlayerRowParts[PlayerRowCount];
        private readonly PlayerRowParts[] reviewPlayers = new PlayerRowParts[PlayerRowCount];
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> texts = new Dictionary<string, Text>();
        private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, RawImage> pictures = new Dictionary<string, RawImage>();

        private bool resolved;
        private FrontEndPage page = FrontEndPage.Home;
        private GameSelectModel model;
        private GameSelectActions actions;
        private Action openGuide;
        private RectTransform libraryContent;
        private GameObject gameEntryTemplate;
        private RectTransform tabHighlight;
        private RectTransform addProfileRow;
        private string highlightTab;
        private readonly List<Image> avatarDots = new List<Image>();
        private readonly Dictionary<string, Image> detailIcons = new Dictionary<string, Image>();
        private int galleryFirst;
        private int galleryChosen;
        private string galleryGame;
        // The Players step rows (inside Viewport since FE_Players design 3, scrollable).
        private string playerRowsPath = "Pages/Players/Rows";

        private sealed class GameEntryParts
        {
            public GameObject root;
            public Button button;
            public Image plate;
            public RawImage picture;
            public Text name;
            public Text detail;
            public Text players;
        }

        private sealed class ProfileRowParts
        {
            public GameObject root;
            public Button button;
            public RawImage avatar;
            public Text small;
            public Text name;
        }

        private sealed class PlayerRowParts
        {
            public GameObject root;
            public Text badge;
            public Image badgePlate;
            public RawImage avatar;
            public Text name;
            public Text controller;
            public Image dot;
            public Image pawn;
        }

        public FrontEndPage Page => page;

        /// <summary>The builder's version this prefab was made with (an older one is built again).</summary>
        public int BuiltVersion => builtVersion;

        public void ValidateReferences() => Resolve();

        /// <summary>Shows the front end with the New table… model; <paramref name="guide"/> opens the help.</summary>
        public void Bind(GameSelectModel selectModel, GameSelectActions selectActions, Action guide)
        {
            RequireAcquired();
            Resolve();
            model = selectModel ?? throw new ArgumentNullException(nameof(selectModel));
            actions = selectActions ?? throw new ArgumentNullException(nameof(selectActions));
            openGuide = guide;
            ShowBackdropOnly(false);
            if (model.OpenAt.HasValue) page = model.OpenAt.Value;
            if (page >= FrontEndPage.Details && page <= FrontEndPage.Review && model.IsEmptyTable) page = FrontEndPage.Library;
            if (pages[(int)page] == null) page = FrontEndPage.Home; // an online screen this front end does not have
            BindButtons();
            Render();
        }

        public override void Unbind()
        {
            foreach (Button button in buttons.Values)
            {
                if (button != null) button.onClick.RemoveAllListeners();
            }

            for (int i = 0; i < gameEntries.Count; i++) gameEntries[i].button.onClick.RemoveAllListeners();
            for (int i = 0; i < boardTiles.Count; i++) boardTiles[i].button.onClick.RemoveAllListeners();
            for (int i = 0; i < profileRows.Length; i++)
            {
                if (profileRows[i] != null && profileRows[i].button != null) profileRows[i].button.onClick.RemoveAllListeners();
            }

            model = null;
            actions = null;
            openGuide = null;
        }

        /// <summary>Goes to a screen (BACK, CONTINUE, the tabs).</summary>
        public void Go(FrontEndPage target)
        {
            if (model == null) return;
            if (pages[(int)target] == null)
            {
                SetText("Status", "The online screens are not in the front end yet: run Console Cards > UI > Apply Kit Look (UI-1).");
                return;
            }

            page = target;
            Render();
        }

        /// <summary>
        /// Owner, 2026-10-26: CREATE's editors open over the front end's background; while they are open the top bar,
        /// the screens and the status line are hidden and Esc is left to the editor.
        /// </summary>
        public void ShowBackdropOnly(bool backdrop)
        {
            backdropOnly = backdrop;
            SetActive("TopBar", !backdrop);
            SetActive("Pages", !backdrop);
            SetActive("Status", !backdrop);
            SetActive("Background/Picture", !backdrop && page == FrontEndPage.Home); // BRAND-1
        }

        private bool backdropOnly;

        // Owner, 2026-10-26: leaving the game's window (Esc to free the mouse, clicking another window) must never
        // take the player to the table. Esc counts only while the game has focus, and not just after it comes back.
        private int ignoreEscapeUntilFrame;

        private void OnApplicationFocus(bool hasFocus)
        {
            ignoreEscapeUntilFrame = Time.frameCount + 2;
        }

        private void Update()
        {
            if (model == null || backdropOnly) return;
            if (highlightTab != null) PlaceHighlight();
            UpdateBoard(); // boards 1–3: a click on a Library tile opens its sheet a moment later
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame
                && UnityEngine.Application.isFocused && Time.frameCount > ignoreEscapeUntilFrame)
            {
                Back();
            }
        }

        private void Back()
        {
            switch (page)
            {
                case FrontEndPage.Home:
                    break; // owner, 2026-10-26: the main menu is the first screen; the table is reached with TABLE
                case FrontEndPage.Create:
                case FrontEndPage.Profile:
                case FrontEndPage.Settings:
                    Go(FrontEndPage.Home);
                    break;
                case FrontEndPage.Lobby:
                    Go(HasBoardOnline ? FrontEndPage.Library : FrontEndPage.Profile);
                    break;
                case FrontEndPage.Room:
                    break; // LEAVE leaves the room; Esc does not
                case FrontEndPage.Library:
                    Go(HasBoardLibrary ? FrontEndPage.Home : PlayPage);
                    break;
                default:
                    Go(page - 1);
                    break;
            }
        }

        // ---------- Wiring ----------

        private void BindButtons()
        {
            foreach (Button button in buttons.Values)
            {
                if (button != null) button.onClick.RemoveAllListeners();
            }

            On("TopBar/Tabs/Play", () => Go(PlayPage));
            On("TopBar/Tabs/Create", () => Go(FrontEndPage.Create));
            On("TopBar/Tabs/Sandbox", () => actions?.Sandbox?.Invoke());
            On("TopBar/Right/Guide", () => openGuide?.Invoke());
            On("TopBar/Right/Settings", OpenSettings);
            On("TopBar/Right/Table", () => actions?.Close?.Invoke());
            On("TopBar/Right/Exit", () => actions?.Exit?.Invoke());

            On("Pages/Home/Menu/Play", () => Go(PlayPage));
            On("Pages/Home/Menu/Create", () => Go(FrontEndPage.Create));
            On("Pages/Home/Menu/Sandbox", () => actions?.Sandbox?.Invoke());

            On("Pages/Create/Menu/CardArt", () => actions?.OpenCardArt?.Invoke());
            On("Pages/Create/Menu/CardEditor", () => actions?.OpenCardEditor?.Invoke());
            On("Pages/Create/Menu/DeckEditor", () => actions?.OpenDeckEditor?.Invoke());
            On("Pages/Create/Menu/Table", () => actions?.OpenTableTools?.Invoke());
            On("Pages/Create/Back", () => Go(FrontEndPage.Home));

            On("Pages/Profile/Panel/Active", () => actions?.OpenProfile?.Invoke());
            On("Pages/Profile/Panel/Active/NameBox", () => actions?.OpenProfile?.Invoke());
            On("Pages/Profile/Panel/Active/Customize", Customize);
            On("Pages/Profile/Panel/Add", () => actions?.OpenProfile?.Invoke());
            On("Pages/Profile/Avatar/Previous", () => actions?.CycleAvatar?.Invoke(-1));
            On("Pages/Profile/Avatar/Next", () => actions?.CycleAvatar?.Invoke(1));
            On("Pages/Profile/Back", () => Go(FrontEndPage.Home));
            On("Pages/Profile/Continue", () => Go(FrontEndPage.Library));

            On("Pages/Library/Back", () => Go(HasBoardLibrary ? FrontEndPage.Home : PlayPage));
            On("Pages/Library/Select", SelectFromLibrary);

            On("Pages/Details/Back", () => Go(FrontEndPage.Library));
            On("Pages/Details/Start", () =>
            {
                if (HasBoardSheet) actions?.Start?.Invoke(); // board 3: the sheet has the whole setup
                else Go(FrontEndPage.Setup);
            });
            for (int i = 0; i < PlayerRowCount; i++)
            {
                int seat = i;
                On(playerRowsPath + "/Row" + i + "/PawnBox/Previous", () => actions?.CycleSeatColour?.Invoke(seat, -1));
                On(playerRowsPath + "/Row" + i + "/PawnBox/Next", () => actions?.CycleSeatColour?.Invoke(seat, 1));
            }

            On("Pages/Details/Media/Previous", () => StepGallery(-1));
            On("Pages/Details/Media/Next", () => StepGallery(1));
            for (int i = 0; i < GalleryThumbCount; i++)
            {
                int thumb = i;
                On("Pages/Details/Media/Thumb" + i, () => ChooseThumb(thumb));
            }

            On("Pages/Setup/Back", () => Go(FrontEndPage.Details));
            Picker("Pages/Setup/Options/Column/Mode", step => actions?.CycleMode?.Invoke(step));
            Picker("Pages/Setup/Options/Column/Players", step => actions?.CyclePlayers?.Invoke(step));
            Picker("Pages/Setup/Options/Column/WinMode", step => actions?.CycleWinMode?.Invoke(step));
            Picker("Pages/Setup/Options/Column/Turns", step => actions?.CycleTurns?.Invoke(step));
            Picker("Pages/Setup/Options/Column/Deck", step => actions?.CycleDeck?.Invoke(step));
            On("Pages/Setup/Continue", () => Go(FrontEndPage.Players));

            On("Pages/Players/Back", () => Go(FrontEndPage.Setup));
            On(playerRowsPath + "/AddRow/Add", () => actions?.CyclePlayers?.Invoke(1));
            On(playerRowsPath + "/AddRow/Remove", () => actions?.CyclePlayers?.Invoke(-1));
            On("Pages/Players/Continue", () => Go(FrontEndPage.Review));

            On("Pages/Review/Back", () => Go(FrontEndPage.Players));
            On("Pages/Review/Settings/Edit", () => Go(FrontEndPage.Setup));
            On("Pages/Review/Players/Edit", () => Go(FrontEndPage.Players));
            On("Pages/Review/Start", () => actions?.Start?.Invoke());
            BindOnlineButtons(); // MP-2
            BindSettingsButtons();
            BindCreateButtons(); // CREATE (board 4)
            BindBoardButtons(); // boards 1–3
            BindOnlineBoard(); // board 2b
        }

        private void On(string path, Action click)
        {
            if (buttons.TryGetValue(path, out Button button) && button != null) button.onClick.AddListener(() => click());
        }

        private void Picker(string row, Action<int> cycle)
        {
            On(row + "/Picker/Previous", () => cycle(-1));
            On(row + "/Picker/Next", () => cycle(1));
        }

        // CUSTOMIZE AVATAR: the character customizer when the game has one, else the next avatar picture.
        private void Customize()
        {
            if (actions?.Customize != null) actions.Customize();
            else actions?.CycleAvatar?.Invoke(1);
        }

        private void SelectFromLibrary()
        {
            if (model == null) return;
            if (model.IsEmptyTable) actions?.Start?.Invoke(); // the Empty Table needs no setup
            else Go(FrontEndPage.Details);
        }

        // ---------- Drawing ----------

        private void Render()
        {
            if (model == null) return;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null) pages[i].SetActive(i == (int)page);
            }

            // BRAND-1 (owner, 2026-10-30): the main menu's picture behind Home only (the other screens keep the plain page).
            SetActive("Background/Picture", page == FrontEndPage.Home && !backdropOnly);
            bool stepped = page >= FrontEndPage.Setup && page <= FrontEndPage.Review;
            SetActive("Pages/Stepper", stepped);
            if (stepped) RenderStepper();
            RenderTabs();
            SetText("Status", model.Status);
            switch (page)
            {
                case FrontEndPage.Home:
                    RenderBoardHome();
                    break;
                case FrontEndPage.Profile:
                    RenderProfile();
                    break;
                case FrontEndPage.Library:
                    if (HasBoardLibrary) RenderBoardLibrary();
                    else RenderLibrary();
                    break;
                case FrontEndPage.Details:
                    if (HasBoardSheet) RenderBoardSheet();
                    else RenderDetails();
                    break;
                case FrontEndPage.Setup:
                    RenderSetup();
                    break;
                case FrontEndPage.Players:
                    RenderStepTitle("Players");
                    RenderPlayersIntro();
                    RenderPlayers(playerRows);
                    SetActive(playerRowsPath + "/AddRow", model.CanChangePlayers);
                    break;
                case FrontEndPage.Review:
                    RenderReview();
                    break;
                case FrontEndPage.Lobby:
                    if (HasBoardOnline) RenderOnlineBoard();
                    else RenderLobby();
                    break;
                case FrontEndPage.Room:
                    if (HasBoardOnline) RenderOnlineBoard();
                    else RenderRoom();
                    break;
                case FrontEndPage.Settings:
                    RenderSettings();
                    break;
                case FrontEndPage.Create:
                    RenderCreate();
                    break;
            }
        }

        private void RenderStepper()
        {
            int current = (int)page - (int)FrontEndPage.Setup;
            for (int step = 0; step < 3; step++)
            {
                bool now = step == current;
                string at = "Pages/Stepper/Step" + step;
                Transform circle = transform.Find(at + "/Circle");
                Image image = circle != null ? circle.GetComponent<Image>() : null;
                if (image != null)
                {
                    image.sprite = now && discSprite != null ? discSprite : ringSprite != null ? ringSprite : image.sprite;
                    image.color = now ? activeTab : idleTab;
                }

                Tint(at + "/Circle/Number", now ? new Color(0.086f, 0.098f, 0.102f, 1f) : idleTab);
                Tint(at + "/Label", now ? activeTab : idleTab);
            }
        }

        private void RenderTabs()
        {
            string active = page == FrontEndPage.Create ? "Create" : "Play";
            foreach (string tab in new[] { "Play", "Create", "Sandbox" })
            {
                Tint("TopBar/Tabs/" + tab + "/Label", tab == active ? activeTab : idleTab);
            }

            // Placed on the next frame the view is shown: while it is being bound it is still hidden, and hidden tabs
            // are not laid out (the underline sat left of PLAY on the first show).
            highlightTab = "TopBar/Tabs/" + active;
            if (isActiveAndEnabled) PlaceHighlight();
            RenderBoardTabs();
        }

        private void PlaceHighlight()
        {
            if (tabHighlight == null || !buttons.TryGetValue(highlightTab, out Button tabButton) || tabButton == null) return;
            RectTransform tabs = (RectTransform)tabHighlight.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(tabs);
            RectTransform tabRect = (RectTransform)tabButton.transform;
            tabHighlight.anchoredPosition = new Vector2(tabRect.localPosition.x, tabHighlight.anchoredPosition.y);
            // Board top bar (no layout): the underline is as wide as its tab.
            if (tabHighlight.GetComponent<LayoutElement>() == null)
            {
                tabHighlight.sizeDelta = new Vector2(tabRect.rect.width, tabHighlight.sizeDelta.y);
            }

            highlightTab = null;
        }

        private void RenderProfile()
        {
            IReadOnlyList<ProfileRowModel> profiles = model.Profiles;
            // The profile playing now (first in the list) fills the cyan row.
            ProfileRowParts active = profileRows[0];
            if (profiles.Count > 0)
            {
                active.small.text = "PLAYER 1  ·  PLAYING NOW";
                active.name.text = profiles[0].Name;
            }

            ShowAvatar(active.avatar, model.AvatarPicture, activeTab);
            SetText("Pages/Profile/Panel/Active/Customize/Label",
                string.IsNullOrEmpty(model.AvatarName) ? "CUSTOMIZE AVATAR" : "CUSTOMIZE AVATAR  ·  " + Upper(model.AvatarName));

            int others = 0;
            for (int i = 1; i < profileRows.Length; i++)
            {
                ProfileRowParts row = profileRows[i];
                bool shown = i < profiles.Count;
                row.root.SetActive(shown);
                row.button.onClick.RemoveAllListeners();
                if (!shown) continue;
                others++;
                ProfileRowModel profile = profiles[i];
                row.small.text = $"PLAYER {i + 1}";
                row.name.text = Upper(profile.Name);
                ShowAvatar(row.avatar, null, idleTab);
                string id = profile.Id;
                row.button.onClick.AddListener(() => actions?.PlayAs?.Invoke(id));
            }

            if (addProfileRow != null)
            {
                bool room = others < ProfileRowCount - 1;
                addProfileRow.gameObject.SetActive(room);
                if (room) addProfileRow.anchoredPosition = new Vector2(addProfileRow.anchoredPosition.x, -(ProfileRowTop + others * ProfileRowPitch));
            }

            SetText("Pages/Profile/Avatar/Name", Upper(model.ProfileName));
            ShowAvatar(Picture("Pages/Profile/Avatar/Picture"), model.AvatarPicture != null ? model.AvatarPicture : fallbackAvatar, idleTab);
            SetText("Pages/Profile/Avatar/AvatarName", model.AvatarName);
            ShowAvatarDots();
            SetActive("Pages/Profile/Avatar/Previous", model.CanChangeAvatar);
            SetActive("Pages/Profile/Avatar/Next", model.CanChangeAvatar);
        }

        // The page dots under the avatar: one per avatar (copies of the prefab's first dot are added when there are more
        // avatars than dots, up to MaxAvatarDots), the current one cyan; each arrow click moves it one dot.
        private const int MaxAvatarDots = 12;

        private void ShowAvatarDots()
        {
            if (avatarDots.Count == 0) return;
            int count = Mathf.Clamp(model.AvatarCount, 1, MaxAvatarDots);
            while (avatarDots.Count < count)
            {
                Image copy = Instantiate(avatarDots[0], avatarDots[0].transform.parent, false);
                copy.name = "Dot" + avatarDots.Count;
                avatarDots.Add(copy);
            }

            int current = model.AvatarCount <= MaxAvatarDots
                ? model.AvatarIndex
                : model.AvatarIndex * MaxAvatarDots / model.AvatarCount;
            for (int i = 0; i < avatarDots.Count; i++)
            {
                Image dot = avatarDots[i];
                bool visible = i < count;
                if (dot.gameObject.activeSelf != visible) dot.gameObject.SetActive(visible);
                dot.color = i == current ? activeTab : new Color(idleTab.r, idleTab.g, idleTab.b, 0.35f);
            }
        }

        private void RenderLibrary()
        {
            IReadOnlyList<CardEditorRowModel> games = model.Games;
            while (gameEntries.Count < games.Count) gameEntries.Add(NewGameEntry());
            for (int i = 0; i < gameEntries.Count; i++)
            {
                GameEntryParts entry = gameEntries[i];
                bool shown = i < games.Count;
                entry.root.SetActive(shown);
                entry.button.onClick.RemoveAllListeners();
                if (!shown) continue;
                CardEditorRowModel game = games[i];
                entry.name.text = Upper(game.Name);
                SplitDetail(game.Detail, out string players, out string rest);
                entry.detail.text = rest;
                entry.players.text = players;
                entry.picture.texture = game.Thumbnail;
                entry.picture.color = game.Thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.06f);
                entry.plate.sprite = game.IsSelected ? selectedGameEntry : gameEntry;
                string id = game.Id;
                entry.button.onClick.AddListener(() => actions?.SelectGame?.Invoke(id));
            }

            SetText("Pages/Library/Detail/Name", Upper(model.GameName));
            SetText("Pages/Library/Detail/Text", model.Rules);
            ShowPicture(Picture("Pages/Library/Detail/Picture"), model.Picture);
            SplitDetail(model.Detail, out string count, out string modes);
            Stat("Pages/Library/Detail/Stats/Cell0", count.Replace(" players", string.Empty), "PLAYERS");
            Stat("Pages/Library/Detail/Stats/Cell1", model.ShowMode ? model.ModeName : "Free", model.ShowMode ? "MODE" : "PLAY");
            Stat("Pages/Library/Detail/Stats/Cell2", "CONSOLE", "CONTROLLER CARDS");
            SetText("Pages/Library/Select/Label", model.IsEmptyTable ? "START EMPTY TABLE" : "SELECT " + Upper(model.GameName));
            SetInteractable("Pages/Library/Select", model.CanStart || !model.IsEmptyTable);
        }

        private void RenderDetails()
        {
            SetText("Pages/Details/Title", Upper(model.GameName));
            SplitDetail(model.Detail, out string count, out string modes);
            SetText("Pages/Details/Tagline", Spaced(Upper(modes.Length > 0 ? modes : count)));
            SetText("Pages/Details/Text", model.Rules);
            SetText("Pages/Details/Stats/Cell0/Label", model.PlayersText.Length > 0 ? model.PlayersText : count);
            SetText("Pages/Details/Stats/Cell1/Label", model.ShowMode ? model.ModeName : "Free play");
            SetText("Pages/Details/Stats/Cell2/Label", model.ShowWinMode ? model.WinModeText : "Tabletop");
            RenderBox();
            RenderGallery();
            RenderFeatures();
            SetText("Pages/Details/Start/Line", $"Configure your {model.GameName} session.");
            SetInteractable("Pages/Details/Start", model.CanStart);
        }

        private void RenderSetup()
        {
            RenderStepTitle("Setup");
            Row("Pages/Setup/Options/Column/Mode", model.ShowMode, model.ModeName, model.CanChangeMode);
            Row("Pages/Setup/Options/Column/Players", model.ShowPlayers, model.PlayersText, model.CanChangePlayers);
            Row("Pages/Setup/Options/Column/WinMode", model.ShowWinMode, model.WinModeText, model.ShowWinMode);
            Row("Pages/Setup/Options/Column/Turns", model.ShowTurns, model.TurnsText, model.ShowTurns);
            Row("Pages/Setup/Options/Column/Deck", true, DeckShort(), model.CanChangeDeck);
            SetText("Pages/Setup/Options/Column/Deck/Hint", model.DeckDetail.Length > 0 ? model.DeckDetail : "Choose the card set to use.");
            Summary("Pages/Setup/Summary/Rows/Row");
            SetInteractable("Pages/Setup/Continue", model.CanStart);
        }

        private void RenderReview()
        {
            RenderStepTitle("Review");
            Summary("Pages/Review/Settings/Grid/Cell", true);
            RenderPlayers(reviewPlayers);
            RenderComponents();
            SetText("Pages/Review/Start/Line", $"Load {model.GameName} and begin playing.");
            SetInteractable("Pages/Review/Start", model.CanStart);
        }

        private void RenderStepTitle(string name)
        {
            SetText("Pages/" + name + "/Title", Upper(model.GameName));
            ShowPicture(Picture("Pages/" + name + "/Thumb"), model.Picture);
        }

        // Mode, Players, Win mode, Turns, Deck (setup order) or Mode, Players, Win mode, Deck, Turns (review grid).
        private void Summary(string prefix, bool reviewOrder = false)
        {
            string[] values = reviewOrder
                ? new[] { model.ModeName, model.PlayersText, model.WinModeText, DeckShort(), model.TurnsText }
                : new[] { model.ModeName, model.PlayersText, model.WinModeText, model.TurnsText, DeckShort() };
            bool[] shown = reviewOrder
                ? new[] { model.ShowMode, model.ShowPlayers, model.ShowWinMode, true, model.ShowTurns }
                : new[] { model.ShowMode, model.ShowPlayers, model.ShowWinMode, model.ShowTurns, true };
            for (int i = 0; i < values.Length; i++)
            {
                SetActive(prefix + i, shown[i]);
                SetText(prefix + i + "/Value", values[i]);
            }
        }

        private string DeckShort() => model.DeckName.Length > 0 ? model.DeckName : model.Deck;

        // COMPONENTS IN TABLE: the same items as IN THE BOX, side by side; the deck line when there are none.
        private void RenderComponents()
        {
            IReadOnlyList<FrontEndBoxItem> items = model.BoxItems;
            bool listed = transform.Find("Pages/Review/Components/Items") != null;
            for (int i = 0; i < BoxRowCount; i++)
            {
                string item = "Pages/Review/Components/Items/Item" + i;
                bool shown = i < items.Count;
                SetActive(item, shown);
                if (!shown) continue;
                SetText(item + "/Label", items[i].Label);
                SetText(item + "/Count", "x " + items[i].Count);
                ShowIcon(item + "/Icon", items[i].Icon);
            }

            SetActive("Pages/Review/Components/Text", items.Count == 0 || !listed);
            SetText("Pages/Review/Components/Text", model.Deck);
        }

        private void RenderPlayers(PlayerRowParts[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                PlayerRowParts row = rows[i];
                if (row == null) continue;
                bool shown = i < model.PlayerCount;
                row.root.SetActive(shown);
                if (!shown) continue;
                Color colour = i < model.PlayerColors.Count ? model.PlayerColors[i] : Color.grey;
                string colourName = i < model.PlayerColorNames.Count ? model.PlayerColorNames[i] : string.Empty;
                if (row.badge != null) row.badge.text = "P" + (i + 1);
                if (row.badgePlate != null) row.badgePlate.color = colour;
                if (row.name != null) row.name.text = i < model.PlayerNames.Count ? model.PlayerNames[i] : $"Player {i + 1}";
                if (row.controller != null) row.controller.text = row.pawn != null ? "Controller: " + colourName : colourName;
                if (row.dot != null) row.dot.color = colour;
                if (row.pawn != null) row.pawn.color = colour;
                if (rows == playerRows)
                {
                    SetInteractable(playerRowsPath + "/Row" + i + "/PawnBox/Previous", model.CanChangeColours);
                    SetInteractable(playerRowsPath + "/Row" + i + "/PawnBox/Next", model.CanChangeColours);
                }
                if (row.avatar != null) ShowAvatar(row.avatar, i == 0 ? model.AvatarPicture : null, colour);
            }
        }

        private void Row(string path, bool shown, string value, bool canChange)
        {
            SetActive(path, shown);
            if (!shown) return;
            SetText(path + "/Picker/Value", value);
            SetInteractable(path + "/Picker/Previous", canChange);
            SetInteractable(path + "/Picker/Next", canChange);
        }

        private void Stat(string cell, string value, string label)
        {
            SetText(cell + "/Words/Value", value);
            SetText(cell + "/Words/Label", label);
        }

        // "2–4 players · 3 modes" → "2–4 players" and "3 modes".
        private static void SplitDetail(string detail, out string first, out string rest)
        {
            detail = detail ?? string.Empty;
            int dot = detail.IndexOf(" · ", StringComparison.Ordinal);
            first = dot < 0 ? detail : detail.Substring(0, dot);
            rest = dot < 0 ? string.Empty : detail.Substring(dot + 3);
        }

        private static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            char[] letters = new char[text.Length * 2 - 1];
            for (int i = 0; i < text.Length; i++)
            {
                letters[i * 2] = text[i];
                if (i < text.Length - 1) letters[i * 2 + 1] = ' ';
            }

            return new string(letters);
        }

        // ---------- Parts ----------

        private void Resolve()
        {
            if (resolved) return;
            string[] names = Enum.GetNames(typeof(FrontEndPage));
            for (int i = 0; i < names.Length; i++)
            {
                // MP-2: the online screens are optional (a front end made before them works without them).
                bool isOptional = IsOnlinePage((FrontEndPage)i) || (FrontEndPage)i == FrontEndPage.Settings;
                Transform optional = isOptional ? transform.Find("Pages/" + names[i]) : null;
                pages[i] = isOptional ? (optional != null ? optional.gameObject : null) : Require("Pages/" + names[i]).gameObject;
            }

            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                string path = PathOf(button.transform);
                if (!buttons.ContainsKey(path)) buttons.Add(path, button);
            }

            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                string path = PathOf(text.transform);
                if (!texts.ContainsKey(path)) texts.Add(path, text);
            }

            foreach (RawImage picture in GetComponentsInChildren<RawImage>(true))
            {
                string path = PathOf(picture.transform);
                if (!pictures.ContainsKey(path)) pictures.Add(path, picture);
            }

            libraryContent = (RectTransform)Require("Pages/Library/List/Viewport/Content");
            gameEntryTemplate = Require("Pages/Library/List/Viewport/Content/Template").gameObject;
            gameEntryTemplate.SetActive(false);
            tabHighlight = Require("TopBar/Tabs/Highlight") as RectTransform;
            addProfileRow = Require("Pages/Profile/Panel/Add") as RectTransform;
            Transform dotsRow = transform.Find("Pages/Profile/Avatar/Dots");
            if (dotsRow != null)
            {
                foreach (Transform dot in dotsRow)
                {
                    Image image = dot.GetComponent<Image>();
                    if (image == null) continue;
                    // One white dot for all (the exported dots have their colour baked in, so a tint could not
                    // switch them); the colour says which one is current.
                    if (discSprite != null) image.sprite = discSprite;
                    avatarDots.Add(image);
                }
            }
            Transform active = Require("Pages/Profile/Panel/Active");
            profileRows[0] = new ProfileRowParts
            {
                root = active.gameObject,
                button = active.GetComponent<Button>(),
                avatar = Require(active, "Avatar").GetComponent<RawImage>(),
                small = Require(active, "Small").GetComponent<Text>(),
                name = Require(active, "NameBox/Name").GetComponent<Text>(),
            };
            for (int i = 1; i < ProfileRowCount; i++)
            {
                Transform row = Require("Pages/Profile/Panel/Row" + i);
                profileRows[i] = new ProfileRowParts
                {
                    root = row.gameObject,
                    button = row.GetComponent<Button>(),
                    avatar = Require(row, "Avatar").GetComponent<RawImage>(),
                    small = Require(row, "Small").GetComponent<Text>(),
                    name = Require(row, "NameBox/Name").GetComponent<Text>(),
                };
            }

            if (transform.Find("Pages/Players/Viewport/Rows") != null) playerRowsPath = "Pages/Players/Viewport/Rows";
            for (int i = 0; i < PlayerRowCount; i++)
            {
                // Seats 7 and 8 exist once Apply Kit Look has added them; until then the screen shows six.
                playerRows[i] = PlayerRow(transform.Find(playerRowsPath + "/Row" + i));
                reviewPlayers[i] = PlayerRow(transform.Find("Pages/Review/Players/Tiles/Tile" + i));
            }

            resolved = true;
        }

        private static PlayerRowParts PlayerRow(Transform row)
        {
            if (row == null) return null;
            Transform badge = row.Find("Badge");
            Transform avatar = row.Find("Avatar");
            Transform name = row.Find("NameBox/Name") ?? row.Find("Name");
            Transform controller = row.Find("Controller");
            Transform dot = row.Find("Dot");
            Transform pawn = row.Find("PawnBox/Pawn");
            return new PlayerRowParts
            {
                root = row.gameObject,
                badgePlate = badge != null ? badge.GetComponent<Image>() : null,
                badge = badge != null ? badge.GetComponentInChildren<Text>(true) : null,
                avatar = avatar != null ? avatar.GetComponent<RawImage>() : null,
                name = name != null ? name.GetComponent<Text>() : null,
                controller = controller != null ? controller.GetComponent<Text>() : null,
                dot = dot != null ? dot.GetComponent<Image>() : null,
                pawn = pawn != null ? pawn.GetComponent<Image>() : null,
            };
        }

        private GameEntryParts NewGameEntry()
        {
            GameObject copy = Instantiate(gameEntryTemplate, libraryContent, false);
            copy.name = "Game" + gameEntries.Count;
            copy.SetActive(true);
            Transform t = copy.transform;
            return new GameEntryParts
            {
                root = copy,
                button = copy.GetComponent<Button>(),
                plate = copy.GetComponent<Image>(),
                picture = Require(t, "Picture").GetComponent<RawImage>(),
                name = Require(t, "Name").GetComponent<Text>(),
                detail = Require(t, "Detail").GetComponent<Text>(),
                players = Require(t, "PlayersRow/Players").GetComponent<Text>(),
            };
        }

        private Transform Require(string path) => Require(transform, path);

        private static Transform Require(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found == null)
            {
                throw new InvalidOperationException(
                    $"The front end has no '{path}': run Console Cards > UI > Apply Kit Look (UI-1) to build it again.");
            }

            return found;
        }

        private string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform at = t.parent; at != null && at != transform; at = at.parent) path = at.name + "/" + path;
            return path;
        }

        // IN THE BOX: a row per kind of component (icon from the screen's IconSet); a game the player made shows its deck line.
        private void RenderBox()
        {
            IReadOnlyList<FrontEndBoxItem> items = model.BoxItems;
            for (int i = 0; i < BoxRowCount; i++)
            {
                string row = "Pages/Details/Box/Row" + i;
                bool shown = i < items.Count;
                SetActive(row, shown);
                if (!shown) continue;
                SetText(row + "/Label", items[i].Label);
                SetText(row + "/Count", "x " + items[i].Count);
                ShowIcon(row + "/Icon", items[i].Icon);
            }

            // A Details screen built before the list (FE_Details older than design 2) has only the text and one picture.
            bool listed = transform.Find("Pages/Details/Box/Row0") != null;
            SetActive("Pages/Details/Box/Text", items.Count == 0 || !listed);
            SetText("Pages/Details/Box/Text", model.Deck);
            ShowPicture(Picture("Pages/Details/Media/Picture"), model.Picture);
        }

        // Three pictures at a time; the arrows move the chosen one (cyan outline) and scroll when it leaves the strip.
        private void RenderGallery()
        {
            IReadOnlyList<Texture> gallery = model.Gallery;
            if (!string.Equals(galleryGame, model.GameName, StringComparison.Ordinal))
            {
                galleryGame = model.GameName;
                galleryFirst = 0;
                galleryChosen = 0;
            }

            if (gallery.Count == 0 && model.Picture != null) gallery = new[] { model.Picture };
            galleryChosen = Mathf.Clamp(galleryChosen, 0, Mathf.Max(0, gallery.Count - 1));
            galleryFirst = Mathf.Clamp(galleryFirst, 0, Mathf.Max(0, gallery.Count - GalleryThumbCount));
            for (int i = 0; i < GalleryThumbCount; i++)
            {
                string thumb = "Pages/Details/Media/Thumb" + i;
                int index = galleryFirst + i;
                bool shown = index < gallery.Count || (i == 0 && gallery.Count == 0);
                SetActive(thumb, shown);
                if (!shown) continue;
                ShowPicture(Picture(thumb + "/Mask/Picture"), index < gallery.Count ? gallery[index] : null);
                SetActive(thumb + "/Selected", index == galleryChosen && gallery.Count > 0);
            }

            SetInteractable("Pages/Details/Media/Previous", galleryChosen > 0);
            SetInteractable("Pages/Details/Media/Next", galleryChosen < gallery.Count - 1);
        }

        private void StepGallery(int step)
        {
            int count = model.Gallery.Count;
            if (count == 0) return;
            galleryChosen = Mathf.Clamp(galleryChosen + step, 0, count - 1);
            if (galleryChosen < galleryFirst) galleryFirst = galleryChosen;
            if (galleryChosen >= galleryFirst + GalleryThumbCount) galleryFirst = galleryChosen - GalleryThumbCount + 1;
            RenderGallery();
        }

        private void ChooseThumb(int thumb)
        {
            if (galleryFirst + thumb >= model.Gallery.Count) return;
            galleryChosen = galleryFirst + thumb;
            RenderGallery();
        }

        private void RenderFeatures()
        {
            IReadOnlyList<FrontEndFeature> features = model.Features;
            for (int i = 0; i < FeatureTileCount; i++)
            {
                string tile = "Pages/Details/Media/Feature" + i;
                bool shown = i < features.Count;
                SetActive(tile, shown);
                if (!shown) continue;
                SetText(tile + "/Title", features[i].Title);
                SetText(tile + "/Line", features[i].Line);
                ShowIcon(tile + "/Icon", features[i].Icon);
            }
        }

        // Copies a picture (sprite and colour) from Pages/Details/Box/IconSet, so the icons are edited in the prefab.
        private void ShowIcon(string path, string icon)
        {
            Transform target = transform.Find(path);
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image == null) return;
            if (!detailIcons.TryGetValue(icon, out Image source))
            {
                Transform found = transform.Find("Pages/Details/Box/IconSet/" + icon);
                source = found != null ? found.GetComponent<Image>() : null;
                detailIcons[icon] = source;
            }

            if (source == null) return;
            image.sprite = source.sprite;
            image.color = source.color;
        }

        private RawImage Picture(string path) => pictures.TryGetValue(path, out RawImage image) ? image : null;

        private void SetText(string path, string value)
        {
            if (texts.TryGetValue(path, out Text text) && text != null) text.text = value ?? string.Empty;
        }

        private void Tint(string path, Color color)
        {
            if (texts.TryGetValue(path, out Text text) && text != null) text.color = color;
        }

        private void SetActive(string path, bool active)
        {
            if (!objects.TryGetValue(path, out GameObject go))
            {
                Transform found = transform.Find(path);
                go = found != null ? found.gameObject : null;
                objects[path] = go;
            }

            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private void SetInteractable(string path, bool interactable)
        {
            if (buttons.TryGetValue(path, out Button button) && button != null) button.interactable = interactable;
        }

        private static void ShowPicture(RawImage image, Texture picture)
        {
            if (image == null) return;
            image.texture = picture;
            image.color = picture != null ? Color.white : new Color(1f, 1f, 1f, 0.06f);
        }

        // A face picture, or a soft tile in the given colour when there is none.
        private static void ShowAvatar(RawImage image, Texture picture, Color tint)
        {
            if (image == null) return;
            image.texture = picture;
            image.color = picture != null ? Color.white : new Color(tint.r, tint.g, tint.b, 0.35f);
        }

        private static string Upper(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.ToUpperInvariant();
    }
}
