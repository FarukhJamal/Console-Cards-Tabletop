using System;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A STAGE-A (owner, 2026-11-13: "deterministic forced-movement synchronization, authority validation"). Pure C#,
    /// no scene and no network: two identical tables stand for the host and a guest, the forced move's command goes
    /// over the real wire format (TableWire) and both run it with the real use case; the table check's own record hash
    /// (TableStateHash) compares them. The pose relay rule is checked against the host's table.
    /// </summary>
    public sealed class ForcedMoveAndPoseRelayTests
    {
        private static readonly PhysicalRotation Upright = new PhysicalRotation(0f, 0f, 0f, 1f);

        private static PhysicalObjectState At(float x, float z, PhysicalObjectMode mode, PlayerId controller) =>
            new PhysicalObjectState(new PhysicalVector3(x, 0.1f, z), Upright, default, default, mode, controller);

        private sealed class Pair
        {
            public TrapFloorTestTable Host;
            public TrapFloorTestTable Guest;
            public PlayerId Mover;
            public TabletopObjectId Pawn;
        }

        // Host and guest, the first player's pawn resting where both tables accepted it.
        private static Pair Tables()
        {
            Pair pair = new Pair { Host = TrapFloorTestTable.Create(2, 11), Guest = TrapFloorTestTable.Create(2, 11) };
            pair.Mover = pair.Host.Players[0];
            pair.Pawn = pair.Host.SetupOf(pair.Mover).PawnId;
            foreach (TrapFloorTestTable table in new[] { pair.Host, pair.Guest })
                table.Match.GetObject(pair.Pawn).SetPhysicalState(At(0.5f, 0.5f, PhysicalObjectMode.Sleeping, pair.Mover));
            Assert.AreEqual(TableStateHash.Of(pair.Host.Match), TableStateHash.Of(pair.Guest.Match), "the two tables start the same");
            return pair;
        }

        // A Crane/Spring drop as TryAutoMovePawnToFloor makes it: resting on the target tile, the table pose inside.
        private static CommitPhysicalObjectCommand ForcedMove(Pair pair)
        {
            TabletopObjectState pawn = pair.Host.Match.GetObject(pair.Pawn);
            TabletopPose layout = new TabletopPose(new TableCoordinate(2.25d, -1.5d), pawn.Pose.RotationDegrees, pawn.Pose.Layer, pawn.Pose.LocalOrder);
            return new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), pair.Host.Match.Id, pair.Mover, pair.Host.Match.Revision),
                pair.Pawn,
                At(1.75f, -1.25f, PhysicalObjectMode.Sleeping, pair.Mover),
                pawn.PhysicalRevision,
                null,
                AuthoritativeActionRecordMode.Transaction,
                layout);
        }

        private static CommandResult Run(TrapFloorTestTable table, CommitPhysicalObjectCommand command) =>
            new CommitPhysicalObjectUseCase().Execute(table.Match, table.Players, command);

        // The guest gets the host's command over the wire, as the table door replays it.
        private static CommitPhysicalObjectCommand OverTheWire(CommitPhysicalObjectCommand command)
        {
            byte[] bytes = TableWire.Write(command, false);
            return (CommitPhysicalObjectCommand)TableWire.Read(bytes, 0, bytes.Length, false);
        }

        // ---------- PHY-3: forced movement ----------

        [Test]
        public void ForcedMove_OnHostAndGuest_GivesEqualTableHashes_AndOnePose()
        {
            Pair pair = Tables();
            CommitPhysicalObjectCommand move = ForcedMove(pair);
            Assert.IsTrue(Run(pair.Host, move).Succeeded, "the host runs the forced move");
            CommitPhysicalObjectCommand replayed = OverTheWire(move);
            Assert.IsTrue(replayed.LayoutPose.HasValue, "the table pose travels with the command");
            Assert.IsTrue(Run(pair.Guest, replayed).Succeeded, "the guest replays it");

            TabletopObjectState onHost = pair.Host.Match.GetObject(pair.Pawn);
            TabletopObjectState onGuest = pair.Guest.Match.GetObject(pair.Pawn);
            Assert.AreEqual(move.LayoutPose.Value, onHost.Pose, "host: the pose the command names");
            Assert.AreEqual(onHost.Pose, onGuest.Pose, "guest: the same pose (it was set on the sender only before)");
            Assert.AreEqual(onHost.PhysicalState.Position.X, onGuest.PhysicalState.Position.X, 1e-6f);
            Assert.AreEqual(onHost.PhysicalState.Position.Z, onGuest.PhysicalState.Position.Z, 1e-6f);
            Assert.AreEqual(TableStateHash.Of(pair.Host.Match), TableStateHash.Of(pair.Guest.Match),
                "host and guest records are identical after the forced move");

            // The hash is the one the table check uses, and it does see a pose set on one computer only (the old defect).
            onHost.SetPose(new TabletopPose(new TableCoordinate(9d, 9d), 0f, 0, 0));
            Assert.AreNotEqual(TableStateHash.Of(pair.Host.Match), TableStateHash.Of(pair.Guest.Match),
                "a sender-only pose change is a table difference");
        }

        [Test]
        public void ForcedMove_RepeatedCommand_MovesNothingMore()
        {
            Pair pair = Tables();
            CommitPhysicalObjectCommand move = ForcedMove(pair);
            Assert.IsTrue(Run(pair.Host, move).Succeeded);
            CommitPhysicalObjectCommand replayed = OverTheWire(move);
            Assert.IsTrue(Run(pair.Guest, replayed).Succeeded);
            ulong hostHash = TableStateHash.Of(pair.Host.Match);
            long hostRevision = pair.Host.Match.Revision;
            long guestRevision = pair.Guest.Match.Revision;

            Assert.IsFalse(Run(pair.Host, move).Succeeded, "the same command id runs once on the host");
            Assert.IsFalse(Run(pair.Guest, OverTheWire(move)).Succeeded, "and once on the guest");
            Assert.AreEqual(hostRevision, pair.Host.Match.Revision, "no second revision");
            Assert.AreEqual(guestRevision, pair.Guest.Match.Revision);
            Assert.AreEqual(hostHash, TableStateHash.Of(pair.Host.Match), "nothing moved again");
            Assert.AreEqual(hostHash, TableStateHash.Of(pair.Guest.Match));

            HostCommandPolicy policy = new HostCommandPolicy();
            Assert.AreEqual(HostCommandVerdict.Allowed, policy.Check(pair.Host.Match, pair.Mover, replayed, null));
            Assert.AreEqual(HostCommandVerdict.Duplicate, policy.Check(pair.Host.Match, pair.Mover, replayed, null),
                "a resent copy is ignored at the host's door");
        }

        [Test]
        public void ForcedMove_Reconnect_RestoresTheSameFinalPose()
        {
            Pair pair = Tables();
            CommitPhysicalObjectCommand move = ForcedMove(pair);
            Assert.IsTrue(Run(pair.Host, move).Succeeded);

            // A guest that was away gets the host's whole table (the same wire format as a reconnect's table).
            byte[] whole = TableWire.Write(pair.Host.Match, true);
            MatchState rejoined = (MatchState)TableWire.Read(whole, 0, whole.Length, true);
            TabletopObjectState there = rejoined.GetObject(pair.Pawn);
            TabletopObjectState onHost = pair.Host.Match.GetObject(pair.Pawn);
            Assert.AreEqual(onHost.Pose, there.Pose, "the final table pose");
            Assert.AreEqual(onHost.PhysicalState.Position.X, there.PhysicalState.Position.X, 1e-6f, "the final resting place");
            Assert.AreEqual(onHost.PhysicalState.Mode, there.PhysicalState.Mode);
            Assert.AreEqual(TableStateHash.Of(pair.Host.Match), TableStateHash.Of(rejoined), "the same record as the host");

            long revision = rejoined.Revision;
            Assert.IsFalse(new CommitPhysicalObjectUseCase().Execute(rejoined, pair.Host.Players, OverTheWire(move)).Succeeded,
                "a late copy of the move after the reconnect does not move it again");
            Assert.AreEqual(revision, rejoined.Revision);
        }

        [Test]
        public void LayoutPose_NotFinite_IsRefusedAtTheHostsDoor()
        {
            Pair pair = Tables();
            TabletopObjectState pawn = pair.Host.Match.GetObject(pair.Pawn);
            CommitPhysicalObjectCommand bad = new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), pair.Host.Match.Id, pair.Mover, pair.Host.Match.Revision),
                pair.Pawn, At(1f, 1f, PhysicalObjectMode.Sleeping, pair.Mover), pawn.PhysicalRevision, null,
                AuthoritativeActionRecordMode.Transaction, new TabletopPose(new TableCoordinate(double.NaN, 0d), 0f, 0, 0));
            Assert.AreEqual(HostCommandVerdict.InvalidPosition, new HostCommandPolicy().Check(pair.Host.Match, pair.Mover, bad, null));
            Assert.IsFalse(HostCommandPolicy.HasSanePosition(bad));
            Assert.IsTrue(HostCommandPolicy.HasSanePosition(ForcedMove(pair)));
        }

        // ---------- PHY-6: pose relay ----------

        private static PoseRelayVerdict Relay(MatchState table, PlayerId connection, TabletopObjectId id, float x = 0.5f) =>
            PoseRelayPolicy.Check(table, connection, id, x, 0.3f, 0.5f, 0f, 0f, 0f, 1f);

        [Test]
        public void PoseRelay_HolderStreamingTheirOwnPiece_IsAllowed()
        {
            Pair pair = Tables();
            pair.Host.Match.GetObject(pair.Pawn).SetPhysicalState(At(0.5f, 0.5f, PhysicalObjectMode.Held, pair.Mover));
            Assert.AreEqual(PoseRelayVerdict.Allowed, Relay(pair.Host.Match, pair.Mover, pair.Pawn), "the player holding it streams it");
        }

        [Test]
        public void PoseRelay_Unauthorized_IsRefused()
        {
            Pair pair = Tables();
            MatchState table = pair.Host.Match;
            PlayerId other = pair.Host.Players[1];

            Assert.AreEqual(PoseRelayVerdict.NotHeld, Relay(table, pair.Mover, pair.Pawn), "resting: nobody streams it");

            table.GetObject(pair.Pawn).SetPhysicalState(At(0.5f, 0.5f, PhysicalObjectMode.Held, pair.Mover));
            Assert.AreEqual(PoseRelayVerdict.NotController, Relay(table, other, pair.Pawn),
                "another player's connection cannot steer a piece someone else holds");
            Assert.AreEqual(PoseRelayVerdict.NoSeat, Relay(table, PlayerId.Empty, pair.Pawn), "a watcher's connection");
            Assert.AreEqual(PoseRelayVerdict.UnknownObject, Relay(table, pair.Mover, new TabletopObjectId(Guid.NewGuid())));
            Assert.AreEqual(PoseRelayVerdict.InvalidPose, Relay(table, pair.Mover, pair.Pawn, float.NaN));
            Assert.AreEqual(PoseRelayVerdict.InvalidPose, Relay(table, pair.Mover, pair.Pawn, 1e7f));
            Assert.AreEqual(PoseRelayVerdict.InvalidPose,
                PoseRelayPolicy.Check(table, pair.Mover, pair.Pawn, 0f, 0f, 0f, 0f, 0f, 0f, 0f), "not a rotation");

            TabletopObjectId contained = TabletopObjectId.Empty;
            foreach (var card in table.Cards)
            {
                if (!card.Value.BaseState.ContainerId.IsEmpty) { contained = card.Key; break; }
            }

            Assert.IsFalse(contained.IsEmpty, "the test table has cards in a pile");
            Assert.AreEqual(PoseRelayVerdict.Contained, Relay(table, pair.Mover, contained), "a card in a pile has no loose pose");
        }
    }
}
