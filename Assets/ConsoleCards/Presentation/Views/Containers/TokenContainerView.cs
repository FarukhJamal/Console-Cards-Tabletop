using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    /// <summary>
    /// Projects an authoritative Token Container as a physical tabletop area.
    /// The Container owns membership; this View only arranges the member Token Views.
    /// </summary>
    public sealed class TokenContainerView : MonoBehaviour, IContainerView
    {
        private ContainerState containerState;
        private TabletopPose anchorPose;
        private IReadOnlyList<TokenView> tokenViews;
        private TextMesh countLabel;
        private string displayLabel;
        private int columnCount;
        private double columnSpacing;
        private double rowSpacing;

        public bool IsBound => containerState != null;

        public ContainerId ContainerId => IsBound ? containerState.Id : ContainerId.Empty;

        public ContainerState ContainerState => containerState;

        public void Configure(TextMesh label)
        {
            countLabel = label != null ? label : throw new ArgumentNullException(nameof(label));
        }

        public void Bind(
            ContainerState state,
            TabletopPose pose,
            TabletopCoordinateConverter converter,
            IReadOnlyList<TokenView> views,
            string label,
            int columns,
            double horizontalSpacing,
            double verticalSpacing)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (converter == null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            if (views == null)
            {
                throw new ArgumentNullException(nameof(views));
            }

            if (countLabel == null)
            {
                throw new InvalidOperationException("TokenContainerView must be configured with a count label before binding.");
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Token Container label cannot be empty.", nameof(label));
            }

            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            if (!IsFinite(horizontalSpacing) || horizontalSpacing <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSpacing));
            }

            if (!IsFinite(verticalSpacing) || verticalSpacing <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(verticalSpacing));
            }

            containerState = state;
            anchorPose = pose;
            tokenViews = views;
            displayLabel = label;
            columnCount = columns;
            columnSpacing = horizontalSpacing;
            rowSpacing = verticalSpacing;

            transform.SetPositionAndRotation(
                converter.ToWorldPosition(pose),
                converter.ToWorldRotation(pose));
            ApplyAcceptedLayout();
        }

        public void ApplyAcceptedLayout()
        {
            EnsureBound();

            for (int index = 0; index < containerState.ObjectIds.Count; index++)
            {
                TabletopObjectId tokenId = containerState.ObjectIds[index];
                TokenView tokenView = FindTokenView(tokenId);
                int row = index / columnCount;
                int column = index % columnCount;
                int rowItemCount = Math.Min(columnCount, containerState.Count - (row * columnCount));
                double localX = (column - ((rowItemCount - 1) * 0.5d)) * columnSpacing;
                double localY = (row - (((containerState.Count - 1) / columnCount) * 0.5d)) * rowSpacing;
                double radians = anchorPose.RotationDegrees * (Math.PI / 180d);
                double rotatedX = (localX * Math.Cos(radians)) + (localY * Math.Sin(radians));
                double rotatedY = (-localX * Math.Sin(radians)) + (localY * Math.Cos(radians));
                tokenView.ApplyContainerLayoutPose(
                    new TabletopPose(
                        new TableCoordinate(
                            anchorPose.Position.X + rotatedX,
                            anchorPose.Position.Y + rotatedY),
                        anchorPose.RotationDegrees,
                        1,
                        index),
                    0.035f);
            }

            countLabel.text = $"{displayLabel}\n{containerState.Count}";
        }

        public void Unbind()
        {
            if (containerState != null && tokenViews != null)
            {
                for (int i = 0; i < tokenViews.Count; i++)
                {
                    TokenView tokenView = tokenViews[i];
                    if (tokenView != null
                        && tokenView.IsBound
                        && tokenView.IsContainerLayoutApplied
                        && tokenView.BoundState.ContainerId == containerState.Id)
                    {
                        tokenView.ClearContainerLayoutAndReconcile();
                    }
                }
            }

            containerState = null;
            tokenViews = null;
            displayLabel = null;
            columnCount = 0;
            columnSpacing = 0d;
            rowSpacing = 0d;
            if (countLabel != null)
            {
                countLabel.text = string.Empty;
            }
        }

        private TokenView FindTokenView(TabletopObjectId tokenId)
        {
            for (int i = 0; i < tokenViews.Count; i++)
            {
                TokenView candidate = tokenViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == tokenId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Authoritative Token Container member has no bound Token View.");
        }

        private void EnsureBound()
        {
            if (!IsBound)
            {
                throw new InvalidOperationException("TokenContainerView is not bound.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
