using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Rules v1.151126, step 5 (owner, 2026-11-17): the Abilities that act on a tile the player picks. Arm and Disarm
    /// (any tile on the floor; the Engineer: 1 or 2 next to each other) and Safe Search (a face-down tile next to the
    /// pawn; the Clairvoyant looks at up to 3 and keeps 1 face up). The tiles that can be picked light up; the player
    /// right-clicks one and chooses it in the tile's menu. A tie for the Trap to rearm, and the Clairvoyant's choice of
    /// the tile to keep, are asked in a popup. The use goes to the table as one command with the picked tiles; every
    /// table works out the result for itself and turns the same tiles. Event-time only.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        private sealed class TilePick
        {
            public ConsoleCardInserted Insertion;
            public CardDefinition Definition;
            public TrapFloorAbilityEffect Effect;
            public PlayerId Player;
            public int Round;
            public TrapFloorAvatarSpecial Special;
            public int MaxPicks;
            public readonly List<TabletopObjectId> Picks = new List<TabletopObjectId>(3);
        }

        private TilePick tilePick;
        private readonly List<TabletopObjectId> tilePickCandidates = new List<TabletopObjectId>(48);

        private static bool NeedsTilePick(TrapFloorAbilityEffect effect) =>
            effect == TrapFloorAbilityEffect.Disarm || effect == TrapFloorAbilityEffect.SafeSearch;

        private static string AbilityName(TrapFloorAbilityEffect effect)
        {
            switch (effect)
            {
                case TrapFloorAbilityEffect.Disarm: return "Arm and Disarm";
                case TrapFloorAbilityEffect.SafeSearch: return "Safe Search";
                case TrapFloorAbilityEffect.Rush: return "Run";
                default: return effect.ToString();
            }
        }

        // A pick still belongs to this turn (Undo, an ended turn or a new floor cancel it).
        private bool IsTilePickCurrent() =>
            tilePick != null
            && host.trapFloorTurnState != null
            && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
            && host.trapFloorTurnState.ActivePlayerId == tilePick.Player
            && host.trapFloorTurnState.CurrentRound == tilePick.Round
            && host.matchState != null
            && host.matchState.Cards.TryGetValue(tilePick.Insertion.CardInstanceId, out CardInstanceState card)
            && card.BaseState.ContainerId == tilePick.Insertion.SlotContainerId;

        private void BeginTilePick(ConsoleCardInserted insertion, CardDefinition definition, TrapFloorAbilityEffect effect)
        {
            PlayerId player = insertion.ConsoleOwnerPlayerId.IsEmpty ? insertion.ActorPlayerId : insertion.ConsoleOwnerPlayerId;
            TrapFloorAvatarSpecial special = TrapFloorAbilityRules.SpecialOf(host.matchState, host.trapFloorTemplate, player);
            tilePick = new TilePick
            {
                Insertion = insertion,
                Definition = definition,
                Effect = effect,
                Player = player,
                Round = host.trapFloorTurnState.CurrentRound,
                Special = special,
                MaxPicks = effect == TrapFloorAbilityEffect.SafeSearch
                    ? (special == TrapFloorAvatarSpecial.Clairvoyant ? 3 : 1)
                    : (special == TrapFloorAvatarSpecial.Engineer ? 2 : 1),
            };

            CollectTilePickCandidates(tilePickCandidates);
            if (tilePickCandidates.Count == 0)
            {
                tilePick = null;
                host.ShowMessage(effect == TrapFloorAbilityEffect.SafeSearch
                    ? "Safe Search: no face-down tile next to your pawn. Nothing was used."
                    : "Arm and Disarm: no floor tile to pick. Nothing was used.");
                return;
            }

            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            // The Clairvoyant looks at every tile next to them when there are 3 or fewer: no pick is needed.
            if (special == TrapFloorAvatarSpecial.Clairvoyant && tilePickCandidates.Count <= 3)
            {
                tilePick.Picks.AddRange(tilePickCandidates);
                FinishTilePick();
                return;
            }

            ShowTilePickTargets();
            host.FeedbackLine(TilePickInstruction(), FeedbackSeverity.Info); // an instruction: always shown
        }

        private string TilePickInstruction()
        {
            if (tilePick.Effect == TrapFloorAbilityEffect.SafeSearch)
            {
                return tilePick.Special == TrapFloorAvatarSpecial.Clairvoyant
                    ? $"SAFE SEARCH (Clairvoyant) — right-click a lit tile › Search this tile ({tilePick.Picks.Count} of 3 picked). "
                        + "You see them, then keep 1 face up."
                    : "SAFE SEARCH — right-click a lit tile next to you › Search this tile. A Trap there does not affect you.";
            }

            if (tilePick.Picks.Count == 1)
                return "ARM AND DISARM (Engineer) — right-click a lit tile next to the first › Disarm this tile, "
                    + "or the first tile › Disarm just this tile.";
            return tilePick.Special == TrapFloorAvatarSpecial.Engineer
                ? "ARM AND DISARM (Engineer) — right-click any floor tile › Disarm this tile (then a 2nd one next to it, if you like)."
                : "ARM AND DISARM — right-click any floor tile › Disarm this tile. Disarming a Trap rearms the nearest triggered one.";
        }

        private void CollectTilePickCandidates(List<TabletopObjectId> into)
        {
            into.Clear();
            if (tilePick == null || host.trapFloorTemplate == null || host.matchState == null) return;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            if (tilePick.Effect == TrapFloorAbilityEffect.SafeSearch)
            {
                if (!TryGetTrapFloorPlayerSetup(tilePick.Player, out TrapFloorPlayerSetupDefinition setup)
                    || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate origin))
                {
                    return;
                }

                TrapFloorAbilityRules.SafeSearchCandidates(host.matchState, template, host.trapFloorCollapseState, origin, into);
                for (int i = into.Count - 1; i >= 0; i--)
                {
                    if (tilePick.Picks.Contains(into[i])) into.RemoveAt(i);
                }

                return;
            }

            for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= template.GridRows; y++)
            {
                for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= template.GridColumns; x++)
                {
                    if (!TrapFloorAbilityRules.IsStandable(host.matchState, template, host.trapFloorCollapseState, x, y)
                        || !template.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id)
                        || tilePick.Picks.Contains(id))
                    {
                        continue;
                    }

                    if (tilePick.Picks.Count == 1
                        && (!template.TryGetFloorCoordinate(tilePick.Picks[0], out TrapFloorCoordinate first)
                            || TrapFloorAbilityRules.Distance(first, new TrapFloorCoordinate(x, y)) != 1))
                    {
                        continue; // the Engineer's 2nd tile is next to the first
                    }

                    into.Add(id);
                }
            }
        }

        private void ShowTilePickTargets()
        {
            if (host.abilityTargetPresenter == null || tilePick == null) return;
            CollectTilePickCandidates(tilePickCandidates);
            host.abilityTargetPresenter.Show(tilePickCandidates);
        }

        // RefreshMovementAssistancePresentation: a pick in progress keeps its tiles lit (false: no pick, or it ended).
        private bool RefreshTilePickPresentation()
        {
            if (tilePick == null) return false;
            if (!IsTilePickCurrent())
            {
                tilePick = null;
                return false;
            }

            ShowTilePickTargets();
            return true;
        }

        // The floor tile's menu while a pick is in progress.
        private void AddTilePickActions(TabletopObjectId tileId, List<PopupActionOption> actions)
        {
            if (tilePick == null) return;
            if (!IsTilePickCurrent())
            {
                tilePick = null;
                return;
            }

            CollectTilePickCandidates(tilePickCandidates);
            string verb = tilePick.Effect == TrapFloorAbilityEffect.SafeSearch ? "Search this tile" : "Disarm this tile";
            if (tilePickCandidates.Contains(tileId))
                actions.Insert(0, new PopupActionOption(verb, true, () => PickTile(tileId)));
            if (tilePick.Effect == TrapFloorAbilityEffect.Disarm && tilePick.Picks.Count == 1 && tilePick.Picks[0] == tileId)
                actions.Insert(0, new PopupActionOption("Disarm just this tile", true, () =>
                {
                    host.ContextMenus.CloseContextMenu();
                    FinishTilePick();
                }));
            actions.Add(new PopupActionOption($"Cancel {AbilityName(tilePick.Effect)}", true, CancelTilePick));
        }

        private void CancelTilePick()
        {
            host.ContextMenus.CloseContextMenu();
            if (tilePick == null) return;
            string name = tilePick.Definition.DisplayName;
            tilePick = null;
            RefreshMovementAssistancePresentation();
            host.ShowMessage($"{name} stays in your Console; nothing was used.");
        }

        private void PickTile(TabletopObjectId tileId)
        {
            host.ContextMenus.CloseContextMenu();
            if (!IsTilePickCurrent())
            {
                tilePick = null;
                RefreshMovementAssistancePresentation();
                return;
            }

            tilePick.Picks.Add(tileId);
            CollectTilePickCandidates(tilePickCandidates);
            if (tilePick.Picks.Count >= tilePick.MaxPicks || tilePickCandidates.Count == 0)
            {
                FinishTilePick();
                return;
            }

            ShowTilePickTargets();
            host.FeedbackLine(TilePickInstruction(), FeedbackSeverity.Info);
        }

        // Every pick is made: ask what is still to choose, then use the Ability.
        private void FinishTilePick()
        {
            TilePick pick = tilePick;
            if (pick == null) return;
            if (pick.Effect == TrapFloorAbilityEffect.SafeSearch)
            {
                if (pick.Special == TrapFloorAvatarSpecial.Clairvoyant && pick.Picks.Count > 1)
                {
                    ShowClairvoyantKeep(pick);
                    return;
                }

                SubmitTilePick(pick, pick.Picks.ToArray(), null);
                return;
            }

            // Arm and Disarm: Traps at the same distance to rearm are the player's choice.
            List<TabletopObjectId> tied = RearmTieFor(pick, out int chooseCount);
            if (tied.Count == 0)
            {
                SubmitTilePick(pick, pick.Picks.ToArray(), null);
                return;
            }

            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(tied.Count);
            for (int i = 0; i < tied.Count; i++)
            {
                if (!host.trapFloorTemplate.TryGetFloorCardState(host.matchState, tied[i], out TrapFloorFloorCardState floor)) continue;
                host.trapFloorTemplate.TryGetFloorCoordinate(tied[i], out TrapFloorCoordinate at);
                options.Add(new FocusedCardOptionModel(
                    tied[i],
                    floor.Content.DisplayName,
                    $"Floor {at}",
                    FloorArt(tied[i]),
                    true));
            }

            ShowSelectionFor(pick.Player, new FocusedCardSelectionModel(
                "REARM WHICH TRAP?",
                chooseCount == 1
                    ? "These triggered Traps are equally near you. Pick the one to turn back face down."
                    : $"These triggered Traps are equally near you. Pick {chooseCount} to turn back face down.",
                "A rearmed Trap triggers again when a pawn lands on it.",
                "Rearm",
                options,
                chooseCount,
                selected => selected.Count == chooseCount,
                selected =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    SubmitTilePick(pick, pick.Picks.ToArray(), new List<TabletopObjectId>(selected).ToArray());
                },
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    SubmitTilePick(pick, pick.Picks.ToArray(), null); // the grid order decides
                },
                "Any"));
        }

        // The Traps a use would rearm that tie at the edge distance (empty: none, or nothing to choose).
        private List<TabletopObjectId> RearmTieFor(TilePick pick, out int chooseCount)
        {
            chooseCount = 0;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            TrapFloorAbilityResolutionState state = host.trapFloorAbilityResolutionState;
            bool disarms = false;
            for (int i = 0; i < pick.Picks.Count && !disarms; i++)
            {
                disarms = template.TryGetFloorCardState(host.matchState, pick.Picks[i], out TrapFloorFloorCardState floor)
                    && floor.Content.Category == TrapFloorFloorContentCategory.Trap
                    && (state == null || !state.IsDisarmed(pick.Picks[i]));
            }

            if (!disarms) return new List<TabletopObjectId>();
            bool engineer = pick.Special == TrapFloorAvatarSpecial.Engineer;
            TrapFloorCoordinate from;
            if (!TryGetTrapFloorPlayerSetup(pick.Player, out TrapFloorPlayerSetupDefinition setup)
                || !TryReadPawnTile(setup.PawnId, out from))
            {
                template.TryGetFloorCoordinate(pick.Picks[0], out from);
            }

            List<TrapFloorCoordinate> pawnTiles = new List<TrapFloorCoordinate>(template.Players.Count);
            for (int i = 0; i < template.Players.Count; i++)
            {
                if (TryReadPawnTile(template.Players[i].PawnId, out TrapFloorCoordinate tile)) pawnTiles.Add(tile);
            }

            List<TabletopObjectId> candidates = new List<TabletopObjectId>(8);
            TrapFloorAbilityRules.RearmCandidates(
                host.matchState, template, host.trapFloorCollapseState, state, from, engineer ? 2 : 0, pick.Picks, pawnTiles, candidates);
            return TrapFloorAbilityRules.RearmTie(template, from, candidates, engineer ? 2 : 1, out chooseCount);
        }

        // The Clairvoyant sees the tiles they searched (only on their own screen) and keeps 1 face up; the others are
        // never turned over on the table.
        private void ShowClairvoyantKeep(TilePick pick)
        {
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(pick.Picks.Count);
            for (int i = 0; i < pick.Picks.Count; i++)
            {
                if (!host.trapFloorTemplate.TryGetFloorCardState(host.matchState, pick.Picks[i], out TrapFloorFloorCardState floor)) continue;
                options.Add(new FocusedCardOptionModel(
                    pick.Picks[i],
                    floor.Content.DisplayName,
                    $"Floor {floor.Coordinate}",
                    FloorArt(pick.Picks[i]),
                    true));
            }

            ShowSelectionFor(pick.Player, new FocusedCardSelectionModel(
                "SAFE SEARCH — KEEP 1 FACE UP",
                "You see these tiles. Pick the one that stays face up; a Trap there is disarmed. The others stay face down.",
                "Only you see this.",
                "Keep",
                options,
                1,
                selected => selected.Count == 1,
                selected =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    SubmitTilePick(pick, new[] { selected[0] }, null);
                },
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    if (tilePick == pick) tilePick = null;
                    RefreshMovementAssistancePresentation();
                    host.ShowMessage($"{pick.Definition.DisplayName} stays in your Console; nothing was used.");
                },
                "Cancel"));
        }

        private UnityEngine.Texture2D FloorArt(TabletopObjectId floorCardId) =>
            host.matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
            && host.TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition definition)
                ? host.FrontArt(definition)
                : null;

        private void SubmitTilePick(TilePick pick, TabletopObjectId[] tiles, TabletopObjectId[] rearmChoices)
        {
            if (tilePick == pick) tilePick = null;
            if (!IsCurrentFor(pick))
            {
                RefreshMovementAssistancePresentation();
                host.ShowMessage($"{pick.Definition.DisplayName} could not be used: the turn has changed.");
                return;
            }

            ActivateAbility(pick.Insertion, pick.Definition, pick.Effect, tiles, rearmChoices);
            RefreshMovementAssistancePresentation();
        }

        private bool IsCurrentFor(TilePick pick)
        {
            TilePick kept = tilePick;
            tilePick = pick;
            bool current = IsTilePickCurrent();
            tilePick = kept;
            return current;
        }

        /// <summary>
        /// Step 5: the tiles a use turned over are turned on the screen (every computer: the result is worked out on each
        /// table). A rearmed tile turns back face down.
        /// </summary>
        internal void ShowAbilityTileChanges(TrapFloorAbilityTileChanges tiles)
        {
            if (tiles == null || host.Registry == null) return;
            bool any = false;
            for (int i = 0; i < tiles.Revealed.Count; i++)
            {
                if (!host.Registry.TryGetCardView(tiles.Revealed[i], out CardView view)) continue;
                RevealFloorCardView(view);
                any = true;
            }

            for (int i = 0; i < tiles.Rearmed.Count; i++)
            {
                if (!host.Registry.TryGetCardView(tiles.Rearmed[i], out CardView view)) continue;
                view.ApplyAcceptedState();
                host.BeginCardFlipTurn(view);
                any = true;
            }

            if (any) host.RefreshCardContentVisibility();
        }

        /// <summary>Step 5: the text that says what a use of a tile Ability did.</summary>
        private string TileChangesText(PlayerId actor, TrapFloorAbilityEffect effect, TrapFloorAbilityTileChanges tiles)
        {
            string who = FormatPlayerShortName(actor);
            if (tiles == null) return $"{who} used {AbilityName(effect)}.";
            if (effect == TrapFloorAbilityEffect.SafeSearch)
            {
                string found = tiles.Revealed.Count == 1 && host.trapFloorTemplate.TryGetFloorCardState(
                        host.matchState, tiles.Revealed[0], out TrapFloorFloorCardState floor)
                    ? floor.Content.DisplayName
                    : "a tile";
                return tiles.Disarmed.Count > 0
                    ? $"{who} used Safe Search: {found} is face up and disarmed."
                    : $"{who} used Safe Search: {found} is face up. A Trap there does not affect them; a Key or Ability there "
                        + "can be claimed on a later turn.";
            }

            if (tiles.Disarmed.Count == 0)
                return $"{who} used Arm and Disarm: not a Trap, no effect. The turn is used.";
            string text = $"{who} used Arm and Disarm: {tiles.Disarmed.Count} Trap{(tiles.Disarmed.Count == 1 ? string.Empty : "s")} disarmed";
            if (tiles.NotTrap.Count > 0) text += $" ({tiles.NotTrap.Count} picked tile was not a Trap)";
            text += tiles.Rearmed.Count == 0
                ? "; no triggered Trap to rearm."
                : $"; {tiles.Rearmed.Count} triggered Trap{(tiles.Rearmed.Count == 1 ? " was" : "s were")} rearmed (face down).";
            return text;
        }
    

        // ---------- Can it work now? (step 5) ----------

        // Jump and Safe Search: is there a tile for them now (the turn rules were checked by the caller)?
        private bool CanUseFloorActionNow(TrapFloorAbilityEffect effect, PlayerId player, out string what)
        {
            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate origin))
            {
                what = $"{AbilityName(effect)} is used from a floor tile: put your pawn on the floor first.";
                return false;
            }

            TrapFloorAvatarSpecial special = TrapFloorAbilityRules.SpecialOf(host.matchState, host.trapFloorTemplate, player);
            List<TabletopObjectId> tiles = new List<TabletopObjectId>(8);
            if (effect == TrapFloorAbilityEffect.Jump)
            {
                TrapFloorAbilityRules.JumpTargets(
                    host.matchState, host.trapFloorTemplate, host.trapFloorCollapseState, origin,
                    special == TrapFloorAvatarSpecial.Athlete, tiles);
                what = tiles.Count == 0
                    ? (special == TrapFloorAvatarSpecial.Athlete
                        ? "Jump needs 1 or 2 missing tiles in a row next to you, with a tile to land on behind them."
                        : "Jump needs a missing tile next to you, with a tile to land on behind it.")
                    : "Jump over the hole to a lit tile, instead of your normal move.";
                return tiles.Count > 0;
            }

            TrapFloorAbilityRules.SafeSearchCandidates(host.matchState, host.trapFloorTemplate, host.trapFloorCollapseState, origin, tiles);
            what = tiles.Count == 0
                ? "Safe Search needs a face-down tile next to you (no diagonals)."
                : special == TrapFloorAvatarSpecial.Clairvoyant
                ? "Look at up to 3 face-down tiles next to you and keep 1 face up (a Trap there is disarmed). "
                    + "A Key or Ability there can be claimed on a later turn."
                : "Flip 1 face-down tile next to you. A Trap there does not affect you; a Key or Ability there can be "
                    + "claimed on a later turn.";
            return tiles.Count > 0;
        }

        // Lasso (Team Play): a teammate who played no Action Ability this round (owner, 2026-11-17), while the turn
        // player's Trap waits. (It also saves a teammate whose tile collapses: by itself, TrySaveByLasso.)
        private bool CanUseLassoNow(TabletopObjectId cardId, PlayerId player, out string what)
        {
            TrapFloorTurnState turn = host.trapFloorTurnState;
            TrapFloorAbilityResolutionState state = host.trapFloorAbilityResolutionState;
            if (turn == null || state == null || turn.IsCurrentFloorFailed || turn.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                what = "Lasso is used during a teammate's turn.";
                return false;
            }

            PlayerId active = turn.ActivePlayerId;
            if (player == active)
            {
                what = "Lasso is used on a teammate: when they trigger a Trap on their turn, pull them to your tile. "
                    + "It also saves them by itself when their tile collapses.";
                return false;
            }

            if (!TrapFloorAbilityRules.AreTeammates(host.matchState, host.trapFloorTemplate, player, active))
            {
                what = $"Lasso is used only on a teammate; {FormatPlayerShortName(active)} is on the other team.";
                return false;
            }

            if (turn.GetPlayerState(player) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
            {
                what = "You are out for this floor.";
                return false;
            }

            if (UsedActionThisTurn(player, cardId))
            {
                what = "Lasso works only if you have played no Action Ability this round.";
                return false;
            }

            if (!TryGetWaitingTrap(active, false, out TrapFloorTrapResolutionRecord trap))
            {
                what = $"Lasso works when {FormatPlayerShortName(active)} triggers a Trap. Keep it until then.";
                return false;
            }

            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)
                || !TrapFloorAbilityRules.IsStandable(host.matchState, host.trapFloorTemplate, host.trapFloorCollapseState, tile.X, tile.Y))
            {
                what = "Your pawn must stand on a floor tile to pull a teammate to it.";
                return false;
            }

            what = $"Pull {FormatPlayerShortName(active)} to your tile {tile}: {trap.Content.DisplayName} does not apply to them.";
            return true;
        }

        // ---------- Lasso (Team Play) ----------

        // The Lasso cards the players on this computer (not the turn player) could use now on the turn player's Trap.
        private void CollectUsableLassos(List<PlayerId> users, List<TabletopObjectId> cards, List<CardDefinition> definitions)
        {
            if (host.trapFloorTemplate == null || host.trapFloorTurnState == null || host.matchState == null) return;
            PlayerId active = host.trapFloorTurnState.ActivePlayerId;
            List<TabletopObjectId> held = new List<TabletopObjectId>(4);
            List<CardDefinition> heldDefinitions = new List<CardDefinition>(4);
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                if (!host.matchState.Seats.TryGetValue(host.trapFloorTemplate.Players[i].SeatId, out var seat)) continue;
                PlayerId player = seat.OccupantPlayerId;
                if (player.IsEmpty || player == active || !host.IsPlayerOnThisComputer(player)) continue;
                held.Clear();
                heldDefinitions.Clear();
                CollectHeldAbilities(player, false, held, heldDefinitions);
                // A bought Lasso lies beside the player's Console, out of any pile.
                foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
                {
                    if (!TrapFloorAbilityRules.IsLooseAbilityOf(host.matchState, host.trapFloorTemplate, player, pair.Key, pair.Value)
                        || !host.TryGetAuthoredCardDefinition(pair.Value.BaseState.DefinitionId, out CardDefinition loose))
                    {
                        continue;
                    }

                    held.Add(pair.Key);
                    heldDefinitions.Add(loose);
                }
                for (int c = 0; c < held.Count; c++)
                {
                    if (TrapFloorAbilityResolutionService.ResolveEffect(heldDefinitions[c].EffectMetadata) != TrapFloorAbilityEffect.Lasso
                        || !CanUseAbilityNow(TrapFloorAbilityEffect.Lasso, held[c], player, out _))
                    {
                        continue;
                    }

                    users.Add(player);
                    cards.Add(held[c]);
                    definitions.Add(heldDefinitions[c]);
                }
            }
        }

        // The Trap tile's menu: a teammate on this computer may Lasso the turn player off it.
        private void AddLassoActions(TabletopObjectId tileId, List<PopupActionOption> actions)
        {
            if (host.trapFloorAbilityResolutionState == null
                || host.trapFloorTurnState == null
                || !host.trapFloorAbilityResolutionState.TryGetTrap(tileId, out TrapFloorTrapResolutionRecord trap)
                || !trap.IsPending
                || trap.AffectedPlayerId != host.trapFloorTurnState.ActivePlayerId)
            {
                return;
            }

            List<PlayerId> users = new List<PlayerId>(2);
            List<TabletopObjectId> cards = new List<TabletopObjectId>(2);
            List<CardDefinition> definitions = new List<CardDefinition>(2);
            CollectUsableLassos(users, cards, definitions);
            for (int i = 0; i < cards.Count; i++)
            {
                TabletopObjectId card = cards[i];
                CardDefinition definition = definitions[i];
                actions.Insert(0, new PopupActionOption($"Lasso ({FormatPlayerShortName(users[i])})", true, () =>
                {
                    host.ContextMenus.CloseContextMenu();
                    BeginAbilityUse(card, definition);
                }));
            }
        }

        // A Trap of the turn player waits: say so to a teammate on this computer who could Lasso them.
        internal void HintLasso(TabletopObjectId trapTileId)
        {
            List<PlayerId> users = new List<PlayerId>(2);
            List<TabletopObjectId> cards = new List<TabletopObjectId>(2);
            List<CardDefinition> definitions = new List<CardDefinition>(2);
            CollectUsableLassos(users, cards, definitions);
            if (users.Count == 0
                || host.trapFloorAbilityResolutionState == null
                || !host.trapFloorAbilityResolutionState.TryGetTrap(trapTileId, out TrapFloorTrapResolutionRecord trap)
                || !trap.IsPending)
            {
                return;
            }

            host.FeedbackLine(
                $"{FormatPlayerShortName(users[0])} can LASSO {FormatPlayerShortName(trap.AffectedPlayerId)} off this Trap: "
                + "right-click the Trap tile › Lasso.",
                FeedbackSeverity.Info);
        }

        // The player who holds this card (hand, beside the Console, or a Console slot); empty: nobody.
        private PlayerId HolderOf(TabletopObjectId cardId)
        {
            if (host.trapFloorTemplate == null || host.matchState == null
                || !host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card))
            {
                return PlayerId.Empty;
            }

            ContainerId at = card.BaseState.ContainerId;
            if (at.IsEmpty) return card.BaseState.OwnerPlayerId; // a bought card lying beside its owner's Console
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                if (at != setup.HandContainerId && at != setup.ActionAbilityAreaContainerId && !IsConsoleSlotOf(setup, at)) continue;
                return host.matchState.Seats.TryGetValue(setup.SeatId, out var seat) ? seat.OccupantPlayerId : PlayerId.Empty;
            }

            return PlayerId.Empty;
        }

        // ---------- Saved from a fall (Insurance; the Strategist's Shield; a teammate's Lasso) ----------

        // The tile under the player collapsed: a card of theirs keeps them (they stay on the hole and step off on their
        // next turn), else a teammate's Lasso pulls them to the teammate's tile (owner, 2026-11-17). False: nothing saves them.
        private bool TrySaveFromCollapse(PlayerId player, TrapFloorCoordinate tile, PlayerId actor)
        {
            if (host.trapFloorTemplate == null || !host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId hole)) return false;
            TabletopObjectId card = EffectExecutor().SaveCardOf(host.matchState, player);
            if (card.IsEmpty) return TrySaveByLasso(player, tile, hole, actor);
            TrapFloorEffectResult saved = host.TableAuthority.Submit<TrapFloorSaveFromFallCommand, TrapFloorEffectResult>(
                new TrapFloorSaveFromFallCommand(LocalContextFor(actor), player, hole, card));
            if (!saved.Succeeded) return false;
            AnnounceFallSaved(player, card, $"the floor collapsed under them at {tile}; they stay on the hole and step off next turn");
            return true;
        }

        // A teammate who holds a Lasso, used no Action Ability this round and stands on a tile pulls the player there.
        private bool TrySaveByLasso(PlayerId player, TrapFloorCoordinate tile, TabletopObjectId hole, PlayerId actor)
        {
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                if (!host.matchState.Seats.TryGetValue(setup.SeatId, out var seat)) continue;
                PlayerId holder = seat.OccupantPlayerId;
                TabletopObjectId lasso = EffectExecutor().LassoSaverOf(host.matchState, player, holder);
                if (lasso.IsEmpty
                    || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate holderTile)
                    || !host.trapFloorTemplate.TryGetFloorCardId(holderTile, out TabletopObjectId target))
                {
                    continue;
                }

                TrapFloorEffectResult saved = host.TableAuthority.Submit<TrapFloorSaveFromFallCommand, TrapFloorEffectResult>(
                    new TrapFloorSaveFromFallCommand(LocalContextFor(actor), player, hole, lasso, holder, target));
                if (!saved.Succeeded) continue;
                AnnounceFallSaved(player, lasso,
                    $"{FormatPlayerShortName(holder)} pulls them off the collapsing tile at {tile} to {holderTile}");
                PullToLassoTile(player, target);
                return true;
            }

            return false;
        }

        // The computer that plays the pulled player moves the pawn to the Lasso holder's tile.
        private void PullToLassoTile(PlayerId player, TabletopObjectId target)
        {
            if (!host.IsPlayerOnThisComputer(player)) return;
            if (!TryAutoMovePawnToFloor(player, target, out _))
                host.FeedbackLine($"Move {FormatPlayerName(player)}'s pawn to the Lasso's tile by hand.", FeedbackSeverity.Warning);
        }

        private void AnnounceFallSaved(PlayerId player, TabletopObjectId card, string why)
        {
            string name = host.matchState.Cards.TryGetValue(card, out CardInstanceState state)
                && host.TryGetAuthoredCardDefinition(state.BaseState.DefinitionId, out CardDefinition definition)
                    ? definition.DisplayName
                    : "An Ability";
            host.FeedbackLine($"{name.ToUpperInvariant()} saves {FormatPlayerName(player).ToUpperInvariant()}: {why}.", FeedbackSeverity.Gain);
            if (PlayAssist.Level == PlayAssistLevel.Full) QueueUsedAbilityDiscard(card);
            else if (PlayAssist.Level == PlayAssistLevel.Reminders)
                host.FeedbackLine($"Reminder: {name} is used now: put it on the ABILITY DISCARD pile.", FeedbackSeverity.Info);
            RefreshTrapFloorStatusUi();
        }

        // A save made on another computer: this screen says so too.
        private void OnFallSaved(TrapFloorSaveFromFallCommand command, TrapFloorEffectResult result)
        {
            if (!result.Succeeded || !host.ViewSync.Ready || !host.Replicated.Replaying) return;
            host.FeedbackLine($"{FormatPlayerName(command.PlayerId).ToUpperInvariant()} is saved from the fall.", FeedbackSeverity.Gain);
            if (command.IsLasso) PullToLassoTile(command.PlayerId, command.TargetFloorCardId);
            RefreshTrapFloorStatusUi();
        }

        // A use made on another computer: the tiles it turned over are turned here too.
        private void OnAbilityUsed(TrapFloorUseAbilityCommand command, TrapFloorAbilityActivationResult result)
        {
            OnEffectAccepted();
            if (!result.Succeeded || !host.ViewSync.Ready || !host.Replicated.Replaying) return;
            ShowAbilityTileChanges(result.Tiles);
            RefreshNeutralizedTrapPresentation();
            if (NeedsTilePick(result.Effect))
                host.FeedbackLine(TileChangesText(command.Context.RequestedByPlayerId, result.Effect, result.Tiles), FeedbackSeverity.Info);
        }
    }
}
