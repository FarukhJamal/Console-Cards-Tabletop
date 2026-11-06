using System;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Authoritative session data only; contains no Unity object or Presentation reference. CART-1b: the game's own
    /// part (its rules state) is the cartridge's <see cref="ICartridgeUndoState"/>, which the table never reads. The
    /// drawing tool: the table's drawings at that moment (<see cref="TableMarkSet"/>, never changed once made).
    /// </summary>
    internal sealed class SessionUndoSnapshot
    {
        private SessionUndoSnapshot(
            GameTemplateInitialSnapshot match,
            ICartridgeUndoState gameState,
            PendingControllerPurchaseStateSnapshot pendingControllerPurchase,
            TableMarkSet tableMarks)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            GameState = gameState;
            PendingControllerPurchase = pendingControllerPurchase;
            TableMarks = tableMarks ?? TableMarkSet.Empty;
        }

        public GameTemplateInitialSnapshot Match { get; }
        public ICartridgeUndoState GameState { get; }
        public PendingControllerPurchaseStateSnapshot PendingControllerPurchase { get; }
        public TableMarkSet TableMarks { get; }

        /// <summary>M1a (2026-11-05): the same snapshot without the payment in progress, for sending to other computers.</summary>
        /// <summary>PRIVACY-SCENE (s146): a recipient's table alone (no game rules state, no payment, no marks).</summary>
        internal static SessionUndoSnapshot ForRecipientView(GameTemplateInitialSnapshot match) =>
            new SessionUndoSnapshot(match, null, null, null);

        public SessionUndoSnapshot WithoutPayment() =>
            PendingControllerPurchase == null ? this : new SessionUndoSnapshot(Match, GameState, null, TableMarks);

        public static SessionUndoSnapshot Capture(
            MatchState match,
            ICartridgeUndoState gameState,
            PendingControllerPurchaseState pendingControllerPurchase,
            TableMarkSet tableMarks)
        {
            PendingControllerPurchaseStateSnapshot purchase = pendingControllerPurchase == null
                ? null
                : PendingControllerPurchaseStateSnapshot.Capture(pendingControllerPurchase);
            return new SessionUndoSnapshot(
                GameTemplateInitialSnapshot.Capture(match),
                gameState,
                purchase,
                tableMarks);
        }
    }
}
