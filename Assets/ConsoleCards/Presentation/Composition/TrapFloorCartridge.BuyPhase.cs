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
    /// Rules v1.151126 Buy Phase (owner's choices, 2026-11-16):
    /// <list type="bullet">
    /// <item>Before round 1 of every floor and after every collapse, every player still in buys at the same time. The
    /// player who plays next is kept; nobody has the turn until everyone has pressed Done Buying.</item>
    /// <item>The computer that deals (offline, or the host) collects every Console Card (spent and unspent), shuffles
    /// the shared deck, deals 10 to each player still in and shuffles the used Abilities back into the Ability deck.
    /// In Free nothing is done for the players: they do it by hand.</item>
    /// <item>Each player draws 3 Abilities, buys what they can pay for, and presses Done Buying: the rest of their draw
    /// goes back into the Ability deck. Online each player buys on their own computer; a computer that plays several
    /// seats has them buy one after another, in turn order (<see cref="BuyerId"/>).</item>
    /// <item>During player turns nothing is drawn (Full refuses a hand draw from the Console deck).</item>
    /// </list>
    /// Event-time; the tick costs a few field checks on idle frames and allocates nothing.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        internal bool IsBuyPhase => host.trapFloorTurnState != null && host.trapFloorTurnState.IsBuyPhase;

        /// <summary>
        /// The player buying on this computer now: in the Buy Phase the first player in turn order who plays here and has
        /// not finished buying (empty when none); outside it the turn player.
        /// </summary>
        internal PlayerId BuyerId
        {
            get
            {
                TrapFloorTurnState turn = host.trapFloorTurnState;
                if (turn == null) return PlayerId.Empty;
                if (!turn.IsBuyPhase) return turn.ActivePlayerId;
                for (int i = 0; i < turn.PlayerOrder.Count; i++)
                {
                    PlayerId player = turn.PlayerOrder[i];
                    if (turn.IsBuyingPlayer(player) && host.IsPlayerOnThisComputer(player)) return player;
                }

                return PlayerId.Empty;
            }
        }

        // A step in which the buyer here may pay for something: their own turn, or the Buy Phase.
        internal bool IsBuyingStep =>
            host.trapFloorTurnState != null
            && !host.trapFloorTurnState.IsCurrentFloorFailed
            && (host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                || host.trapFloorTurnState.Phase == TrapFloorTurnPhase.BuyPhase);

        // The player the status card shows in the Buy Phase: the buyer here, else the player who plays next.
        private PlayerId BuyPhaseCardPlayer()
        {
            PlayerId buyer = BuyerId;
            return buyer.IsEmpty ? host.trapFloorTurnState.NextPlayerId : buyer;
        }

        private string BuyPhaseText()
        {
            PlayerId buyer = BuyerId;
            if (buyer.IsEmpty) return "BUY PHASE - WAITING FOR THE OTHERS";
            return host.TryGetSeatIndex(buyer, out int seatIndex)
                ? $"BUY PHASE - {PlayerColorName(seatIndex)} BUYING"
                : $"BUY PHASE - {FormatPlayerName(buyer).ToUpperInvariant()} BUYING";
        }

        // The status card's step line (its first word names the HUD's step: "Buy" → SHOP PHASE).
        private string BuyPhaseGuidanceText()
        {
            if (IsFloorSetupOpen) return FloorSetupStatusText(); // stage 2
            PlayerId buyer = BuyerId;
            if (buyer.IsEmpty)
                return $"Buy Phase: you are done. Waiting for the others to finish buying; then {FormatPlayerName(host.trapFloorTurnState.NextPlayerId)} plays.";

            PendingControllerPurchaseState payment = host.pendingControllerPurchaseState;
            if (payment != null && payment.IsActive && payment.PlayerId == buyer)
            {
                if (payment.IsPaymentComplete) return "PAYMENT READY — Confirm Payment";
                return $"{PaymentVerb(payment)} {FindPurchaseDisplayName(payment.PurchasedCardDefinitionStableId).ToUpperInvariant()} — Place the chosen cards in your Console";
            }

            string name = FormatPlayerName(buyer);
            return IsAbilityBuyingOpen()
                ? $"Buy a drawn Ability you can pay for, then Done Buying\n{name} is buying"
                : $"Buy Phase: draw {AbilityOfferSize} Abilities (turn menu, or right-click the Ability deck), buy any you can pay for, then Done Buying\n{name} is buying";
        }

        // The turn menu in the Buy Phase.
        private void AddBuyPhaseActions(List<PopupActionOption> actions)
        {
            AddFloorSetupActions(actions); // stage 2: the players lay the floor out first
            PlayerId buyer = BuyerId;
            PendingControllerPurchaseState payment = host.pendingControllerPurchaseState;
            bool purchasePending = !buyer.IsEmpty && payment != null && payment.IsActive && payment.PlayerId == buyer;
            if (purchasePending)
            {
                actions.Add(new PopupActionOption("Confirm Payment", payment.IsPaymentComplete, ConfirmPendingActionAbilityPurchase));
                actions.Add(new PopupActionOption("Cancel Payment", true, CancelPendingActionAbilityPurchase));
            }
            else if (!buyer.IsEmpty)
            {
                if (TryGetAbilityOfferAction(out string drawLabel)) actions.Add(new PopupActionOption(drawLabel, true, OpenAbilityOffer));
                actions.Add(new PopupActionOption(BuyersHereCount() > 1 ? $"Done Buying ({FormatPlayerName(buyer)})" : "Done Buying", true, FinishBuying));
            }
            else
            {
                actions.Add(new PopupActionOption("Waiting for the others to buy", false, () => { }));
            }

            AddManualEndFloor(actions); // Free: the players end the floor by hand (owner, 2026-11-05)
        }

        private int BuyersHereCount()
        {
            TrapFloorTurnState turn = host.trapFloorTurnState;
            int count = 0;
            for (int i = 0; turn != null && i < turn.PlayerOrder.Count; i++)
            {
                if (turn.IsBuyingPlayer(turn.PlayerOrder[i]) && host.IsPlayerOnThisComputer(turn.PlayerOrder[i])) count++;
            }

            return count;
        }

        // ---------- Done Buying ----------

        internal bool CanFinishBuying() => IsBuyPhase && !BuyerId.IsEmpty && !(host.HasOnline && host.Online.IsPrivateGuest);

        // The buyer here is done: what is left of their own draw goes back into the Ability deck, then every computer
        // marks them done (the host replays it). The last one done starts the coming player's turn.
        internal void FinishBuying()
        {
            host.ContextMenus.CloseContextMenu();
            PlayerId buyer = BuyerId;
            if (!IsBuyPhase || buyer.IsEmpty) return;
            if (IsPaymentBlocking())
            {
                host.ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            host.runtimeUi?.CloseActionAbilityPurchase();
            SyncDrawWithTable();
            bool ownDraw = IsAbilityOfferTurn() && abilityOfferPlayer == buyer && dealtOffer.Count > 0;
            TrapFloorTurnAdvanceResult result;
            host.SessionService.BeginUndoBatch();
            try
            {
                // Rules v1.151126: "Immediately return all unbought Ability Cards from the offer to the Ability Deck."
                if (ownDraw) ReturnDealtOffer(buyer, false);
                result = host.TableAuthority.Submit<TrapFloorFinishBuyingCommand, TrapFloorTurnAdvanceResult>(
                    new TrapFloorFinishBuyingCommand(host.CreateCommandContext(buyer)));
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            ForgetAbilityOffer();
            if (!result.Succeeded && result.Error == TrapFloorTurnAdvanceError.FloorSetupPending)
            {
                host.ShowMessage("Done Buying cannot end the Buy Phase yet: set up the floor first (lay every tile, then Floor set up — Done).");
                RefreshTrapFloorStatusUi();
                return;
            }

            if (!result.Succeeded && result.Error != TrapFloorTurnAdvanceError.None)
            {
                host.ShowMessage($"Done Buying was refused: {result.Error}.");
                RefreshTrapFloorStatusUi();
                return;
            }

            ShowBuyPhaseProgress(buyer);
            RefreshTrapFloorStatusUi();
        }

        // Every computer: what the last Done Buying means for the table (the next buyer here, or the turn that begins).
        private void ShowBuyPhaseProgress(PlayerId finished)
        {
            TrapFloorTurnState turn = host.trapFloorTurnState;
            if (turn == null) return;
            if (!turn.IsBuyPhase)
            {
                PlayerId next = turn.ActivePlayerId;
                host.FeedbackLine(
                    $"Everyone has bought. {FormatPlayerName(next).ToUpperInvariant()}'S TURN{PlayerEffectChips(next)}: move or use an Ability.",
                    FeedbackSeverity.Info);
                return;
            }

            PlayerId buyerHere = BuyerId;
            host.ShowMessage(buyerHere.IsEmpty
                ? $"{FormatPlayerName(finished)} finished buying. Waiting for the others."
                : $"{FormatPlayerName(finished)} finished buying. {FormatPlayerName(buyerHere)}: draw {AbilityOfferSize} Abilities, buy, then Done Buying.");
        }

        // A Done Buying made on another computer (replayed here).
        private void OnBuyingFinished(TrapFloorFinishBuyingCommand command, TrapFloorTurnAdvanceResult result)
        {
            if (!host.ViewSync.Ready || !host.Replicated.Replaying) return;
            ShowBuyPhaseProgress(command.Context.RequestedByPlayerId);
            RefreshTrapFloorStatusUi();
        }

        // Host check: a joined computer finishes buying, and buys, only for its own player.
        private bool HostAllowsBuyPhaseCommand(object command, PlayerId sender)
        {
            PlayerId requester;
            switch (command)
            {
                case TrapFloorFinishBuyingCommand finish: requester = finish.Context.RequestedByPlayerId; break;
                case TrapFloorBuyFromShopCommand buy: requester = buy.Context.RequestedByPlayerId; break;
                case TrapFloorArrangeFloorCommand _:
                    hostRefusal = "The host refused that: only the host sets up the floor."; // stage 2
                    return false;
                default: return true;
            }

            if (requester == sender) return true;
            hostRefusal = "The host refused that: you can only buy for yourself.";
            return false;
        }

        // ---------- opening and dealing ----------

        // After a collapse (End Floor Turn ran here: offline, or on the host), in the same Undo step as the turn change.
        private void OpenBuyPhaseAfterCollapse()
        {
            TrapFloorTurnState turn = host.trapFloorTurnState;
            if (turn == null || turn.IsCurrentFloorFailed || turn.Phase != TrapFloorTurnPhase.PlayerTurn) return;
            turn.OpenBuyPhase();
        }

        // The computer that collects and deals: offline, or the host (a joined computer gets the result as commands).
        private bool IsDealingComputer =>
            !(host.HasOnline && (host.Online.IsJoinedSharedClient || host.Online.IsPrivateGuest));

        private MatchId buyPhaseSeenMatch;
        private int buyPhaseSeenRound = -1;
        private PlayerId buyPhaseSeenNext;
        private bool buyPhaseSeen;

        // TickEarly: notices a Buy Phase that just opened (here or on the host), and deals it once on the dealing computer.
        internal void TickBuyPhase()
        {
            TrapFloorTurnState turn = host.trapFloorTurnState;
            if (turn == null || host.matchState == null || !turn.IsBuyPhase)
            {
                buyPhaseSeen = false;
                return;
            }

            if (!buyPhaseSeen
                || buyPhaseSeenRound != turn.CurrentRound
                || buyPhaseSeenNext != turn.NextPlayerId
                || !buyPhaseSeenMatch.Equals(host.matchState.Id))
            {
                buyPhaseSeen = true;
                buyPhaseSeenRound = turn.CurrentRound;
                buyPhaseSeenNext = turn.NextPlayerId;
                buyPhaseSeenMatch = host.matchState.Id;
                OnBuyPhaseOpened();
            }

            if (turn.IsBuyPhaseDealt
                || !IsDealingComputer
                || host.undoTransactionInProgress
                || host.rebuildingFromUndo
                || host.trapFloorTemplate == null
                || !host.ViewSync.Ready)
            {
                return;
            }

            DealBuyPhase(turn);
        }

        // On every computer: a new Buy Phase. Nothing of an earlier draw is this phase's (the offer is forgotten here; the
        // dealing computer puts any card still lying out back into the deck).
        private void OnBuyPhaseOpened()
        {
            ForgetAbilityOffer();
            PlayerId buyer = BuyerId;
            if (buyer.IsEmpty) return;
            host.FeedbackLine(
                $"BUY PHASE — {FormatPlayerName(buyer)}: draw {AbilityOfferSize} Abilities (turn menu, or right-click the Ability deck), "
                    + "buy any you can pay for, then Done Buying.",
                FeedbackSeverity.Info);
            RefreshTrapFloorStatusUi();
        }

        private void ForgetAbilityOffer()
        {
            abilityOffer.Clear();
            dealtOffer.Clear();
            abilityOfferTurnState = null;
            abilityOfferRound = -1;
            abilityOfferPlayer = PlayerId.Empty;
            pendingDeckCardId = TabletopObjectId.Empty;
        }

        // Once per Buy Phase, on the dealing computer, folded into the step that opened the phase (one Undo step).
        private void DealBuyPhase(TrapFloorTurnState turn)
        {
            turn.MarkBuyPhaseDealt(); // first: nothing below runs twice
            if (PlayAssist.IsFree)
            {
                KeepBuyPhaseInUndoStep();
                host.FeedbackLine(
                    "BUY PHASE (Free): collect every Console Card, shuffle the Console deck and deal 10 to each player; put the "
                        + "used Abilities back into the Ability deck. Then each player draws 3 Abilities, buys, and presses Done Buying.",
                    FeedbackSeverity.Info);
                return;
            }

            ContainerId consoleDeckId = host.trapFloorTemplate.Players.Count > 0
                ? host.trapFloorTemplate.Players[0].ControllerDeckId
                : ContainerId.Empty;
            int usedBack = 0;
            int collected = 0;
            int dealtPlayers = 0;
            host.SessionService.BeginUndoAmend();
            try
            {
                ReturnDealtOffer(host.localPlayerId); // a draw still lying out goes back (the Ability deck is then shuffled)
                ForgetAbilityOffer();
                usedBack = ReturnUsedAbilitiesToDeck();
                if (!consoleDeckId.IsEmpty && host.matchState.Containers.TryGetValue(consoleDeckId, out ContainerState consoleDeck))
                {
                    collected = CollectConsoleCards(consoleDeckId);
                    if (consoleDeck.Count > 1) host.ShuffleDeck(consoleDeckId);
                    dealtPlayers = DealConsoleHands(turn, consoleDeckId);
                }

                BeginFloorSetupIfChosen(turn); // stage 2: the players lay the floor out themselves (FloorSetup.cs)
            }
            finally
            {
                host.SessionService.EndUndoAmend();
            }

            KeepBuyPhaseInUndoStep();
            int handSize = host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
            string used = usedBack > 0 ? $" {usedBack} used Abilit{(usedBack == 1 ? "y" : "ies")} went back into the Ability deck." : string.Empty;
            host.FeedbackLine(
                $"BUY PHASE — {collected} Console Card{(collected == 1 ? string.Empty : "s")} collected and shuffled; "
                    + $"{handSize} dealt to {dealtPlayers} player{(dealtPlayers == 1 ? string.Empty : "s")}.{used}",
                FeedbackSeverity.Info);
            host.GameplayLog($"Buy Phase: the Console deck was shuffled and {handSize} cards were dealt to each player.");
            RefreshTrapFloorStatusUi();
        }

        // The dealt flag (and anything else) is kept in the Undo step that opened the phase.
        private void KeepBuyPhaseInUndoStep()
        {
            if (!host.undoBatchActive && host.undoTrackedMatch == host.matchState && host.undoHistory.CurrentStateIndex >= 0
                && !host.undoTransactionInProgress)
                host.undoHistory.ReplaceCurrentState(host.SessionService.CaptureUndoSnapshot());
        }

        // Rules v1.151126: "Return used/discarded Ability Cards to the eligible Ability Deck and reshuffle."
        private int ReturnUsedAbilitiesToDeck()
        {
            ContainerId discardId = host.trapFloorTemplate.AbilityDiscardId;
            if (discardId.IsEmpty
                || !TryGetAbilityDeck(out ContainerState deck)
                || !host.matchState.Containers.TryGetValue(discardId, out ContainerState discard)
                || discard.Count == 0)
            {
                return 0;
            }

            List<TabletopObjectId> cards = new List<TabletopObjectId>(discard.Count);
            for (int i = 0; i < discard.Count; i++) cards.Add(discard.GetObjectAt(i));
            var starts = host.CaptureCardTransforms(cards);
            int moved = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!host.matchState.Cards.TryGetValue(cards[i], out CardInstanceState card)) continue;
                if (card.BaseState.IsUserLocked)
                    host.TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(new SetObjectLockedCommand(host.CreateCommandContext(), cards[i], false));
                if (card.Face != CardFace.FaceDown)
                    host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(host.CreateCommandContext(), cards[i], CardFace.FaceDown));
                TransferCardResult back = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                    TransferCardCommand.ToContainer(host.CreateCommandContext(), cards[i], discardId, deck.Id));
                if (back.Succeeded) moved++;
            }

            host.ViewSync.SyncContainer(deck.Id);
            host.ViewSync.SyncContainer(discardId);
            host.RefreshAcceptedCardFaces();
            host.AnimateCardsQueued(starts, 0.03f);
            if (moved > 0 && deck.Count > 1) host.ShuffleDeck(deck.Id);
            return moved;
        }

        // Rules v1.151126: "collect all spent and unspent Console Cards from all players" (hands, Consoles, the table).
        private int CollectConsoleCards(ContainerId consoleDeckId)
        {
            List<TabletopObjectId> cards = new List<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
            {
                if (TrapFloorTemplateFactory.IsConsoleCardId(pair.Key) && pair.Value.BaseState.ContainerId != consoleDeckId)
                    cards.Add(pair.Key);
            }

            if (cards.Count == 0) return 0;
            var starts = host.CaptureCardTransforms(cards);
            HashSet<ContainerId> emptied = new HashSet<ContainerId>();
            int moved = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (!host.matchState.Cards.TryGetValue(cards[i], out CardInstanceState card)) continue;
                ContainerId from = card.BaseState.ContainerId;
                if (card.BaseState.IsUserLocked)
                    host.TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(new SetObjectLockedCommand(host.CreateCommandContext(), cards[i], false));
                if (card.Face != CardFace.FaceDown)
                    host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(host.CreateCommandContext(), cards[i], CardFace.FaceDown));
                TransferCardResult back = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                    TransferCardCommand.ToContainer(host.CreateCommandContext(), cards[i], from, consoleDeckId));
                if (!back.Succeeded) continue;
                moved++;
                if (!from.IsEmpty) emptied.Add(from);
            }

            host.ViewSync.SyncContainer(consoleDeckId);
            foreach (ContainerId from in emptied) host.ViewSync.SyncContainer(from);
            host.RefreshAcceptedCardFaces();
            host.AnimateCardsQueued(starts, 0.02f);
            return moved;
        }

        // Rules v1.151126: "deal 10 Console Cards to each player" (each player still in, in turn order). Blind in Full: the
        // Blind Trap's "no Controller Cards this round" still holds, so that player is dealt nothing.
        private int DealConsoleHands(TrapFloorTurnState turn, ContainerId consoleDeckId)
        {
            int limit = host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
            int dealt = 0;
            for (int i = 0; i < turn.PlayerOrder.Count; i++)
            {
                PlayerId player = turn.PlayerOrder[i];
                if (turn.GetPlayerState(player) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound) continue;
                if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                    || !host.matchState.Containers.TryGetValue(setup.HandContainerId, out ContainerState hand)
                    || !host.matchState.Containers.TryGetValue(consoleDeckId, out ContainerState deck))
                {
                    continue;
                }

                if (PlayAssist.Level == PlayAssistLevel.Full
                    && host.trapFloorAbilityResolutionState != null
                    && host.trapFloorAbilityResolutionState.TryGetBlindStatus(player, turn.CurrentRound, out _))
                {
                    host.FeedbackLine($"BLIND — {FormatPlayerName(player)} is dealt no Console Cards this round.", FeedbackSeverity.Warning);
                    continue;
                }

                int need = Math.Min(limit - hand.Count, deck.Count);
                if (need <= 0) continue;
                var starts = host.CaptureContainerCardTransforms(consoleDeckId, setup.HandContainerId);
                DrawCardsResult result = host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(
                    host.CreateCommandContext(), // the dealing computer's own player deals (a seat played here, so it runs here)
                    consoleDeckId,
                    setup.HandContainerId,
                    need));
                if (!result.Succeeded)
                {
                    host.ApplyLayout(consoleDeckId);
                    host.ApplyLayout(setup.HandContainerId);
                    UnityEngine.Debug.LogWarning($"[BuyPhase] Dealing to {FormatPlayerName(player)} was refused: {result.Error}.");
                }
                else
                {
                    dealt++;
                }

                host.presentationTransitions.AnimateCardsFromCurrentResults(
                    starts,
                    result.Succeeded ? host.handReflowDuration : host.returnDuration,
                    result.Succeeded ? 0.035f : 0f);
            }

            return dealt;
        }

        // Full (rules v1.151126): a player's own draw from the Console deck during turns is refused; the Buy Phase deals
        // the hands.
        internal bool RefusesTurnDraw(PlayerId drawer, ContainerId source)
        {
            if (PlayAssist.Level != PlayAssistLevel.Full || drawer.IsEmpty || host.trapFloorTemplate == null || !IsControllerDeck(source))
                return false;
            bool here = host.IsPlayerOnThisComputer(drawer) && (host.ReplicatedIfMade == null || !host.ReplicatedIfMade.Replaying);
            if (here) host.FeedbackLine("Full: Console Cards are dealt in the Buy Phase (10 each); nothing is drawn during turns.", FeedbackSeverity.Warning);
            return true;
        }
    }
}
