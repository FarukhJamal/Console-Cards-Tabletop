using System;
using System.Collections.Generic;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
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
        // An editor or panel opened from the menu goes back to it when closed.
        internal bool returnToGameSelect;
        // L2-5: the deck an Empty Table starts with (0: none), and the cards waiting to be placed after Start.
        internal int emptyTableDeckIndex;
        internal string pendingDeckName = string.Empty;
        internal List<ObjectDefinitionId> pendingDeckCards;

        internal bool OpenGameSelect()
        {
            host.EnsureContentArt();
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
                    CycleDeck = CycleGameDeck,
                    OpenProfile = () => OpenFromGameSelect(host.ProfilePanel.OpenProfilePanel),
                    OpenCardArt = () => OpenFromGameSelect(host.OpenCardArtPanel),
                    OpenCardEditor = OpenCardEditorFromGameSelect,
                    OpenDeckEditor = OpenDeckEditorFromGameSelect,
                    Start = StartSelectedGame,
                    Delete = host.PlayerGamesPanel.DeleteSelectedPlayerGame,
                    Close = CloseGameSelect,
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
                return host.runtimeUi.ShowGameSelect(host.PlayerGamesPanel.PlayerGameSelectModel(entry, rows, status), gameSelectActions);
            }

            bool isEmpty = entry.Game == null;
            bool playable = isEmpty || entry.Templates.Count > 0;
            GameSelectModel model = new GameSelectModel
            {
                Games = rows,
                Picture = GamePicture(entry.Game),
                GameName = entry.Name,
                Detail = GameDetail(entry),
                ShowMode = entry.ModeNames.Count > 0,
                ModeName = entry.ModeNames.Count > 0 ? entry.ModeNames[gameSelectModeIndex] : string.Empty,
                CanChangeMode = entry.ModeNames.Count > 1,
                Rules = isEmpty ? "No rules: free play with the platform's components." : GameRulesText(entry),
                Deck = DeckLineText(entry, out bool canChangeDeck),
                CanChangeDeck = canChangeDeck,
                ProfileName = host.EnsureProfile().Active.displayName,
                CanOpenCardArt = ReferenceEquals(entry.Game, host.trapFloorGameDefinition) && entry.Game != null,
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
            if (!host.runtimeUi.ShowGameSelect(model, gameSelectActions)) return false;
            return true;
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
                // Only Trap Floor has playable templates so far; each is one mode ("Trap Floor — Easy").
                if (ReferenceEquals(game, host.trapFloorGameDefinition))
                {
                    foreach (KeyValuePair<GameTemplateId, TrapFloorTemplateDefinition> pair in host.availableTrapFloorTemplates)
                    {
                        entry.Templates.Add(pair.Key);
                        string mode = pair.Value.ActiveMode != null ? pair.Value.ActiveMode.DisplayName : string.Empty;
                        entry.ModeNames.Add(string.IsNullOrWhiteSpace(mode) ? pair.Value.Template.DisplayName : mode);
                    }
                }

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
            if (ReferenceEquals(entry.Game, host.trapFloorGameDefinition))
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
                    return ReferenceEquals(game, host.trapFloorGameDefinition)
                        ? host.FrontArt(card)
                        : card.FrontArtwork;
                }
            }

            return null;
        }

        internal ContentArtServiceScope ArtFor(GameDefinition game)
        {
            if (ReferenceEquals(game, host.trapFloorGameDefinition)) return new ContentArtServiceScope(host.contentArt, false);
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
            if (entry.PlayerGame != null)
            {
                // Doc 29: a game you made starts as an Empty Table, then its table is laid out.
                pendingDeckCards = null;
                pendingPlayerGame = entry.PlayerGame;
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
            else if (ReferenceEquals(entry.Game, host.trapFloorGameDefinition) && !string.IsNullOrEmpty(ChosenTrapFloorDeckId()))
            {
                // A deck of yours must be able to set up the board (for example enough Traps for the floor).
                string problem = TrialTrapFloorDeck(templateId);
                if (problem != null)
                {
                    ShowGameSelect($"This deck cannot set up {entry.Name}: {problem} Choose another deck, or add cards to it.", entries);
                    return;
                }
            }

            returnToGameSelect = false;
            host.runtimeUi?.CloseGameSelect();
            host.SessionService.RequestTableReplacement(entry.Game == null
                ? TabletopSessionSelection.EmptyCustom
                : TabletopSessionSelection.FromGameTemplate(templateId));
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

        internal string ChosenTrapFloorDeckId()
        {
            if (host.trapFloorGameDefinition == null) return string.Empty;
            host.EnsureContentArt();
            return PlayDeckIdOf(host.trapFloorGameDefinition);
        }

        // Sets the game up once, off the table, with the chosen deck. Null when it works, else what went wrong.
        internal string TrialTrapFloorDeck(GameTemplateId templateId)
        {
            if (!host.availableTrapFloorTemplates.TryGetValue(templateId, out TrapFloorTemplateDefinition template)) return null;
            host.RulesCardPanel.EnsureHouseRulesLoaded();
            GameContent content = host.TrapFloorGame.BuildTrapFloorContent(true);
            try
            {
                GameDefinitionData data = content.ToData(host.RulesCardPanel.ActiveRuleSet);
                TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    new SystemRandomValueSource(), data, template.ActiveMode.StableId, host.GetConsoleLayout());
                return null;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            finally
            {
                content.Destroy();
            }
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
            pendingDeckCards = null;
            pendingDeckName = string.Empty;
            pendingPlayerGame = null;
            host.PlayerGamesPanel.ResetGameDeck();
            // Doc 29: the table is a game you made only when it was started from one.
            host.PlayerGamesPanel.activePlayerGame = selection.Kind == TabletopSessionKind.EmptyCustom ? game : null;
            if (host.PlayerGamesPanel.activePlayerGame != null)
            {
                host.PlayerGamesPanel.LayOutPlayerGame(host.PlayerGamesPanel.activePlayerGame);
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
            host.runtimeUi?.CloseGameSelect();
            open();
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
    }
}
