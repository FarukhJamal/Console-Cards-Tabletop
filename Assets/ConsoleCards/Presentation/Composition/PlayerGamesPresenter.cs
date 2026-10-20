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
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
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
    /// The player-made games panel: saving the table as a game, step by step, and playing saved games.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>PlayerGamesPanel</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class PlayerGamesPresenter
    {
        private readonly TabletopComposition host;

        internal PlayerGamesPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.PlayerGames.cs ----------


        // The game you made that this Empty Table was started from (null: a plain Empty Table), and the one waiting
        // to be laid out once the table is replaced.
        internal PlayerGameFile activePlayerGame;
        internal GameSaveActions gameSaveActions;
        internal string saveName = string.Empty;
        internal string saveDescription = string.Empty;
        internal string saveRules = string.Empty;
        internal int saveMinimumPlayers = 2;
        internal int saveMaximumPlayers = 4;
        internal string deleteArmedGameId = string.Empty;

        // L2-6b: the save step, the deck chosen for the game (no game ID: no deck), the deck of it on this table
        // (and which deck that is), and the way back from an editor opened by a step.
        internal int saveStep = GameSaveModel.DeckStep;
        internal string saveDeckGameId = string.Empty;
        internal string saveDeckId = string.Empty;
        internal ContainerId gameDeckContainerId;
        internal string gameDeckOnTableKey = string.Empty;
        internal ContainerId lastPlacedContainerId;
        internal bool returnToGameSave;
        internal HashSet<string> saveKnownDeckKeys;
        internal const int SaveDeckLinesShown = 12;
        // DEAL-1: the game's deal rule in the save panel (null: the game does not deal).
        internal DealRule saveDeal;

        internal PlayerGameStore PlayerGames() => PlayerGameStore.ForProfile(host.EnsureProfile());

        internal void RefreshSaveGameEntry()
        {
            bool emptyTable = host.activeSession != null && host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom;
            host.runtimeUi?.ShowSaveGameEntry(emptyTable ? (Action)OpenGameSave : null);
        }

        // ---------- Save table as a game ----------

        internal void OpenGameSave()
        {
            PlayerGameFile from = activePlayerGame;
            saveName = from != null ? from.name : "My game";
            saveDescription = from != null ? from.description : string.Empty;
            saveRules = from != null ? from.rulesText : "# How to play\n";
            saveMinimumPlayers = from != null ? from.minimumPlayers : 2;
            saveMaximumPlayers = from != null ? from.maximumPlayers : 4;
            saveStep = GameSaveModel.DeckStep;
            saveDeal = from != null && DealRule.TryParse(from.deal, out DealRule deal) ? deal : null;
            ShowGameSave(string.Empty);
        }

        internal void ShowGameSave(string status)
        {
            if (host.runtimeUi == null || host.matchState == null) return;
            if (gameSaveActions == null)
            {
                gameSaveActions = new GameSaveActions
                {
                    ChangePlayers = ChangeSavePlayers,
                    SaveAsNew = typed => SavePlayerGame(typed, false),
                    SaveOver = typed => SavePlayerGame(typed, true),
                    Close = () => host.runtimeUi?.CloseGameSave(),
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
            if (!host.runtimeUi.ShowGameSave(model, gameSaveActions))
            {
                host.ShowMessage("The Save table as a game panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        internal void ChangeSavePlayers(bool fewest, int step, GameSaveTyped typed)
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

        internal void SavePlayerGame(GameSaveTyped typed, bool over)
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
            if (deckOnTable && host.matchState.TryGetContainerPlacement(gameDeckContainerId, out ContainerPlacementState deckPlacement))
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
                deal = saveDeal != null ? saveDeal.ToNotation() : string.Empty,
            };
            if (!PlayerGames().Save(game, out string error))
            {
                ShowGameSave($"The game could not be saved: {error}.");
                return;
            }

            // The table is now that game: its name on the chip and its rules on the rules card.
            activePlayerGame = game;
            host.runtimeUi?.CloseGameSave();
            host.SessionService.ShowActiveSessionUi();
            host.ShowMessage($"Saved {game.name}. Find it under New table… to play it again.");
        }

        internal void KeepTyped(GameSaveTyped typed)
        {
            saveName = typed.Name;
            saveDescription = typed.Description;
            saveRules = typed.RulesText;
        }

        // ---------- the steps (L2-6b) ----------

        internal void FillSaveStep(GameSaveModel model, DeckChoice deck, bool deckOnTable)
        {
            model.Step = saveStep;
            if (saveStep == GameSaveModel.SaveStep) return;
            bool hasDeck = deck.Game != null;
            List<string> lines = new List<string>();
            int different = 0;
            int count = hasDeck ? SaveDeckCards(deck, out lines, out different).Count : 0;
            if (saveStep == GameSaveModel.DeckStep)
            {
                model.StepTitle = "STEP 1 OF 4 · YOUR GAME'S DECK";
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
                        + (!gameDeckContainerId.IsEmpty && host.matchState.TryGetContainerPlacement(gameDeckContainerId, out _)
                            ? " The deck you put on the table before stays there as an ordinary deck; remove it if you do not need it."
                            : string.Empty);
                }

                return;
            }

            if (saveStep == GameSaveModel.DealStep)
            {
                FillDealStep(model, deck, hasDeck);
                return;
            }

            model.StepTitle = "STEP 2 OF 4 · CARDS";
            model.ShowPicker = false;
            if (!hasDeck)
            {
                model.StepText = "Choose your game's deck in step 1 first; its cards are made and picked here.\n\n"
                    + "Without a deck, Next goes on to dealing and saving.";
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

        internal void GoToSaveStep(int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            saveStep = Mathf.Clamp(step, GameSaveModel.DeckStep, GameSaveModel.SaveStep);
            ShowGameSave(string.Empty);
        }

        // ---------- the Deal step (DEAL-1) ----------

        // < > choose how many each (no dealing, 1 to 13, all); A: hands or table; B: face on the table, or how many at
        // a time into hands. The game deals this way when it starts, and Deal on its deck follows it.
        internal void FillDealStep(GameSaveModel model, DeckChoice deck, bool hasDeck)
        {
            model.StepTitle = "STEP 3 OF 4 · DEALING";
            model.ShowPicker = true;
            DealRule deal = saveDeal;
            model.PickText = deal == null
                ? "No dealing"
                : deal.DealsAll ? "Deal all the cards, evenly" : deal.CountEach == 1 ? "Deal 1 card to each player" : $"Deal {deal.CountEach} cards to each player";
            if (deal == null)
            {
                model.StepText = "Your game does not deal: players take cards themselves.\n\n"
                    + "Use < and > to choose how many cards each player is dealt when the game starts.";
                return;
            }

            model.ActionAText = deal.To == DealDestination.Hand ? "To: their hands" : "To: the table";
            model.ActionBText = deal.To == DealDestination.Table
                ? deal.Face == DealFace.Down ? "Face: down" : "Face: up"
                : deal.Packet == 1 ? "At a time: one card" : $"At a time: {deal.Packet} cards";
            model.StepText = deal.Describe()
                + (hasDeck ? $"\n\nFrom {deck.Name}, in turn order from the left of the dealer." : "\n\nChoose your game's deck in step 1: the game deals from it.")
                + "\n\nWhen the game allows several numbers of players, starting it asks how many play. Deal on the deck's menu (or T) deals this way again; the rules card shows it.";
        }

        internal void CycleSaveDeal(int step)
        {
            int index = saveDeal == null ? 0 : saveDeal.DealsAll ? DealRuleChoices - 1 : Mathf.Clamp(saveDeal.CountEach, 1, DealRuleChoices - 2);
            index = (((index + step) % DealRuleChoices) + DealRuleChoices) % DealRuleChoices;
            if (index == 0)
            {
                saveDeal = null;
                return;
            }

            if (saveDeal == null) saveDeal = new DealRule { When = DealTiming.Setup };
            saveDeal.CountEach = index == DealRuleChoices - 1 ? 0 : index;
            saveDeal.When = DealTiming.Setup;
        }

        // No dealing, 1 to 13 each, all.
        internal const int DealRuleChoices = 15;

        internal void CycleSaveDeck(int step, GameSaveTyped typed)
        {
            KeepTyped(typed);
            if (saveStep == GameSaveModel.DealStep)
            {
                CycleSaveDeal(step);
                ShowGameSave(string.Empty);
                return;
            }

            List<DeckChoice> choices = SaveDeckChoices();
            int index = choices.IndexOf(ChosenSaveDeck(choices));
            DeckChoice next = choices[((index + step) % choices.Count + choices.Count) % choices.Count];
            saveDeckGameId = next.Game != null ? next.Game.StableId ?? string.Empty : string.Empty;
            saveDeckId = next.Game != null ? next.Id : string.Empty;
            ShowGameSave(string.Empty);
        }

        // Deck step: New deck… / Put on table. Cards step: Create a card… / Pick cards….
        internal void SaveStepActionA(GameSaveTyped typed)
        {
            KeepTyped(typed);
            if (saveStep == GameSaveModel.DealStep)
            {
                if (saveDeal != null) saveDeal.To = saveDeal.To == DealDestination.Hand ? DealDestination.Table : DealDestination.Hand;
                ShowGameSave(string.Empty);
                return;
            }

            DeckChoice deck = ChosenSaveDeck(SaveDeckChoices());
            if (saveStep == GameSaveModel.DeckStep) NewDeckFromSave(deck);
            else if (saveStep == GameSaveModel.CardsStep && deck.Game != null) CreateCardFromSave(deck);
        }

        internal void SaveStepActionB(GameSaveTyped typed)
        {
            KeepTyped(typed);
            if (saveStep == GameSaveModel.DealStep)
            {
                if (saveDeal != null && saveDeal.To == DealDestination.Table) saveDeal.Face = saveDeal.Face == DealFace.Down ? DealFace.Up : DealFace.Down;
                else if (saveDeal != null) saveDeal.Packet = saveDeal.Packet >= 3 ? 1 : saveDeal.Packet + 1;
                ShowGameSave(string.Empty);
                return;
            }

            DeckChoice deck = ChosenSaveDeck(SaveDeckChoices());
            if (deck.Game == null) return;
            if (saveStep == GameSaveModel.DeckStep) PutSaveDeckOnTable(deck);
            else if (saveStep == GameSaveModel.CardsStep) PickCardsFromSave(deck);
        }

        // "No deck", then for every game: its own deck and the player's decks for it.
        internal List<DeckChoice> SaveDeckChoices()
        {
            List<DeckChoice> choices = new List<DeckChoice> { new DeckChoice { Name = "No deck" } };
            host.EnsureContentArt();
            host.CardEditorPanel.CollectEditorGames();
            for (int g = 0; g < host.CardEditorPanel.editorGames.Count; g++)
            {
                GameDefinition game = host.CardEditorPanel.editorGames[g];
                choices.Add(new DeckChoice { Id = CardEditorPresenter.GameDeckId, Name = $"{game.DisplayName} deck", Game = game });
                host.GameSelectPanel.AddDeckChoices(choices, game, true);
            }

            return choices;
        }

        internal DeckChoice ChosenSaveDeck(List<DeckChoice> choices)
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

        internal static string DeckKey(string gameId, string deckId) => $"{gameId}|{deckId}".ToLowerInvariant();

        internal static string DeckKey(DeckChoice choice) =>
            choice.Game != null ? DeckKey(choice.Game.StableId ?? string.Empty, choice.Id) : string.Empty;

        // The chosen deck is on this table when the deck put there (or laid out at Start) is that deck and is still
        // on the table.
        internal bool SaveDeckOnTable() =>
            !string.IsNullOrEmpty(saveDeckGameId)
            && !gameDeckContainerId.IsEmpty
            && gameDeckOnTableKey == DeckKey(saveDeckGameId, saveDeckId)
            && host.matchState != null
            && host.matchState.TryGetContainerPlacement(gameDeckContainerId, out _);

        // The deck's cards with their copies, from the profile as saved (the deck editor's counting), and one
        // "3 x Name" line per different card.
        internal List<ObjectDefinitionId> SaveDeckCards(DeckChoice choice, out List<string> lines, out int different)
        {
            lines = new List<string>();
            different = 0;
            List<ObjectDefinitionId> cards = new List<ObjectDefinitionId>();
            if (choice?.Game == null) return cards;
            ContentArtService previous = host.CardEditorPanel.UseEditorGame(choice.Game);
            previous?.Dispose();
            try
            {
                List<DeckEditorCard> all = host.DeckEditorPanel.DeckEditorCards();
                for (int i = 0; i < all.Count; i++)
                {
                    int copies = host.DeckEditorPanel.DeckEditorCopies(choice.Id, all[i]);
                    if (copies <= 0 || !Guid.TryParse(all[i].Id, out Guid id)) continue;
                    different++;
                    lines.Add($"{copies} x {host.DeckEditorPanel.CardName(all[i])}");
                    for (int c = 0; c < copies; c++) cards.Add(new ObjectDefinitionId(id));
                }
            }
            finally
            {
                host.CardEditorPanel.DisposeCardEditor();
            }

            return cards;
        }

        // The deck editor with a new deck for the chosen deck's game; back here when it closes, with that deck chosen.
        internal void NewDeckFromSave(DeckChoice deck)
        {
            List<DeckChoice> choices = SaveDeckChoices();
            if (host.CardEditorPanel.editorGames.Count == 0)
            {
                ShowGameSave("There are no games whose cards a deck could use.");
                return;
            }

            saveKnownDeckKeys = new HashSet<string>();
            for (int i = 0; i < choices.Count; i++) saveKnownDeckKeys.Add(DeckKey(choices[i]));
            host.CardEditorPanel.editorGame = deck.Game ?? host.CardEditorPanel.editorGames[0];
            OpenEditorFromSave(host.DeckEditorPanel.OpenDeckEditor);
            host.DeckEditorPanel.NewDeckInEditor();
            host.DeckEditorPanel.ShowDeckEditor("A new deck for your game: name it, use + to put cards in, then Save and Close to go back.");
        }

        // The card editor with a new card ticked into the chosen deck.
        internal void CreateCardFromSave(DeckChoice deck)
        {
            host.CardEditorPanel.editorGame = deck.Game;
            OpenEditorFromSave(host.CardEditorPanel.OpenCardEditor);
            host.CardEditorPanel.NewEditorCard(default);
            if (host.CardEditorPanel.editorDraft == null || !host.CardEditorPanel.editorDraft.IsNew) return;
            if (deck.Deck != null) host.CardEditorPanel.editorDraft.DeckCopies[deck.Id] = 1;
            host.CardEditorPanel.ShowCardEditor($"A new card in {deck.Name}. Give it a name and pictures, Save, then Close to go back to your game.");
        }

        // The deck editor on the chosen deck.
        internal void PickCardsFromSave(DeckChoice deck)
        {
            host.CardEditorPanel.editorGame = deck.Game;
            OpenEditorFromSave(host.DeckEditorPanel.OpenDeckEditor);
            host.DeckEditorPanel.deckEditorSelectedId = deck.Deck != null ? deck.Id : CardEditorPresenter.GameDeckId;
            host.DeckEditorPanel.deckEditorShowAll = true;
            host.DeckEditorPanel.ShowDeckEditor($"{deck.Name}: use + and − to add and remove cards, then Save and Close to go back to your game.");
        }

        internal void OpenEditorFromSave(Action open)
        {
            host.GameSelectPanel.returnToGameSelect = false;
            returnToGameSave = true;
            host.runtimeUi?.CloseGameSave();
            open();
        }

        // An editor opened by a step closed: back to the same step; a deck made meanwhile becomes the choice.
        internal void ReturnToGameSave()
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
        internal void PutSaveDeckOnTable(DeckChoice deck)
        {
            List<ObjectDefinitionId> cards = SaveDeckCards(deck, out _, out _);
            if (cards.Count == 0 || host.componentPlacementController == null)
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
            TabletopSession session = host.activeSession;
            host.runtimeUi?.CloseGameSave();
            float rotation = host.localSeatLayout != null ? host.localSeatLayout.PlayerZonePose.RotationDegrees : 0f;
            host.componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Deck,
                host.CreateDeckOfCardsPlacementPreview(cards.Count),
                rotation,
                0,
                host.toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose =>
                {
                    if (!host.CommitRealCardsPlacement(name, cards, pose, CardFace.FaceUp,
                            ToolboxArrivalFace(TabletopComponentKind.Deck), true)) return false;
                    gameDeckContainerId = lastPlacedContainerId;
                    gameDeckOnTableKey = key;
                    return true;
                },
                placed =>
                {
                    if (!ReferenceEquals(session, host.activeSession)) return;
                    ShowGameSave(placed
                        ? $"{name} is on the table.{trimmed} Next: its cards."
                        : $"{name} was not placed. Press Put on table to try again.");
                });
            host.ShowPlacementHint($"{name} (your game's deck)");
        }

        // Start (doc 29, L2-6b): the game's deck, with the cards it has now, where it lay, shuffled.
        internal bool LayOutGameDeck(SavedGameDeck deck)
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
                || !host.CommitRealCardsPlacement(choice.Name, cards, LoadPose(deck.pose), CardFace.FaceUp,
                    ToolboxArrivalFace(TabletopComponentKind.Deck), true, true))
            {
                return false;
            }

            gameDeckContainerId = lastPlacedContainerId;
            gameDeckOnTableKey = DeckKey(choice);
            if (!gameDeckContainerId.IsEmpty) host.ShuffleDeck(gameDeckContainerId);
            return true;
        }

        // A new table forgets the deck of the table before; a game started from a saved one sets it again.
        internal void ResetGameDeck()
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
        internal SavedTable CaptureTable()
        {
            SavedTable table = new SavedTable();
            if (host.matchState == null) return table;
            foreach (KeyValuePair<ContainerId, ContainerState> pair in host.matchState.Containers)
            {
                ContainerState container = pair.Value;
                string kind = container.Kind == ContainerKind.Deck
                    ? SavedPile.Deck
                    : container.Kind == ContainerKind.Stack
                        ? SavedPile.Stack
                        : container.Kind == ContainerKind.DiscardPile ? SavedPile.DiscardPile : null;
                if (kind == null
                    || host.matchState.IsTemplateContainer(container.Id)
                    || (container.Id.Equals(gameDeckContainerId) && SaveDeckOnTable()) // the game's deck: saved by reference
                    || !host.matchState.TryGetContainerPlacement(container.Id, out ContainerPlacementState placement))
                {
                    continue;
                }

                SavedPile pile = new SavedPile { kind = kind, arrivalFace = (int)container.ArrivalFace, pose = SavePose(placement.Pose) };
                for (int i = 0; i < container.ObjectIds.Count; i++)
                {
                    if (host.matchState.Cards.TryGetValue(container.ObjectIds[i], out CardInstanceState card))
                    {
                        pile.cards.Add(card.BaseState.DefinitionId.Value.ToString("D"));
                    }
                }

                table.piles.Add(pile);
            }

            foreach (CardInstanceState card in host.matchState.Cards.Values)
            {
                if (!card.BaseState.ContainerId.IsEmpty || host.matchState.IsTemplateObject(card.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece
                {
                    kind = SavedPiece.Card,
                    definitionId = card.BaseState.DefinitionId.Value.ToString("D"),
                    faceDown = card.Face == CardFace.FaceDown,
                    pose = SavePose(card.BaseState.Pose),
                });
            }

            foreach (PawnState pawn in host.matchState.Pawns.Values)
            {
                if (!pawn.BaseState.ContainerId.IsEmpty || host.matchState.IsTemplateObject(pawn.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece { kind = SavedPiece.Pawn, pose = SavePose(pawn.BaseState.Pose) });
            }

            foreach (TokenState token in host.matchState.Tokens.Values)
            {
                if (!token.BaseState.ContainerId.IsEmpty || host.matchState.IsTemplateObject(token.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece { kind = SavedPiece.Token, pose = SavePose(token.BaseState.Pose) });
            }

            foreach (DieState die in host.matchState.Dice.Values)
            {
                if (!die.BaseState.ContainerId.IsEmpty || host.matchState.IsTemplateObject(die.BaseState.Id)) continue;
                table.pieces.Add(new SavedPiece { kind = SavedPiece.Die, sides = die.SideCount, pose = SavePose(die.BaseState.Pose) });
            }

            foreach (PlacedConsoleState console in host.matchState.PlacedConsoles.Values)
            {
                table.consoles.Add(new SavedConsole { showsBack = console.Console.ShowsBack, pose = SavePose(console.Pose) });
            }

            foreach (PlacedBoardState board in host.matchState.PlacedBoards.Values)
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

            host.SaveTableMarks(table.marks); // drawing tool: the drawings on the table
            return table;
        }

        // Lays a saved table out on the (new, empty) table: boards first, so everything else rests on them.
        internal void LayOutPlayerGame(PlayerGameFile game)
        {
            try
            {
                host.SessionService.RegisterGameTemplates(); // your newest cards look right on the table
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Your games] Card looks could not be refreshed: {exception.Message}", host);
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
                if (!grid.IsValid || !host.CommitBoardCreate(LoadPose(board.pose), grid, false)) failed++;
            }

            for (int i = 0; i < table.consoles.Count; i++)
            {
                SavedConsole console = table.consoles[i];
                if (console == null) continue;
                if (!host.AddToolboxComponentAtPose(TabletopComponentKind.Console, 0, LoadPose(console.pose), console.showsBack, true).Succeeded) failed++;
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
                    laid = host.AddToolboxComponentAtPose(kind, 0, LoadPose(pile.pose), false, true).Succeeded;
                }
                else
                {
                    // A pile of cards is laid out as a deck of those cards, bottom to top.
                    laid = host.CommitRealCardsPlacement("deck", cards, LoadPose(pile.pose), CardFace.FaceUp,
                        (ContainerArrivalFace)pile.arrivalFace, true, true);
                }

                if (!laid) failed++;
            }

            if (game.deck != null && game.deck.IsSet && !LayOutGameDeck(game.deck))
            {
                failed++;
                Debug.LogWarning($"[Your games] {game.name}'s deck ({game.deck.name}) is missing or has no cards.", host);
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
                        laid = host.AddToolboxComponentAtPose(TabletopComponentKind.Pawn, 0, pose, false, true).Succeeded;
                        break;
                    case SavedPiece.Token:
                        laid = host.AddToolboxComponentAtPose(TabletopComponentKind.Token, 0, pose, false, true).Succeeded;
                        break;
                    case SavedPiece.Die:
                        laid = ToolboxComponentDefinitions.IsSupportedDieSideCount(piece.sides)
                            && host.AddToolboxComponentAtPose(TabletopComponentKind.Die, piece.sides, pose, false, true).Succeeded;
                        break;
                    default:
                        List<ObjectDefinitionId> card = CardIds(new List<string> { piece.definitionId });
                        laid = card.Count == 1 && host.CommitRealCardsPlacement("card", card, pose,
                            piece.faceDown ? CardFace.FaceDown : CardFace.FaceUp, ContainerArrivalFace.Unchanged, false, true);
                        break;
                }

                if (!laid) failed++;
            }

            host.LoadTableMarks(table.marks); // drawing tool: the game's drawings, part of its start
            host.SessionService.ShowActiveSessionUi(); // the chip shows the game's name and the rules card its rules
            host.DealAtGameStart(game, gameDeckContainerId); // DEAL-1: a game that deals when it starts
            host.ShowMessage(failed == 0
                ? $"{game.name} is ready. Its rules are on the rules card."
                : $"{game.name} is ready, but {failed} piece{(failed == 1 ? string.Empty : "s")} could not be laid out.");
        }

        internal static List<ObjectDefinitionId> CardIds(List<string> ids)
        {
            List<ObjectDefinitionId> cards = new List<ObjectDefinitionId>(ids != null ? ids.Count : 0);
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                if (Guid.TryParse(ids[i], out Guid id) && id != Guid.Empty) cards.Add(new ObjectDefinitionId(id));
            }

            return cards;
        }

        internal static SavedPose SavePose(TabletopPose pose) => new SavedPose
        {
            x = pose.Position.X,
            y = pose.Position.Y,
            rotation = pose.RotationDegrees,
            layer = pose.Layer,
            order = pose.LocalOrder,
        };

        internal static TabletopPose LoadPose(SavedPose pose) =>
            pose == null
                ? TabletopPose.Default
                : new TabletopPose(new TableCoordinate(pose.x, pose.y), pose.rotation, pose.layer, pose.order);

        internal static string TableSummary(SavedTable table)
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
            if (table.marks.Count > 0) parts.Add(CountText(table.marks.Count, "drawing"));
            return "The table: " + string.Join(", ", parts) + ".";
        }

        internal static string CountText(int count, string one, string many = null) =>
            count == 1 ? $"1 {one}" : $"{count} {many ?? one + "s"}";

        internal static string PlayersText(int minimum, int maximum) =>
            minimum == maximum ? $"{minimum} player{(minimum == 1 ? string.Empty : "s")}" : $"{minimum}–{maximum} players";

        // ---------- the rules card and the menu ----------

        // The written rules: a line starting with "#" is a heading; every other line is a numbered rule.
        internal RulesCardModel PlayerGameRulesCardModel()
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

            // DEAL-1: the game's deal rule (set in the Deal step).
            if (DealRule.TryParse(activePlayerGame.deal, out DealRule deal))
            {
                lines.Add(new RulesCardLine(RulesCardLineKind.Heading, string.Empty, "Dealing", false));
                lines.Add(new RulesCardLine(RulesCardLineKind.Rule, "1", deal.Describe(), false));
            }

            return new RulesCardModel(activePlayerGame.name, false, lines);
        }

        internal GameSelectModel PlayerGameSelectModel(GameSelectEntry entry, List<CardEditorRowModel> rows, string status)
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
                    ? host.GameSelectPanel.GameDetail(entry)
                    : $"{host.GameSelectPanel.GameDetail(entry)} · {game.description}",
                ShowMode = false,
                Rules = rules == 0 ? "Rules: none written yet" : $"Rules: {CountText(rules, "rule")} written by you",
                Deck = game.deck != null && game.deck.IsSet
                    ? $"Deck: {game.deck.name}, shuffled at Start. {TableSummary(game.table ?? new SavedTable())}"
                    : TableSummary(game.table ?? new SavedTable()),
                CanChangeDeck = false,
                ProfileName = host.EnsureProfile().Active.displayName,
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
        internal void DeleteSelectedPlayerGame()
        {
            List<GameSelectEntry> entries = host.GameSelectPanel.GameSelectEntries();
            GameSelectEntry entry = GameSelectPresenter.FindGameSelectEntry(entries, host.GameSelectPanel.gameSelectEntryId);
            if (entry?.PlayerGame == null) return;
            if (deleteArmedGameId != entry.PlayerGame.id)
            {
                deleteArmedGameId = entry.PlayerGame.id;
                host.GameSelectPanel.ShowGameSelect($"Delete {entry.Name}? Press Delete again; this cannot be undone.", entries);
                return;
            }

            deleteArmedGameId = string.Empty;
            if (!PlayerGames().Delete(entry.PlayerGame.id, out string error))
            {
                host.GameSelectPanel.ShowGameSelect($"{entry.Name} could not be deleted: {error}.", entries);
                return;
            }

            if (activePlayerGame != null && activePlayerGame.id == entry.PlayerGame.id) activePlayerGame = null;
            host.GameSelectPanel.gameSelectEntryId = GameSelectPresenter.EmptyTableEntryId;
            host.GameSelectPanel.ShowGameSelect($"Deleted {entry.Name}.");
        }
    }
}
