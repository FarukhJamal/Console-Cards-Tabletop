using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "Network disconnect & unwinnable floor"). Pure C#, no scene, no network transport:
    /// the joined computer's table guard and the command door it freezes; the no-escape rule's three outcomes and the
    /// floor-level decision; and one terminal outcome when every player is out.
    /// </summary>
    public sealed class ConnectionAndNoEscapeTests
    {
        private sealed class Probe
        {
        }

        // A joined computer's door that this guard freezes. Probe has no handler: what matters is whether the door
        // refused it because the table is stale (RefusedWhileFrozen) or let it through to the local rules.
        private static ReplicatedTableAuthority GuestDoor(TableLinkGuard guard) =>
            new ReplicatedTableAuthority(new LocalTableAuthority(), () => null, null, null)
            {
                Mode = TableReplicationMode.Client,
                IsLocalPlayer = _ => true,
                Frozen = () => guard.IsStale,
            };

        private static void Try(ReplicatedTableAuthority door)
        {
            try
            {
                door.Submit<Probe, object>(new Probe());
            }
            catch (Exception)
            {
                // a command no handler takes may throw once it reaches the local rules: it was not refused as stale
            }
        }

        private static TableLinkGuard LiveGuest(long revision = 5)
        {
            TableLinkGuard guard = new TableLinkGuard();
            guard.Joined();
            Assert.IsFalse(guard.AcceptsLocalInput, "joined: frozen until the host's table is here");
            Assert.IsTrue(guard.HostTableArrived(revision));
            Assert.IsTrue(guard.AcceptsLocalInput);
            return guard;
        }

        // ---------- 1-4: the connection ----------

        [Test]
        public void GuestDisconnects_ItsTableFreezes_WhileTheHostsDoorPlaysOn()
        {
            TableLinkGuard guard = LiveGuest();
            ReplicatedTableAuthority guest = GuestDoor(guard);
            ReplicatedTableAuthority hostDoor = new ReplicatedTableAuthority(new LocalTableAuthority(), () => null, null, null)
            {
                Mode = TableReplicationMode.Host,
                IsLocalPlayer = _ => true,
            };

            Try(guest);
            Assert.AreEqual(0, guest.RefusedWhileFrozen, "live: played normally");

            guard.ConnectionLost();
            Assert.AreEqual(TableLinkPhase.Reconnecting, guard.Phase);
            Assert.IsFalse(guard.AcceptsLocalInput);
            Try(guest);
            Try(guest);
            Assert.AreEqual(2, guest.RefusedWhileFrozen, "a stale table takes no move made on it");

            Try(hostDoor);
            Assert.AreEqual(0, hostDoor.RefusedWhileFrozen, "the host is never frozen by a guest's lost connection");
        }

        [Test]
        public void Rejoin_PlaysOnlyAfterANewHostTable_NeverOnAQueuedOldOne()
        {
            TableLinkGuard guard = LiveGuest(5);
            ReplicatedTableAuthority guest = GuestDoor(guard);
            guard.ConnectionLost();

            Assert.IsFalse(guard.HostTableArrived(6), "a table that arrives while lost is not trusted");
            Assert.IsTrue(guard.IsStale);
            guard.BackInRoom();
            Assert.AreEqual(TableLinkPhase.Resyncing, guard.Phase);
            Assert.IsTrue(guard.IsStale, "back in the room is not enough");
            Try(guest);
            Assert.AreEqual(1, guest.RefusedWhileFrozen);

            Assert.IsTrue(guard.HostTableArrived(42), "the host's latest table after getting back in");
            Assert.AreEqual(42, guard.LastHostTableRevision);
            Assert.AreEqual(TableLinkPhase.Live, guard.Phase);
            Try(guest);
            Assert.AreEqual(1, guest.RefusedWhileFrozen, "play resumes on the host's table");
            Assert.IsFalse(guard.HostTableArrived(43), "later tables keep it live (no second resume)");
        }

        [Test]
        public void FailedReconnect_NeverLeavesAPlayableStaleTable()
        {
            TableLinkGuard guard = LiveGuest();
            guard.ConnectionLost();
            guard.SessionEnded(MultiplayerLeaveReason.ConnectionFailed);
            Assert.AreEqual(TableLinkPhase.Ended, guard.Phase);
            Assert.AreEqual(MultiplayerLeaveReason.ConnectionFailed, guard.EndReason);
            Assert.IsTrue(guard.IsStale);

            Assert.IsFalse(guard.HostTableArrived(50), "an ended session is not revived by a late table");
            guard.BackInRoom();
            guard.ConnectionLost();
            Assert.AreEqual(TableLinkPhase.Ended, guard.Phase, "stays ended until the table is replaced");

            guard.Reset(); // a new game or the main menu's new table
            Assert.IsTrue(guard.AcceptsLocalInput);
            Assert.AreEqual(TableLinkPhase.NotJoined, guard.Phase);
        }

        [Test]
        public void HostLoss_EndsTheGuestSession_AndSaysTheHostLeft()
        {
            TableLinkGuard guard = LiveGuest();
            guard.ConnectionLost();
            guard.SessionEnded(MultiplayerLeaveReason.HostClosedRoom);
            Assert.AreEqual(TableLinkPhase.Ended, guard.Phase);
            Assert.AreEqual(MultiplayerLeaveReason.HostClosedRoom, guard.EndReason, "confirmed host departure, not a network failure");
            Assert.IsFalse(guard.AcceptsLocalInput);

            TableLinkGuard other = LiveGuest();
            other.SessionEnded(MultiplayerLeaveReason.None);
            Assert.AreEqual(MultiplayerLeaveReason.ConnectionFailed, other.EndReason, "an unknown end reads as a network failure");

            TableLinkGuard offline = new TableLinkGuard();
            offline.SessionEnded(MultiplayerLeaveReason.HostClosedRoom);
            Assert.AreEqual(TableLinkPhase.NotJoined, offline.Phase, "a computer that never joined a table has nothing to end");
        }

        // ---------- 5-7: no escape ----------

        // A 3×3 floor: the pawn at (1,1) walled in by holes at (2,1) and (1,2); the Grand Exit at (3,3), intact.
        private static readonly HashSet<TrapFloorCoordinate> Walls = new HashSet<TrapFloorCoordinate>
        {
            new TrapFloorCoordinate(2, 1), new TrapFloorCoordinate(1, 2),
        };

        private static readonly TrapFloorCoordinate Exit = new TrapFloorCoordinate(3, 3);

        private static TrapFloorEscapeOutlook Isolated(
            bool heldCard = false, bool supply = false, bool faceDownUnderPawn = false) =>
            TrapFloorEscapeOutlook.Evaluate(3, 3, new TrapFloorCoordinate(1, 1), Walls.Contains, c => c == Exit,
                c => faceDownUnderPawn && c == new TrapFloorCoordinate(1, 1), heldCard, null, supply,
                heldCard ? "Blue holds one in the Hand" : null, supply ? "3 card(s) left in the Ability deck" : null);

        [Test]
        public void IsolatedPlayer_WithNothingLeft_IsProvablyNoEscape_AndTheFloorEnds()
        {
            TrapFloorEscapeOutlook outlook = Isolated();
            Assert.AreEqual(TrapFloorEscapeVerdict.Impossible, outlook.Verdict, outlook.Reason);
            Assert.IsTrue(TrapFloorNoEscapeRule.PutsEveryoneOut(new[] { outlook }), "the only player left: the floor is lost");
            Assert.AreEqual(TrapFloorGrandExitInvariant.Holds, TrapFloorGrandExitCheck.Check(new[] { Exit }, Walls.Contains),
                "an intact, unreachable Grand Exit is a legitimate loss, not a defect");
            Assert.AreEqual(TrapFloorGrandExitInvariant.Collapsed,
                TrapFloorGrandExitCheck.Check(new[] { new TrapFloorCoordinate(2, 1) }, Walls.Contains),
                "a collapsed Grand Exit is a software defect");
        }

        [Test]
        public void OneTrappedPlayer_WhileAnotherCanEscape_PlaysOn()
        {
            TrapFloorEscapeOutlook trapped = Isolated();
            TrapFloorEscapeOutlook free = TrapFloorEscapeOutlook.Evaluate(3, 3, new TrapFloorCoordinate(3, 2), Walls.Contains,
                c => c == Exit, _ => false, false);
            Assert.AreEqual(TrapFloorEscapeVerdict.Impossible, trapped.Verdict);
            Assert.AreEqual(TrapFloorEscapeVerdict.Possible, free.Verdict);
            Assert.IsFalse(TrapFloorNoEscapeRule.PutsEveryoneOut(new[] { trapped, free }), "no GAME OVER while someone can still escape");
            Assert.IsFalse(TrapFloorNoEscapeRule.PutsEveryoneOut(new TrapFloorEscapeOutlook[0]), "nobody on the floor is not a no-escape loss");
        }

        [Test]
        public void ASpecialMoveOrResourceLeft_IsUndetermined_NeverAPrematureGameOver()
        {
            foreach (TrapFloorEscapeOutlook outlook in new[]
                     {
                         Isolated(heldCard: true),
                         Isolated(supply: true),
                         Isolated(faceDownUnderPawn: true),
                     })
            {
                Assert.AreEqual(TrapFloorEscapeVerdict.Undetermined, outlook.Verdict, outlook.Reason);
                Assert.IsFalse(TrapFloorNoEscapeRule.PutsEveryoneOut(new[] { outlook }), outlook.Reason);
            }

            StringAssert.Contains("Blue holds one in the Hand", Isolated(heldCard: true).Reason, "the diagnostic says why");
            StringAssert.Contains("Ability deck", Isolated(supply: true).Reason);
        }

        // ---------- 8: everyone out ----------

        [Test]
        public void EveryActivePlayerOut_GivesExactlyOneTerminalOutcome()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, 11);
            TrapFloorAbilityResolutionState ability = new TrapFloorAbilityResolutionState(t.Match.Id);
            TrapFloorTurnService turns = new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed, ability);
            List<PlayerId> players = new List<PlayerId>(t.Players);
            for (int i = 0; i < players.Count; i++)
            {
                Assert.IsFalse(t.Turn.IsCurrentFloorFailed, "not over while someone is still in");
                PlayerId actor = t.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? t.Turn.FloorOperatorPlayerId : t.Turn.ActivePlayerId;
                Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(actor.IsEmpty ? players[i] : actor), players[i]).Succeeded,
                    $"player {i + 1} goes out");
            }

            Assert.IsTrue(t.Turn.IsCurrentFloorFailed, "everyone is out: the floor is lost");
            long revision = t.Match.Revision;
            Assert.IsFalse(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(players[0]), players[0]).Succeeded, "never twice");
            Assert.AreEqual(revision, t.Match.Revision, "no second terminal change");
            Assert.IsTrue(t.Turn.IsCurrentFloorFailed);
        }
    }
}
