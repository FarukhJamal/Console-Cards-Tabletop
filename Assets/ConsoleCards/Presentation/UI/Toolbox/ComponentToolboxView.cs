using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Catalog;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI.Toolbox
{
    /// <summary>What the Toolbox needs from the session: the library, the current game, and how to place.</summary>
    public readonly struct ComponentToolboxBindings
    {
        public ComponentToolboxBindings(
            ComponentLibrary library,
            GameDefinition currentGame,
            Action<ComponentCatalogEntry, int> place,
            bool showHandSwitch = false,
            bool handOn = false,
            Action<bool> setHand = null)
        {
            Library = library ?? throw new ArgumentNullException(nameof(library));
            CurrentGame = currentGame;
            Place = place ?? throw new ArgumentNullException(nameof(place));
            if (showHandSwitch && setHand == null)
            {
                throw new ArgumentNullException(nameof(setHand), "A shown Hand switch needs its handler.");
            }

            ShowHandSwitch = showHandSwitch;
            HandOn = handOn;
            SetHand = setHand;
        }

        public ComponentLibrary Library { get; }

        /// <summary>The game being played, or null at an Empty Table.</summary>
        public GameDefinition CurrentGame { get; }

        /// <summary>Place an entry; the value is the tile's count, chosen size, or 0.</summary>
        public Action<ComponentCatalogEntry, int> Place { get; }

        /// <summary>The footer's Hand switch is shown (an Empty Table, doc 22 H-E).</summary>
        public bool ShowHandSwitch { get; }

        public bool HandOn { get; }

        /// <summary>Called with the new state when the player flips the Hand switch.</summary>
        public Action<bool> SetHand { get; }
    }

    /// <summary>An authored ON / OFF switch row (the Toolbox footer's Hand switch). The state is shown in words.</summary>
    [Serializable]
    public sealed class ToolboxSwitch
    {
        [SerializeField] private GameObject row;
        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Text word;
        [SerializeField] private Text hint;
        [SerializeField] private Color onFill = Color.green;
        [SerializeField] private Color offFill = Color.gray;
        [SerializeField] private Color onWordColor = Color.white;
        [SerializeField] private Color offWordColor = Color.black;
        [SerializeField] private float knobOffset = 22f;
        [SerializeField] private float wordOffset = 14f;
        [SerializeField] private string onHint = string.Empty;
        [SerializeField] private string offHint = string.Empty;
        [Tooltip("Board 6 (optional): the knob's colour on and off.")]
        [SerializeField] private Image knobImage;
        [SerializeField] private Color knobOnColor = Color.white;
        [SerializeField] private Color knobOffColor = Color.white;

        public bool IsComplete =>
            row != null && button != null && fill != null && knob != null && word != null && hint != null;

        public GameObject Row => row;

        public Button Button => button;

        public void Show(bool on)
        {
            fill.color = on ? onFill : offFill;
            knob.anchoredPosition = new Vector2(on ? knobOffset : -knobOffset, knob.anchoredPosition.y);
            RectTransform wordRect = word.rectTransform;
            wordRect.anchoredPosition = new Vector2(on ? -wordOffset : wordOffset, wordRect.anchoredPosition.y);
            word.text = on ? "ON" : "OFF";
            word.color = on ? onWordColor : offWordColor;
            hint.text = on ? onHint : offHint;
            if (knobImage != null) knobImage.color = on ? knobOnColor : knobOffColor;
        }
    }

    /// <summary>A group of authored tiles for one catalog, with the message shown when it has none.</summary>
    [Serializable]
    public sealed class ToolboxShelf
    {
        [SerializeField] private ComponentCatalog catalog;
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private ToolboxEntryTile[] tiles = Array.Empty<ToolboxEntryTile>();

        public ComponentCatalog Catalog => catalog;

        public GameObject Root => root;

        public GameObject EmptyState => emptyState;

        public IReadOnlyList<ToolboxEntryTile> Tiles => tiles;
    }

    /// <summary>A tab for one shelf category (Base Box, Controller Box, Games, Environment).</summary>
    [Serializable]
    public sealed class ToolboxTab
    {
        [SerializeField] private ComponentCatalogCategory category;
        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;
        [SerializeField] private GameObject chipRow;
        [SerializeField] private int shelfIndex = -1;

        public ComponentCatalogCategory Category => category;

        public Button Button => button;

        public Image Fill => fill;

        public Text Label => label;

        /// <summary>The Games tab's row of game chips; null on other tabs.</summary>
        public GameObject ChipRow => chipRow;

        /// <summary>The shelf this tab shows; -1 on the Games tab (each chip has its own shelf).</summary>
        public int ShelfIndex => shelfIndex;
    }

    /// <summary>A chip on the Games tab for one game box.</summary>
    [Serializable]
    public sealed class ToolboxGameChip
    {
        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;
        [SerializeField] private int shelfIndex;

        public Button Button => button;

        public Image Fill => fill;

        public Text Label => label;

        public int ShelfIndex => shelfIndex;
    }

    /// <summary>
    /// The Toolbox (doc 22, C2a): an authored prefab whose tabs, chips and tiles are built at edit time from
    /// the component library (Console Cards > Toolbox > Build / Sync). At runtime it only binds clicks, checks
    /// that every Toolbox entry has a tile and every tile has an entry, and switches what is shown. While a
    /// piece is being placed the panel folds into the Placing card.
    /// </summary>
    public sealed class ComponentToolboxView : ReusableUiView
    {
        public const string SyncHint = "Run Console Cards > Toolbox > Sync Tiles from Library.";

        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private ToolboxTab[] tabs = Array.Empty<ToolboxTab>();
        [SerializeField] private ToolboxGameChip[] gameChips = Array.Empty<ToolboxGameChip>();
        [SerializeField] private ToolboxShelf[] shelves = Array.Empty<ToolboxShelf>();
        [SerializeField] private GameObject placingCard;
        [SerializeField] private Image placingIcon;
        [SerializeField] private Text placingSubtitle;
        [SerializeField] private GameObject placingControls;

        [Header("Footer Hand switch (Empty Table, H-E)")]
        [SerializeField] private ToolboxSwitch handSwitch = new ToolboxSwitch();
        [Header("Colour of new pieces (board 8)")]
        [SerializeField] private GameObject pieceColourRow;
        [SerializeField] private Button[] pieceColourButtons = Array.Empty<Button>();
        [SerializeField] private Image[] pieceColourSwatches = Array.Empty<Image>();
        [SerializeField] private GameObject[] pieceColourRings = Array.Empty<GameObject>();
        [SerializeField] private RectTransform contentViewport;
        [SerializeField] private float viewportBottomWithSwitch;
        [SerializeField] private float viewportBottomWithoutSwitch;

        [Header("Tab and chip colours")]
        [SerializeField] private Color tabFill = Color.gray;
        [SerializeField] private Color tabText = Color.gray;
        [SerializeField] private Color tabSelectedFill = Color.white;
        [SerializeField] private Color tabSelectedText = Color.black;
        [SerializeField] private Color chipFill = Color.white;
        [SerializeField] private Color chipText = Color.black;
        [SerializeField] private Color chipSelectedFill = Color.green;
        [SerializeField] private Color chipSelectedText = Color.white;

        [Header("Colour-B Toolbox (TOOL-B, optional): search, a detail line, tiles in a grid")]
        [SerializeField] private InputField searchField;
        [SerializeField] private Text detailLabel;
        [SerializeField, TextArea(2, 4)] private string detailIdle =
            "Pick a piece, then click the table to place it; keep clicking for more. Right-click or Esc stops.";
        [SerializeField] private string detailHow = "Click the table to place it; keep clicking for more. Right-click or Esc stops.";
        [Tooltip("Lay the tiles out in a grid at runtime (a tile wider than one cell takes two), so a search closes the gaps.")]
        [SerializeField] private bool flowTiles;
        [SerializeField] private float flowCell = 120f;
        [SerializeField] private float flowGap = 10f;
        [SerializeField] private int flowColumns = 4;
        [SerializeField] private float flowTop;
        [SerializeField] private Color tabNoMatchText = new Color(0.42f, 0.455f, 0.467f, 1f);

        [Header("Board 8 (optional): RECENT, the table tools, the placing chip, the detail under the tiles")]
        [Tooltip("The shelf of RECENT copies (-1: none). A copy shows while its piece is among the last ones placed.")]
        [SerializeField] private int recentShelfIndex = -1;
        [SerializeField] private int recentLimit = 8;
        [SerializeField] private Button[] tableToolButtons = Array.Empty<Button>();
        [SerializeField] private TableToolKind[] tableToolKinds = Array.Empty<TableToolKind>();
        [Tooltip("The placing words: {0} the piece, {1} its turn in degrees.")]
        [SerializeField] private string placingFormat = "{0} · rotation {1}°";
        [Tooltip("The placing chip sits beside the pointer (board 8) instead of staying where it was made.")]
        [SerializeField] private bool placingFollowsPointer;
        [SerializeField] private Vector2 placingPointerOffset = new Vector2(48f, 0f);
        [Tooltip("When set, the tiles' viewport is only as tall as its rows (up to viewportMaxHeight) and this box sits under it.")]
        [SerializeField] private RectTransform detailBox;
        [SerializeField] private float viewportMaxHeight = 520f;
        [SerializeField] private float detailGap = 21f;

        private readonly Dictionary<string, ComponentCatalogEntry> entriesById =
            new Dictionary<string, ComponentCatalogEntry>(StringComparer.Ordinal);
        private Action<ComponentCatalogEntry, int> place;
        private Action beforeOpen;
        private ToolboxEntryTile selectedTile;
        private int tabIndex;
        private int chipIndex;
        private Action<bool> setHand;
        private bool handOn;
        private Action<bool> panelOpenChanged;
        private bool placingHintVisible = true;
        private Action<TableToolKind> chooseTableTool;
        private readonly List<string> recent = new List<string>();
        private bool recentLoaded;
        private const string RecentPref = "ConsoleCards.ToolboxRecent";

        public void ValidateReferences()
        {
            if (openButton == null
                || panel == null
                || closeButton == null
                || placingCard == null
                || placingSubtitle == null
                || placingControls == null
                || tabs.Length == 0
                || shelves.Length == 0)
            {
                throw new InvalidOperationException(
                    "ComponentToolboxView requires its open button, panel, close button, tabs, shelves and placing card. "
                    + SyncHint);
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                ToolboxTab tab = tabs[i];
                if (tab == null || tab.Button == null || tab.Fill == null || tab.Label == null
                    || (tab.ShelfIndex < 0 && tab.ChipRow == null)
                    || tab.ShelfIndex >= shelves.Length)
                {
                    throw new InvalidOperationException($"Toolbox tab {i} is incomplete. " + SyncHint);
                }
            }

            for (int i = 0; i < gameChips.Length; i++)
            {
                ToolboxGameChip chip = gameChips[i];
                if (chip == null || chip.Button == null || chip.Fill == null || chip.Label == null
                    || chip.ShelfIndex < 0 || chip.ShelfIndex >= shelves.Length)
                {
                    throw new InvalidOperationException($"Toolbox game chip {i} is incomplete. " + SyncHint);
                }
            }

            for (int i = 0; i < shelves.Length; i++)
            {
                ToolboxShelf shelf = shelves[i];
                if (shelf == null || shelf.Catalog == null || shelf.Root == null || shelf.EmptyState == null)
                {
                    throw new InvalidOperationException($"Toolbox shelf {i} is incomplete. " + SyncHint);
                }

                for (int t = 0; t < shelf.Tiles.Count; t++)
                {
                    if (shelf.Tiles[t] == null)
                    {
                        throw new InvalidOperationException($"Toolbox shelf {i} has a missing tile. " + SyncHint);
                    }

                    shelf.Tiles[t].ValidateReferences();
                }
            }
        }

        public void Bind(ComponentToolboxBindings bindings, Action beforeOpenToolbox)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            place = bindings.Place;
            beforeOpen = beforeOpenToolbox;
            BindHandSwitch(bindings);
            IndexAndMatchTiles(bindings.Library);

            openButton.onClick.AddListener(OpenToolbox);
            closeButton.onClick.AddListener(CloseToolbox);
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].Button.onClick.AddListener(() => SelectTab(index));
            }

            chipIndex = 0;
            for (int i = 0; i < gameChips.Length; i++)
            {
                int index = i;
                gameChips[i].Button.onClick.AddListener(() => SelectChip(index));
                if (bindings.CurrentGame != null && shelves[gameChips[i].ShelfIndex].Catalog.Game == bindings.CurrentGame)
                {
                    chipIndex = i;
                }
            }

            for (int i = 0; i < shelves.Length; i++)
            {
                for (int t = 0; t < shelves[i].Tiles.Count; t++)
                {
                    shelves[i].Tiles[t].Bind(HandleTilePicked);
                }

                shelves[i].EmptyState.SetActive(shelves[i].Tiles.Count == 0);
            }

            for (int i = 0; i < tableToolButtons.Length; i++)
            {
                if (tableToolButtons[i] == null || i >= tableToolKinds.Length) continue;
                TableToolKind kind = tableToolKinds[i];
                tableToolButtons[i].onClick.AddListener(() => PickTableTool(kind));
            }

            LoadRecent();
            BindSearchAndDetail();
            SelectTab(0);
            CloseToolbox();
            ClearPlacementHint();
        }

        // ---------- board 8: the table tools, RECENT ----------

        /// <summary>
        /// Board 8: "Colour of new pieces": the swatches (the first one shows the seat colour), which is chosen, and
        /// what a click does. A Toolbox made before has no swatches.
        /// </summary>
        public void SetPieceColours(Color seatColour, int chosen, Action<int> choose)
        {
            if (pieceColourRow != null && pieceColourRow.activeSelf != (choose != null)) pieceColourRow.SetActive(choose != null);
            if (pieceColourSwatches.Length > 0 && pieceColourSwatches[0] != null) pieceColourSwatches[0].color = seatColour;
            for (int i = 0; i < pieceColourButtons.Length; i++)
            {
                if (pieceColourButtons[i] == null) continue;
                int which = i;
                pieceColourButtons[i].onClick.RemoveAllListeners();
                if (choose != null) pieceColourButtons[i].onClick.AddListener(() => choose(which));
                if (i < pieceColourRings.Length && pieceColourRings[i] != null && pieceColourRings[i].activeSelf != (i == chosen))
                {
                    pieceColourRings[i].SetActive(i == chosen);
                }
            }
        }

        /// <summary>What the TABLE TOOLS buttons do (choose that tool; the Toolbox closes). Null hides them.</summary>
        public void SetTableTools(Action<TableToolKind> choose)
        {
            chooseTableTool = choose;
            for (int i = 0; i < tableToolButtons.Length; i++)
            {
                if (tableToolButtons[i] != null) tableToolButtons[i].gameObject.SetActive(choose != null);
            }
        }

        private void PickTableTool(TableToolKind kind)
        {
            Action<TableToolKind> choose = chooseTableTool;
            CloseToolbox();
            choose?.Invoke(kind);
        }

        private void LoadRecent()
        {
            if (recentLoaded) return;
            recentLoaded = true;
            recent.Clear();
            string saved = PlayerPrefs.GetString(RecentPref, string.Empty);
            if (saved.Length == 0) return;
            string[] keys = saved.Split(';');
            for (int i = 0; i < keys.Length && recent.Count < recentLimit; i++)
            {
                if (keys[i].Length > 0 && !recent.Contains(keys[i])) recent.Add(keys[i]);
            }
        }

        private void RememberRecent(ToolboxEntryTile tile)
        {
            if (recentShelfIndex < 0 || tile == null) return;
            string key = tile.RecentKey;
            recent.Remove(key);
            recent.Insert(0, key);
            while (recent.Count > Mathf.Max(1, recentLimit)) recent.RemoveAt(recent.Count - 1);
            PlayerPrefs.SetString(RecentPref, string.Join(";", recent));
            ApplySearch();
        }

        // RECENT's count is the number of recent pieces shown on it.
        private void RefreshRecentCount()
        {
            if (recentShelfIndex < 0 || recentShelfIndex >= shelves.Length) return;
            int shown = 0;
            IReadOnlyList<ToolboxEntryTile> tiles = shelves[recentShelfIndex].Tiles;
            for (int t = 0; t < tiles.Count; t++)
            {
                if (tiles[t].gameObject.activeSelf) shown++;
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i].ShelfIndex != recentShelfIndex) continue;
                Transform count = tabs[i].Label.transform.parent.Find("Count");
                Text label = count != null ? count.GetComponent<Text>() : null;
                if (label != null) label.text = shown.ToString();
            }
        }

        // ---------- TOOL-B: search, the detail line, the grid ----------

        private string query = string.Empty;

        private void BindSearchAndDetail()
        {
            for (int i = 0; i < shelves.Length; i++)
            {
                for (int t = 0; t < shelves[i].Tiles.Count; t++)
                {
                    ToolboxTileHover hover = shelves[i].Tiles[t].GetComponent<ToolboxTileHover>();
                    if (hover != null) hover.Bind(ShowDetail);
                }
            }

            query = string.Empty;
            if (searchField != null)
            {
                searchField.SetTextWithoutNotify(string.Empty);
                searchField.onValueChanged.AddListener(Search);
            }

            ApplySearch();
        }

        private void ShowDetail(ToolboxEntryTile tile)
        {
            if (detailLabel == null) return;
            if (tile == null || !entriesById.TryGetValue(tile.EntryId, out ComponentCatalogEntry entry))
            {
                detailLabel.text = detailIdle;
                return;
            }

            if (!string.IsNullOrEmpty(tile.DetailTitle))
            {
                detailLabel.text = $"<b><color=#E8ECED>{tile.DetailTitle}</color></b>\n{detailHow}";
                return;
            }

            detailLabel.text = string.IsNullOrEmpty(entry.Description)
                ? $"<b><color=#E8ECED>{entry.DisplayName}</color></b>\n{detailHow}"
                : $"<b><color=#E8ECED>{entry.DisplayName}</color></b>  ·  {entry.Description}\n{detailHow}";
        }

        private void Search(string text)
        {
            query = text == null ? string.Empty : text.Trim();
            ApplySearch();
        }

        // Tiles that match the words show (all when there are none); a tab with no match is dimmed, and the open tab
        // moves to the first one with a match.
        private void ApplySearch()
        {
            for (int i = 0; i < shelves.Length; i++)
            {
                int shown = 0;
                for (int t = 0; t < shelves[i].Tiles.Count; t++)
                {
                    ToolboxEntryTile tile = shelves[i].Tiles[t];
                    bool match = Matches(tile);
                    if (tile.gameObject.activeSelf != match) tile.gameObject.SetActive(match);
                    if (match) shown++;
                }

                shelves[i].EmptyState.SetActive(shown == 0);
                if (flowTiles) Flow(shelves[i], i == recentShelfIndex);
            }

            RefreshRecentCount();

            if (query.Length > 0 && !TabHasMatch(tabIndex))
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    if (TabHasMatch(i))
                    {
                        SelectTab(i);
                        break;
                    }
                }
            }

            FollowSearchWithChip();
            RefreshTabColours();
            ScrollToTop();
            int visible = VisibleShelfIndex();
            if (visible >= 0) FitViewportToTiles(shelves[visible]);
        }

        private int VisibleShelfIndex()
        {
            if (tabs.Length == 0) return -1;
            ToolboxTab tab = tabs[Mathf.Clamp(tabIndex, 0, tabs.Length - 1)];
            if (tab.ShelfIndex >= 0) return tab.ShelfIndex;
            return gameChips.Length > 0 ? gameChips[Mathf.Clamp(chipIndex, 0, gameChips.Length - 1)].ShelfIndex : -1;
        }

        // On the Game boxes tab, the chip moves to a game with a match when the shown one has none.
        private void FollowSearchWithChip()
        {
            if (query.Length == 0 || tabs[tabIndex].ShelfIndex >= 0 || gameChips.Length == 0) return;
            if (ShelfHasMatch(gameChips[Mathf.Clamp(chipIndex, 0, gameChips.Length - 1)].ShelfIndex)) return;
            for (int c = 0; c < gameChips.Length; c++)
            {
                if (!ShelfHasMatch(gameChips[c].ShelfIndex)) continue;
                chipIndex = c;
                RefreshShelves();
                return;
            }
        }

        private void ScrollToTop()
        {
            ScrollRect scroll = contentViewport != null ? contentViewport.GetComponent<ScrollRect>() : null;
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private bool Matches(ToolboxEntryTile tile)
        {
            if (tile.IsRecentCopy && !recent.Contains(tile.RecentKey)) return false; // RECENT: only the recent pieces
            if (query.Length == 0) return true;
            if (!entriesById.TryGetValue(tile.EntryId, out ComponentCatalogEntry entry)) return false;
            return (entry.DisplayName != null && entry.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (entry.Description != null && entry.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(tile.DetailTitle)
                    && tile.DetailTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool TabHasMatch(int index)
        {
            ToolboxTab tab = tabs[index];
            if (tab.ShelfIndex >= 0) return ShelfHasMatch(tab.ShelfIndex);
            for (int i = 0; i < gameChips.Length; i++)
            {
                if (ShelfHasMatch(gameChips[i].ShelfIndex)) return true;
            }

            return false;
        }

        private bool ShelfHasMatch(int shelf)
        {
            IReadOnlyList<ToolboxEntryTile> tiles = shelves[shelf].Tiles;
            for (int t = 0; t < tiles.Count; t++)
            {
                if (tiles[t].gameObject.activeSelf) return true;
            }

            return false;
        }

        private void RefreshTabColours()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool selected = i == tabIndex;
                tabs[i].Label.color = selected ? tabSelectedText
                    : query.Length > 0 && !TabHasMatch(i) ? tabNoMatchText
                    : tabText;
            }
        }

        private readonly List<ToolboxEntryTile> flowOrder = new List<ToolboxEntryTile>();

        // The shown tiles of a shelf in rows of flowColumns cells; a tile wider than a cell and a half takes two.
        // RECENT lays its tiles out newest first.
        private void Flow(ToolboxShelf shelf, bool byRecency = false)
        {
            int column = 0;
            float y = flowTop;
            float rowHeight = 0f;
            int columns = Mathf.Max(1, flowColumns);
            flowOrder.Clear();
            for (int t = 0; t < shelf.Tiles.Count; t++) flowOrder.Add(shelf.Tiles[t]);
            if (byRecency) flowOrder.Sort((a, b) => RecentRank(a).CompareTo(RecentRank(b)));
            for (int t = 0; t < flowOrder.Count; t++)
            {
                ToolboxEntryTile tile = flowOrder[t];
                if (!tile.gameObject.activeSelf) continue;
                RectTransform rect = (RectTransform)tile.transform;
                int span = rect.sizeDelta.x > flowCell * 1.5f ? Mathf.Min(2, columns) : 1;
                if (column + span > columns)
                {
                    column = 0;
                    y += rowHeight + flowGap;
                    rowHeight = 0f;
                }

                rect.anchoredPosition = new Vector2(column * (flowCell + flowGap), -y);
                rowHeight = Mathf.Max(rowHeight, rect.sizeDelta.y);
                column += span;
            }

            shelfHeights[shelf] = y + rowHeight;
        }

        private readonly Dictionary<ToolboxShelf, float> shelfHeights = new Dictionary<ToolboxShelf, float>();

        private int RecentRank(ToolboxEntryTile tile)
        {
            int rank = recent.IndexOf(tile.RecentKey);
            return rank < 0 ? int.MaxValue : rank;
        }

        // Board 8: the tiles' viewport is as tall as the shown shelf's rows (up to its most) and the detail box sits
        // right under it.
        private void FitViewportToTiles(ToolboxShelf shown)
        {
            if (detailBox == null || contentViewport == null || shown == null) return;
            float height = shelfHeights.TryGetValue(shown, out float rows) ? rows : viewportMaxHeight;
            if (height <= 0f) height = 120f; // the empty line
            RectTransform shelfRect = shown.Root.transform as RectTransform;
            float top = shelfRect != null ? -shelfRect.anchoredPosition.y : 0f;
            height = Mathf.Min(viewportMaxHeight, height + top);
            contentViewport.anchorMin = new Vector2(0f, 1f);
            contentViewport.anchorMax = new Vector2(1f, 1f);
            contentViewport.pivot = new Vector2(0.5f, 1f);
            contentViewport.offsetMin = new Vector2(0f, -height);
            contentViewport.offsetMax = Vector2.zero;
            detailBox.anchoredPosition = new Vector2(detailBox.anchoredPosition.x, -(height + detailGap));
        }

        private void BindHandSwitch(ComponentToolboxBindings bindings)
        {
            if (handSwitch == null || !handSwitch.IsComplete)
            {
                if (bindings.ShowHandSwitch)
                {
                    throw new InvalidOperationException(
                        "The Toolbox prefab has no footer Hand switch. Run Console Cards > Toolbox > Build Toolbox Prefab.");
                }

                return;
            }

            handSwitch.Row.SetActive(bindings.ShowHandSwitch);
            if (contentViewport != null)
            {
                contentViewport.offsetMin = new Vector2(
                    contentViewport.offsetMin.x,
                    bindings.ShowHandSwitch ? viewportBottomWithSwitch : viewportBottomWithoutSwitch);
            }

            if (!bindings.ShowHandSwitch)
            {
                setHand = null;
                return;
            }

            setHand = bindings.SetHand;
            handOn = bindings.HandOn;
            handSwitch.Show(handOn);
            handSwitch.Button.onClick.AddListener(FlipHandSwitch);
        }

        private void FlipHandSwitch()
        {
            if (setHand == null)
            {
                return;
            }

            handOn = !handOn;
            handSwitch.Show(handOn);
            ClearSelectedUiObject();
            setHand(handOn);
        }

        public bool IsPanelOpen => panel != null && panel.activeSelf;

        /// <summary>Shows or hides the "Left-click place…" bar while placing (Hints switch, doc 23 R1).</summary>
        public void SetPlacingHintVisible(bool visible)
        {
            placingHintVisible = visible;
            if (placingControls != null && placingCard != null && placingCard.activeSelf)
            {
                placingControls.SetActive(visible);
            }
        }

        /// <summary>Told whenever the panel opens or closes (the session bar moves beside the open panel, UI-1).</summary>
        public void SetPanelOpenListener(Action<bool> listener)
        {
            panelOpenChanged = listener;
        }

        /// <summary>HUD-B: the + TOOLBOX button stays hidden (the HUD has its own Toolbox button); the panel still opens.</summary>
        public void SetLauncherHidden(bool hidden)
        {
            launcherHidden = hidden;
            if (openButton == null) return;
            bool show = !hidden
                && (panel == null || !panel.activeSelf)
                && (placingCard == null || !placingCard.activeSelf);
            openButton.gameObject.SetActive(show);
        }

        private bool launcherHidden;

        public void OpenToolbox()
        {
            beforeOpen?.Invoke();
            ClearPlacementHint();
            panel.SetActive(true);
            openButton.gameObject.SetActive(false);
            panelOpenChanged?.Invoke(true);
            ClearSelectedUiObject();
        }

        public void CloseToolbox()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            panelOpenChanged?.Invoke(false);

            if (openButton != null)
            {
                openButton.gameObject.SetActive(!launcherHidden && (placingCard == null || !placingCard.activeSelf));
            }

            ClearSelectedUiObject();
        }

        /// <summary>The panel folds into the Placing card while a piece is placed or moved.</summary>
        public void ShowPlacementHint(string placementSubject, float rotationDegrees, Sprite icon = null)
        {
            if (string.IsNullOrWhiteSpace(placementSubject))
            {
                throw new ArgumentException("A placement hint requires a component name.", nameof(placementSubject));
            }

            if (float.IsNaN(rotationDegrees) || float.IsInfinity(rotationDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
            }

            panel.SetActive(false);
            panelOpenChanged?.Invoke(false);
            openButton.gameObject.SetActive(false);
            placingCard.SetActive(true);
            placingControls.SetActive(placingHintVisible);
            string subject = placementSubject;
            if (placingFollowsPointer)
            {
                // Board 8 words: "d6  click to place · Esc stops" (the chip has its own stop words).
                int cut = subject.IndexOf(" · ", StringComparison.Ordinal);
                if (cut > 0) subject = subject.Substring(0, cut);
            }

            placingSubtitle.text = string.Format(placingFormat, subject, Mathf.RoundToInt(rotationDegrees));
            FollowPointer();
            if (placingIcon != null)
            {
                placingIcon.gameObject.SetActive(icon != null);
                placingIcon.sprite = icon;
            }
        }

        public void ClearPlacementHint()
        {
            if (placingCard != null)
            {
                placingCard.SetActive(false);
            }

            if (placingControls != null)
            {
                placingControls.SetActive(false);
            }

            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
                selectedTile = null;
            }

            if (!launcherHidden && openButton != null && panel != null && !panel.activeSelf)
            {
                openButton.gameObject.SetActive(true);
            }
        }

        private void LateUpdate()
        {
            if (placingFollowsPointer && placingCard != null && placingCard.activeSelf) FollowPointer();
        }

        // The placing chip beside the pointer, kept on the screen.
        private void FollowPointer()
        {
            if (!placingFollowsPointer || placingCard == null || Mouse.current == null) return;
            RectTransform chip = placingCard.transform as RectTransform;
            RectTransform parent = chip != null ? chip.parent as RectTransform : null;
            if (parent == null) return;
            Canvas canvas = chip.GetComponentInParent<Canvas>();
            UnityEngine.Camera eye = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Mouse.current.position.ReadValue(), eye,
                    out Vector2 local))
            {
                return;
            }

            Rect area = parent.rect;
            Vector2 size = chip.rect.size;
            Vector2 at = local + placingPointerOffset;
            if (at.x + size.x > area.xMax) at.x = local.x - placingPointerOffset.x - size.x;
            at.y = Mathf.Clamp(at.y, area.yMin + size.y * 0.5f, area.yMax - size.y * 0.5f);
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
            chip.pivot = new Vector2(0f, 0.5f);
            chip.anchoredPosition = at - parent.rect.center;
        }

        public override void Unbind()
        {
            Clear(openButton);
            for (int i = 0; i < tableToolButtons.Length; i++) Clear(tableToolButtons[i]);
            chooseTableTool = null;
            Clear(closeButton);
            Clear(handSwitch?.Button);
            setHand = null;
            panelOpenChanged = null;
            for (int i = 0; i < tabs.Length; i++)
            {
                Clear(tabs[i]?.Button);
            }

            for (int i = 0; i < gameChips.Length; i++)
            {
                Clear(gameChips[i]?.Button);
            }

            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i] == null)
                {
                    continue;
                }

                for (int t = 0; t < shelves[i].Tiles.Count; t++)
                {
                    shelves[i].Tiles[t]?.Unbind();
                }
            }

            entriesById.Clear();
            place = null;
            beforeOpen = null;
            selectedTile = null;
            if (searchField != null) searchField.onValueChanged.RemoveListener(Search);
            for (int i = 0; i < shelves.Length; i++)
            {
                if (shelves[i] == null) continue;
                for (int t = 0; t < shelves[i].Tiles.Count; t++)
                {
                    ToolboxEntryTile tile = shelves[i].Tiles[t];
                    ToolboxTileHover hover = tile != null ? tile.GetComponent<ToolboxTileHover>() : null;
                    if (hover != null) hover.Bind(null);
                }
            }
        }

        // Every Toolbox entry of the library has exactly one tile and every tile names a Toolbox entry.
        private void IndexAndMatchTiles(ComponentLibrary library)
        {
            library.Validate();
            entriesById.Clear();
            IReadOnlyList<ComponentCatalog> catalogs = library.Catalogs;
            for (int c = 0; c < catalogs.Count; c++)
            {
                IReadOnlyList<ComponentCatalogEntry> entries = catalogs[c].Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    if (entries[e].ShowInToolbox)
                    {
                        entriesById[entries[e].Id] = entries[e];
                    }
                }
            }

            HashSet<string> tiled = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < shelves.Length; i++)
            {
                for (int t = 0; t < shelves[i].Tiles.Count; t++)
                {
                    string id = shelves[i].Tiles[t].EntryId;
                    // Board 8: a size tile (d6) and a RECENT copy share their entry with other tiles.
                    bool shared = shelves[i].Tiles[t].FixedValue != 0 || shelves[i].Tiles[t].IsRecentCopy;
                    if (!entriesById.ContainsKey(id) || (!tiled.Add(id) && !shared))
                    {
                        throw new InvalidOperationException(
                            $"Toolbox tile {shelves[i].Tiles[t].name} has no Toolbox entry, or repeats one. " + SyncHint);
                    }
                }
            }

            foreach (KeyValuePair<string, ComponentCatalogEntry> pair in entriesById)
            {
                if (!tiled.Contains(pair.Key))
                {
                    throw new InvalidOperationException(
                        $"Toolbox entry '{pair.Value.DisplayName}' has no tile. " + SyncHint);
                }
            }
        }

        private void HandleTilePicked(ToolboxEntryTile tile, int value)
        {
            if (!entriesById.TryGetValue(tile.EntryId, out ComponentCatalogEntry entry) || place == null)
            {
                return;
            }

            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
            }

            selectedTile = tile;
            tile.SetSelected(true);
            ShowDetail(tile);
            Action<ComponentCatalogEntry, int> placing = place;
            RememberRecent(tile);
            placing(entry, value);
        }

        private void SelectTab(int index)
        {
            tabIndex = Mathf.Clamp(index, 0, tabs.Length - 1);
            for (int i = 0; i < tabs.Length; i++)
            {
                bool selected = i == tabIndex;
                tabs[i].Fill.color = selected ? tabSelectedFill : tabFill;
                tabs[i].Label.color = selected ? tabSelectedText : tabText;
                if (tabs[i].ChipRow != null)
                {
                    tabs[i].ChipRow.SetActive(selected);
                }
            }

            RefreshTabColours();
            ShowDetail(null);
            RefreshShelves();
        }

        private void SelectChip(int index)
        {
            chipIndex = index;
            RefreshShelves();
        }

        private void RefreshShelves()
        {
            ToolboxTab tab = tabs[tabIndex];
            int visibleShelf = tab.ShelfIndex >= 0
                ? tab.ShelfIndex
                : gameChips.Length > 0 ? gameChips[Mathf.Clamp(chipIndex, 0, gameChips.Length - 1)].ShelfIndex : -1;
            for (int i = 0; i < shelves.Length; i++)
            {
                shelves[i].Root.SetActive(i == visibleShelf);
            }

            if (visibleShelf >= 0 && visibleShelf < shelves.Length) FitViewportToTiles(shelves[visibleShelf]);

            for (int i = 0; i < gameChips.Length; i++)
            {
                bool selected = i == chipIndex;
                gameChips[i].Fill.color = selected ? chipSelectedFill : chipFill;
                gameChips[i].Label.color = selected ? chipSelectedText : chipText;
            }

            ScrollToTop(); // a shorter shelf never opens scrolled past its end
        }

        private static void Clear(Button target)
        {
            if (target != null)
            {
                target.onClick.RemoveAllListeners();
            }
        }

        private static void ClearSelectedUiObject()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
