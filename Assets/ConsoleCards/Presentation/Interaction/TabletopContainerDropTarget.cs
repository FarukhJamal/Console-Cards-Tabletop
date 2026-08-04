using System;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopContainerDropTarget : MonoBehaviour
    {
        private IContainerView containerView;
        private Collider targetCollider;
        private ContainerId containerId;
        private bool isConfigured;

        public bool IsConfigured => isConfigured;

        public IContainerView ContainerView => isConfigured ? containerView : null;

        public ContainerId ContainerId => isConfigured ? containerId : ContainerId.Empty;

        public Collider TargetCollider => isConfigured ? targetCollider : null;

        public void Configure(IContainerView boundContainerView, Collider collider)
        {
            ValidateConfiguration(boundContainerView, collider, out Component viewComponent);

            containerView = boundContainerView;
            targetCollider = collider;
            containerId = boundContainerView.ContainerId;
            isConfigured = true;
        }

        public void ClearConfiguration()
        {
            containerView = null;
            targetCollider = null;
            containerId = ContainerId.Empty;
            isConfigured = false;
        }

        private void ValidateConfiguration(
            IContainerView boundContainerView,
            Collider collider,
            out Component viewComponent)
        {
            if (boundContainerView == null)
            {
                throw new ArgumentNullException(nameof(boundContainerView));
            }

            viewComponent = boundContainerView as Component;
            if (viewComponent == null)
            {
                throw new ArgumentException("Container View must be a Unity Component.", nameof(boundContainerView));
            }

            if (!boundContainerView.IsBound)
            {
                throw new ArgumentException("Container View must be bound.", nameof(boundContainerView));
            }

            if (boundContainerView.ContainerId.IsEmpty)
            {
                throw new ArgumentException("Container View ID cannot be empty.", nameof(boundContainerView));
            }

            if (boundContainerView.ContainerState == null)
            {
                throw new ArgumentException("Container View must expose Container Runtime State.", nameof(boundContainerView));
            }

            if (boundContainerView.ContainerState.Id != boundContainerView.ContainerId)
            {
                throw new ArgumentException("Container View ID must match its Container Runtime State.", nameof(boundContainerView));
            }

            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            if (collider.transform != transform && !collider.transform.IsChildOf(transform))
            {
                throw new ArgumentException("Target Collider must belong to this drop target hierarchy.", nameof(collider));
            }
        }
    }
}
