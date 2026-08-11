using System;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Marks a physical Container area as a Token transfer destination.</summary>
    public sealed class TabletopTokenContainerDropTarget : MonoBehaviour
    {
        private TokenContainerView containerView;
        private Collider targetCollider;

        public bool IsConfigured => containerView != null && targetCollider != null;

        public TokenContainerView ContainerView => containerView;

        public ContainerId ContainerId => IsConfigured ? containerView.ContainerId : ContainerId.Empty;

        public Collider TargetCollider => targetCollider;

        public void Configure(TokenContainerView view, Collider collider)
        {
            if (view == null || !view.IsBound)
            {
                throw new ArgumentException("Token Container View must be bound.", nameof(view));
            }

            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            if (collider.transform != transform && !collider.transform.IsChildOf(transform))
            {
                throw new ArgumentException("Target Collider must belong to this hierarchy.", nameof(collider));
            }

            containerView = view;
            targetCollider = collider;
        }

        public void ClearConfiguration()
        {
            containerView = null;
            targetCollider = null;
        }
    }
}
