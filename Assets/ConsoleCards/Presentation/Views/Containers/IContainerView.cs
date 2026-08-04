using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Presentation.Views.Containers
{
    public interface IContainerView
    {
        bool IsBound { get; }

        ContainerId ContainerId { get; }

        ContainerState ContainerState { get; }
    }
}
