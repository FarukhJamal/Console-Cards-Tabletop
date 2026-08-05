using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Presentation.Interaction
{
    public interface IContainedCardDragFeedback
    {
        void Begin(ContainerId sourceContainerId);

        void Update(ContainerId sourceContainerId, CardDropTarget target, bool targetWouldAccept);

        void ShowRejected(ContainerId sourceContainerId, CardDropTarget target);

        void Clear();
    }
}
