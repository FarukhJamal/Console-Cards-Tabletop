using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
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
    /// Using Abilities is the player's choice, with help (owner's choices 2026-10-20):
    /// <list type="bullet">
    /// <item>Putting an Ability card into the Console asks first: what it would do right now (or why it cannot, and
    /// when it can), its cost, and <b>Use</b> / <b>Not now</b>. The card stays in the slot either way.</item>
    /// <item>When a trap the player revealed is waiting and the player holds a Reaction that can stop it (Check, Dodge,
    /// Shield; in the hand, beside the Console or in a Console slot), the game offers it: on the reveal, in the turn
    /// menu and before End Turn applies the trap. The player can always let it happen.</item>
    /// <item>Using an Ability the player owns is free (owner, 2026-10-21, as in the playtest: buying it is the payment).
    /// It is used from a Console slot; a card in the hand goes into a free slot of the player's Console first.</item>
    /// </list>
    /// Event-time only; nothing here runs per frame.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        // A card the player chose to use: when it lands in a Console slot it is used at once (no question).
        private TabletopObjectId abilityUseOnInsertCardId;

        private static bool IsAssistedAbility(TrapFloorAbilityEffect effect) =>
            effect == TrapFloorAbilityEffect.Disarm
            || effect == TrapFloorAbilityEffect.Shield
            || effect == TrapFloorAbilityEffect.Dodge
            || effect == TrapFloorAbilityEffect.Rush
            || effect == TrapFloorAbilityEffect.Check;

        private static bool IsReaction(TrapFloorAbilityEffect effect) =>
            effect == TrapFloorAbilityEffect.Check
            || effect == TrapFloorAbilityEffect.Dodge
            || effect == TrapFloorAbilityEffect.Shield;

        // ---------- Can it work now? ----------

        // Whether this Ability would do something now for the player, and what (or why not, and when it will).
        private bool CanUseAbilityNow(
            TrapFloorAbilityEffect effect,
            TabletopObjectId cardId,
            PlayerId player,
            out string what)
        {
            if (host.trapFloorTurnState == null
                || host.trapFloorAbilityResolutionState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || host.trapFloorTurnState.ActivePlayerId != player)
            {
                what = "Abilities are used on your own turn.";
                return false;
            }

            if (host.trapFloorAbilityResolutionState.HasAbilityActivatedThisTurn(
                    cardId,
                    player,
                    host.trapFloorTurnState.CurrentRound))
            {
                what = "This card was already used this turn.";
                return false;
            }

            if (effect == TrapFloorAbilityEffect.Rush)
            {
                what = "Move up to 3 spaces, even over a hole, instead of your normal move.";
                return true;
            }

            bool needsConsequence = effect == TrapFloorAbilityEffect.Shield;
            if (!TryGetWaitingTrap(player, needsConsequence, out TrapFloorTrapResolutionRecord trap))
            {
                what = effect == TrapFloorAbilityEffect.Shield
                    ? "Shield stops what a trap you revealed would do to you. Keep it until a trap is waiting; "
                        + "the game will offer it then."
                    : $"{effect} works on a trap you revealed that is still waiting. Keep it until then; "
                        + "the game will offer it.";
                return false;
            }

            string name = trap.Content.DisplayName;
            switch (effect)
            {
                case TrapFloorAbilityEffect.Check:
                    what = $"{name} becomes harmless: it hurts no one.";
                    break;
                case TrapFloorAbilityEffect.Dodge:
                    what = $"Step off {name} to an adjacent tile (they light up), so it does not hit you.";
                    break;
                case TrapFloorAbilityEffect.Shield:
                    what = $"Blocks what {name} would do to you ({TrapEffectText(trap.Content.TrapEffect)}).";
                    break;
                default:
                    what = $"Disarms {name}.";
                    break;
            }

            return true;
        }

        // The newest trap waiting for this player (with an effect the game applies, when asked).
        private bool TryGetWaitingTrap(PlayerId player, bool needsConsequence, out TrapFloorTrapResolutionRecord trap)
        {
            trap = null;
            if (host.trapFloorAbilityResolutionState == null) return false;
            IReadOnlyList<TrapFloorTrapResolutionRecord> records = host.trapFloorAbilityResolutionState.TrapRecords;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                TrapFloorTrapResolutionRecord record = records[i];
                if (record.AffectedPlayerId == player
                    && record.IsPending
                    && (!needsConsequence || record.Content.HasSupportedAssistedTrapEffect))
                {
                    trap = record;
                    return true;
                }
            }

            return false;
        }

        // ---------- The question when a card goes into the Console ----------

        private void OfferAbilityOnInsert(ConsoleCardInserted insertion, CardDefinition definition)
        {
            if (host.runtimeUi == null) return;
            TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
            PlayerId player = insertion.ActorPlayerId;
            bool usable = CanUseAbilityNow(effect, insertion.CardInstanceId, player, out string what);
            string name = definition.DisplayName;
            string title = $"USE {name.ToUpperInvariant()}?";
            string status = usable ? "You own it, so using it costs nothing." : "It stays in your Console.";
            TabletopObjectId cardId = insertion.CardInstanceId;
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(1)
            {
                new FocusedCardOptionModel(
                    cardId,
                    name,
                    usable ? "Ready" : "Not now",
                    host.FrontArt(definition),
                    usable,
                    true),
            };
            host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                title,
                what,
                status,
                "Use",
                options,
                1,
                selected => usable && selected.Count == 1,
                selected =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    BeginAbilityUse(cardId, definition);
                },
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    host.ShowMessage(usable
                        ? $"{name} stays in your Console; nothing was used."
                        : $"{name} stays in your Console. The game offers it when it can help.");
                },
                usable ? "Not now" : "OK"));
        }

        // ---------- The offer when a trap is waiting ----------

        // How many Reactions the player holds that would stop the waiting trap now.
        internal int CountUsableReactions(PlayerId player)
        {
            List<TabletopObjectId> cards = new List<TabletopObjectId>(4);
            List<CardDefinition> definitions = new List<CardDefinition>(4);
            CollectHeldAbilities(player, true, cards, definitions);
            int count = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definitions[i].EffectMetadata);
                if (CanUseAbilityNow(effect, cards[i], player, out _))
                {
                    count++;
                }
            }

            return count;
        }

        // The Reactions the player holds, each with what it would do and its cost; pick one, or let the trap happen.
        internal void ShowReactionOffer(PlayerId player)
        {
            if (host.runtimeUi == null) return;
            List<TabletopObjectId> cards = new List<TabletopObjectId>(4);
            List<CardDefinition> definitions = new List<CardDefinition>(4);
            CollectHeldAbilities(player, true, cards, definitions);
            TryGetWaitingTrap(player, false, out TrapFloorTrapResolutionRecord trap);
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(cards.Count);
            Dictionary<TabletopObjectId, CardDefinition> byCard = new Dictionary<TabletopObjectId, CardDefinition>();
            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition definition = definitions[i];
                TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
                bool usable = CanUseAbilityNow(effect, cards[i], player, out _);
                options.Add(new FocusedCardOptionModel(
                    cards[i],
                    definition.DisplayName,
                    usable ? "Can stop it" : "Cannot help now",
                    host.FrontArt(definition),
                    usable));
                byCard[cards[i]] = definition;
            }

            if (options.Count == 0)
            {
                host.ShowMessage("You hold no Reaction (Check, Dodge, Shield) right now.");
                return;
            }

            string trapName = trap != null ? trap.Content.DisplayName : "The trap";
            string effectText = trap != null ? TrapEffectText(trap.Content.TrapEffect) : string.Empty;
            host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                $"{trapName.ToUpperInvariant()} IS WAITING",
                $"If it applies: {effectText} Pick a Reaction to stop it: Check makes it harmless, Dodge steps you "
                    + "off it, Shield blocks what it does.",
                "Or let it happen: End Turn applies it.",
                "Use",
                options,
                1,
                selected => selected.Count == 1 && IsOfferOptionUsable(player, selected[0], byCard),
                selected =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    if (selected.Count == 1 && byCard.TryGetValue(selected[0], out CardDefinition chosen))
                    {
                        BeginAbilityUse(selected[0], chosen);
                    }
                },
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    host.ShowMessage($"{trapName} still waits; End Turn applies it. Use a Reaction from the turn menu "
                        + "until then.");
                },
                "Let it happen"));
        }

        private bool IsOfferOptionUsable(
            PlayerId player,
            TabletopObjectId cardId,
            Dictionary<TabletopObjectId, CardDefinition> byCard)
        {
            if (!byCard.TryGetValue(cardId, out CardDefinition definition)) return false;
            TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
            return CanUseAbilityNow(effect, cardId, player, out _);
        }

        // The Ability cards the player holds: in the hand, beside the Console, in the Console's slots.
        private void CollectHeldAbilities(
            PlayerId player,
            bool reactionsOnly,
            List<TabletopObjectId> cards,
            List<CardDefinition> definitions)
        {
            if (host.matchState == null || !TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)) return;
            CollectAbilitiesIn(setup.HandContainerId, reactionsOnly, cards, definitions);
            CollectAbilitiesIn(setup.ActionAbilityAreaContainerId, reactionsOnly, cards, definitions);
            CollectAbilitiesIn(setup.MainSlotContainerId, reactionsOnly, cards, definitions);
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
            {
                CollectAbilitiesIn(setup.SideSlotContainerIds[i], reactionsOnly, cards, definitions);
            }
        }

        private void CollectAbilitiesIn(
            ContainerId containerId,
            bool reactionsOnly,
            List<TabletopObjectId> cards,
            List<CardDefinition> definitions)
        {
            if (containerId.IsEmpty || !host.matchState.Containers.TryGetValue(containerId, out ContainerState container)) return;
            for (int i = 0; i < container.Count; i++)
            {
                TabletopObjectId cardId = container.GetObjectAt(i);
                if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                    || !host.TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition definition)
                    || definition.ConsoleBehavior != ConsoleCardBehavior.ActivateOnInsert)
                {
                    continue;
                }

                TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
                if (!IsAssistedAbility(effect) || (reactionsOnly && !IsReaction(effect))) continue;
                cards.Add(cardId);
                definitions.Add(definition);
            }
        }

        // ---------- Using ----------

        // The player chose to use this card: it is used now (owning it is enough; buying was the payment).
        private void BeginAbilityUse(TabletopObjectId cardId, CardDefinition definition)
        {
            UseAbilityCard(cardId, definition);
        }

        // Uses the card from a Console slot: where it already is, or a free slot it is put into first.
        private void UseAbilityCard(TabletopObjectId cardId, CardDefinition definition)
        {
            PlayerId player = host.trapFloorTurnState != null ? host.trapFloorTurnState.ActivePlayerId : PlayerId.Empty;
            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                || !host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card))
            {
                host.ShowMessage("That Ability cannot be used now.");
                return;
            }

            TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
            ContainerId slot = card.BaseState.ContainerId;
            if (IsConsoleSlotOf(setup, slot))
            {
                ActivateAbility(
                    new ConsoleCardInserted(
                        new DomainEventContext(host.matchState.Id, host.matchState.Revision),
                        player,
                        cardId,
                        card.BaseState.DefinitionId,
                        player,
                        slot),
                    definition,
                    effect);
                return;
            }

            // In the hand or beside the Console: into a free slot of the Console, where it is used as it lands.
            ContainerId free = FreeConsoleSlot(setup);
            abilityUseOnInsertCardId = cardId;
            if (free.IsEmpty
                || host.TransferCoordinator == null
                || !host.Registry.TryGetCardView(cardId, out CardView view)
                || !host.TransferCoordinator.Transfer(view, CardDropTarget.ForContainer(free)).Succeeded)
            {
                // Could not place it: the player puts it in; it is used as it lands.
                host.ShowMessage($"Put {definition.DisplayName} into your Console to use it.");
                return;
            }

            if (abilityUseOnInsertCardId == cardId)
            {
                // It landed without being used (no Console event): use it from there.
                abilityUseOnInsertCardId = TabletopObjectId.Empty;
                if (host.matchState.Cards.TryGetValue(cardId, out CardInstanceState placed)
                    && IsConsoleSlotOf(setup, placed.BaseState.ContainerId))
                {
                    UseAbilityCard(cardId, definition);
                }
            }
        }

        private static bool IsConsoleSlotOf(TrapFloorPlayerSetupDefinition setup, ContainerId containerId)
        {
            if (containerId.IsEmpty) return false;
            if (containerId == setup.MainSlotContainerId) return true;
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
            {
                if (setup.SideSlotContainerIds[i] == containerId) return true;
            }

            return false;
        }

        // The Main slot when empty, otherwise the first empty side slot; empty when every slot is taken.
        private ContainerId FreeConsoleSlot(TrapFloorPlayerSetupDefinition setup)
        {
            if (IsEmptySlot(setup.MainSlotContainerId)) return setup.MainSlotContainerId;
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
            {
                if (IsEmptySlot(setup.SideSlotContainerIds[i])) return setup.SideSlotContainerIds[i];
            }

            return ContainerId.Empty;
        }

        private bool IsEmptySlot(ContainerId containerId) =>
            !containerId.IsEmpty
            && host.matchState.Containers.TryGetValue(containerId, out ContainerState slot)
            && slot.Count == 0;

        // ---------- ABIL-DECK: buying from the table (owner, 2026-10-21) ----------
        // On their turn a player draws 3 from the shared Ability deck (shown in a popup; the cards stay in the deck
        // until bought), pays for any they want (the usual Hand → Console payment, then Confirm Payment) and keeps
        // those; the rest go back and the deck is shuffled when the turn ends. One draw per turn. Event-time only.
        internal const int AbilityOfferSize = 3;
        private readonly List<TabletopObjectId> abilityOffer = new List<TabletopObjectId>(AbilityOfferSize);
        private TrapFloorTurnState abilityOfferTurnState;
        private int abilityOfferRound = -1;
        private PlayerId abilityOfferPlayer;

        // The deck card being paid for (the Shop's payment flow then buys that card out of the deck).
        internal TabletopObjectId pendingDeckCardId;

        private bool IsAbilityOfferTurn() =>
            ReferenceEquals(abilityOfferTurnState, host.trapFloorTurnState)
            && host.trapFloorTurnState != null
            && abilityOfferRound == host.trapFloorTurnState.CurrentRound
            && abilityOfferPlayer == host.trapFloorTurnState.ActivePlayerId;

        private bool TryGetAbilityDeck(out ContainerState deck)
        {
            deck = null;
            return host.trapFloorTemplate != null
                && !host.trapFloorTemplate.AbilityDeckId.IsEmpty
                && host.matchState != null
                && host.matchState.Containers.TryGetValue(host.trapFloorTemplate.AbilityDeckId, out deck);
        }

        // The turn menu's entry: draw 3, or reopen this turn's draw while some of it is left.
        internal bool TryGetAbilityOfferAction(out string label)
        {
            label = null;
            if (host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || !TryGetAbilityDeck(out ContainerState deck))
            {
                return false;
            }

            if (IsAbilityOfferTurn())
            {
                int left = CountOfferStillInDeck(deck);
                if (left == 0) return false;
                label = $"Ability Draw ({left} to buy)";
                return true;
            }

            if (deck.Count == 0 && AbilityDiscardCount() == 0) return false;
            label = $"Draw {AbilityOfferSize} Abilities";
            return true;
        }

        private int CountOfferStillInDeck(ContainerState deck)
        {
            int count = 0;
            for (int i = 0; i < abilityOffer.Count; i++)
            {
                if (deck.Contains(abilityOffer[i])) count++;
            }

            return count;
        }

        internal void OpenAbilityOffer()
        {
            host.ContextMenus.CloseContextMenu();
            if (host.pendingControllerPurchaseState != null && host.pendingControllerPurchaseState.IsActive)
            {
                host.ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            if (!TryGetAbilityDeck(out ContainerState deck)) return;
            if (!IsAbilityOfferTurn())
            {
                // TABLE-1: when the Ability deck runs out, the discard pile is shuffled into it.
                if (deck.Count < AbilityOfferSize && AbilityDiscardCount() > 0) RefillAbilityDeckFromDiscard(deck);

                // The top 3 of the deck (the top is the last card).
                abilityOffer.Clear();
                for (int i = deck.Count - 1; i >= 0 && abilityOffer.Count < AbilityOfferSize; i--)
                {
                    abilityOffer.Add(deck.GetObjectAt(i));
                }

                abilityOfferTurnState = host.trapFloorTurnState;
                abilityOfferRound = host.trapFloorTurnState.CurrentRound;
                abilityOfferPlayer = host.trapFloorTurnState.ActivePlayerId;
            }

            ShowAbilityOffer(deck);
        }

        private void ShowAbilityOffer(ContainerState deck)
        {
            ContainerState hand = null;
            if (TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition setup))
            {
                host.matchState.Containers.TryGetValue(setup.HandContainerId, out hand);
            }

            ControllerInputCostEvaluator evaluator = new ControllerInputCostEvaluator();
            List<FocusedCardOptionModel> options = new List<FocusedCardOptionModel>(AbilityOfferSize);
            for (int i = 0; i < abilityOffer.Count; i++)
            {
                TabletopObjectId cardId = abilityOffer[i];
                if (!deck.Contains(cardId) || !TryGetDeckCardDefinition(cardId, out CardDefinitionData definition)) continue;
                bool canPay = hand != null
                    && evaluator.Evaluate(host.matchState, hand, definition.InputCost, host.trapFloorTemplate.GameDefinition).CanPay;
                string cost = InputCostNotation.Format(definition.InputCost);
                CardDefinition authored = null;
                if (host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card))
                {
                    host.TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out authored);
                }

                options.Add(new FocusedCardOptionModel(
                    cardId,
                    definition.DisplayName,
                    canPay ? $"{cost} — you can pay" : $"{cost} — your Hand cannot pay yet",
                    host.FrontArt(authored),
                    canPay));
            }

            if (options.Count == 0)
            {
                host.ShowMessage("Nothing left in this turn's Ability draw.");
                RefreshTrapFloorStatusUi();
                return;
            }

            host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                "ABILITY DRAW — BUY?",
                "Pick one to buy: pay its cost from your Hand through your Console. Come back for another from the "
                    + "turn menu. What you do not buy goes back, and the deck is shuffled when your turn ends.",
                $"{options.Count} drawn",
                "Buy",
                options,
                1,
                selected => selected.Count == 1,
                selected => BeginDeckPurchase(selected[0]),
                () =>
                {
                    host.runtimeUi?.CloseFocusedCardSelection();
                    RefreshTrapFloorStatusUi();
                },
                "Close"));
        }

        // The game's card behind a deck card (its cost and name).
        private bool TryGetDeckCardDefinition(TabletopObjectId cardId, out CardDefinitionData definition)
        {
            definition = null;
            if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
            IReadOnlyList<CardDefinitionData> cards = host.trapFloorTemplate.GameDefinition.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (Guid.TryParse(cards[i].StableId, out Guid id)
                    && new ObjectDefinitionId(id) == card.BaseState.DefinitionId)
                {
                    definition = cards[i];
                    return TrapFloorShop.IsForSale(definition);
                }
            }

            return false;
        }

        private void BeginDeckPurchase(TabletopObjectId cardId)
        {
            if (!TryGetDeckCardDefinition(cardId, out CardDefinitionData definition)) return;
            host.runtimeUi?.CloseFocusedCardSelection();
            pendingDeckCardId = cardId;
            TryPurchaseActionAbilityFromPopup(definition.StableId); // the Shop's payment picker, for this card
        }

        // The card to buy out of the deck for this payment, if it is still there; cleared either way.
        internal TabletopObjectId TakePendingDeckCard(string cardDefinitionStableId)
        {
            TabletopObjectId cardId = pendingDeckCardId;
            pendingDeckCardId = TabletopObjectId.Empty;
            return !cardId.IsEmpty
                && TryGetAbilityDeck(out ContainerState deck)
                && deck.Contains(cardId)
                && TryGetDeckCardDefinition(cardId, out CardDefinitionData definition)
                && string.Equals(definition.StableId, cardDefinitionStableId, StringComparison.OrdinalIgnoreCase)
                    ? cardId
                    : TabletopObjectId.Empty;
        }

        private int AbilityDiscardCount() =>
            host.trapFloorTemplate != null
            && !host.trapFloorTemplate.AbilityDiscardId.IsEmpty
            && host.matchState.Containers.TryGetValue(host.trapFloorTemplate.AbilityDiscardId, out ContainerState discard)
                ? discard.Count
                : 0;

        // Every card of the Ability discard pile back into the Ability deck, then the deck is shuffled (one Undo step).
        private void RefillAbilityDeckFromDiscard(ContainerState deck)
        {
            ContainerId discardId = host.trapFloorTemplate.AbilityDiscardId;
            if (!host.matchState.Containers.TryGetValue(discardId, out ContainerState discard)) return;
            List<TabletopObjectId> cards = new List<TabletopObjectId>(discard.Count);
            for (int i = 0; i < discard.Count; i++) cards.Add(discard.GetObjectAt(i));
            host.SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                        new TransferCardCommand(host.CreateCommandContext(), cards[i], discardId, deck.Id, null));
                }

                if (deck.Count > 1) host.ShuffleDeck(deck.Id);
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            host.ShowMessage($"The Ability deck ran out: {cards.Count} used Abilities were shuffled back into it.");
        }

        // TABLE-1 (owner, 2026-10-21): a used Ability goes on the Ability discard pile, a moment after it was used
        // (the next frame, so the Console insertion that used it has finished). Event-time; idle frames do nothing.
        private TabletopObjectId usedAbilityToDiscard;

        internal void QueueUsedAbilityDiscard(TabletopObjectId cardId) => usedAbilityToDiscard = cardId;

        internal void DiscardUsedAbility()
        {
            if (usedAbilityToDiscard.IsEmpty || host.undoTransactionInProgress) return;
            TabletopObjectId cardId = usedAbilityToDiscard;
            usedAbilityToDiscard = TabletopObjectId.Empty;
            if (host.trapFloorTemplate == null
                || host.trapFloorTemplate.AbilityDiscardId.IsEmpty
                || host.TransferCoordinator == null
                || !host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                || card.BaseState.ContainerId == host.trapFloorTemplate.AbilityDiscardId
                || !host.Registry.TryGetCardView(cardId, out CardView view))
            {
                return;
            }

            host.TransferCoordinator.Transfer(view, CardDropTarget.ForContainer(host.trapFloorTemplate.AbilityDiscardId));
        }

        // As the turn ends: the draw is over; the deck is shuffled when cards were drawn from it this turn.
        internal void EndAbilityOffer()
        {
            bool drawn = IsAbilityOfferTurn() && abilityOffer.Count > 0;
            abilityOffer.Clear();
            abilityOfferTurnState = null;
            abilityOfferRound = -1;
            abilityOfferPlayer = PlayerId.Empty;
            pendingDeckCardId = TabletopObjectId.Empty;
            if (drawn && TryGetAbilityDeck(out ContainerState deck) && deck.Count > 1)
            {
                host.ShuffleDeck(deck.Id);
            }
        }
    }
}
