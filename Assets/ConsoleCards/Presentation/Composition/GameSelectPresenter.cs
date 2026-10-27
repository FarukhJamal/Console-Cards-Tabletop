using System;
using System.Collections.Generic;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The game selection panel: choosing a game and setting it up.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>GameSelectPanel</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class GameSelectPresenter
    {
        private readonly TabletopComposition host;

        internal GameSelectPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.GameSelect.cs ----------

        internal const string EmptyTableEntryId = "empty-table";

        internal GameSelectActions gameSelectActions;
        internal string gameSelectEntryId = string.Empty;
        internal int gameSelectModeIndex;
        // PLACE-1: how many players the chosen game is dealt for (0: the game's current count).
        internal int gameSelectPlayerCount;
        // An editor or panel opened from the menu goes back to it when closed.
        internal bool returnToGameSelect;
        // L2-5: the deck an Empty Table starts with (0: none), and the cards waiting to be placed after Start.
        internal int emptyTableDeckIndex;
        internal string pendingDeckName = string.Empty;
        internal List<ObjectDefinitionId> pendingDeckCards;

        // UI-2: the front end screen to open on next (null: the one shown). Kept across the scene reload a profile
        // switch makes, so the front end comes back on the Player profile screen.
        internal static FrontEndPage? pendingFrontEndPage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetFrontEndPage() => pendingFrontEndPage = null;

        /// <summary>UI-2: the front end at start (the main menu, or Player profile after a profile switch).</summary>
        internal bool OpenFrontEnd(FrontEndPage page)
        {
            if (page == FrontEndPage.Home && host.HasOnline && host.Online.InRoom) page = FrontEndPage.Room; // MP-2: back to the room
            if (!pendingFrontEndPage.HasValue) pendingFrontEndPage = page;
            return OpenGameSelect();
        }

        internal bool OpenGameSelect()
        {
            if (!pendingFrontEndPage.HasValue) pendingFrontEndPage = FrontEndPage.Library; // New table… from the game
            host.EnsureContentArt();
            host.TrapFloorGame.PendingWinMode = null; // MODE-1: a choice from a menu left without dealing is dropped
            host.TrapFloorGame.PendingAnticlockwise = null;
            List<GameSelectEntry> entries = GameSelectEntries();
            if (FindGameSelectEntry(entries, gameSelectEntryId) == null)
            {
                // Start on the game on the table (or Trap Floor from an Empty Table).
                bool onEmptyTable = host.PlayerGamesPanel.activePlayerGame == null
                    && (host.activeSession == null || host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom);
                gameSelectEntryId = onEmptyTable && entries.Count > 1 ? entries[1].Id : CurrentGameEntryId(entries);
                gameSelectModeIndex = CurrentModeIndex(FindGameSelectEntry(entries, gameSelectEntryId));
            }

            return ShowGameSelect(string.Empty, entries);
        }

        internal bool ShowGameSelect(string status, List<GameSelectEntry> entries = null)
        {
            if (host.runtimeUi == null) return false;
            entries = entries ?? GameSelectEntries();
            if (gameSelectActions == null)
            {
                gameSelectActions = new GameSelectActions
                {
                    SelectGame = SelectGameEntry,
                    CycleMode = CycleGameMode,
                    CyclePlayers = CycleGamePlayers,
                    CycleWinMode = CycleGameWinMode,
                    CycleTurns = CycleGameTurns,
                    CycleDeck = CycleGameDeck,
                    OpenProfile = () => OpenFromGameSelect(host.ProfilePanel.OpenProfilePanel),
                    OpenCardArt = () => OpenFromGameSelect(host.OpenCardArtPanel),
                    OpenCardEditor = OpenCardEditorFromGameSelect,
                    OpenDeckEditor = OpenDeckEditorFromGameSelect,
                    Start = StartWithChosenColours,
                    Delete = host.PlayerGamesPanel.DeleteSelectedPlayerGame,
                    Close = CloseGameSelect,
                    PlayAs = PlayAsFromFrontEnd,
                    CycleAvatar = CycleFrontEndAvatar,
                    Sandbox = StartSandbox,
                    Exit = ExitGame,
                    CycleSeatColour = CycleFrontEndColour,
                    OpenTableTools = OpenTableToolsFromFrontEnd,
                    OpenOnline = () => host.Online.OpenLobby(),
                    CreateRoom = (title, isPrivate) => host.Online.CreateRoom(title, isPrivate),
                    JoinRoom = code => host.Online.JoinRoom(code),
                    LeaveRoom = () => host.Online.LeaveRoom(),
                    ToggleReady = () => host.Online.ToggleReady(),
                    CycleOnlineColour = step => host.Online.CycleColour(step),
                    CycleOnlineSeat = step => host.Online.CycleSeat(step),
                };
            }

            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId) ?? entries[0];
            gameSelectEntryId = entry.Id;
            if (gameSelectModeIndex >= entry.ModeNames.Count) gameSelectModeIndex = 0;
            List<CardEditorRowModel> rows = new List<CardEditorRowModel>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                rows.Add(new CardEditorRowModel(entries[i].Id, entries[i].Name, GameDetail(entries[i]),
                    GamePicture(entries[i].Game), entries[i].Id == entry.Id));
            }

            if (entry.PlayerGame != null)
            {
                GameSelectModel mine = host.PlayerGamesPanel.PlayerGameSelectModel(entry, rows, status);
                AddFrontEnd(mine, entry);
                return host.runtimeUi.ShowGameSelect(mine, gameSelectActions);
            }

            bool isEmpty = entry.Game == null;
            bool playable = isEmpty || entry.Templates.Count > 0;
            bool showPlayers = PlayerRange(entry, out int fewestPlayers, out int mostPlayers);
            GameSelectModel model = new GameSelectModel
            {
                Games = rows,
                Picture = GamePicture(entry.Game),
                GameName = entry.Name,
                Detail = GameDetail(entry),
                ShowMode = entry.ModeNames.Count > 0,
                ModeName = entry.ModeNames.Count > 0 ? entry.ModeNames[gameSelectModeIndex] : string.Empty,
                CanChangeMode = entry.ModeNames.Count > 1,
                ShowPlayers = showPlayers,
                PlayersText = PlayersLineText(ChosenPlayerCount(entry)),
                CanChangePlayers = mostPlayers > fewestPlayers,
                ShowWinMode = IsTrapFloorEntry(entry),
                WinModeText = IsTrapFloorEntry(entry) ? TrapFloorCartridge.WinModeName(ChosenWinMode()) : string.Empty,
                ShowTurns = IsTrapFloorEntry(entry) && ChosenPlayerCount(entry) > 2,
                TurnsText = TrapFloorCartridge.TurnsName(ChosenAnticlockwise()),
                Rules = isEmpty ? "No rules: free play with the platform's components." : GameRulesText(entry),
                Deck = DeckLineText(entry, out bool canChangeDeck),
                CanChangeDeck = canChangeDeck,
                ProfileName = host.EnsureProfile().Active.displayName,
                CanOpenCardArt = host.UsesTableArt(entry.Game),
                CanEditCards = !isEmpty,
                CanEditDecks = !isEmpty,
                CanStart = playable,
                StartText = isEmpty ? "Start an Empty Table" : $"Start {entry.Name}",
                Status = !string.IsNullOrEmpty(status)
                    ? status
                    : playable
                        ? "Choose a game and a mode, make it yours if you like, then Start."
                        : $"{entry.Name} cannot be played yet; you can already make its cards and decks yours.",
            };
            AddFrontEnd(model, entry);
            if (!host.runtimeUi.ShowGameSelect(model, gameSelectActions)) return false;
            return true;
        }

        // UI-2: what the front end shows besides the New table… choices: the profiles and avatar, and the players.
        private void AddFrontEnd(GameSelectModel model, GameSelectEntry entry)
        {
            model.OpenAt = pendingFrontEndPage;
            pendingFrontEndPage = null;
            model.IsEmptyTable = entry.Game == null && entry.PlayerGame == null;
            ProfileService service = host.EnsureProfile();
            List<ProfileInfo> saved = service.List();
            List<ProfileRowModel> profiles = new List<ProfileRowModel>(saved.Count);
            for (int i = 0; i < saved.Count; i++)
            {
                bool isActive = string.Equals(saved[i].id, service.Active.id, StringComparison.OrdinalIgnoreCase);
                ProfileRowModel row = new ProfileRowModel(saved[i].id, saved[i].displayName, string.Empty, isActive);
                if (isActive) profiles.Insert(0, row);
                else profiles.Add(row);
            }

            model.Profiles = profiles;
            PersonaSet personas = host.PersonaSetForProfile;
            if (personas != null && personas.AvatarCount > 0)
            {
                model.AvatarPicture = personas.Avatar(service.Active.avatar);
                model.AvatarName = personas.AvatarName(service.Active.avatar);
                model.CanChangeAvatar = !personas.CharacterCustomizer && personas.AvatarCount > 1;
                model.AvatarCount = personas.AvatarCount;
                model.AvatarIndex = PersonaSet.Wrap(service.Active.avatar, personas.AvatarCount);
            }

            gameSelectActions.Customize = personas != null && personas.CharacterCustomizer
                ? () => OpenFromGameSelect(host.PersonaPanel.Open)
                : (Action)null;
            int count = model.IsEmptyTable ? 0 : Mathf.Clamp(ChosenPlayerCount(entry), 0, FrontEndView.PlayerRowCount);
            string[] names = new string[count];
            Color[] colors = new Color[count];
            string[] colorNames = new string[count];
            int[] order = FrontEndColours();
            for (int i = 0; i < count; i++)
            {
                names[i] = i == 0 ? service.Active.displayName : $"Player {i + 1}";
                colors[i] = ColourByIndex(i < order.Length ? order[i] : i);
                colorNames[i] = ColourNameByIndex(i < order.Length ? order[i] : i);
            }

            // MP-2: in an online room the seats are the room's players, in their seat order and chosen colours.
            bool inRoom = host.HasOnline && host.Online.InRoom;
            if (inRoom) host.Online.FillSeats(names, colors, colorNames);
            model.PlayerCount = count;
            model.PlayerNames = names;
            model.PlayerColors = colors;
            model.PlayerColorNames = colorNames;
            model.CanChangeColours = count > 0 && order.Length > 1 && !inRoom;
            model.Online = host.Online.BuildScreenModel();
            if (inRoom)
            {
                model.CanChangePlayers = false; // everyone in the room plays (more than the game's seats watch)
                if (!host.Online.IsHosting)
                {
                    model.CanStart = false;
                    model.Status = "The host chooses the game and starts it for everyone.";
                }
                else if (!model.Online.AllReady)
                {
                    model.CanStart = false;
                    model.Status = "Waiting for everyone in the room to press READY.";
                }
                else if (entry.PlayerGame != null)
                {
                    model.CanStart = false;
                    model.Status = "Games you made cannot be played online yet: choose another game.";
                }
            }
            AddGameDetails(model, entry.Game);
            AddDeckSet(model, entry);
        }

        // Owner, 2026-10-26: Deck Set in one word. The game's own deck is Standard (None on an Empty Table), a deck of
        // yours is Custom (Custom 2, Custom 3 ... when there are several); the deck's name and size go in the detail.
        private void AddDeckSet(GameSelectModel model, GameSelectEntry entry)
        {
            if (entry.PlayerGame != null)
            {
                model.DeckName = "Custom";
                model.DeckDetail = DeckDetail(model.Deck);
                return;
            }

            List<DeckChoice> decks = DeckChoices(entry);
            int index = ChosenDeckIndex(entry, decks);
            DeckChoice choice = decks[index];
            if (index == 0)
            {
                model.DeckName = entry.Game == null ? "None" : "Standard";
                model.DeckDetail = entry.Game == null ? "No deck; bring components from the Toolbox." : DeckDetail(model.Deck);
                return;
            }

            model.DeckName = decks.Count > 2 ? $"Custom {index}" : "Custom";
            model.DeckDetail = $"{choice.Name}, {choice.Cards} card{(choice.Cards == 1 ? string.Empty : "s")}";
        }

        // "Deck: Trap Floor deck, 37 cards · 2 decks of yours" -> "Trap Floor deck, 37 cards".
        private static string DeckDetail(string line)
        {
            string detail = line ?? string.Empty;
            if (detail.StartsWith("Deck: ", StringComparison.Ordinal)) detail = detail.Substring(6);
            int dot = detail.IndexOf(" · ", StringComparison.Ordinal);
            return dot > 0 ? detail.Substring(0, dot) : detail;
        }

        // Owner, 2026-10-25: Game details as in the reference: IN THE BOX as a list, a strip of pictures and three
        // feature tiles, all counted from the game's definition (a game the player made keeps its deck line).
        private void AddGameDetails(GameSelectModel model, GameDefinition game)
        {
            if (game == null) return;
            int tiles = 0;
            int actions = 0;
            int keys = 0;
            List<FrontEndBoxItem> box = new List<FrontEndBoxItem>(6);
            List<FrontEndBoxItem> others = new List<FrontEndBoxItem>(2);
            HashSet<string> counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> keyCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Texture> gallery = new List<Texture>(6);
            Texture2D cover = GamePicture(game);
            if (cover != null) gallery.Add(cover);
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                if (set == null) continue;
                bool floor = string.Equals(set.Role, "floor", StringComparison.OrdinalIgnoreCase);
                int setCards = 0;
                int other = 0;
                bool pictured = false;
                for (int c = 0; c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    if (card == null) continue;
                    if (string.Equals(card.Category, "Key", StringComparison.OrdinalIgnoreCase)) keyCards.Add(card.StableId);
                    if (!pictured && gallery.Count < 6 && card.RepresentedControllerInput == null)
                    {
                        Texture2D art = host.UsesTableArt(game) ? host.FrontArt(card) : card.FrontArtwork;
                        if (art != null && !gallery.Contains(art)) gallery.Add(art);
                        pictured = true;
                    }

                    if (floor) setCards += card.Quantity;
                    else if (!counted.Add(card.StableId)) continue;
                    else if (card.RepresentedControllerInput != null) actions += card.Quantity;
                    else other += card.Quantity;
                }

                if (floor) tiles += set.DrawCount > 0 ? set.DrawCount : setCards;
                if (other > 0) others.Add(new FrontEndBoxItem("Abilities", SetLabel(set.DisplayName, game.DisplayName), other));
            }

            keys = keyCards.Count;
            if (tiles > 0) box.Add(new FrontEndBoxItem("Tiles", "Floor Tiles", tiles));
            if (actions > 0) box.Add(new FrontEndBoxItem("Cards", "Action Cards", actions));
            box.AddRange(others);
            if (keys > 0) box.Add(new FrontEndBoxItem("Keys", "Keys", keys));
            box.Add(new FrontEndBoxItem("Pawns", "Player Pawns", game.MaximumPlayers));
            if (game.ConsoleConfiguration != null) box.Add(new FrontEndBoxItem("Console", "Consoles", game.MaximumPlayers));
            if (box.Count > FrontEndView.BoxRowCount) box.RemoveRange(FrontEndView.BoxRowCount, box.Count - FrontEndView.BoxRowCount);

            List<FrontEndFeature> features = new List<FrontEndFeature>(3);
            if (tiles > 0) features.Add(new FrontEndFeature("Tiles", "Dynamic Tiles", "Every move can change the floor."));
            if (actions > 0) features.Add(new FrontEndFeature("Cards", "Action Cards", "Pay costs with Controller cards."));
            if (keys > 0) features.Add(new FrontEndFeature("Keys", "Collect Keys", "Find the keys and reach the exit."));
            if (features.Count < 3 && others.Count > 0)
            {
                features.Add(new FrontEndFeature("Abilities", others[0].Label, "Each avatar brings its own powers."));
            }

            model.BoxItems = box;
            model.Gallery = gallery;
            model.Features = features;
        }

        // "Trap Floor Abilities" -> "Abilities" (the game's name is already the title).
        private static string SetLabel(string setName, string gameName)
        {
            string label = setName ?? string.Empty;
            if (!string.IsNullOrEmpty(gameName) && label.StartsWith(gameName, StringComparison.OrdinalIgnoreCase))
            {
                label = label.Substring(gameName.Length).Trim();
            }

            return label.Length > 0 ? label : "Cards";
        }

        // Owner, 2026-10-25: the pawn arrows on the Players step pick each seat's colour. Taking a colour another seat has
        // swaps the two, so colours stay different; the table uses them from the next Start.
        private int[] frontEndColours;

        private int[] FrontEndColours()
        {
            if (frontEndColours == null || frontEndColours.Length != SeatColourCount)
            {
                frontEndColours = new int[SeatColourCount];
                for (int i = 0; i < frontEndColours.Length; i++) frontEndColours[i] = i;
            }

            return frontEndColours;
        }

        private void CycleFrontEndColour(int seat, int step)
        {
            int[] order = FrontEndColours();
            if (seat < 0 || seat >= order.Length || order.Length < 2) return;
            int next = ((order[seat] + step) % order.Length + order.Length) % order.Length;
            int other = Array.IndexOf(order, next);
            if (other >= 0) order[other] = order[seat];
            order[seat] = next;
            ShowGameSelect(string.Empty);
        }

        private void StartWithChosenColours()
        {
            SetSeatColourOrder(FrontEndColours());
            StartSelectedGame();
        }

        // UI-2: the avatar arrows on the Player profile screen (saved in the profile at once).
        private void CycleFrontEndAvatar(int step)
        {
            PersonaSet personas = host.PersonaSetForProfile;
            if (personas == null || personas.AvatarCount == 0) return;
            ProfileService service = host.EnsureProfile();
            int next = PersonaSet.Wrap(service.Active.avatar + step, personas.AvatarCount);
            ShowGameSelect(service.SaveAvatar(next, out string error)
                ? $"Your avatar is now {personas.AvatarName(next)}."
                : $"Could not save your avatar: {error}.");
        }

        // UI-2: SANDBOX starts an Empty Table.
        private void StartSandbox()
        {
            if (host.HasOnline && host.Online.InRoom)
            {
                // MP-2 (found testing, 2026-10-27): SANDBOX in a room started an Empty Table for everyone at once.
                host.Online.ShowRoomNotice(host.Online.IsHosting
                    ? "Sandbox is solo: LEAVE first, or pick Empty Table in CHOOSE GAME."
                    : "Sandbox is solo: LEAVE the room first.");
                return;
            }

            gameSelectEntryId = EmptyTableEntryId;
            StartSelectedGame();
        }

        // UI-2: playing as another profile reloads the table; the front end comes back on Player profile.
        private void PlayAsFromFrontEnd(string profileId)
        {
            pendingFrontEndPage = FrontEndPage.Profile;
            host.runtimeUi?.CloseGameSelect();
            host.ProfilePanel.PlayAsProfile(profileId);
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        // ---------- the games ----------

        internal List<GameSelectEntry> GameSelectEntries()
        {
            List<GameSelectEntry> entries = new List<GameSelectEntry>
            {
                new GameSelectEntry { Id = EmptyTableEntryId, Name = "Empty Table" },
            };
            host.CardEditorPanel.CollectEditorGames();
            for (int g = 0; g < host.CardEditorPanel.editorGames.Count; g++)
            {
                GameDefinition game = host.CardEditorPanel.editorGames[g];
                GameSelectEntry entry = new GameSelectEntry
                {
                    Id = game.StableId ?? game.name,
                    Name = string.IsNullOrWhiteSpace(game.DisplayName) ? game.name : game.DisplayName,
                    Game = game,
                };
                // CART-1b: a game with code lists its playable templates, one per mode ("Trap Floor — Easy").
                host.Cartridges.CodeCartridgeFor(game)?.ListTemplates(entry.Templates, entry.ModeNames);

                entries.Add(entry);
            }

            // Doc 29: the games you made, after the platform's games.
            List<PlayerGameFile> mine = host.PlayerGamesPanel.PlayerGames().List();
            for (int i = 0; i < mine.Count; i++)
            {
                entries.Add(new GameSelectEntry
                {
                    Id = PlayerGameEntryPrefix + mine[i].id,
                    Name = string.IsNullOrWhiteSpace(mine[i].name) ? "My game" : mine[i].name,
                    PlayerGame = mine[i],
                });
            }

            return entries;
        }

        internal static GameSelectEntry FindGameSelectEntry(List<GameSelectEntry> entries, string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, StringComparison.OrdinalIgnoreCase)) return entries[i];
            }

            return null;
        }

        internal string CurrentGameEntryId(List<GameSelectEntry> entries)
        {
            if (host.PlayerGamesPanel.activePlayerGame != null && host.activeSession != null && host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return PlayerGameEntryPrefix + host.PlayerGamesPanel.activePlayerGame.id;
            }

            if (host.activeSession == null || host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom) return EmptyTableEntryId;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Templates.Contains(host.activeSession.Selection.GameTemplateId)) return entries[i].Id;
            }

            return EmptyTableEntryId;
        }

        internal int CurrentModeIndex(GameSelectEntry entry)
        {
            if (entry == null || host.activeSession == null || host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom) return 0;
            int index = entry.Templates.IndexOf(host.activeSession.Selection.GameTemplateId);
            return index < 0 ? 0 : index;
        }

        internal string GameDetail(GameSelectEntry entry)
        {
            if (entry.PlayerGame != null) return $"Your game · {PlayerGamesPresenter.PlayersText(entry.PlayerGame.minimumPlayers, entry.PlayerGame.maximumPlayers)}";
            if (entry.Game == null) return "Free play · any number of players";
            int min = entry.Game.MinimumPlayers;
            int max = entry.Game.MaximumPlayers;
            string players = min == max ? $"{min} player{(min == 1 ? string.Empty : "s")}" : $"{min}–{max} players";
            string modes = entry.ModeNames.Count > 0
                ? $"{entry.ModeNames.Count} mode{(entry.ModeNames.Count == 1 ? string.Empty : "s")}"
                : "not playable yet";
            return $"{players} · {modes}";
        }

        internal string GameRulesText(GameSelectEntry entry)
        {
            if (entry.Game != null && ReferenceEquals(entry.Game, host.TableGame))
            {
                host.RulesCardPanel.EnsureHouseRulesLoaded();
                RuleSetDefinition rules = host.RulesCardPanel.ActiveRuleSet;
                string name = rules != null ? rules.DisplayName : "Default";
                return $"Rules: {name} (change them on the rules card, with everyone's agreement)";
            }

            RuleSetDefinition defaults = entry.Game.DefaultRuleSet;
            return defaults != null ? $"Rules: {defaults.DisplayName}" : "Rules: from the game box";
        }

        // The game's deck as it will be played: its cards with your copies and your own cards (doc 24, L2-2).
        internal string GameDeckText(GameSelectEntry entry)
        {
            ContentArtServiceScope scope = ArtFor(entry.Game);
            try
            {
                PlayerCardsFile cards = scope.Art.Store.LoadCards(scope.Art.GameId);
                PlayerDecksFile decks = scope.Art.Store.LoadDecks(scope.Art.GameId);
                int total = 0;
                int yours = 0;
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int s = 0; s < entry.Game.ContentSets.Count; s++)
                {
                    GameContentSet set = entry.Game.ContentSets[s];
                    for (int c = 0; set != null && c < set.Cards.Count; c++)
                    {
                        CardDefinition card = set.Cards[c];
                        if (card == null || card.RepresentedControllerInput != null || !seen.Add(card.StableId)) continue;
                        CardChangeRecord change = null;
                        for (int i = 0; i < cards.changes.Count; i++)
                        {
                            if (cards.changes[i] != null && string.Equals(cards.changes[i].cardId, card.StableId, StringComparison.OrdinalIgnoreCase)) change = cards.changes[i];
                        }

                        total += change != null && change.changeQuantity ? change.quantity : card.Quantity;
                    }
                }

                for (int i = 0; i < cards.cards.Count; i++)
                {
                    if (cards.cards[i] == null) continue;
                    total += Math.Max(0, cards.cards[i].quantity);
                    yours += Math.Max(0, cards.cards[i].quantity);
                }

                string own = yours > 0 ? $", {yours} of yours" : string.Empty;
                string myDecks = decks.decks.Count > 0
                    ? $" · {decks.decks.Count} deck{(decks.decks.Count == 1 ? string.Empty : "s")} of yours"
                    : string.Empty;
                return $"Deck: {entry.Name} deck, {total} card{(total == 1 ? string.Empty : "s")}{own}{myDecks}";
            }
            finally
            {
                scope.Dispose();
            }
        }

        // A picture for the game: its first card's front (the game box has no picture of its own yet).
        internal Texture2D GamePicture(GameDefinition game)
        {
            if (game == null) return null;
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    if (card == null || card.RepresentedControllerInput != null) continue;
                    return host.UsesTableArt(game)
                        ? host.FrontArt(card)
                        : card.FrontArtwork;
                }
            }

            return null;
        }

        internal ContentArtServiceScope ArtFor(GameDefinition game)
        {
            if (host.UsesTableArt(game)) return new ContentArtServiceScope(host.contentArt, false);
            ContentArtService art = new ContentArtService(ContentPackStore.ForProfile(host.EnsureProfile()));
            art.UseGame(game);
            return new ContentArtServiceScope(art, true);
        }

        // ---------- actions ----------

        internal void SelectGameEntry(string entryId)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, entryId);
            if (entry == null) return;
            host.PlayerGamesPanel.deleteArmedGameId = string.Empty;
            gameSelectEntryId = entry.Id;
            gameSelectModeIndex = CurrentGameEntryId(entries) == entry.Id ? CurrentModeIndex(entry) : 0;
            gameSelectPlayerCount = 0;
            ShowGameSelect(string.Empty, entries);
        }

        // ---------- how many players (PLACE-1) ----------

        // The players a game can be dealt for (false: the game does not ask, for example the Empty Table).
        internal bool PlayerRange(GameSelectEntry entry, out int fewest, out int most)
        {
            if (entry != null && entry.PlayerGame != null)
            {
                // PLACE-2: a game you made, from its saved players (each saved Console is a seat).
                PlayerGameFile game = entry.PlayerGame;
                fewest = Mathf.Clamp(game.minimumPlayers, 1, PlayerGameFile.MaximumPlayers);
                most = Mathf.Clamp(game.maximumPlayers, fewest, PlayerGameFile.MaximumPlayers);
                return true;
            }

            IGameCartridge cartridge = entry != null && entry.PlayerGame == null ? host.Cartridges.CodeCartridgeFor(entry.Game) : null;
            fewest = cartridge != null ? cartridge.MinimumDealPlayers : 0;
            most = cartridge != null ? cartridge.MaximumDealPlayers : 0;
            return fewest > 0 && most >= fewest && entry.Templates.Count > 0;
        }

        // The count chosen in the menu, else the game's current count (the one its table was last dealt for).
        internal int ChosenPlayerCount(GameSelectEntry entry)
        {
            if (!PlayerRange(entry, out int fewest, out int most)) return 0;
            if (host.HasOnline && host.Online.InRoom) return Mathf.Clamp(host.Online.MemberCount, fewest, most); // MP-2
            int count = gameSelectPlayerCount > 0
                ? gameSelectPlayerCount
                : entry.PlayerGame != null
                    ? SavedConsoleCount(entry.PlayerGame, most)
                    : host.Cartridges.CodeCartridgeFor(entry.Game).DealPlayerCount;
            return Mathf.Clamp(count, fewest, most);
        }

        // A game you made starts on as many players as it has Consoles (its maximum when it has none).
        private static int SavedConsoleCount(PlayerGameFile game, int most)
        {
            int consoles = 0;
            List<SavedConsole> saved = game.table != null ? game.table.consoles : null;
            for (int i = 0; saved != null && i < saved.Count; i++)
            {
                if (saved[i] != null) consoles++;
            }

            return consoles > 0 ? consoles : most;
        }

        internal static string PlayersLineText(int count) =>
            count <= 0 ? string.Empty : $"{count} player{(count == 1 ? string.Empty : "s")}";

        internal void CycleGamePlayers(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId);
            if (!PlayerRange(entry, out int fewest, out int most) || most <= fewest) return;
            int span = most - fewest + 1;
            int index = ChosenPlayerCount(entry) - fewest;
            gameSelectPlayerCount = fewest + (((index + step) % span) + span) % span;
            ShowGameSelect(string.Empty, entries);
        }

        // ---------- the win mode (MODE-1) and the turn direction (DIR-1), owner 2026-10-22 ----------

        private int gameSelectWinMode = -1;
        private int gameSelectTurns = -1;

        private bool IsTrapFloorEntry(GameSelectEntry entry) =>
            entry != null
            && entry.PlayerGame == null
            && entry.Game != null
            && entry.Templates.Count > 0
            && host.Cartridges.CodeCartridgeFor(entry.Game) is TrapFloorCartridge;

        private TrapFloorWinMode ChosenWinMode() =>
            gameSelectWinMode >= 0 ? (TrapFloorWinMode)gameSelectWinMode : host.TrapFloorGame.WinMode;

        private bool ChosenAnticlockwise() =>
            gameSelectTurns >= 0 ? gameSelectTurns == 1 : host.TrapFloorGame.Anticlockwise;

        internal void CycleGameWinMode(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            if (!IsTrapFloorEntry(FindGameSelectEntry(entries, gameSelectEntryId))) return;
            gameSelectWinMode = ((((int)ChosenWinMode() + step) % 3) + 3) % 3;
            ShowGameSelect(TrapFloorCartridge.WinModeHint(ChosenWinMode()), entries);
        }

        internal void CycleGameTurns(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            if (!IsTrapFloorEntry(FindGameSelectEntry(entries, gameSelectEntryId))) return;
            gameSelectTurns = ChosenAnticlockwise() ? 0 : 1;
            ShowGameSelect(string.Empty, entries);
        }

        internal void CycleGameMode(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId);
            if (entry == null || entry.ModeNames.Count == 0) return;
            gameSelectModeIndex = ((gameSelectModeIndex + step) % entry.ModeNames.Count + entry.ModeNames.Count) % entry.ModeNames.Count;
            ShowGameSelect(string.Empty, entries);
        }

        internal void StartSelectedGame()
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId);
            if (entry == null) return;
            bool online = host.HasOnline && host.Online.InRoom; // MP-2: the host starts the room's game for everyone
            if (online && !host.Online.IsHosting)
            {
                ShowGameSelect("The host chooses the game and starts it for everyone.", entries);
                return;
            }

            if (entry.PlayerGame != null)
            {
                // MP-4: a game you made is played online too: the room gets your content and your table.
                if (online && !host.Online.PrepareHostedStart(ChosenPlayerCount(entry), entry.Name, out string notReady))
                {
                    ShowGameSelect(notReady, entries);
                    return;
                }

                // Doc 29: a game you made starts as an Empty Table, then its table is laid out.
                pendingDeckCards = null;
                pendingPlayerGame = entry.PlayerGame;
                pendingPlayerGameCount = ChosenPlayerCount(entry); // PLACE-2
                returnToGameSelect = false;
                host.runtimeUi?.CloseGameSelect();
                host.SessionService.RequestTableReplacement(TabletopSessionSelection.EmptyCustom);
                return;
            }

            if (entry.Game != null && entry.Templates.Count == 0)
            {
                ShowGameSelect($"{entry.Name} cannot be played yet.", entries);
                return;
            }

            GameTemplateId templateId = entry.Game == null
                ? GameTemplateId.Empty
                : entry.Templates[Mathf.Clamp(gameSelectModeIndex, 0, entry.Templates.Count - 1)];
            pendingDeckCards = null;
            pendingDeckName = string.Empty;
            if (entry.Game == null)
            {
                List<DeckChoice> choices = DeckChoices(entry);
                DeckChoice choice = choices[Mathf.Clamp(emptyTableDeckIndex, 0, choices.Count - 1)];
                if (choice.Deck != null)
                {
                    pendingDeckCards = DeckCardIds(choice.Deck);
                    pendingDeckName = choice.Name;
                    if (pendingDeckCards.Count == 0)
                    {
                        pendingDeckCards = null;
                        ShowGameSelect($"{choice.Name} has no cards yet; add some in the Deck editor.", entries);
                        return;
                    }
                }
            }
            else if (host.Cartridges.CodeCartridgeFor(entry.Game) != null && !string.IsNullOrEmpty(ChosenDeckIdOf(entry.Game)))
            {
                // A deck of yours must be able to set up the board (for example enough Traps for the floor).
                string problem = host.Cartridges.CodeCartridgeFor(entry.Game).TrialChosenDeck(templateId);
                if (problem != null)
                {
                    ShowGameSelect($"This deck cannot set up {entry.Name}: {problem} Choose another deck, or add cards to it.", entries);
                    return;
                }
            }

            if (IsTrapFloorEntry(entry))
            {
                // MODE-1 / DIR-1: the choices apply when the new table is dealt (Cancel on "replace the table?"
                // keeps the game in play as it is).
                host.TrapFloorGame.PendingWinMode = ChosenWinMode();
                host.TrapFloorGame.PendingAnticlockwise = ChosenAnticlockwise();
                gameSelectWinMode = -1;
                gameSelectTurns = -1;
            }

            if (online)
            {
                int seats = entry.Game == null ? host.Online.MemberCount : ChosenPlayerCount(entry);
                if (!host.Online.PrepareHostedStart(seats, entry.Name, out string problem))
                {
                    ShowGameSelect(problem, entries);
                    return;
                }
            }

            returnToGameSelect = false;
            host.runtimeUi?.CloseGameSelect();
            host.SessionService.RequestTableReplacement(
                entry.Game == null
                    ? TabletopSessionSelection.EmptyCustom
                    : TabletopSessionSelection.FromGameTemplate(templateId),
                ChosenPlayerCount(entry));
        }

        // ---------- the deck to play with (L2-5) ----------

        internal List<DeckChoice> DeckChoices(GameSelectEntry entry)
        {
            List<DeckChoice> choices = new List<DeckChoice>();
            if (entry.Game == null)
            {
                choices.Add(new DeckChoice { Name = "No deck" });
                host.CardEditorPanel.CollectEditorGames();
                for (int g = 0; g < host.CardEditorPanel.editorGames.Count; g++) AddDeckChoices(choices, host.CardEditorPanel.editorGames[g], true);
                return choices;
            }

            choices.Add(new DeckChoice { Name = $"{entry.Name} deck" });
            AddDeckChoices(choices, entry.Game, false);
            return choices;
        }

        internal void AddDeckChoices(List<DeckChoice> choices, GameDefinition game, bool withGameName)
        {
            ContentArtServiceScope scope = ArtFor(game);
            try
            {
                PlayerDecksFile decks = scope.Art.Store.LoadDecks(scope.Art.GameId);
                for (int d = 0; d < decks.decks.Count; d++)
                {
                    PlayerDeckRecord deck = decks.decks[d];
                    if (deck == null || string.IsNullOrWhiteSpace(deck.id)) continue;
                    int cards = 0;
                    for (int i = 0; i < deck.cards.Count; i++) cards += deck.cards[i] != null ? Math.Max(0, deck.cards[i].count) : 0;
                    string name = string.IsNullOrWhiteSpace(deck.name) ? "My deck" : deck.name;
                    choices.Add(new DeckChoice
                    {
                        Id = deck.id,
                        Name = withGameName ? $"{name} ({game.DisplayName})" : name,
                        Cards = cards,
                        Deck = deck,
                        Game = game,
                    });
                }
            }
            finally
            {
                scope.Dispose();
            }
        }

        internal int ChosenDeckIndex(GameSelectEntry entry, List<DeckChoice> choices)
        {
            if (entry.Game == null) return Mathf.Clamp(emptyTableDeckIndex, 0, choices.Count - 1);
            string chosen = PlayDeckIdOf(entry.Game);
            for (int i = 1; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Id, chosen, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return 0;
        }

        internal string DeckLineText(GameSelectEntry entry, out bool canChange)
        {
            List<DeckChoice> choices = DeckChoices(entry);
            canChange = choices.Count > 1;
            DeckChoice choice = choices[ChosenDeckIndex(entry, choices)];
            if (entry.Game == null)
            {
                return choice.Deck == null
                    ? canChange ? "Deck: none (use < > to start with a deck of yours)" : "Deck: none; bring components from the Toolbox"
                    : $"Deck: {choice.Name}, {choice.Cards} card{(choice.Cards == 1 ? string.Empty : "s")}; you place it after Start";
            }

            return choice.Deck == null
                ? GameDeckText(entry)
                : $"Deck: {choice.Name}, {choice.Cards} card{(choice.Cards == 1 ? string.Empty : "s")} (your deck)";
        }

        internal void CycleGameDeck(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId);
            if (entry == null || entry.PlayerGame != null) return;
            List<DeckChoice> choices = DeckChoices(entry);
            if (choices.Count < 2) return;
            int index = ((ChosenDeckIndex(entry, choices) + step) % choices.Count + choices.Count) % choices.Count;
            if (entry.Game == null)
            {
                emptyTableDeckIndex = index;
            }
            else
            {
                SetPlayDeckId(entry.Game, choices[index].Id);
            }

            ShowGameSelect(index == 0 && entry.Game != null
                ? $"Playing with {entry.Name}'s own deck."
                : $"Playing with {choices[index].Name}.", entries);
        }

        // The game's deck choice is kept in the profile (games/<game>/settings.json).
        internal string PlayDeckIdOf(GameDefinition game)
        {
            ContentArtServiceScope scope = ArtFor(game);
            try
            {
                return scope.Art.Store.LoadSettings(scope.Art.GameId).playDeckId ?? string.Empty;
            }
            finally
            {
                scope.Dispose();
            }
        }

        internal void SetPlayDeckId(GameDefinition game, string deckId)
        {
            ContentArtServiceScope scope = ArtFor(game);
            try
            {
                ContentSettings settings = scope.Art.Store.LoadSettings(scope.Art.GameId);
                settings.playDeckId = deckId ?? string.Empty;
                scope.Art.Store.SaveSettings(scope.Art.GameId, settings);
            }
            finally
            {
                scope.Dispose();
            }
        }

        // The deck of yours chosen to play a game with (L2-5); empty: the game's own deck.
        internal string ChosenDeckIdOf(GameDefinition game)
        {
            if (game == null) return string.Empty;
            host.EnsureContentArt();
            return PlayDeckIdOf(game);
        }

        internal static List<ObjectDefinitionId> DeckCardIds(PlayerDeckRecord deck)
        {
            List<ObjectDefinitionId> cards = new List<ObjectDefinitionId>();
            for (int i = 0; i < deck.cards.Count; i++)
            {
                PlayerDeckEntry entry = deck.cards[i];
                if (entry == null || entry.count <= 0 || !Guid.TryParse(entry.cardId, out Guid id)) continue;
                for (int c = 0; c < entry.count; c++) cards.Add(new ObjectDefinitionId(id));
            }

            return cards;
        }

        // After a table is replaced: an Empty Table started with a deck of yours puts that deck in your hand to place.
        internal void OnTableReplaced(TabletopSessionSelection selection)
        {
            List<ObjectDefinitionId> cards = pendingDeckCards;
            string name = pendingDeckName;
            PlayerGameFile game = pendingPlayerGame;
            int gamePlayers = pendingPlayerGameCount;
            pendingPlayerGameCount = 0;
            pendingDeckCards = null;
            pendingDeckName = string.Empty;
            pendingPlayerGame = null;
            host.PlayerGamesPanel.ResetGameDeck();
            // Doc 29: the table is a game you made only when it was started from one.
            host.PlayerGamesPanel.activePlayerGame = selection.Kind == TabletopSessionKind.EmptyCustom ? game : null;
            if (host.PlayerGamesPanel.activePlayerGame != null)
            {
                host.PlayerGamesPanel.LayOutPlayerGame(host.PlayerGamesPanel.activePlayerGame, gamePlayers);
                return;
            }

            if (cards == null || selection.Kind != TabletopSessionKind.EmptyCustom) return;
            try
            {
                host.SessionService.RegisterGameTemplates(); // your newest cards look right on the table
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Your decks] Card looks could not be refreshed: {exception.Message}", host);
            }

            int maximum = CreateTabletopComponentUseCase.MaximumDeckCardCount;
            if (cards.Count > maximum)
            {
                host.ShowMessage($"{name} has {cards.Count} cards; a deck on the table holds {maximum}, so the first {maximum} are placed.");
                cards.RemoveRange(maximum, cards.Count - maximum);
            }

            host.BeginRealCardsPlacement(name, cards);
            host.ShowMessage($"Place {name} on the table.");
        }

        internal void CloseGameSelect()
        {
            returnToGameSelect = false;
            host.runtimeUi?.CloseGameSelect();
        }

        internal void OpenFromGameSelect(Action open)
        {
            returnToGameSelect = true;
            // Owner, 2026-10-26: from the front end, the editor opens over its background, not over the table.
            bool backdrop = host.runtimeUi != null && host.runtimeUi.ShowFrontEndBackdrop();
            if (!backdrop) host.runtimeUi?.CloseGameSelect();
            open();
            if (backdrop && host.runtimeUi.IsFrontEndBackdropAlone)
            {
                // Nothing opened (for example no game has cards to edit): the front end comes back as it was.
                returnToGameSelect = false;
                ShowGameSelect(string.Empty);
            }
        }

        // Owner, 2026-10-26: CREATE > TABLE opens Table tools over the table (it changes the table you see); Done or
        // Cancel comes back to the front end.
        private void OpenTableToolsFromFrontEnd()
        {
            returnToGameSelect = true;
            host.runtimeUi?.CloseGameSelect();
            if (host.OpenTableToolsFromFrontEnd()) return;
            returnToGameSelect = false;
            ShowGameSelect("There is no table here to change.");
        }

        internal void OpenCardEditorFromGameSelect()
        {
            GameSelectEntry entry = FindGameSelectEntry(GameSelectEntries(), gameSelectEntryId);
            if (entry?.Game != null) host.CardEditorPanel.editorGame = entry.Game;
            OpenFromGameSelect(host.CardEditorPanel.OpenCardEditor);
        }

        internal void OpenDeckEditorFromGameSelect()
        {
            GameSelectEntry entry = FindGameSelectEntry(GameSelectEntries(), gameSelectEntryId);
            if (entry?.Game != null) host.CardEditorPanel.editorGame = entry.Game;
            OpenFromGameSelect(host.DeckEditorPanel.OpenDeckEditor);
        }

        internal void CloseCardArtPanel()
        {
            host.runtimeUi?.CloseContentArtPanel();
            ReturnToGameSelect();
        }

        // Called when an editor or panel opened from the menu closes (or from a Save table as a game step, L2-6b).
        internal void ReturnToGameSelect()
        {
            if (host.PlayerGamesPanel.returnToGameSave)
            {
                host.PlayerGamesPanel.ReturnToGameSave();
                return;
            }

            if (!returnToGameSelect) return;
            returnToGameSelect = false;
            ShowGameSelect(string.Empty);
        }
        // ---------- from TabletopComposition.PlayerGames.cs ----------

        internal const string PlayerGameEntryPrefix = "my:";
        internal PlayerGameFile pendingPlayerGame;
        // PLACE-2: how many play the game you made that is starting (0: not chosen).
        internal int pendingPlayerGameCount;
    }
}
