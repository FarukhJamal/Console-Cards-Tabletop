using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// DEAL-1 (doc 30 Part H, doc 31 PL-DEAL): dealing by the game's rules. A game's rules can carry a deal rule
    /// (<see cref="DealRule"/>: typed settings <c>deal.*</c>, so the rules card shows it and a house rule can change
    /// it); a game a player made saves one in its Deal step. <b>Deal</b> on a deck or stack (menu, or T over it)
    /// follows the rule; without one, or with "Deal another way...", a small form asks (players, how many each, how many
    /// at a time, hands or table, face). Players are dealt in turn order starting left of the dealer (this screen's
    /// seat); with no seats the cards are laid out on the table in front of the pile, one pile per player. The cards
    /// fly out one by one. One Undo step.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const int DealMaximumPlayers = 8;
        private const int DealFormMaximumEach = 13;
        private const string DealSoundName = "Deal";

        private readonly DealRule dealDraft = new DealRule();
        private int dealDraftPlayers = 2;
        // A deal waiting for the number of players (a player's game that deals when it starts).
        private DealRule pendingStartDeal;
        private int pendingStartMinimum = 1;
        private int pendingStartMaximum = 1;

        private struct DealSeat
        {
            public string Name;
            public ContainerId Hand;
            public TableCoordinate Spot;
            // Where the player sits (for a card flying to a hand this screen does not show).
            public TableCoordinate Seat;
        }

        // ---------- the rule in play ----------

        // This table's deal rule: a player's game's own, else the rules in play (house rules included).
        internal bool TryGetDealRule(out DealRule rule)
        {
            rule = null;
            PlayerGameFile game = PlayerGamesPanel.activePlayerGame;
            if (game != null) return DealRule.TryParse(game.deal, out rule);
            // An Empty Table follows no game's rules (as the rules card).
            if (activeSession == null
                || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom
                || TableGame == null)
            {
                return false;
            }

            string modeStableId = ActiveCartridge.ActiveModeStableId;
            return DealRule.TryRead(RulesCardPanel.ActiveRuleSet, modeStableId, out rule);
        }

        // ---------- menus ----------

        // The deal rows of a deck's or stack's menu.
        internal void AddDealActions(List<PopupActionOption> actions, ContainerId pileId)
        {
            if (!CanDealFrom(pileId)) return;
            if (TryGetDealRule(out DealRule rule))
            {
                actions.Add(new PopupActionOption(
                    $"Deal ({rule.Summary()})",
                    true,
                    () =>
                    {
                        ContextMenus.CloseContextMenu();
                        DealFromPile(pileId, rule, DealPlayersWithoutSeats());
                    }));
                actions.Add(new PopupActionOption("Deal another way...", true, () => OpenDealForm(rule)));
                return;
            }

            actions.Add(new PopupActionOption("Deal...", true, () => OpenDealForm(null)));
        }

        internal bool CanDealFrom(ContainerId pileId)
        {
            return !pileId.IsEmpty
                && matchState != null
                && matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                && (pile.Kind == ContainerKind.Deck || pile.Kind == ContainerKind.Stack)
                && pile.Count > 0
                && !ActiveCartridge.IsAssistedControllerDeck(pileId);
        }

        private void OpenDealForm(DealRule from)
        {
            DealRule start = from ?? (TryGetDealRule(out DealRule rule) ? rule : null);
            if (start != null)
            {
                dealDraft.CountEach = start.CountEach;
                dealDraft.Packet = start.Packet;
                dealDraft.Face = start.Face;
                dealDraft.To = start.To;
            }

            dealDraft.When = DealTiming.Manual;
            ContextMenus.SetContextMenuMode(ContextMenuMode.DealForm);
        }

        // A small form in the menu: each row shows a value and a click changes it; Deal deals.
        internal void ShowDealForm()
        {
            ContainerId pileId = contextMenuContainerId;
            int seats = DealSeatsWithHands().Count;
            List<PopupActionOption> actions = new List<PopupActionOption>(7);
            if (seats > 0)
            {
                actions.Add(new PopupActionOption($"Players: {seats} (the seats at the table)", false, () => { }));
            }
            else
            {
                actions.Add(new PopupActionOption($"Players: {dealDraftPlayers}", true, () =>
                {
                    dealDraftPlayers = dealDraftPlayers >= DealMaximumPlayers ? 1 : dealDraftPlayers + 1;
                    ContextMenus.SetContextMenuMode(ContextMenuMode.DealForm);
                }));
            }

            actions.Add(new PopupActionOption(
                dealDraft.DealsAll ? "Each: all the cards, evenly" : $"Each: {dealDraft.CountEach}",
                true,
                () =>
                {
                    dealDraft.CountEach = dealDraft.DealsAll ? 1 : dealDraft.CountEach >= DealFormMaximumEach ? 0 : dealDraft.CountEach + 1;
                    ContextMenus.SetContextMenuMode(ContextMenuMode.DealForm);
                }));
            actions.Add(new PopupActionOption(
                dealDraft.Packet == 1 ? "At a time: one card" : $"At a time: {dealDraft.Packet} cards",
                true,
                () =>
                {
                    dealDraft.Packet = dealDraft.Packet >= 3 ? 1 : dealDraft.Packet + 1;
                    ContextMenus.SetContextMenuMode(ContextMenuMode.DealForm);
                }));
            actions.Add(new PopupActionOption(
                dealDraft.To == DealDestination.Hand ? "To: their hands" : "To: the table",
                true,
                () =>
                {
                    dealDraft.To = dealDraft.To == DealDestination.Hand ? DealDestination.Table : DealDestination.Hand;
                    ContextMenus.SetContextMenuMode(ContextMenuMode.DealForm);
                }));
            if (dealDraft.To == DealDestination.Table)
            {
                actions.Add(new PopupActionOption(
                    dealDraft.Face == DealFace.Down ? "Face: down" : "Face: up",
                    true,
                    () =>
                    {
                        dealDraft.Face = dealDraft.Face == DealFace.Down ? DealFace.Up : DealFace.Down;
                        ContextMenus.SetContextMenuMode(ContextMenuMode.DealForm);
                    }));
            }

            actions.Add(new PopupActionOption("Deal", true, () =>
            {
                ContextMenus.CloseContextMenu();
                DealFromPile(pileId, dealDraft.Copy(), dealDraftPlayers);
            }));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DEAL",
                "Click a line to change it. Dealt in turn order from the left of the dealer.",
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        // A player's game that deals when it starts asks how many play when the game allows several.
        internal void ShowDealPlayersMenu()
        {
            ContainerId pileId = contextMenuContainerId;
            DealRule rule = pendingStartDeal;
            List<PopupActionOption> actions = new List<PopupActionOption>(DealMaximumPlayers + 1);
            for (int players = pendingStartMinimum; rule != null && players <= pendingStartMaximum; players++)
            {
                int count = players;
                actions.Add(new PopupActionOption(
                    count == 1 ? "1 player" : $"{count} players",
                    true,
                    () =>
                    {
                        pendingStartDeal = null;
                        ContextMenus.CloseContextMenu();
                        dealDraftPlayers = count;
                        DealFromPile(pileId, rule, count);
                    }));
            }

            actions.Add(new PopupActionOption("Don't deal now", true, () =>
            {
                pendingStartDeal = null;
                ContextMenus.CloseContextMenu();
            }));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "HOW MANY PLAYERS?",
                rule != null ? rule.Describe() : string.Empty,
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        internal bool IsDealPlayersMenuAvailable() => pendingStartDeal != null && CanDealFrom(contextMenuContainerId);

        // ---------- dealing at the start (a player's game) and by key ----------

        // Called after a player's game laid out its deck: deals now when its rule says so.
        internal void DealAtGameStart(PlayerGameFile game, ContainerId deckId)
        {
            if (game == null || deckId.IsEmpty || !DealRule.TryParse(game.deal, out DealRule rule) || rule.When != DealTiming.Setup)
            {
                return;
            }

            if (!CanDealFrom(deckId)) return;
            int seats = DealSeatsWithHands().Count;
            if (seats > 0 || game.minimumPlayers >= game.maximumPlayers)
            {
                DealFromPile(deckId, rule, Mathf.Max(1, game.minimumPlayers));
                return;
            }

            pendingStartDeal = rule;
            pendingStartMinimum = Mathf.Clamp(game.minimumPlayers, 1, DealMaximumPlayers);
            pendingStartMaximum = Mathf.Clamp(game.maximumPlayers, pendingStartMinimum, DealMaximumPlayers);
            contextMenuAnchorScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = deckId;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            ContextMenus.SetContextMenuMode(ContextMenuMode.DealPlayers);
        }

        // T over a deck or stack: deal by the rule, or open the form there.
        private void DealByKey()
        {
            if (!IsPointerOnDrawablePile() || !CanDealFrom(hoverPileId)) return;
            if (TryGetDealRule(out DealRule rule))
            {
                DealFromPile(hoverPileId, rule, DealPlayersWithoutSeats());
                return;
            }

            contextMenuAnchorScreenPosition = Pointer.current != null
                ? Pointer.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = hoverPileId;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            OpenDealForm(null);
        }

        private int DealPlayersWithoutSeats()
        {
            PlayerGameFile game = PlayerGamesPanel.activePlayerGame;
            return game != null && dealDraftPlayers < game.minimumPlayers ? game.minimumPlayers : dealDraftPlayers;
        }

        // ---------- dealing ----------

        // Deals from the top of the pile: round after round, each player in turn gets the rule's packet, until each
        // has their cards (or the pile runs out). One Undo step; the cards fly out one after another.
        internal void DealFromPile(ContainerId pileId, DealRule rule, int playersWithoutSeats)
        {
            EnsureInitialized();
            if (rule == null || !CanDealFrom(pileId)
                || !matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                || !matchState.TryGetContainerPlacement(pileId, out ContainerPlacementState placement))
            {
                ShowMessage("Deal unavailable: deal from a deck or stack with cards.");
                return;
            }

            List<DealSeat> seats = DealSeatsWithHands();
            bool toHands = rule.To == DealDestination.Hand;
            string note = string.Empty;
            if (seats.Count == 0)
            {
                int players = Mathf.Clamp(playersWithoutSeats, 1, DealMaximumPlayers);
                if (toHands && players == 1 && HasLocalHand())
                {
                    seats.Add(new DealSeat { Name = "you", Hand = handContainerId });
                }
                else
                {
                    if (toHands) note = " This table has no players' hands, so each player's cards are laid in front of the pile.";
                    toHands = false;
                    AddTableSpots(seats, players, placement.Pose, pileId);
                }
            }
            else if (!toHands)
            {
                for (int i = 0; i < seats.Count; i++)
                {
                    DealSeat seat = seats[i];
                    seat.Spot = FreeSpotNear(SpotBetween(seat.Spot, placement.Pose.Position), seat.Seat, pileId);
                    seats[i] = seat;
                }
            }

            int perPlayer = rule.DealsAll ? pile.Count / seats.Count : rule.CountEach;
            if (perPlayer <= 0)
            {
                ShowMessage($"Not enough cards to deal one to each of {seats.Count} players.");
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(pileId);
            List<Transform> dealtOrder = new List<Transform>(perPlayer * seats.Count);
            List<int> dealtTo = new List<int>(perPlayer * seats.Count);
            ContainerId[] tableStacks = new ContainerId[seats.Count];
            int[] given = new int[seats.Count];
            int packet = Mathf.Max(1, rule.Packet);
            bool ranOut = false;
            SessionService.BeginUndoBatch();
            try
            {
                while (!ranOut)
                {
                    bool anyGiven = false;
                    for (int s = 0; s < seats.Count && !ranOut; s++)
                    {
                        int wanted = Mathf.Min(packet, perPlayer - given[s]);
                        for (int c = 0; c < wanted; c++)
                        {
                            if (pile.Count == 0)
                            {
                                ranOut = true;
                                break;
                            }

                            TabletopObjectId cardId = pile.ObjectIds[pile.Count - 1];
                            Registry.TryGetCardView(cardId, out CardView card);
                            bool dealt = toHands
                                ? DealToHand(pileId, seats[s].Hand)
                                : DealToTable(card, seats[s].Spot, placement.Pose.RotationDegrees, rule.Face, ref tableStacks[s]);
                            if (!dealt)
                            {
                                ranOut = true;
                                break;
                            }

                            given[s]++;
                            anyGiven = true;
                            if (card != null)
                            {
                                dealtOrder.Add(card.transform);
                                dealtTo.Add(s);
                            }
                        }
                    }

                    bool done = true;
                    for (int s = 0; s < seats.Count; s++) done &= given[s] >= perPlayer;
                    if (done || !anyGiven) break;
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            AnimateDealtCards(starts, dealtOrder, dealtTo, seats, toHands);
            int total = 0;
            for (int s = 0; s < seats.Count; s++) total += given[s];
            if (total > 0) PlayShuffleClip(DealSoundName, false);
            string to = toHands ? "their hands" : rule.Face == DealFace.Up ? "the table, face up" : "the table, face down";
            ShowMessage(ranOut
                ? $"Dealt {total} card{(total == 1 ? string.Empty : "s")} to {seats.Count} player{(seats.Count == 1 ? string.Empty : "s")} ({to}); the pile ran out.{note}"
                : $"Dealt {perPlayer} to each of {seats.Count} player{(seats.Count == 1 ? string.Empty : "s")} ({to}).{note}");
            RefreshCardContentVisibility();
        }

        // One card from the top of the pile into a hand (the table's draw, so the hand's own rules apply).
        private bool DealToHand(ContainerId pileId, ContainerId handId)
        {
            DrawCardsResult result = TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(
                new DrawCardsCommand(CreateCommandContext(), pileId, handId, 1));
            if (result.Succeeded)
            {
                ApplyLayout(pileId);
                ApplyLayout(handId);
            }

            return result.Succeeded;
        }

        // One card onto the player's pile on the table (a stack made with the first card), turned the rule's way. The
        // table's own commands, as a draw: the deal animates every card once at the end.
        private bool DealToTable(CardView card, TableCoordinate spot, float rotation, DealFace face, ref ContainerId stackId)
        {
            if (card == null || card.CardState == null) return false;
            ContainerId sourceId = card.CardState.BaseState.ContainerId;
            if (stackId.IsEmpty || !matchState.Containers.ContainsKey(stackId))
            {
                CreateTabletopComponentResult created = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                    new CreateTabletopComponentRequest(
                        CreateCommandContext(),
                        TabletopComponentKind.Stack,
                        new TabletopPose(spot, rotation, 0, 0)));
                if (!created.Succeeded) return false;
                stackId = created.ContainerId;
            }

            TransferCardResult moved = TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                TransferCardCommand.ToContainer(CreateCommandContext(), card.ObjectId, sourceId, stackId));
            if (!moved.Succeeded) return false;
            CardFace wanted = face == DealFace.Up ? CardFace.FaceUp : CardFace.FaceDown;
            if (card.CardState != null && card.CardState.Face != wanted)
            {
                TableAuthority.Submit<FlipCardCommand, FlipCardResult>(
                    new FlipCardCommand(CreateCommandContext(), card.ObjectId, wanted));
            }

            if (card.IsBound) card.ApplyAcceptedState();
            ApplyLayout(sourceId);
            ApplyLayout(stackId);
            return true;
        }

        // The cards fly from the pile one after another, in the order they were dealt. Each card's place is where the
        // table laid it out (any move already running is finished first, so it is the real place). A card for a hand
        // this screen does not show flies toward that player's seat and is put away there.
        private void AnimateDealtCards(
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts,
            List<Transform> order,
            List<int> dealtTo,
            List<DealSeat> seats,
            bool toHands)
        {
            if (order.Count == 0) return;
            FeelSettings feel = Feel;
            float speed = Mathf.Max(0.05f, feel.ShuffleStyleSpeed);
            float step = Mathf.Min(0.09f, 2.5f / order.Count) / speed;
            float flight = Mathf.Max(0.12f, feel.DrawSeconds) / speed;
            float card = WorldPerCardWidth();
            float arc = Mathf.Max(0.05f, feel.DrawArc) * card;
            for (int i = 0; i < order.Count; i++)
            {
                Transform target = order[i];
                if (target == null || !starts.TryGetValue(target, out TabletopTransformSnapshot start)) continue;
                presentationTransitions.Complete(target);
                DealSeat seat = seats[dealtTo[i]];
                bool hiddenHand = toHands && seat.Hand != handContainerId;
                if (!hiddenHand)
                {
                    presentationTransitions.AnimateFromCurrentResult(target, start, flight, arc, i * step);
                    continue;
                }

                Vector3 seatPoint = coordinateConverter.ToWorldPosition(SpotBetween(seat.Seat, PileCentre(start)));
                seatPoint.y = start.Position.y + (0.3f * card);
                presentationTransitions.AnimateThrough(
                    target,
                    start,
                    seatPoint,
                    start.Rotation,
                    i * step,
                    flight,
                    arc,
                    (i * step) + flight,
                    0.0001f,
                    0f);
            }
        }

        private TableCoordinate PileCentre(TabletopTransformSnapshot start) => coordinateConverter.ToTableCoordinate(start.Position);

        // ---------- who is dealt to ----------

        // The seats with a hand, in turn order starting left of the dealer (this screen's seat).
        private List<DealSeat> DealSeatsWithHands()
        {
            List<DealSeat> seats = new List<DealSeat>();
            if (matchState == null) return seats;
            IReadOnlyList<CartridgeSeat> gameSeats = ActiveCartridge.Seats; // CART-1b: a game's seats, in seat order
            if (gameSeats.Count > 0)
            {
                for (int i = 0; i < gameSeats.Count; i++)
                {
                    CartridgeSeat player = gameSeats[i];
                    if (player.HandContainerId.IsEmpty || !matchState.Containers.ContainsKey(player.HandContainerId)) continue;
                    TableCoordinate spot = matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                        ? SeatMovedPoint(player, seat, seat.TablePose.Position) // CART-1c: where the seat is now
                        : new TableCoordinate(0d, 0d);
                    seats.Add(new DealSeat { Name = HandOwnerName(player.HandContainerId), Hand = player.HandContainerId, Spot = spot, Seat = spot });
                }
            }
            else
            {
                foreach (SeatState seat in matchState.Seats.Values)
                {
                    if (seat == null || seat.HandContainerId.IsEmpty || !matchState.Containers.ContainsKey(seat.HandContainerId)) continue;
                    seats.Add(new DealSeat
                    {
                        Name = seat.Id.ToString(),
                        Hand = seat.HandContainerId,
                        Spot = seat.TablePose.Position,
                        Seat = seat.TablePose.Position,
                    });
                }
            }

            // Left of the dealer first: the seat after this screen's hand, round to the dealer last.
            int dealer = -1;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].Hand == handContainerId) dealer = i;
            }

            if (dealer >= 0 && seats.Count > 1)
            {
                List<DealSeat> ordered = new List<DealSeat>(seats.Count);
                for (int i = 1; i <= seats.Count; i++) ordered.Add(seats[(dealer + i) % seats.Count]);
                return ordered;
            }

            return seats;
        }

        private const int DealSpotTries = 8;

        // Without seats: one place per player in a row in front of the pile (nearer the camera side). A row where a pile
        // or a loose card already lies (an earlier deal, say) is skipped for the next row out, so piles never overlap.
        private void AddTableSpots(List<DealSeat> seats, int players, TabletopPose pile, ContainerId pileId)
        {
            double spacing = DealSpotSpacing;
            for (int row = 0; row < DealSpotTries; row++)
            {
                double y = pile.Position.Y - (spacing * 1.6d * (row + 1));
                bool free = true;
                for (int i = 0; i < players && free; i++)
                {
                    free = !IsDealSpotTaken(new TableCoordinate(pile.Position.X + RowOffset(i, players, spacing), y), pileId);
                }

                if (!free && row < DealSpotTries - 1) continue;
                for (int i = 0; i < players; i++)
                {
                    seats.Add(new DealSeat
                    {
                        Name = $"Player {i + 1}",
                        Hand = ContainerId.Empty,
                        Spot = new TableCoordinate(pile.Position.X + RowOffset(i, players, spacing), y),
                    });
                }

                return;
            }
        }

        private static double RowOffset(int index, int count, double spacing) => (index - ((count - 1) * 0.5d)) * spacing;

        private double DealSpotSpacing => Mathf.Max(0.5f, Feel.SpreadSpacing) * 1.3d;

        // A seat's place, moved sideways (across the line from the seat) when a pile or loose card already lies there.
        private TableCoordinate FreeSpotNear(TableCoordinate spot, TableCoordinate seat, ContainerId pileId)
        {
            if (!IsDealSpotTaken(spot, pileId)) return spot;
            double dx = spot.X - seat.X;
            double dy = spot.Y - seat.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            double sideX = length > 0.0001d ? -dy / length : 1d;
            double sideY = length > 0.0001d ? dx / length : 0d;
            double spacing = DealSpotSpacing;
            for (int k = 1; k < DealSpotTries; k++)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    TableCoordinate moved = new TableCoordinate(spot.X + (sideX * spacing * k * sign), spot.Y + (sideY * spacing * k * sign));
                    if (!IsDealSpotTaken(moved, pileId)) return moved;
                }
            }

            return spot;
        }

        // True when a deck, stack or pile (not the one dealt from, not a hand) or a loose card lies within a card's
        // footprint of the spot.
        private bool IsDealSpotTaken(TableCoordinate spot, ContainerId pileId)
        {
            double halfWidth = DealSpotSpacing * 0.75d;
            double halfDepth = DealSpotSpacing * 1.1d;
            foreach (KeyValuePair<ContainerId, ContainerPlacementState> pair in matchState.ContainerPlacements)
            {
                if (pair.Key == pileId || pair.Value == null) continue;
                if (!matchState.Containers.TryGetValue(pair.Key, out ContainerState container)) continue;
                if (container.Kind != ContainerKind.Deck && container.Kind != ContainerKind.Stack
                    && container.Kind != ContainerKind.DiscardPile) continue;
                TableCoordinate at = pair.Value.Pose.Position;
                if (Math.Abs(at.X - spot.X) < halfWidth && Math.Abs(at.Y - spot.Y) < halfDepth) return true;
            }

            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                if (card == null || !card.BaseState.ContainerId.IsEmpty) continue;
                TableCoordinate at = card.BaseState.Pose.Position;
                if (Math.Abs(at.X - spot.X) < halfWidth && Math.Abs(at.Y - spot.Y) < halfDepth) return true;
            }

            return false;
        }

        // A seat's place on the table: a third of the way from the seat toward the pile.
        private static TableCoordinate SpotBetween(TableCoordinate seat, TableCoordinate pile)
        {
            return new TableCoordinate(seat.X + ((pile.X - seat.X) * 0.35d), seat.Y + ((pile.Y - seat.Y) * 0.35d));
        }
    }
}
