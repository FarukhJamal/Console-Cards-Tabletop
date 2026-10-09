using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.GameTemplates.ControllerInputs
{
    public enum ControllerInputCostEvaluationError
    {
        None = 0,
        MatchMissing = 1,
        HandMissing = 2,
        CostMissing = 3,
        InvalidControllerCardDefinition = 4,
        HandObjectMissing = 5,
        HandObjectNotCard = 6,
        InsufficientInput = 7,
    }

    public sealed class ControllerInputCardCatalog
    {
        private readonly Dictionary<ObjectDefinitionId, ControllerInput> inputsByDefinitionId;

        private ControllerInputCardCatalog(Dictionary<ObjectDefinitionId, ControllerInput> inputsByDefinitionId)
        {
            this.inputsByDefinitionId = inputsByDefinitionId;
        }

        public static bool TryCreate(
            GameDefinitionData gameDefinition,
            out ControllerInputCardCatalog catalog)
        {
            catalog = null;
            if (gameDefinition == null)
            {
                return false;
            }

            Dictionary<ObjectDefinitionId, ControllerInput> mappings =
                new Dictionary<ObjectDefinitionId, ControllerInput>();
            for (int i = 0; i < gameDefinition.Cards.Count; i++)
            {
                CardDefinitionData card = gameDefinition.Cards[i];
                if (!card.RepresentedControllerInput.HasValue)
                {
                    continue;
                }

                if (!Guid.TryParse(card.StableId, out Guid stableId))
                {
                    return false;
                }

                ObjectDefinitionId definitionId = new ObjectDefinitionId(stableId);
                if (mappings.ContainsKey(definitionId))
                {
                    return false;
                }

                mappings.Add(definitionId, card.RepresentedControllerInput.Value);
            }

            catalog = new ControllerInputCardCatalog(mappings);
            return true;
        }

        public bool TryGetInput(ObjectDefinitionId definitionId, out ControllerInput input)
        {
            return inputsByDefinitionId.TryGetValue(definitionId, out input);
        }
    }

    public sealed class ControllerInputCostEvaluation
    {
        private static readonly ReadOnlyCollection<TabletopObjectId> EmptyPayment =
            new ReadOnlyCollection<TabletopObjectId>(new List<TabletopObjectId>());

        private ControllerInputCostEvaluation(
            bool canPay,
            ControllerInputCostEvaluationError error,
            IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            CanPay = canPay;
            Error = error;
            PaymentCardIds = paymentCardIds;
        }

        public bool CanPay { get; }
        public ControllerInputCostEvaluationError Error { get; }
        public IReadOnlyList<TabletopObjectId> PaymentCardIds { get; }

        public static ControllerInputCostEvaluation Affordable(IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            return new ControllerInputCostEvaluation(
                true,
                ControllerInputCostEvaluationError.None,
                new ReadOnlyCollection<TabletopObjectId>(
                    new List<TabletopObjectId>(paymentCardIds ?? throw new ArgumentNullException(nameof(paymentCardIds)))));
        }

        public static ControllerInputCostEvaluation Failure(ControllerInputCostEvaluationError error)
        {
            if (error == ControllerInputCostEvaluationError.None)
            {
                throw new ArgumentException("A failed cost evaluation requires an error.", nameof(error));
            }

            return new ControllerInputCostEvaluation(false, error, EmptyPayment);
        }
    }

    /// <summary>
    /// Selects an exact, identity-based payment from one authoritative Hand. Requirements are unordered; a
    /// requirement may accept any one of several inputs ("A/B/X/Y", C1), so cards are matched to requirement
    /// slots so that every slot is filled. Runs only when a cost is checked or paid, never per frame.
    /// </summary>
    public sealed class ControllerInputCostEvaluator
    {
        public ControllerInputCostEvaluation Evaluate(
            MatchState matchState,
            ContainerState playerHand,
            InputCostData cost,
            GameDefinitionData gameDefinition)
        {
            if (matchState == null)
            {
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.MatchMissing);
            }

            if (playerHand == null || playerHand.Kind != ContainerKind.Hand)
            {
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.HandMissing);
            }

            if (cost == null)
            {
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.CostMissing);
            }

            if (!ControllerInputCardCatalog.TryCreate(gameDefinition, out ControllerInputCardCatalog catalog))
            {
                return ControllerInputCostEvaluation.Failure(
                    ControllerInputCostEvaluationError.InvalidControllerCardDefinition);
            }

            List<TabletopObjectId> candidateIds = new List<TabletopObjectId>();
            List<ControllerInput> candidateInputs = new List<ControllerInput>();
            for (int index = 0; index < playerHand.Count; index++)
            {
                TabletopObjectId objectId = playerHand.GetObjectAt(index);
                if (!matchState.Cards.TryGetValue(objectId, out CardInstanceState card))
                {
                    return ControllerInputCostEvaluation.Failure(
                        matchState.ContainsObject(objectId)
                            ? ControllerInputCostEvaluationError.HandObjectNotCard
                            : ControllerInputCostEvaluationError.HandObjectMissing);
                }

                if (catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input) && cost.Accepts(input))
                {
                    candidateIds.Add(objectId);
                    candidateInputs.Add(input);
                }
            }

            List<InputRequirementData> slots = ExpandSlots(cost);
            if (!TryMatch(slots, candidateInputs, out int[] cardForSlot))
            {
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.InsufficientInput);
            }

            // The payment keeps the Hand's order.
            bool[] used = new bool[candidateIds.Count];
            for (int slot = 0; slot < cardForSlot.Length; slot++)
            {
                used[cardForSlot[slot]] = true;
            }

            List<TabletopObjectId> selected = new List<TabletopObjectId>(slots.Count);
            for (int i = 0; i < candidateIds.Count; i++)
            {
                if (used[i])
                {
                    selected.Add(candidateIds[i]);
                }
            }

            return ControllerInputCostEvaluation.Affordable(selected);
        }

        /// <summary>
        /// Validates that the supplied authoritative Card instances exactly satisfy an authored
        /// input cost. Their current Container is deliberately interpreted by the calling use case.
        /// </summary>
        public ControllerInputCostEvaluation EvaluateExact(
            MatchState matchState,
            IReadOnlyList<TabletopObjectId> selectedCardIds,
            InputCostData cost,
            GameDefinitionData gameDefinition)
        {
            if (matchState == null)
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.MatchMissing);
            if (selectedCardIds == null)
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.InsufficientInput);
            if (cost == null)
                return ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.CostMissing);
            if (!ControllerInputCardCatalog.TryCreate(gameDefinition, out ControllerInputCardCatalog catalog))
            {
                return ControllerInputCostEvaluation.Failure(
                    ControllerInputCostEvaluationError.InvalidControllerCardDefinition);
            }

            HashSet<TabletopObjectId> unique = new HashSet<TabletopObjectId>();
            List<ControllerInput> selectedInputs = new List<ControllerInput>(selectedCardIds.Count);
            for (int i = 0; i < selectedCardIds.Count; i++)
            {
                TabletopObjectId objectId = selectedCardIds[i];
                if (objectId.IsEmpty || !unique.Add(objectId))
                    return ControllerInputCostEvaluation.Failure(
                        ControllerInputCostEvaluationError.InsufficientInput);
                if (!matchState.Cards.TryGetValue(objectId, out CardInstanceState card))
                {
                    return ControllerInputCostEvaluation.Failure(
                        matchState.ContainsObject(objectId)
                            ? ControllerInputCostEvaluationError.HandObjectNotCard
                            : ControllerInputCostEvaluationError.HandObjectMissing);
                }
                if (!catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input)
                    || !cost.Accepts(input))
                {
                    return ControllerInputCostEvaluation.Failure(
                        ControllerInputCostEvaluationError.InsufficientInput);
                }

                selectedInputs.Add(input);
            }

            List<InputRequirementData> slots = ExpandSlots(cost);
            return selectedInputs.Count == slots.Count && TryMatch(slots, selectedInputs, out _)
                ? ControllerInputCostEvaluation.Affordable(new List<TabletopObjectId>(selectedCardIds))
                : ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.InsufficientInput);
        }

        // One slot per card the cost takes. Single-input slots come first so they claim their exact cards
        // before any choice slot does; the matching below still finds a payment whenever one exists.
        private static List<InputRequirementData> ExpandSlots(InputCostData cost)
        {
            List<InputRequirementData> slots = new List<InputRequirementData>(cost.CardCount);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < cost.Requirements.Count; i++)
                {
                    InputRequirementData requirement = cost.Requirements[i];
                    if (requirement.IsChoice != (pass == 1))
                    {
                        continue;
                    }

                    for (int copy = 0; copy < requirement.Count; copy++)
                    {
                        slots.Add(requirement);
                    }
                }
            }

            return slots;
        }

        // Assigns a distinct card to every slot (bipartite matching by augmenting paths). Costs and Hands are
        // a handful of cards, so this is cheap.
        private static bool TryMatch(
            IReadOnlyList<InputRequirementData> slots,
            IReadOnlyList<ControllerInput> cardInputs,
            out int[] cardForSlot)
        {
            cardForSlot = new int[slots.Count];
            if (slots.Count > cardInputs.Count)
            {
                return false;
            }

            int[] slotForCard = new int[cardInputs.Count];
            for (int i = 0; i < slotForCard.Length; i++)
            {
                slotForCard[i] = -1;
            }

            for (int slot = 0; slot < slots.Count; slot++)
            {
                bool[] visited = new bool[cardInputs.Count];
                if (!TryAssign(slot, slots, cardInputs, slotForCard, visited))
                {
                    return false;
                }
            }

            for (int card = 0; card < slotForCard.Length; card++)
            {
                if (slotForCard[card] >= 0)
                {
                    cardForSlot[slotForCard[card]] = card;
                }
            }

            return true;
        }

        private static bool TryAssign(
            int slot,
            IReadOnlyList<InputRequirementData> slots,
            IReadOnlyList<ControllerInput> cardInputs,
            int[] slotForCard,
            bool[] visited)
        {
            for (int card = 0; card < cardInputs.Count; card++)
            {
                if (visited[card] || !slots[slot].Accepts(cardInputs[card]))
                {
                    continue;
                }

                visited[card] = true;
                if (slotForCard[card] < 0 || TryAssign(slotForCard[card], slots, cardInputs, slotForCard, visited))
                {
                    slotForCard[card] = slot;
                    return true;
                }
            }

            return false;
        }
    }
}
