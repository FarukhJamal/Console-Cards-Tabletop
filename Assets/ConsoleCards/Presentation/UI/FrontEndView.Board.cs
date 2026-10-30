using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Boards 1–3 of the menu flow (owner, 2026-10-30: "everything the same as the reference"), when the screens were
    /// built from them (Console Cards › UI › Build Board Screens):
    /// <list type="bullet">
    /// <item>The top bar: PLAY, CREATE, SANDBOX; "Playing as" (the player profile), GUIDE, SETTINGS, EXIT; on the Game
    /// sheet the way back ("PLAY › TRAP FLOOR").</item>
    /// <item>Home: PLAY, CREATE, SANDBOX and the CONTINUE card (the table behind the front end, or the game played last
    /// with PLAY AGAIN).</item>
    /// <item>The Library: ALL, OUR GAMES, YOUR GAMES; ON THIS COMPUTER or ONLINE; a tile per game. One click opens its
    /// sheet, a double click starts it with the last settings.</item>
    /// <item>The Game sheet: the game and HOW TO PLAY on the left; SETUP on the right (players, the seats, difficulty,
    /// win mode, more options) and START.</item>
    /// </list>
    /// A front end without these screens works as before.
    /// </summary>
    public sealed partial class FrontEndView
    {
        public const int BoardSeatCount = 8;
        public const int HowToPlayRows = 4;
        private const float TileWidth = 402f;
        private const float TileHeight = 492f;
        private const float TileGap = 31.5f;
        private const float DoubleClickSeconds = 0.35f;

        private static readonly Color ChipOn = new Color(0.910f, 0.925f, 0.929f, 1f);
        private static readonly Color ChipOnWords = new Color(0.086f, 0.098f, 0.102f, 1f);
        private static readonly Color ChipOffWords = new Color(0.718f, 0.753f, 0.765f, 1f);
        private static readonly Color TileEdgeColour = new Color(0.235f, 0.255f, 0.263f, 1f);
        private static readonly Color TileChosenEdge = new Color(0.024f, 0.937f, 0.941f, 1f);
        private static readonly Color CoverGame = new Color(0.024f, 0.937f, 0.941f, 1f);
        private static readonly Color CoverMine = new Color(0.110f, 0.235f, 0.239f, 1f);
        private static readonly Color SoonTag = new Color(0.949f, 0.463f, 0.180f, 1f); // #F2762E
        private static readonly Color SoonWords = new Color(0.102f, 0.055f, 0.024f, 1f); // #1A0E06
        private static readonly Color SoonCover = new Color(0.169f, 0.188f, 0.200f, 1f); // #2B3033

        private sealed class BoardTileParts
        {
            public GameObject root;
            public Button button;
            public Outline[] edges;
            public RawImage picture;
            public Image cover;
            public GameObject mark;
            public Text tag;
            public Image tagFill;
            public Text name;
            public Text line;
            public GameObject footer;
            public CanvasGroup dim;
        }

        private readonly List<BoardTileParts> boardTiles = new List<BoardTileParts>();
        private readonly List<FrontEndLibraryTile> shownTiles = new List<FrontEndLibraryTile>();
        private int libraryFilter;
        private bool moreOptionsOpen;
        private string clickedTile;
        private float clickedAt = -10f;
        private bool sheetPending;

        private bool HasBoardLibrary => Find("Pages/Library/Filters") != null;
        private bool HasBoardSheet => Find("Pages/Details/Sheet") != null;
        private bool HasBoardHome => Find("Pages/Home/Continue") != null;

        private Transform Find(string path) => transform.Find(path);

        private void BindBoardButtons()
        {
            On("TopBar/Right/Profile", () => Go(FrontEndPage.Profile));
            On("Pages/Home/Continue/Continue", () => actions?.Close?.Invoke());
            On("Pages/Home/Continue/PlayAgain", () => actions?.PlayAgain?.Invoke());
            On("Pages/Library/Filters/All", () => SetLibraryFilter(0));
            On("Pages/Library/Filters/Ours", () => SetLibraryFilter(1));
            On("Pages/Library/Filters/Yours", () => SetLibraryFilter(2));
            On("Pages/Library/Where/Online", OpenOnline);
            On("Pages/Details/Setup/Players/Minus", () => actions?.CyclePlayers?.Invoke(-1));
            On("Pages/Details/Setup/Players/Plus", () => actions?.CyclePlayers?.Invoke(1));
            On("Pages/Details/Setup/Difficulty/Previous", () => actions?.CycleMode?.Invoke(-1));
            On("Pages/Details/Setup/Difficulty/Next", () => actions?.CycleMode?.Invoke(1));
            On("Pages/Details/Setup/WinMode/Previous", () => actions?.CycleWinMode?.Invoke(-1));
            On("Pages/Details/Setup/WinMode/Next", () => actions?.CycleWinMode?.Invoke(1));
            On("Pages/Details/Setup/Turns/Previous", () => actions?.CycleTurns?.Invoke(-1));
            On("Pages/Details/Setup/Turns/Next", () => actions?.CycleTurns?.Invoke(1));
            On("Pages/Details/Setup/Deck/Previous", () => actions?.CycleDeck?.Invoke(-1));
            On("Pages/Details/Setup/Deck/Next", () => actions?.CycleDeck?.Invoke(1));
            On("Pages/Details/Setup/More", () =>
            {
                moreOptionsOpen = !moreOptionsOpen;
                Render();
            });
            for (int i = 0; i < BoardSeatCount; i++)
            {
                int seat = i;
                On("Pages/Details/Setup/Seats/Seat" + i, () => actions?.CycleSeatColour?.Invoke(seat, 1));
            }
        }

        // ---------- the top bar ----------

        private void RenderBoardTabs()
        {
            if (Find("TopBar/Right/Profile") == null) return;
            SetText("TopBar/Right/Profile/Name", Upper(model.ProfileName));
            ShowPicture(Picture("TopBar/Right/Profile/Avatar"), model.AvatarPicture); // PROFILE-2: the player's face
            bool sheet = page == FrontEndPage.Details && HasBoardSheet;
            SetActive("TopBar/Right", !sheet);
            SetActive("TopBar/Crumb", sheet);
            if (sheet) SetText("TopBar/Crumb", $"PLAY  ›  <b><color=#E8ECED>{Upper(model.GameName)}</color></b>");
            bool underline = page != FrontEndPage.Home && page != FrontEndPage.Profile && page != FrontEndPage.Settings;
            SetActive("TopBar/Tabs/Highlight", underline);
            if (!underline)
            {
                foreach (string tab in new[] { "Play", "Create", "Sandbox" }) Tint("TopBar/Tabs/" + tab + "/Label", idleTab);
            }
        }

        // ---------- Home ----------

        private void RenderBoardHome()
        {
            if (!HasBoardHome) return;
            SetActive("Pages/Home/Continue", model.ContinueShown);
            if (!model.ContinueShown) return;
            SetText("Pages/Home/Continue/Heading", model.ContinueInProgress ? "CONTINUE" : "PLAYED LAST");
            SetText("Pages/Home/Continue/Name", model.ContinueName);
            SetText("Pages/Home/Continue/Line", model.ContinueLine);
            SetText("Pages/Home/Continue/SubLine", model.ContinueSubLine);
            ShowCover(Picture("Pages/Home/Continue/Picture"), Find("Pages/Home/Continue/Cover"), model.ContinuePicture,
                CoverGame);
            SetActive("Pages/Home/Continue/Continue", model.ContinueInProgress);
            // With nothing to continue, PLAY AGAIN takes CONTINUE's place.
            Transform again = Find("Pages/Home/Continue/PlayAgain");
            if (again is RectTransform againRect)
            {
                againRect.anchoredPosition = new Vector2(model.ContinueInProgress ? 243f : 36f, againRect.anchoredPosition.y);
            }
            SetText("Pages/Home/Continue/Note", model.ContinueInProgress
                ? "CONTINUE goes back to the table as you left it. PLAY AGAIN starts this game afresh."
                : "With no table to go back to, this card shows the game played last: PLAY AGAIN starts it with its last settings.");
        }

        // ---------- the Library ----------

        private void SetLibraryFilter(int filter)
        {
            libraryFilter = filter;
            Render();
        }

        private void RenderBoardLibrary()
        {
            string[] filters = { "All", "Ours", "Yours" };
            for (int i = 0; i < filters.Length; i++)
            {
                bool on = i == libraryFilter;
                Transform fill = Find("Pages/Library/Filters/" + filters[i]);
                Image image = fill != null ? fill.GetComponent<Image>() : null;
                if (image != null) image.color = on ? ChipOn : new Color(0f, 0f, 0f, 0f);
                Tint("Pages/Library/Filters/" + filters[i] + "/Label", on ? ChipOnWords : ChipOffWords);
            }

            shownTiles.Clear();
            IReadOnlyList<FrontEndLibraryTile> tiles = model.LibraryTiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (libraryFilter == 1 && tiles[i].Mine) continue;
                if (libraryFilter == 2 && !tiles[i].Mine) continue;
                shownTiles.Add(tiles[i]);
            }

            while (boardTiles.Count < shownTiles.Count) boardTiles.Add(NewBoardTile());
            for (int i = 0; i < boardTiles.Count; i++)
            {
                BoardTileParts parts = boardTiles[i];
                bool shown = i < shownTiles.Count;
                if (parts.root.activeSelf != shown) parts.root.SetActive(shown);
                parts.button.onClick.RemoveAllListeners();
                if (!shown) continue;
                FrontEndLibraryTile tile = shownTiles[i];
                RectTransform rect = (RectTransform)parts.root.transform;
                rect.anchoredPosition = new Vector2((i % 4) * (TileWidth + TileGap), -(i / 4) * (TileHeight + TileGap));
                if (parts.name != null) parts.name.text = Upper(tile.Name);
                if (parts.line != null) parts.line.text = tile.Line;
                if (parts.tag != null) parts.tag.text = tile.Tag;
                if (parts.tagFill != null)
                {
                    parts.tagFill.color = tile.ComingSoon ? SoonTag
                        : tile.Mine ? new Color(0.063f, 0.075f, 0.078f, 1f) : new Color(0.039f, 0.208f, 0.216f, 1f);
                }

                if (parts.tag != null) parts.tag.color = tile.ComingSoon ? SoonWords : tile.Mine ? ChipOn : CoverGame;
                ShowCover(parts.picture, parts.cover != null ? parts.cover.transform : null, tile.Picture,
                    tile.ComingSoon ? SoonCover : tile.Mine ? CoverMine : CoverGame);
                parts.dim.alpha = tile.ComingSoon ? 0.62f : 1f;
                parts.button.interactable = !tile.ComingSoon;
                if (parts.mark != null) parts.mark.SetActive(tile.Mine);
                if (parts.footer != null) parts.footer.SetActive(tile.Chosen);
                for (int e = 0; e < parts.edges.Length; e++)
                {
                    parts.edges[e].effectColor = tile.Chosen ? TileChosenEdge : TileEdgeColour;
                }

                string id = tile.Id;
                if (!tile.ComingSoon) parts.button.onClick.AddListener(() => ClickTile(id));
            }

            int rows = (shownTiles.Count + 3) / 4;
            if (libraryContent != null)
            {
                libraryContent.sizeDelta = new Vector2(libraryContent.sizeDelta.x,
                    Mathf.Max(TileHeight, (rows * (TileHeight + TileGap)) - TileGap));
            }

            SetActive("Pages/Library/Empty", shownTiles.Count == 0);
        }

        // One click opens the sheet (a moment later, so a second click can start the game instead).
        private void ClickTile(string id)
        {
            if (clickedTile == id && Time.unscaledTime - clickedAt <= DoubleClickSeconds)
            {
                sheetPending = false;
                clickedTile = null;
                actions?.Start?.Invoke();
                return;
            }

            clickedTile = id;
            clickedAt = Time.unscaledTime;
            sheetPending = true;
            actions?.SelectGame?.Invoke(id);
        }

        private void UpdateBoard()
        {
            if (!sheetPending || Time.unscaledTime - clickedAt <= DoubleClickSeconds) return;
            sheetPending = false;
            if (page == FrontEndPage.Library) Go(FrontEndPage.Details);
        }

        private BoardTileParts NewBoardTile()
        {
            GameObject copy = Instantiate(gameEntryTemplate, libraryContent, false);
            copy.name = "Tile" + boardTiles.Count;
            copy.SetActive(true);
            Transform t = copy.transform;
            Transform picture = t.Find("Picture");
            Transform cover = t.Find("Cover");
            Transform tag = t.Find("Tag");
            Transform name = t.Find("Name");
            Transform line = t.Find("Detail");
            Transform footer = t.Find("Footer");
            Transform mark = t.Find("Mark");
            return new BoardTileParts
            {
                root = copy,
                button = copy.GetComponent<Button>(),
                edges = copy.GetComponents<Outline>(),
                picture = picture != null ? picture.GetComponent<RawImage>() : null,
                cover = cover != null ? cover.GetComponent<Image>() : null,
                mark = mark != null ? mark.gameObject : null,
                tagFill = tag != null ? tag.GetComponent<Image>() : null,
                tag = tag != null ? tag.GetComponentInChildren<Text>(true) : null,
                name = name != null ? name.GetComponent<Text>() : null,
                line = line != null ? line.GetComponent<Text>() : null,
                footer = footer != null ? footer.gameObject : null,
                dim = copy.TryGetComponent(out CanvasGroup group) ? group : copy.AddComponent<CanvasGroup>(),
            };
        }

        // The game's picture, or the board's plain colour block when it has none.
        private static void ShowCover(RawImage picture, Transform cover, Texture texture, Color colour)
        {
            if (picture != null)
            {
                picture.texture = texture;
                picture.color = texture != null ? Color.white : new Color(0f, 0f, 0f, 0f);
            }

            Image fill = cover != null ? cover.GetComponent<Image>() : null;
            if (fill != null) fill.color = colour;
        }

        // ---------- the Game sheet ----------

        private void RenderBoardSheet()
        {
            const string sheet = "Pages/Details/Sheet/";
            SplitDetail(model.Detail, out string players, out _);
            SetText(sheet + "Tag/Label", Upper(model.IsEmptyTable ? "ANY NUMBER" : players));
            SetText(sheet + "Name", Upper(model.GameName));
            SetText(sheet + "Tagline", model.Tagline);
            ShowCover(Picture(sheet + "Picture"), Find(sheet + "Cover"), model.Picture, CoverGame);
            for (int i = 0; i < HowToPlayRows; i++)
            {
                string row = sheet + "How/Row" + i;
                bool shown = i < model.HowToPlay.Count;
                SetActive(row, shown);
                if (shown) SetText(row + "/Label", model.HowToPlay[i]);
            }

            SetActive(sheet + "HowEmpty", model.HowToPlay.Count == 0);

            const string setup = "Pages/Details/Setup/";
            SetActive(setup + "Players", model.ShowPlayers);
            SetText(setup + "Players/Count", model.PlayerCount.ToString());
            SetInteractable(setup + "Players/Minus", model.CanChangePlayers);
            SetInteractable(setup + "Players/Plus", model.CanChangePlayers);
            for (int i = 0; i < BoardSeatCount; i++)
            {
                string seat = setup + "Seats/Seat" + i;
                bool shown = i < model.PlayerCount;
                SetActive(seat, shown);
                if (!shown) continue;
                Color colour = i < model.PlayerColors.Count ? model.PlayerColors[i] : Color.grey;
                Transform dot = Find(seat + "/Dot");
                Image dotImage = dot != null ? dot.GetComponent<Image>() : null;
                if (dotImage != null) dotImage.color = colour;
                SetText(seat + "/Colour", i < model.PlayerColorNames.Count ? Upper(model.PlayerColorNames[i]) : string.Empty);
                string name = i < model.PlayerNames.Count ? model.PlayerNames[i] : $"Player {i + 1}";
                SetText(seat + "/Name", i == 0 ? $"{Upper(name)} (you)" : name);
                SetInteractable(seat, model.CanChangeColours);
            }

            SetActive(setup + "Seats", model.PlayerCount > 0);
            SheetRow(setup + "Difficulty", model.ShowMode, model.ModeName, model.CanChangeMode);
            SheetRow(setup + "WinMode", model.ShowWinMode, model.WinModeText, model.ShowWinMode);
            Transform chevron = Find(setup + "More/Chevron");
            if (chevron != null) chevron.localEulerAngles = new Vector3(0f, 0f, moreOptionsOpen ? -90f : 0f);
            SheetRow(setup + "Turns", moreOptionsOpen && model.ShowTurns, model.TurnsText, model.ShowTurns);
            SheetRow(setup + "Deck", moreOptionsOpen, DeckShort(), model.CanChangeDeck);
            SetText(setup + "More/Hint", model.ShowTurns ? "Turns · Deck" : "Deck");

            string summary = model.GameName;
            if (model.ShowPlayers && model.PlayersText.Length > 0) summary += " · " + model.PlayersText;
            if (model.ShowMode && model.ModeName.Length > 0) summary += " · " + model.ModeName;
            if (model.ShowWinMode && model.WinModeText.Length > 0) summary += " · " + model.WinModeText;
            SetText("Pages/Details/Summary", summary);
            SetInteractable("Pages/Details/Start", model.CanStart);
        }

        private void SheetRow(string path, bool shown, string value, bool canChange)
        {
            SetActive(path, shown);
            if (!shown) return;
            SetText(path + "/Value", value);
            SetInteractable(path + "/Previous", canChange);
            SetInteractable(path + "/Next", canChange);
        }
    }
}
