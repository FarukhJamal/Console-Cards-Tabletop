using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views.Containers;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class ContainerLayoutViewLookup
    {
        private readonly Dictionary<ContainerId, IContainerLayoutView> viewsByContainerId;

        public ContainerLayoutViewLookup(IReadOnlyList<IContainerLayoutView> layoutViews)
        {
            if (layoutViews == null)
            {
                throw new ArgumentNullException(nameof(layoutViews));
            }

            viewsByContainerId = new Dictionary<ContainerId, IContainerLayoutView>();
            for (int i = 0; i < layoutViews.Count; i++)
            {
                IContainerLayoutView view = layoutViews[i];
                if (view == null)
                {
                    throw new ArgumentException("Layout View collection cannot contain null entries.", nameof(layoutViews));
                }

                if (!view.IsBound)
                {
                    throw new ArgumentException("Every layout View must be bound.", nameof(layoutViews));
                }

                if (view.ContainerId.IsEmpty)
                {
                    throw new ArgumentException("Layout View Container ID cannot be empty.", nameof(layoutViews));
                }

                if (viewsByContainerId.ContainsKey(view.ContainerId))
                {
                    throw new ArgumentException("Layout View collection cannot contain duplicate Container IDs.", nameof(layoutViews));
                }

                viewsByContainerId.Add(view.ContainerId, view);
            }
        }

        public bool TryGet(ContainerId containerId, out IContainerLayoutView view)
        {
            if (containerId.IsEmpty)
            {
                view = null;
                return false;
            }

            return viewsByContainerId.TryGetValue(containerId, out view);
        }
    }
}
