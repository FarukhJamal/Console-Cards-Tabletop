using ConsoleCards.Definitions;

namespace ConsoleCards.Presentation.Hints
{
    /// <summary>
    /// A game hint helper (doc 23 §10, layer C): a small piece of a game's code for hints a rule tag cannot
    /// express, such as which tiles a pawn can reach or why a move is not allowed. A rule names it in its
    /// hintHelper field; the hint service runs it only while a rule in play names it, and asks it about every
    /// table event. A helper explains and marks; it never blocks a move or changes game state.
    /// </summary>
    public interface IGameHintHelper
    {
        /// <summary>The name a rule's hintHelper field uses, for example "trapfloor.reach".</summary>
        string HelperId { get; }

        /// <summary>Once a frame while in play: show or hide the helper's board markers. Must not allocate.</summary>
        void Tick(HintLevel level);

        /// <summary>
        /// An event happened at the table (see <see cref="HintTriggers"/>). Returns the hint to show for it now,
        /// usually a reminder, or null. Called only while hints are on; may allocate (events, not frames).
        /// </summary>
        HintEntry Explain(string trigger, string context, HintLevel level);

        /// <summary>The helper is no longer in play (another game or rule set): hide anything it shows.</summary>
        void Stop();
    }
}
