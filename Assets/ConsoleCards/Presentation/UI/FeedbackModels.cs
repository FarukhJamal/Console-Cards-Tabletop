using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>How a feedback message reads (doc 25): colour of its row in the message stack.</summary>
    public enum FeedbackSeverity
    {
        Info = 0,
        Warning = 1,
        Loss = 2,
        Gain = 3,
    }

    /// <summary>The kind of moment a banner announces; it picks the banner's sound.</summary>
    public enum FeedbackEventKind
    {
        General = 0,
        TurnStart = 1,
        Trap = 2,
        Eliminated = 3,
        Reward = 4,
        Collapse = 5,
        RoundStart = 6,
    }

    /// <summary>One announcement for the feedback banner. Games build these from their own records.</summary>
    public readonly struct FeedbackBannerModel
    {
        public FeedbackBannerModel(FeedbackEventKind kind, string title, string subtitle, Color accent)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Accent = accent;
        }

        public FeedbackEventKind Kind { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public Color Accent { get; }
    }
}
