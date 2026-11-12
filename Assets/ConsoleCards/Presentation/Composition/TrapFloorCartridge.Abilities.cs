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
            // USE-1 (owner, 2026-10-22: "why can't I use my Ability when I didn't move"): the turn is judged by the
            // Console's owner, not by the seat shown on screen (one screen plays every seat).
            PlayerId player = insertion.ConsoleOwnerPlayerId.IsEmpty ? insertion.ActorPlayerId : insertion.ConsoleOwnerPlayerId;
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
            ShowSelectionFor(player, new FocusedCardSelectionModel(
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
            ShowSelectionFor(player, new FocusedCardSelectionModel(
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

        // The first empty side slot, then the Main slot (D3: it is kept for the Avatar the player chooses); empty when
        // every slot is taken.
        private ContainerId FreeConsoleSlot(TrapFloorPlayerSetupDefinition setup)
        {
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
            {
                if (IsEmptySlot(setup.SideSlotContainerIds[i])) return setup.SideSlotContainerIds[i];
            }

            if (IsEmptySlot(setup.MainSlotContainerId)) return setup.MainSlotContainerId;
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
        // P8A (owner, 2026-11-08, as at the real table): this turn's draw lies dealt face up beside the buyer's Console,
        // locked (it is bought, never picked up into a hand); what is not bought goes back into the deck at End Turn.
        private readonly HashSet<TabletopObjectId> dealtOffer = new HashSet<TabletopObjectId>();

        // A card of this turn's draw that can still be bought: still in the deck, or dealt and still lying out.
        // R4: a dealt card is for sale only while nobody owns it (the purchase gives it its owner).
        private bool IsOfferCardAvailable(ContainerState deck, TabletopObjectId cardId) =>
            deck.Contains(cardId)
            || (dealtOffer.Contains(cardId) && host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                && card.BaseState.ContainerId.IsEmpty && card.BaseState.OwnerPlayerId.IsEmpty)
            || IsUnboughtInOwnConsole(cardId);

        // Owner, 2026-11-12 (Free): a drawn Ability deck card nobody has bought, put by the turn player into one of
        // their own Console slots: that is how a player chooses it by hand.
        internal bool IsUnboughtInOwnConsole(TabletopObjectId cardId)
        {
            if (!TrapFloorTemplateFactory.IsAbilityDeckCardId(cardId) || host.trapFloorTurnState == null
                || !host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card) || !card.BaseState.OwnerPlayerId.IsEmpty
                || card.BaseState.ContainerId.IsEmpty
                || !TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition setup))
            {
                return false;
            }

            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
                if (setup.SideSlotContainerIds[i] == card.BaseState.ContainerId) return true;
            return false;
        }

        // Deals this turn's draw out of the deck: face up, locked, in a row beside the buyer's Console (one Undo step).
        private void DealAbilityOffer(ContainerState deck, PlayerId buyer)
        {
            if (!TryGetTrapFloorPlayerSetup(buyer, out TrapFloorPlayerSetupDefinition setup)) return;
            TabletopPose basePose = setup.AbilityOverflowPose;
            double radians = basePose.RotationDegrees * Math.PI / 180d;
            double acrossX = Math.Sin(radians), acrossY = Math.Cos(radians);
            double alongX = Math.Cos(radians), alongY = -Math.Sin(radians);
            // Owner, 2026-11-10: separate cards clear of the Console and the turn cartridge, sliding out of the deck.
            List<TableCoordinate> spots = new List<TableCoordinate>(abilityOffer.Count);
            bool placed = host.TryAbilityDealSpots(buyer, abilityOffer.Count, spots);
            var starts = host.CaptureCardTransforms(new List<TabletopObjectId>(abilityOffer)); // only the dealt cards slide
            bool batch = !host.undoBatchActive;
            if (batch) host.SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < abilityOffer.Count; i++)
                {
                    TabletopObjectId cardId = abilityOffer[i];
                    if (!deck.Contains(cardId)) continue;
                    TableCoordinate at = placed
                        ? spots[i]
                        : new TableCoordinate(
                            basePose.Position.X - (acrossX * 1.3d) + (alongX * i * 0.6d),
                            basePose.Position.Y - (acrossY * 1.3d) + (alongY * i * 0.6d));
                    TabletopPose pose = new TabletopPose(at, basePose.RotationDegrees, basePose.Layer, basePose.LocalOrder + i);
                    TransferCardResult moved = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(
                        new TransferCardCommand(host.CreateCommandContext(buyer), cardId, deck.Id, ContainerId.Empty, pose));
                    if (!moved.Succeeded) continue;
                    host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(host.CreateCommandContext(buyer), cardId, CardFace.FaceUp));
                    // Full keeps the draw where it was dealt (locked); Reminders and Free leave the cards in the players' hands
                    // to handle (owner, 2026-11-12: "players retain control over physical card handling").
                    if (PlayAssist.Level == PlayAssistLevel.Full)
                        host.TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(new SetObjectLockedCommand(host.CreateCommandContext(buyer), cardId, true));
                    dealtOffer.Add(cardId);
                    host.PresentAcceptedObject(cardId);
                }
            }
            finally
            {
                if (batch) host.SessionService.EndUndoBatch();
            }

            host.ViewSync.SyncContainer(deck.Id);
            host.RefreshAcceptedCardFaces();
            host.SlideCards(starts, AbilityDealSlideSeconds, AbilityDealStagger);
        }

        private const float AbilityDealSlideSeconds = 0.45f;
        private const float AbilityDealStagger = 0.12f;

        internal bool HasDrawLyingOut() => !PlayAssist.IsFree && FindDrawLyingOut().Count > 0;

        // P8A audit (reconnect / rebuild): the draw is known from the table, not only from this computer's memory.
        private List<TabletopObjectId> FindDrawLyingOut()
        {
            List<TabletopObjectId> found = new List<TabletopObjectId>();
            if (host.matchState == null || host.trapFloorTemplate == null || !host.IsAbilityDeck(host.trapFloorTemplate.AbilityDeckId)) return found;
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
            {
                CardInstanceState card = pair.Value;
                // R4 (owner, 2026-11-12): by the table's own state, never by place or lock: a card of the Ability deck, out of
                // every pile, face up, that nobody owns (a bought card has its buyer as owner; starting Abilities, prizes
                // and Shop copies are not Ability deck cards).
                if (!TrapFloorTemplateFactory.IsAbilityDeckCardId(pair.Key)) continue;
                if (!card.BaseState.ContainerId.IsEmpty || !card.BaseState.OwnerPlayerId.IsEmpty || card.Face != CardFace.FaceUp) continue;
                if (TryGetDeckCardDefinition(pair.Key, out _)) found.Add(pair.Key);
            }

            return found;
        }

        private bool SyncedDraw()
        {
            SyncDrawWithTable();
            return true;
        }

        // P8A audit (Undo): the turn's draw as the table now shows it. Undo of the deal puts the cards back in the deck:
        // the draw is then forgotten, so Draw 3 deals again (the same cards, the deck order is restored). Undo of End
        // Buying puts them back out: they are this turn's draw again.
        private void SyncDrawWithTable()
        {
            if (!IsAbilityOfferTurn() || !TryGetAbilityDeck(out ContainerState deck)) return;
            List<TabletopObjectId> lying = FindDrawLyingOut();
            if (lying.Count > 0)
            {
                foreach (TabletopObjectId cardId in lying) dealtOffer.Add(cardId);
                return;
            }

            if (dealtOffer.Count == 0) return;
            foreach (TabletopObjectId cardId in dealtOffer)
                if (!deck.Contains(cardId)) return;
            dealtOffer.Clear();
            abilityOffer.Clear();
            abilityOfferTurnState = null;
            abilityOfferRound = -1;
            abilityOfferPlayer = PlayerId.Empty;
        }

        // Takes a draw that lies out as this turn's (so a reconnected player can still buy from it or End Buying, and
        // Draw 3 never deals a second row).
        private bool AdoptDrawLyingOut()
        {
            if (host.trapFloorTurnState == null) return false;
            List<TabletopObjectId> lying = FindDrawLyingOut();
            if (lying.Count == 0) return false;
            abilityOffer.Clear();
            dealtOffer.Clear();
            foreach (TabletopObjectId cardId in lying)
            {
                abilityOffer.Add(cardId);
                dealtOffer.Add(cardId);
            }

            abilityOfferTurnState = host.trapFloorTurnState;
            abilityOfferRound = host.trapFloorTurnState.CurrentRound;
            abilityOfferPlayer = host.trapFloorTurnState.ActivePlayerId;
            return true;
        }

        // End Turn: what was not bought goes back into the deck (the deck is then shuffled).
        private void ReturnDealtOffer(PlayerId owner)
        {
            // P8A audit: what this computer dealt, and any draw it did not deal that still lies out (dealt by the turn
            // player's computer before a reconnect, or held only there), so nothing stays locked on the table.
            foreach (TabletopObjectId lying in FindDrawLyingOut()) dealtOffer.Add(lying);
            if (dealtOffer.Count == 0 || !TryGetAbilityDeck(out ContainerState deck)) return;
            var starts = host.CaptureCardTransforms(dealtOffer);
            int returned = 0;
            bool batch = !host.undoBatchActive;
            if (batch) host.SessionService.BeginUndoBatch();
            try
            {
                foreach (TabletopObjectId cardId in dealtOffer)
                {
                    if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card) || !card.BaseState.ContainerId.IsEmpty) continue;
                    if (!card.BaseState.OwnerPlayerId.IsEmpty) continue; // R4: bought (it has an owner): it stays the buyer's
                    CommandContext context = host.CreateCommandContext(owner);
                    if (card.BaseState.IsUserLocked) host.TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(new SetObjectLockedCommand(context, cardId, false));
                    // Owner, 2026-11-12: face down before it goes back into the deck (the deck is hidden).
                    if (card.Face != CardFace.FaceDown)
                        host.TableAuthority.Submit<FlipCardCommand, FlipCardResult>(new FlipCardCommand(host.CreateCommandContext(owner), cardId, CardFace.FaceDown));
                    TransferCardResult back = host.TableAuthority.Submit<TransferCardCommand, TransferCardResult>(TransferCardCommand.ToContainer(host.CreateCommandContext(owner), cardId, ContainerId.Empty, deck.Id));
                    if (back.Succeeded) returned++;
                }
            }
            finally
            {
                if (batch) host.SessionService.EndUndoBatch();
            }

            dealtOffer.Clear();
            host.ViewSync.SyncContainer(deck.Id);
            host.RefreshAcceptedCardFaces();
            host.presentationTransitions.AnimateCardsFromCurrentResults(starts, AbilityDealSlideSeconds, 0.06f); // into the deck (not a loose body)
            // The deck is shuffled once, after what was returned (End Buying, or End Turn when buying was not ended; a
            // second call finds nothing to return and does not shuffle again).
            if (returned > 0 && deck.Count > 1) host.ShuffleDeck(deck.Id);
        }
        private TrapFloorTurnState abilityOfferTurnState;
        private int abilityOfferRound = -1;
        private PlayerId abilityOfferPlayer;

        // The deck card being paid for (the Shop's payment flow then buys that card out of the deck).
        internal TabletopObjectId pendingDeckCardId;

        // The same turn: the same turn state, or (owner, 2026-11-10: a client's draw never opened its window) the copy
        // of it a table rebuild hands back (an Undo step, a table received from the host) for the same game.
        private bool IsAbilityOfferTurn() =>
            host.trapFloorTurnState != null
            && abilityOfferTurnState != null
            && (ReferenceEquals(abilityOfferTurnState, host.trapFloorTurnState) || abilityOfferTurnState.MatchId.Equals(host.trapFloorTurnState.MatchId))
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
                if (IsOfferCardAvailable(deck, abilityOffer[i])) count++;
            }

            return count;
        }

        // P8A (owner, 2026-11-08): the same draw of 3 for a player on another computer (a private guest). Fills this
        // turn's offer from the top of the Ability deck as OpenAbilityOffer does and names what was drawn (for sale only).
        internal bool TryPrepareAbilityOfferFor(PlayerId player, out List<string> drawnStableIds)
        {
            drawnStableIds = new List<string>(AbilityOfferSize);
            if (PlayAssist.IsFree) return false; // owner, 2026-11-12: Free draws by hand
            if (player.IsEmpty || host.trapFloorTurnState == null || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || host.trapFloorTurnState.ActivePlayerId != player
                || IsPaymentBlocking()
                || !TryGetAbilityDeck(out ContainerState deck))
            {
                return false;
            }

            if (!IsAbilityOfferTurn())
            {
                if (deck.Count < AbilityOfferSize && AbilityDiscardCount() > 0) RefillAbilityDeckFromDiscard(deck);
                abilityOffer.Clear();
                for (int i = deck.Count - 1; i >= 0 && abilityOffer.Count < AbilityOfferSize; i--) abilityOffer.Add(deck.GetObjectAt(i));
                abilityOfferTurnState = host.trapFloorTurnState;
                abilityOfferRound = host.trapFloorTurnState.CurrentRound;
                abilityOfferPlayer = host.trapFloorTurnState.ActivePlayerId;
                DealAbilityOffer(deck, player);
            }

            for (int i = 0; i < abilityOffer.Count; i++)
            {
                if (IsOfferCardAvailable(deck, abilityOffer[i]) && TryGetDeckCardDefinition(abilityOffer[i], out CardDefinitionData definition))
                    drawnStableIds.Add(definition.StableId);
            }

            return drawnStableIds.Count > 0;
        }

        // A card of this turn's draw (still in the Ability deck) that the turn player buys: its id, else empty.
        internal TabletopObjectId PeekAbilityOfferCard(PlayerId player, string cardDefinitionStableId)
        {
            if (player.IsEmpty || !IsAbilityOfferTurn() || abilityOfferPlayer != player || !TryGetAbilityDeck(out ContainerState deck)) return TabletopObjectId.Empty;
            for (int i = 0; i < abilityOffer.Count; i++)
            {
                if (IsOfferCardAvailable(deck, abilityOffer[i]) && TryGetDeckCardDefinition(abilityOffer[i], out CardDefinitionData definition)
                    && string.Equals(definition.StableId, cardDefinitionStableId, StringComparison.Ordinal))
                    return abilityOffer[i];
            }

            return TabletopObjectId.Empty;
        }

        internal void OpenAbilityOffer()
        {
            host.ContextMenus.CloseContextMenu();
            if (IsPaymentBlocking())
            {
                host.ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            if (host.trapFloorTurnState != null && !host.IsPlayerOnThisComputer(host.trapFloorTurnState.ActivePlayerId))
            {
                host.ShowMessage("Only the player whose turn it is can buy from the Ability deck."); // B01
                return;
            }

            if (host.trapFloorTurnState != null && host.HasOnline && host.Online.IsPrivateHost
                && host.Online.IsPlayerOnAnotherComputer(host.trapFloorTurnState.ActivePlayerId))
            {
                host.ShowMessage("It is another player's turn: they draw their Abilities on their own computer.");
                return;
            }

            if (!TryGetAbilityDeck(out ContainerState deck)) return;
            if (PlayAssist.IsFree)
            {
                // Owner, 2026-11-12: in Free nothing is drawn for the player.
                host.ShowMessage("Free: draw Ability cards by hand. To buy one, put it in your Console with its payment, then CONFIRM PAYMENT.");
                return;
            }

            SyncDrawWithTable();
            if (!IsAbilityOfferTurn() && AdoptDrawLyingOut())
            {
                ShowAbilityOffer(deck); // P8A: this turn's draw already lies out (dealt before a reconnect, a rebuild)
                return;
            }

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
                DealAbilityOffer(deck, abilityOfferPlayer);
                // Owner, 2026-11-10: the cards slide out first; the window opens once they lie in front of the player.
                host.StartCoroutine(ShowAbilityOfferAfterDeal());
                return;
            }

            ShowAbilityOffer(deck);
        }

        private System.Collections.IEnumerator ShowAbilityOfferAfterDeal()
        {
            yield return new UnityEngine.WaitForSecondsRealtime(AbilityDealSlideSeconds + (AbilityDealStagger * (AbilityOfferSize - 1)) + 0.1f);
            bool turn = IsAbilityOfferTurn();
            bool deckFound = TryGetAbilityDeck(out ContainerState deck);
            int left = deckFound ? CountOfferStillInDeck(deck) : 0;
            bool paying = IsPaymentBlocking();
            if (turn && !paying && left > 0) ShowAbilityOffer(deck);
            else UnityEngine.Debug.Log($"[Abilities] The draw's window did not open: same turn {turn}, payment open {paying}, cards to buy {left} of {abilityOffer.Count}.");
        }

        // P8A (owner, 2026-11-10: "a way to end the buying phase"): the turn player is done buying; what is left of this
        // turn's draw slides back into the deck now (no second draw this turn; the deck is shuffled at End Turn).
        internal bool CanEndAbilityBuying() =>
            !PlayAssist.IsFree
            && SyncedDraw()
            && IsAbilityOfferTurn()
            && dealtOffer.Count > 0
            && TryGetAbilityDeck(out ContainerState deck)
            && CountOfferStillInDeck(deck) > 0
            && host.IsPlayerOnThisComputer(abilityOfferPlayer)
            && !(host.HasOnline && host.Online.IsPrivateGuest);

        internal void EndAbilityBuying()
        {
            host.ContextMenus.CloseContextMenu();
            if (!CanEndAbilityBuying()) return;
            if (IsPaymentBlocking())
            {
                host.ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            host.runtimeUi?.CloseActionAbilityPurchase();
            ReturnDealtOffer(abilityOfferPlayer);
            host.ShowMessage("Buying is over for this turn: the rest went back into the Ability deck. Move, then End Turn.");
            RefreshTrapFloorStatusUi();
        }

        // The buying step's line for the turn guidance (the HUD names it SHOP PHASE), while this turn's draw lies out.
        internal bool IsAbilityBuyingOpen() =>
            !PlayAssist.IsFree
            && SyncedDraw()
            && IsAbilityOfferTurn() && dealtOffer.Count > 0 && TryGetAbilityDeck(out ContainerState deck) && CountOfferStillInDeck(deck) > 0;

        // P8A (owner, 2026-11-08): this turn's draw shows in the Shop window, on every computer the same way: only the
        // drawn Abilities, each with its cost and whether the Hand can pay; Buy runs the Shop's own payment for that card.
        private void ShowAbilityOffer(ContainerState deck)
        {
            PlayerId buyer = host.trapFloorTurnState.ActivePlayerId;
            HashSet<string> drawn = new HashSet<string>(StringComparer.Ordinal);
            drawnCounts.Clear();
            for (int i = 0; i < abilityOffer.Count; i++)
            {
                if (IsOfferCardAvailable(deck, abilityOffer[i]) && TryGetDeckCardDefinition(abilityOffer[i], out CardDefinitionData definition))
                {
                    drawn.Add(definition.StableId);
                    drawnCounts[definition.StableId] = drawnCounts.TryGetValue(definition.StableId, out int n) ? n + 1 : 1;
                }
            }

            if (drawn.Count == 0 || host.runtimeUi == null)
            {
                host.ShowMessage("Nothing left in this turn's Ability draw.");
                RefreshTrapFloorStatusUi();
                return;
            }

            List<ActionAbilityPurchaseOption> options = new List<ActionAbilityPurchaseOption>();
            foreach (ActionAbilityPurchaseOption option in BuildActionAbilityPurchaseOptions())
            {
                if (drawn.Contains(option.CardDefinitionStableId)) options.Add(option);
            }

            drawnCounts.Clear();

            host.runtimeUi.ShowActionAbilityPurchase(
                options,
                stableId =>
                {
                    TabletopObjectId card = PeekAbilityOfferCard(buyer, stableId);
                    if (card.IsEmpty)
                    {
                        host.runtimeUi?.SetActionAbilityPurchaseStatus("That card is no longer in this turn's draw.");
                        return;
                    }

                    BeginDeckPurchase(card);
                },
                host.runtimeUi.CloseActionAbilityPurchase,
                "Pick one to buy. Done? Close this, then End Buying (turn menu or right-click the Ability deck): the rest goes back.",
                "ABILITY DRAW",
                "DRAWN");
        }

        // P8A (owner, 2026-11-10): two of the same Ability in the draw show as one row with its count ("Shield ×2").
        private readonly Dictionary<string, int> drawnCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        internal string DrawnCountName(CardDefinitionData definition) =>
            drawnCounts.TryGetValue(definition.StableId, out int n) && n > 1 ? $"{definition.DisplayName} ×{n}" : definition.DisplayName;

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

        // Owner, 2026-11-12 (Free, and any level where a drawn card is not locked): a drawn Ability put into the turn
        // player's own Console is chosen for buying; the existing payment then asks for its price (cards in the Console,
        // CONFIRM PAYMENT). An unbought card is never used as an Ability.
        internal bool BeginPurchaseOfInsertedCard(TabletopObjectId cardId, PlayerId actor)
        {
            if (!IsUnboughtInOwnConsole(cardId)) return false;
            if (host.trapFloorTurnState == null || actor != host.trapFloorTurnState.ActivePlayerId || !host.IsPlayerOnThisComputer(actor))
            {
                host.ShowMessage("That Ability is not bought yet: only the player whose turn it is can buy it.");
                return true;
            }

            if (IsPaymentBlocking())
            {
                if (pendingDeckCardId != cardId) host.ShowMessage("Finish or cancel the current payment first.");
                return true;
            }

            BeginDeckPurchase(cardId);
            return true;
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
            TabletopObjectId cardId = PeekPendingDeckCard(cardDefinitionStableId);
            pendingDeckCardId = TabletopObjectId.Empty;
            return cardId;
        }

        // B01: the same card without forgetting it, so a rejected purchase can be confirmed again for the same card.
        internal TabletopObjectId PeekPendingDeckCard(string cardDefinitionStableId)
        {
            TabletopObjectId cardId = pendingDeckCardId;
            return !cardId.IsEmpty
                && TryGetAbilityDeck(out ContainerState deck)
                && IsOfferCardAvailable(deck, cardId) // P8A: a dealt card is bought itself, not a Shop copy (owner, 2026-11-10)
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
        // R1 (owner, 2026-11-12): only Full cleans up by itself. Reminders says what is left; Free leaves it to the players.
        internal void EndAbilityOffer()
        {
            if (PlayAssist.Level != PlayAssistLevel.Full)
            {
                int left = FindDrawLyingOut().Count;
                if (left > 0 && PlayAssist.Level == PlayAssistLevel.Reminders)
                    host.FeedbackLine($"Reminder: {left} drawn Abilit{(left == 1 ? "y is" : "ies are")} still on the table: put back what was not bought (or End Buying), then shuffle the Ability deck.", FeedbackSeverity.Info);
                dealtOffer.Clear();
                abilityOffer.Clear();
                abilityOfferTurnState = null;
                abilityOfferRound = -1;
                abilityOfferPlayer = PlayerId.Empty;
                pendingDeckCardId = TabletopObjectId.Empty;
                return;
            }

            ReturnDealtOffer(abilityOfferPlayer); // the same clean-up as End Buying (it shuffles once, when it returned cards)
            abilityOffer.Clear();
            abilityOfferTurnState = null;
            abilityOfferRound = -1;
            abilityOfferPlayer = PlayerId.Empty;
            pendingDeckCardId = TabletopObjectId.Empty;
        }
    }
}
