using System;
using System.IO;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// AUTH-ET / AUTH-DRAW (owner approval, 2026-11-06): the first host-authoritative actions.
    /// <list type="bullet">
    /// <item>End Turn: a joined computer sends a request (no player id in it). The host takes the sender from the
    /// connection's seat, checks it is that seat's turn and still the turn asked about, refuses a request id it has
    /// already run, ends the turn once (as the sender's seat), and sends the resulting table to everyone.</item>
    /// <item>Commands from joined computers (draws included) run once per command id, and a draw goes only into the
    /// sender's own Hand (<see cref="HostCommandPolicy"/>).</item>
    /// </list>
    /// What stays as before is written in doc 10 §15.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private readonly TrapFloorEndTurnAuthority endTurnAuthority = new TrapFloorEndTurnAuthority();
        private readonly HostCommandPolicy hostCommandPolicy = new HostCommandPolicy();
        private Func<ContainerId, bool> controllerDeckCheck;
        private Func<object, PlayerId, bool> gameCommandCheck; // AUTH-PRIZE

        // Set only on the host, only while it runs a request it has checked: the sender's seat counts as played here.
        private Guid hostActingFor;

        /// <summary>
        /// P8A batch 4: the host is running a step it checked for a joined computer's player (that computer's End Turn).
        /// What only that player's own computer can do (throw the dice, show a popup) is not done here.
        /// </summary>
        internal bool IsActingForAnotherComputer
        {
            get
            {
                if (hostActingFor == Guid.Empty) return false;
                for (int i = 0; i < members.Count; i++)
                    if (!members[i].IsHost && members[i].Player == hostActingFor) return true;
                return false;
            }
        }

        /// <summary>A joined computer's End Turn goes to the host. False offline, on the host, or while away.</summary>
        /// <summary>P7: a joined computer of a normal shared table (its host decides turns).</summary>
        internal bool IsJoinedSharedClient => sharedTable && !hosting && session != null;

        internal bool TrySendEndTurnRequest(TrapFloorEndTurnRequest request)
        {
            // PRIVACY-P6A: on the host of a private development session the host's own End Turn goes through the private
            // pipeline's check (the same one a guest's request meets); false everywhere else, so normal play is unchanged.
            if (TryRunHostOwnPrivateEndTurn(request)) return true;
            if (!sharedTable || hosting || session == null || awaitingTable) return false;
            // Changes this computer made outside the command door (a pawn hop, a flipped tile) go first, in order.
            if (dirty || matchReplaced)
            {
                dirty = false;
                matchReplaced = false;
                SendWholeTable();
            }

            using (MemoryStream stream = new MemoryStream(48))
            using (BinaryWriter payload = new BinaryWriter(stream))
            {
                payload.Write(request.RequestId.ToByteArray());
                payload.Write(request.Round);
                payload.Write((int)request.Phase);
                payload.Write(request.TurnOwner.Value.ToByteArray());
                payload.Write(request.Skipped);
                payload.Flush();
                SendOrderedToHost(MsgEndTurn, stream.ToArray());
            }

            Debug.Log($"[Authority] End Turn requested (round {request.Round}, {request.Phase}).");
            return true;
        }

        private void HostEndTurn(Member member, byte[] data)
        {
            if (!sharedTable || !hosting || member == null || data.Length < 9 + 16 + 4 + 4 + 16 + 1) return;
            TrapFloorEndTurnRequest request;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(data, 9, data.Length - 9)))
            {
                Guid id = new Guid(reader.ReadBytes(16));
                int round = reader.ReadInt32();
                TrapFloorTurnPhase phase = (TrapFloorTurnPhase)reader.ReadInt32();
                PlayerId owner = new PlayerId(new Guid(reader.ReadBytes(16)));
                bool skipped = reader.ReadBoolean();
                request = new TrapFloorEndTurnRequest(id, round, phase, owner, skipped);
            }

            PlayerId sender = new PlayerId(member.Player); // from the host's own seat table, never from the request
            bool won = host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon;
            TrapFloorEndTurnVerdict verdict = endTurnAuthority.Check(host.trapFloorTurnState, sender, request, won);
            if (verdict != TrapFloorEndTurnVerdict.Accepted)
            {
                Debug.Log($"[Authority] End Turn from {member.Name}: refused ({verdict}).");
                if (verdict == TrapFloorEndTurnVerdict.Duplicate) return; // it already ran; its table was sent
                SendNotice(member, EndTurnRefusalText(verdict));
                SendTable(member);
                return;
            }

            TrapFloorTurnState turn = host.trapFloorTurnState;
            int roundBefore = turn.CurrentRound;
            TrapFloorTurnPhase phaseBefore = turn.Phase;
            PlayerId ownerBefore = TrapFloorEndTurnAuthority.TurnOwnerOf(turn);
            hostActingFor = member.Player;
            try
            {
                host.TrapFloorGame.AdvanceTrapFloorTurn(request.Skipped);
            }
            finally
            {
                hostActingFor = Guid.Empty;
            }

            TrapFloorTurnState after = host.trapFloorTurnState;
            bool moved = after == null
                || after.CurrentRound != roundBefore
                || after.Phase != phaseBefore
                || after.IsCurrentFloorFailed
                || TrapFloorEndTurnAuthority.TurnOwnerOf(after) != ownerBefore;
            Debug.Log($"[Authority] End Turn from {member.Name}: accepted, {(moved ? "turn ended" : "the turn did not end (a step is still waiting)")}.");
            if (!moved) SendNotice(member, host.TrapFloorGame.lastFloorTurnRefusal ?? "The host could not end the turn yet: a step is still waiting (see the table).");
            dirty = false;
            matchReplaced = false;
            SendWholeTable(); // everyone, the sender too, gets the host's table
        }

        /// <summary>
        /// Foundation stage (audit A2, owner "OK", 2026-11-13: "one host-authority check applied to host and guest
        /// commands alike"): on the host of a normal shared room, a command the host's own player makes meets the same
        /// game check a joined computer's command meets (<see cref="TrapFloorCartridge.HostAllowsCommand"/>: Main slots,
        /// tiles, prizes, the one collapse per floor turn, and in Full the pawn move rules). A game step with no player,
        /// a step the host runs for a joined player, and physics commits are not checked here: a refused commit of a body
        /// the host itself simulates would leave it where its physics put it (guests get a put-back and a correction
        /// from the host; the host has no one above it), so the host's own pawn drags stay as before (doc 15).
        /// </summary>
        internal bool HostAllowsOwnCommand(object command, PlayerId requester)
        {
            if (!sharedTable || !hosting || privateHosting || command == null || requester.IsEmpty) return true;
            if (IsActingForAnotherComputer || command is CommitPhysicalObjectCommand) return true;
            if (host.TrapFloorGame == null || host.trapFloorTemplate == null) return true;
            if (host.TrapFloorGame.HostAllowsCommand(command, requester)) return true;
            string why = host.TrapFloorGame.HostRefusal;
            Debug.Log($"[Authority] The host player's {command.GetType().Name}: refused by the game check ({(why ?? "no reason given")}).");
            host.ShowMessage(string.IsNullOrEmpty(why) ? "The rules do not allow that move." : why.Replace("The host refused", "Refused"));
            return false;
        }

        private static string EndTurnRefusalText(TrapFloorEndTurnVerdict verdict)
        {
            switch (verdict)
            {
                case TrapFloorEndTurnVerdict.NotTurnOwner: return "The host refused: it is not your turn.";
                case TrapFloorEndTurnVerdict.StaleTurn: return "The host refused: the turn had already moved on.";
                case TrapFloorEndTurnVerdict.FloorEnded: return "The host refused: the floor has ended.";
                case TrapFloorEndTurnVerdict.NotSeated: return "The host refused: you have no seat at this table.";
                default: return "The host refused to end the turn.";
            }
        }
    }
}
