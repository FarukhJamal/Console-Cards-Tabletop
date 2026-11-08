using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P7 (s162), owner 2026-11-08 "win and loss are equally important": losing and unwinnable floors, the Grand Exit
    /// invariant (a defect, not a loss), and the escape outlook. Unit; the turn and collapse services are the game's own.
    /// </summary>
    public sealed class FloorOutcomeTests
    {
        private static TrapFloorCoordinate C(int x, int y) => new TrapFloorCoordinate(x, y);

        private static TrapFloorEscapeOutlook Outlook(int size, TrapFloorCoordinate pawn, ISet<TrapFloorCoordinate> holes,
            ISet<TrapFloorCoordinate> exits, ISet<TrapFloorCoordinate> faceDown = null, bool moveCard = false) =>
            TrapFloorEscapeOutlook.Evaluate(size, size, pawn, holes.Contains, exits.Contains,
                (faceDown ?? new HashSet<TrapFloorCoordinate>()).Contains, moveCard);

        // ---------- Escape outlook (reachability) ----------

        [Test]
        public void GrandExitIsolatedByHoles_AllFaceUp_NoMoveCards_IsNoEscape()
        {
            // 4×4, Grand Exit at (4,4) ringed by holes (3,4) and (4,3); the pawn at (1,1).
            var holes = new HashSet<TrapFloorCoordinate> { C(3, 4), C(4, 3) };
            var exits = new HashSet<TrapFloorCoordinate> { C(4, 4) };
            Assert.AreEqual(TrapFloorEscapeVerdict.NoEscape, Outlook(4, C(1, 1), holes, exits).Verdict);
        }

        [Test]
        public void AtLeastOneWalkingRoute_CanReachExit()
        {
            var holes = new HashSet<TrapFloorCoordinate> { C(3, 4) };
            var exits = new HashSet<TrapFloorCoordinate> { C(4, 4) };
            Assert.AreEqual(TrapFloorEscapeVerdict.CanReachExit, Outlook(4, C(1, 1), holes, exits).Verdict, "around by (4,3)");
        }

        [Test]
        public void CutOff_ButAFaceDownTileInReach_IsUndetermined_NotALoss()
        {
            var holes = new HashSet<TrapFloorCoordinate> { C(3, 4), C(4, 3) };
            var exits = new HashSet<TrapFloorCoordinate> { C(4, 4) };
            var faceDown = new HashSet<TrapFloorCoordinate> { C(2, 2) }; // may hold a Spring, Crane, Ability or Friend
            TrapFloorEscapeOutlook o = Outlook(4, C(1, 1), holes, exits, faceDown);
            Assert.AreEqual(TrapFloorEscapeVerdict.Undetermined, o.Verdict);
            StringAssert.Contains("face-down", o.Reason);
        }

        [Test]
        public void CutOff_ButAMoveCardHeld_IsUndetermined_TheAbilityRouteCounts()
        {
            var holes = new HashSet<TrapFloorCoordinate> { C(3, 4), C(4, 3) };
            var exits = new HashSet<TrapFloorCoordinate> { C(4, 4) };
            TrapFloorEscapeOutlook o = Outlook(4, C(1, 1), holes, exits, null, moveCard: true);
            Assert.AreEqual(TrapFloorEscapeVerdict.Undetermined, o.Verdict, "Rush or a Friend may still cross the holes");
        }

        [Test]
        public void SecretExitCollapsed_GrandExitStillReachable_And_NoSecretExitAtAll_BothStillWinnable()
        {
            var exitsAfterSecretLost = new HashSet<TrapFloorCoordinate> { C(5, 5) }; // the Secret Exit's hole is not an exit
            var holes = new HashSet<TrapFloorCoordinate> { C(1, 5) }; // where the Secret Exit was
            Assert.AreEqual(TrapFloorEscapeVerdict.CanReachExit, Outlook(5, C(1, 1), holes, exitsAfterSecretLost).Verdict);
            Assert.AreEqual(TrapFloorEscapeVerdict.CanReachExit,
                Outlook(4, C(1, 1), new HashSet<TrapFloorCoordinate>(), new HashSet<TrapFloorCoordinate> { C(4, 4) }).Verdict,
                "a 4×4 floor without a Secret Exit is winnable by the Grand Exit");
        }

        [Test]
        public void PawnOnAHole_IsUndetermined_NotAVerdictOnAFallenPlayer()
        {
            var holes = new HashSet<TrapFloorCoordinate> { C(1, 1) };
            Assert.AreEqual(TrapFloorEscapeVerdict.Undetermined,
                Outlook(4, C(1, 1), holes, new HashSet<TrapFloorCoordinate> { C(4, 4) }).Verdict);
        }

        // ---------- Grand Exit invariant (a defect, never a loss) ----------

        [Test]
        public void GrandExitInvariant_HoldsMissingDuplicatedCollapsed()
        {
            var none = new HashSet<TrapFloorCoordinate>();
            Assert.AreEqual(TrapFloorGrandExitInvariant.Holds, TrapFloorGrandExitCheck.Check(new[] { C(4, 4) }, none.Contains));
            Assert.AreEqual(TrapFloorGrandExitInvariant.Missing, TrapFloorGrandExitCheck.Check(new TrapFloorCoordinate[0], none.Contains));
            Assert.AreEqual(TrapFloorGrandExitInvariant.Duplicated, TrapFloorGrandExitCheck.Check(new[] { C(1, 1), C(4, 4) }, none.Contains));
            var hole = new HashSet<TrapFloorCoordinate> { C(4, 4) };
            Assert.AreEqual(TrapFloorGrandExitInvariant.Collapsed, TrapFloorGrandExitCheck.Check(new[] { C(4, 4) }, hole.Contains));
        }

        [Test]
        public void GrandExitInvariant_OnRealDeals_Holds_AndAnUnprotectedCollapseIsCaught()
        {
            for (int floor = 0; floor < 3; floor++)
            {
                // Intentionally corrupted set-up: the collapse use case with the protection switched off, which is how
                // the Grand Exit was lost before the 2026-11-08 rule (D-S5). The check must call it a defect.
                GrandExitProtectionTests.Floorfall f = GrandExitProtectionTests.Floorfall.Create(floor, protects: false);
                List<TrapFloorCoordinate> grand = new List<TrapFloorCoordinate>();
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in f.T.Template.FloorCardIds)
                {
                    Assert.IsTrue(f.T.Template.TryGetFloorCardState(f.T.Match, pair.Value, out TrapFloorFloorCardState s));
                    if (s.Content.Category == TrapFloorFloorContentCategory.SecretExit && TrapFloorObjectiveUseCase.IsGrandExit(s.Content))
                        grand.Add(pair.Key);
                }

                bool IsHole(TrapFloorCoordinate at) =>
                    f.T.Template.TryGetFloorCardId(at, out TabletopObjectId id) && f.State.IsCollapsed(id);
                Assert.AreEqual(TrapFloorGrandExitInvariant.Holds, TrapFloorGrandExitCheck.Check(grand, IsHole), $"floor {floor + 1} as dealt");
                f.Begin();
                Assert.IsTrue(f.Roll(grand[0]).Succeeded);
                Assert.AreEqual(TrapFloorGrandExitInvariant.Collapsed, TrapFloorGrandExitCheck.Check(grand, IsHole),
                    "a collapsed Grand Exit is reported as a defect");
            }
        }

        // ---------- Losing the floor through the game's own turn service ----------

        [Test]
        public void AllPlayersEliminated_TheFloorFails_NoTurnIsGiven_NoOneIsPutOutTwice()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, 11);
            var ability = new TrapFloorAbilityResolutionState(t.Match.Id);
            var turns = new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed, ability);
            PlayerId first = t.Turn.ActivePlayerId;

            // One out: the turn skips them; the floor goes on.
            Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(first), first).Succeeded);
            Assert.IsFalse(t.Turn.IsCurrentFloorFailed);
            Assert.AreNotEqual(first, t.Turn.ActivePlayerId, "an eliminated player is never given the turn");

            foreach (PlayerId p in t.Players)
            {
                if (t.Turn.GetPlayerState(p) == TrapFloorCurrentFloorPlayerState.Active)
                    Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(p), p).Succeeded);
            }

            Assert.IsTrue(t.Turn.IsCurrentFloorFailed, "every player out: the floor is lost");
            Assert.IsTrue(t.Turn.ActivePlayerId.IsEmpty, "no turn is given");
            // End Turn on a lost floor is refused one level up, where the commands arrive: the cartridge shows the
            // floor-end panel instead (TrapFloorCartridge End Turn: IsCurrentFloorFailed → ShowFloorEnded) and the host
            // refuses a guest's End Turn unless a player turn is running (OnlinePlayPresenter.Private turn check). The
            // turn service itself keeps its older "new round brings everyone back" step, which nothing sends now.
            Assert.IsFalse(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(first), first).Succeeded,
                "no player is put out twice");
        }
    }
}
