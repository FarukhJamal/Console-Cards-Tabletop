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

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// The game selection menu (doc 27 List 2, L2-4): Table menu > New table… opens it. It lists the Empty Table and
    /// every game the platform has (this table's game and each game box in the library; DLC games arrive as more
    /// boxes). For the chosen game it shows its picture, players, mode, the rules in play and its deck, who is
    /// playing, and the places to make the game yours: card art, the card editor and the deck editor (they open from
    /// here and come back here when closed). Start replaces the table, asking first when the table has content.
    /// The deck line chooses the deck to play with (L2-5): a game plays with its own deck or one of the player's
    /// decks for it (remembered per game in the profile); an Empty Table can start with any deck of the player's,
    /// placed on the table right after Start.
    /// </summary>
    public sealed partial class TabletopPrototypeComposition
    {
        private const string EmptyTableEntryId = "empty-table";

        private GameSelectActions gameSelectActions;
        private string gameSelectEntryId = string.Empty;
        private int gameSelectModeIndex;
        // An editor or panel opened from the menu goes back to it when closed.
        private bool returnToGameSelect;
        // L2-5: the deck an Empty Table starts with (0: none), and the cards waiting to be placed after Start.
        private int emptyTableDeckIndex;
        private string pendingDeckName = string.Empty;
        private List<ObjectDefinitionId> pendingDeckCards;

        // A deck the menu offers: the game's own deck (empty ID), or one of the player's decks.
        private sealed class DeckChoice
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public int Cards;
            public PlayerDeckRecord Deck;
            // The game whose cards the deck uses (doc 29, L2-6b).
            public GameDefinition Game;
        }

        private sealed class GameSelectEntry
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public GameDefinition Game;
            // Doc 29: a game the player made (Game is then null).
            public PlayerGameFile PlayerGame;
            public readonly List<GameTemplateId> Templates = new List<GameTemplateId>();
            public readonly List<string> ModeNames = new List<string>();
        }

        private bool OpenGameSelect()
        {
            EnsureContentArt();
            List<GameSelectEntry> entries = GameSelectEntries();
            if (FindGameSelectEntry(entries, gameSelectEntryId) == null)
            {
                // Start on the game on the table (or Trap Floor from an Empty Table).
                bool onEmptyTable = activePlayerGame == null
                    && (activeSession == null || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom);
                gameSelectEntryId = onEmptyTable && entries.Count > 1 ? entries[1].Id : CurrentGameEntryId(entries);
                gameSelectModeIndex = CurrentModeIndex(FindGameSelectEntry(entries, gameSelectEntryId));
            }

            return ShowGameSelect(string.Empty, entries);
        }

        private bool ShowGameSelect(string status, List<GameSelectEntry> entries = null)
        {
            if (runtimeUi == null) return false;
            entries = entries ?? GameSelectEntries();
            if (gameSelectActions == null)
            {
                gameSelectActions = new GameSelectActions
                {
                    SelectGame = SelectGameEntry,
                    CycleMode = CycleGameMode,
                    CycleDeck = CycleGameDeck,
                    OpenProfile = () => OpenFromGameSelect(OpenProfilePanel),
                    OpenCardArt = () => OpenFromGameSelect(OpenCardArtPanel),
                    OpenCardEditor = OpenCardEditorFromGameSelect,
                    OpenDeckEditor = OpenDeckEditorFromGameSelect,
                    Start = StartSelectedGame,
                    Delete = DeleteSelectedPlayerGame,
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
                return runtimeUi.ShowGameSelect(PlayerGameSelectModel(entry, rows, status), gameSelectActions);
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
                ProfileName = EnsureProfile().Active.displayName,
                CanOpenCardArt = ReferenceEquals(entry.Game, trapFloorGameDefinition) && entry.Game != null,
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
            if (!runtimeUi.ShowGameSelect(model, gameSelectActions)) return false;
            return true;
        }

        // ---------- the games ----------

        private List<GameSelectEntry> GameSelectEntries()
        {
            List<GameSelectEntry> entries = new List<GameSelectEntry>
            {
                new GameSelectEntry { Id = EmptyTableEntryId, Name = "Empty Table" },
            };
            CollectEditorGames();
            for (int g = 0; g < editorGames.Count; g++)
            {
                GameDefinition game = editorGames[g];
                GameSelectEntry entry = new GameSelectEntry
                {
                    Id = game.StableId ?? game.name,
                    Name = string.IsNullOrWhiteSpace(game.DisplayName) ? game.name : game.DisplayName,
                    Game = game,
                };
                // Only Trap Floor has playable templates so far; each is one mode ("Trap Floor — Easy").
                if (ReferenceEquals(game, trapFloorGameDefinition))
                {
                    foreach (KeyValuePair<GameTemplateId, TrapFloorTemplateDefinition> pair in availableTrapFloorTemplates)
                    {
                        entry.Templates.Add(pair.Key);
                        string mode = pair.Value.ActiveMode != null ? pair.Value.ActiveMode.DisplayName : string.Empty;
                        entry.ModeNames.Add(string.IsNullOrWhiteSpace(mode) ? pair.Value.Template.DisplayName : mode);
                    }
                }

                entries.Add(entry);
            }

            // Doc 29: the games you made, after the platform's games.
            List<PlayerGameFile> mine = PlayerGames().List();
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

        private static GameSelectEntry FindGameSelectEntry(List<GameSelectEntry> entries, string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Id, id, StringComparison.OrdinalIgnoreCase)) return entries[i];
            }

            return null;
        }

        private string CurrentGameEntryId(List<GameSelectEntry> entries)
        {
            if (activePlayerGame != null && activeSession != null && activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return PlayerGameEntryPrefix + activePlayerGame.id;
            }

            if (activeSession == null || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom) return EmptyTableEntryId;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Templates.Contains(activeSession.Selection.GameTemplateId)) return entries[i].Id;
            }

            return EmptyTableEntryId;
        }

        private int CurrentModeIndex(GameSelectEntry entry)
        {
            if (entry == null || activeSession == null || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom) return 0;
            int index = entry.Templates.IndexOf(activeSession.Selection.GameTemplateId);
            return index < 0 ? 0 : index;
        }

        private string GameDetail(GameSelectEntry entry)
        {
            if (entry.PlayerGame != null) return $"Your game · {PlayersText(entry.PlayerGame.minimumPlayers, entry.PlayerGame.maximumPlayers)}";
            if (entry.Game == null) return "Free play · any number of players";
            int min = entry.Game.MinimumPlayers;
            int max = entry.Game.MaximumPlayers;
            string players = min == max ? $"{min} player{(min == 1 ? string.Empty : "s")}" : $"{min}–{max} players";
            string modes = entry.ModeNames.Count > 0
                ? $"{entry.ModeNames.Count} mode{(entry.ModeNames.Count == 1 ? string.Empty : "s")}"
                : "not playable yet";
            return $"{players} · {modes}";
        }

        private string GameRulesText(GameSelectEntry entry)
        {
            if (ReferenceEquals(entry.Game, trapFloorGameDefinition))
            {
                EnsureHouseRulesLoaded();
                RuleSetDefinition rules = ActiveRuleSet;
                string name = rules != null ? rules.DisplayName : "Default";
                return $"Rules: {name} (change them on the rules card, with everyone's agreement)";
            }

            RuleSetDefinition defaults = entry.Game.DefaultRuleSet;
            return defaults != null ? $"Rules: {defaults.DisplayName}" : "Rules: from the game box";
        }

        // The game's deck as it will be played: its cards with your copies and your own cards (doc 24, L2-2).
        private string GameDeckText(GameSelectEntry entry)
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
        private Texture2D GamePicture(GameDefinition game)
        {
            if (game == null) return null;
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    if (card == null || card.RepresentedControllerInput != null) continue;
                    return ReferenceEquals(game, trapFloorGameDefinition)
                        ? FrontArt(card)
                        : card.FrontArtwork;
                }
            }

            return null;
        }

        // The table's own picture service for its game; a short-lived one for any other game.
        private readonly struct ContentArtServiceScope : IDisposable
        {
            public ContentArtServiceScope(ContentArtService art, bool owned)
            {
                Art = art;
                Owned = owned;
            }

            public ContentArtService Art { get; }
            private bool Owned { get; }

            public void Dispose()
            {
                if (Owned) Art?.Dispose();
            }
        }

        private ContentArtServiceScope ArtFor(GameDefinition game)
        {
            if (ReferenceEquals(game, trapFloorGameDefinition)) return new ContentArtServiceScope(contentArt, false);
            ContentArtService art = new ContentArtService(ContentPackStore.ForProfile(EnsureProfile()));
            art.UseGame(game);
            return new ContentArtServiceScope(art, true);
        }

        // ---------- actions ----------

        private void SelectGameEntry(string entryId)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, entryId);
            if (entry == null) return;
            deleteArmedGameId = string.Empty;
            gameSelectEntryId = entry.Id;
            gameSelectModeIndex = CurrentGameEntryId(entries) == entry.Id ? CurrentModeIndex(entry) : 0;
            ShowGameSelect(string.Empty, entries);
        }

        private void CycleGameMode(int step)
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId);
            if (entry == null || entry.ModeNames.Count == 0) return;
            gameSelectModeIndex = ((gameSelectModeIndex + step) % entry.ModeNames.Count + entry.ModeNames.Count) % entry.ModeNames.Count;
            ShowGameSelect(string.Empty, entries);
        }

        private void StartSelectedGame()
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
                runtimeUi?.CloseGameSelect();
                RequestTableReplacement(TabletopSessionSelection.EmptyCustom);
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
            else if (ReferenceEquals(entry.Game, trapFloorGameDefinition) && !string.IsNullOrEmpty(ChosenTrapFloorDeckId()))
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
            runtimeUi?.CloseGameSelect();
            RequestTableReplacement(entry.Game == null
                ? TabletopSessionSelection.EmptyCustom
                : TabletopSessionSelection.FromGameTemplate(templateId));
        }

        // ---------- the deck to play with (L2-5) ----------

        private List<DeckChoice> DeckChoices(GameSelectEntry entry)
        {
            List<DeckChoice> choices = new List<DeckChoice>();
            if (entry.Game == null)
            {
                choices.Add(new DeckChoice { Name = "No deck" });
                CollectEditorGames();
                for (int g = 0; g < editorGames.Count; g++) AddDeckChoices(choices, editorGames[g], true);
                return choices;
            }

            choices.Add(new DeckChoice { Name = $"{entry.Name} deck" });
            AddDeckChoices(choices, entry.Game, false);
            return choices;
        }

        private void AddDeckChoices(List<DeckChoice> choices, GameDefinition game, bool withGameName)
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

        private int ChosenDeckIndex(GameSelectEntry entry, List<DeckChoice> choices)
        {
            if (entry.Game == null) return Mathf.Clamp(emptyTableDeckIndex, 0, choices.Count - 1);
            string chosen = PlayDeckIdOf(entry.Game);
            for (int i = 1; i < choices.Count; i++)
            {
                if (string.Equals(choices[i].Id, chosen, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return 0;
        }

        private string DeckLineText(GameSelectEntry entry, out bool canChange)
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

        private void CycleGameDeck(int step)
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
        private string PlayDeckIdOf(GameDefinition game)
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

        private void SetPlayDeckId(GameDefinition game, string deckId)
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

        private string ChosenTrapFloorDeckId()
        {
            if (trapFloorGameDefinition == null) return string.Empty;
            EnsureContentArt();
            return PlayDeckIdOf(trapFloorGameDefinition);
        }

        // Sets the game up once, off the table, with the chosen deck. Null when it works, else what went wrong.
        private string TrialTrapFloorDeck(GameTemplateId templateId)
        {
            if (!availableTrapFloorTemplates.TryGetValue(templateId, out TrapFloorTemplateDefinition template)) return null;
            EnsureHouseRulesLoaded();
            GameContent content = BuildTrapFloorContent(true);
            try
            {
                GameDefinitionData data = content.ToData(ActiveRuleSet);
                TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    new SystemRandomValueSource(), data, template.ActiveMode.StableId, GetConsoleLayout());
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

        private static List<ObjectDefinitionId> DeckCardIds(PlayerDeckRecord deck)
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
        private void OnTableReplaced(TabletopSessionSelection selection)
        {
            List<ObjectDefinitionId> cards = pendingDeckCards;
            string name = pendingDeckName;
            PlayerGameFile game = pendingPlayerGame;
            pendingDeckCards = null;
            pendingDeckName = string.Empty;
            pendingPlayerGame = null;
            ResetGameDeck();
            // Doc 29: the table is a game you made only when it was started from one.
            activePlayerGame = selection.Kind == TabletopSessionKind.EmptyCustom ? game : null;
            if (activePlayerGame != null)
            {
                LayOutPlayerGame(activePlayerGame);
                return;
            }

            if (cards == null || selection.Kind != TabletopSessionKind.EmptyCustom) return;
            try
            {
                RegisterFreshTrapFloorTemplates(); // your newest cards look right on the table
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Your decks] Card looks could not be refreshed: {exception.Message}", this);
            }

            int maximum = CreateTabletopComponentUseCase.MaximumDeckCardCount;
            if (cards.Count > maximum)
            {
                ShowMessage($"{name} has {cards.Count} cards; a deck on the table holds {maximum}, so the first {maximum} are placed.");
                cards.RemoveRange(maximum, cards.Count - maximum);
            }

            BeginRealCardsPlacement(name, cards);
            ShowMessage($"Place {name} on the table.");
        }

        private void CloseGameSelect()
        {
            returnToGameSelect = false;
            runtimeUi?.CloseGameSelect();
        }

        private void OpenFromGameSelect(Action open)
        {
            returnToGameSelect = true;
            runtimeUi?.CloseGameSelect();
            open();
        }

        private void OpenCardEditorFromGameSelect()
        {
            GameSelectEntry entry = FindGameSelectEntry(GameSelectEntries(), gameSelectEntryId);
            if (entry?.Game != null) editorGame = entry.Game;
            OpenFromGameSelect(OpenCardEditor);
        }

        private void OpenDeckEditorFromGameSelect()
        {
            GameSelectEntry entry = FindGameSelectEntry(GameSelectEntries(), gameSelectEntryId);
            if (entry?.Game != null) editorGame = entry.Game;
            OpenFromGameSelect(OpenDeckEditor);
        }

        private void CloseCardArtPanel()
        {
            runtimeUi?.CloseContentArtPanel();
            ReturnToGameSelect();
        }

        // Called when an editor or panel opened from the menu closes (or from a Save table as a game step, L2-6b).
        private void ReturnToGameSelect()
        {
            if (returnToGameSave)
            {
                ReturnToGameSave();
                return;
            }

            if (!returnToGameSelect) return;
            returnToGameSelect = false;
            ShowGameSelect(string.Empty);
        }
    }
}
