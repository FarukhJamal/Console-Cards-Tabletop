using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    internal readonly struct CardLayoutPlan
    {
        public CardLayoutPlan(CardView cardView, TabletopPose pose, float additionalWorldHeight)
        {
            CardView = cardView;
            Pose = pose;
            AdditionalWorldHeight = additionalWorldHeight;
        }

        public CardView CardView { get; }

        public TabletopPose Pose { get; }

        public float AdditionalWorldHeight { get; }
    }

    internal static class ContainerViewBinding
    {
        public static void ValidateContainer(
            ContainerState container,
            ContainerKind expectedKind)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (container.Id.IsEmpty)
            {
                throw new ArgumentException("Container ID cannot be empty.", nameof(container));
            }

            if (container.Kind != expectedKind)
            {
                throw new ArgumentException($"Container kind must be {expectedKind}.", nameof(container));
            }
        }

        public static void ValidatePlacement(
            ContainerState container,
            ContainerPlacementState placement)
        {
            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            if (placement.ContainerId.IsEmpty)
            {
                throw new ArgumentException("Container placement ID cannot be empty.", nameof(placement));
            }

            if (placement.ContainerId != container.Id)
            {
                throw new ArgumentException("Container placement ID must match the Container ID.", nameof(placement));
            }

            ValidateFinitePose(placement.Pose, nameof(placement));
        }

        public static void ValidateConverter(TabletopCoordinateConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }
        }

        public static void ValidateAnchor(Transform layoutAnchor)
        {
            if (layoutAnchor == null)
            {
                throw new ArgumentNullException(nameof(layoutAnchor));
            }

            ValidateFinite(layoutAnchor.position, nameof(layoutAnchor));
            ValidateFinite(layoutAnchor.rotation.eulerAngles, nameof(layoutAnchor));
        }

        public static void ValidateFiniteNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        public static Dictionary<TabletopObjectId, CardView> BuildLookup(IReadOnlyList<CardView> cardViews)
        {
            if (cardViews == null)
            {
                throw new ArgumentNullException(nameof(cardViews));
            }

            Dictionary<TabletopObjectId, CardView> lookup = new Dictionary<TabletopObjectId, CardView>();
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView cardView = cardViews[i];
                if (cardView == null)
                {
                    throw new ArgumentException("CardView collection cannot contain null entries.", nameof(cardViews));
                }

                if (!cardView.IsBound || cardView.CardState == null)
                {
                    throw new ArgumentException("Every CardView must be bound to Card Runtime State.", nameof(cardViews));
                }

                if (lookup.ContainsKey(cardView.ObjectId))
                {
                    throw new ArgumentException("CardView collection cannot contain duplicate object IDs.", nameof(cardViews));
                }

                lookup.Add(cardView.ObjectId, cardView);
            }

            return lookup;
        }

        public static List<CardView> ResolveOrderedCards(
            ContainerState container,
            Dictionary<TabletopObjectId, CardView> lookup)
        {
            List<CardView> resolvedCards = new List<CardView>(container.Count);
            for (int i = 0; i < container.ObjectIds.Count; i++)
            {
                TabletopObjectId objectId = container.ObjectIds[i];
                if (!lookup.TryGetValue(objectId, out CardView cardView))
                {
                    throw new KeyNotFoundException("Container member does not have a supplied CardView.");
                }

                if (cardView.CardState.BaseState.ContainerId != container.Id)
                {
                    throw new ArgumentException("CardView CardState ContainerId must match the bound Container.");
                }

                resolvedCards.Add(cardView);
            }

            return resolvedCards;
        }

        public static void ApplyPlan(
            IReadOnlyList<CardLayoutPlan> plan,
            List<CardView> previouslyAppliedCards,
            ContainerId containerId)
        {
            for (int i = 0; i < previouslyAppliedCards.Count; i++)
            {
                CardView previousCard = previouslyAppliedCards[i];
                if (previousCard != null
                    && previousCard.IsContainerLayoutApplied
                    && previousCard.CardState != null
                    && previousCard.CardState.BaseState.ContainerId != containerId)
                {
                    previousCard.ClearContainerLayout();
                }
            }

            previouslyAppliedCards.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                CardLayoutPlan item = plan[i];
                item.CardView.ApplyContainerLayoutPose(item.Pose, item.AdditionalWorldHeight);
                previouslyAppliedCards.Add(item.CardView);
            }
        }

        public static TabletopPose CreatePose(
            TableCoordinate position,
            float rotationDegrees,
            TabletopPose sourcePose)
        {
            return new TabletopPose(position, rotationDegrees, sourcePose.Layer, sourcePose.LocalOrder);
        }

        public static TabletopPose PoseFromWorld(
            TabletopCoordinateConverter converter,
            Vector3 worldPosition,
            float rotationDegrees,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(
                converter.ToTableCoordinate(worldPosition),
                rotationDegrees,
                layer,
                localOrder);
        }

        public static void ClearAppliedCards(List<CardView> appliedCards)
        {
            for (int i = 0; i < appliedCards.Count; i++)
            {
                if (appliedCards[i] != null)
                {
                    appliedCards[i].ClearContainerLayout();
                }
            }

            appliedCards.Clear();
        }

        public static void ValidateFinitePose(TabletopPose pose, string parameterName)
        {
            if (!IsFinite(pose.Position.X)
                || !IsFinite(pose.Position.Y)
                || !IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateFinite(Vector3 value, string parameterName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
