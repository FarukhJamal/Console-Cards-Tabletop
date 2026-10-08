using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Catalog;
using UnityEngine;
using UnityEngine.EventSystems;
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

        private readonly Dictionary<string, ComponentCatalogEntry> entriesById =
            new Dictionary<string, ComponentCatalogEntry>(StringComparer.Ordinal);
        private Action<ComponentCatalogEntry, int> place;
        private Action beforeOpen;
        private ToolboxEntryTile selectedTile;
        private int tabIndex;
        private int chipIndex;
        private Action<bool> setHand;
        private bool handOn;

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

            SelectTab(0);
            CloseToolbox();
            ClearPlacementHint();
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

        public void OpenToolbox()
        {
            beforeOpen?.Invoke();
            ClearPlacementHint();
            panel.SetActive(true);
            openButton.gameObject.SetActive(false);
            ClearSelectedUiObject();
        }

        public void CloseToolbox()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (openButton != null)
            {
                openButton.gameObject.SetActive(placingCard == null || !placingCard.activeSelf);
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
            openButton.gameObject.SetActive(false);
            placingCard.SetActive(true);
            placingControls.SetActive(true);
            placingSubtitle.text = $"{placementSubject} · rotation {Mathf.RoundToInt(rotationDegrees)}°";
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

            if (openButton != null && panel != null && !panel.activeSelf)
            {
                openButton.gameObject.SetActive(true);
            }
        }

        public override void Unbind()
        {
            Clear(openButton);
            Clear(closeButton);
            Clear(handSwitch?.Button);
            setHand = null;
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
                    if (!entriesById.ContainsKey(id) || !tiled.Add(id))
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
            place(entry, value);
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

            for (int i = 0; i < gameChips.Length; i++)
            {
                bool selected = i == chipIndex;
                gameChips[i].Fill.color = selected ? chipSelectedFill : chipFill;
                gameChips[i].Label.color = selected ? chipSelectedText : chipText;
            }
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
