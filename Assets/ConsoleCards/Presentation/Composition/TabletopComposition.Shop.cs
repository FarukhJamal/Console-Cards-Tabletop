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

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>The Trap Floor Shop and its payment step. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        // ---------- the Shop (doc 18 §15.4) ----------

        // Open while the rule in play says so (shop.enabled, a Toggle); read live, so a change applies at once.
        private bool IsShopOpen()
        {
            RuleSetDefinition rules = ActiveRuleSet;
            if (rules == null
                || trapFloorTemplate == null
                || activeSession == null
                || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return false;
            }

            string modeStableId = trapFloorTemplate.ActiveMode.StableId;
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
        private void RefreshShopEntry()
        {
            if (runtimeUi == null) return;
            runtimeUi.ShowShopEntry(IsShopOpen() ? (Action)OpenActionAbilityPurchase : null);
        }

        private bool IsShopBuyingTurn() =>
            trapFloorTurnState != null
            && !trapFloorTurnState.IsCurrentFloorFailed
            && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn;

        private void OpenActionAbilityPurchase()
        {
            CloseContextMenu();
            if (!IsShopOpen())
            {
                ShowMessage("The Shop is closed in the rules in play.");
                return;
            }

            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive)
            {
                ShowMessage("Finish or cancel the current payment first.");
                return;
            }

            ShowActionAbilityPurchase(IsShopBuyingTurn()
                ? $"{FormatPlayerName(trapFloorTurnState.ActivePlayerId)} is buying."
                : "You can look; buying is on a player's own turn.");
        }

        private void ShowActionAbilityPurchase(string statusMessage)
        {
            if (runtimeUi == null || trapFloorTemplate == null)
            {
                return;
            }

            runtimeUi.ShowActionAbilityPurchase(
                BuildActionAbilityPurchaseOptions(),
                TryPurchaseActionAbilityFromPopup,
                runtimeUi.CloseActionAbilityPurchase,
                statusMessage,
                "SHOP",
                "ABILITIES");
        }

        private void TryPurchaseActionAbilityFromPopup(string cardDefinitionStableId)
        {
            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive)
            {
                runtimeUi?.SetActionAbilityPurchaseStatus(
                    "Finish or cancel the current payment first.");
                return;
            }

            if (!IsShopBuyingTurn())
            {
                runtimeUi?.SetActionAbilityPurchaseStatus("Buying is on a player's own turn.");
                return;
            }

            if (!trapFloorTemplate.GameDefinition.TryGetCard(cardDefinitionStableId, out CardDefinitionData shopCard)
                || !TrapFloorShop.IsForSale(shopCard))
            {
                runtimeUi?.SetActionAbilityPurchaseStatus("Not for sale until the rules give it a price.");
                return;
            }

            if (!TryBuildPurchasePaymentOptions(
                    cardDefinitionStableId,
                    out CardDefinitionData purchasedDefinition,
                    out List<PurchasePaymentCardOption> options,
                    out string missing))
            {
                runtimeUi?.SetActionAbilityPurchaseStatus(
                    "This price could not be read.");
                return;
            }

            pendingPurchaseDefinitionStableId = cardDefinitionStableId;
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
            runtimeUi.ShowFocusedCardSelection(new FocusedCardSelectionModel(
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

        private bool TryBuildPurchasePaymentOptions(
            string cardDefinitionStableId,
            out CardDefinitionData purchasedDefinition,
            out List<PurchasePaymentCardOption> options,
            out string missingRequirements)
        {
            purchasedDefinition = null;
            options = new List<PurchasePaymentCardOption>();
            missingRequirements = string.Empty;
            if (trapFloorTemplate == null
                || !trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out purchasedDefinition)
                || purchasedDefinition.InputCost == null
                || purchasedDefinition.InputCost.IsEmpty
                || !ControllerInputCardCatalog.TryCreate(
                    trapFloorTemplate.GameDefinition,
                    out ControllerInputCardCatalog catalog))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand)
                || hand.Kind != ContainerKind.Hand)
                return false;

            InputCostData cost = purchasedDefinition.InputCost;
            for (int i = 0; i < hand.Count; i++)
            {
                TabletopObjectId cardId = hand.GetObjectAt(i);
                if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
                ControllerInput? input = catalog.TryGetInput(
                    card.BaseState.DefinitionId,
                    out ControllerInput resolvedInput)
                    ? resolvedInput
                    : (ControllerInput?)null;
                bool eligible = input.HasValue && cost.Accepts(input.Value);

                CardDefinition authoredDefinition = null;
                string displayName = TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out authoredDefinition)
                    ? authoredDefinition.DisplayName
                    : "Controller Card";
                options.Add(new PurchasePaymentCardOption(
                    cardId,
                    input,
                    displayName,
                    FrontArt(authoredDefinition),
                    eligible));
            }

            // With "any one of" costs there is no single missing input, so the whole cost is named.
            missingRequirements = new ControllerInputCostEvaluator().Evaluate(
                    matchState,
                    hand,
                    cost,
                    trapFloorTemplate.GameDefinition).CanPay
                ? string.Empty
                : $"your Hand cannot pay {InputCostNotation.Format(cost)}";
            return true;
        }

        private bool IsPurchasePaymentReady(IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (string.IsNullOrWhiteSpace(pendingPurchaseDefinitionStableId)
                || trapFloorTemplate == null
                || !trapFloorTemplate.GameDefinition.TryGetCard(
                    pendingPurchaseDefinitionStableId,
                    out CardDefinitionData purchasedDefinition))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand))
                return false;
            for (int i = 0; i < selectedCardIds.Count; i++)
                if (!hand.Contains(selectedCardIds[i])) return false;
            return new ControllerInputCostEvaluator().EvaluateExact(
                matchState,
                selectedCardIds,
                purchasedDefinition.InputCost,
                trapFloorTemplate.GameDefinition).CanPay;
        }

        private void ConfirmFocusedPurchasePayment(
            IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (!IsPurchasePaymentReady(selectedCardIds))
            {
                ShowMessage("Choose exactly the cards the cost asks for.");
                return;
            }

            string definitionId = pendingPurchaseDefinitionStableId;
            PlayerId playerId = trapFloorTurnState.ActivePlayerId;
            pendingControllerPurchaseState.Begin(playerId, definitionId, selectedCardIds, pendingPurchaseSource);
            pendingPurchaseDefinitionStableId = string.Empty;
            runtimeUi?.CloseFocusedCardSelection();
            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"{PaymentVerb(pendingControllerPurchaseState)} {FindPurchaseDisplayName(definitionId).ToUpperInvariant()} — "
                + "Place the chosen cards in your Console, then Confirm Payment");
        }

        private void CancelFocusedPurchaseSelection()
        {
            pendingPurchaseDefinitionStableId = string.Empty;
            runtimeUi?.CloseFocusedCardSelection();
            ShowMessage("Payment cancelled.");
        }

        private void ConfirmPendingActionAbilityPurchase()
        {
            if (pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive
                || !pendingControllerPurchaseState.IsPaymentComplete
                || trapFloorTurnState == null
                || pendingControllerPurchaseState.PlayerId != trapFloorTurnState.ActivePlayerId)
            {
                ShowMessage("Place the chosen cards in your Console first.");
                return;
            }

            if (pendingControllerPurchaseState.Source == PendingPurchaseSource.Shop)
            {
                CompleteShopPurchase(
                    pendingControllerPurchaseState.PurchasedCardDefinitionStableId,
                    pendingControllerPurchaseState.SelectedCardIds);
                RefreshTrapFloorStatusUi();
                return;
            }

            if (!TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prizeTile)
                || prizeTile.Content.AuthoredCard == null
                || !string.Equals(
                    prizeTile.Content.AuthoredCard.StableId,
                    pendingControllerPurchaseState.PurchasedCardDefinitionStableId,
                    StringComparison.OrdinalIgnoreCase))
            {
                ShowMessage("Stand on the Ability you are paying for, then confirm.");
                return;
            }

            CompleteTakeFloorPrize(prizeTile.ObjectId, pendingControllerPurchaseState.SelectedCardIds);
            RefreshTrapFloorStatusUi();
        }

        private void CancelPendingActionAbilityPurchase()
        {
            pendingPurchaseDefinitionStableId = string.Empty;
            runtimeUi?.CloseFocusedCardSelection();
            runtimeUi?.CloseActionAbilityPurchase();
            if (pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive)
                return;

            pendingControllerPurchaseState.Clear();
            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            ShowMessage(
                "Payment cancelled. Cards stay where you placed them.");
        }

        private string FindPurchaseDisplayName(string cardDefinitionStableId)
        {
            return trapFloorTemplate != null
                && trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out CardDefinitionData definition)
                ? definition.DisplayName
                : "ABILITY";
        }

        // The Shop's list (doc 18 §15.4): every Ability of the game, Actions first, then Reactions. One without a
        // price is listed but not for sale. Affordability is for the player whose turn it is.
        private List<ActionAbilityPurchaseOption> BuildActionAbilityPurchaseOptions()
        {
            List<ActionAbilityPurchaseOption> options =
                new List<ActionAbilityPurchaseOption>();
            GameDefinitionData gameDefinition = trapFloorTemplate.GameDefinition;
            ContainerState hand = null;
            if (IsShopBuyingTurn())
            {
                TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
                hand = matchState.GetContainer(player.HandContainerId);
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
                        matchState,
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
                    && TryGetAuthoredCardDefinition(
                        new ObjectDefinitionId(definitionGuid),
                        out CardDefinition authoredDefinition))
                {
                    artwork = FrontArt(authoredDefinition);
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

        private static string FormatPurchaseInputCost(InputCostData cost)
        {
            return InputCostNotation.Format(cost);
        }

        private static string PaymentVerb(PendingControllerPurchaseState state) =>
            state != null && state.Source == PendingPurchaseSource.Shop ? "BUY" : "TAKE";

        // Confirm Payment for a Shop card: the card goes to the Hand, or beside the Console when the Hand is full.
        private void CompleteShopPurchase(string cardDefinitionStableId, IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            if (trapFloorShopUseCase == null || trapFloorTurnState == null)
            {
                ShowMessage("The Shop is unavailable.");
                return;
            }

            TrapFloorBuyFromShopResult result = trapFloorShopUseCase.Execute(
                matchState,
                new TrapFloorBuyFromShopCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    cardDefinitionStableId,
                    TabletopObjectId.New(),
                    paymentCardIds));
            if (!result.Succeeded)
            {
                ShowMessage($"Shop purchase rejected: {result.Error}.");
                return;
            }

            pendingControllerPurchaseState.Clear();
            pendingPurchaseDefinitionStableId = string.Empty;
            ReplaceCurrentUndoStateForPurchaseAssistance();

            string name = result.Card.DisplayName;
            labelsByCardId[result.GrantedCardId] = name;
            CardView boughtView = CreateCardView(
                matchState.Cards[result.GrantedCardId],
                name,
                out TabletopSelectionVisual selectionVisual);
            cardViews.Add(boughtView);
            cardSelectionVisuals.Add(selectionVisual);
            physicalAuthority?.Register(boughtView);
            RefreshContainerCardViewSources();
            ApplyLayout(result.DestinationContainerId);
            RefreshSelectionPresenterAfterRuntimeProjection();
            presentationTransitions.Appear(boughtView.transform, settleDuration);
            if (TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition buyer))
            {
                string buyerName = HandOwnerName(buyer.HandContainerId);
                ShowMessage(result.PlacedInHand
                    ? $"Bought {name} from the Shop into {buyerName}'s hand."
                    : $"Bought {name} from the Shop. {buyerName}'s hand is full, so it is beside their Console.");
            }
        }
    }
}
