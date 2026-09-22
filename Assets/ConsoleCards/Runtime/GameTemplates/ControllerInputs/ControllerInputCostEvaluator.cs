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
    /// Selects an exact, identity-based payment from one authoritative Hand. Requirements are
    /// unordered and repeated entries are aggregated before any state is changed.
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

            Dictionary<ControllerInput, int> remaining = AggregateRequirements(cost);
            List<TabletopObjectId> selected = new List<TabletopObjectId>();
            for (int index = 0; index < playerHand.Count && remaining.Count > 0; index++)
            {
                TabletopObjectId objectId = playerHand.GetObjectAt(index);
                if (!matchState.Cards.TryGetValue(objectId, out CardInstanceState card))
                {
                    return ControllerInputCostEvaluation.Failure(
                        matchState.ContainsObject(objectId)
                            ? ControllerInputCostEvaluationError.HandObjectNotCard
                            : ControllerInputCostEvaluationError.HandObjectMissing);
                }

                if (!catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input)
                    || !remaining.TryGetValue(input, out int requiredCount))
                {
                    continue;
                }

                selected.Add(objectId);
                if (requiredCount == 1)
                {
                    remaining.Remove(input);
                }
                else
                {
                    remaining[input] = requiredCount - 1;
                }
            }

            return remaining.Count == 0
                ? ControllerInputCostEvaluation.Affordable(selected)
                : ControllerInputCostEvaluation.Failure(ControllerInputCostEvaluationError.InsufficientInput);
        }

        private static Dictionary<ControllerInput, int> AggregateRequirements(InputCostData cost)
        {
            Dictionary<ControllerInput, int> requirements = new Dictionary<ControllerInput, int>();
            for (int i = 0; i < cost.Requirements.Count; i++)
            {
                InputRequirementData requirement = cost.Requirements[i];
                requirements.TryGetValue(requirement.Input, out int current);
                requirements[requirement.Input] = checked(current + requirement.Count);
            }

            return requirements;
        }
    }
}
