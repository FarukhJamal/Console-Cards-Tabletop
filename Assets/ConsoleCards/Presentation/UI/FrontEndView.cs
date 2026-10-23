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
    }

    /// <summary>
    /// UI-2 (owner, 2026-10-24: the Milanote user journey): the front end. Main menu (PLAY, CREATE, SANDBOX), Player
    /// profile (profiles and avatar), Game library, Game details, then three setup steps (Game setup, Players,
    /// Review) and START GAME. It shows the same model and actions as the New table… window
    /// (<see cref="GameSelectModel"/>), so choosing a game, a mode or a player count works exactly as before.
    /// An authored Final UI prefab (Console Cards > UI > Apply Kit Look builds it when missing); the parts are found by
    /// name once, when the view is first used.
    /// </summary>
    public sealed class FrontEndView : ReusableUiView
    {
        public const int ProfileRowCount = 4;
        public const int PlayerRowCount = 6;

        [Header("Art (set by the builder)")]
        [SerializeField] private Sprite gameEntry;
        [SerializeField] private Sprite selectedGameEntry;
        [SerializeField] private Sprite profileEntry;
        [SerializeField] private Sprite selectedProfileEntry;
        [SerializeField] private Texture fallbackAvatar;
        [SerializeField] private Color activeTab = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color idleTab = new Color(0.91f, 0.925f, 0.929f, 1f);

        private readonly GameObject[] pages = new GameObject[8];
        private readonly List<GameEntryParts> gameEntries = new List<GameEntryParts>();
        private readonly ProfileRowParts[] profileRows = new ProfileRowParts[ProfileRowCount];
        private readonly PlayerRowParts[] playerRows = new PlayerRowParts[PlayerRowCount];
        private readonly PlayerRowParts[] reviewPlayers = new PlayerRowParts[PlayerRowCount];
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> texts = new Dictionary<string, Text>();
        private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();

        private bool resolved;
        private FrontEndPage page = FrontEndPage.Home;
        private GameSelectModel model;
        private GameSelectActions actions;
        private Action openGuide;
        private RectTransform libraryContent;
        private GameObject gameEntryTemplate;
        private RawImage avatarPicture;
        private RawImage libraryPicture;
        private RawImage detailsPicture;
        private RectTransform tabHighlight;

        private sealed class GameEntryParts
        {
            public GameObject root;
            public Button button;
            public Image plate;
            public RawImage picture;
            public Text name;
            public Text detail;
            public string id;
        }

        private sealed class ProfileRowParts
        {
            public GameObject root;
            public Button button;
            public Image plate;
            public RawImage avatar;
            public Text small;
            public Text name;
            public GameObject edit;
            public string id;
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
        }

        public FrontEndPage Page => page;

        public void ValidateReferences() => Resolve();

        /// <summary>Shows the front end with the New table… model; <paramref name="guide"/> opens the help.</summary>
        public void Bind(GameSelectModel selectModel, GameSelectActions selectActions, Action guide)
        {
            RequireAcquired();
            Resolve();
            model = selectModel ?? throw new ArgumentNullException(nameof(selectModel));
            actions = selectActions ?? throw new ArgumentNullException(nameof(selectActions));
            openGuide = guide;
            if (model.OpenAt.HasValue) page = model.OpenAt.Value;
            if (page >= FrontEndPage.Details && model.IsEmptyTable) page = FrontEndPage.Library;
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
            for (int i = 0; i < profileRows.Length; i++)
            {
                if (profileRows[i] != null) profileRows[i].button.onClick.RemoveAllListeners();
            }

            model = null;
            actions = null;
            openGuide = null;
        }

        /// <summary>Goes to a screen (BACK, CONTINUE, the tabs).</summary>
        public void Go(FrontEndPage target)
        {
            if (model == null) return;
            page = target;
            Render();
        }

        private void Update()
        {
            if (model == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back();
        }

        private void Back()
        {
            switch (page)
            {
                case FrontEndPage.Home:
                    actions?.Close?.Invoke();
                    break;
                case FrontEndPage.Create:
                case FrontEndPage.Profile:
                    Go(FrontEndPage.Home);
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

            On("TopBar/Tabs/Play", () => Go(FrontEndPage.Profile));
            On("TopBar/Tabs/Create", () => Go(FrontEndPage.Create));
            On("TopBar/Tabs/Sandbox", () => actions?.Sandbox?.Invoke());
            On("TopBar/Right/Guide", () => openGuide?.Invoke());
            On("TopBar/Right/Table", () => actions?.Close?.Invoke());
            On("TopBar/Right/Exit", () => actions?.Exit?.Invoke());

            On("Pages/Home/Menu/Play", () => Go(FrontEndPage.Profile));
            On("Pages/Home/Menu/Create", () => Go(FrontEndPage.Create));
            On("Pages/Home/Menu/Sandbox", () => actions?.Sandbox?.Invoke());

            On("Pages/Create/Menu/CardArt", () => actions?.OpenCardArt?.Invoke());
            On("Pages/Create/Menu/CardEditor", () => actions?.OpenCardEditor?.Invoke());
            On("Pages/Create/Menu/DeckEditor", () => actions?.OpenDeckEditor?.Invoke());
            On("Pages/Create/Back", () => Go(FrontEndPage.Home));

            On("Pages/Profile/Panel/Add", () => actions?.OpenProfile?.Invoke());
            On("Pages/Profile/Avatar/Previous", () => actions?.CycleAvatar?.Invoke(-1));
            On("Pages/Profile/Avatar/Next", () => actions?.CycleAvatar?.Invoke(1));
            On("Pages/Profile/Avatar/Customize", () => actions?.Customize?.Invoke());
            On("Pages/Profile/Back", () => Go(FrontEndPage.Home));
            On("Pages/Profile/Continue", () => Go(FrontEndPage.Library));

            On("Pages/Library/Back", () => Go(FrontEndPage.Profile));
            On("Pages/Library/Select", SelectFromLibrary);

            On("Pages/Details/Back", () => Go(FrontEndPage.Library));
            On("Pages/Details/Start", () => Go(FrontEndPage.Setup));

            On("Pages/Setup/Back", () => Go(FrontEndPage.Details));
            Picker("Pages/Setup/Options/Column/Mode", step => actions?.CycleMode?.Invoke(step));
            Picker("Pages/Setup/Options/Column/Players", step => actions?.CyclePlayers?.Invoke(step));
            Picker("Pages/Setup/Options/Column/WinMode", step => actions?.CycleWinMode?.Invoke(step));
            Picker("Pages/Setup/Options/Column/Turns", step => actions?.CycleTurns?.Invoke(step));
            Picker("Pages/Setup/Options/Column/Deck", step => actions?.CycleDeck?.Invoke(step));
            On("Pages/Setup/Continue", () => Go(FrontEndPage.Players));

            On("Pages/Players/Back", () => Go(FrontEndPage.Setup));
            On("Pages/Players/Add", () => actions?.CyclePlayers?.Invoke(1));
            On("Pages/Players/Remove", () => actions?.CyclePlayers?.Invoke(-1));
            On("Pages/Players/Continue", () => Go(FrontEndPage.Review));

            On("Pages/Review/Back", () => Go(FrontEndPage.Players));
            On("Pages/Review/Settings/Edit", () => Go(FrontEndPage.Setup));
            On("Pages/Review/Players/Edit", () => Go(FrontEndPage.Players));
            On("Pages/Review/Start", () => actions?.Start?.Invoke());
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

            bool stepped = page >= FrontEndPage.Setup;
            SetActive("Pages/Stepper", stepped);
            if (stepped)
            {
                for (int step = 0; step < 3; step++)
                {
                    bool current = (int)page - (int)FrontEndPage.Setup == step;
                    Tint("Pages/Stepper/Step" + step + "/Number", current ? activeTab : idleTab);
                    Tint("Pages/Stepper/Step" + step + "/Label", current ? activeTab : idleTab);
                }
            }

            RenderTabs();
            SetText("Status", model.Status);
            switch (page)
            {
                case FrontEndPage.Profile:
                    RenderProfile();
                    break;
                case FrontEndPage.Library:
                    RenderLibrary();
                    break;
                case FrontEndPage.Details:
                    RenderDetails();
                    break;
                case FrontEndPage.Setup:
                    RenderSetup();
                    break;
                case FrontEndPage.Players:
                    RenderPlayers(playerRows);
                    SetText("Pages/Players/Title", Upper(model.GameName));
                    SetActive("Pages/Players/Add", model.CanChangePlayers);
                    SetActive("Pages/Players/Remove", model.CanChangePlayers);
                    break;
                case FrontEndPage.Review:
                    RenderReview();
                    break;
            }
        }

        private void RenderTabs()
        {
            string active = page == FrontEndPage.Create ? "Create" : "Play";
            foreach (string tab in new[] { "Play", "Create", "Sandbox" })
            {
                Tint("TopBar/Tabs/" + tab + "/Label", tab == active ? activeTab : idleTab);
            }

            if (tabHighlight != null && buttons.TryGetValue("TopBar/Tabs/" + active, out Button tabButton) && tabButton != null)
            {
                RectTransform tabRect = (RectTransform)tabButton.transform;
                tabHighlight.position = new Vector3(tabRect.position.x, tabHighlight.position.y, tabHighlight.position.z);
            }
        }

        private void RenderProfile()
        {
            IReadOnlyList<ProfileRowModel> profiles = model.Profiles;
            for (int i = 0; i < profileRows.Length; i++)
            {
                ProfileRowParts row = profileRows[i];
                bool shown = i < profiles.Count;
                row.root.SetActive(shown);
                row.button.onClick.RemoveAllListeners();
                if (!shown) continue;
                ProfileRowModel profile = profiles[i];
                row.id = profile.Id;
                row.small.text = profile.IsActive ? "PLAYING NOW" : $"PLAYER {i + 1}";
                row.small.color = profile.IsActive ? activeTab : idleTab;
                row.name.text = Upper(profile.Name);
                row.plate.sprite = profile.IsActive ? selectedProfileEntry : profileEntry;
                row.edit.SetActive(profile.IsActive);
                Texture picture = profile.IsActive && model.AvatarPicture != null ? model.AvatarPicture : null;
                row.avatar.texture = picture;
                row.avatar.color = picture != null ? Color.white : activeTab;
                string id = profile.Id;
                if (profile.IsActive) row.button.onClick.AddListener(() => actions?.OpenProfile?.Invoke());
                else row.button.onClick.AddListener(() => actions?.PlayAs?.Invoke(id));
            }

            SetActive("Pages/Profile/Panel/Add", profiles.Count < ProfileRowCount);
            SetText("Pages/Profile/Avatar/Name", Upper(model.ProfileName));
            if (avatarPicture != null)
            {
                avatarPicture.texture = model.AvatarPicture != null ? model.AvatarPicture : fallbackAvatar;
                avatarPicture.color = avatarPicture.texture != null ? Color.white : Color.clear;
            }

            SetText("Pages/Profile/Avatar/AvatarName", model.AvatarName);
            SetActive("Pages/Profile/Avatar/Previous", model.CanChangeAvatar);
            SetActive("Pages/Profile/Avatar/Next", model.CanChangeAvatar);
            SetActive("Pages/Profile/Avatar/Customize", actions?.Customize != null);
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
                entry.id = game.Id;
                entry.name.text = Upper(game.Name);
                entry.detail.text = game.Detail;
                entry.picture.texture = game.Thumbnail;
                entry.picture.color = game.Thumbnail != null ? Color.white : new Color(0.024f, 0.937f, 0.941f, 1f);
                entry.plate.sprite = game.IsSelected ? selectedGameEntry : gameEntry;
                string id = game.Id;
                entry.button.onClick.AddListener(() => actions?.SelectGame?.Invoke(id));
            }

            SetText("Pages/Library/Detail/Name", Upper(model.GameName));
            SetText("Pages/Library/Detail/Text", model.Rules);
            SetText("Pages/Library/Detail/PlayersRow/Players", model.Detail);
            SetPicture(libraryPicture, model.Picture);
            SetText("Pages/Library/Select/Label", model.IsEmptyTable ? "START EMPTY TABLE" : "SELECT " + Upper(model.GameName));
            SetInteractable("Pages/Library/Select", model.CanStart || !model.IsEmptyTable);
        }

        private void RenderDetails()
        {
            SetText("Pages/Details/Title", Upper(model.GameName));
            SetText("Pages/Details/Tagline", Upper(model.Detail));
            SetText("Pages/Details/Text", model.Rules);
            SetText("Pages/Details/Chips/Players/Label", model.PlayersText.Length > 0 ? model.PlayersText : model.Detail);
            SetText("Pages/Details/Chips/Modes/Label", model.ShowMode ? model.ModeName : "Free play");
            SetText("Pages/Details/Box/Text", model.Deck);
            SetPicture(detailsPicture, model.Picture);
            SetInteractable("Pages/Details/Start", model.CanStart);
        }

        private void RenderSetup()
        {
            SetText("Pages/Setup/Title", Upper(model.GameName));
            Row("Pages/Setup/Options/Column/Mode", model.ShowMode, model.ModeName, model.CanChangeMode);
            Row("Pages/Setup/Options/Column/Players", model.ShowPlayers, model.PlayersText, model.CanChangePlayers);
            Row("Pages/Setup/Options/Column/WinMode", model.ShowWinMode, model.WinModeText, model.ShowWinMode);
            Row("Pages/Setup/Options/Column/Turns", model.ShowTurns, model.TurnsText, model.ShowTurns);
            Row("Pages/Setup/Options/Column/Deck", true, model.Deck, model.CanChangeDeck);
            SetText("Pages/Setup/Summary/Text", SettingsText());
            SetInteractable("Pages/Setup/Continue", model.CanStart);
        }

        private void RenderReview()
        {
            SetText("Pages/Review/Title", Upper(model.GameName));
            SetText("Pages/Review/Settings/Text", SettingsText());
            RenderPlayers(reviewPlayers);
            SetText("Pages/Review/Start/Label", "START GAME");
            SetInteractable("Pages/Review/Start", model.CanStart);
        }

        private string SettingsText()
        {
            string text = string.Empty;
            if (model.ShowMode) text += $"Mode   <b>{model.ModeName}</b>\n";
            if (model.ShowPlayers) text += $"Players   <b>{model.PlayersText}</b>\n";
            if (model.ShowWinMode) text += $"Win mode   <b>{model.WinModeText}</b>\n";
            if (model.ShowTurns) text += $"Turns   <b>{model.TurnsText}</b>\n";
            text += $"Deck   <b>{model.Deck}</b>";
            return text;
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
                if (row.controller != null) row.controller.text = row.dot != null ? "Controller: " + colourName : colourName;
                if (row.dot != null) row.dot.color = colour;
                if (row.avatar != null)
                {
                    Texture picture = i == 0 ? model.AvatarPicture : null;
                    row.avatar.texture = picture;
                    row.avatar.color = picture != null ? Color.white : colour;
                }
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

        // ---------- Parts ----------

        private void Resolve()
        {
            if (resolved) return;
            string[] names = Enum.GetNames(typeof(FrontEndPage));
            for (int i = 0; i < names.Length; i++)
            {
                pages[i] = Require("Pages/" + names[i]).gameObject;
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

            libraryContent = (RectTransform)Require("Pages/Library/List/Viewport/Content");
            gameEntryTemplate = Require("Pages/Library/List/Viewport/Content/Template").gameObject;
            gameEntryTemplate.SetActive(false);
            avatarPicture = Require("Pages/Profile/Avatar/Picture").GetComponent<RawImage>();
            libraryPicture = Require("Pages/Library/Detail/Picture").GetComponent<RawImage>();
            detailsPicture = Require("Pages/Details/Picture").GetComponent<RawImage>();
            tabHighlight = Require("TopBar/Tabs/Highlight") as RectTransform;
            for (int i = 0; i < ProfileRowCount; i++)
            {
                Transform row = Require("Pages/Profile/Panel/Rows/Row" + i);
                profileRows[i] = new ProfileRowParts
                {
                    root = row.gameObject,
                    button = row.GetComponent<Button>(),
                    plate = row.GetComponent<Image>(),
                    avatar = Require(row, "Avatar").GetComponent<RawImage>(),
                    small = Require(row, "Words/Small").GetComponent<Text>(),
                    name = Require(row, "Words/Name").GetComponent<Text>(),
                    edit = Require(row, "Edit").gameObject,
                };
            }

            for (int i = 0; i < PlayerRowCount; i++)
            {
                playerRows[i] = PlayerRow(Require("Pages/Players/Rows/Row" + i));
                reviewPlayers[i] = PlayerRow(Require("Pages/Review/Players/Tiles/Tile" + i));
            }

            resolved = true;
        }

        private PlayerRowParts PlayerRow(Transform row)
        {
            Transform badge = row.Find("Badge");
            Transform avatar = row.Find("Avatar");
            Transform name = row.Find("Words/Name") ?? row.Find("Name");
            Transform controller = row.Find("Words/Controller") ?? row.Find("Controller");
            Transform dot = row.Find("Words/Dot") ?? row.Find("Dot");
            return new PlayerRowParts
            {
                root = row.gameObject,
                badgePlate = badge != null ? badge.GetComponent<Image>() : null,
                badge = badge != null ? badge.GetComponentInChildren<Text>(true) : null,
                avatar = avatar != null ? avatar.GetComponent<RawImage>() : null,
                name = name != null ? name.GetComponent<Text>() : null,
                controller = controller != null ? controller.GetComponent<Text>() : null,
                dot = dot != null ? dot.GetComponent<Image>() : null,
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
                name = Require(t, "Words/Name").GetComponent<Text>(),
                detail = Require(t, "Words/Detail").GetComponent<Text>(),
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

        private static void SetPicture(RawImage image, Texture picture)
        {
            if (image == null) return;
            image.texture = picture;
            image.color = picture != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        }

        private static string Upper(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.ToUpperInvariant();
    }
}
