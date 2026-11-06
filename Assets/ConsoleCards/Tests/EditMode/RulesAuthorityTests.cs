using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// 2026-11-06, host rules authority: AUTH-RULES (what of a pushed rules state the host takes), UNDO-AUTH (host-checked
    /// Undo), AUTH-MOVE (Full move checks on the host) and AUTH-POS. Forged states are built the way a modified client
    /// could send them. Pure; no network.
    /// </summary>
    public sealed class RulesAuthorityTests
    {
        // ---------- a rules state for a test table ----------

        private sealed class Rules
        {
            public TrapFloorTestTable Table;
            public TrapFloorActivityFeedState Feed;
            public TrapFloorObjectiveState Objective;
            public TrapFloorCollapseState Collapse;
            public TrapFloorTurnState Turn;
            public TrapFloorAbilityResolutionState Ability;

            // Same seed and ids: the host's and the joined computer's copy of one game.
            public static Rules Create(int players = 3)
            {
                TrapFloorTestTable t = TrapFloorTestTable.Create(players);
                return new Rules
                {
                    Table = t,
                    Feed = t.Feed,
                    Turn = t.Turn,
                    Objective = new TrapFloorObjectiveState(t.Match.Id, 1),
                    Collapse = new TrapFloorCollapseState(t.Match.Id, 9),
                    Ability = new TrapFloorAbilityResolutionState(t.Match.Id),
                };
            }

            public PlayerId P(int i) => Table.Players[i];

            public TrapFloorSessionStateSnapshot Snap() =>
                TrapFloorSessionStateSnapshot.Capture(Feed, Objective, Collapse, Turn, Ability);

            public TabletopObjectId FloorCard(int x, int y)
            {
                Assert.IsTrue(Table.Template.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id));
                return id;
            }
        }

        // AUTH-RULES-2 (2026-11-06): the host takes nothing of a pushed rules state; it reports what the push tried.
        private static TrapFloorSessionState Merge(Rules host, Rules pushed, PlayerId sender, List<string> refused) =>
            TrapFloorRulesPushGuard.Merge(host.Snap(), pushed.Snap(), sender, refused).Restore();

        private static bool IsOut(TrapFloorSessionState state, PlayerId player) =>
            state.Turn.GetPlayerState(player) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound;

        // ---------- AUTH-RULES: forged ----------

        [Test]
        public void ForgedElimination_OfAnotherPlayer_IsRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            guest.Turn.MarkPlayerEliminatedForCurrentRound(guest.P(2)); // the sender (seat 1) knocks out seat 3
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.IsFalse(IsOut(merged, host.P(2)), "the host's table shows no reason for it");
            Assert.IsTrue(refused.Exists(r => r.Contains("elimination")), string.Join("; ", refused));
        }

        [Test]
        public void ForgedReturn_OfAnEliminatedPlayer_IsRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            host.Turn.MarkPlayerEliminatedForCurrentRound(host.P(1));
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused); // the guest's copy has seat 2 still in
            Assert.IsTrue(IsOut(merged, host.P(1)));
            Assert.IsTrue(refused.Exists(r => r.Contains("back in")));
        }

        [Test]
        public void ForgedKeys_AreRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            guest.Objective.Restore(
                new[] { new TrapFloorCollectedKeyState(guest.FloorCard(2, 2), default, guest.P(1), 5) },
                Array.Empty<PlayerId>(), false, PlayerId.Empty, TabletopObjectId.Empty);
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(0, merged.Objective.CollectedKeyCount);
            Assert.IsTrue(refused.Exists(r => r.StartsWith("Keys")));
        }

        [Test]
        public void ForgedEscape_AndFloorWin_AreRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            guest.Objective.Restore(Array.Empty<TrapFloorCollectedKeyState>(), new[] { guest.P(1) }, true, guest.P(1), guest.FloorCard(3, 3));
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.IsFalse(merged.Objective.IsPlayerEscaped(host.P(1)));
            Assert.IsFalse(merged.Objective.IsWon);
            Assert.IsTrue(refused.Exists(r => r.StartsWith("escapes")));
        }

        [Test]
        public void ForgedCollapse_IsRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            guest.Collapse.Restore(
                new[] { new TrapFloorCollapsedFloorState(guest.FloorCard(1, 1), new TrapFloorCoordinate(1, 1), guest.P(1), 1, 1, 4) },
                true, guest.P(1), null, null, null);
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(0, merged.Collapse.CollapsedFloorCount);
            Assert.IsFalse(merged.Collapse.IsCollapsePending);
            Assert.IsTrue(refused.Exists(r => r.StartsWith("collapse")));
        }

        [Test]
        public void ForgedTurnIndex_IsRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            guest.Turn.Advance(); // the sender moves the turn on by itself
            guest.Turn.Advance();
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(host.Turn.ActivePlayerId, merged.Turn.ActivePlayerId);
            Assert.AreEqual(host.Turn.CurrentRound, merged.Turn.CurrentRound);
            Assert.IsTrue(refused.Exists(r => r.StartsWith("turn")));
        }

        [Test]
        public void ForgedStatuses_AreRefused_TheSendersOwnToo()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            TabletopObjectId trap = guest.FloorCard(2, 1);
            guest.Ability.Restore(
                Array.Empty<TrapFloorTrapResolutionRecord>(),
                Array.Empty<TrapFloorAbilityActivationRecord>(),
                null,
                null,
                Array.Empty<TrapFloorBlindStatusState>(),
                new[] { new TrapFloorSlowStatusState(guest.P(1), 2, -1, trap) }, // the sender's own (taken)
                new[] { new TrapFloorStickyStatusState(guest.P(2), 2, false, trap) }); // another player's (refused)
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(0, merged.AbilityResolution.SlowStatuses.Count, "a status comes only from a Trap the host applied");
            Assert.AreEqual(0, merged.AbilityResolution.StickyStatuses.Count);
            Assert.IsTrue(refused.Exists(r => r.Contains("Slow")), string.Join("; ", refused));
            Assert.IsTrue(refused.Exists(r => r.Contains("Sticky")), string.Join("; ", refused));
        }

        [Test]
        public void StaleOwnStatus_IsNotBroughtBack()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            host.Turn = new TrapFloorTurnState(host.Table.Match.Id, host.Table.Players, 3, TrapFloorTurnPhase.PlayerTurn, 0, -1,
                Array.Empty<PlayerId>(), false, -1); // the host is in round 3
            guest.Turn = new TrapFloorTurnState(guest.Table.Match.Id, guest.Table.Players, 3, TrapFloorTurnPhase.PlayerTurn, 0, -1,
                Array.Empty<PlayerId>(), false, -1);
            guest.Ability.Restore(
                Array.Empty<TrapFloorTrapResolutionRecord>(),
                Array.Empty<TrapFloorAbilityActivationRecord>(),
                null,
                null,
                Array.Empty<TrapFloorBlindStatusState>(),
                new[] { new TrapFloorSlowStatusState(guest.P(1), 2, 1, guest.FloorCard(2, 1)) }, // round 2 has passed
                Array.Empty<TrapFloorStickyStatusState>());
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), new List<string>());
            Assert.AreEqual(0, merged.AbilityResolution.SlowStatuses.Count);
        }

        [Test]
        public void SelfElimination_ByPush_IsRefused_ItComesAsAHostCheckedCommand()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            PlayerId active = guest.Turn.ActivePlayerId;
            guest.Turn.MarkPlayerEliminatedForCurrentRound(active); // a modified client declares itself out
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, active, refused);
            Assert.IsFalse(IsOut(merged, active), "no concession in the rules: an elimination needs a reason on the host's table");
            Assert.AreEqual(host.Turn.ActivePlayerId, merged.Turn.ActivePlayerId);
            Assert.IsTrue(refused.Exists(r => r.Contains("elimination")), string.Join("; ", refused));
        }

        [Test]
        public void ForgedTrapRemoval_OfTheSendersOwnWaitingTrap_IsRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            TabletopObjectId tile = host.FloorCard(2, 1);
            Assert.IsTrue(host.Table.Template.TryGetFloorCardState(host.Table.Match, tile, out TrapFloorFloorCardState floor));
            if (floor.Content.Category != TrapFloorFloorContentCategory.Trap)
            {
                // The record needs Trap content: take any Trap tile of this deal.
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in host.Table.Template.FloorCardIds)
                {
                    if (host.Table.Template.TryGetFloorCardState(host.Table.Match, pair.Value, out floor)
                        && floor.Content.Category == TrapFloorFloorContentCategory.Trap)
                        break;
                }
            }

            Assert.AreEqual(TrapFloorFloorContentCategory.Trap, floor.Content.Category, "the test deal has no Trap tile");
            PlayerId me = host.P(0);
            host.Ability.RecordRevealedTrap(3, me, floor); // the host saw the reveal: it waits for the sender
            guest.Ability.RecordRevealedTrap(3, me, floor);
            guest.Ability.TrapRecords[0].SetDisposition(TrapFloorTrapResolutionDisposition.Neutralized); // cleared by hand
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, me, refused);
            Assert.IsTrue(merged.AbilityResolution.TrapRecords[0].IsPending, "a waiting Trap ends only by a host-run step");
            Assert.IsTrue(refused.Exists(r => r.StartsWith("Trap outcomes")), string.Join("; ", refused));
        }

        [Test]
        public void ForgedStatusRemoval_IsRefused_TheHostKeepsTheSlow()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            PlayerId me = host.P(0);
            host.Ability.Restore(
                Array.Empty<TrapFloorTrapResolutionRecord>(), Array.Empty<TrapFloorAbilityActivationRecord>(), null, null,
                Array.Empty<TrapFloorBlindStatusState>(),
                new[] { new TrapFloorSlowStatusState(me, 2, -1, host.FloorCard(2, 1)) },
                Array.Empty<TrapFloorStickyStatusState>());
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, me, refused); // the guest's copy dropped it
            Assert.AreEqual(1, merged.AbilityResolution.SlowStatuses.Count);
            Assert.IsTrue(refused.Exists(r => r.Contains("Slow")), string.Join("; ", refused));
        }

        [Test]
        public void ForgedAbilityUse_AndForgedGuidance_AreRefused()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            PlayerId me = host.P(0);
            guest.Ability.RecordAbilityActivation(new TabletopObjectId(Guid.NewGuid()), me, 1);
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, me, refused);
            Assert.AreEqual(0, merged.AbilityResolution.CopyAbilityActivations().Length);
            Assert.IsTrue(refused.Exists(r => r.Contains("Ability uses")), string.Join("; ", refused));
        }

        [Test]
        public void ForgedLogLines_AreNotTaken()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            guest.Feed.RecordPlayerEliminated(5, guest.P(1)); // a log line with no step behind it
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(host.Feed.Entries.Count, merged.Activity.Entries.Count, "the host's own log stands");
        }

        [Test]
        public void LegitimateKeysEscapesAndCollapse_RunOnTheHost_AreKept()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            // As after the host replayed Claim Key, Attempt Escape and Resolve Collapse: both computers have them.
            foreach (Rules r in new[] { host, guest })
            {
                r.Objective.Restore(
                    new[] { new TrapFloorCollectedKeyState(r.FloorCard(2, 2), default, r.P(0), 5) },
                    new[] { r.P(0) }, false, PlayerId.Empty, TabletopObjectId.Empty);
                r.Collapse.Restore(
                    new[] { new TrapFloorCollapsedFloorState(r.FloorCard(1, 1), new TrapFloorCoordinate(1, 1), r.P(0), 1, 1, 4) },
                    false, PlayerId.Empty, null, null, null);
            }

            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(1, merged.Objective.CollectedKeyCount);
            Assert.IsTrue(merged.Objective.IsPlayerEscaped(host.P(0)));
            Assert.AreEqual(1, merged.Collapse.CollapsedFloorCount);
            Assert.AreEqual(0, refused.Count, string.Join("; ", refused));
        }

        [Test]
        public void UnchangedPush_ChangesNothing()
        {
            Rules host = Rules.Create(), guest = Rules.Create();
            List<string> refused = new List<string>();
            TrapFloorSessionState merged = Merge(host, guest, guest.P(1), refused);
            Assert.AreEqual(0, refused.Count, string.Join("; ", refused));
            Assert.AreEqual(host.Turn.ActivePlayerId, merged.Turn.ActivePlayerId);
        }

        // ---------- UNDO-AUTH ----------

        private static readonly PlayerId Me = new PlayerId(Guid.NewGuid());
        private static readonly PlayerId Other = new PlayerId(Guid.NewGuid());

        [Test]
        public void Undo_OfMyLatestChange_IsAllowed_AndARepeatedRequestIsNot()
        {
            HostUndoPolicy policy = new HostUndoPolicy();
            CommandId change = CommandId.New();
            Guid request = Guid.NewGuid();
            Assert.AreEqual(HostUndoVerdict.Allowed, policy.Check(Me, request, change, true, change, Me));
            Assert.AreEqual(HostUndoVerdict.Duplicate, policy.Check(Me, request, change, true, change, Me));
        }

        [Test]
        public void Undo_OfAnotherPlayersChange_OrOfAGameStep_IsRefused()
        {
            HostUndoPolicy policy = new HostUndoPolicy();
            CommandId change = CommandId.New();
            Assert.AreEqual(HostUndoVerdict.NotYourChange, policy.Check(Me, Guid.NewGuid(), change, true, change, Other));
            Assert.AreEqual(HostUndoVerdict.NoPlayer, policy.Check(Me, Guid.NewGuid(), change, true, change, PlayerId.Empty));
            Assert.AreEqual(HostUndoVerdict.NotSeated, policy.Check(PlayerId.Empty, Guid.NewGuid(), change, true, change, Me));
            Assert.AreEqual(HostUndoVerdict.NothingToUndo, policy.Check(Me, Guid.NewGuid(), change, false, CommandId.Empty, PlayerId.Empty));
            Assert.AreEqual(HostUndoVerdict.MissingRequestId, policy.Check(Me, Guid.Empty, change, true, change, Me));
        }

        [Test]
        public void DoublePressedUndo_DoesNotTakeBackASecondChange()
        {
            HostUndoPolicy policy = new HostUndoPolicy();
            CommandId latest = CommandId.New(), earlier = CommandId.New();
            Assert.AreEqual(HostUndoVerdict.Allowed, policy.Check(Me, Guid.NewGuid(), latest, true, latest, Me));
            // The host took back "latest"; the second press (a new request id) still names "latest", but "earlier" is next.
            Assert.AreEqual(HostUndoVerdict.NotTheNamedChange, policy.Check(Me, Guid.NewGuid(), latest, true, earlier, Me));
            Assert.AreEqual(HostUndoVerdict.NotTheNamedChange, policy.Check(Me, Guid.NewGuid(), CommandId.Empty, true, earlier, Me));
        }

        // ---------- AUTH-MOVE ----------

        private static TrapFloorHostMoveFacts Move(int fromX, int fromY, int toX, int toY, bool full = true)
        {
            return new TrapFloorHostMoveFacts
            {
                FullRules = full,
                Sender = Me,
                PawnOwner = Me,
                IsPlayerTurn = true,
                ActivePlayer = Me,
                Current = new TrapFloorCoordinate(fromX, fromY),
                TurnStart = new TrapFloorCoordinate(fromX, fromY),
                Target = new TrapFloorCoordinate(toX, toY),
                Columns = 6,
                Rows = 6,
                Walkable = (x, y) => x >= 1 && y >= 1 && x <= 6 && y <= 6 && !(x == 3 && y == 3), // (3,3) is a hole
                Allowance = 2,
            };
        }

        [Test]
        public void ManualAndReminders_KeepTabletopFreedom()
        {
            TrapFloorHostMoveFacts f = Move(1, 1, 4, 5, full: false); // diagonal and far
            f.Sender = Other; // even another player's pawn, out of turn
            f.ActivePlayer = Other;
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(f));
        }

        [Test]
        public void Full_LegalStraightMove_IsAllowed()
        {
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(Move(1, 1, 1, 3)));
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(Move(2, 2, 2, 2)), "settling in place");
            TrapFloorHostMoveFacts fall = Move(1, 1, 1, 1);
            fall.Target = null;
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(fall), "off the floor is a fall");
        }

        [Test]
        public void Full_InvalidMoves_AreRefused()
        {
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotStraight, TrapFloorHostMoveRules.Check(Move(1, 1, 2, 2)));
            Assert.AreEqual(TrapFloorHostMoveVerdict.TooFar, TrapFloorHostMoveRules.Check(Move(1, 1, 1, 4)));
            Assert.AreEqual(TrapFloorHostMoveVerdict.OntoHole, TrapFloorHostMoveRules.Check(Move(3, 1, 3, 3)));
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotStraight, TrapFloorHostMoveRules.Check(Move(3, 2, 3, 4)), "across a hole");

            TrapFloorHostMoveFacts other = Move(1, 1, 1, 2);
            other.PawnOwner = Other;
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotYourPawn, TrapFloorHostMoveRules.Check(other));

            TrapFloorHostMoveFacts notTurn = Move(1, 1, 1, 2);
            notTurn.ActivePlayer = Other;
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotYourTurn, TrapFloorHostMoveRules.Check(notTurn));

            TrapFloorHostMoveFacts floorTurn = Move(1, 1, 1, 2);
            floorTurn.IsPlayerTurn = false;
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotYourTurn, TrapFloorHostMoveRules.Check(floorTurn));

            TrapFloorHostMoveFacts outPlayer = Move(1, 1, 1, 2);
            outPlayer.PawnOwnerOut = true;
            Assert.AreEqual(TrapFloorHostMoveVerdict.PlayerIsOut, TrapFloorHostMoveRules.Check(outPlayer));

            TrapFloorHostMoveFacts stuck = Move(1, 1, 1, 2);
            stuck.Stuck = true;
            Assert.AreEqual(TrapFloorHostMoveVerdict.Stuck, TrapFloorHostMoveRules.Check(stuck));

            TrapFloorHostMoveFacts blind = Move(1, 1, 1, 2);
            blind.BlindDirection = TrapFloorBlindMovementDirection.Right;
            Assert.AreEqual(TrapFloorHostMoveVerdict.Blind, TrapFloorHostMoveRules.Check(blind));
            blind.Target = new TrapFloorCoordinate(2, 1);
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(blind));
        }

        [Test]
        public void Full_GuidedAndRuleMoves_AreAllowed()
        {
            TrapFloorHostMoveFacts lit = Move(1, 1, 4, 4);
            lit.LitTiles = new HashSet<TrapFloorCoordinate> { new TrapFloorCoordinate(4, 4) };
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(lit), "a Crane / Dodge / Rush tile");
            lit.Target = new TrapFloorCoordinate(1, 2);
            Assert.AreEqual(TrapFloorHostMoveVerdict.NotALitTile, TrapFloorHostMoveRules.Check(lit));

            TrapFloorHostMoveFacts home = Move(4, 4, 1, 1);
            home.RuleMoveExpected = true; // back to the start after a Key
            Assert.AreEqual(TrapFloorHostMoveVerdict.Allowed, TrapFloorHostMoveRules.Check(home));
        }

        // ---------- AUTH-POS ----------

        [Test]
        public void ImpossiblePositions_AreRefused()
        {
            HostCommandPolicy policy = new HostCommandPolicy();
            PlayerId sender = new PlayerId(Guid.NewGuid());
            CommandContext Context() => new CommandContext(CommandId.New(), new MatchId(Guid.NewGuid()), sender, null);
            MoveObjectCommand far = new MoveObjectCommand(Context(), new TabletopObjectId(Guid.NewGuid()),
                new TabletopPose(new TableCoordinate(1e7, 0d), 0f, 0, 0));
            Assert.AreEqual(HostCommandVerdict.InvalidPosition, policy.Check(null, sender, far, null));

            PhysicalObjectState farAway = new PhysicalObjectState(new PhysicalVector3(1e7f, 0f, 0f), new PhysicalRotation(0f, 0f, 0f, 1f),
                default, default, PhysicalObjectMode.Sleeping, sender);
            CommitPhysicalObjectCommand commit = new CommitPhysicalObjectCommand(Context(), new TabletopObjectId(Guid.NewGuid()), farAway, 0);
            Assert.AreEqual(HostCommandVerdict.InvalidPosition, policy.Check(null, sender, commit, null));

            MoveObjectCommand near = new MoveObjectCommand(Context(), new TabletopObjectId(Guid.NewGuid()),
                new TabletopPose(new TableCoordinate(3d, -2d), 90f, 0, 0));
            Assert.AreEqual(HostCommandVerdict.Allowed, policy.Check(null, sender, near, null));
        }
    }
}
