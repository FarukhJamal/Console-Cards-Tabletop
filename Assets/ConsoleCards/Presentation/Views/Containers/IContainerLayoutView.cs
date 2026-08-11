using System.Collections.Generic;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Views.Containers
{
    public interface IContainerLayoutView : IContainerView
    {
        void SetCardViews(IReadOnlyList<CardView> cardViews);

        void ApplyAcceptedLayout();
    }
}
