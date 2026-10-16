using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Content;
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
    /// </summary>
    public sealed partial class TabletopPrototypeComposition
    {
        private const string EmptyTableEntryId = "empty-table";

        private GameSelectActions gameSelectActions;
        private string gameSelectEntryId = string.Empty;
        private int gameSelectModeIndex;
        // An editor or panel opened from the menu goes back to it when closed.
        private bool returnToGameSelect;

        private sealed class GameSelectEntry
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public GameDefinition Game;
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
                bool onEmptyTable = activeSession == null || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom;
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
                    OpenProfile = () => OpenFromGameSelect(OpenProfilePanel),
                    OpenCardArt = () => OpenFromGameSelect(OpenCardArtPanel),
                    OpenCardEditor = OpenCardEditorFromGameSelect,
                    OpenDeckEditor = OpenDeckEditorFromGameSelect,
                    Start = StartSelectedGame,
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
                Deck = isEmpty ? "Bring any components from the Toolbox." : GameDeckText(entry),
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
            if (entry.Game != null && entry.Templates.Count == 0)
            {
                ShowGameSelect($"{entry.Name} cannot be played yet.", entries);
                return;
            }

            returnToGameSelect = false;
            runtimeUi?.CloseGameSelect();
            RequestTableReplacement(entry.Game == null
                ? TabletopSessionSelection.EmptyCustom
                : TabletopSessionSelection.FromGameTemplate(entry.Templates[Mathf.Clamp(gameSelectModeIndex, 0, entry.Templates.Count - 1)]));
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

        // Called when an editor or panel opened from the menu closes.
        private void ReturnToGameSelect()
        {
            if (!returnToGameSelect) return;
            returnToGameSelect = false;
            ShowGameSelect(string.Empty);
        }
    }
}
