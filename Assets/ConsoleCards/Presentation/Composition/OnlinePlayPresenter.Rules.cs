using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Audit C-6 (owner, 2026-11-14: "fix as proposed"): in a normal online room a house-rule change is proposed on the
    /// host's computer, each seated player agrees (or says No) on their own computer, and the host puts the agreed rules
    /// in play and sends them to every computer, in order with its commands. Before, the whole vote ran on whichever
    /// computer proposed it and the result stayed there. The private development session keeps its own messages.
    /// Messages: RuleVote (host → guest: a proposal opened, changed or closed), RuleBallot (guest → host: this
    /// computer's player's vote), RuleSet (host → guest, in order: the rules now in play, JSON of the house-rule set,
    /// empty for the game's own rules).
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const byte MsgRuleVote = 23;
        private const byte MsgRuleBallot = 24;
        private const byte MsgRuleSet = 25;

        private Guid roomRuleProposal;
        private bool roomRuleVoted;

        /// <summary>The host of a normal room with a game on: rule votes go to every computer.</summary>
        internal bool IsRoomRulesHost => hosting && sharedTable && !privateHosting && InRoom;

        /// <summary>A joined computer in a normal room with a game on: rule changes are proposed on the host's computer.</summary>
        internal bool IsRoomRulesGuest => !hosting && sharedTable && !privateGuest && InRoom;

        /// <summary>The host: a proposal opened, changed (someone agreed) or closed; sent to every joined computer.</summary>
        internal void RoomRuleVoteChanged(Guid proposal, bool open, string current, string preview, IReadOnlyList<PlayerId> voters,
            IReadOnlyList<bool> agreed, string message)
        {
            if (!IsRoomRulesHost) return;
            BeginMessage(MsgRuleVote);
            writer.Write(open);
            WriteGuid(proposal);
            writer.Write(current ?? string.Empty);
            writer.Write(preview ?? string.Empty);
            writer.Write(message ?? string.Empty);
            writer.Write(voters.Count);
            for (int i = 0; i < voters.Count; i++)
            {
                WriteGuid(voters[i].Value);
                writer.Write(host.PlayerLabel(voters[i]) ?? string.Empty);
                writer.Write(i < agreed.Count && agreed[i]);
            }

            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (!member.IsHost && member.Connected && member.Connection >= 0) SendTo(member);
            }

            Debug.Log($"[Rules] Proposal {proposal.ToString().Substring(0, 8)} {(open ? "open" : "closed")}: sent to the room.");
        }

        /// <summary>The host: the rules in play after an agreed change, to every joined computer in order.</summary>
        internal void BroadcastRoomRules(string recordJson, string message)
        {
            if (!IsRoomRulesHost || !AnyClientConnected(null)) return;
            BeginOrdered(MsgRuleSet);
            writer.Write(recordJson ?? string.Empty);
            writer.Write(message ?? string.Empty);
            BroadcastPrepared(null);
        }

        // The host: a seated guest's vote.
        private void HostRoomRuleBallot(Member member, byte[] data)
        {
            string verdict;
            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                Guid proposal = new Guid(reader.ReadBytes(16));
                bool agree = reader.ReadBoolean();
                verdict = member.Player == Guid.Empty
                    ? "NotSeated"
                    : host.RulesCardPanel.ReceiveRemoteBallot(new PlayerId(member.Player), proposal, agree);
            }
            catch (Exception exception)
            {
                verdict = "Malformed (" + exception.Message + ")";
            }

            Debug.Log($"[Rules] Vote from {member.Name}: {verdict}.");
        }

        // A joined computer: the host's proposal, shown with this computer's player's Agree / No.
        private void ClientRoomRuleVote(byte[] data)
        {
            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                bool open = reader.ReadBoolean();
                Guid proposal = new Guid(reader.ReadBytes(16));
                string current = reader.ReadString(), preview = reader.ReadString(), message = reader.ReadString();
                int count = reader.ReadInt32();
                if (count < 0 || count > 8) return;
                RuleProposalVoter[] voters = new RuleProposalVoter[count];
                int agreedCount = 0;
                int mine = -1;
                for (int i = 0; i < count; i++)
                {
                    Guid player = new Guid(reader.ReadBytes(16));
                    string label = reader.ReadString();
                    bool agreed = reader.ReadBoolean();
                    if (agreed) agreedCount++;
                    if (player == myPlayer) mine = i;
                    voters[i] = new RuleProposalVoter(label, agreed, player == myPlayer && !agreed);
                }

                if (proposal != roomRuleProposal) roomRuleVoted = false;
                roomRuleProposal = open ? proposal : Guid.Empty;
                if (host.runtimeUi == null) return;
                if (!open)
                {
                    host.runtimeUi.CloseRuleProposal();
                    host.ruleProposalOpen = false;
                    if (!string.IsNullOrEmpty(message)) host.ShowNotice(message);
                    return;
                }

                RuleProposalModel model = new RuleProposalModel
                {
                    Agreeing = true,
                    CurrentText = current,
                    PreviewText = preview,
                    Voters = voters,
                    Status = $"{agreedCount} of {count} agreed. Everyone must agree; one \"No\" keeps the rules."
                        + (mine < 0 ? " You are watching: no vote." : roomRuleVoted ? " Your vote is in." : string.Empty),
                };
                int myIndex = mine;
                RuleProposalActions actions = new RuleProposalActions
                {
                    Step = _ => { },
                    TextChanged = _ => { },
                    Propose = () => { },
                    Agree = index => { if (index == myIndex) RoomBallot(true); },
                    Reject = () => { if (myIndex >= 0) RoomBallot(false); else host.runtimeUi.CloseRuleProposal(); },
                };
                host.ruleProposalOpen = host.runtimeUi.ShowRuleProposal(model, actions);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Rules] The host's rule proposal could not be read: {exception.Message}");
            }
        }

        private void RoomBallot(bool agree)
        {
            if (roomRuleProposal == Guid.Empty || roomRuleVoted) return;
            roomRuleVoted = true;
            BeginMessage(MsgRuleBallot);
            WriteGuid(roomRuleProposal);
            writer.Write(agree);
            SendToHost();
        }

        // A joined computer: the rules the host put in play (in order with its commands).
        private void ClientRoomRules(byte[] data)
        {
            if (data == null || data.Length < 10) return;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(data, 9, data.Length - 9)))
            {
                string recordJson = reader.ReadString();
                string message = reader.ReadString();
                host.RulesCardPanel.UseRoomRules(recordJson, message);
            }
        }
    }
}
