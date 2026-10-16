using System;
using System.Collections.Generic;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Games players make (doc 29). On an Empty Table, Table menu > Save table as a game… saves what is on the table
    /// (boards, Consoles, decks with their cards, loose cards with their face, pawns, tokens, dice) with a name,
    /// players, description and written rules, in the player's profile. The game selection menu lists those games;
    /// Start lays the table out again and the rules card shows the written rules. Saving again over the game
    /// changes it; Delete game removes it. Nothing is enforced: players follow the rules themselves.
    /// L2-6b: saving goes in steps. 1 Deck: choose one of your decks or a game's deck (or make a new one in the deck
    /// editor) and put it on the table. 2 Cards: create a card in that deck (card editor) or pick its cards (deck
    /// editor). 3 Save: name, players, description and rules. The deck is the game's own deck, kept by reference:
    /// Start puts it on the table where it lay, shuffled, with the cards it has then.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const string PlayerGameEntryPrefix = "my:";

        // The game you made that this Empty Table was started from (null: a plain Empty Table), and the one waiting
        // to be laid out once the table is replaced.
        private PlayerGameFile activePlayerGame;
        private PlayerGameFile pendingPlayerGame;
        private GameSaveActions gameSaveActions;
        private string saveName = string.Empty;
        private string saveDescription = string.Empty;
        private string saveRules = string.Empty;
        private int saveMinimumPlayers = 2;
        private int saveMaximumPlayers = 4;
        private string deleteArmedGameId = string.Empty;

        // L2-6b: the save step, the deck chosen for the game (no game ID: no deck), the deck of it on this table
        // (and which deck that is), and the way back from an editor opened by a step.
        private int saveStep = GameSaveModel.DeckStep;
        private string saveDeckGameId = string.Empty;
        private string saveDeckId = string.Empty;
        private ContainerId gameDeckContainerId;
        private string gameDeckOnTableKey = string.Empty;
        private ContainerId lastPlacedContainerId;
        private bool returnToGameSave;
        private HashSet<string> saveKnownDeckKeys;
        private const int SaveDeckLinesShown = 12;

        private PlayerGameStore PlayerGames() => PlayerGameStore.ForProfile(EnsureProfile());

        private void RefreshSaveGameEntry()
        {
            bool emptyTable = activeSession != null && activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom;
            runtimeUi?.ShowSaveGameEntry(emptyTable ? (Action)OpenGameSave : null);
        }

        // ---------- Save table as a game ----------

        private void OpenGameSave()
        {
            PlayerGameFile from = activePlayerGame;
            saveName = from != null ? from.name : "My game";
            saveDescription = from != null ? from.description : string.Empty;
            saveRules = from != null ? from.rulesText : "# How to play\n";
            saveMinimumPlayers = from != null ? from.minimumPlayers : 2;
            saveMaximumPlayers = from != null ? from.maximumPlayers : 4;
            saveStep = GameSaveModel.DeckStep;
            ShowGameSave(string.Empty);
        }

        private void ShowGameSave(string status)
        {
            if (runtimeUi == null || matchState == null) return;
            if (gameSaveActions == null)
            {
                gameSaveActions = new GameSaveActions
                {
                    ChangePlayers = ChangeSavePlayers,
                    SaveAsNew = typed => SavePlayerGame(typed, false),
                    SaveOver = typed => SavePlayerGame(typed, true),
                    Close = () => runtimeUi?.CloseGameSave(),
                    GoToStep = GoToSaveStep,
                    CyclePick = CycleSaveDeck,
                    ActionA = SaveStepActionA,
                    ActionB = SaveStepActionB,
                };
            }

            SavedTable table = CaptureTable();
            DeckChoice deck = ChosenSaveDeck(SaveDeckChoices());
            bool deckOnTable = SaveDeckOnTable();
            string summary = deckOnTable && table.Count == 0
                ? $"The table: {deck.Name} (your game's deck)."
                : deckOnTable ? $"{TableSummary(table)} Your game's deck: {deck.Name}." : TableSummary(table);
            GameSaveModel model = new GameSaveModel
            {
                Summary = summary,
                Name = saveName,
                Description = saveDescription,
                RulesText = saveRules,
                MinimumPlayers = saveMinimumPlayers,
                MaximumPlayers = saveMaximumPlayers,
                CanSaveOver = activePlayerGame != null,
                SaveOverText = activePlayerGame != null ? $"Save over {activePlayerGame.name}" : "Save over",
                Status = !string.IsNullOrEmpty(status)
                    ? status
                    : table.Count == 0 && !deckOnTable
                        ? "The table is empty: put out components with the Toolbox first."
                        : "Starting this game lays the table out exactly like this. Hands and cards in Console slots are not saved.",
            };
            FillSaveStep(model, deck, deckOnTable);
            if (!runtimeUi.ShowGameSave(model, gameSaveActions))
            {
                ShowMessage("The Save table as a game panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        private void ChangeSavePlayers(bool fewest, int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            if (fewest)
            {
                saveMinimumPlayers = Mathf.Clamp(saveMinimumPlayers + step, 1, PlayerGameFile.MaximumPlayers);
                saveMaximumPlayers = Math.Max(saveMaximumPlayers, saveMinimumPlayers);
            }
            else
            {
                saveMaximumPlayers = Mathf.Clamp(saveMaximumPlayers + step, 1, PlayerGameFile.MaximumPlayers);
                saveMinimumPlayers = Math.Min(saveMinimumPlayers, saveMaximumPlayers);
            }

            ShowGameSave(string.Empty);
        }

        private void SavePlayerGame(GameSaveTyped typed, bool over)
        {
            KeepTyped(typed);
            string name = saveName.Trim();
            if (name.Length == 0)
            {
                ShowGameSave("Give the game a name first.");
                return;
            }

            if (name.Length > PlayerGameFile.MaximumNameLength)
            {
                ShowGameSave($"A game's name has at most {PlayerGameFile.MaximumNameLength} letters.");
                return;
            }

            DeckChoice deck = ChosenSaveDeck(SaveDeckChoices());
            bool deckOnTable = SaveDeckOnTable();
            if (deck.Game != null && !deckOnTable)
            {
                saveStep = GameSaveModel.DeckStep;
                ShowGameSave($"Put {deck.Name} on the table first, where it should lie when the game starts (or choose No deck).");
                return;
            }

            SavedTable table = CaptureTable();
            if (table.Count == 0 && !deckOnTable)
            {
                ShowGameSave("The table is empty: put out components with the Toolbox first.");
                return;
            }

            SavedGameDeck gameDeck = new SavedGameDeck();
            if (deckOnTable && matchState.TryGetContainerPlacement(gameDeckContainerId, out ContainerPlacementState deckPlacement))
            {
                gameDeck.gameId = saveDeckGameId;
                gameDeck.deckId = saveDeckId;
                gameDeck.name = deck.Name;
                gameDeck.pose = SavePose(deckPlacement.Pose);
            }

            PlayerGameFile game = new PlayerGameFile
            {
                id = over && activePlayerGame != null ? activePlayerGame.id : PlayerGameFile.NewId(),
                createdUtc = over && activePlayerGame != null ? activePlayerGame.createdUtc : string.Empty,
                name = name,
                description = saveDescription.Trim(),
                minimumPlayers = saveMinimumPlayers,
                maximumPlayers = saveMaximumPlayers,
                rulesText = saveRules,
                table = table,
                deck = gameDeck,
            };
            if (!PlayerGames().Save(game, out string error))
            {
                ShowGameSave($"The game could not be saved: {error}.");
                return;
            }

            // The table is now that game: its name on the chip and its rules on the rules card.
            activePlayerGame = game;
            runtimeUi?.CloseGameSave();
            ShowActiveSessionUi();
            ShowMessage($"Saved {game.name}. Find it under New table… to play it again.");
        }

        private void KeepTyped(GameSaveTyped typed)
        {
            saveName = typed.Name;
            saveDescription = typed.Description;
            saveRules = typed.RulesText;
        }

        // ---------- the steps (L2-6b) ----------

        private void FillSaveStep(GameSaveModel model, DeckChoice deck, bool deckOnTable)
        {
            model.Step = saveStep;
            if (saveStep == GameSaveModel.SaveStep) return;
            bool hasDeck = deck.Game != null;
            List<string> lines = new List<string>();
            int different = 0;
            int count = hasDeck ? SaveDeckCards(deck, out lines, out different).Count : 0;
            if (saveStep == GameSaveModel.DeckStep)
            {
                model.StepTitle = "STEP 1 OF 3 · YOUR GAME'S DECK";
                model.ShowPicker = true;
                model.PickText = hasDeck ? $"{deck.Name} · {CountText(count, "card")}" : "No deck";
                model.ActionAText = "New deck…";
                model.ActionBText = hasDeck && !deckOnTable && count > 0 ? "Put on table" : string.Empty;
                if (!hasDeck)
                {
                    model.StepText = "Your game has no deck of its own: it uses only what is on the table.\n\n"
                        + "Use < and > to choose one of your decks or a game's deck, or New deck… to make one in the deck editor.";
                }
                else if (count == 0)
                {
                    model.StepText = $"{deck.Name} has no cards yet. Add some in step 2 (Cards), then come back to put it on the table.";
                }
                else if (deckOnTable)
                {
                    model.StepText = $"{deck.Name} ({CountText(count, "card")}) is on the table: move it like any deck.\n\n"
                        + "Starting your game puts this deck there again, shuffled and face down, with the cards it has then. "
                        + "Change its cards any time in step 2.";
                }
                else
                {
                    model.StepText = $"{deck.Name} ({CountText(count, "card")}) is your game's deck.\n\n"
                        + "Press Put on table and place it where it should lie when the game starts."
                        + (!gameDeckContainerId.IsEmpty && matchState.TryGetContainerPlacement(gameDeckContainerId, out _)
                            ? " The deck you put on the table before stays there as an ordinary deck; remove it if you do not need it."
                            : string.Empty);
                }

                return;
            }

            model.StepTitle = "STEP 2 OF 3 · CARDS";
            model.ShowPicker = false;
            if (!hasDeck)
            {
                model.StepText = "Choose your game's deck in step 1 first; its cards are made and picked here.\n\n"
                    + "Without a deck, Next goes on to saving.";
                return;
            }

            string text = $"{deck.Name}: {CountText(count, "card")}, {different} different.";
            for (int i = 0; i < lines.Count && i < SaveDeckLinesShown; i++) text += (i == 0 ? "\n" : ", ") + lines[i];
            if (lines.Count > SaveDeckLinesShown) text += $" and {lines.Count - SaveDeckLinesShown} more";
            text += "\n\nCreate a card… makes a new card in this deck (card editor). Pick cards… adds and removes cards (deck editor).";
            if (deck.Deck == null) text += " This is a game's own deck: changing it changes that game too.";
            if (deckOnTable) text += " The deck on the table now stays as it is; your game uses the changed deck from its next start.";
            model.StepText = text;
            model.ActionAText = "Create a card…";
            model.ActionBText = "Pick cards…";
        }

        private void GoToSaveStep(int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            saveStep = Mathf.Clamp(step, GameSaveModel.DeckStep, GameSaveModel.SaveStep);
            ShowGameSave(string.Empty);
        }

        private void CycleSaveDeck(int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            List<DeckChoice> choices = SaveDeckChoices();
            int index = choices.IndexOf(ChosenSaveDeck(choices));
            DeckChoice next = choices[((index + step) % choices.Count + choices.Count) % choices.Count];
            saveDeckGameId = next.Game != null ? next.Game.StableId ?? string.Empty : string.Empty;
            saveDeckId = next.Game != null ? next.Id : string.Empty;
            ShowGameSave(string.Empty);
        }

        // Deck step: New deck… / Put on table. Cards step: Create a card… / Pick cards….
        private void SaveStepActionA(GameSaveTyped typed)
        {
            KeepTyped(typed);
            DeckChoice deck = ChosenSaveDeck(SaveDeckChoices());
            if (saveStep == GameSaveModel.DeckStep) NewDeckFromSave(deck);
            else if (saveStep == GameSaveModel.CardsStep && deck.Game != null) CreateCardFromSave(deck);
        }

        private void SaveStepActionB(GameSaveTyped typed)
        {
            KeepTyped(typed);
            DeckChoice deck = ChosenSaveDeck(SaveDeckChoices());
            if (deck.Game == null) return;
            if (saveStep == GameSaveModel.DeckStep) PutSaveDeckOnTable(deck);
            else if (saveStep == GameSaveModel.CardsStep) PickCardsFromSave(deck);
        }

        // "No deck", then for every game: its own deck and the player's decks for it.
        private List<DeckChoice> SaveDeckChoices()
        {
            List<DeckChoice> choices = new List<DeckChoice> { new DeckChoice { Name = "No deck" } };
            EnsureContentArt();
            CollectEditorGames();
            for (int g = 0; g < editorGames.Count; g++)
            {
                GameDefinition game = editorGames[g];
                choices.Add(new DeckChoice { Id = GameDeckId, Name = $"{game.DisplayName} deck", Game = game });
                AddDeckChoices(choices, game, true);
            }

            return choices;
        }

        private DeckChoice ChosenSaveDeck(List<DeckChoice> choices)
        {
            for (int i = 1; i < choices.Count; i++)
            {
                if (choices[i].Game != null
                    && string.Equals(choices[i].Game.StableId, saveDeckGameId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(choices[i].Id, saveDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    return choices[i];
                }
            }

            return choices[0];
        }

        private static string DeckKey(string gameId, string deckId) => $"{gameId}|{deckId}".ToLowerInvariant();

        private static string DeckKey(DeckChoice choice) =>
            choice.Game != null ? DeckKey(choice.Game.StableId ?? string.Empty, choice.Id) : string.Empty;

        // The chosen deck is on this table when the deck put there (or laid out at Start) is that deck and is still
        // on the table.
        private bool SaveDeckOnTable() =>
            !string.IsNullOrEmpty(saveDeckGameId)
            && !gameDeckContainerId.IsEmpty
            && gameDeckOnTableKey == DeckKey(saveDeckGameId, saveDeckId)
            && matchState != null
            && matchState.TryGetContainerPlacement(gameDeckContainerId, out _);

        // The deck's cards with their copies, from the profile as saved (the deck editor's counting), and one
        // "3 x Name" line per different card.
        private List<ObjectDefinitionId> SaveDeckCards(DeckChoice choice, out List<string> lines, out int different)
        {
            lines = new List<string>();
            different = 0;
            List<ObjectDefinitionId> cards = new List<ObjectDefinitionId>();
            if (choice?.Game == null) return cards;
            ContentArtService previous = UseEditorGame(choice.Game);
            previous?.Dispose();
            try
            {
                List<DeckEditorCard> all = DeckEditorCards();
                for (int i = 0; i < all.Count; i++)
                {
                    int copies = DeckEditorCopies(choice.Id, all[i]);
                    if (copies <= 0 || !Guid.TryParse(all[i].Id, out Guid id)) continue;
                    different++;
                    lines.Add($"{copies} x {CardName(all[i])}");
                    for (int c = 0; c < copies; c++) cards.Add(new ObjectDefinitionId(id));
                }
            }
            finally
            {
                DisposeCardEditor();
            }

            return cards;
        }

        // The deck editor with a new deck for the chosen deck's game; back here when it closes, with that deck chosen.
        private void NewDeckFromSave(DeckChoice deck)
        {
            List<DeckChoice> choices = SaveDeckChoices();
            if (editorGames.Count == 0)
            {
                ShowGameSave("There are no games whose cards a deck could use.");
                return;
            }

            saveKnownDeckKeys = new HashSet<string>();
            for (int i = 0; i < choices.Count; i++) saveKnownDeckKeys.Add(DeckKey(choices[i]));
            editorGame = deck.Game ?? editorGames[0];
            OpenEditorFromSave(OpenDeckEditor);
            NewDeckInEditor();
            ShowDeckEditor("A new deck for your game: name it, use + to put cards in, then Save and Close to go back.");
        }

        // The card editor with a new card ticked into the chosen deck.
        private void CreateCardFromSave(DeckChoice deck)
        {
            editorGame = deck.Game;
            OpenEditorFromSave(OpenCardEditor);
            NewEditorCard(default);
            if (editorDraft == null || !editorDraft.IsNew) return;
            if (deck.Deck != null) editorDraft.DeckCopies[deck.Id] = 1;
            ShowCardEditor($"A new card in {deck.Name}. Give it a name and pictures, Save, then Close to go back to your game.");
        }

        // The deck editor on the chosen deck.
        private void PickCardsFromSave(DeckChoice deck)
        {
            editorGame = deck.Game;
            OpenEditorFromSave(OpenDeckEditor);
            deckEditorSelectedId = deck.Deck != null ? deck.Id : GameDeckId;
            deckEditorShowAll = true;
            ShowDeckEditor($"{deck.Name}: use + and − to add and remove cards, then Save and Close to go back to your game.");
        }

        private void OpenEditorFromSave(Action open)
        {
            returnToGameSelect = false;
            returnToGameSave = true;
            runtimeUi?.CloseGameSave();
            open();
        }

        // An editor opened by a step closed: back to the same step; a deck made meanwhile becomes the choice.
        private void ReturnToGameSave()
        {
            returnToGameSave = false;
            string status = string.Empty;
            if (saveKnownDeckKeys != null)
            {
                List<DeckChoice> choices = SaveDeckChoices();
                for (int i = 1; i < choices.Count; i++)
                {
                    if (choices[i].Deck == null || saveKnownDeckKeys.Contains(DeckKey(choices[i]))) continue;
                    saveDeckGameId = choices[i].Game.StableId ?? string.Empty;
                    saveDeckId = choices[i].Id;
                    status = $"{choices[i].Name} is your game's deck now.";
                    break;
                }

                saveKnownDeckKeys = null;
            }

            ShowGameSave(status);
        }

        // Places the chosen deck where the game's deck should lie; the panel comes back when placed or cancelled.
        private void PutSaveDeckOnTable(DeckChoice deck)
        {
            List<ObjectDefinitionId> cards = SaveDeckCards(deck, out _, out _);
            if (cards.Count == 0 || componentPlacementController == null)
            {
                ShowGameSave($"{deck.Name} has no cards yet: add some in step 2 (Cards).");
                return;
            }

            int maximum = CreateTabletopComponentUseCase.MaximumDeckCardCount;
            string trimmed = string.Empty;
            if (cards.Count > maximum)
            {
                trimmed = $" A deck on the table holds {maximum} cards, so the first {maximum} were placed.";
                cards.RemoveRange(maximum, cards.Count - maximum);
            }

            string key = DeckKey(deck);
            string name = deck.Name;
            TabletopSession session = activeSession;
            runtimeUi?.CloseGameSave();
            float rotation = localSeatLayout != null ? localSeatLayout.PlayerZonePose.RotationDegrees : 0f;
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Deck,
                CreateDeckOfCardsPlacementPreview(cards.Count),
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose =>
                {
                    if (!CommitRealCardsPlacement(name, cards, pose, CardFace.FaceUp,
                            ToolboxArrivalFace(TabletopComponentKind.Deck), true)) return false;
                    gameDeckContainerId = lastPlacedContainerId;
                    gameDeckOnTableKey = key;
                    return true;
                },
                placed =>
                {
                    if (!ReferenceEquals(session, activeSession)) return;
                    ShowGameSave(placed
                        ? $"{name} is on the table.{trimmed} Next: its cards."
                        : $"{name} was not placed. Press Put on table to try again.");
                });
            ShowPlacementHint($"{name} (your game's deck)");
        }

        // Start (doc 29, L2-6b): the game's deck, with the cards it has now, where it lay, shuffled.
        private bool LayOutGameDeck(SavedGameDeck deck)
        {
            List<DeckChoice> choices = SaveDeckChoices();
            saveDeckGameId = deck.gameId;
            saveDeckId = deck.deckId;
            DeckChoice choice = ChosenSaveDeck(choices);
            if (choice.Game == null) return false;
            List<ObjectDefinitionId> cards = SaveDeckCards(choice, out _, out _);
            int maximum = CreateTabletopComponentUseCase.MaximumDeckCardCount;
            if (cards.Count > maximum) cards.RemoveRange(maximum, cards.Count - maximum);
            if (cards.Count == 0
                || !CommitRealCardsPlacement(choice.Name, cards, LoadPose(deck.pose), CardFace.FaceUp,
                    ToolboxArrivalFace(TabletopComponentKind.Deck), true, true))
            {
                return false;
            }

            gameDeckContainerId = lastPlacedContainerId;
            gameDeckOnTableKey = DeckKey(choice);
            if (!gameDeckContainerId.IsEmpty) ShuffleDeck(gameDeckContainerId);
            return true;
        }

        // A new table forgets the deck of the table before; a game started from a saved one sets it again.
        private void ResetGameDeck()
        {
            saveStep = GameSaveModel.DeckStep;
            saveDeckGameId = string.Empty;
            saveDeckId = string.Empty;
            gameDeckContainerId = ContainerId.Empty;
            gameDeckOnTableKey = string.Empty;
            returnToGameSave = false;
            saveKnownDeckKeys = null;
        }

        // ---------- the table save file ----------

        // Everything players put on the table, by kind, position and contents (never the game's own fixed pieces).
        private SavedTable CaptureTable()
        {
            SavedTable table = new SavedTable();
            if (matchState == null) return table;
            foreach (KeyValuePair<ContainerId, ContainerState> pair in matchState.Containers)
            {
                ContainerState container = pair.Value;
                string kind = container.Kind == ContainerKind.Deck
                    ? SavedPile.Deck
                    : container.Kind == ContainerKind.Stack
                        ? SavedPile.Stack
                        : container.Kind == ContainerKind.DiscardPile ? SavedPile.DiscardPile : null;
                if (kind == null
                    || matchState.IsTemplateContainer(container.Id)
                    || (container.Id.Equals(gameDeckContainerId) && SaveDeckOnTable()) // the game's deck: saved by reference
                    || !matchState.TryGetContainerPlacement(container.Id, out ContainerPlacementState placement))
                {
                    continue;
                }

                SavedPile pile = new SavedPile { kind = kind, arrivalFace = (int)container.ArrivalFace, pose = SavePose(placement.Pose) };
                for (int i = 0; i < container.ObjectIds.Count; i++)
                {
                    if (matchState.Cards.TryGetValue(container.ObjectIds[i], out CardInstanceState card))
                    {
                        pile.cards.Add(card.BaseState.DefinitionId.Value.ToString("D"));
                    }
                }

                table.piles.Add(pile);
            }

            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                if (!card.BaseState.ContainerId.IsEmpty || matchState.IsTemplateObject(card.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece
                {
                    kind = SavedPiece.Card,
                    definitionId = card.BaseState.DefinitionId.Value.ToString("D"),
                    faceDown = card.Face == CardFace.FaceDown,
                    pose = SavePose(card.BaseState.Pose),
                });
            }

            foreach (PawnState pawn in matchState.Pawns.Values)
            {
                if (!pawn.BaseState.ContainerId.IsEmpty || matchState.IsTemplateObject(pawn.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece { kind = SavedPiece.Pawn, pose = SavePose(pawn.BaseState.Pose) });
            }

            foreach (TokenState token in matchState.Tokens.Values)
            {
                if (!token.BaseState.ContainerId.IsEmpty || matchState.IsTemplateObject(token.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece { kind = SavedPiece.Token, pose = SavePose(token.BaseState.Pose) });
            }

            foreach (DieState die in matchState.Dice.Values)
            {
                if (!die.BaseState.ContainerId.IsEmpty || matchState.IsTemplateObject(die.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece { kind = SavedPiece.Die, sides = die.SideCount, pose = SavePose(die.BaseState.Pose) });
            }

            foreach (PlacedConsoleState console in matchState.PlacedConsoles.Values)
            {
                table.consoles.Add(new SavedConsole { showsBack = console.Console.ShowsBack, pose = SavePose(console.Pose) });
            }

            foreach (PlacedBoardState board in matchState.PlacedBoards.Values)
            {
                table.boards.Add(new SavedBoard
                {
                    columns = board.Grid.Columns,
                    rows = board.Grid.Rows,
                    spaceWidth = board.Grid.SpaceWidth,
                    spaceDepth = board.Grid.SpaceDepth,
                    gap = board.Grid.Gap,
                    frame = board.Grid.Frame,
                    pose = SavePose(board.Pose),
                });
            }

            return table;
        }

        // Lays a saved table out on the (new, empty) table: boards first, so everything else rests on them.
        private void LayOutPlayerGame(PlayerGameFile game)
        {
            try
            {
                RegisterFreshTrapFloorTemplates(); // your newest cards look right on the table
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Your games] Card looks could not be refreshed: {exception.Message}", this);
            }

            SavedTable table = game.table ?? new SavedTable();
            int failed = 0;
            for (int i = 0; i < table.boards.Count; i++)
            {
                SavedBoard board = table.boards[i];
                if (board == null) continue;
                BoardGrid grid = new BoardGrid(
                    Mathf.Clamp(board.columns, 1, BoardGrid.MaximumColumns),
                    Mathf.Clamp(board.rows, 1, BoardGrid.MaximumRows),
                    board.spaceWidth, board.spaceDepth, board.gap, board.frame);
                if (!grid.IsValid || !CommitBoardCreate(LoadPose(board.pose), grid, false)) failed++;
            }

            for (int i = 0; i < table.consoles.Count; i++)
            {
                SavedConsole console = table.consoles[i];
                if (console == null) continue;
                if (!AddToolboxComponentAtPose(TabletopComponentKind.Console, 0, LoadPose(console.pose), console.showsBack, true).Succeeded) failed++;
            }

            int maximum = CreateTabletopComponentUseCase.MaximumDeckCardCount;
            for (int i = 0; i < table.piles.Count; i++)
            {
                SavedPile pile = table.piles[i];
                if (pile == null) continue;
                List<ObjectDefinitionId> cards = CardIds(pile.cards);
                if (cards.Count > maximum) cards.RemoveRange(maximum, cards.Count - maximum);
                bool laid;
                if (cards.Count == 0)
                {
                    TabletopComponentKind kind = pile.kind == SavedPile.Stack
                        ? TabletopComponentKind.Stack
                        : pile.kind == SavedPile.DiscardPile ? TabletopComponentKind.DiscardPile : TabletopComponentKind.Deck;
                    laid = AddToolboxComponentAtPose(kind, 0, LoadPose(pile.pose), false, true).Succeeded;
                }
                else
                {
                    // A pile of cards is laid out as a deck of those cards, bottom to top.
                    laid = CommitRealCardsPlacement("deck", cards, LoadPose(pile.pose), CardFace.FaceUp,
                        (ContainerArrivalFace)pile.arrivalFace, true, true);
                }

                if (!laid) failed++;
            }

            if (game.deck != null && game.deck.IsSet && !LayOutGameDeck(game.deck))
            {
                failed++;
                Debug.LogWarning($"[Your games] {game.name}'s deck ({game.deck.name}) is missing or has no cards.", this);
            }

            for (int i = 0; i < table.pieces.Count; i++)
            {
                SavedPiece piece = table.pieces[i];
                if (piece == null) continue;
                TabletopPose pose = LoadPose(piece.pose);
                bool laid;
                switch (piece.kind)
                {
                    case SavedPiece.Pawn:
                        laid = AddToolboxComponentAtPose(TabletopComponentKind.Pawn, 0, pose, false, true).Succeeded;
                        break;
                    case SavedPiece.Token:
                        laid = AddToolboxComponentAtPose(TabletopComponentKind.Token, 0, pose, false, true).Succeeded;
                        break;
                    case SavedPiece.Die:
                        laid = ToolboxComponentDefinitions.IsSupportedDieSideCount(piece.sides)
                            && AddToolboxComponentAtPose(TabletopComponentKind.Die, piece.sides, pose, false, true).Succeeded;
                        break;
                    default:
                        List<ObjectDefinitionId> card = CardIds(new List<string> { piece.definitionId });
                        laid = card.Count == 1 && CommitRealCardsPlacement("card", card, pose,
                            piece.faceDown ? CardFace.FaceDown : CardFace.FaceUp, ContainerArrivalFace.Unchanged, false, true);
                        break;
                }

                if (!laid) failed++;
            }

            ShowActiveSessionUi(); // the chip shows the game's name and the rules card its rules
            ShowMessage(failed == 0
                ? $"{game.name} is ready. Its rules are on the rules card."
                : $"{game.name} is ready, but {failed} piece{(failed == 1 ? string.Empty : "s")} could not be laid out.");
        }

        private static List<ObjectDefinitionId> CardIds(List<string> ids)
        {
            List<ObjectDefinitionId> cards = new List<ObjectDefinitionId>(ids != null ? ids.Count : 0);
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                if (Guid.TryParse(ids[i], out Guid id) && id != Guid.Empty) cards.Add(new ObjectDefinitionId(id));
            }

            return cards;
        }

        private static SavedPose SavePose(TabletopPose pose) => new SavedPose
        {
            x = pose.Position.X,
            y = pose.Position.Y,
            rotation = pose.RotationDegrees,
            layer = pose.Layer,
            order = pose.LocalOrder,
        };

        private static TabletopPose LoadPose(SavedPose pose) =>
            pose == null
                ? TabletopPose.Default
                : new TabletopPose(new TableCoordinate(pose.x, pose.y), pose.rotation, pose.layer, pose.order);

        private static string TableSummary(SavedTable table)
        {
            if (table.Count == 0) return "The table is empty.";
            int cards = 0;
            int decks = 0;
            for (int i = 0; i < table.piles.Count; i++)
            {
                if (table.piles[i] == null) continue;
                decks++;
                cards += table.piles[i].cards.Count;
            }

            int loose = 0;
            int others = 0;
            for (int i = 0; i < table.pieces.Count; i++)
            {
                if (table.pieces[i] == null) continue;
                if (table.pieces[i].kind == SavedPiece.Card) loose++;
                else others++;
            }

            List<string> parts = new List<string>();
            if (table.boards.Count > 0) parts.Add(CountText(table.boards.Count, "board"));
            if (table.consoles.Count > 0) parts.Add(CountText(table.consoles.Count, "Console"));
            if (decks > 0) parts.Add($"{CountText(decks, "pile")} ({CountText(cards, "card")})");
            if (loose > 0) parts.Add(CountText(loose, "loose card"));
            if (others > 0) parts.Add(CountText(others, "pawn, token or die", "pawns, tokens and dice"));
            return "The table: " + string.Join(", ", parts) + ".";
        }

        private static string CountText(int count, string one, string many = null) =>
            count == 1 ? $"1 {one}" : $"{count} {many ?? one + "s"}";

        private static string PlayersText(int minimum, int maximum) =>
            minimum == maximum ? $"{minimum} player{(minimum == 1 ? string.Empty : "s")}" : $"{minimum}–{maximum} players";

        // ---------- the rules card and the menu ----------

        // The written rules: a line starting with "#" is a heading; every other line is a numbered rule.
        private RulesCardModel PlayerGameRulesCardModel()
        {
            List<RulesCardLine> lines = new List<RulesCardLine>();
            int number = 0;
            string[] written = (activePlayerGame.rulesText ?? string.Empty).Split('\n');
            for (int i = 0; i < written.Length; i++)
            {
                string line = written[i].Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    string heading = line.TrimStart('#').Trim();
                    if (heading.Length == 0) continue;
                    lines.Add(new RulesCardLine(RulesCardLineKind.Heading, string.Empty, heading, false));
                    number = 0;
                    continue;
                }

                number++;
                lines.Add(new RulesCardLine(RulesCardLineKind.Rule, number.ToString(), line, false));
            }

            if (lines.Count == 0)
            {
                lines.Add(new RulesCardLine(RulesCardLineKind.Rule, "•", "No rules written yet: Save table as a game… to write them.", false));
            }

            return new RulesCardModel(activePlayerGame.name, false, lines);
        }

        private GameSelectModel PlayerGameSelectModel(GameSelectEntry entry, List<CardEditorRowModel> rows, string status)
        {
            PlayerGameFile game = entry.PlayerGame;
            int rules = 0;
            string[] written = (game.rulesText ?? string.Empty).Split('\n');
            for (int i = 0; i < written.Length; i++)
            {
                string line = written[i].Trim();
                if (line.Length > 0 && line[0] != '#') rules++;
            }

            bool armed = deleteArmedGameId == game.id;
            return new GameSelectModel
            {
                Games = rows,
                Picture = null,
                GameName = entry.Name,
                Detail = string.IsNullOrWhiteSpace(game.description)
                    ? GameDetail(entry)
                    : $"{GameDetail(entry)} · {game.description}",
                ShowMode = false,
                Rules = rules == 0 ? "Rules: none written yet" : $"Rules: {CountText(rules, "rule")} written by you",
                Deck = game.deck != null && game.deck.IsSet
                    ? $"Deck: {game.deck.name}, shuffled at Start. {TableSummary(game.table ?? new SavedTable())}"
                    : TableSummary(game.table ?? new SavedTable()),
                CanChangeDeck = false,
                ProfileName = EnsureProfile().Active.displayName,
                CanOpenCardArt = false,
                CanEditCards = false,
                CanEditDecks = false,
                CanStart = true,
                StartText = $"Start {entry.Name}",
                CanDelete = true,
                DeleteText = armed ? "Press again to delete" : "Delete game",
                Status = !string.IsNullOrEmpty(status)
                    ? status
                    : "Your game: Start lays its table out. To change it, start it, change the table, then Save table as a game… > Save over.",
            };
        }

        // Deleting asks for a second press.
        private void DeleteSelectedPlayerGame()
        {
            List<GameSelectEntry> entries = GameSelectEntries();
            GameSelectEntry entry = FindGameSelectEntry(entries, gameSelectEntryId);
            if (entry?.PlayerGame == null) return;
            if (deleteArmedGameId != entry.PlayerGame.id)
            {
                deleteArmedGameId = entry.PlayerGame.id;
                ShowGameSelect($"Delete {entry.Name}? Press Delete again; this cannot be undone.", entries);
                return;
            }

            deleteArmedGameId = string.Empty;
            if (!PlayerGames().Delete(entry.PlayerGame.id, out string error))
            {
                ShowGameSelect($"{entry.Name} could not be deleted: {error}.", entries);
                return;
            }

            if (activePlayerGame != null && activePlayerGame.id == entry.PlayerGame.id) activePlayerGame = null;
            gameSelectEntryId = EmptyTableEntryId;
            ShowGameSelect($"Deleted {entry.Name}.");
        }
    }
}
