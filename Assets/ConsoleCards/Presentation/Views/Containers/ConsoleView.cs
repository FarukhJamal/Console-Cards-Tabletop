using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Identifiers;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    public sealed class ConsoleView : MonoBehaviour
    {
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private float slotSpacing = 1.25f;
        [SerializeField] private Transform[] slotAnchors = Array.Empty<Transform>();

        private readonly List<ConsoleSlotView> slotViews = new List<ConsoleSlotView>();
        private ConsoleState consoleState;
        private bool isBound;

        public bool IsBound => isBound;

        public ConsoleState ConsoleState => isBound ? consoleState : null;

        public Transform LayoutAnchor => layoutAnchor;

        public IReadOnlyList<Transform> SlotAnchors => slotAnchors;

        public int VisibleSlotCount => isBound ? slotViews.Count : 0;

        public float SlotSpacing
        {
            get => slotSpacing;
            set
            {
                ContainerViewBinding.ValidateFiniteNonNegative(value, nameof(value));
                slotSpacing = value;
            }
        }

        public void Bind(
            ConsoleState console,
            Transform anchor,
            IReadOnlyList<ConsoleSlotView> orderedSlotViews)
        {
            ValidateBinding(console, anchor, orderedSlotViews);

            consoleState = console;
            layoutAnchor = anchor;
            slotViews.Clear();
            slotViews.AddRange(orderedSlotViews);
            isBound = true;
            ApplyAcceptedLayout();
        }

        public void ApplyAcceptedLayout()
        {
            EnsureBound();

            float center = (slotViews.Count - 1) * 0.5f;
            transform.SetPositionAndRotation(layoutAnchor.position, layoutAnchor.rotation);
            for (int i = 0; i < slotViews.Count; i++)
            {
                ConsoleSlotView slotView = slotViews[i];
                if (slotAnchors.Length > 0)
                {
                    slotView.LayoutAnchor.SetPositionAndRotation(slotAnchors[i].position, slotAnchors[i].rotation);
                }
                else
                {
                    float centeredIndex = i - center;
                    slotView.LayoutAnchor.SetPositionAndRotation(
                        layoutAnchor.position + (layoutAnchor.right * centeredIndex * slotSpacing),
                        layoutAnchor.rotation);
                }

                slotView.ApplyAcceptedLayout();
            }
        }

        public void Unbind()
        {
            consoleState = null;
            layoutAnchor = null;
            slotViews.Clear();
            isBound = false;
        }

        private void ValidateBinding(
            ConsoleState console,
            Transform anchor,
            IReadOnlyList<ConsoleSlotView> orderedSlotViews)
        {
            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            ContainerViewBinding.ValidateAnchor(anchor);
            ContainerViewBinding.ValidateFiniteNonNegative(slotSpacing, nameof(slotSpacing));
            if (orderedSlotViews == null)
            {
                throw new ArgumentNullException(nameof(orderedSlotViews));
            }

            if (orderedSlotViews.Count != console.SlotCount)
            {
                throw new ArgumentException("Console Slot View count must match Console slot count.", nameof(orderedSlotViews));
            }

            ValidateAuthoredSlotAnchors(orderedSlotViews.Count);

            HashSet<ConsoleSlotView> seenViews = new HashSet<ConsoleSlotView>();
            HashSet<ContainerId> seenContainerIds = new HashSet<ContainerId>();
            for (int i = 0; i < orderedSlotViews.Count; i++)
            {
                ConsoleSlotView slotView = orderedSlotViews[i];
                if (slotView == null)
                {
                    throw new ArgumentException("Console Slot View collection cannot contain null entries.", nameof(orderedSlotViews));
                }

                if (!seenViews.Add(slotView))
                {
                    throw new ArgumentException("Console Slot View collection cannot contain duplicate Views.", nameof(orderedSlotViews));
                }

                if (!slotView.IsBound || slotView.LayoutAnchor == null)
                {
                    throw new ArgumentException("Every Console Slot View must be bound before ConsoleView binding.", nameof(orderedSlotViews));
                }

                ContainerId expectedContainerId = console.SlotContainerIds[i];
                if (!seenContainerIds.Add(expectedContainerId))
                {
                    throw new ArgumentException("Console slot IDs cannot contain duplicates.", nameof(console));
                }

                if (slotView.ContainerId != expectedContainerId)
                {
                    throw new ArgumentException("Console Slot View order must match ConsoleState slot order.", nameof(orderedSlotViews));
                }
            }
        }

        private void ValidateAuthoredSlotAnchors(int expectedCount)
        {
            if (slotAnchors == null || slotAnchors.Length == 0)
            {
                return;
            }

            if (slotAnchors.Length != expectedCount)
            {
                throw new InvalidOperationException("ConsoleView authored Slot anchor count must match Console slot count.");
            }

            HashSet<Transform> seenAnchors = new HashSet<Transform>();
            for (int i = 0; i < slotAnchors.Length; i++)
            {
                Transform slotAnchor = slotAnchors[i];
                if (slotAnchor == null)
                {
                    throw new InvalidOperationException("ConsoleView authored Slot anchors cannot contain null entries.");
                }

                if (slotAnchor != transform && !slotAnchor.IsChildOf(transform))
                {
                    throw new InvalidOperationException("ConsoleView authored Slot anchors must belong to the Console hierarchy.");
                }

                if (!seenAnchors.Add(slotAnchor))
                {
                    throw new InvalidOperationException("ConsoleView authored Slot anchors must be distinct.");
                }
            }
        }

        private void EnsureBound()
        {
            if (!isBound)
            {
                throw new InvalidOperationException("ConsoleView is not bound.");
            }
        }
    }
}
