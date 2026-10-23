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
using ConsoleCards.Games.TrapFloor;
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
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The Trap Floor game, the first cartridge (doc 30 A4): turns, dice, waiting Traps, landing, prizes, Keys, escape, its status texts and turn menu, the Shop and its hint helpers.
    /// (R1e: moved out of TabletopComposition; the table reaches it as <c>TrapFloorGame</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed partial class TrapFloorCartridge : IGameCartridge
    {
        private readonly TabletopComposition host;

        internal TrapFloorCartridge(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.Shop.cs ----------

        // ---------- the Shop (doc 18 §15.4) ----------

        // Open while the rule in play says so (shop.enabled, a Toggle); read live, so a change applies at once.
        internal bool IsShopOpen()
        {
            RuleSetDefinition rules = host.RulesCardPanel.ActiveRuleSet;
            if (rules == null
                || host.trapFloorTemplate == null
                || host.activeSession == null
                || host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return false;
            }

            string modeStableId = host.trapFloorTemplate.ActiveMode.StableId;
            for (int s = 0; s < rules.Sections.Count; s++)
            {
                RuleSection section = rules.Sections[s];
                if (section == null) continue;
                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry != null
                        && string.Equals(entry.SettingKey, RuleSettingKeys.ShopEnabled, StringComparison.Ordinal)
                        && entry.AppliesToMode(modeStableId))
                    {
                        return RuleSettingKeys.IsToggleOn(entry.Value);
                    }
                }
            }

            return false; // no Shop rule: no Shop
        }

        // The Shop entry on the session bar: the shop icon and the word Shop, shown while the Shop is open.
        internal void RefreshShopEntry()
        {
            if (host.runtimeUi == null) return;
            bool open = IsShopOpen();
            if (open)
            {
                // TABLE-1 check (owner, 2026-10-21: the Shop still showed): which rules opened it.
                RuleSetDefinition rules = host.RulesCardPanel.ActiveRuleSet;
                Debug.Log($"[Shop] Open: the rules in play ('{(rules != null ? rules.DisplayName : "none")}') set the Shop on.");
            }

            host.runtimeUi.ShowShopEntry(open ? (Action)OpenActionAbilityPurchase : null);
        }

        internal bool IsShopBuyingTurn() =>
            host.trapFloorTurnState != null
            && !host.trapFloorTurnState.IsCurrentFloorFailed
            && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn;

        internal void OpenActionAbilityPurchase()
        {
            host.ContextMenus.CloseContextMenu();
            if (!IsShopOpen())
            {
                host.ShowMessage("The Shop is closed in the rules in play.");
                return;
            }

            if (host.pendingControllerPurchaseState != null
                && host.pendingControllerPurchaseState.IsActive)
            {
                host.ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            pendingDeckCardId = TabletopObjectId.Empty; // a Shop purchase, not a card from the Ability deck
            ShowActionAbilityPurchase(IsShopBuyingTurn()
                ? $"{FormatPlayerName(host.trapFloorTurnState.ActivePlayerId)} is buying."
                : "You can look; buying is on a player's own turn.");
        }

        internal void ShowActionAbilityPurchase(string statusMessage)
        {
            if (host.runtimeUi == null || host.trapFloorTemplate == null)
            {
                return;
            }

            host.runtimeUi.ShowActionAbilityPurchase(
                BuildActionAbilityPurchaseOptions(),
                TryPurchaseActionAbilityFromPopup,
                host.runtimeUi.CloseActionAbilityPurchase,
                statusMessage,
                "SHOP",
                "ABILITIES");
        }

        internal void TryPurchaseActionAbilityFromPopup(string cardDefinitionStableId)
        {
            if (host.pendingControllerPurchaseState != null
                && host.pendingControllerPurchaseState.IsActive)
            {
                host.runtimeUi?.SetActionAbilityPurchaseStatus(
                    "Finish or cancel the current payment first.");
                return;
            }

            if (!IsShopBuyingTurn())
            {
                host.runtimeUi?.SetActionAbilityPurchaseStatus("Buying is on a player's own turn.");
                return;
            }

            if (!host.trapFloorTemplate.GameDefinition.TryGetCard(cardDefinitionStableId, out CardDefinitionData shopCard)
                || !TrapFloorShop.IsForSale(shopCard))
            {
                host.runtimeUi?.SetActionAbilityPurchaseStatus("Not for sale until the rules give it a price.");
                return;
            }

            if (!TryBuildPurchasePaymentOptions(
                    cardDefinitionStableId,
                    out CardDefinitionData purchasedDefinition,
                    out List<PurchasePaymentCardOption> options,
                    out string missing))
            {
                host.runtimeUi?.SetActionAbilityPurchaseStatus(
                    "This price could not be read.");
                return;
            }

            host.pendingPurchaseDefinitionStableId = cardDefinitionStableId;
            pendingPurchaseSource = PendingPurchaseSource.Shop;
            List<FocusedCardOptionModel> cards =
                new List<FocusedCardOptionModel>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                PurchasePaymentCardOption option = options[i];
                cards.Add(new FocusedCardOptionModel(
                    option.CardId,
                    option.DisplayName,
                    option.Input.HasValue ? option.Input.Value.ToString() : "Not eligible",
                    option.Artwork,
                    option.Eligible));
            }

            int requiredCardCount = purchasedDefinition.InputCost.CardCount;
            string status = string.IsNullOrEmpty(missing)
                ? "Pick the cards that pay the cost."
                : $"Missing: {missing}";
            host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                $"BUY {purchasedDefinition.DisplayName.ToUpperInvariant()} — HAND",
                $"Choose {FormatPurchaseInputCost(purchasedDefinition.InputCost)} from your Hand.",
                status,
                "Confirm Cards",
                cards,
                requiredCardCount,
                IsPurchasePaymentReady,
                ConfirmFocusedPurchasePayment,
                CancelFocusedPurchaseSelection));
        }

        internal bool TryBuildPurchasePaymentOptions(
            string cardDefinitionStableId,
            out CardDefinitionData purchasedDefinition,
            out List<PurchasePaymentCardOption> options,
            out string missingRequirements)
        {
            purchasedDefinition = null;
            options = new List<PurchasePaymentCardOption>();
            missingRequirements = string.Empty;
            if (host.trapFloorTemplate == null
                || !host.trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out purchasedDefinition)
                || purchasedDefinition.InputCost == null
                || purchasedDefinition.InputCost.IsEmpty
                || !ControllerInputCardCatalog.TryCreate(
                    host.trapFloorTemplate.GameDefinition,
                    out ControllerInputCardCatalog catalog))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!host.matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand)
                || hand.Kind != ContainerKind.Hand)
                return false;

            InputCostData cost = purchasedDefinition.InputCost;
            for (int i = 0; i < hand.Count; i++)
            {
                TabletopObjectId cardId = hand.GetObjectAt(i);
                if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
                ControllerInput? input = catalog.TryGetInput(
                    card.BaseState.DefinitionId,
                    out ControllerInput resolvedInput)
                    ? resolvedInput
                    : (ControllerInput?)null;
                bool eligible = input.HasValue && cost.Accepts(input.Value);

                CardDefinition authoredDefinition = null;
                string displayName = host.TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out authoredDefinition)
                    ? authoredDefinition.DisplayName
                    : "Controller Card";
                options.Add(new PurchasePaymentCardOption(
                    cardId,
                    input,
                    displayName,
                    host.FrontArt(authoredDefinition),
                    eligible));
            }

            // With "any one of" costs there is no single missing input, so the whole cost is named.
            missingRequirements = new ControllerInputCostEvaluator().Evaluate(
                    host.matchState,
                    hand,
                    cost,
                    host.trapFloorTemplate.GameDefinition).CanPay
                ? string.Empty
                : $"your Hand cannot pay {InputCostNotation.Format(cost)}";
            return true;
        }

        internal bool IsPurchasePaymentReady(IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (string.IsNullOrWhiteSpace(host.pendingPurchaseDefinitionStableId)
                || host.trapFloorTemplate == null
                || !host.trapFloorTemplate.GameDefinition.TryGetCard(
                    host.pendingPurchaseDefinitionStableId,
                    out CardDefinitionData purchasedDefinition))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!host.matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand))
                return false;
            for (int i = 0; i < selectedCardIds.Count; i++)
                if (!hand.Contains(selectedCardIds[i])) return false;
            return new ControllerInputCostEvaluator().EvaluateExact(
                host.matchState,
                selectedCardIds,
                purchasedDefinition.InputCost,
                host.trapFloorTemplate.GameDefinition).CanPay;
        }

        internal void ConfirmFocusedPurchasePayment(
            IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (!IsPurchasePaymentReady(selectedCardIds))
            {
                host.ShowMessage("Choose exactly the cards the cost asks for.");
                return;
            }

            string definitionId = host.pendingPurchaseDefinitionStableId;
            PlayerId playerId = host.trapFloorTurnState.ActivePlayerId;
            host.pendingControllerPurchaseState.Begin(playerId, definitionId, selectedCardIds, pendingPurchaseSource);
            host.pendingPurchaseDefinitionStableId = string.Empty;
            host.runtimeUi?.CloseFocusedCardSelection();
            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            host.ShowMessage(
                $"{PaymentVerb(host.pendingControllerPurchaseState)} {FindPurchaseDisplayName(definitionId).ToUpperInvariant()} — "
                + "Place the chosen cards in your Console, then Confirm Payment");
        }


        internal void CancelFocusedPurchaseSelection()
        {
            host.pendingPurchaseDefinitionStableId = string.Empty;
            pendingDeckCardId = TabletopObjectId.Empty;
            host.runtimeUi?.CloseFocusedCardSelection();
            host.ShowMessage("Payment cancelled.");
        }

        internal void ConfirmPendingActionAbilityPurchase()
        {
            if (host.pendingControllerPurchaseState == null
                || !host.pendingControllerPurchaseState.IsActive
                || !host.pendingControllerPurchaseState.IsPaymentComplete
                || host.trapFloorTurnState == null
                || host.pendingControllerPurchaseState.PlayerId != host.trapFloorTurnState.ActivePlayerId)
            {
                host.ShowMessage("Place the chosen cards in your Console first.");
                return;
            }

            if (host.pendingControllerPurchaseState.Source == PendingPurchaseSource.Shop)
            {
                CompleteShopPurchase(
                    host.pendingControllerPurchaseState.PurchasedCardDefinitionStableId,
                    host.pendingControllerPurchaseState.SelectedCardIds);
                RefreshTrapFloorStatusUi();
                return;
            }

            if (!TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile)
                || prizeTile.Content.AuthoredCard == null
                || !string.Equals(
                    prizeTile.Content.AuthoredCard.StableId,
                    host.pendingControllerPurchaseState.PurchasedCardDefinitionStableId,
                    StringComparison.OrdinalIgnoreCase))
            {
                host.ShowMessage("Stand on the Ability you are paying for, then confirm.");
                return;
            }

            CompleteTakeFloorPrize(prizeTile.ObjectId, host.pendingControllerPurchaseState.SelectedCardIds);
            RefreshTrapFloorStatusUi();
        }

        internal void CancelPendingActionAbilityPurchase()
        {
            host.pendingPurchaseDefinitionStableId = string.Empty;
            pendingDeckCardId = TabletopObjectId.Empty;
            host.runtimeUi?.CloseFocusedCardSelection();
            host.runtimeUi?.CloseActionAbilityPurchase();
            if (host.pendingControllerPurchaseState == null
                || !host.pendingControllerPurchaseState.IsActive)
                return;

            host.pendingControllerPurchaseState.Clear();
            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            host.ShowMessage(
                "Payment cancelled. Cards stay where you placed them.");
        }

        internal string FindPurchaseDisplayName(string cardDefinitionStableId)
        {
            return host.trapFloorTemplate != null
                && host.trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out CardDefinitionData definition)
                ? definition.DisplayName
                : "ABILITY";
        }

        // The Shop's list (doc 18 §15.4): every Ability of the game, Actions first, then Reactions. One without a
        // price is listed but not for sale. Affordability is for the player whose turn it is.
        internal List<ActionAbilityPurchaseOption> BuildActionAbilityPurchaseOptions()
        {
            List<ActionAbilityPurchaseOption> options =
                new List<ActionAbilityPurchaseOption>();
            GameDefinitionData gameDefinition = host.trapFloorTemplate.GameDefinition;
            ContainerState hand = null;
            if (IsShopBuyingTurn())
            {
                TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
                hand = host.matchState.GetContainer(player.HandContainerId);
            }

            ControllerInputCostEvaluator evaluator = new ControllerInputCostEvaluator();
            List<CardDefinitionData> catalog = TrapFloorShop.Catalog(gameDefinition);
            for (int i = 0; i < catalog.Count; i++)
            {
                CardDefinitionData definition = catalog[i];
                bool forSale = TrapFloorShop.IsForSale(definition);
                string cost = forSale ? FormatPurchaseInputCost(definition.InputCost) : "not set yet";
                bool canPay = false;
                string affordability;
                if (!forSale)
                {
                    affordability = "Not for sale until the rules give it a price.";
                }
                else if (hand == null)
                {
                    affordability = "Buying is on a player's own turn.";
                }
                else
                {
                    ControllerInputCostEvaluation evaluation = evaluator.Evaluate(
                        host.matchState,
                        hand,
                        definition.InputCost,
                        gameDefinition);
                    canPay = evaluation.CanPay;
                    affordability = evaluation.CanPay
                        ? "Your Hand can pay this."
                        : evaluation.Error == ControllerInputCostEvaluationError.InsufficientInput
                            ? $"Your Hand cannot pay {cost} yet."
                            : $"Unavailable: {evaluation.Error}";
                }
                Texture artwork = null;
                if (Guid.TryParse(definition.StableId, out Guid definitionGuid)
                    && host.TryGetAuthoredCardDefinition(
                        new ObjectDefinitionId(definitionGuid),
                        out CardDefinition authoredDefinition))
                {
                    artwork = host.FrontArt(authoredDefinition);
                }
                options.Add(new ActionAbilityPurchaseOption(
                    definition.StableId,
                    definition.DisplayName,
                    definition.Description,
                    cost,
                    artwork,
                    canPay,
                    affordability,
                    forSale,
                    TrapFloorShop.IsAction(definition) ? "Action" : TrapFloorShop.IsReaction(definition) ? "Reaction" : string.Empty));
            }

            return options;
        }

        internal static string FormatPurchaseInputCost(InputCostData cost)
        {
            return InputCostNotation.Format(cost);
        }

        internal static string PaymentVerb(PendingControllerPurchaseState state) =>
            state != null && state.Source == PendingPurchaseSource.Shop ? "BUY" : "TAKE";

        // Confirm Payment for a Shop card: the card goes to the Hand, or beside the Console when the Hand is full.
        internal void CompleteShopPurchase(string cardDefinitionStableId, IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            if (host.trapFloorShopUseCase == null || host.trapFloorTurnState == null)
            {
                host.ShowMessage("The Shop is unavailable.");
                return;
            }

            // ABIL-DECK: a card drawn from the Ability deck is bought out of the deck itself.
            TabletopObjectId deckCardId = TakePendingDeckCard(cardDefinitionStableId);
            TrapFloorBuyFromShopResult result = host.TableAuthority.Submit<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(
                new TrapFloorBuyFromShopCommand(
                    host.CreateCommandContext(host.trapFloorTurnState.ActivePlayerId),
                    cardDefinitionStableId,
                    deckCardId.IsEmpty ? TabletopObjectId.New() : deckCardId,
                    paymentCardIds,
                    deckCardId));
            if (!result.Succeeded)
            {
                host.ShowMessage($"Shop purchase rejected: {result.Error}.");
                return;
            }

            host.pendingControllerPurchaseState.Clear();
            host.pendingPurchaseDefinitionStableId = string.Empty;
            ReplaceCurrentUndoStateForPurchaseAssistance();

            string name = result.Card.DisplayName;
            if (!deckCardId.IsEmpty)
            {
                // The deck card already has a view: lay out the deck and the card's new place, and show its face.
                host.ViewSync.SyncContainer(host.trapFloorTemplate.AbilityDeckId);
                host.ViewSync.SyncContainer(result.DestinationContainerId);
                if (result.DestinationContainerId.IsEmpty) host.PresentAcceptedObject(deckCardId); // loose beside the Console
                host.RefreshCardContentVisibility();
                host.RefreshSelectionPresenterAfterRuntimeProjection();
            }

            ShowPaymentReturned();

            // R2: the bought card got its view from the table's view sync (PresentGrantedCard).
            if (host.Registry.TryGetCardView(result.GrantedCardId, out CardView boughtView))
            {
                host.presentationTransitions.Appear(boughtView.transform, host.settleDuration);
            }
            if (TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition buyer))
            {
                string buyerName = host.HandOwnerName(buyer.HandContainerId);
                string from = deckCardId.IsEmpty ? "the Shop" : "the Ability deck";
                host.ShowMessage(result.PlacedInHand
                    ? $"Bought {name} from {from} into {buyerName}'s hand."
                    : $"Bought {name} from {from}. {buyerName}'s hand is full, so it is beside their Console.");
            }
        }
        // ---------- from TabletopComposition.TrapFloor.cs ----------

        public TrapFloorRoundActionResult CompleteTrapFloorStart()
        {
            host.EnsureInitialized();
            if (host.trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("Round progression is available only in the active Trap Floor session.");
            }

            TrapFloorRoundActionResult result = host.trapFloorRoundOrchestrationService.CompleteStart(
                new TrapFloorRoundActionRequest(host.CreateCommandContext()));
            host.ShowMessage(result.Succeeded
                ? host.trapFloorRoundState.CurrentRoundNumber == 1
                    ? "Round 1 Start complete. Each participating Player may now Search once."
                    : $"Round {host.trapFloorRoundState.CurrentRoundNumber} started. Move your pawns by hand if the round asks for it."
                : $"Start progression rejected: {result.Error}.");
            return result;
        }

        public TrapFloorRoundFloorfallResult TriggerFloorfall()
        {
            host.EnsureInitialized();
            if (host.trapFloorRoundOrchestrationService == null || host.floorfallTargetPresenter == null)
            {
                throw new InvalidOperationException("Floorfall is available only in the active Trap Floor session.");
            }

            TrapFloorRoundFloorfallResult result = host.trapFloorRoundOrchestrationService.RollFloorfall(
                new TrapFloorRoundActionRequest(host.CreateCommandContext(
                    host.physicalFloorfallActor.IsEmpty ? host.localPlayerId : host.physicalFloorfallActor)));
            if (!result.Succeeded)
            {
                host.ShowMessage($"Official Floorfall rejected: {result.Error}.");
                return result;
            }

            TrapFloorFloorfallTarget target = result.Target.Value;
            AnimateAcceptedDieResult(host.trapFloorTemplate.FloorfallXAxisDieId);
            AnimateAcceptedDieResult(host.trapFloorTemplate.FloorfallYAxisDieId);
            ShowFloorfallTarget(target.FloorCardId); // a board feedback marker (H3), not a tint on the tile
            host.ShowMessage(
                $"Floorfall {host.trapFloorRoundState.AcceptedFloorfallCount}: X {target.XAxisRoll.Value}, Y {target.YAxisRoll.Value} -> {target.Coordinate}.");
            return result;
        }

        internal bool BeginPhysicalFloorfall()
        {
            if (host.physicalFloorfallPending || host.trapFloorRoundState == null
                || host.trapFloorRoundState.Phase != TrapFloorRoundPhase.Floorfall
                || !host.Registry.TryGetDieView(host.trapFloorTemplate.FloorfallXAxisDieId, out DieView x)
                || !host.Registry.TryGetDieView(host.trapFloorTemplate.FloorfallYAxisDieId, out DieView y)
                || x.PhysicalObject == null || y.PhysicalObject == null) return false;
            if (host.physicalFloorfallActor.IsEmpty) host.physicalFloorfallActor = host.localPlayerId;
            // DICE-1 (owner, 2026-10-21): the game never rolls; it asks, and reads the dice once the player has rolled them.
            AskForFloorDiceRoll(true, true);
            host.physicalFloorfallPending = true;
            host.ShowMessage("FLOORFALL — roll both floor dice (pick them up and throw them).");
            return true;
        }

        // ---------- DICE-1: the players roll the dice themselves (owner, 2026-10-21) ----------
        // When a roll is needed the game only asks for it and remembers each die's state at that moment; a die counts
        // as rolled once it has moved since (a new accepted state) and lies still. A cocked die is asked again.
        // Event-time only.
        private PhysicalObjectState askedXDieState;
        private PhysicalObjectState askedYDieState;
        private PhysicalObjectState askedBlindDieState;
        private bool askedXDie;
        private bool askedYDie;

        private PhysicalObjectState DiePhysicalState(TabletopObjectId dieId) =>
            host.matchState != null && host.matchState.Dice.TryGetValue(dieId, out DieState die)
                ? die.BaseState.PhysicalState
                : null;

        private void AskForFloorDiceRoll(bool xDie, bool yDie)
        {
            askedXDie = xDie;
            askedYDie = yDie;
            askedXDieState = DiePhysicalState(host.trapFloorTemplate.FloorfallXAxisDieId);
            askedYDieState = DiePhysicalState(host.trapFloorTemplate.FloorfallYAxisDieId);
        }

        // True once every asked floor die was rolled and lies still (the others only need to lie still).
        private bool FloorDiceRolledAsAsked()
        {
            return DieRolledAsAsked(host.trapFloorTemplate.FloorfallXAxisDieId, askedXDie, ref askedXDieState, "column")
                & DieRolledAsAsked(host.trapFloorTemplate.FloorfallYAxisDieId, askedYDie, ref askedYDieState, "row");
        }

        private bool DieRolledAsAsked(TabletopObjectId dieId, bool asked, ref PhysicalObjectState askedState, string name)
        {
            PhysicalObjectState state = DiePhysicalState(dieId);
            if (state == null) return false;
            if (!asked) return state.Mode == PhysicalObjectMode.Sleeping;
            if (ReferenceEquals(state, askedState)) return false;
            if (state.Mode == PhysicalObjectMode.SleepingUnresolved)
            {
                askedState = state;
                host.FeedbackLine($"The {name} die is cocked: roll it again.", FeedbackSeverity.Warning); // DICE-2: always shown
                return false;
            }

            return state.Mode == PhysicalObjectMode.Sleeping;
        }

        internal void CompletePhysicalFloorfallIfSettled()
        {
            if (!host.physicalFloorfallPending) return;
            if (host.trapFloorRoundState == null || host.trapFloorRoundState.Phase != TrapFloorRoundPhase.Floorfall)
            { host.physicalFloorfallPending = false; host.physicalFloorfallActor = PlayerId.Empty; return; }
            if (!FloorDiceRolledAsAsked()) return;
            host.physicalFloorfallPending = false;
            if (host.floorfallService.IsProtectedPhysicalResult(new TrapFloorFloorfallContext(host.trapFloorRoundState.CurrentRoundNumber)))
            { BeginPhysicalFloorfall(); return; }
            TriggerFloorfall();
            host.physicalFloorfallActor = PlayerId.Empty;
        }

        // While a collapse waits for its roll, the turn menu's entry reminds the player instead of being greyed out;
        // after an Undo or a rebuild it picks the wait up again for the player who began it.
        internal void RemindFloorCollapseRoll()
        {
            if (host.trapFloorCollapseState == null || !host.trapFloorCollapseState.IsCollapsePending) return;
            if (!host.physicalFloorCollapsePending)
            {
                host.physicalFloorCollapsePending = true;
                host.physicalFloorCollapseActor = host.trapFloorCollapseState.PendingActorPlayerId;
                AskForFloorDiceRoll(true, true);
                RefreshTrapFloorStatusUi();
            }

            host.FeedbackLine( // COLLAPSE-3: always shown
                "FLOOR TURN — any player: roll the two floor dice (column and row): "
                + "the tile they point at collapses.",
                FeedbackSeverity.Info);
        }

        // COLLAPSE-3 (owner, 2026-10-21: "why we need to click a button and then roll"): during a floor turn the game
        // watches the two floor dice by itself, so throwing them collapses the floor (Collapse Floor still works and
        // reminds). A collapse waiting for its roll after an Undo or a rebuild is picked up again too. Watching costs a
        // few field checks per frame; it never submits a command until the dice were thrown.
        private bool collapseWatching;
        private bool floorTurnCollapseDone;

        private void ArmFloorCollapseForFloorTurn()
        {
            if (host.trapFloorTurnState == null
                || host.trapFloorCollapseState == null
                || host.trapFloorTemplate == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.FloorTurn)
            {
                collapseWatching = false;
                floorTurnCollapseDone = false;
                return;
            }

            if (host.physicalFloorCollapsePending || host.undoTransactionInProgress) return;
            if (host.trapFloorCollapseState.IsCollapsePending)
            {
                // Waiting for its roll but not armed (after an Undo or a rebuild): wait for the dice again, silently.
                collapseWatching = false;
                ResetCollapsePreview();
                host.physicalFloorCollapsePending = true;
                host.physicalFloorCollapseActor = host.trapFloorCollapseState.PendingActorPlayerId;
                AskForFloorDiceRoll(true, true);
                RefreshTrapFloorStatusUi();
                return;
            }

            if (floorTurnCollapseDone || host.trapFloorCollapseState.IsBoardExhausted) return;
            if (!collapseWatching)
            {
                collapseWatching = true;
                AskForFloorDiceRoll(true, true); // the next throw of both floor dice collapses the floor
                host.FeedbackLine(
                    (host.trapFloorTurnState.IsFloorTurnBetweenPlayers
                        ? "FLOOR TURN — "
                        : $"ROUND {host.trapFloorTurnState.CurrentRound} ENDED — ")
                        + "any player: roll the two floor dice (column and row) to collapse a tile.",
                    FeedbackSeverity.Info);
                return;
            }

            if (!FloorDiceRolledAsAsked()) return;
            collapseWatching = false;
            if (BeginPhysicalFloorCollapse(true)) CompletePhysicalFloorCollapseIfSettled();
        }

        internal bool BeginPhysicalFloorCollapse() => BeginPhysicalFloorCollapse(false);

        // afterThrow (COLLAPSE-3): the dice were already thrown while the floor turn watched them; that throw resolves it.
        private bool BeginPhysicalFloorCollapse(bool afterThrow)
        {
            if (host.physicalFloorCollapsePending
                || host.trapFloorCollapseState == null
                || host.trapFloorCollapseUseCase == null
                || host.trapFloorTurnState == null
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.FloorTurn)
            {
                return false;
            }

            if (host.trapFloorCollapseState.IsBoardExhausted)
            {
                host.ShowMessage("Collapse Floor rejected: the Board has no usable Floors remaining.");
                return false;
            }

            if (!TryGetOfficialFloorfallDieViews(out DieView xAxisDieView, out DieView yAxisDieView))
            {
                host.ShowMessage("Collapse Floor rejected: the two official physical d6 are unavailable.");
                return false;
            }

            PlayerId actor = host.trapFloorTurnState.FloorOperatorPlayerId;
            TrapFloorCollapseResult begin = host.TableAuthority.Submit<TrapFloorBeginCollapseCommand, TrapFloorCollapseResult>(
                afterThrow
                    ? new TrapFloorBeginCollapseCommand(host.CreateCommandContext(actor), askedXDieState, askedYDieState)
                    : new TrapFloorBeginCollapseCommand(host.CreateCommandContext(actor)));
            if (!begin.Succeeded)
            {
                host.ShowMessage($"Collapse Floor rejected: {begin.Error}.");
                return false;
            }

            host.physicalFloorCollapsePending = true;
            host.physicalFloorCollapseActor = actor;
            collapseWatching = false;
            ResetCollapsePreview(); // ROUND-1: a new collapse starts with no tile marked
            if (afterThrow) return true; // the throw already made is read now (the asked states are from before it)
            AskForFloorDiceRoll(true, true); // DICE-1: the player rolls them
            RefreshTrapFloorStatusUi();
            host.FeedbackLine( // COLLAPSE-3: an instruction, always shown
                "FLOOR TURN — any player: roll the two floor dice (column and row): the tile they point at collapses.",
                FeedbackSeverity.Info);
            return true;
        }

        internal void CompletePhysicalFloorCollapseIfSettled()
        {
            if (!host.physicalFloorCollapsePending)
            {
                return;
            }

            // DICE-1 fix (owner, 2026-10-21): a die the player is holding to roll does not end the wait (it used to,
            // leaving the collapse pending with no way to finish it and Collapse Floor greyed out).
            if (host.trapFloorCollapseState == null
                || host.trapFloorCollapseUseCase == null
                || !host.trapFloorCollapseState.IsCollapsePending)
            {
                host.physicalFloorCollapsePending = false;
                host.physicalFloorCollapseActor = PlayerId.Empty;
                ResetCollapsePreview(); // ROUND-1
                return;
            }

            if (!FloorDiceRolledAsAsked()) return; // DICE-1: wait for the player's roll

            // ROUND-1 (owner, 2026-10-22: "after rolling the game records the tile and highlights it and gives a button to
            // collapse"): a tile on the floor waits for Collapse; dice beyond the floor or on a hole go on to the reroll.
            if (!collapseConfirmed && TryPreviewCollapseTarget(out TabletopObjectId previewId, out TrapFloorCoordinate previewTile))
            {
                if (previewId != collapsePreviewId)
                {
                    collapsePreviewId = previewId;
                    collapsePreviewTile = previewTile;
                    ShowFloorfallTarget(previewId);
                    ShowCollapsePopup(previewId, previewTile);
                    RefreshTrapFloorStatusUi();
                }

                return;
            }

            ResetCollapsePreview();
            TrapFloorCollapseResult result = host.TableAuthority.Submit<TrapFloorResolveCollapseCommand, TrapFloorCollapseResult>(
                new TrapFloorResolveCollapseCommand(
                    host.CreateCommandContext(host.physicalFloorCollapseActor)));
            if (!result.Succeeded)
            {
                host.physicalFloorCollapsePending = false;
                host.physicalFloorCollapseActor = PlayerId.Empty;
                host.ShowMessage($"Collapse Floor rejected: {result.Error}.");
                return;
            }

            if (result.RerollRequired)
            {
                // FLOORS-1: a die beyond a smaller floor is rolled again on its own.
                bool beyond = result.Roll.FloorCardId.IsEmpty;
                bool rerollX = !beyond || result.Roll.XAxisResult > host.trapFloorTemplate.GridColumns;
                bool rerollY = !beyond || result.Roll.YAxisResult > host.trapFloorTemplate.GridRows;
                host.FeedbackLine(beyond // COLLAPSE-3: always shown
                    ? $"Floorfall rolled {result.Roll.XAxisResult}/{result.Roll.YAxisResult}: beyond the "
                        + $"{host.trapFloorTemplate.GridColumns}×{host.trapFloorTemplate.GridRows} floor: roll "
                        + (rerollX && rerollY ? "both dice" : rerollX ? "the column die" : "the row die") + " again."
                    : $"Floor {result.Roll.Coordinate} is already collapsed: roll both floor dice again.",
                    FeedbackSeverity.Warning);
                AskForFloorDiceRoll(rerollX, rerollY); // DICE-1: the player rolls again

                RefreshTrapFloorStatusUi();
                return;
            }

            host.physicalFloorCollapsePending = false;
            host.physicalFloorCollapseActor = PlayerId.Empty;
            floorTurnCollapseDone = true; // COLLAPSE-3: one collapse per floor turn
            ApplyCollapsedFloorPresentation(result.CollapsedFloor.FloorCardId);
            KnockOutPawnsOnCollapsedTile(result.Roll.Coordinate); // FALL-1
            host.selectionState?.ClearAll();
            host.selectionPresenter?.Refresh();
            RefreshTrapFloorStatusUi();
            host.FeedbackLine( // COLLAPSE-3: always shown
                $"Floorfall rolled {result.Roll.XAxisResult}/{result.Roll.YAxisResult}; "
                + $"Floor {result.Roll.Coordinate} collapsed: lift the tile off and set it beside the board, then End Floor Turn "
                + "(it sets the tile aside otherwise)."
                + (result.BoardExhausted ? " The Board is exhausted." : string.Empty),
                FeedbackSeverity.Info);
        }

        // FALL-1 (owner, 2026-10-22: "if a floor tile under a player collapses the player gets eliminated"): every
        // player still in whose pawn stands on the collapsed tile is out for the rest of this floor (one Undo step right
        // after the collapse's own). Event-time only.
        private void KnockOutPawnsOnCollapsedTile(TrapFloorCoordinate collapsedTile)
        {
            if (host.trapFloorTurnState == null || host.trapFloorTurnService == null || host.trapFloorTemplate == null) return;
            bool anyOut = false;
            host.SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
                {
                    if (host.trapFloorTurnState.IsCurrentFloorFailed) break;
                    TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                    if (!host.matchState.Seats.TryGetValue(setup.SeatId, out var seat)
                        || seat.OccupantPlayerId.IsEmpty
                        || !IsTurnParticipant(seat.OccupantPlayerId)
                        || host.trapFloorTurnState.GetPlayerState(seat.OccupantPlayerId)
                            == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                        || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)
                        || tile != collapsedTile)
                    {
                        continue;
                    }

                    PlayerId player = seat.OccupantPlayerId;
                    PlayerId actor = host.physicalFloorCollapseActor.IsEmpty
                        ? host.trapFloorTurnState.FloorOperatorPlayerId
                        : host.physicalFloorCollapseActor;
                    TrapFloorTurnAdvanceResult knocked = host.trapFloorTurnService.MarkPlayerEliminatedForCurrentRound(
                        host.matchState,
                        host.CreateCommandContext(actor.IsEmpty ? player : actor),
                        player);
                    if (!knocked.Succeeded) continue;
                    anyOut = true;
                    host.FeedbackLine(
                        $"{FormatPlayerName(player).ToUpperInvariant()} fell with the collapsing floor at {collapsedTile}: ELIMINATED for this floor.",
                        FeedbackSeverity.Loss);
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            if (!anyOut) return;
            if (host.trapFloorTurnState.IsCurrentFloorFailed)
            {
                host.FeedbackLine(FloorFailedCountsAsWin()
                    ? $"Everyone left has escaped or is out: the floor is won. {FloorEndedChoiceText(true)}"
                    : $"Everyone is out: the floor is lost. {FloorEndedChoiceText()}", FeedbackSeverity.Loss);
                ShowFloorEnded(FloorFailedCountsAsWin()); // FLOOR-2
            }
            else
            {
                TryTakeExitsAfterMove(TabletopObjectId.Empty); // KEYS-1: the players left may all be on the Grand Exit
            }
        }

        // DICE-2 (owner, 2026-10-22: "roll d4 should also record same as d6"): while the turn player is Blind and has no
        // direction yet, the game watches the d4 by itself, so throwing it records the direction (Roll Blind Direction
        // still works and reminds). Per frame: a few field checks; the d4 lookup runs only while it waits to be armed.
        private void ArmBlindDirectionForTurn()
        {
            if (host.physicalBlindDirectionPending
                || host.undoTransactionInProgress
                || host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || host.trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved
                || !TryGetAvailableBlindD4(out TabletopObjectId dieId, out _))
            {
                return;
            }

            PlayerId actor = host.trapFloorTurnState.ActivePlayerId;
            host.physicalBlindDirectionPending = true;
            host.physicalBlindDirectionActor = actor;
            host.physicalBlindDirectionDieId = dieId;
            askedBlindDieState = DiePhysicalState(dieId);
            RefreshTrapFloorStatusUi();
            host.FeedbackLine(
                $"BLIND — {FormatPlayerName(actor)}, roll the d4: it sets the only direction you may move this turn.",
                FeedbackSeverity.Info);
        }

        // ---------- ROUND-1: the rolled tile waits for Collapse ----------

        private TabletopObjectId collapsePreviewId;
        private TrapFloorCoordinate collapsePreviewTile;
        private bool collapseConfirmed;

        // The tile the two floor dice point at, when it is on this floor and still there. Per frame while waiting:
        // two dictionary lookups, no allocation.
        private bool TryPreviewCollapseTarget(out TabletopObjectId floorCardId, out TrapFloorCoordinate tile)
        {
            floorCardId = TabletopObjectId.Empty;
            tile = default;
            if (!host.matchState.Dice.TryGetValue(host.trapFloorTemplate.FloorfallXAxisDieId, out DieState xDie)
                || !host.matchState.Dice.TryGetValue(host.trapFloorTemplate.FloorfallYAxisDieId, out DieState yDie))
            {
                return false;
            }

            int x = xDie.CurrentValue;
            int y = yDie.CurrentValue;
            if (x < TrapFloorCoordinate.MinimumAxisValue || y < TrapFloorCoordinate.MinimumAxisValue
                || x > host.trapFloorTemplate.GridColumns || y > host.trapFloorTemplate.GridRows)
            {
                return false;
            }

            tile = new TrapFloorCoordinate(x, y);
            return host.trapFloorTemplate.TryGetFloorCardId(tile, out floorCardId)
                && !host.trapFloorCollapseState.IsCollapsed(floorCardId);
        }

        private string CollapsePreviewLabel() => $"Collapse tile {collapsePreviewTile}";

        // ROUND-1: forgets the marked tile and closes its popup.
        private void ResetCollapsePreview()
        {
            bool wasShown = !collapsePreviewId.IsEmpty;
            collapsePreviewId = TabletopObjectId.Empty;
            collapseConfirmed = false;
            host.floorfallTargetPresenter?.Clear();
            if (wasShown) CloseFocusedSearchReveal();
        }

        private void ConfirmFloorCollapse()
        {
            if (collapsePreviewId.IsEmpty
                || !host.physicalFloorCollapsePending
                || host.trapFloorTurnState == null
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.FloorTurn)
            {
                return;
            }

            CloseFocusedSearchReveal();
            collapseConfirmed = true;
            CompletePhysicalFloorCollapseIfSettled();
        }

        private void ShowCollapsePopup(TabletopObjectId floorCardId, TrapFloorCoordinate tile)
        {
            if (host.runtimeUi == null) return;
            Texture front = null;
            if (host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floor)
                && floor.IsRevealed)
            {
                FloorPopupArt(floor, out _, out front);
            }

            host.runtimeUi.ShowFocusedCardReveal(new FocusedCardRevealModel(
                floorCardId,
                host.CurrentBoardCardBack(),
                front,
                "THE FLOOR COLLAPSES",
                $"TILE {tile}",
                $"Tile {tile}",
                "The dice point at this tile.",
                $"The two floor dice point at tile {tile} (it is marked on the board). Press Collapse: a player standing "
                    + "on it falls. Throw the dice again to choose another roll.",
                "Collapse",
                ConfirmFloorCollapse));
        }

        // TURN-2: how many Controller Cards the turn player may draw up to the hand limit (Blind: none this turn).
        private bool TryGetHandDrawNeed(out int need, out bool blindNoDraw)
        {
            need = 0;
            blindNoDraw = TryGetActiveBlindStatus(out _);
            if (host.trapFloorTemplate == null
                || !TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player)
                || !host.matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand)
                || !host.matchState.Containers.TryGetValue(player.ControllerDeckId, out ContainerState deck))
            {
                return false;
            }

            int limit = host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
            need = Math.Min(limit - hand.Count, deck.Count);
            return need > 0;
        }

        private void DrawUpToHandLimitForTurn()
        {
            host.ContextMenus.CloseContextMenu();
            if (!TryGetHandDrawNeed(out int need, out bool blindNoDraw) || blindNoDraw) return;
            PlayerId active = host.trapFloorTurnState.ActivePlayerId;
            if (!TryGetTrapFloorPlayerSetup(active, out TrapFloorPlayerSetupDefinition player)) return;
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts =
                host.CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            DrawCardsResult result = host.TableAuthority.Submit<DrawCardsCommand, DrawCardsResult>(new DrawCardsCommand(
                host.CreateCommandContext(active),
                player.ControllerDeckId,
                player.HandContainerId,
                need));
            if (!result.Succeeded)
            {
                host.ApplyLayout(player.ControllerDeckId);
                host.ApplyLayout(player.HandContainerId);
            }

            host.presentationTransitions.AnimateCardsFromCurrentResults(
                starts,
                result.Succeeded ? host.handReflowDuration : host.returnDuration,
                result.Succeeded ? 0.035f : 0f);
            host.FeedbackLine(
                result.Succeeded
                    ? $"{FormatPlayerName(active)} drew {need} Controller Card{(need == 1 ? string.Empty : "s")}."
                    : $"Draw rejected: {result.Error}.",
                result.Succeeded ? FeedbackSeverity.Info : FeedbackSeverity.Warning);
            RefreshTrapFloorStatusUi();
        }

        internal bool BeginPhysicalBlindDirection()
        {
            if (host.physicalBlindDirectionPending
                || host.trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved)
            {
                host.ShowMessage("Roll Blind Direction is unavailable.");
                return false;
            }

            if (!TryGetAvailableBlindD4(out TabletopObjectId dieId, out DieView dieView))
            {
                host.ShowMessage("Roll Blind Direction requires an available loose physical d4.");
                return false;
            }

            PlayerId actor = host.trapFloorTurnState.ActivePlayerId;
            host.physicalBlindDirectionPending = true;
            host.physicalBlindDirectionActor = actor;
            host.physicalBlindDirectionDieId = dieId;
            askedBlindDieState = DiePhysicalState(dieId); // DICE-1: the player rolls it
            host.ShowMessage($"BLIND — {FormatPlayerName(actor)}, roll the d4 for your direction.");
            return true;
        }

        internal void CompletePhysicalBlindDirectionIfSettled()
        {
            if (!host.physicalBlindDirectionPending) return;
            if (host.matchState == null
                || host.trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved
                || host.physicalBlindDirectionActor != host.trapFloorTurnState.ActivePlayerId
                || !host.matchState.Dice.TryGetValue(host.physicalBlindDirectionDieId, out DieState die))
            {
                CancelPhysicalBlindDirection("Blind direction roll cancelled because its active status changed.");
                return;
            }

            PhysicalObjectState physicalState = die.BaseState.PhysicalState;
            if (physicalState == null
                || (physicalState.Mode != PhysicalObjectMode.Sleeping
                    && physicalState.Mode != PhysicalObjectMode.SleepingUnresolved))
            {
                return;
            }

            if (ReferenceEquals(physicalState, askedBlindDieState)) return; // DICE-1: not rolled yet
            if (physicalState.Mode == PhysicalObjectMode.SleepingUnresolved)
            {
                askedBlindDieState = physicalState;
                host.FeedbackLine("The d4 is cocked: roll it again.", FeedbackSeverity.Warning); // DICE-2: always shown
                return;
            }

            TrapFloorBlindDirectionRollResult result =
                host.trapFloorAbilityResolutionService.ResolveBlindDirection(
                    host.matchState,
                    host.CreateCommandContext(host.physicalBlindDirectionActor),
                    host.physicalBlindDirectionDieId);
            if (!result.Succeeded)
            {
                CancelPhysicalBlindDirection($"Blind direction rejected: {result.Error}.");
                return;
            }

            host.physicalBlindDirectionPending = false;
            host.physicalBlindDirectionActor = PlayerId.Empty;
            host.physicalBlindDirectionDieId = TabletopObjectId.Empty;
            RefreshTrapFloorStatusUi();
            host.FeedbackLine( // DICE-2: always shown
                $"BLIND DIRECTION: {result.Direction.ToString().ToUpperInvariant()} — move only that way this turn.",
                FeedbackSeverity.Info);
        }

        internal void CancelPhysicalBlindDirection(string message)
        {
            PlayerId actor = host.physicalBlindDirectionActor;
            host.physicalBlindDirectionPending = false;
            host.physicalBlindDirectionActor = PlayerId.Empty;
            host.physicalBlindDirectionDieId = TabletopObjectId.Empty;
            if (host.undoTransactionInProgress && host.matchState != null && !actor.IsEmpty)
            {
                host.matchState.AdvanceRevision(
                    CommandId.New(),
                    actor,
                    AuthoritativeActionKind.Unspecified,
                    AuthoritativeActionRecordMode.CancelTransaction);
            }
            host.ShowMessage(message);
        }

        internal bool TryGetAvailableBlindD4(
            out TabletopObjectId dieId,
            out DieView dieView)
        {
            if (host.matchState != null)
            {
                foreach (KeyValuePair<TabletopObjectId, DieState> pair in host.matchState.Dice)
                {
                    if (pair.Value.SideCount == 4
                        && host.Registry.TryGetDieView(pair.Key, out DieView candidate)
                        && candidate.PhysicalObject != null
                        && candidate.PhysicalObject.OwnsLooseTransform
                        && !candidate.PhysicalObject.IsHeld)
                    {
                        dieId = pair.Key;
                        dieView = candidate;
                        return true;
                    }
                }
            }

            dieId = TabletopObjectId.Empty;
            dieView = null;
            return false;
        }

        internal bool TryGetOfficialFloorfallDieViews(
            out DieView xAxisDieView,
            out DieView yAxisDieView)
        {
            xAxisDieView = null;
            yAxisDieView = null;
            return host.trapFloorTemplate != null
                && host.Registry.TryGetDieView(host.trapFloorTemplate.FloorfallXAxisDieId, out xAxisDieView)
                && host.Registry.TryGetDieView(host.trapFloorTemplate.FloorfallYAxisDieId, out yAxisDieView)
                && xAxisDieView.PhysicalObject != null
                && yAxisDieView.PhysicalObject != null
                && !xAxisDieView.PhysicalObject.IsHeld
                && !yAxisDieView.PhysicalObject.IsHeld
                && xAxisDieView.PhysicalObject.OwnsLooseTransform
                && yAxisDieView.PhysicalObject.OwnsLooseTransform
                && !xAxisDieView.BoundState.IsUserLocked
                && !yAxisDieView.BoundState.IsUserLocked;
        }

        public TrapFloorRoundSearchResult SearchFloormasterDeck()
        {
            return SearchFloormasterDeck(host.localPlayerId);
        }

        public TrapFloorRoundSearchResult SearchFloormasterDeck(PlayerId searchingPlayerId)
        {
            host.EnsureInitialized();
            if (host.trapFloorRoundOrchestrationService == null || host.floormasterLifecycleState == null)
            {
                throw new InvalidOperationException("Floormaster Search is available only in the active Trap Floor session.");
            }

            TrapFloorRoundSearchResult result = host.trapFloorRoundOrchestrationService.Search(
                new TrapFloorFloormasterSearchRequest(host.CreateCommandContext(searchingPlayerId)));
            if (!result.Succeeded)
            {
                string detail = result.Error == TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected
                    ? result.LifecycleError.ToString()
                    : result.Error.ToString();
                host.ShowMessage($"Floormaster Search rejected: {detail}.");
                return result;
            }

            host.ApplyLayout(host.trapFloorTemplate.FloormasterDeckId);
            if (result.ReshuffledDiscard)
            {
                host.ApplyLayout(host.trapFloorTemplate.FloormasterDiscardId);
            }

            CardView pendingCardView = FindCardView(result.PendingCard.CardId);
            pendingCardView.ApplyAcceptedState();
            host.RefreshCardContentVisibility();
            string reshuffleStatus = result.ReshuffledDiscard ? " Discard reshuffled first." : string.Empty;
            host.ShowMessage(
                $"{FormatPlayerName(result.PendingCard.SearchingPlayerId)} searched: {result.PendingCard.Category} pending.{reshuffleStatus}");
            return result;
        }

        public TrapFloorRoundTriggerResult CompletePendingFloormasterTrigger()
        {
            host.EnsureInitialized();
            if (host.trapFloorRoundOrchestrationService == null || host.floormasterLifecycleState == null)
            {
                throw new InvalidOperationException("Floormaster Trigger completion is available only in the active Trap Floor session.");
            }

            TrapFloorPendingFloormasterCard pendingCard = host.floormasterLifecycleState.PendingCard;
            if (pendingCard == null)
            {
                host.ShowMessage("There is no searched card waiting to be resolved.");
                return TrapFloorRoundTriggerResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            TrapFloorRoundTriggerResult result = host.trapFloorRoundOrchestrationService.CompleteTrigger(
                new CompletePendingFloormasterCardRequest(host.CreateCommandContext(), pendingCard.CardId));
            if (!result.Succeeded)
            {
                string detail = result.Error == TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected
                    ? result.LifecycleError.ToString()
                    : result.Error.ToString();
                host.ShowMessage($"The searched card could not be marked done: {detail}.");
                return result;
            }

            host.ApplyLayout(host.trapFloorTemplate.FloormasterDiscardId);
            host.RefreshCardContentVisibility();
            host.ShowMessage("Searched card discarded. Its effect is carried out by the players at the table.");
            return result;
        }

        public TrapFloorRoundActionResult CompleteFloorfallPhase()
        {
            host.EnsureInitialized();
            if (host.trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("Floorfall phase progression is available only in the active Trap Floor session.");
            }

            TrapFloorRoundActionResult result = host.trapFloorRoundOrchestrationService.CompleteFloorfallPhase(
                new TrapFloorRoundActionRequest(host.CreateCommandContext()));
            host.ShowMessage(result.Succeeded
                ? "Floorfall done. Check together that your mode's Floorfalls were all rolled."
                : $"Floorfall phase completion rejected: {result.Error}.");
            return result;
        }

        public TrapFloorRoundActionResult CompleteEndPhase()
        {
            host.EnsureInitialized();
            if (host.trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("End phase progression is available only in the active Trap Floor session.");
            }

            int completedRound = host.trapFloorRoundState.CurrentRoundNumber;
            TrapFloorRoundActionResult result = host.trapFloorRoundOrchestrationService.CompleteEnd(
                new TrapFloorRoundActionRequest(host.CreateCommandContext()));
            if (!result.Succeeded)
            {
                host.ShowMessage($"End completion rejected: {result.Error}.");
            }
            else if (host.trapFloorRoundState.IsScheduleCompleted)
            {
                host.ShowMessage("Round 10 schedule complete. Win/loss remains unresolved.");
            }
            else
            {
                host.ShowMessage($"Round {completedRound} End acknowledged. Round {host.trapFloorRoundState.CurrentRoundNumber} begins at Start.");
            }

            return result;
        }

        internal void AnimateAcceptedDieResult(TabletopObjectId dieId)
        {
            for (int i = 0; i < host.Registry.dieViews.Count; i++)
            {
                DieView view = host.Registry.dieViews[i];
                if (view == null || !view.IsBound || view.ObjectId != dieId)
                {
                    continue;
                }

                view.ApplyAcceptedState();
                TabletopTransformSnapshot destination = host.presentationTransitions.Capture(view.transform);
                host.presentationTransitions.AnimateFromCurrentResult(
                    view.transform,
                    new TabletopTransformSnapshot(
                        destination.Position + (Vector3.up * 0.18f),
                        destination.Rotation * Quaternion.Euler(30f, 210f, 20f),
                        destination.LocalScale),
                    host.settleDuration,
                    0.12f);
                return;
            }
        }

        // F3: before a turn ends, list what End Turn will apply (waiting Traps, a launch move) and ask.
        internal void RequestEndTurn(bool skipped)
        {
            if (host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || host.runtimeUi == null)
            {
                AdvanceTrapFloorTurn(skipped);
                return;
            }

            RevealWaitingLandingTile(true); // the tile under the pawn turns over first, then the turn ends
            PlayerId actor = host.trapFloorTurnState.ActivePlayerId;
            List<string> lines = new List<string>(4);
            if (PendingEndTurnEffects(actor, lines) == 0)
            {
                AdvanceTrapFloorTurn(skipped);
                return;
            }

            int reactions = CountUsableReactions(actor);
            string body = $"{(skipped ? "Skipping" : "Ending")} {host.PlayerLabel(actor)}'s turn applies:\n• "
                + string.Join("\n• ", lines)
                + (reactions > 0
                    ? $"\n\nYou hold {reactions} Reaction{(reactions == 1 ? string.Empty : "s")} that can stop it."
                    : "\n\nPlay a Reaction (Dodge, Shield) first if you want to stop it.");
            Action close = () => host.runtimeUi?.CloseTabletopPopup();
            List<PopupActionOption> options = new List<PopupActionOption>(3);
            if (reactions > 0)
            {
                // Owner, 2026-10-20: the player is helped to stop the trap before it applies.
                options.Add(new PopupActionOption("Use a Reaction…", true, () =>
                {
                    close();
                    ShowReactionOffer(actor);
                }));
            }

            options.Add(new PopupActionOption(
                skipped ? "Apply and Skip" : "Apply and End Turn",
                true,
                () =>
                {
                    close();
                    AdvanceTrapFloorTurn(skipped);
                }));
            options.Add(new PopupActionOption("Not yet", true, close));
            host.runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.66f),
                skipped ? "SKIP TURN?" : "END TURN?",
                body,
                options.ToArray(),
                close,
                _ => close());
        }

        // What End Turn would apply for this player now; fills lines when given. No game state changes.
        internal int PendingEndTurnEffects(PlayerId player, List<string> lines)
        {
            if (host.trapFloorAbilityResolutionState == null || player.IsEmpty)
            {
                return 0;
            }

            int count = 0;
            if (HasPendingLaunchMove(player))
            {
                count++;
                lines?.Add("Your pawn moves onto the lit tile, and that tile flips.");
            }

            IReadOnlyList<TrapFloorTrapResolutionRecord> records = host.trapFloorAbilityResolutionState.TrapRecords;
            for (int i = 0; i < records.Count; i++)
            {
                TrapFloorTrapResolutionRecord record = records[i];
                if (!record.IsPending
                    || record.AffectedPlayerId != player
                    || !record.Content.HasSupportedAssistedTrapEffect)
                {
                    continue;
                }

                count++;
                lines?.Add($"{record.Content.DisplayName}: {TrapEffectText(record.Content.TrapEffect)}");
            }

            return count;
        }

        internal static string TrapEffectText(TrapFloorTrapEffectCategory effect)
        {
            switch (effect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound: return "you are OUT for the rest of this floor.";
                case TrapFloorTrapEffectCategory.BlindNextRound: return "BLIND next round (no draw, random direction).";
                case TrapFloorTrapEffectCategory.SlowNextRound: return "SLOW next round (less movement).";
                case TrapFloorTrapEffectCategory.StickyNextRound: return "STUCK next round.";
                case TrapFloorTrapEffectCategory.SpringLaunch: return "your pawn is launched across the board (off the edge, you fall).";
                case TrapFloorTrapEffectCategory.CraneDrop: return "the Crane drops your pawn where the dice say (roll them first).";
                default: return "applied.";
            }
        }

        internal void AdvanceTrapFloorTurn()
        {
            AdvanceTrapFloorTurn(false);
        }

        internal void SkipTrapFloorTurn()
        {
            AdvanceTrapFloorTurn(true);
        }

        internal void AdvanceTrapFloorTurn(bool skipped)
        {
            host.EnsureInitialized();
            if (host.trapFloorTurnState == null || host.trapFloorTurnService == null)
            {
                host.ShowMessage("Turn tracking is unavailable outside Trap Floor.");
                return;
            }
            if (host.trapFloorTurnState.IsCurrentFloorFailed)
            {
                ShowFloorEnded(FloorFailedCountsAsWin()); // FLOORS-1: everyone out loses the floor (Survival: won if someone escaped)
                return;
            }

            bool wasFloorTurn = host.trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn;
            if (wasFloorTurn && !collapsePreviewId.IsEmpty && host.physicalFloorCollapsePending)
            {
                // ROUND-1: the dice already point at a tile; it collapses first (Undo takes the roll back instead).
                host.FeedbackLine(
                    $"Press {CollapsePreviewLabel()} first (or Undo the roll), then End Floor Turn.",
                    FeedbackSeverity.Warning);
                return;
            }

            bool reactivatesPlayers = wasFloorTurn && host.trapFloorTurnState.IsCurrentFloorFailed; // knocked out = out for the floor
            PlayerId actor = host.trapFloorTurnState.IsCurrentFloorFailed
                ? host.trapFloorTurnState.PlayerOrder[0]
                : host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                ? host.trapFloorTurnState.ActivePlayerId
                : host.trapFloorTurnState.FloorOperatorPlayerId;
            TrapFloorTurnAdvanceResult result = default;
            bool knockedOut = false;
            string trapSummary = string.Empty;
            int tilesSetAside = 0;
            host.SessionService.BeginUndoBatch();
            try
            {
                // Owner, 2026-10-21: a collapsed tile nobody lifted off is set beside the board as the floor turn ends.
                if (wasFloorTurn) tilesSetAside = SetCollapsedTilesAside();

                if (host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
                {
                    RevealWaitingLandingTile(true); // a tile still waiting turns over before the turn passes
                }

                // A Trap left waiting is applied before the turn passes (G2c); only a Reaction prevents it.
                if (!host.trapFloorTurnState.IsCurrentFloorFailed
                    && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                    && !ApplyPendingTrapsBeforeTurnEnds(actor, out trapSummary))
                {
                    return;
                }

                // A waiting Trap knocked the player out: the turn has already passed on, so it is not ended twice.
                knockedOut = !wasFloorTurn
                    && (host.trapFloorTurnState.IsCurrentFloorFailed
                        || host.trapFloorTurnState.GetPlayerState(actor)
                            == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound);
                if (!knockedOut && !wasFloorTurn) RememberPrizeLeft(actor);
                if (!wasFloorTurn) EndAbilityOffer(); // ABIL-DECK: what was not bought goes back; the deck is shuffled
                if (!knockedOut)
                {
                    result = skipped
                        ? host.trapFloorTurnService.Skip(host.matchState, host.CreateCommandContext(actor))
                        : host.trapFloorTurnService.Advance(host.matchState, host.CreateCommandContext(actor));
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            if (!knockedOut && !result.Succeeded)
            {
                string action = skipped ? "Skip Turn" : "End Turn";
                host.ShowMessage($"{trapSummary}{action} rejected: {result.Error}.");
                return;
            }

            pendingLaunchPlayerId = PlayerId.Empty;
            pendingLaunchTargetId = TabletopObjectId.Empty;

            if (host.trapFloorAbilityResolutionService != null
                && host.trapFloorAbilityResolutionService.ClearMovementAssistance())
            {
                host.abilityTargetPresenter?.ClearCurrent();
                if (host.undoTrackedMatch == host.matchState && host.undoHistory.CurrentStateIndex >= 0)
                    host.undoHistory.ReplaceCurrentState(host.SessionService.CaptureUndoSnapshot());
            }

            RefreshTrapFloorStatusUi();
            string skip = (skipped ? $"{FormatPlayerName(actor)} skipped. " : string.Empty) + trapSummary;
            if (wasFloorTurn)
            {
                string reactivated = reactivatesPlayers ? " All Players reactivated." : string.Empty;
                string setAside = tilesSetAside == 0
                    ? string.Empty
                    : $" {(tilesSetAside == 1 ? "The collapsed tile was" : $"{tilesSetAside} collapsed tiles were")} set beside the board.";
                host.ShowMessage(
                    $"ROUND {host.trapFloorTurnState.CurrentRound} — "
                    + $"{FormatPlayerName(host.trapFloorTurnState.ActivePlayerId)} turn.{reactivated}{setAside}");
                // NEXT-1 popup off (owner, 2026-10-24): the effects show on the player card's Status chip.
            }
            else if (host.trapFloorTurnState.IsCurrentFloorFailed)
            {
                host.FeedbackLine(FloorFailedCountsAsWin() // MODE-1
                    ? $"{skip}Everyone left has escaped or is out: the floor is won. {FloorEndedChoiceText(true)}"
                    : $"{skip}Everyone is out: the floor is lost. {FloorEndedChoiceText()}", FeedbackSeverity.Loss);
                ShowFloorEnded(FloorFailedCountsAsWin()); // FLOOR-2: the next phase popup at once
            }
            else if (host.trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                // ROUND-1: the floor turn's own message ("ROUND n ENDED — any player: roll…") comes from the dice watcher.
                if (!string.IsNullOrEmpty(skip)) host.ShowMessage(skip);
            }
            else
            {
                int handLimit = host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
                PlayerId next = host.trapFloorTurnState.ActivePlayerId;
                string effects = PlayerEffectChips(next);
                host.FeedbackLine( // TURN-2: always shown
                    $"{skip}{FormatPlayerName(next).ToUpperInvariant()}'S TURN{effects}: draw up to {handLimit} Controller Cards "
                        + "and 3 Abilities to buy, then move or use an Ability.",
                    FeedbackSeverity.Info);
                // NEXT-1 popup off (owner, 2026-10-24): the effects show on the player card's Status chip.
            }

            // KEYS-1: a player knocked out may leave everyone still in on the Grand Exit (not only a move wins).
            TryTakeExitsAfterMove(TabletopObjectId.Empty);
            lastTurnAdvanceFrame = Time.frameCount; // ROUND-1
        }

        private int lastTurnAdvanceFrame = -100;

        // Applies every Trap still waiting on the player, in order, before their turn passes (G2c).
        // Spring/Crane onto a real tile move the Pawn through the move/physical-commit command; a face-down
        // target flips at once and any Trap there is applied too. False keeps the turn (message shown).
        internal bool ApplyPendingTrapsBeforeTurnEnds(PlayerId player, out string summary)
        {
            summary = string.Empty;
            if (host.trapFloorAbilityResolutionState == null
                || host.trapFloorAbilityResolutionService == null
                || host.trapFloorTurnService == null)
            {
                return true;
            }

            for (int guard = 0; guard < 64; guard++)
            {
                if (host.trapFloorTurnState.IsCurrentFloorFailed
                    || host.trapFloorTurnState.GetPlayerState(player)
                        == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    break;
                }

                if (HasPendingLaunchMove(player))
                {
                    TabletopObjectId target = pendingLaunchTargetId;
                    pendingLaunchPlayerId = PlayerId.Empty;
                    pendingLaunchTargetId = TabletopObjectId.Empty;
                    if (!TryAutoMovePawnToFloor(player, target, out TrapFloorCoordinate targetTile))
                    {
                        host.ShowMessage("A Trap still has to move your pawn, but the move was rejected. Move it by hand, then End Turn.");
                        return false;
                    }

                    if (host.trapFloorAbilityResolutionService.ClearMovementAssistance())
                        host.abilityTargetPresenter?.ClearCurrent();
                    summary += $"Pawn moved to {targetTile}. ";
                    if (TryLandingFlipNow(player, target, out string flipped))
                        summary += $"Flipped {flipped}. ";
                    continue;
                }

                if (!TryGetOldestPendingTrap(player, out TrapFloorTrapResolutionRecord trap))
                    break;

                if (trap.Content.IsLaunchTrap)
                {
                    TrapFloorLaunchResult launch = host.trapFloorAbilityResolutionService.ResolveLaunchTrap(
                        host.matchState,
                        host.CreateCommandContext(player),
                        trap.FloorCardId,
                        host.trapFloorTurnService);
                    if (!launch.Succeeded)
                    {
                        host.ShowMessage(launch.Error == TrapFloorLaunchError.DieBeyondGrid
                            ? "CRANE — a die shows more than this floor has: roll that die again, then End Turn again."
                            : launch.Error == TrapFloorLaunchError.DiceNotSettled
                            ? "CRANE — roll both dice and let them settle, then End Turn again."
                            : $"End Turn: {trap.Content.DisplayName} could not be applied ({launch.Error}).");
                        return false;
                    }

                    if (launch.IsEliminated)
                    {
                        summary += $"{trap.Content.DisplayName}: {FormatPlayerName(player).ToUpperInvariant()} ELIMINATED. ";
                        continue;
                    }

                    pendingLaunchPlayerId = player;
                    pendingLaunchTargetId = launch.TargetFloorCardId;
                    continue;
                }

                TrapFloorTurnAdvanceResult applied = host.trapFloorAbilityResolutionService.ResolveSupportedTrap(
                    host.matchState,
                    host.CreateCommandContext(player),
                    trap.FloorCardId,
                    host.trapFloorTurnService);
                if (!applied.Succeeded)
                {
                    host.ShowMessage($"End Turn: {trap.Content.DisplayName} could not be applied ({applied.Error}).");
                    return false;
                }

                summary += trap.Content.TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound
                    ? $"{trap.Content.DisplayName}: {FormatPlayerName(player).ToUpperInvariant()} ELIMINATED. "
                    : $"{trap.Content.DisplayName} applied. ";
            }

            RefreshMovementAssistancePresentation();
            return true;
        }

        internal bool HasPendingLaunchMove(PlayerId player)
        {
            TrapFloorDodgeAssistanceState assistance = host.trapFloorAbilityResolutionState.ActiveDodgeAssistance;
            return pendingLaunchPlayerId == player
                && !pendingLaunchTargetId.IsEmpty
                && assistance != null
                && assistance.PlayerId == player
                && assistance.TargetFloorCardIds.Count == 1
                && assistance.TargetFloorCardIds[0] == pendingLaunchTargetId;
        }

        internal bool TryGetOldestPendingTrap(PlayerId player, out TrapFloorTrapResolutionRecord trap)
        {
            IReadOnlyList<TrapFloorTrapResolutionRecord> records = host.trapFloorAbilityResolutionState.TrapRecords;
            for (int i = 0; i < records.Count; i++)
            {
                TrapFloorTrapResolutionRecord candidate = records[i];
                if (candidate.IsPending
                    && candidate.AffectedPlayerId == player
                    && candidate.Content.HasSupportedAssistedTrapEffect)
                {
                    trap = candidate;
                    return true;
                }
            }

            trap = null;
            return false;
        }

        // Moves the player's Pawn onto a Floor tile through the authority: a physical Pawn by a physical
        // commit resolved on the table surface, otherwise by MoveObject. Never a direct transform write.
        internal bool TryAutoMovePawnToFloor(
            PlayerId player,
            TabletopObjectId floorCardId,
            out TrapFloorCoordinate targetTile)
        {
            targetTile = default;
            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                || !host.matchState.Pawns.TryGetValue(setup.PawnId, out PawnState pawn)
                || !host.matchState.Cards.TryGetValue(floorCardId, out CardInstanceState floor)
                || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorState))
            {
                return false;
            }

            targetTile = floorState.Coordinate;
            TabletopPose currentPose = pawn.BaseState.Pose;
            TabletopPose targetPose = new TabletopPose(
                floor.BaseState.Pose.Position,
                currentPose.RotationDegrees,
                currentPose.Layer,
                currentPose.LocalOrder);
            PawnView pawnView = null;
            for (int i = 0; i < host.Registry.pawnViews.Count; i++)
            {
                if (host.Registry.pawnViews[i] != null && host.Registry.pawnViews[i].IsBound && host.Registry.pawnViews[i].ObjectId == pawn.BaseState.Id)
                {
                    pawnView = host.Registry.pawnViews[i];
                    break;
                }
            }

            // F4: remember where the pawn was seen, so it can hop from there to the tile.
            Transform hopVisual = pawnView != null ? pawnView.transform.Find("VisualRoot") : null;
            Vector3 hopStart = hopVisual != null
                ? hopVisual.position
                : pawnView != null ? pawnView.transform.position : Vector3.zero;
            TabletopTransformSnapshot hopRootStart = pawnView != null
                ? host.presentationTransitions.Capture(pawnView.transform)
                : default;
            bool moved;
            if (pawn.BaseState.PhysicalState != null
                && host.physicalAuthority != null
                && pawnView != null
                && pawnView.PhysicalObject != null)
            {
                if (!host.physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(
                        targetPose,
                        player,
                        pawnView.PhysicalObject.PhysicalCollider,
                        pawn.BaseState.IsUserLocked,
                        out PhysicalObjectState placed))
                {
                    return false;
                }

                moved = host.TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                    new CommitPhysicalObjectCommand(
                        host.CreateCommandContext(player),
                        pawn.BaseState.Id,
                        placed,
                        pawn.BaseState.PhysicalRevision,
                        null,
                        AuthoritativeActionRecordMode.Transaction)).Succeeded;
            }
            else
            {
                moved = host.TableAuthority.Submit<MoveObjectCommand, MoveObjectResult>(
                    new MoveObjectCommand(host.CreateCommandContext(player), pawn.BaseState.Id, targetPose)).Succeeded;
            }

            if (!moved) return false;
            pawnView?.ApplyAcceptedState();
            landingPawnTiles[pawn.BaseState.Id] = targetTile;
            StartPawnHop(pawnView, hopVisual, hopStart, hopRootStart);
            return true;
        }

        // F4: a pawn moved for the player hops from where it was to where it now stands. A physical pawn hops its
        // VisualRoot child only (the body is already placed); otherwise the whole pawn is animated by the
        // Presentation transitions. Never changes game state.
        internal void StartPawnHop(
            PawnView pawnView,
            Transform visual,
            Vector3 startWorld,
            TabletopTransformSnapshot rootStart)
        {
            if (pawnView == null) return;
            if (visual == null)
            {
                host.presentationTransitions?.AnimateFromCurrentResult(pawnView.transform, rootStart, host.pawnHopSeconds, host.pawnHopHeight);
                return;
            }

            Vector3 restLocal = visual.localPosition;
            for (int i = host.pawnHops.Count - 1; i >= 0; i--)
            {
                if (host.pawnHops[i].Visual == visual)
                {
                    restLocal = host.pawnHops[i].RestLocal; // a hop already running keeps the true rest place
                    host.pawnHops.RemoveAt(i);
                }
            }

            host.pawnHops.Add(new PawnHop
            {
                Visual = visual,
                RestLocal = restLocal,
                StartWorld = startWorld,
                Started = Time.unscaledTime,
            });
        }

        internal void FinishPawnHops()
        {
            for (int i = 0; i < host.pawnHops.Count; i++)
            {
                if (host.pawnHops[i].Visual != null) host.pawnHops[i].Visual.localPosition = host.pawnHops[i].RestLocal;
            }

            host.pawnHops.Clear();
        }

        // Flips a face-down, usable target at once for the player (same Landing reveal as a hand move).
        internal bool TryLandingFlipNow(PlayerId player, TabletopObjectId floorCardId, out string flippedName)
        {
            flippedName = string.Empty;
            if (host.trapFloorRevealFloorUseCase == null
                || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || floorCard.IsRevealed
                || host.trapFloorCollapseState.IsCollapsed(floorCardId))
            {
                return false;
            }

            TrapFloorRevealFloorResult result = host.TableAuthority.Submit<TrapFloorRevealFloorCommand, TrapFloorRevealFloorResult>(
                new TrapFloorRevealFloorCommand(
                    host.CreateCommandContext(player),
                    floorCardId,
                    TrapFloorSearchKind.Landing,
                    Array.Empty<TabletopObjectId>()));
            if (!result.Succeeded) return false;
            if (host.Registry.TryGetCardView(floorCardId, out CardView floorCardView))
                RevealFloorCardView(floorCardView);
            host.RefreshCardContentVisibility();
            host.activeFloorRevealActivity = result.RevealedActivity;
            flippedName = result.FloorCard.Content.DisplayName;
            return true;
        }

        internal string TrapFloorActionHelpText()
        {
            switch (host.trapFloorRoundState.Phase)
            {
                case TrapFloorRoundPhase.Start:
                    return host.trapFloorRoundState.CurrentRoundNumber == 1
                        ? string.Empty
                        : "Move your pawns by hand as the round starts, then continue.";
                case TrapFloorRoundPhase.Search:
                    return "Choose the player who searches next.";
                case TrapFloorRoundPhase.Trigger:
                    return "Carry out the card's effect at the table, then mark it done.";
                case TrapFloorRoundPhase.Floorfall:
                    return "Roll as many Floorfalls as your mode asks for, then continue.";
                case TrapFloorRoundPhase.End:
                    return "Check together who survived and whether you won, then continue.";
                case TrapFloorRoundPhase.Completed:
                    return "All 10 rounds are played. Decide the result together.";
                default:
                    return string.Empty;
            }
        }

        internal void ShowPendingFloormasterCardContextMenu()
        {
            TrapFloorPendingFloormasterCard pendingCard = host.floormasterLifecycleState.PendingCard;
            bool canComplete = host.trapFloorRoundState != null
                && host.trapFloorRoundState.Phase == TrapFloorRoundPhase.Trigger;
            TabletopObjectId targetCardId = pendingCard.CardId;
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                "PENDING FLOORMASTER CARD",
                $"Category: {pendingCard.Category}\nCarry out its effect at the table, then mark it done.",
                new[]
                {
                    new PopupActionOption(
                        "Inspect",
                        true,
                        () => host.CardInspectPanel.OpenCardInspect(targetCardId)),
                    new PopupActionOption(
                        "Card effect done",
                        canComplete,
                        () =>
                        {
                            TrapFloorRoundTriggerResult result = CompletePendingFloormasterTrigger();
                            if (result.Succeeded)
                            {
                                host.ContextMenus.CloseContextMenu();
                            }
                        }),
                    new PopupActionOption(
                        "Duplicate as Generic Card",
                        true,
                        () => host.BeginDuplicatePlacement(targetCardId)),
                },
                host.ContextMenus.CloseContextMenu,
                host.ContextMenus.DismissPopupFromSecondary);
        }

        internal void ShowFloorCardContextMenu()
        {
            if (!host.trapFloorTemplate.TryGetFloorCardState(
                    host.matchState,
                    host.contextMenuCardId,
                    out TrapFloorFloorCardState floorCard))
            {
                host.ContextMenus.CloseContextMenu();
                return;
            }

            TabletopObjectId targetCardId = host.contextMenuCardId;
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (IsArrangingTable)
            {
                actions.Add(new PopupActionOption("Move board", true, BeginArrangeBoardMove)); // LAYOUT-1
            }

            bool isCollapsed = host.trapFloorCollapseState != null
                && host.trapFloorCollapseState.IsCollapsed(targetCardId);
            if (floorCard.IsRevealed
                && floorCard.Content.HasSupportedAssistedTrapEffect
                && IsTrapFloorTrapPending(targetCardId))
            {
                actions.Add(new PopupActionOption(
                    "Resolve Trap",
                    CanResolveTrapFloorTrap(targetCardId),
                    () => ResolveTrapFloorTrap(targetCardId)));
            }
            else if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveUseCase != null)
            {
                if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                    && host.trapFloorTurnState != null
                    && !host.trapFloorObjectiveState.HasPlayerClaimedKey(targetCardId, host.trapFloorTurnState.ActivePlayerId)) // KEYS-3
                {
                    actions.Add(new PopupActionOption(
                        "Claim Key",
                        !host.trapFloorObjectiveState.IsWon,
                        () => ClaimTrapFloorKey(targetCardId)));
                }
                // Owner, 2026-10-21: exits are taken by landing on them (no Attempt Escape button).
            }

            actions.Add(new PopupActionOption(
                "Inspect",
                true,
                () => host.CardInspectPanel.OpenCardInspect(targetCardId)));
            actions.Add(new PopupActionOption(
                "Duplicate as Generic Card",
                true,
                () => host.BeginDuplicatePlacement(targetCardId)));
            host.runtimeUi.ShowContextMenu(
                host.contextMenuAnchorScreenPosition,
                $"FLOOR {floorCard.Coordinate}",
                isCollapsed
                    ? $"COLLAPSED HOLE — Floor {floorCard.Coordinate} is permanently unusable."
                    : floorCard.IsRevealed
                    ? FloorCardContextDescription(floorCard)
                    : "MYSTERY — content is hidden until the turn player's pawn lands on it.",
                actions,
                host.ContextMenus.CloseContextMenu,
                host.ContextMenus.DismissPopupFromSecondary);
        }

        internal string FloorCardContextDescription(TrapFloorFloorCardState floorCard)
        {
            string description =
                $"Revealed: {floorCard.Content.Category} — {floorCard.Content.DisplayName}";
            if (floorCard.Content.Category == TrapFloorFloorContentCategory.Trap)
            {
                if (WasTrapSafelyRevealed(floorCard.ObjectId))
                {
                    return $"{description}\nCAREFUL SEARCH — this reveal did not trigger the Trap; it remains a Trap.";
                }
                if (IsTrapFloorTrapNeutralized(floorCard.ObjectId))
                {
                    return $"{description}\nTRAP NEUTRALIZED — this Floor is safe from assisted Trap consequences.";
                }
                if (IsTrapFloorTrapResolved(floorCard.ObjectId))
                {
                    return $"{description}\nTRAP RESOLVED — its assisted consequence has already been applied or prevented.";
                }
                return $"{description}\n{TrapFloorTrapEffectDescription(floorCard.Content)}";
            }
            if (host.trapFloorObjectiveState == null)
            {
                return description;
            }

            if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key)
            {
                return host.trapFloorObjectiveState.TryGetClaim(
                        floorCard.ObjectId,
                        out TrapFloorCollectedKeyState claim)
                    ? $"{description}\nKEY STATUS: taken first by {FormatPlayerName(claim.ClaimedByPlayerId)}; every player may take it once."
                    : $"{description}\nKEY STATUS: UNCLAIMED — use Claim Key to collect it.";
            }

            if (floorCard.Content.Category == TrapFloorFloorContentCategory.SecretExit)
            {
                return TrapFloorObjectiveUseCase.IsGrandExit(floorCard.Content)
                    ? $"{description}\nEvery player still in stands here with the team's Keys to win the floor. "
                        + $"KEYS {host.trapFloorObjectiveState.CollectedKeyCount} / {host.trapFloorObjectiveState.RequiredKeyCount}"
                    : $"{description}\nLand here holding a Key you claimed to win the floor.";
            }

            return description;
        }

        internal bool CanResolveTrapFloorTrap(TabletopObjectId floorCardId)
        {
            return host.trapFloorTurnState != null
                && host.trapFloorTurnService != null
                && host.trapFloorAbilityResolutionService != null
                && host.trapFloorAbilityResolutionState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.IsPending
                && trap.AffectedPlayerId == host.trapFloorTurnState.ActivePlayerId;
        }

        internal static string TrapFloorTrapEffectDescription(
            TrapFloorFloorContentDefinition content)
        {
            switch (content.TrapEffect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound:
                    return "Assisted consequence: the resolving Player is out for the rest of this floor.";
                case TrapFloorTrapEffectCategory.BlindNextRound:
                    return "Assisted consequence: no Controller Card draw next Round; roll a d4 for movement direction.";
                case TrapFloorTrapEffectCategory.SlowNextRound:
                    return $"Assisted consequence: movement {FormatSignedModifier(content.MovementModifier.Value)} next Round.";
                case TrapFloorTrapEffectCategory.StickyNextRound:
                    return "Assisted consequence: stay in place during the next Round.";
                case TrapFloorTrapEffectCategory.SpringLaunch:
                    return $"Assisted consequence: launched 1 tile {content.LaunchDirection.ToString().ToLowerInvariant()}; off the grid or onto a hole, out for this floor.";
                case TrapFloorTrapEffectCategory.CraneDrop:
                    return "Assisted consequence: roll both dice (column, row) and move there; onto a hole, out for this floor.";
                default:
                    return "Manual consequence: follow the authored Trap text.";
            }
        }

        internal void ResolveTrapFloorTrap(TabletopObjectId floorCardId)
        {
            if (!CanResolveTrapFloorTrap(floorCardId)
                || !host.trapFloorTemplate.TryGetFloorCardState(
                    host.matchState,
                    floorCardId,
                    out TrapFloorFloorCardState floorCard)
                || !floorCard.IsRevealed
                || !floorCard.Content.HasSupportedAssistedTrapEffect)
            {
                host.ShowMessage("Resolve Trap is unavailable for this Trap or assistance state.");
                return;
            }

            if (floorCard.Content.IsLaunchTrap)
            {
                ResolveTrapFloorLaunchTrap(floorCardId, floorCard.Content);
                return;
            }

            PlayerId affectedPlayerId = host.trapFloorTurnState.ActivePlayerId;
            TrapFloorTurnAdvanceResult result = host.trapFloorAbilityResolutionService.ResolveSupportedTrap(
                host.matchState,
                host.CreateCommandContext(affectedPlayerId),
                floorCardId,
                host.trapFloorTurnService);
            if (!result.Succeeded)
            {
                host.ShowMessage($"Resolve Trap rejected: {result.Error}.");
                return;
            }

            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            RefreshTrapFloorStatusUi();
            if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.BlindNextRound)
            {
                host.ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be BLIND during "
                    + $"Round {host.trapFloorTurnState.CurrentRound + 1}.");
            }
            else if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.SlowNextRound)
            {
                host.ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be SLOWED "
                    + $"during Round {host.trapFloorTurnState.CurrentRound + 1}.");
            }
            else if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.StickyNextRound)
            {
                host.ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be STICKY "
                    + $"during Round {host.trapFloorTurnState.CurrentRound + 1}.");
            }
            else
            {
                host.ShowMessage($"{FormatPlayerName(affectedPlayerId).ToUpperInvariant()} ELIMINATED");
            }
        }

        // Spring / Crane (G2b): eliminate, or highlight the tile the player must move their Pawn to.
        internal void ResolveTrapFloorLaunchTrap(
            TabletopObjectId floorCardId,
            TrapFloorFloorContentDefinition content)
        {
            PlayerId affectedPlayerId = host.trapFloorTurnState.ActivePlayerId;
            bool spring = content.TrapEffect == TrapFloorTrapEffectCategory.SpringLaunch;
            string trapName = spring ? "SPRING" : "CRANE";
            TrapFloorLaunchResult result = host.trapFloorAbilityResolutionService.ResolveLaunchTrap(
                host.matchState,
                host.CreateCommandContext(affectedPlayerId),
                floorCardId,
                host.trapFloorTurnService);
            if (!result.Succeeded)
            {
                host.ShowMessage(result.Error == TrapFloorLaunchError.DieBeyondGrid
                    ? "CRANE — a die shows more than this floor has: roll that die again, then choose Resolve Trap again."
                    : result.Error == TrapFloorLaunchError.DiceNotSettled
                    ? "CRANE — roll both dice and let them settle, then choose Resolve Trap again."
                    : $"Resolve Trap rejected: {result.Error}.");
                return;
            }

            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            RefreshMovementAssistancePresentation();
            RefreshTrapFloorStatusUi();
            if (result.IsEliminated)
            {
                string where = result.TargetOnGrid
                    ? $"onto a hole at ({result.TargetX},{result.TargetY})"
                    : "off the floor";
                host.ShowMessage($"{trapName} — {FormatPlayerName(affectedPlayerId).ToUpperInvariant()} launched {where}. ELIMINATED");
                return;
            }

            pendingLaunchPlayerId = affectedPlayerId;
            pendingLaunchTargetId = result.TargetFloorCardId;
            // TRAP-1: an instruction, shown even with action messages off.
            host.FeedbackLine(
                $"{trapName} — move your pawn to the lit tile ({result.TargetX},{result.TargetY}). "
                + "A face-down tile there flips when you land. End Turn moves it for you.",
                FeedbackSeverity.Info);
        }

        internal void ShowFocusedSearchReveal(TrapFloorRevealFloorResult result)
        {
            if (host.runtimeUi == null
                || host.matchState == null
                || host.trapFloorTemplate == null
                || !host.matchState.Cards.TryGetValue(
                    result.FloorCard.ObjectId,
                    out CardInstanceState card)
                || card.Face != CardFace.FaceUp
                || !host.trapFloorTemplate.TryGetFloorCardState(
                    host.matchState,
                    result.FloorCard.ObjectId,
                    out TrapFloorFloorCardState revealedFloor)
                || !revealedFloor.IsRevealed)
            {
                host.ShowMessage("The tile was revealed, but its card could not be shown.");
                return;
            }

            // The tile's own picture is its content's art; its back is the round's floor back (A1).
            Texture backArtwork = host.CurrentBoardCardBack();
            Texture frontArtwork = null;
            if (host.TryGetAuthoredCardDefinition(revealedFloor.Content.Id, out CardDefinition contentDefinition))
            {
                frontArtwork = host.FrontArt(contentDefinition);
            }
            else if (host.TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition definition))
            {
                Texture2D definitionBack = host.BackArt(definition);
                backArtwork = definitionBack != null ? definitionBack : backArtwork;
                frontArtwork = host.FrontArt(definition);
            }

            TabletopObjectId revealedId = revealedFloor.ObjectId;
            bool trapWaiting = !result.TrapSuppressed
                && revealedFloor.Content.HasSupportedAssistedTrapEffect
                && IsTrapFloorTrapPending(revealedId)
                && CanResolveTrapFloorTrap(revealedId);
            string body = result.TrapSuppressed
                ? $"{revealedFloor.Content.DisplayText}\n\nREVEALED SAFELY"
                : trapWaiting
                ? $"{revealedFloor.Content.DisplayText}\n\nResolve it now, or return to play a Reaction first. "
                    + "End Turn applies it if you leave it."
                : revealedFloor.Content.DisplayText;
            // Owner, 2026-10-20: a player holding a Reaction that can stop this trap is offered it right here.
            int usableReactions = trapWaiting ? CountUsableReactions(host.trapFloorTurnState.ActivePlayerId) : 0;
            if (usableReactions > 0)
            {
                body = $"{revealedFloor.Content.DisplayText}\n\nYou hold {usableReactions} Reaction"
                    + $"{(usableReactions == 1 ? string.Empty : "s")} that can stop it. Use one now, or let it happen.";
            }

            const string header = "YOU LANDED ON";
            if (revealedFloor.Content.Category == TrapFloorFloorContentCategory.Key
                && IsUnclaimedKeyForTurnPlayer(revealedId, out TrapFloorFloorCardState keyFloor))
            {
                ShowKeyPopup(keyFloor, header);
                return;
            }

            // TRAP-2 / PRIZE-3 (owner, 2026-10-22, gameplay reference): a Trap shows its effect and when it happens,
            // with Apply; an Ability or Friend offers Take it or Reject.
            if (trapWaiting)
            {
                ShowTrapPopup(revealedFloor, header, backArtwork, frontArtwork);
                return;
            }

            if (TryOfferPrizePopup(revealedFloor, header, backArtwork, frontArtwork)) return;

            string state = $"{revealedFloor.Content.Category.ToString().ToUpperInvariant()} • REVEALED";
            FocusedCardRevealModel model = new FocusedCardRevealModel(
                revealedId,
                backArtwork,
                frontArtwork,
                header,
                state,
                revealedFloor.Content.DisplayName,
                revealedFloor.Content.DisplayText,
                body,
                "OK",
                CloseFocusedSearchReveal);
            host.runtimeUi.ShowFocusedCardReveal(model);
        }

        // ---------- TRAP-2 / PRIZE-3 popups (owner, 2026-10-22, gameplay reference, doc 34) ----------

        // When a Trap's effect happens: now, or on the player's next turn.
        internal static string TrapTimingTag(TrapFloorFloorContentDefinition content)
        {
            switch (content.TrapEffect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound:
                case TrapFloorTrapEffectCategory.SpringLaunch:
                case TrapFloorTrapEffectCategory.CraneDrop:
                    return "NOW";
                case TrapFloorTrapEffectCategory.BlindNextRound:
                case TrapFloorTrapEffectCategory.SlowNextRound:
                case TrapFloorTrapEffectCategory.StickyNextRound:
                    return "NEXT ROUND";
                default:
                    return "FOLLOW THE CARD";
            }
        }

        internal static string TrapTimingText(TrapFloorFloorContentDefinition content)
        {
            switch (content.TrapEffect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound:
                    return "NOW: you fall and are out for the rest of this floor.";
                case TrapFloorTrapEffectCategory.SpringLaunch:
                    return $"NOW: you are launched 1 tile {content.LaunchDirection.ToString().ToLowerInvariant()}; "
                        + "off the floor or onto a hole, you are out for this floor.";
                case TrapFloorTrapEffectCategory.CraneDrop:
                    return "NOW: roll the two floor dice (column, row), then Apply: the Crane drops your pawn there; "
                        + "onto a hole, you are out for this floor.";
                case TrapFloorTrapEffectCategory.BlindNextRound:
                    return "NEXT ROUND: you draw no Controller Cards, and you roll the d4 for the only direction you may move.";
                case TrapFloorTrapEffectCategory.SlowNextRound:
                    return content.MovementModifier.HasValue
                        ? $"NEXT ROUND: movement {FormatSignedModifier(content.MovementModifier.Value)}."
                        : "NEXT ROUND: you move less.";
                case TrapFloorTrapEffectCategory.StickyNextRound:
                    return "NEXT ROUND: you stay where you are.";
                default:
                    return "Do what the card says.";
            }
        }

        // The Trap popup: its effect, NOW or NEXT ROUND, and Apply (a held Reaction may stop it instead).
        private void ShowTrapPopup(TrapFloorFloorCardState floor, string header, Texture backArtwork, Texture frontArtwork)
        {
            if (host.runtimeUi == null || host.trapFloorTurnState == null) return;
            TabletopObjectId trapId = floor.ObjectId;
            PlayerId active = host.trapFloorTurnState.ActivePlayerId;
            int usableReactions = CountUsableReactions(active);
            string body = $"{floor.Content.DisplayText}\n\n{TrapTimingText(floor.Content)}"
                + (usableReactions > 0
                    ? $"\n\nYou hold {usableReactions} Reaction{(usableReactions == 1 ? string.Empty : "s")} that can stop it."
                    : string.Empty);
            host.runtimeUi.ShowFocusedCardReveal(new FocusedCardRevealModel(
                trapId,
                backArtwork,
                frontArtwork,
                header,
                $"TRAP • {TrapTimingTag(floor.Content)}",
                floor.Content.DisplayName,
                floor.Content.DisplayText,
                body,
                "Apply",
                () =>
                {
                    CloseFocusedSearchReveal();
                    ResolveTrapFloorTrap(trapId);
                },
                usableReactions > 0 ? "Use a Reaction…" : null,
                usableReactions > 0 ? (Action)(() => ShowReactionOffer(active)) : null));
        }

        // The Ability or Friend popup for the turn player standing on it: Take it (an Ability's cost is paid) or Reject.
        // Survival's endgame turns prizes off. False when the tile is not a prize on offer.
        private bool TryOfferPrizePopup(TrapFloorFloorCardState floor, string header, Texture backArtwork, Texture frontArtwork)
        {
            if (host.runtimeUi == null || !TrapFloorTakePrizeUseCase.IsPrize(floor.Content)) return false;
            if (IsSurvivalEndgame())
            {
                host.runtimeUi.ShowFocusedCardReveal(new FocusedCardRevealModel(
                    floor.ObjectId,
                    backArtwork,
                    frontArtwork,
                    header,
                    $"{floor.Content.Category.ToString().ToUpperInvariant()} • OFF",
                    floor.Content.DisplayName,
                    floor.Content.DisplayText,
                    $"{floor.Content.DisplayText}\n\nSurvival: the Grand Exit is open, so prizes can no longer be taken.",
                    "OK",
                    CloseFocusedSearchReveal));
                return true;
            }

            if (!TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prize) || prize.ObjectId != floor.ObjectId) return false;
            InputCostData cost = TrapFloorTakePrizeUseCase.CostOf(floor.Content);
            string costLine = cost.IsEmpty
                ? "It is free."
                : $"Cost {InputCostNotation.Format(cost)}: pay it from your Hand through your Console.";
            PlayerId active = host.trapFloorTurnState.ActivePlayerId;
            host.runtimeUi.ShowFocusedCardReveal(new FocusedCardRevealModel(
                floor.ObjectId,
                backArtwork,
                frontArtwork,
                header,
                $"{floor.Content.Category.ToString().ToUpperInvariant()} • PRIZE",
                floor.Content.DisplayName,
                floor.Content.DisplayText,
                $"{floor.Content.DisplayText}\n\nTake it? {costLine} It comes to your hand when you move off this tile.",
                "Take it",
                () =>
                {
                    CloseFocusedSearchReveal();
                    BeginTakeFloorPrize();
                },
                "Reject",
                () =>
                {
                    CloseFocusedSearchReveal();
                    RememberPrizeLeft(active);
                    RefreshTrapFloorStatusUi();
                    host.FeedbackLine(
                        $"{FormatPlayerName(active)} left {floor.Content.DisplayName}. The next player to land here may take it.",
                        FeedbackSeverity.Info);
                }));
            return true;
        }

        // A face-up tile's pictures for a popup: its content's front, the floor's back.
        private void FloorPopupArt(TrapFloorFloorCardState floor, out Texture backArtwork, out Texture frontArtwork)
        {
            backArtwork = host.CurrentBoardCardBack();
            frontArtwork = host.TryGetAuthoredCardDefinition(floor.Content.Id, out CardDefinition definition)
                ? host.FrontArt(definition)
                : null;
        }

        internal void CloseFocusedSearchReveal()
        {
            host.runtimeUi?.CloseFocusedCardSelection();
        }

        internal void ReplaceCurrentUndoStateForPurchaseAssistance()
        {
            if (host.undoTrackedMatch != host.matchState || host.undoHistory.CurrentStateIndex < 0) return;
            host.undoHistory.ReplaceCurrentState(host.SessionService.CaptureUndoSnapshot());
            host.SessionService.RefreshUndoUi();
        }

        // PRIZE-2 (owner, 2026-10-22: "the card from the floor is removed when the player moves next, otherwise that
        // player will fall"): every taken prize still on the floor that no pawn stands on goes to its taker's hand (or
        // beside their Console), amending the move that took the pawn off. Event-time only.
        private void CollectTakenPrizesOffPawns()
        {
            if (host.trapFloorCollapseState == null || host.trapFloorTakePrizeUseCase == null) return;
            IReadOnlyList<TrapFloorCollapsedFloorState> floors = host.trapFloorCollapseState.CollapsedFloors;
            for (int i = 0; i < floors.Count; i++)
            {
                TrapFloorCollapsedFloorState taken = floors[i];
                if (!taken.IsTakenPrize
                    || !IsTakenPrizeOnFloor(taken.FloorCardId)
                    || IsAnyPawnOnFloorCard(taken.FloorCardId))
                {
                    continue;
                }

                TrapFloorTakePrizeResult result = host.TableAuthority.Submit<TrapFloorCollectPrizeCommand, TrapFloorTakePrizeResult>(
                    new TrapFloorCollectPrizeCommand(host.CreateCommandContext(taken.CollapsedByPlayerId), taken.FloorCardId));
                if (!result.Succeeded) continue;
                if (!result.DestinationContainerId.IsEmpty) host.ViewSync.SyncContainer(result.DestinationContainerId);
                else host.PresentAcceptedObject(taken.FloorCardId); // loose beside the Console
                ApplyCollapsedFloorPresentation(taken.FloorCardId); // its look as an ordinary card
                host.RefreshAcceptedCardFaces();
                host.RefreshCardContentVisibility();
                host.RefreshSelectionPresenterAfterRuntimeProjection();
                host.FeedbackLine(
                    result.PlacedInHand
                        ? $"{result.FloorCard.Content.DisplayName} came off the floor into {FormatPlayerName(taken.CollapsedByPlayerId)}'s hand. Its space is now empty."
                        : $"{result.FloorCard.Content.DisplayName} came off the floor; {FormatPlayerName(taken.CollapsedByPlayerId)}'s hand is full, so it is beside their Console. Its space is now empty.",
                    FeedbackSeverity.Info);
            }
        }

        // KEYS-2 (owner, 2026-10-22, gameplay reference: "if a key then show the key and give option in popup to take
        // key and no other option"): the active player's pawn landing on an unclaimed Key that was already face up
        // (another player revealed it) shows the Key popup; a Key this landing revealed gets it from the reveal popup.
        // Claiming straight away (KEYS-1) closed that popup. Event-time only.
        private TabletopObjectId keyRevealedThisLanding;

        private void OfferRevealedKeyOnLanding(TabletopObjectId floorCardId)
        {
            TabletopObjectId revealedNow = keyRevealedThisLanding;
            keyRevealedThisLanding = TabletopObjectId.Empty;
            if (floorCardId.IsEmpty || floorCardId == revealedNow) return; // the reveal popup already offers it
            if (IsUnclaimedKeyForTurnPlayer(floorCardId, out TrapFloorFloorCardState floorCard))
            {
                ShowKeyPopup(floorCard, "YOU LANDED ON");
                return;
            }

            // PRIZE-3 / MODE-2: a face-up Ability or Friend offers itself again; in Survival's endgame a face-up Trap
            // triggers again.
            if (host.trapFloorTurnState == null
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floor)
                || !floor.IsRevealed
                || (host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(floorCardId)))
            {
                return;
            }

            FloorPopupArt(floor, out Texture backArtwork, out Texture frontArtwork);
            if (floor.Content.Category == TrapFloorFloorContentCategory.Trap)
            {
                PlayerId active = host.trapFloorTurnState.ActivePlayerId;
                if (IsSurvivalEndgame()
                    && floor.Content.HasSupportedAssistedTrapEffect
                    && host.trapFloorAbilityResolutionService != null
                    && host.trapFloorAbilityResolutionService.RetriggerRevealedTrap(
                        host.matchState,
                        host.CreateCommandContext(active),
                        floorCardId,
                        active))
                {
                    RefreshTrapFloorStatusUi();
                    ShowTrapPopup(floor, "TRIGGERED AGAIN", backArtwork, frontArtwork);
                }

                return;
            }

            TryOfferPrizePopup(floor, "YOU LANDED ON", backArtwork, frontArtwork);
        }

        // FIX-3: the unclaimed, face-up Key under the turn player's pawn, if any.
        private bool TryGetActivePawnUnclaimedKey(out TrapFloorFloorCardState floorCard)
        {
            floorCard = null;
            return host.trapFloorTemplate != null
                && host.trapFloorTurnState != null
                && TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player)
                && TryReadPawnTile(player.PawnId, out TrapFloorCoordinate tile)
                && host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                && IsUnclaimedKeyForTurnPlayer(floorCardId, out floorCard);
        }

        private bool IsUnclaimedKeyForTurnPlayer(TabletopObjectId floorCardId, out TrapFloorFloorCardState floorCard)
        {
            floorCard = null;
            return !floorCardId.IsEmpty
                && host.trapFloorObjectiveState != null
                && !host.trapFloorObjectiveState.IsWon
                && host.trapFloorTurnState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out floorCard)
                && floorCard.IsRevealed
                && floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                && !host.trapFloorObjectiveState.HasPlayerClaimedKey(floorCardId, host.trapFloorTurnState.ActivePlayerId); // KEYS-3
        }

        // The Key popup: the Key's card and one button, Take Key (it claims the Key for the turn player).
        private void ShowKeyPopup(TrapFloorFloorCardState floorCard, string header)
        {
            if (host.runtimeUi == null) return;
            Texture frontArtwork = host.TryGetAuthoredCardDefinition(floorCard.Content.Id, out CardDefinition keyDefinition)
                ? host.FrontArt(keyDefinition)
                : null;
            TabletopObjectId keyId = floorCard.ObjectId;
            host.runtimeUi.ShowFocusedCardReveal(new FocusedCardRevealModel(
                keyId,
                host.CurrentBoardCardBack(),
                frontArtwork,
                header,
                "KEY • REVEALED",
                floorCard.Content.DisplayName,
                floorCard.Content.DisplayText,
                $"{floorCard.Content.DisplayText}\n\nTake the Key: it is yours to carry to an Exit.",
                "Take Key",
                () =>
                {
                    CloseFocusedSearchReveal();
                    ClaimTrapFloorKey(keyId);
                }));
        }

        internal void ClaimTrapFloorKey(TabletopObjectId floorCardId)
        {
            if (host.trapFloorObjectiveUseCase == null)
            {
                host.ShowMessage("Claim Key unavailable outside Trap Floor.");
                return;
            }

            TrapFloorObjectiveResult result = host.TableAuthority.Submit<TrapFloorClaimKeyCommand, TrapFloorObjectiveResult>(
                new TrapFloorClaimKeyCommand(ClaimContext(), floorCardId));
            if (!result.Succeeded)
            {
                host.ShowMessage($"Claim Key rejected: {result.Error}.");
                return;
            }

            bool keepInspectOpen = host.inspectedCardId == floorCardId;
            if (!keepInspectOpen)
            {
                host.ContextMenus.CloseContextMenu();
            }

            RefreshTrapFloorFloorCardPresentation(floorCardId);
            RefreshTrapFloorStatusUi();
            if (keepInspectOpen)
            {
                host.CardInspectPanel.RefreshCardInspectPopup();
            }

            host.FeedbackLine( // KEYS-1: always shown
                $"{FormatPlayerName(result.Activity.ActorPlayerId)} claimed "
                + $"{result.FloorCard.Content.DisplayName}. "
                + $"KEYS {result.CollectedKeyCount} / {result.RequiredKeyCount}.",
                FeedbackSeverity.Info);
        }

        // KEYS-1 (audit B1): on a player turn a Key is claimed for the turn player, not for this screen's seat.
        private CommandContext ClaimContext() =>
            host.trapFloorTurnState != null
            && !host.trapFloorTurnState.IsCurrentFloorFailed
            && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                ? host.CreateCommandContext(host.trapFloorTurnState.ActivePlayerId)
                : host.CreateCommandContext();

        // MODE-1: winsFloor false records one player's escape only (Survival). True when the escape was accepted.
        internal bool AttemptTrapFloorEscape(TabletopObjectId floorCardId, PlayerId actor, bool winsFloor = true)
        {
            if (host.trapFloorObjectiveUseCase == null)
            {
                host.ShowMessage("Attempt Escape unavailable outside Trap Floor.");
                return false;
            }

            TrapFloorObjectiveResult result = host.TableAuthority.Submit<TrapFloorAttemptEscapeCommand, TrapFloorObjectiveResult>(
                new TrapFloorAttemptEscapeCommand(host.CreateCommandContext(actor), floorCardId, winsFloor));
            if (!result.Succeeded)
            {
                if (result.Error == TrapFloorObjectiveError.EscaperHasNoKey)
                {
                    host.ShowMessage("SECRET EXIT — claim a Key first, then land here to win the floor.");
                }
                else if (result.Error == TrapFloorObjectiveError.RequiredKeysMissing)
                {
                    host.ShowMessage(
                        $"Attempt Escape rejected: Need {result.RequiredKeyCount} Keys — "
                        + $"{result.CollectedKeyCount} claimed.");
                }
                else if (result.Error == TrapFloorObjectiveError.PlayerAlreadyEscaped)
                {
                    host.ShowMessage("Attempt Escape rejected: This Player has already escaped.");
                }
                else
                {
                    host.ShowMessage($"Attempt Escape rejected: {result.Error}.");
                }

                return false;
            }

            if (result.IsWon)
            {
                CloseFocusedSearchReveal(); // the exit was just turned over: the win is the news
                ShowTrapFloorVictory(result);
            }
            else
            {
                host.ContextMenus.CloseContextMenu();
            }
            RefreshTrapFloorStatusUi();
            string escapedPlayer = FormatPlayerName(result.EscapedActivity.ActorPlayerId);
            bool grand = TrapFloorObjectiveUseCase.IsGrandExit(result.FloorCard.Content);
            if (result.IsWon)
            {
                host.FeedbackLine( // always shown
                    grand
                        ? WinMode == TrapFloorWinMode.Regular
                            ? "The team escaped through the Grand Exit. FLOOR WON."
                            : $"{escapedPlayer} escaped through the Grand Exit. FLOOR WON."
                        : $"{escapedPlayer} escaped through the Secret Exit. FLOOR WON.",
                    FeedbackSeverity.Info);
            }

            return true;
        }

        internal void ShowTrapFloorVictory(TrapFloorObjectiveResult result)
        {
            host.ContextMenus.CloseContextMenu();
            ShowFloorEnded(
                true,
                (TrapFloorObjectiveUseCase.IsGrandExit(result.FloorCard.Content)
                    ? WinMode == TrapFloorWinMode.Regular
                        ? "Every player still in escaped through the Grand Exit"
                        : WinMode == TrapFloorWinMode.Survival
                            ? $"Every player still in escaped; {FormatPlayerName(result.EscapedActivity.ActorPlayerId)} was the last, through the Grand Exit"
                            : $"{FormatPlayerName(result.EscapedActivity.ActorPlayerId)} escaped through the Grand Exit"
                    : $"{FormatPlayerName(result.EscapedActivity.ActorPlayerId)} escaped through the Secret Exit")
                    + $" at {result.FloorCard.Coordinate}.");
        }

        internal void ShowTrapFloorReveal(TrapFloorRevealFloorResult result)
        {
            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            host.activeFloorRevealActivity = result.RevealedActivity;
            host.inspectedCardId = result.FloorCard.ObjectId;
            host.inspectedCardRenderedRevision = result.Revision;
            if (!host.CardInspectPanel.TryBuildCardInspectModel(host.inspectedCardId, out CardInspectModel model))
            {
                host.activeFloorRevealActivity = null;
                host.inspectedCardId = TabletopObjectId.Empty;
                host.inspectedCardRenderedRevision = -1;
                return;
            }

            host.runtimeUi.ShowCardInspect(model, host.CardInspectPanel.CloseCardInspect);
        }

        // G2a: the face-up Ability or Friend tile under the active Player's pawn, if it can still be taken.
        internal bool TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile)
        {
            prizeTile = null;
            if (host.trapFloorTemplate == null
                || host.matchState == null
                || host.trapFloorTurnState == null
                || host.trapFloorCollapseState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || !TryGetTrapFloorPlayerSetup(
                    host.trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition player)
                || !TryReadPawnTile(player.PawnId, out TrapFloorCoordinate tile)
                || !host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                || WasPrizeLeft(host.trapFloorTurnState.ActivePlayerId, floorCardId)
                || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || !floorCard.IsRevealed
                || host.trapFloorCollapseState.IsCollapsed(floorCardId)
                || !TrapFloorTakePrizeUseCase.IsPrize(floorCard.Content)
                || IsSurvivalEndgame()) // MODE-2: Survival's endgame turns prizes off
            {
                return false;
            }

            prizeTile = floorCard;
            return true;
        }

        // Owner, 2026-10-21: a prize the player left when their turn ended is not offered to them again while they
        // stay on it ("you are safe there and walk off next turn"); the next player to land there may take it.
        // Presentation-only memory: one entry per player, dropped once they stand anywhere else.
        private struct PrizeLeft
        {
            public TabletopObjectId FloorCardId;
            public int Round;
        }

        private readonly Dictionary<PlayerId, PrizeLeft> prizesLeft = new Dictionary<PlayerId, PrizeLeft>(4);
        private TrapFloorTurnState prizesLeftTurnState;

        private void RememberPrizeLeft(PlayerId player)
        {
            if (TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prize))
            {
                prizesLeft[player] = new PrizeLeft
                {
                    FloorCardId = prize.ObjectId,
                    Round = host.trapFloorTurnState.CurrentRound,
                };
            }
        }

        // True while the player still stands on the prize they left in an earlier round (an Undo back into that
        // round offers it again).
        private bool WasPrizeLeft(PlayerId player, TabletopObjectId floorCardId)
        {
            if (!ReferenceEquals(prizesLeftTurnState, host.trapFloorTurnState))
            {
                prizesLeft.Clear();
                prizesLeftTurnState = host.trapFloorTurnState;
            }

            if (!prizesLeft.TryGetValue(player, out PrizeLeft left)) return false;
            if (left.FloorCardId != floorCardId)
            {
                prizesLeft.Remove(player); // they walked off; landing there again later is a new visit
                return false;
            }

            return left.Round < host.trapFloorTurnState.CurrentRound;
        }

        internal static string TakePrizeLabel(TrapFloorFloorCardState prizeTile)
        {
            InputCostData cost = TrapFloorTakePrizeUseCase.CostOf(prizeTile.Content);
            return cost.IsEmpty
                ? $"Take {prizeTile.Content.DisplayName}"
                : $"Take {prizeTile.Content.DisplayName} ({InputCostNotation.Format(cost)})";
        }

        // Take Ability / Take Friend (doc 18 §15.4). A free prize is taken at once; a cost opens the Hand picker
        // and then the usual Console payment, confirmed from the turn menu.
        internal void BeginTakeFloorPrize()
        {
            host.ContextMenus.CloseContextMenu();
            if (host.pendingControllerPurchaseState != null && host.pendingControllerPurchaseState.IsActive)
            {
                host.ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            if (!TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile)
                || prizeTile.Content.AuthoredCard == null)
            {
                host.ShowMessage("Stand on a face-up Ability or Friend to take it.");
                return;
            }

            InputCostData cost = TrapFloorTakePrizeUseCase.CostOf(prizeTile.Content);
            if (cost.IsEmpty)
            {
                CompleteTakeFloorPrize(prizeTile.ObjectId, Array.Empty<TabletopObjectId>());
                RefreshTrapFloorStatusUi();
                return;
            }

            string cardDefinitionStableId = prizeTile.Content.AuthoredCard.StableId;
            if (!TryBuildPurchasePaymentOptions(
                    cardDefinitionStableId,
                    out CardDefinitionData prizeDefinition,
                    out List<PurchasePaymentCardOption> options,
                    out string missing))
            {
                host.ShowMessage($"{prizeTile.Content.DisplayName} cannot be taken: its cost could not be read.");
                return;
            }

            host.pendingPurchaseDefinitionStableId = cardDefinitionStableId;
            pendingPurchaseSource = PendingPurchaseSource.Table;
            List<FocusedCardOptionModel> cards =
                new List<FocusedCardOptionModel>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                PurchasePaymentCardOption option = options[i];
                cards.Add(new FocusedCardOptionModel(
                    option.CardId,
                    option.DisplayName,
                    option.Input.HasValue ? option.Input.Value.ToString() : "Not eligible",
                    option.Artwork,
                    option.Eligible));
            }

            string costText = InputCostNotation.Format(prizeDefinition.InputCost);
            host.runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
                $"TAKE {prizeDefinition.DisplayName.ToUpperInvariant()} — HAND",
                $"Choose {costText} from your Hand.",
                string.IsNullOrEmpty(missing)
                    ? "Pick the cards that pay the cost."
                    : $"Missing: {missing}",
                "Confirm Cards",
                cards,
                prizeDefinition.InputCost.CardCount,
                IsPurchasePaymentReady,
                ConfirmFocusedPurchasePayment,
                CancelFocusedPurchaseSelection));
        }

        internal void CompleteTakeFloorPrize(
            TabletopObjectId floorCardId,
            IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            if (host.trapFloorTakePrizeUseCase == null
                || host.trapFloorTurnState == null
                || !TryGetTrapFloorPlayerSetup(
                    host.trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition taker)
                || !host.matchState.Pawns.TryGetValue(taker.PawnId, out PawnState takerPawn))
            {
                host.ShowMessage("Taking prizes is unavailable.");
                return;
            }

            TrapFloorTakePrizeResult result = host.TableAuthority.Submit<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(
                new TrapFloorTakePrizeCommand(
                    host.CreateCommandContext(host.trapFloorTurnState.ActivePlayerId),
                    floorCardId,
                    TabletopObjectId.New(),
                    paymentCardIds,
                    PawnTablePosition(takerPawn)));
            if (!result.Succeeded)
            {
                host.ShowMessage($"Take rejected: {result.Error}.");
                return;
            }

            if (host.pendingControllerPurchaseState != null && host.pendingControllerPurchaseState.IsActive)
            {
                host.pendingControllerPurchaseState.Clear();
            }

            host.pendingPurchaseDefinitionStableId = string.Empty;
            ReplaceCurrentUndoStateForPurchaseAssistance();

            string prizeName = result.FloorCard.Content.DisplayName;
            ShowPaymentReturned();
            // PRIZE-2 (owner, 2026-10-22): the tile stays under the pawn, face up, until the taker moves off it; then it
            // comes to their hand (CollectTakenPrizesOffPawns).
            if (host.waitingLandingFloorCardId == floorCardId)
            {
                host.waitingLandingFloorCardId = TabletopObjectId.Empty;
                host.waitingLandingActor = PlayerId.Empty;
            }

            if (host.Registry.TryGetCardView(floorCardId, out CardView takenView) && takenView.HoldsFaceDown)
            {
                RevealFloorCardView(takenView);
            }

            host.RefreshCardContentVisibility();
            host.FeedbackLine( // always shown
                $"{FormatPlayerName(host.trapFloorTurnState.ActivePlayerId)} took {prizeName}. "
                    + "It stays under the pawn and comes to the hand when the pawn moves off this tile.",
                FeedbackSeverity.Info);
        }

        // TABLE-1: the Controller Cards that paid went back into the Controller deck: lay out the deck and the
        // active player's Console slots again.
        internal void ShowPaymentReturned()
        {
            if (!TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                return;
            }

            host.ViewSync.SyncContainer(player.ControllerDeckId);
            for (int i = 0; i < player.SideSlotContainerIds.Count; i++)
            {
                host.ViewSync.SyncContainer(player.SideSlotContainerIds[i]);
            }

            host.RefreshAcceptedCardFaces();
            host.RefreshCardContentVisibility();
        }

        // R2: a card the Shop or a prize hands out gets its view, named after the card, in the pile or hand it went to.
        internal void PresentGrantedCard(TabletopObjectId cardId, string name, ContainerId destinationContainerId)
        {
            if (host.Registry.TryGetCardView(cardId, out _)
                || !host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card))
            {
                return;
            }

            host.labelsByCardId[cardId] = name;
            CardView view = host.Spawner.CreateCardView(card, name, out TabletopSelectionVisual selectionVisual);
            host.Registry.cardViews.Add(view);
            host.Registry.cardSelectionVisuals.Add(selectionVisual);
            host.physicalAuthority?.Register(view);
            host.RefreshContainerCardViewSources();
            host.ViewSync.SyncContainer(destinationContainerId);
            host.RefreshSelectionPresenterAfterRuntimeProjection();
        }

        // Owner, 2026-10-21 (team game): exits are taken by landing. The Secret Exit wins the floor for the turn
        // player whose pawn lands on it holding a Key they claimed. The Grand Exit wins it once every player still in
        // stands on it and the team has the mode's Keys. Event-time only (after a pawn move).
        private void TryTakeExitsAfterMove(TabletopObjectId activeLandedFloorCardId)
        {
            if (host.trapFloorObjectiveState == null
                || host.trapFloorObjectiveUseCase == null
                || host.trapFloorTurnState == null
                || host.trapFloorObjectiveState.IsWon
                || host.trapFloorTurnState.IsCurrentFloorFailed)
            {
                return;
            }

            PlayerId active = host.trapFloorTurnState.ActivePlayerId;
            if (WinMode != TrapFloorWinMode.Regular)
            {
                TryTakeExitsForMode(activeLandedFloorCardId, active); // MODE-1
                return;
            }

            if (!activeLandedFloorCardId.IsEmpty
                && !active.IsEmpty
                && IsOpenExit(activeLandedFloorCardId, out bool landedOnGrand)
                && !landedOnGrand)
            {
                if (host.trapFloorObjectiveUseCase.HasClaimedKey(active)) AttemptTrapFloorEscape(activeLandedFloorCardId, active);
                else host.FeedbackLine("SECRET EXIT — you need a Key you claimed: land on a Key, then come back here to win the floor.", FeedbackSeverity.Warning); // KEYS-1
                return;
            }

            if (!TryGetGrandExitWithEveryoneOn(out TabletopObjectId grandExitId, out PlayerId anyoneOn))
            {
                ExplainGrandExitWait(activeLandedFloorCardId); // EXIT-2
                return;
            }

            if (!host.trapFloorObjectiveState.HasRequiredKeys)
            {
                host.FeedbackLine( // KEYS-1: always shown
                    $"GRAND EXIT — everyone still in is here, but the team needs {host.trapFloorObjectiveState.RequiredKeyCount} "
                    + $"Key(s): {host.trapFloorObjectiveState.CollectedKeyCount} claimed. Land on a Key to claim it.",
                    FeedbackSeverity.Warning);
                return;
            }

            AttemptTrapFloorEscape(grandExitId, active.IsEmpty ? anyoneOn : active);
        }

        // EXIT-2 (owner, 2026-10-22: "both landed on grand exit but no popup for win"; the Grand Exit needs every player
        // still in on it): landing on the Grand Exit before that says who is still missing and how many Keys the team
        // has, so a wait is never silent. Event-time only.
        private void ExplainGrandExitWait(TabletopObjectId landedFloorCardId)
        {
            if (landedFloorCardId.IsEmpty || !IsOpenExit(landedFloorCardId, out bool isGrand) || !isGrand) return;
            StringBuilder missing = new StringBuilder();
            int onIt = 0;
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                if (!host.matchState.Seats.TryGetValue(setup.SeatId, out var seat)
                    || seat.OccupantPlayerId.IsEmpty
                    || !IsTurnParticipant(seat.OccupantPlayerId)
                    || host.trapFloorTurnState.GetPlayerState(seat.OccupantPlayerId)
                        == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    continue;
                }

                if (TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)
                    && host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                    && floorCardId == landedFloorCardId)
                {
                    onIt++;
                    continue;
                }

                if (missing.Length > 0) missing.Append(", ");
                missing.Append(FormatPlayerName(seat.OccupantPlayerId));
            }

            string keys = $"Keys {host.trapFloorObjectiveState.CollectedKeyCount} / {host.trapFloorObjectiveState.RequiredKeyCount}";
            host.FeedbackLine(
                missing.Length > 0
                    ? $"GRAND EXIT — {onIt} here. Every player still in must stand here to win the floor: waiting for {missing}. {keys}."
                    : $"GRAND EXIT — everyone still in is here. {keys}.",
                FeedbackSeverity.Warning);
        }

        // A revealed exit tile still in play (not collapsed).
        private bool IsOpenExit(TabletopObjectId floorCardId, out bool isGrandExit)
        {
            isGrandExit = false;
            if (!host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || !floorCard.IsRevealed
                || floorCard.Content.Category != TrapFloorFloorContentCategory.SecretExit
                || (host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(floorCardId)))
            {
                return false;
            }

            isGrandExit = TrapFloorObjectiveUseCase.IsGrandExit(floorCard.Content);
            return true;
        }

        // True when every seated player who is not knocked out has their pawn on the same revealed Grand Exit.
        private bool TryGetGrandExitWithEveryoneOn(out TabletopObjectId grandExitId, out PlayerId anyoneOn)
        {
            grandExitId = TabletopObjectId.Empty;
            anyoneOn = PlayerId.Empty;
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition setup = host.trapFloorTemplate.Players[i];
                if (!host.matchState.Seats.TryGetValue(setup.SeatId, out var seat)
                    || seat.OccupantPlayerId.IsEmpty
                    || !IsTurnParticipant(seat.OccupantPlayerId)
                    || host.trapFloorTurnState.GetPlayerState(seat.OccupantPlayerId)
                        == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    continue;
                }

                if (!TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)
                    || !host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                    || (!grandExitId.IsEmpty && floorCardId != grandExitId))
                {
                    return false;
                }

                grandExitId = floorCardId;
                anyoneOn = seat.OccupantPlayerId;
            }

            return !grandExitId.IsEmpty && IsOpenExit(grandExitId, out bool isGrand) && isGrand;
        }

        private bool IsTurnParticipant(PlayerId playerId)
        {
            IReadOnlyList<PlayerId> order = host.trapFloorTurnState.PlayerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == playerId) return true;
            }

            return false;
        }

        // G2a: remembers the tile under every pawn, so only a real move onto a new tile flips it.
        internal void SyncLandingPawnTiles()
        {
            landingPawnTiles.Clear();
            host.landingCheckPending = false;
            if (host.trapFloorTemplate == null || host.matchState == null) return;
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TabletopObjectId pawnId = host.trapFloorTemplate.Players[i].PawnId;
                landingPawnTiles[pawnId] = TryReadPawnTile(pawnId, out TrapFloorCoordinate tile)
                    ? tile
                    : (TrapFloorCoordinate?)null;
            }

            // After Undo, Redo or a seat switch: a landed tile already triggered but still under a pawn keeps
            // waiting to turn over; its new view is held face down again. A tile Undo turned back is no longer waiting.
            TabletopObjectId waitingId = host.waitingLandingFloorCardId;
            host.waitingLandingFloorCardId = TabletopObjectId.Empty;
            host.waitingLandingActor = PlayerId.Empty;
            if (!waitingId.IsEmpty
                && host.trapFloorTurnState != null
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.landedFloorCards.Contains(waitingId)
                && host.trapFloorTemplate.TryGetFloorCardState(host.matchState, waitingId, out TrapFloorFloorCardState waitingCard)
                && waitingCard.IsRevealed
                && (host.trapFloorCollapseState == null || !host.trapFloorCollapseState.IsCollapsed(waitingId))
                && IsAnyPawnOnFloorCard(waitingId))
            {
                host.waitingLandingFloorCardId = waitingId;
                host.waitingLandingActor = host.trapFloorTurnState.ActivePlayerId;
                if (host.Registry.TryGetCardView(waitingId, out CardView waitingView)) waitingView.SetHoldFaceDown(true);
            }
        }

        // A landed tile still under a pawn shows face down on its (re)bound view until it turns over.
        internal void HoldWaitingLandingTileView()
        {
            if (!host.waitingLandingFloorCardId.IsEmpty && host.Registry.TryGetCardView(host.waitingLandingFloorCardId, out CardView view))
            {
                view.SetHoldFaceDown(true);
            }
        }

        internal void RevealFloorCardView(CardView floorCardView)
        {
            floorCardView.SetHoldFaceDown(false);
            floorCardView.ApplyAcceptedState();
            host.BeginCardFlipTurn(floorCardView); // the same turn-over as any card flip
        }

        internal bool IsAnyPawnOnFloorCard(TabletopObjectId floorCardId)
        {
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                if (TryReadPawnTile(host.trapFloorTemplate.Players[i].PawnId, out TrapFloorCoordinate tile)
                    && host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId id)
                    && id == floorCardId)
                {
                    return true;
                }
            }

            return false;
        }

        // Physical part of a landing (owner, 2026-10-15): the tile, already triggered when the pawn landed, turns
        // over on the table once no pawn is on it. At the end of a turn a pawn still on it hops up while it turns
        // (the automatic flip).
        internal void RevealWaitingLandingTile(bool turnEnds)
        {
            if (host.waitingLandingFloorCardId.IsEmpty || host.trapFloorTemplate == null || host.matchState == null)
            {
                return;
            }

            TabletopObjectId floorCardId = host.waitingLandingFloorCardId;
            if (!turnEnds && IsAnyPawnOnFloorCard(floorCardId)) return;
            host.waitingLandingFloorCardId = TabletopObjectId.Empty;
            host.waitingLandingActor = PlayerId.Empty;
            if (!host.Registry.TryGetCardView(floorCardId, out CardView floorCardView) || !floorCardView.HoldsFaceDown) return;
            if (turnEnds) HopPawnsOffFloorCard(floorCardId);
            RevealFloorCardView(floorCardView);
        }

        // Trigger part of a landing: the reveal runs at once for the player who landed (the "YOU LANDED ON" popup,
        // effects and messages), while the tile on the table stays face down until the pawn is off it.
        internal void TriggerLandedTile(TabletopObjectId floorCardId, PlayerId actor)
        {
            if (host.trapFloorRevealFloorUseCase == null) return;
            TrapFloorRevealFloorResult result = host.TableAuthority.Submit<TrapFloorRevealFloorCommand, TrapFloorRevealFloorResult>(
                new TrapFloorRevealFloorCommand(
                    host.CreateCommandContext(actor),
                    floorCardId,
                    TrapFloorSearchKind.Landing,
                    Array.Empty<TabletopObjectId>()));
            if (!result.Succeeded)
            {
                host.ShowMessage($"Landing flip rejected: {result.Error}.");
                return;
            }

            keyRevealedThisLanding = floorCardId; // KEYS-2 / PRIZE-3: this landing's popup comes from the reveal
            bool waits = IsAnyPawnOnFloorCard(floorCardId);
            if (host.Registry.TryGetCardView(floorCardId, out CardView floorCardView))
            {
                if (waits) floorCardView.SetHoldFaceDown(true); // the card is revealed; the tile turns later
                else RevealFloorCardView(floorCardView);
            }

            if (waits)
            {
                host.waitingLandingFloorCardId = floorCardId;
                host.waitingLandingActor = actor;
            }

            host.RefreshCardContentVisibility();
            host.selectionPresenter?.Refresh();
            host.activeFloorRevealActivity = result.RevealedActivity;
            ShowFocusedSearchReveal(result);
            RefreshTrapFloorStatusUi();
            host.ShowMessage(
                $"{FormatPlayerShortName(result.RevealedActivity.ActorPlayerId)} landed on "
                + $"{result.FloorCard.Content.DisplayName}."
                + (waits ? " The tile turns over when the pawn moves off it, or when the turn ends." : string.Empty));
        }

        // The pawn on a tile turning at the end of a turn lifts and lands again in place (Presentation only).
        internal void HopPawnsOffFloorCard(TabletopObjectId floorCardId)
        {
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TabletopObjectId pawnId = host.trapFloorTemplate.Players[i].PawnId;
                if (!TryReadPawnTile(pawnId, out TrapFloorCoordinate tile)
                    || !host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId id)
                    || id != floorCardId)
                {
                    continue;
                }

                for (int v = 0; v < host.Registry.pawnViews.Count; v++)
                {
                    PawnView pawnView = host.Registry.pawnViews[v];
                    if (pawnView == null || !pawnView.IsBound || pawnView.ObjectId != pawnId) continue;
                    Transform hopVisual = pawnView.transform.Find("VisualRoot");
                    StartPawnHop(
                        pawnView,
                        hopVisual,
                        hopVisual != null ? hopVisual.position : pawnView.transform.position,
                        host.presentationTransitions.Capture(pawnView.transform));
                    break;
                }
            }
        }

        internal bool IsHiddenUsableTile(TrapFloorCoordinate tile)
        {
            return host.trapFloorTemplate.TryGetFloorCardId(tile, out TabletopObjectId floorCardId)
                && host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                && !floorCard.IsRevealed
                && (host.trapFloorCollapseState == null || !host.trapFloorCollapseState.IsCollapsed(floorCardId));
        }

        internal bool TryReadPawnTile(TabletopObjectId pawnId, out TrapFloorCoordinate tile)
        {
            tile = default;
            return host.matchState != null
                && host.trapFloorTemplate != null
                && host.matchState.Pawns.TryGetValue(pawnId, out PawnState pawn)
                && host.trapFloorTemplate.TryGetFloorCoordinateAt(host.matchState, PawnTablePosition(pawn), out tile);
        }

        // Where a pawn stands on the table. A dragged pawn keeps its position in its physics state (world space);
        // its pose only holds the position it was placed at, so the physics state wins when there is one.
        internal TableCoordinate PawnTablePosition(PawnState pawn)
        {
            PhysicalObjectState physical = pawn.BaseState.PhysicalState;
            if (physical != null && host.coordinateConverter != null)
            {
                return host.coordinateConverter.ToTableCoordinate(
                    new Vector3(physical.Position.X, physical.Position.Y, physical.Position.Z));
            }

            return pawn.BaseState.Pose.Position;
        }

        // Flip on landing (doc 18 §15.2): when the active Player's pawn has moved onto a new face-down tile, that
        // tile flips for free through the reveal use case. The flip amends the move's Undo step.
        internal void CompletePendingLandingFlip()
        {
            if (!host.landingCheckPending || host.undoTransactionInProgress) return;
            host.landingCheckPending = false;
            activePawnLeftGrid = false;
            RunLandingCheck(out TabletopObjectId activeLandedFloorCardId, out bool anyPawnMoved);
            if (activePawnLeftGrid) KnockOutIfOffTheFloor(); // FIX-3
            if (!anyPawnMoved) return;
            if (host.trapFloorTurnState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn
                && !floorTurnCollapseDone
                && Time.frameCount - lastTurnAdvanceFrame > 5) // a Trap's own move at End Turn is not a turn taken
            {
                // ROUND-1 (owner, 2026-10-22: "if someone tries to take a turn, notify all players and ask that player to
                // undo; no forcing"): nothing is blocked.
                host.FeedbackLine(
                    "The round is over: roll the two floor dice to collapse a tile first. Press Undo to take that move back.",
                    FeedbackSeverity.Warning);
            }

            CollectTakenPrizesOffPawns(); // PRIZE-2
            OfferRevealedKeyOnLanding(activeLandedFloorCardId); // KEYS-2
            TryTakeExitsAfterMove(activeLandedFloorCardId);
        }

        private void RunLandingCheck(out TabletopObjectId activeLandedFloorCardId, out bool anyPawnMoved)
        {
            activeLandedFloorCardId = TabletopObjectId.Empty;
            anyPawnMoved = false;
            if (host.trapFloorTemplate == null
                || host.matchState == null
                || host.trapFloorTurnState == null
                || host.trapFloorRevealFloorUseCase == null
                || host.trapFloorCollapseState == null)
            {
                return;
            }

            TabletopObjectId activePawnId = TabletopObjectId.Empty;
            if (!host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                activePawnId = player.PawnId;
            }

            TrapFloorCoordinate? landedTile = null;
            TrapFloorCoordinate? landedFrom = null;
            bool otherPawnOnHiddenTile = false;
            bool otherPawnMoved = false;
            for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
            {
                TabletopObjectId pawnId = host.trapFloorTemplate.Players[i].PawnId;
                TrapFloorCoordinate? tile = TryReadPawnTile(pawnId, out TrapFloorCoordinate readTile)
                    ? readTile
                    : (TrapFloorCoordinate?)null;
                bool known = landingPawnTiles.TryGetValue(pawnId, out TrapFloorCoordinate? previous);
                if (pawnId == activePawnId && known && previous.HasValue && !tile.HasValue) activePawnLeftGrid = true; // FIX-3
                if (known && tile.HasValue && !Nullable.Equals(previous, tile))
                {
                    if (pawnId == activePawnId)
                    {
                        landedTile = tile;
                        landedFrom = previous;
                    }
                    else
                    {
                        otherPawnMoved = true;
                        if (!activePawnId.IsEmpty && IsHiddenUsableTile(tile.Value)) otherPawnOnHiddenTile = true;
                    }
                }

                landingPawnTiles[pawnId] = tile;
            }

            anyPawnMoved = landedTile.HasValue || otherPawnMoved;

            // Owner, 2026-10-15 (H3): a landing the rules do not allow is a rule check, hints on or off. It does
            // not flip its tile; a warning says why and points at the Undo button; the turn menu offers Keep move.
            string moveProblem = null;
            if (landedTile.HasValue)
            {
                moveProblem = CheckGuidedMove(landedTile.Value) ?? CheckActivePawnMove(landedFrom, landedTile.Value);
                if (moveProblem != null)
                {
                    host.FeedbackLine(
                        $"Move not allowed: {moveProblem} The tile does not flip. Press the Undo button to take the "
                        + "move back, or choose Keep move in the turn menu if the table agrees.",
                        FeedbackSeverity.Warning);
                    RefreshTrapFloorStatusUi();
                }

                host.RulesCardPanel.RaiseHint(HintTriggers.PawnMoved, "own");
                if (moveProblem == null
                    && host.trapFloorTemplate.TryGetFloorCardId(landedTile.Value, out TabletopObjectId landedId))
                {
                    activeLandedFloorCardId = landedId; // an allowed landing can take an exit
                }
            }
            else if (otherPawnMoved)
            {
                host.RulesCardPanel.RaiseHint(HintTriggers.PawnMoved, "other");
            }

            // The tile waiting from an earlier landing turns once no pawn stands on it.
            if (!host.waitingLandingFloorCardId.IsEmpty)
            {
                RevealWaitingLandingTile(false);
            }

            if (!landedTile.HasValue && otherPawnOnHiddenTile)
            {
                // UX-1 hint: moving another player's pawn never flips; say whose pawn does.
                string colour = host.TryGetSeatIndex(host.trapFloorTurnState.ActivePlayerId, out int seatIndex)
                    ? $" ({PlayerColorName(seatIndex).ToLowerInvariant()})"
                    : string.Empty;
                host.ShowMessage(
                    $"It's {FormatPlayerName(host.trapFloorTurnState.ActivePlayerId)}'s turn{colour}. "
                    + "Only their pawn flips tiles.");
                return;
            }

            if (!landedTile.HasValue
                || !host.trapFloorTemplate.TryGetFloorCardId(landedTile.Value, out TabletopObjectId floorCardId)
                || !host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                || floorCard.IsRevealed
                || host.trapFloorCollapseState.IsCollapsed(floorCardId))
            {
                return;
            }

            if (moveProblem != null)
            {
                notAllowedLandingFloorCardId = floorCardId; // flips only if the table keeps the move
                RefreshTrapFloorStatusUi();
                return;
            }

            // The landing triggers now (popup and effects); the tile on the table turns once the pawn is off it
            // (aside or ahead), or when the turn ends.
            host.landedFloorCards.Add(floorCardId);
            TriggerLandedTile(floorCardId, host.trapFloorTurnState.ActivePlayerId);
        }

        // FIX-3 (owner, 2026-10-22: on a smaller floor "the row and column removed… stepping there = fall"): the turn
        // player's pawn put down where the floor was removed (up to as many cells past the edge as the floors have shrunk)
        // falls and is out for this floor. Further away (beside the board, at a Console) nothing happens. Event-time only.
        private bool activePawnLeftGrid;

        private void KnockOutIfOffTheFloor()
        {
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            if (template == null
                || template.FloorIndex <= 0
                || host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || host.trapFloorTurnService == null)
            {
                return;
            }

            PlayerId active = host.trapFloorTurnState.ActivePlayerId;
            if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(active)) return;
            if (!TryGetTrapFloorPlayerSetup(active, out TrapFloorPlayerSetupDefinition player)
                || !host.matchState.Pawns.TryGetValue(player.PawnId, out PawnState pawn)
                || !template.TryGetCellPosition(host.matchState, new TrapFloorCoordinate(1, 1), out TableCoordinate firstCell))
            {
                return;
            }

            TableCoordinate position = PawnTablePosition(pawn);
            int x = 1 + (int)Math.Round((position.X - firstCell.X) / template.Grid.ColumnPitch);
            int y = 1 + (int)Math.Round((position.Y - firstCell.Y) / template.Grid.RowPitch);
            int removed = template.FloorIndex; // each floor is one row and one column smaller
            bool onRemovedEdge = x >= 1 - removed && y >= 1 - removed
                && x <= template.GridColumns + removed && y <= template.GridRows + removed
                && (x < 1 || y < 1 || x > template.GridColumns || y > template.GridRows);
            if (!onRemovedEdge) return;

            host.SessionService.BeginUndoBatch();
            try
            {
                host.trapFloorTurnService.MarkPlayerEliminatedForCurrentRound(host.matchState, host.CreateCommandContext(active), active);
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            RefreshTrapFloorStatusUi();
            host.FeedbackLine(
                $"{FormatPlayerName(active).ToUpperInvariant()} stepped off the floor where it was removed and fell: "
                    + "ELIMINATED for this floor. Press Undo to take the move back.",
                FeedbackSeverity.Loss);
            if (host.trapFloorTurnState.IsCurrentFloorFailed) ShowFloorEnded(FloorFailedCountsAsWin());
            else TryTakeExitsAfterMove(TabletopObjectId.Empty);
        }

        internal bool IsAssistedTrapFloorConsoleSlot(ContainerId slotContainerId)
        {
            if (host.trapFloorTemplate == null || slotContainerId.IsEmpty)
            {
                return false;
            }

            if (host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                return false;
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (player.MainSlotContainerId == slotContainerId)
            {
                return true;
            }

            for (int i = 0; i < player.SideSlotContainerIds.Count; i++)
            {
                if (player.SideSlotContainerIds[i] == slotContainerId)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool IsAssistedControllerDeck(ContainerId containerId)
        {
            if (host.trapFloorTemplate == null || containerId.IsEmpty)
            {
                return false;
            }

            if (host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                return false;
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            return player.ControllerDeckId == containerId
                && host.matchState.Containers.TryGetValue(containerId, out ContainerState container)
                && container.Kind == ContainerKind.Deck
                && (container.OwnerSeatId.IsEmpty || container.OwnerSeatId == player.SeatId) // TABLE-1: shared deck
                && host.matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                && seat.OccupantPlayerId == host.trapFloorTurnState.ActivePlayerId;
        }

        // The game's cards as played on this computer (doc 27 L2-1): the active pack's and your own card changes and
        // new cards applied to the built-in cards. Problems are skipped with a console warning, never stopping play.
        internal GameContent BuildTrapFloorContent(bool useChosenDeck)
        {
            host.EnsureContentArt();
            GameContent content = new GameContent(host.trapFloorGameDefinition, host.contentArt.LoadPlayerCards(), host.contentArt.LoadPlayerDecks(),
                useChosenDeck ? host.GameSelectPanel.ChosenDeckIdOf(Game) : null);
            for (int i = 0; i < content.Warnings.Count; i++)
            {
                Debug.LogWarning($"[Your cards] {content.Warnings[i]}", host);
            }

            if (content.ChangedCount > 0 || content.NewCards.Count > 0)
            {
                Debug.Log(
                    $"[Your cards] {host.trapFloorGameDefinition.DisplayName}: {content.ChangedCount} changed, {content.NewCards.Count} new"
                    + (content.PlayDeckName.Length > 0 ? $", playing with your deck '{content.PlayDeckName}'." : "."),
                    host);
            }

            return content;
        }

        internal void HandleTrapFloorConsoleCardInteractionAccepted(
            IConsoleCardInteraction interaction,
            ConsoleCardBehavior behavior)
        {
            HandlePendingPurchaseConsoleInteraction(interaction);

            if (!(interaction is ConsoleCardInserted insertion)
                || behavior != ConsoleCardBehavior.ActivateOnInsert
                || host.trapFloorAbilityResolutionService == null
                || !host.TryGetAuthoredCardDefinition(
                    interaction.CardDefinitionId,
                    out CardDefinition definition))
            {
                return;
            }

            TrapFloorAbilityEffect effect =
                TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
            if (!IsAssistedAbility(effect))
            {
                return;
            }

            // Owner, 2026-10-20: putting an Ability into the Console no longer uses it behind the scenes. The player
            // is asked (what it would do now, its cost, Use / Not now); a card the player already chose to use (from a
            // prompt, after paying) is used at once when it lands.
            if (insertion.CardInstanceId == abilityUseOnInsertCardId)
            {
                abilityUseOnInsertCardId = TabletopObjectId.Empty;
                ActivateAbility(insertion, definition, effect);
                return;
            }

            OfferAbilityOnInsert(insertion, definition);
        }

        // Uses an Ability for real (the player chose it, and paid when it has a cost): the trap it answers is marked,
        // Dodge and Rush light their tiles, and the result is said.
        private void ActivateAbility(ConsoleCardInserted insertion, CardDefinition definition, TrapFloorAbilityEffect effect)
        {
            IConsoleCardInteraction interaction = insertion;
            TrapFloorAbilityActivationResult result = host.trapFloorAbilityResolutionService.Activate(
                host.matchState,
                insertion,
                definition.ToData());
            if (!result.Succeeded)
            {
                host.ShowMessage(TrapFloorAbilityRejectionMessage(effect, result.Error));
                return;
            }

            if (host.undoTrackedMatch == host.matchState && host.undoHistory.CurrentStateIndex >= 0)
            {
                // The accepted transfer already owns this transaction. Replace its After snapshot
                // with the Trap Floor interpretation instead of creating a second history entry.
                host.undoHistory.ReplaceCurrentState(host.SessionService.CaptureUndoSnapshot());
                host.SessionService.RefreshUndoUi();
            }

            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            QueueUsedAbilityDiscard(insertion.CardInstanceId); // TABLE-1: the used card goes on the Ability discard pile
            RefreshTrapFloorStatusUi();
            if (effect == TrapFloorAbilityEffect.Dodge)
            {
                RefreshMovementAssistancePresentation();
                host.FeedbackLine("DODGE — move to a lit tile next to you.", FeedbackSeverity.Info); // TRAP-1: always shown
            }
            else if (effect == TrapFloorAbilityEffect.Rush)
            {
                RefreshMovementAssistancePresentation();
                host.FeedbackLine("RUSH — move up to 3 tiles, to a lit tile.", FeedbackSeverity.Info); // TRAP-1: always shown
            }
            else if (effect == TrapFloorAbilityEffect.Check)
            {
                RefreshNeutralizedTrapPresentation();
                host.ShowMessage(
                    $"{FormatPlayerShortName(interaction.ActorPlayerId)} checked "
                    + $"{result.Trap.Content.DisplayName} — Trap neutralized");
            }
            else
            {
                string action = effect == TrapFloorAbilityEffect.Disarm
                    ? "used Disarm on"
                    : "used Shield against";
                host.ShowMessage(
                    $"{FormatPlayerShortName(interaction.ActorPlayerId)} {action} "
                    + result.Trap.Content.DisplayName);
            }
        }

        internal void HandlePendingPurchaseConsoleInteraction(
            IConsoleCardInteraction interaction)
        {
            if (interaction == null
                || host.pendingControllerPurchaseState == null
                || !host.pendingControllerPurchaseState.IsActive
                || !TryGetTrapFloorPlayerSetup(
                    host.pendingControllerPurchaseState.PlayerId,
                    out TrapFloorPlayerSetupDefinition player))
            {
                return;
            }

            // BUY-1 (owner, 2026-10-21: "I am unable to buy an ability"): on one screen whoever moves the cards does
            // it for the payer (the screen seat need not be the turn player's), and any card giving the same input as
            // a chosen one pays too. The cards must still go into the payer's own Console.
            PlayerId payer = host.pendingControllerPurchaseState.PlayerId;
            bool inserted = interaction is ConsoleCardInserted;
            if (inserted && interaction.ConsoleOwnerPlayerId == payer && IsPayerItemSlot(player, interaction.SlotContainerId))
            {
                TryUseMatchingPaymentCard(interaction.CardInstanceId);
            }

            bool changed = inserted
                ? host.pendingControllerPurchaseState.RecordConsoleInsertion(
                    payer,
                    interaction.ConsoleOwnerPlayerId,
                    interaction.SlotContainerId,
                    interaction.CardInstanceId,
                    player.SideSlotContainerIds)
                : interaction is ConsoleCardRemoved
                    && host.pendingControllerPurchaseState.RecordConsoleRemoval(
                        interaction.ActorPlayerId,
                        interaction.ConsoleOwnerPlayerId,
                        interaction.SlotContainerId,
                        interaction.CardInstanceId,
                        player.SideSlotContainerIds);
            if (!changed)
            {
                // A card put into the payer's Console that cannot pay this cost: say so (a warning, always shown).
                if (inserted
                    && interaction.ConsoleOwnerPlayerId == payer
                    && IsPayerItemSlot(player, interaction.SlotContainerId)
                    && !host.pendingControllerPurchaseState.IsInserted(interaction.CardInstanceId))
                {
                    host.ShowMessage(
                        $"That card cannot pay for {FindPurchaseDisplayName(host.pendingControllerPurchaseState.PurchasedCardDefinitionStableId)}: "
                        + "put in the highlighted cards from the Hand (or cards with the same buttons).");
                }

                return;
            }

            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            string abilityName = FindPurchaseDisplayName(
                host.pendingControllerPurchaseState.PurchasedCardDefinitionStableId).ToUpperInvariant();
            host.ShowMessage(host.pendingControllerPurchaseState.IsPaymentComplete
                ? "PAYMENT READY — Confirm Purchase"
                : $"BUY {abilityName} — Place the selected Cards in your Console");
        }

        // BUY-1: a slot of the payer's Console that payment cards go into (every slot but Main).
        private static bool IsPayerItemSlot(TrapFloorPlayerSetupDefinition player, ContainerId slotContainerId)
        {
            for (int i = 0; i < player.SideSlotContainerIds.Count; i++)
            {
                if (player.SideSlotContainerIds[i] == slotContainerId) return true;
            }

            return false;
        }

        // BUY-1: a card put into the payer's Console that is not one of the chosen cards takes the place of a chosen
        // card giving the same input that is not in the Console yet. Event-time only.
        private void TryUseMatchingPaymentCard(TabletopObjectId cardId)
        {
            PendingControllerPurchaseState purchase = host.pendingControllerPurchaseState;
            IReadOnlyList<TabletopObjectId> chosen = purchase.SelectedCardIds;
            if (cardId.IsEmpty || purchase.IsInserted(cardId)) return;
            for (int i = 0; i < chosen.Count; i++)
            {
                if (chosen[i] == cardId) return; // already one of the chosen cards
            }

            if (host.trapFloorTemplate == null
                || !ControllerInputCardCatalog.TryCreate(host.trapFloorTemplate.GameDefinition, out ControllerInputCardCatalog catalog)
                || !TryGetControllerInput(catalog, cardId, out ControllerInput input))
            {
                return;
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                if (purchase.IsInserted(chosen[i])
                    || !TryGetControllerInput(catalog, chosen[i], out ControllerInput chosenInput)
                    || chosenInput != input)
                {
                    continue;
                }

                purchase.ReplaceSelected(chosen[i], cardId);
                return;
            }
        }

        private bool TryGetControllerInput(ControllerInputCardCatalog catalog, TabletopObjectId cardId, out ControllerInput input)
        {
            input = default;
            return host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                && catalog.TryGetInput(card.BaseState.DefinitionId, out input);
        }

        internal static string TrapFloorAbilityRejectionMessage(
            TrapFloorAbilityEffect effect,
            TrapFloorAbilityActivationError error)
        {
            switch (error)
            {
                case TrapFloorAbilityActivationError.NoUnresolvedTrap:
                    return effect == TrapFloorAbilityEffect.Check
                        ? "Check unavailable — no unresolved Trap."
                        : $"{effect} has no revealed unresolved Trap for the active Player.";
                case TrapFloorAbilityActivationError.NoPendingTrapConsequence:
                    return "Shield has no pending assisted Trap consequence for the active Player.";
                case TrapFloorAbilityActivationError.AbilityAlreadyUsed:
                    return $"This {effect} Card has already activated.";
                case TrapFloorAbilityActivationError.ActorIsNotActivePlayer:
                    return $"{effect} assistance activates only for the active Player.";
                case TrapFloorAbilityActivationError.PawnFloorUnavailable:
                    return $"{effect} could not identify the active Player's current Floor.";
                case TrapFloorAbilityActivationError.NoValidDodgeDestination:
                    return "Dodge unavailable — no adjacent Floor.";
                case TrapFloorAbilityActivationError.NoValidRushDestination:
                    return "Rush unavailable — no reachable Floor.";
                default:
                    return $"{effect} assistance could not resolve ({error}).";
            }
        }

        internal void RefreshMovementAssistancePresentation()
        {
            if (host.abilityTargetPresenter == null) return;
            IReadOnlyList<TabletopObjectId> targetFloorCardIds =
                host.trapFloorAbilityResolutionState?.ActiveRushAssistance?.TargetFloorCardIds
                ?? host.trapFloorAbilityResolutionState?.ActiveDodgeAssistance?.TargetFloorCardIds;
            if (targetFloorCardIds == null)
            {
                host.abilityTargetPresenter.ClearCurrent();
                return;
            }

            host.abilityTargetPresenter.Show(targetFloorCardIds);
        }

        internal void RefreshNeutralizedTrapPresentation()
        {
            if (host.abilityTargetPresenter == null) return;
            List<TabletopObjectId> neutralizedFloorCardIds = new List<TabletopObjectId>();
            if (host.trapFloorAbilityResolutionState != null)
            {
                for (int i = 0; i < host.trapFloorAbilityResolutionState.TrapRecords.Count; i++)
                {
                    TrapFloorTrapResolutionRecord trap =
                        host.trapFloorAbilityResolutionState.TrapRecords[i];
                    if (trap.Disposition == TrapFloorTrapResolutionDisposition.Neutralized)
                        neutralizedFloorCardIds.Add(trap.FloorCardId);
                }
            }
            host.abilityTargetPresenter.ShowNeutralized(neutralizedFloorCardIds);
        }

        internal bool IsTrapFloorTrapNeutralized(TabletopObjectId floorCardId)
        {
            return host.trapFloorAbilityResolutionState != null
                && host.trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.Disposition == TrapFloorTrapResolutionDisposition.Neutralized;
        }

        internal bool WasTrapSafelyRevealed(TabletopObjectId floorCardId)
        {
            if (host.trapFloorAbilityResolutionState != null
                && host.trapFloorAbilityResolutionState.TryGetTrap(floorCardId, out _))
                return false;
            if (host.trapFloorActivityFeed == null) return false;
            for (int i = host.trapFloorActivityFeed.Entries.Count - 1; i >= 0; i--)
            {
                TrapFloorActivityEntry entry = host.trapFloorActivityFeed.Entries[i];
                if (entry.FloorCardId == floorCardId
                    && entry.Kind == TrapFloorActivityKind.SafelyRevealedTrap)
                    return true;
            }
            return false;
        }

        internal bool IsTrapFloorTrapPending(TabletopObjectId floorCardId)
        {
            return host.trapFloorAbilityResolutionState != null
                && host.trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.IsPending;
        }

        internal bool IsTrapFloorTrapResolved(TabletopObjectId floorCardId)
        {
            return host.trapFloorAbilityResolutionState != null
                && host.trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && !trap.IsPending;
        }

        // The cost line on inspect text, in card notation (C1). The active game data wins, so a cost set by the
        // rules shows; without a loaded game the card's authored cost is shown.
        internal string FormatInputCost(CardDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string cost;
            if (host.trapFloorTemplate != null
                && host.trapFloorTemplate.GameDefinition.TryGetCard(definition.StableId, out CardDefinitionData data))
            {
                cost = InputCostNotation.Format(data.InputCost);
            }
            else
            {
                cost = FormatAuthoredInputCost(definition.InputCost);
            }

            return CartridgeText.CostLine(cost);
        }

        internal static string FormatAuthoredInputCost(InputCostDefinition inputCost) => CartridgeText.FormatAuthoredInputCost(inputCost);
        // ---------- from TabletopComposition.TrapFloorHints.cs ----------

        // R4 hints (doc 23 §9), behind the hint helper "trapfloor.reach" since H3: the tiles in reach light while
        // the active player holds their pawn, with a RULE reminder over a tile the rules do not allow. A landing the
        // rules do not allow does not flip its tile; a warning points at the Undo button and the turn menu offers
        // Keep move. Nothing is blocked.
        internal bool reachHintShown;
        internal bool reachHintHeld;
        internal int moveTurnRound = -1;
        internal PlayerId moveTurnPlayer;
        internal TrapFloorCoordinate? moveStartTile;
        internal bool guidedMoveThisTurn;
        internal int tooFarStateIndex = -1;
        internal readonly List<TabletopObjectId> reachTileIds = new List<TabletopObjectId>();
        internal readonly List<Vector3> reachPositions = new List<Vector3>();
        internal static readonly Color ReachMarkerColour = new Color(0.55f, 0.95f, 0.4f, 0.9f);
        internal int[,] reachSteps;
        // H3 (owner, 2026-10-15): the face-down tile of a landing the rules do not allow (it does not flip unless
        // the table keeps the move), and the tile under the held pawn for the RULE reminder while holding.
        internal TabletopObjectId notAllowedLandingFloorCardId;
        internal bool holdStartKnown;
        internal TrapFloorCoordinate holdStart;
        internal bool holdTileKnown;
        internal TrapFloorCoordinate holdTile;

        // ---------- R4 hints: reach and "why" (doc 23 §9), run by the hint helpers below (H3) ----------

        // A move destination from an Ability or a Trap (Rush, Dodge, Spring, Crane) is being shown.
        internal bool IsGuidedMoveShown() =>
            host.trapFloorAbilityResolutionState != null
            && (host.trapFloorAbilityResolutionState.ActiveRushAssistance != null
                || host.trapFloorAbilityResolutionState.ActiveDodgeAssistance != null);

        // Each player turn starts its own move count (the round and the active player name the turn).
        internal void EnsureMoveTurn()
        {
            if (host.trapFloorTurnState == null) return;
            if (moveTurnRound == host.trapFloorTurnState.CurrentRound && moveTurnPlayer == host.trapFloorTurnState.ActivePlayerId) return;
            moveTurnRound = host.trapFloorTurnState.CurrentRound;
            moveTurnPlayer = host.trapFloorTurnState.ActivePlayerId;
            moveStartTile = null;
            guidedMoveThisTurn = false;
            tooFarStateIndex = -1;
        }

        internal bool TryGetActivePawnTile(out TabletopObjectId pawnId, out TrapFloorCoordinate tile)
        {
            pawnId = TabletopObjectId.Empty;
            tile = default;
            return host.trapFloorTemplate != null
                && host.trapFloorTurnState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player)
                && (pawnId = player.PawnId) != TabletopObjectId.Empty
                && TryReadPawnTile(pawnId, out tile);
        }

        // True while the active player holds their own pawn (dragged or lifted). Polled each frame; no allocation.
        internal bool IsActivePawnHeld() => host.IsTurnPawnHeld(); // CART-1b: the table's seat pawns

        // Any pawn held or dragged (for the hint's console note when it is not the active player's).
        internal bool IsAnyPawnHeld(out TabletopObjectId pawnId) => host.IsAnyPawnHeld(out pawnId);

        // Called once a frame by the reach helper while a rule in play names it.
        internal void UpdateReachHint(bool hintsOn)
        {
            TabletopObjectId heldPawnId = TabletopObjectId.Empty;
            bool anyHeld = host.trapFloorTemplate != null && IsAnyPawnHeld(out heldPawnId);
            bool show = anyHeld
                && hintsOn
                && !IsGuidedMoveShown()
                && IsActivePawnHeld();
            if (anyHeld != reachHintHeld)
            {
                reachHintHeld = anyHeld;
                if (anyHeld && !show) LogReachSkipped(heldPawnId, hintsOn);
            }

            if (show == reachHintShown) return;
            reachHintShown = show;
            if (!show)
            {
                host.boardFeedback?.HideReach();
                return;
            }

            EnsureMoveTurn();
            if (guidedMoveThisTurn)
            {
                Debug.Log("[Hints] No reach lights: this turn already used an Ability or Trap move.", host);
                return;
            }

            if (!TryGetActivePawnTile(out _, out TrapFloorCoordinate current))
            {
                Debug.Log("[Hints] No reach lights: the active pawn is not over a floor tile.", host);
                return;
            }

            TrapFloorBlindMovementDirection? onlyDirection = null;
            if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blind))
            {
                if (!blind.IsDirectionResolved)
                {
                    Debug.Log("[Hints] No reach lights: roll the Blind direction first.", host);
                    return;
                }

                onlyDirection = blind.MovementDirection;
            }

            TrapFloorCoordinate start = moveStartTile ?? current;
            int allowance = MoveAllowance(out _);
            ComputeMoveSteps(start, onlyDirection);
            reachTileIds.Clear();
            for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= host.trapFloorTemplate.GridColumns; x++)
            {
                for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= host.trapFloorTemplate.GridRows; y++)
                {
                    int steps = reachSteps[x, y];
                    if (steps <= 0 || steps > allowance) continue;
                    if (host.trapFloorTemplate.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id))
                        reachTileIds.Add(id);
                }
            }

            reachPositions.Clear();
            for (int i = 0; i < reachTileIds.Count; i++)
            {
                if (host.Registry.TryGetCardView(reachTileIds[i], out CardView tileView)) reachPositions.Add(tileView.transform.position);
            }

            if (host.EnsureBoardFeedback()) host.boardFeedback.ShowReach(reachPositions, ReachMarkerColour);
        }

        // Console note (once per pick-up) for why a held pawn shows no reach.
        internal void LogReachSkipped(TabletopObjectId heldPawnId, bool hintsOn)
        {
            string reason;
            if (!hintsOn) reason = "Hints are off in the Table menu";
            else if (IsGuidedMoveShown()) reason = "an Ability or Trap destination is shown";
            else if (host.trapFloorTurnState == null || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
                reason = "it is not a player turn";
            else if (host.trapFloorTurnState.IsCurrentFloorFailed) reason = "this round failed";
            else reason = $"this pawn is not the turn player's ({FormatPlayerName(host.trapFloorTurnState.ActivePlayerId)})";
            Debug.Log($"[Hints] No reach lights: {reason}.", host);
        }

        // The stable ID of the active player's Avatar (the card in their Main slot at setup); empty if unknown.
        internal string ActiveAvatarStableId()
        {
            if (host.trapFloorTurnState == null
                || host.matchState == null
                || !TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player)
                || !host.matchState.Cards.TryGetValue(player.AvatarCardId, out CardInstanceState avatarCard)
                || !host.TryGetAvatarLook(avatarCard.BaseState.DefinitionId, out AvatarDefinition avatar))
            {
                return string.Empty;
            }

            return avatar.StableId;
        }

        // Tiles the active player may move this turn: the rules' "move.tiles" (default 2) or their Avatar's own
        // count (move.tiles.avatar.<id>, the Athlete), plus Overcharged or
        // minus Slow; Stuck allows none. <paramref name="why"/> names the changes, empty when there are none.
        internal int MoveAllowance(out string why)
        {
            int tiles = 2;
            string modeId = host.trapFloorTemplate.ActiveMode.StableId;
            if (host.trapFloorTemplate.GameDefinition.TryGetRuleSetting(RuleSettingKeys.MoveTiles, modeId, out string raw)
                && int.TryParse(raw, out int parsed)
                && parsed >= 0)
            {
                tiles = parsed;
            }

            // An Avatar with its own count (the Athlete) uses it instead.
            string avatarId = ActiveAvatarStableId();
            if (!string.IsNullOrEmpty(avatarId)
                && host.trapFloorTemplate.GameDefinition.TryGetRuleSetting(
                    RuleSettingKeys.MoveTilesAvatarPrefix + avatarId, modeId, out string avatarRaw)
                && int.TryParse(avatarRaw, out int avatarTiles)
                && avatarTiles >= 0)
            {
                tiles = avatarTiles;
            }

            why = string.Empty;
            if (TryGetActiveStickyStatus(out _))
            {
                why = "STUCK";
                return 0;
            }

            if (TryGetActiveSlowStatus(out TrapFloorSlowStatusState slow) && slow.MovementModifier != 0)
            {
                why = $"{tiles}, {(slow.MovementModifier > 0 ? "OVERCHARGED" : "SLOW")} {FormatSignedModifier(slow.MovementModifier)}";
                tiles += slow.MovementModifier;
            }

            return Mathf.Max(0, tiles);
        }

        // Steps from the start along each straight line (up, down, left, right) until a hole or the edge (-1: not
        // reachable in one straight line). With a Blind direction only that line counts. Fills reachSteps; reuses it.
        internal void ComputeMoveSteps(TrapFloorCoordinate start, TrapFloorBlindMovementDirection? onlyDirection)
        {
            int columns = host.trapFloorTemplate.GridColumns;
            int rows = host.trapFloorTemplate.GridRows;
            if (reachSteps == null || reachSteps.GetLength(0) != columns + 1 || reachSteps.GetLength(1) != rows + 1)
                reachSteps = new int[columns + 1, rows + 1];
            for (int x = 0; x <= columns; x++)
            {
                for (int y = 0; y <= rows; y++) reachSteps[x, y] = -1;
            }

            if (start.X < 1 || start.Y < 1 || start.X > columns || start.Y > rows) return;
            reachSteps[start.X, start.Y] = 0;
            for (int d = 0; d < 4; d++)
            {
                TrapFloorBlindMovementDirection direction = (TrapFloorBlindMovementDirection)d;
                if (onlyDirection.HasValue && onlyDirection.Value != direction) continue;
                int dx = direction == TrapFloorBlindMovementDirection.Right ? 1 : direction == TrapFloorBlindMovementDirection.Left ? -1 : 0;
                int dy = direction == TrapFloorBlindMovementDirection.Up ? 1 : direction == TrapFloorBlindMovementDirection.Down ? -1 : 0;
                int x = start.X + dx, y = start.Y + dy, step = 1;
                while (IsWalkableTile(x, y))
                {
                    reachSteps[x, y] = step++;
                    x += dx;
                    y += dy;
                }
            }
        }

        // On the grid and not a hole.
        internal bool IsWalkableTile(int x, int y)
        {
            return x >= TrapFloorCoordinate.MinimumAxisValue
                && y >= TrapFloorCoordinate.MinimumAxisValue
                && x <= host.trapFloorTemplate.GridColumns
                && y <= host.trapFloorTemplate.GridRows
                && host.trapFloorTemplate.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id)
                && (host.trapFloorCollapseState == null || !host.trapFloorCollapseState.IsCollapsed(id));
        }

        // After the active pawn lands on another tile (owner, 2026-10-15, H3): a rule check, not a hint, so it runs
        // with hints off too. The count runs from where the pawn stood when the turn started (moving 2 then 2 is a
        // 4-tile move). Returns why the rules do not allow the move, or null; a move not allowed does not flip its
        // tile; the Undo button takes it back or the turn menu's Keep move keeps it (nothing is blocked).
        // TRAP-1: while an Ability or a Trap shows where to go (Spring, Crane, Dodge, Rush), a landing on a lit tile
        // finishes that move (the lights go out); a landing anywhere else keeps the lights and says why. Null when
        // nothing is shown or the pawn landed on a lit tile. Event-time only.
        internal string CheckGuidedMove(TrapFloorCoordinate to)
        {
            if (!IsGuidedMoveShown()) return null;
            EnsureMoveTurn();
            IReadOnlyList<TabletopObjectId> targets =
                host.trapFloorAbilityResolutionState.ActiveRushAssistance?.TargetFloorCardIds
                ?? host.trapFloorAbilityResolutionState.ActiveDodgeAssistance.TargetFloorCardIds;
            if (host.trapFloorTemplate.TryGetFloorCardId(to, out TabletopObjectId landedId))
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != landedId) continue;
                    guidedMoveThisTurn = true; // R4: not a regular move
                    if (pendingLaunchTargetId == landedId)
                    {
                        pendingLaunchPlayerId = PlayerId.Empty;
                        pendingLaunchTargetId = TabletopObjectId.Empty;
                    }

                    if (host.trapFloorAbilityResolutionService != null
                        && host.trapFloorAbilityResolutionService.ClearMovementAssistance())
                        host.abilityTargetPresenter?.ClearCurrent();
                    return null;
                }
            }

            notAllowedLandingFloorCardId = TabletopObjectId.Empty;
            tooFarStateIndex = host.undoHistory.CurrentStateIndex;
            return HasPendingLaunchMove(host.trapFloorTurnState.ActivePlayerId)
                ? "the Trap sends your pawn to the lit tile. Move it there, or End Turn moves it for you."
                : "the Ability moves your pawn to a lit tile only.";
        }

        internal string CheckActivePawnMove(TrapFloorCoordinate? from, TrapFloorCoordinate to)
        {
            EnsureMoveTurn();
            notAllowedLandingFloorCardId = TabletopObjectId.Empty;
            if (!moveStartTile.HasValue)
            {
                if (!from.HasValue) return null; // the pawn came from off the grid: nothing to count from
                moveStartTile = from;
            }

            tooFarStateIndex = -1;
            if (guidedMoveThisTurn || !IsWalkableTile(to.X, to.Y)) return null;
            string problem = DescribeMoveProblem(moveStartTile.Value, to);
            if (problem == null) return null;
            tooFarStateIndex = host.undoHistory.CurrentStateIndex;
            return problem;
        }

        // Why the rules do not allow a move from start to a tile this turn, or null. Reuses reachSteps; call it on
        // an event or when the held pawn's tile changes, not every frame.
        internal string DescribeMoveProblem(TrapFloorCoordinate start, TrapFloorCoordinate to)
        {
            if (to.Equals(start)) return null;
            TrapFloorBlindMovementDirection? onlyDirection = null;
            if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blind))
            {
                if (!blind.IsDirectionResolved) return "BLIND: roll the Blind direction before moving.";
                onlyDirection = blind.MovementDirection;
            }

            int allowance = MoveAllowance(out string why);
            if (allowance == 0 && why == "STUCK") return "STUCK: this turn you stay where you are.";
            if (!IsWalkableTile(to.X, to.Y)) return "That tile is a hole.";
            ComputeMoveSteps(start, onlyDirection);
            int steps = reachSteps[to.X, to.Y];
            if (steps < 0)
            {
                return onlyDirection.HasValue
                    ? $"BLIND: this turn you move {onlyDirection.Value.ToString().ToLowerInvariant()} only."
                    : to.X == start.X || to.Y == start.Y
                        ? "A hole is in the way; moves cannot cross holes."
                        : "Moves go in one straight line (up, down, left or right), with no turning.";
            }

            return steps > allowance
                ? $"That is {steps} tiles from where the turn started; the rules allow {allowance}"
                    + (string.IsNullOrEmpty(why) ? "." : $" ({why}).")
                : null;
        }

        // While the active player holds their pawn (hints on), the tile under it is checked whenever it changes;
        // over a tile the rules do not allow, a RULE reminder in the hint strip says why. Per frame: a position
        // lookup only; text is made only when the tile changes.
        internal void UpdateHoldReminder(bool hintsOn)
        {
            bool held = hintsOn && host.trapFloorTemplate != null && !IsGuidedMoveShown() && IsActivePawnHeld();
            if (!held)
            {
                holdStartKnown = false;
                holdTileKnown = false;
                if (IsShownHint(HoldHintId)) host.RulesCardPanel.HideShownHint();
                return;
            }

            if (!holdStartKnown)
            {
                EnsureMoveTurn();
                if (guidedMoveThisTurn || !TryGetActivePawnTile(out _, out TrapFloorCoordinate pickedUpFrom)) return;
                holdStart = moveStartTile ?? pickedUpFrom;
                holdStartKnown = true;
                holdTileKnown = false;
            }

            if (!TryGetHeldPawnTile(out TrapFloorCoordinate over))
            {
                holdTileKnown = false;
                if (IsShownHint(HoldHintId)) host.RulesCardPanel.HideShownHint(); // off the board
                return;
            }

            if (holdTileKnown && over.Equals(holdTile)) return;
            holdTile = over;
            holdTileKnown = true;
            string problem = DescribeMoveProblem(holdStart, over);
            if (problem == null)
            {
                if (IsShownHint(HoldHintId)) host.RulesCardPanel.HideShownHint();
                return;
            }

            ShowHelperHint(HelperReminder(HoldHintId, HintTriggers.PawnDragged, "own",
                $"Not allowed here: {problem} Put the pawn on a lit tile.", HoldHintPriority));
        }

        // The tile under the active player's pawn view right now (it moves while held; the match state does not).
        internal bool TryGetHeldPawnTile(out TrapFloorCoordinate tile)
        {
            tile = default;
            if (host.coordinateConverter == null
                || host.matchState == null
                || !TryGetTrapFloorPlayerSetup(host.trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                return false;
            }

            for (int i = 0; i < host.Registry.pawnViews.Count; i++)
            {
                PawnView view = host.Registry.pawnViews[i];
                if (view == null || !view.IsBound || view.ObjectId != player.PawnId) continue;
                return host.trapFloorTemplate.TryGetFloorCoordinateAt(
                    host.matchState,
                    host.coordinateConverter.ToTableCoordinate(view.transform.position),
                    out tile);
            }

            return false;
        }

        // Shows a helper's reminder in the hint strip now (not tied to an event). Reminders show with hints on.
        internal void ShowHelperHint(HintEntry hint)
        {
            if (host.runtimeUi == null || host.hintService == null || host.hintService.Level == HintLevel.Off || hint == null) return;
            if (host.shownHint != null
                && host.runtimeUi.IsHintShown
                && host.shownHint.Priority > hint.Priority
                && !string.Equals(host.shownHint.StableId, hint.StableId, StringComparison.Ordinal))
            {
                return;
            }

            if (!host.runtimeUi.ShowHint("RULE", hint.Text, host.RulesCardPanel.DismissShownHint, host.RulesCardPanel.HideShownHint)) return;
            host.shownHint = hint;
        }

        // Keep move: the table agrees to a move the rules do not allow, so its face-down tile flips now.
        internal void KeepNotAllowedMove()
        {
            TabletopObjectId floorCardId = notAllowedLandingFloorCardId;
            notAllowedLandingFloorCardId = TabletopObjectId.Empty;
            tooFarStateIndex = -1;
            if (!floorCardId.IsEmpty
                && host.trapFloorTurnState != null
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.trapFloorTemplate.TryGetFloorCardState(host.matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                && !floorCard.IsRevealed
                && (host.trapFloorCollapseState == null || !host.trapFloorCollapseState.IsCollapsed(floorCardId)))
            {
                host.landedFloorCards.Add(floorCardId);
                TriggerLandedTile(floorCardId, host.trapFloorTurnState.ActivePlayerId);
            }

            RefreshTrapFloorStatusUi();
        }

        // "Keep move" is offered only while the move is still the latest step in the history.
        internal bool IsTooFarMoveUndoable()
        {
            return tooFarStateIndex >= 0
                && moveTurnRound == host.trapFloorTurnState.CurrentRound
                && moveTurnPlayer == host.trapFloorTurnState.ActivePlayerId
                && host.undoHistory.CurrentStateIndex == tooFarStateIndex;
        }

        // ---------- H3: Trap Floor hint helpers (doc 23 §10) ----------

        internal const string HoldHintId = "helper.trapfloor.reach.hold";
        internal const string PrizeHintId = "helper.trapfloor.prize";
        internal const string TrapsWaitingHintId = "helper.trapfloor.traps-waiting";
        internal const string CollapseHintId = "helper.trapfloor.collapse";
        // The holding reminder outranks every tip; the other reminders let a rule's own tip show first (twice at most).
        internal const int HoldHintPriority = 8;
        internal const int HelperHintPriority = 5;

        // Added once with the hint service; each runs only while a rule in play names it.
        internal void AddTrapFloorHintHelpers()
        {
            host.hintService.AddHelper(new ReachHintHelper(host));
            host.hintService.AddHelper(new PrizeHintHelper(host));
            host.hintService.AddHelper(new TrapsWaitingHintHelper(host));
            host.hintService.AddHelper(new CollapseTargetHintHelper(host));
        }

        internal bool IsShownHint(string stableId) =>
            host.shownHint != null && string.Equals(host.shownHint.StableId, stableId, StringComparison.Ordinal);

        internal static HintEntry HelperReminder(string stableId, string trigger, string context, string text, int priority) =>
            new HintEntry(stableId, trigger, context, text, HintKind.Reminder, priority, 0);

        // Marks the Floorfall collapse target with the board feedback marker (the tile's art is never recoloured).
        internal void ShowFloorfallTarget(TabletopObjectId floorCardId)
        {
            if (host.floorfallTargetPresenter == null || floorCardId.IsEmpty) return;
            if (host.EnsureBoardFeedback() && host.Registry.TryGetCardView(floorCardId, out CardView tileView))
                host.floorfallTargetPresenter.Show(floorCardId, tileView.transform.position, host.boardFeedback);
            else
                host.floorfallTargetPresenter.Clear();
        }
        // ---------- from TabletopComposition.TrapFloorStatus.cs ----------

        // R2c: asks for a redraw at the end of the frame (the status card is no longer rebuilt every frame).
        internal void RefreshTrapFloorStatusUi()
        {
            host.MarkTableUiDirty();
        }

        // R2c: the turn, round, waiting and pending states the status card shows, folded into one number each frame
        // (no garbage), so a change made without an announcement still redraws it.
        internal int StatusUiSignature()
        {
            unchecked
            {
                int hash = 17;
                TrapFloorTurnState turn = host.trapFloorTurnState;
                if (turn != null)
                {
                    hash = (hash * 31) + (int)turn.Phase;
                    hash = (hash * 31) + turn.CurrentRound;
                    hash = (hash * 31) + turn.ActivePlayerId.GetHashCode();
                    hash = (hash * 31) + turn.FloorOperatorPlayerId.GetHashCode();
                    hash = (hash * 31) + (turn.IsCurrentFloorFailed ? 1 : 0);
                }

                if (host.trapFloorObjectiveState != null) hash = (hash * 31) + (host.trapFloorObjectiveState.IsWon ? 1 : 0);
                if (host.trapFloorRoundState != null)
                {
                    hash = (hash * 31) + (int)host.trapFloorRoundState.Phase;
                    hash = (hash * 31) + host.trapFloorRoundState.CurrentRoundNumber;
                    hash = (hash * 31) + host.trapFloorRoundState.AcceptedFloorfallCount;
                    hash = (hash * 31) + (host.trapFloorRoundState.IsScheduleCompleted ? 1 : 0);
                }

                hash = (hash * 31) + (host.floormasterLifecycleState?.PendingCard != null ? 1 : 0);
                hash = (hash * 31) + (host.physicalFloorfallPending ? 1 : 0);
                hash = (hash * 31) + (host.physicalFloorCollapsePending ? 1 : 0);
                hash = (hash * 31) + (host.physicalBlindDirectionPending ? 1 : 0);
                hash = (hash * 31) + (host.pendingPurchaseDefinitionStableId?.Length ?? 0);
                hash = (hash * 31) + host.waitingLandingFloorCardId.GetHashCode();
                hash = (hash * 31) + pendingLaunchPlayerId.GetHashCode();
                hash = (hash * 31) + pendingLaunchTargetId.GetHashCode();
                hash = (hash * 31) + (host.trapFloorActivityFeed?.Entries.Count ?? 0);
                hash = (hash * 31) + host.localPlayerId.GetHashCode();
                // V5: the player card's In hand and Keys redraw when they change.
                if (turn != null && TryGetTrapFloorPlayerSetup(turn.ActivePlayerId, out TrapFloorPlayerSetupDefinition handOwner))
                {
                    hash = (hash * 31) + ContainerCount(handOwner.HandContainerId);
                }

                if (host.trapFloorObjectiveState != null) hash = (hash * 31) + host.trapFloorObjectiveState.CollectedKeyCount;
                return hash;
            }
        }

        // R2c: builds and shows the status card and turn strip (called by the composition when something changed).
        internal void RenderTrapFloorStatusUi()
        {
            if (host.runtimeUi == null || !host.IsInitialized)
            {
                return;
            }

            if (host.trapFloorObjectiveState != null && host.trapFloorTurnState != null)
            {
                // UX-1: the turn names the player's colour, and the strip's chip shows it.
                PlayerId turnPlayerId = host.trapFloorTurnState.IsCurrentFloorFailed
                    ? PlayerId.Empty
                    : host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                        ? host.trapFloorTurnState.ActivePlayerId
                        : host.trapFloorTurnState.FloorOperatorPlayerId;
                Color? turnColor = host.TryGetSeatIndex(turnPlayerId, out int turnSeatIndex)
                    ? PlayerSeatColor(turnSeatIndex)
                    : (Color?)null;
                // V5 (owner, 2026-10-24: follow the Figma): "RED'S TURN - 2 MOVES"; the effects are on the player card.
                string phase = host.trapFloorTurnState.IsCurrentFloorFailed
                    ? "ALL PLAYERS ELIMINATED"
                    : host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                        ? $"{(turnColor.HasValue ? PlayerColorName(turnSeatIndex) : FormatPlayerName(turnPlayerId).ToUpperInvariant())}'S TURN"
                            + TurnMovesText()
                        : "FLOOR TURN";
                string turnDetail = CurrentTrapFloorCollapseStatusText();
                if (!host.trapFloorTurnState.IsCurrentFloorFailed
                    && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
                {
                    turnDetail += $"\nOperator: {FormatPlayerName(host.trapFloorTurnState.FloorOperatorPlayerId)}";
                }

                // The player list lives on the status card; the one-line turn strip keeps only the Keys.
                turnDetail += $"\n{CurrentTrapFloorPlayerStatesText()}";
                TrapFloorStatusModel turnStatus = new TrapFloorStatusModel(
                    $"ROUND {host.trapFloorTurnState.CurrentRound}", // the floor is on the player card (V5)
                    phase,
                    CurrentTrapFloorObjectiveProgressText(),
                    turnDetail,
                    host.trapFloorTurnState.IsCurrentFloorFailed
                        ? "ALL PLAYERS ELIMINATED"
                        : host.trapFloorObjectiveState.IsWon ? "VICTORY" : string.Empty,
                    host.RulesCardPanel.HintText(TrapFloorTurnGuidanceText()),
                    turnColor,
                    BuildHudPlayers(turnPlayerId),
                    BuildPlayerCard(turnPlayerId));
                host.runtimeUi.ShowTrapFloorStatus(
                    turnStatus,
                    BuildFloorfallStatusModel(),
                    host.AddHandTrayToggleAction(BuildTrapFloorTurnActions()));
                return;
            }

            if (host.trapFloorRoundState == null)
            {
                host.runtimeUi.HideTrapFloorStatus();
                return;
            }

            TrapFloorPendingFloormasterCard pendingCard = host.floormasterLifecycleState?.PendingCard;
            string detail = string.Empty;
            if (pendingCard != null)
            {
                detail =
                    $"Pending Trigger: {FormatPlayerName(pendingCard.SearchingPlayerId)} / {pendingCard.Category}\n"
                    + "Resolve the card's effect at the table, then continue.";
            }
            else if (host.trapFloorRoundState.Phase == TrapFloorRoundPhase.Floorfall)
            {
                detail =
                    $"Floorfalls performed this phase: {host.trapFloorRoundState.AcceptedFloorfallCount}\n"
                    + "Roll as many Floorfalls as your mode asks for.";
            }
            else if (host.trapFloorRoundState.IsScheduleCompleted)
            {
                detail = "All 10 rounds are played. Decide the result together.";
            }

            TrapFloorStatusModel status = new TrapFloorStatusModel(
                $"Round: {host.trapFloorRoundState.CurrentRoundNumber} / {TrapFloorRoundState.FinalRoundNumber}",
                $"Phase: {host.trapFloorRoundState.Phase}",
                $"Search + Trigger: {host.trapFloorRoundState.CompletedSearchTriggerCount} / "
                    + $"{host.trapFloorRoundState.ParticipatingPlayerIds.Count} Players complete",
                detail,
                $"Hand: {ContainerCount(host.handContainerId)}",
                host.RulesCardPanel.HintText(TrapFloorActionHelpText()));

            FloorfallStatusModel floorfall = BuildFloorfallStatusModel();
            host.runtimeUi.ShowTrapFloorStatus(status, floorfall, host.AddHandTrayToggleAction(BuildTrapFloorAssistedActions()));
        }

        // V5: " - 2 MOVES" after the turn player's name (nothing outside a player turn).
        private string TurnMovesText()
        {
            if (host.trapFloorTemplate == null || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn) return string.Empty;
            int moves = MoveAllowance(out _);
            return moves == 1 ? " - 1 MOVE" : $" - {moves} MOVES";
        }

        // V5: the players in turn order for the round bar ("Blue", "Red", ...), the turn player ringed.
        private List<TrapFloorHudPlayer> BuildHudPlayers(PlayerId turnPlayerId)
        {
            List<TrapFloorHudPlayer> players = new List<TrapFloorHudPlayer>(host.trapFloorTurnState.PlayerOrder.Count);
            for (int i = 0; i < host.trapFloorTurnState.PlayerOrder.Count; i++)
            {
                PlayerId playerId = host.trapFloorTurnState.PlayerOrder[i];
                bool seated = host.TryGetSeatIndex(playerId, out int seatIndex);
                string name = seated ? TitleCase(PlayerColorName(seatIndex)) : FormatPlayerName(playerId);
                Color color = PlayerSeatColor(seated ? seatIndex : -1);
                bool gone = host.trapFloorTurnState.GetPlayerState(playerId) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                    || (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(playerId));
                if (gone) color.a = 0.35f;
                players.Add(new TrapFloorHudPlayer(name, color, playerId == turnPlayerId));
            }

            return players;
        }

        // V5: the turn player's card: RED PLAYER, Avatar, Keys, Status, In hand, Floor.
        private TrapFloorPlayerCardModel BuildPlayerCard(PlayerId playerId)
        {
            if (playerId.IsEmpty) return null;
            string title = host.TryGetSeatIndex(playerId, out int seatIndex)
                ? $"{PlayerColorName(seatIndex)} PLAYER"
                : FormatPlayerName(playerId).ToUpperInvariant();
            string avatar = "—";
            string inHand = "—";
            if (TryGetTrapFloorPlayerSetup(playerId, out TrapFloorPlayerSetupDefinition setup))
            {
                if (host.matchState.Cards.TryGetValue(setup.AvatarCardId, out CardInstanceState avatarCard)
                    && host.TryGetAvatarLook(avatarCard.BaseState.DefinitionId, out AvatarDefinition look))
                {
                    avatar = look.DisplayName;
                }

                int cards = ContainerCount(setup.HandContainerId);
                inHand = cards == 1 ? "1 CARD" : $"{cards} CARDS";
            }

            string keys = host.trapFloorObjectiveState != null
                ? $"{host.trapFloorObjectiveState.CollectedKeyCount} / {KeysNeededForGrandExit()}"
                : "—";
            string floor = host.trapFloorTemplate != null
                ? (host.trapFloorTemplate.FloorCount > 1
                    ? $"{host.trapFloorTemplate.FloorIndex + 1}/{host.trapFloorTemplate.FloorCount} · {host.trapFloorTemplate.ActiveMode.DisplayName}"
                    : $"{host.trapFloorTemplate.FloorIndex + 1} · {host.trapFloorTemplate.ActiveMode.DisplayName}")
                : "—";
            return new TrapFloorPlayerCardModel(title, avatar, keys, PlayerStatusText(playerId), inHand, floor);
        }

        // V5: the effects on a player this round, as the card's Status chip ("OVERCHARGED +1"); empty when none.
        private string PlayerStatusText(PlayerId playerId)
        {
            if (host.trapFloorAbilityResolutionState == null || host.trapFloorTurnState == null) return string.Empty;
            int round = host.trapFloorTurnState.CurrentRound;
            string status = string.Empty;
            if (host.trapFloorAbilityResolutionState.TryGetSlowStatus(playerId, round, out TrapFloorSlowStatusState slow)
                && slow.MovementModifier != 0)
            {
                status = $"{(slow.MovementModifier > 0 ? "OVERCHARGED" : "SLOW")} {FormatSignedModifier(slow.MovementModifier)}";
            }

            if (host.trapFloorAbilityResolutionState.TryGetBlindStatus(playerId, round, out _)) status = JoinStatus(status, "BLIND");
            if (host.trapFloorAbilityResolutionState.TryGetStickyStatus(playerId, round, out _)) status = JoinStatus(status, "STUCK");
            int pending = PendingEndTurnEffects(playerId, null);
            if (pending > 0) status = JoinStatus(status, pending == 1 ? "1 TRAP WAITING" : $"{pending} TRAPS WAITING");
            return status;
        }

        private static string JoinStatus(string status, string more) => status.Length == 0 ? more : status + " · " + more;

        private static string TitleCase(string word) =>
            string.IsNullOrEmpty(word) ? string.Empty : word.Substring(0, 1) + word.Substring(1).ToLowerInvariant();

        internal FloorfallStatusModel BuildFloorfallStatusModel()
        {
            if (host.floorfallState == null || !host.floorfallState.CurrentTarget.HasValue)
            {
                return new FloorfallStatusModel(false, string.Empty, string.Empty, string.Empty);
            }

            TrapFloorFloorfallTarget target = host.floorfallState.CurrentTarget.Value;
            return new FloorfallStatusModel(
                true,
                $"Die 1 / X: {target.XAxisRoll.Value}   Die 2 / Y: {target.YAxisRoll.Value}",
                $"Coordinate: {target.Coordinate}",
                $"Current target: Floor Card {target.Coordinate}");
        }

        internal List<PopupActionOption> BuildTrapFloorAssistedActions()
        {
            List<PopupActionOption> actions = new List<PopupActionOption>();
            switch (host.trapFloorRoundState.Phase)
            {
                case TrapFloorRoundPhase.Start:
                    actions.Add(new PopupActionOption(
                        host.trapFloorRoundState.CurrentRoundNumber == 1
                            ? "Begin Round / Continue to Search"
                            : "Movement done / Continue",
                        true,
                        () => CompleteTrapFloorStart()));
                    break;
                case TrapFloorRoundPhase.Search:
                    for (int i = 0; i < host.trapFloorRoundState.ParticipatingPlayerIds.Count; i++)
                    {
                        PlayerId playerId = host.trapFloorRoundState.ParticipatingPlayerIds[i];
                        if (host.trapFloorRoundState.HasCompletedSearchTrigger(playerId))
                        {
                            continue;
                        }

                        PlayerId searchingPlayerId = playerId;
                        actions.Add(new PopupActionOption(
                            $"Search - {FormatPlayerName(searchingPlayerId)}",
                            true,
                            () => SearchFloormasterDeck(searchingPlayerId)));
                    }

                    break;
                case TrapFloorRoundPhase.Trigger:
                    actions.Add(new PopupActionOption(
                        "Card effect done",
                        true,
                        () => CompletePendingFloormasterTrigger()));
                    break;
                case TrapFloorRoundPhase.Floorfall:
                    actions.Add(new PopupActionOption(
                        host.physicalFloorfallPending ? "Floorfall: roll the dice" : "Roll Floorfall",
                        true,
                        () =>
                        {
                            // While waiting it only reminds the player (DICE-1).
                            if (host.physicalFloorfallPending) host.ShowMessage("FLOORFALL — roll both floor dice (pick them up and throw them).");
                            else BeginPhysicalFloorfall();
                        }));
                    actions.Add(new PopupActionOption(
                        "Floorfall done",
                        host.trapFloorRoundState.AcceptedFloorfallCount > 0,
                        () => CompleteFloorfallPhase()));
                    break;
                case TrapFloorRoundPhase.End:
                    actions.Add(new PopupActionOption(
                        "End round",
                        true,
                        () => CompleteEndPhase()));
                    break;
            }

            return actions;
        }

        internal List<PopupActionOption> BuildTrapFloorTurnActions()
        {
            List<PopupActionOption> actions = new List<PopupActionOption>();
            if (host.trapFloorTurnState == null)
            {
                return actions;
            }

            if (host.trapFloorTurnState.IsCurrentFloorFailed
                || (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon))
            {
                // FLOORS-1: a won or lost floor goes on to the next, smaller floor (or ends the game on the last).
                bool won = !host.trapFloorTurnState.IsCurrentFloorFailed || FloorFailedCountsAsWin(); // MODE-1
                actions.Add(new PopupActionOption(
                    FloorEndedActionLabel(),
                    true,
                    () => ShowFloorEnded(won)));
                return actions;
            }

            if (host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                    && !blindStatus.IsDirectionResolved)
                {
                    actions.Add(new PopupActionOption(
                        host.physicalBlindDirectionPending ? "Blind: roll the d4" : "Roll Blind Direction",
                        true,
                        () =>
                        {
                            // While waiting it only reminds the player (DICE-1).
                            if (host.physicalBlindDirectionPending) host.ShowMessage("BLIND — roll the d4 for your direction.");
                            else BeginPhysicalBlindDirection();
                        }));
                }
                // Search and Careful Search are gone (doc 18 §15.2, G2d): landing on a tile flips it for free.
                bool purchasePending = host.pendingControllerPurchaseState != null
                    && host.pendingControllerPurchaseState.IsActive
                    && host.pendingControllerPurchaseState.PlayerId == host.trapFloorTurnState.ActivePlayerId;
                if (purchasePending)
                {
                    actions.Add(new PopupActionOption(
                        "Confirm Payment",
                        host.pendingControllerPurchaseState.IsPaymentComplete,
                        ConfirmPendingActionAbilityPurchase));
                    actions.Add(new PopupActionOption(
                        "Cancel Payment",
                        true,
                        CancelPendingActionAbilityPurchase));
                }
                else if (TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile))
                {
                    // The Buy Ability market is retired (doc 18 §15.4); prizes are taken from the floor.
                    actions.Add(new PopupActionOption(
                        TakePrizeLabel(prizeTile),
                        true,
                        BeginTakeFloorPrize));
                }
                // After a move the rules do not allow: the Undo button takes it back (owner, 2026-10-15: no Undo
                // entry here); the table may keep it instead (nothing is blocked).
                if (IsTooFarMoveUndoable())
                {
                    actions.Add(new PopupActionOption(
                        notAllowedLandingFloorCardId.IsEmpty ? "Keep move" : "Keep move (flip the tile)",
                        true,
                        KeepNotAllowedMove));
                }

                // ABIL-DECK: the turn menu no longer offers "Draw 3 Abilities" (owner, 2026-10-21): players draw
                // from the Ability deck themselves, as the rules card (tf-turn-draw) says.
                // FIX-3 (owner, 2026-10-22: "other player can take an already revealed key… it's not working"): a Key under
                // the turn player's pawn can always be taken from the turn panel too.
                if (!purchasePending && TryGetActivePawnUnclaimedKey(out TrapFloorFloorCardState keyTile))
                {
                    TabletopObjectId keyId = keyTile.ObjectId;
                    actions.Add(new PopupActionOption(
                        $"Take Key ({keyTile.Content.DisplayName})",
                        true,
                        () =>
                        {
                            host.ContextMenus.CloseContextMenu();
                            ClaimTrapFloorKey(keyId);
                        }));
                }

                // TURN-2 (owner, 2026-10-22, gameplay reference): drawing the Controller hand up to the limit is one row.
                if (!purchasePending && TryGetHandDrawNeed(out int handNeed, out bool blindNoDraw))
                {
                    int limit = host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
                    actions.Add(new PopupActionOption(
                        blindNoDraw ? "Blind: no draw this turn" : $"Draw up to {limit} (+{handNeed})",
                        !blindNoDraw,
                        DrawUpToHandLimitForTurn));
                }

                // Owner, 2026-10-20: a waiting trap and a Reaction that can stop it: offered in the turn menu too.
                if (!purchasePending && CountUsableReactions(host.trapFloorTurnState.ActivePlayerId) > 0)
                {
                    actions.Add(new PopupActionOption(
                        "Use a Reaction…",
                        true,
                        () => ShowReactionOffer(host.trapFloorTurnState.ActivePlayerId)));
                }

                // F3: End Turn says when it will apply waiting Traps, and asks first.
                int waiting = PendingEndTurnEffects(host.trapFloorTurnState.ActivePlayerId, null);
                string applies = waiting > 0 ? $" (applies {waiting})" : string.Empty;
                actions.Add(new PopupActionOption(
                    "Skip Turn" + applies,
                    true,
                    () => RequestEndTurn(true)));
                actions.Add(new PopupActionOption(
                    "End Turn" + applies,
                    true,
                    () => RequestEndTurn(false)));
                return actions;
            }

            if (host.trapFloorCollapseState != null && !collapsePreviewId.IsEmpty && host.physicalFloorCollapsePending)
            {
                actions.Add(new PopupActionOption(CollapsePreviewLabel(), true, ConfirmFloorCollapse)); // ROUND-1
            }
            else if (host.trapFloorCollapseState != null)
            {
                bool collapseWaiting = host.trapFloorCollapseState.IsCollapsePending;
                actions.Add(new PopupActionOption(
                    collapseWaiting ? "Roll the 2 floor dice" : "Collapse Floor",
                    collapseWaiting || !host.trapFloorCollapseState.IsBoardExhausted,
                    collapseWaiting ? (Action)RemindFloorCollapseRoll : () => BeginPhysicalFloorCollapse()));
            }

            actions.Add(new PopupActionOption(
                "End Floor Turn",
                true,
                AdvanceTrapFloorTurn));
            return actions;
        }

        internal string TrapFloorTurnGuidanceText()
        {
            if (host.trapFloorTurnState.IsCurrentFloorFailed)
            {
                return FloorFailedCountsAsWin() // MODE-1
                    ? $"Everyone left has escaped or is out: the floor is won. {FloorEndedChoiceText(true)}"
                    : $"Everyone is out: the floor is lost. {FloorEndedChoiceText()}";
            }

            if (host.trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                return "Roll 2d6 to resolve Collapse\nEnd Floor Turn when complete";
            }

            if (host.pendingControllerPurchaseState != null
                && host.pendingControllerPurchaseState.IsActive
                && host.pendingControllerPurchaseState.PlayerId == host.trapFloorTurnState.ActivePlayerId)
            {
                if (host.pendingControllerPurchaseState.IsPaymentComplete)
                    return "PAYMENT READY — Confirm Payment";
                string abilityName = FindPurchaseDisplayName(
                    host.pendingControllerPurchaseState.PurchasedCardDefinitionStableId)
                    .ToUpperInvariant();
                return $"{PaymentVerb(host.pendingControllerPurchaseState)} {abilityName} — Place the chosen cards in your Console";
            }

            int handLimit = host.trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
            int moveTiles = MoveAllowance(out _);
            string standardGuidance =
                $"Draw up to {handLimit} if needed\n"
                + (TryGetAbilityOfferAction(out _) ? $"Draw {AbilityOfferSize} from the Ability deck and buy any you can pay for\n" : string.Empty)
                + $"Move up to {moveTiles} tile{(moveTiles == 1 ? string.Empty : "s")} "
                + "in one straight line (they light while you hold your pawn); landing flips a tile. "
                + "Take a prize you stand on, then End Turn";
            bool isBlind = TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus);
            bool isSlow = TryGetActiveSlowStatus(out TrapFloorSlowStatusState slowStatus);
            bool isSticky = TryGetActiveStickyStatus(out _);
            string slowGuidance = isSlow
                ? $"{(slowStatus.MovementModifier > 0 ? "OVERCHARGED" : "SLOW")} — Movement {FormatSignedModifier(slowStatus.MovementModifier)} this round"
                : string.Empty;
            string movementGuidance = isSticky
                ? "STICKY — Stay in place this round"
                : string.Empty;
            if (isSlow)
                movementGuidance = string.IsNullOrEmpty(movementGuidance)
                    ? slowGuidance
                    : $"{movementGuidance}\n{slowGuidance}";
            // G2d: the old Search / Buy words are gone; the Shop is named only while it is open.
            string shopWord = IsShopOpen() ? "Shop / " : string.Empty;
            if (!isBlind)
            {
                string availableActions = isSticky
                    ? $"Draw up to {handLimit} if needed\nAction ability / {shopWord}Skip"
                    : standardGuidance;
                return string.IsNullOrEmpty(movementGuidance)
                    ? availableActions
                    : $"{movementGuidance}\n{availableActions}";
            }
            string directionGuidance = blindStatus.IsDirectionResolved
                ? $"BLIND DIRECTION: {blindStatus.MovementDirection.Value.ToString().ToUpperInvariant()}"
                : "BLIND — Roll d4 for movement direction";
            string blindActions = isSticky ? $"Action ability / {shopWord}Skip" : $"Move / Action ability / {shopWord}Skip";
            return string.IsNullOrEmpty(movementGuidance)
                ? $"BLIND — No draw this round\n{directionGuidance}\n{blindActions}"
                : $"BLIND — No draw this round\n{directionGuidance}\n{movementGuidance}\n{blindActions}";
        }

        internal bool TryGetActiveBlindStatus(out TrapFloorBlindStatusState status)
        {
            if (host.trapFloorAbilityResolutionState != null
                && host.trapFloorTurnState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.trapFloorAbilityResolutionState.TryGetBlindStatus(
                    host.trapFloorTurnState.ActivePlayerId,
                    host.trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        internal bool TryGetActiveSlowStatus(out TrapFloorSlowStatusState status)
        {
            if (host.trapFloorAbilityResolutionState != null
                && host.trapFloorTurnState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.trapFloorAbilityResolutionState.TryGetSlowStatus(
                    host.trapFloorTurnState.ActivePlayerId,
                    host.trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        internal bool TryGetActiveStickyStatus(out TrapFloorStickyStatusState status)
        {
            if (host.trapFloorAbilityResolutionState != null
                && host.trapFloorTurnState != null
                && !host.trapFloorTurnState.IsCurrentFloorFailed
                && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && host.trapFloorAbilityResolutionState.TryGetStickyStatus(
                    host.trapFloorTurnState.ActivePlayerId,
                    host.trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        internal static string FormatSignedModifier(int modifier) =>
            modifier > 0 ? $"+{modifier}" : modifier.ToString();

        internal bool HasEliminatedTrapFloorPlayers()
        {
            for (int i = 0; i < host.trapFloorTurnState.PlayerOrder.Count; i++)
            {
                if (host.trapFloorTurnState.GetPlayerState(host.trapFloorTurnState.PlayerOrder[i])
                    == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    return true;
                }
            }

            return false;
        }

        internal string CurrentStatusText()
        {
            if (host.activeSession != null
                && host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                if (Time.unscaledTime <= host.operationMessageUntil)
                {
                    return host.operationMessage;
                }

                return $"Empty Table | Objects {host.matchState?.ObjectCount ?? 0}";
            }

            string counts = $"Trap Floor | Floor Cards {CurrentFloorCardCount()}";
            string objective = CurrentTrapFloorObjectiveText();
            string activity = CurrentTrapFloorActivityText();
            string persistentStatus = string.IsNullOrEmpty(objective)
                ? counts
                : $"{counts}\n{objective}";
            if (!string.IsNullOrEmpty(activity))
            {
                persistentStatus = $"{persistentStatus}\n{activity}";
            }

            return Time.unscaledTime <= host.operationMessageUntil
                ? $"{host.operationMessage}\n{persistentStatus}"
                : persistentStatus;
        }

        internal string CurrentTrapFloorActivityText()
        {
            if (host.trapFloorActivityFeed == null || host.trapFloorActivityFeed.Entries.Count == 0)
            {
                return string.Empty;
            }

            int firstIndex = Math.Max(0, host.trapFloorActivityFeed.Entries.Count - 2);
            string text = string.Empty;
            for (int i = firstIndex; i < host.trapFloorActivityFeed.Entries.Count; i++)
            {
                TrapFloorActivityEntry entry = host.trapFloorActivityFeed.Entries[i];
                string line;
                switch (entry.Kind)
                {
                    case TrapFloorActivityKind.RevealedFloorContent:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} revealed "
                            + $"{entry.ContentName} [{entry.ContentCategory}]";
                        break;
                    case TrapFloorActivityKind.SafelyRevealedTrap:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} revealed {entry.ContentName} safely";
                        break;
                    case TrapFloorActivityKind.ClaimedKey:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} claimed {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.WonGame:
                        line = "TRAP FLOOR VICTORY";
                        break;
                    case TrapFloorActivityKind.PlayerEscaped:
                        line = $"{FormatPlayerName(entry.ActorPlayerId).ToUpperInvariant()} ESCAPED";
                        break;
                    case TrapFloorActivityKind.TriggeredFloorfall:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} triggered Floorfall";
                        break;
                    case TrapFloorActivityKind.RolledFloorfall:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} rolled "
                            + $"{entry.XAxisResult}/{entry.YAxisResult} for Floor {entry.Coordinate}";
                        break;
                    case TrapFloorActivityKind.CollapsedFloor:
                        line = $"Floor {entry.Coordinate} collapsed";
                        break;
                    case TrapFloorActivityKind.SkippedTurn:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} skipped their turn";
                        break;
                    case TrapFloorActivityKind.PlayerEliminated:
                        line = entry.ContentDefinitionId.IsEmpty
                            ? $"{FormatPlayerName(entry.ActorPlayerId).ToUpperInvariant()} ELIMINATED"
                            : $"{FormatPlayerShortName(entry.ActorPlayerId)} was eliminated by {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.AllPlayersEliminated:
                        line = "ALL PLAYERS ELIMINATED";
                        break;
                    case TrapFloorActivityKind.PlayersReactivated:
                        line = "ALL PLAYERS REACTIVATED";
                        break;
                    case TrapFloorActivityKind.UsedDisarm:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Disarm on {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedShield:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Shield against {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedDodge:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Dodge on {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedRush:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} activated Rush";
                        break;
                    case TrapFloorActivityKind.UsedCheck:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} checked "
                            + $"{entry.ContentName} — Trap neutralized";
                        break;
                    case TrapFloorActivityKind.BlindApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Blind applies next Round";
                        break;
                    case TrapFloorActivityKind.BlindDirectionRolled:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} rolled "
                            + $"{entry.BlindDirectionResult} for Blind direction";
                        break;
                    case TrapFloorActivityKind.SlowApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Slow applies next Round";
                        break;
                    case TrapFloorActivityKind.StickyApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Sticky applies next Round";
                        break;
                    default:
                        line = string.Empty;
                        break;
                }

                text = string.IsNullOrEmpty(text) ? line : $"{text}\n{line}";
            }

            return text;
        }

        internal string CurrentTrapFloorObjectiveText()
        {
            if (host.trapFloorObjectiveState == null)
            {
                return string.Empty;
            }

            string progress = CurrentTrapFloorObjectiveProgressText();
            if (host.trapFloorTurnState != null && host.trapFloorTurnState.IsCurrentFloorFailed)
            {
                return $"{progress} | ALL PLAYERS ELIMINATED";
            }
            return host.trapFloorObjectiveState.IsWon ? $"{progress} | VICTORY" : progress;
        }

        internal string CurrentTrapFloorObjectiveProgressText()
        {
            string keys =
                $"KEYS {host.trapFloorObjectiveState.CollectedKeyCount} / {KeysNeededForGrandExit()}";
            return WinMode == TrapFloorWinMode.Survival // MODE-1
                ? $"{keys}\nESCAPED {host.trapFloorObjectiveState.EscapedPlayerCount} / "
                    + $"{host.activeSession.Request.ActivePlayerIds.Count}"
                : keys;
        }

        // F3: the effects on a player this round, as chips after their name on the turn strip.
        internal string PlayerEffectChips(PlayerId playerId)
        {
            if (host.trapFloorAbilityResolutionState == null || host.trapFloorTurnState == null || playerId.IsEmpty)
            {
                return string.Empty;
            }

            int round = host.trapFloorTurnState.CurrentRound;
            string chips = string.Empty;
            if (host.trapFloorAbilityResolutionState.TryGetBlindStatus(playerId, round, out _)) chips += "  · BLIND";
            if (host.trapFloorAbilityResolutionState.TryGetSlowStatus(playerId, round, out TrapFloorSlowStatusState slow))
            {
                chips += slow.MovementModifier > 0 ? "  · OVERCHARGED" : "  · SLOW";
            }

            if (host.trapFloorAbilityResolutionState.TryGetStickyStatus(playerId, round, out _)) chips += "  · STUCK";
            int pending = PendingEndTurnEffects(playerId, null);
            if (pending > 0) chips += $"  · {pending} TRAP{(pending == 1 ? string.Empty : "S")} WAITING";
            return chips;
        }

        // NEXT-1 (owner, 2026-10-23: the next-round effect popup): a Blind, Slow, Stuck or Overcharged effect on the turn
        // player shows again at their turn start, as a centre popup with OK. Event-time only.
        private void ShowNextRoundEffectsPopup(PlayerId player)
        {
            if (host.runtimeUi == null
                || host.trapFloorAbilityResolutionState == null
                || host.trapFloorTurnState == null
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || player.IsEmpty)
            {
                return;
            }

            int round = host.trapFloorTurnState.CurrentRound;
            List<string> lines = new List<string>(3);
            if (host.trapFloorAbilityResolutionState.TryGetBlindStatus(player, round, out _))
            {
                lines.Add("BLIND — no Controller Cards drawn this turn. Throw the d4: you may move only in the direction it "
                    + "shows (the lit tiles).");
            }

            if (host.trapFloorAbilityResolutionState.TryGetSlowStatus(player, round, out TrapFloorSlowStatusState slow)
                && slow.MovementModifier != 0)
            {
                int tiles = Math.Abs(slow.MovementModifier);
                string tileWord = tiles == 1 ? "tile" : "tiles";
                lines.Add(slow.MovementModifier > 0
                    ? $"OVERCHARGED — you may move {tiles} {tileWord} more this turn."
                    : $"SLOW — you move {tiles} {tileWord} less this turn.");
            }

            if (host.trapFloorAbilityResolutionState.TryGetStickyStatus(player, round, out _))
            {
                lines.Add("STUCK — your pawn cannot move this turn. You may still use an Ability, then End Turn.");
            }

            if (lines.Count == 0) return;
            Action close = () => host.runtimeUi?.CloseTabletopPopup();
            host.runtimeUi.ShowContextMenu(
                new Vector2((Screen.width * 0.5f) - 200f, Screen.height * 0.66f),
                $"{FormatPlayerName(player).ToUpperInvariant()} — THIS TURN",
                string.Join("\n", lines),
                new[] { new PopupActionOption("OK", true, close) },
                close,
                _ => close());
        }

        internal string CurrentTrapFloorPlayerStatesText()
        {
            if (host.trapFloorTurnState == null) return string.Empty;

            string text = "PLAYERS ";
            for (int i = 0; i < host.trapFloorTurnState.PlayerOrder.Count; i++)
            {
                PlayerId playerId = host.trapFloorTurnState.PlayerOrder[i];
                string state = host.trapFloorObjectiveState != null
                    && host.trapFloorObjectiveState.IsPlayerEscaped(playerId)
                        ? "ESCAPED"
                        : host.trapFloorTurnState.GetPlayerState(playerId)
                            == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                            ? "ELIMINATED"
                            : "ACTIVE";
                if (host.trapFloorAbilityResolutionState != null
                    && host.trapFloorAbilityResolutionState.TryGetBlindStatus(
                        playerId,
                        host.trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / BLIND";
                }
                if (host.trapFloorAbilityResolutionState != null
                    && host.trapFloorAbilityResolutionState.TryGetSlowStatus(
                        playerId,
                        host.trapFloorTurnState.CurrentRound,
                        out TrapFloorSlowStatusState movementStatus))
                {
                    state += movementStatus.MovementModifier > 0 ? " / OVERCHARGED" : " / SLOW";
                }
                if (host.trapFloorAbilityResolutionState != null
                    && host.trapFloorAbilityResolutionState.TryGetStickyStatus(
                        playerId,
                        host.trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / STICKY";
                }
                if (i > 0) text += " | ";
                text += $"{FormatPlayerName(playerId)} {state}";
            }
            return text;
        }

        internal string CurrentTrapFloorCollapseStatusText()
        {
            if (host.trapFloorCollapseState == null)
            {
                return string.Empty;
            }

            TrapFloorCollapseRollState roll = host.trapFloorCollapseState.LastRoll;
            if (host.trapFloorCollapseState.IsBoardExhausted)
            {
                return $"USABLE FLOORS 0 / {host.trapFloorCollapseState.TotalFloorCount} | BOARD EXHAUSTED";
            }

            if (host.trapFloorCollapseState.IsCollapsePending)
            {
                return roll != null && roll.RequiredReroll
                    ? roll.FloorCardId.IsEmpty
                        ? $"FLOORFALL {roll.XAxisResult}/{roll.YAxisResult}: BEYOND THE FLOOR — ROLL AGAIN"
                        : $"FLOORFALL {roll.XAxisResult}/{roll.YAxisResult}: ALREADY A HOLE — ROLL AGAIN"
                    : "FLOORFALL: ROLL THE 2 FLOOR DICE";
            }

            string usable = $"USABLE FLOORS {host.trapFloorCollapseState.UsableFloorCount}"
                + $" / {host.trapFloorCollapseState.TotalFloorCount}";
            string collapse = roll == null
                ? usable
                : $"{usable} | LAST {roll.XAxisResult}/{roll.YAxisResult} → {roll.Coordinate}";
            if (host.trapFloorTemplate == null) return collapse;
            return $"{host.trapFloorTemplate.ActiveMode.DisplayName} · {WinModeName(WinMode)} game · "
                + $"{FormatCollapseSchedule(host.trapFloorTemplate.ActiveMode.Collapse.ScheduleKind)}\n{collapse}";
        }

        internal static string FormatCollapseSchedule(CollapseScheduleKind schedule)
        {
            switch (schedule)
            {
                case CollapseScheduleKind.RoundBased:
                    return "floor collapses each round";
                case CollapseScheduleKind.RealTime:
                    return "floor collapses in real time";
                default:
                    return "no collapse";
            }
        }

        internal void ApplyCollapsedFloorPresentation(TabletopObjectId floorCardId)
        {
            if (!host.matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
                || !host.CardInspectPanel.TryGetCardVisualReferences(floorCardId, out CardVisualReferences visualReferences))
            {
                return;
            }

            string label = host.labelsByCardId.TryGetValue(floorCardId, out string configuredLabel)
                ? configuredLabel
                : "FLOOR";
            host.Spawner.ConfigureCardVisuals(visualReferences, card, label);
            // Owner, 2026-10-21: a lifted collapsed tile keeps its own face and back, so its content shows as usual.
            visualReferences.SetCardContentVisible(
                IsHoleMarker(floorCardId) || host.ShouldShowCardContent(card));
            visualReferences.CardView.ApplyAcceptedState();
            if (host.inspectedCardId == floorCardId)
            {
                host.CardInspectPanel.CloseCardInspect();
            }
        }

        internal void RefreshTrapFloorFloorCardPresentation(TabletopObjectId floorCardId)
        {
            if (!host.matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
                || !host.CardInspectPanel.TryGetCardVisualReferences(floorCardId, out CardVisualReferences visualReferences))
            {
                return;
            }

            string label = host.labelsByCardId.TryGetValue(floorCardId, out string configuredLabel)
                ? configuredLabel
                : "FLOOR";
            host.Spawner.ConfigureCardVisuals(visualReferences, card, label);
            visualReferences.SetCardContentVisible(host.ShouldShowCardContent(card));
        }

        internal int CurrentFloorCardCount()
        {
            if (host.trapFloorTemplate == null || host.matchState == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TabletopObjectId objectId in host.trapFloorTemplate.FloorCardIds.Values)
            {
                if (host.matchState.Cards.ContainsKey(objectId))
                {
                    count++;
                }
            }

            return count;
        }

        internal int ContainerCount(ContainerId containerId)
        {
            return host.matchState != null && host.matchState.Containers.TryGetValue(containerId, out ContainerState container)
                ? container.Count
                : 0;
        }

        internal CardView FindCardView(TabletopObjectId objectId)
        {
            for (int i = 0; i < host.Registry.cardViews.Count; i++)
            {
                CardView candidate = host.Registry.cardViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == objectId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Authoritative Card has no bound Presentation View.");
        }

        internal string FormatPlayerName(PlayerId playerId) => host.FormatPlayerName(playerId); // CART-1b: the table's seat names

        internal string FormatPlayerShortName(PlayerId playerId)
        {
            if (host.trapFloorTemplate != null && host.matchState != null)
            {
                for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = host.trapFloorTemplate.Players[i];
                    if (host.matchState.GetSeat(player.SeatId).OccupantPlayerId == playerId)
                    {
                        return $"P{player.LayoutSeatIndex + 1}";
                    }
                }
            }

            return "Player";
        }

        internal int AvailableDrawableCount(ContainerId sourceDeckContainerId) => host.AvailableDrawableCount(sourceDeckContainerId);

        internal int AvailableControllerDeckCount(ContainerId sourceDeckContainerId) => host.AvailableAssistedDeckCount(sourceDeckContainerId);

        internal TrapFloorPlayerSetupDefinition GetAssistedTrapFloorPlayerSetup()
        {
            if (host.trapFloorTemplate == null
                || host.trapFloorTurnState == null
                || host.trapFloorTurnState.IsCurrentFloorFailed
                || host.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                throw new InvalidOperationException(
                    "Controller assistance requires an active Trap Floor Player turn.");
            }

            if (TryGetTrapFloorPlayerSetup(
                    host.trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition player))
                return player;

            throw new InvalidOperationException("The active Player has no authored Controller Hand setup.");
        }

        internal bool TryGetTrapFloorPlayerSetup(
            PlayerId playerId,
            out TrapFloorPlayerSetupDefinition resolvedPlayer)
        {
            if (host.trapFloorTemplate != null && host.matchState != null && !playerId.IsEmpty)
            {
                for (int i = 0; i < host.trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = host.trapFloorTemplate.Players[i];
                    if (host.matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                        && seat.OccupantPlayerId == playerId)
                    {
                        resolvedPlayer = player;
                        return true;
                    }
                }
            }

            resolvedPlayer = null;
            return false;
        }
        // ---------- from TabletopComposition.cs ----------

        internal PendingPurchaseSource pendingPurchaseSource;
        // G2a landing flips: the tile each pawn stood on when last checked, and whether an accepted move is
        // waiting to be checked. Checked once per accepted move, never per frame.
        internal readonly Dictionary<TabletopObjectId, TrapFloorCoordinate?> landingPawnTiles =
            new Dictionary<TabletopObjectId, TrapFloorCoordinate?>();
        // Spring/Crane target the active player still has to move to (G2c); applied by End Turn if left.
        internal PlayerId pendingLaunchPlayerId;
        internal TabletopObjectId pendingLaunchTargetId;

        // ---------- the cartridge contract (CART-1a, doc 30 §A4) ----------

        public string StableId => host.trapFloorGameDefinition != null ? host.trapFloorGameDefinition.StableId ?? string.Empty : "trap-floor";
        public string DisplayName => host.trapFloorGameDefinition != null ? host.trapFloorGameDefinition.DisplayName : "Trap Floor";
        public GameDefinition Game => host.trapFloorGameDefinition;

        // SETUP-1 / SEAT-1: the game's Table Setup asset as the factory's data; null (the built-in places) without one.
        internal TrapFloorTableSetup TableSetup()
        {
            TrapFloorSeatPiecesDefinition definition = Game != null ? Game.TableSetup : null;
            if (definition == null) return null;
            try
            {
                TrapFloorSeatPiecesLayout seatPieces = new TrapFloorSeatPiecesLayout(
                    definition.StartingAbilitiesSide,
                    definition.StartingAbilitiesTowardBoard,
                    definition.StartingAbilitiesOutward,
                    definition.StartingAbilitiesGap,
                    definition.StartingAbilitiesTurn,
                    definition.HandZoneTowardBoard,
                    definition.HandZoneOutward,
                    definition.ActionStackTowardBoard,
                    definition.ActionStackOutward);
                return new TrapFloorTableSetup(
                    definition.BoardMatSize.x,
                    definition.BoardMatSize.y,
                    SetupPoint(definition.BoardCentre),
                    new[]
                    {
                        SetupPose(definition.Player1Console),
                        SetupPose(definition.Player2Console),
                        SetupPose(definition.Player3Console),
                        SetupPose(definition.Player4Console),
                    },
                    SetupPose(definition.ControllerDeck),
                    SetupPose(definition.AbilityDeck),
                    SetupPose(definition.AbilityDiscard),
                    SetupPoint(definition.XAxisDie),
                    SetupPoint(definition.YAxisDie),
                    SetupPoint(definition.DirectionDie),
                    seatPieces);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[Layout] The table setup '{definition.name}' could not be read ({exception.Message}); the built-in places are used.");
                return null;
            }
        }

        private static TableCoordinate SetupPoint(Vector2 point) => new TableCoordinate(point.x, point.y);

        private static TabletopPose SetupPose(TrapFloorTablePlace place) =>
            place != null
                ? new TabletopPose(SetupPoint(place.Position), place.Turn, 0, 0)
                : throw new ArgumentException("A table setup place is missing.");
        public bool HasCode => true;

        // One table setup per mode, from the rule set in play and your cards. With a deck of yours chosen (L2-5) that
        // cannot set up the game, the game's own deck is used instead: a choice never stops play.
        public void RegisterTemplates(List<GameTemplateRegistration> registrations)
        {
            if (registrations == null) throw new ArgumentNullException(nameof(registrations));
            List<GameTemplateRegistration> mine;
            try
            {
                mine = BuildTemplates(true);
            }
            catch (Exception exception) when (!string.IsNullOrEmpty(host.GameSelectPanel.ChosenDeckIdOf(Game)))
            {
                string message = $"Your deck cannot set up Trap Floor ({exception.Message}); the game's own deck is used.";
                Debug.LogWarning("[Your decks] " + message, host);
                host.ShowMessage(message);
                mine = BuildTemplates(false);
            }

            registrations.AddRange(mine);
        }

        // (Moved from TableSessionService.RegisterTrapFloorTemplates.)
        private List<GameTemplateRegistration> BuildTemplates(bool useChosenDeck)
        {
            if (host.trapFloorGameDefinition == null)
            {
                throw new InvalidOperationException("TabletopComposition requires trapFloorGameDefinition.");
            }

            if (host.authoritativeRandomValueSource == null)
            {
                host.authoritativeRandomValueSource = new SystemRandomValueSource();
            }

            // Rules drive play (doc 23, R2a): the rule set in play (the default or agreed house rules, R3) feeds the
            // game data the template uses.
            host.RulesCardPanel.EnsureHouseRulesLoaded();
            RuleSetDefinition rules = host.RulesCardPanel.ActiveRuleSet;
            // Your cards (doc 27 L2-1) come first: changed default cards and new cards; house-rule costs win on top.
            GameContent content = BuildTrapFloorContent(useChosenDeck);
            GameDefinitionData gameDefinition;
            try
            {
                gameDefinition = content.ToData(rules);
            }
            catch
            {
                content.Destroy();
                throw;
            }

            GameContent previousContent = host.tableContent;
            host.tableContent = content;
            previousContent?.Destroy();
            if (gameDefinition.Modes.Count == 0)
                throw new InvalidOperationException("Trap Floor requires at least one authored Mode.");

            host.availableTrapFloorTemplates.Clear();
            List<GameTemplateRegistration> registrations = new List<GameTemplateRegistration>(gameDefinition.Modes.Count);
            // FLOORS-1: while the next floor is being dealt, every mode's setup is that smaller floor.
            int floorIndex = Math.Min(
                Math.Max(0, host.trapFloorNextFloorIndex),
                TrapFloorTemplateFactory.FloorCountFor(gameDefinition.Grid) - 1);
            // LAYOUT-1: dealt with the players' table layout (the one being arranged, else the saved one).
            TrapFloorTableLayout tableLayout = LayoutForDeal();
            // SETUP-1: the game's default table setup (its Table Setup asset), else the built-in places.
            TrapFloorTableSetup tableSetup = TableSetup();
            for (int i = 0; i < gameDefinition.Modes.Count; i++)
            {
                TrapFloorTemplateDefinition template;
                try
                {
                    template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                        host.authoritativeRandomValueSource,
                        gameDefinition,
                        gameDefinition.Modes[i].StableId,
                        host.GetConsoleLayout(),
                        floorIndex,
                        tableLayout,
                        DealPlayerCount,
                        tableSetup);
                }
                catch (Exception exception) when (tableLayout != null || tableSetup != null)
                {
                    // A layout that no longer fits never stops a deal: the default places are used.
                    Debug.LogWarning($"[Layout] The table layout or the table setup could not be used ({exception.Message}); the built-in places are used.");
                    host.ShowMessage(tableLayout != null
                        ? "The saved table layout could not be used here, so the default layout is used."
                        : "The table setup does not fit, so the built-in places are used.");
                    tableLayout = null;
                    tableSetup = null;
                    template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                        host.authoritativeRandomValueSource,
                        gameDefinition,
                        gameDefinition.Modes[i].StableId,
                        host.GetConsoleLayout(),
                        floorIndex,
                        null,
                        DealPlayerCount);
                }

                host.availableTrapFloorTemplates.Add(template.Template.Id, template);
                registrations.Add(new GameTemplateRegistration(template.Template, template.ContentCatalog));
            }

            host.templatesRuleSet = rules;
            return registrations;
        }

        public void OnTableReady()
        {
            RefreshNeutralizedTrapPresentation();
            RefreshMovementAssistancePresentation();
        }

        public void TickEarly()
        {
            UpdateTurnOrderRule(); // IMP-1
            CompletePendingLandingFlip();
            DiscardUsedAbility();
            DealArrangedBoardIfPlaced();
        }

        public void TickLate()
        {
            host.RefreshBoardCardBacksIfRoundChanged(); // the floor's backs change with the round
            CompletePhysicalFloorfallIfSettled();
            ArmFloorCollapseForFloorTurn(); // COLLAPSE-3
            ArmBlindDirectionForTurn(); // DICE-2
            CompletePhysicalFloorCollapseIfSettled();
            CompletePhysicalBlindDirectionIfSettled();
        }

        public string StatusText() => CurrentStatusText();

        int IGameCartridge.StatusUiSignature() => StatusUiSignature();

        public void RenderStatusUi() => RenderTrapFloorStatusUi();

        public void RefreshStatusUi() => RefreshTrapFloorStatusUi();

        void IGameCartridge.RefreshShopEntry() => RefreshShopEntry();

        public void AddHintHelpers() => AddTrapFloorHintHelpers();

        public void OnConsoleCardInteraction(IConsoleCardInteraction interaction, ConsoleCardBehavior behavior) =>
            HandleTrapFloorConsoleCardInteractionAccepted(interaction, behavior);

        public bool TryShowContextMenu(ContextMenuMode mode)
        {
            switch (mode)
            {
                case ContextMenuMode.BoardPiece:
                    ShowFloorCardContextMenu();
                    return true;
                case ContextMenuMode.GamePendingCard:
                    ShowPendingFloormasterCardContextMenu();
                    return true;
                default:
                    return false;
            }
        }

        bool IGameCartridge.IsAssistedControllerDeck(ContainerId containerId) => IsAssistedControllerDeck(containerId);
    }
}
