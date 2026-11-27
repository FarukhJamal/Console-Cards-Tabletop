using System;
using System.IO;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// SYNC-1 (owner's test with a friend, 2026-11-02: "the ping is not syncing", "drawing is only syncing when you finish
    /// drawing; it should be live"): things that last a moment and are never part of the table's state, sent at once
    /// to the other computers: a ping, and a line while it is being drawn. The finished line still arrives with the
    /// table (it is kept with it), and then replaces the line drawn live.
    /// A joined computer sends to the host, and the host shows it and passes it on to the others.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const byte MsgLive = 18;
        private const byte LivePing = 1;
        private const byte LiveStroke = 2;
        private const byte LiveStrokeEnd = 3;
        private const byte LiveMeasure = 4; // owner, 2026-11-03: the Measure tool is seen by everyone too
        private const byte LiveHeldCard = 5; // LIVE-HELD (owner, 2026-11-18): a card held out of a deck, hand or Console
        private const byte LiveChat = 6; // CHAT-1 (designer note 03, 2026-11-17): a player's chat message
        private const byte LiveHand = 7; // HANDS-1 (designer note 01, 2026-11-17): where a player's pointer is on the table
        private const byte LiveRulebook = 8; // BOOK-SYNC (owner, 2026-11-27): the table's rulebook was put down here
        private const float HandKeepSeconds = 2f; // a still pointer is said again this often (the others drop it after 6 s)
        private const int MaxLivePoints = 256; // per message; a longer line goes in several
        private readonly Vector3[] livePoints = new Vector3[MaxLivePoints];

        /// <summary>A ping made here: the others see it, in this player's colour.</summary>
        internal void SendPing(PlayerId player, Vector3 point)
        {
            if (!CanSendLive())
            {
                Debug.Log($"[Online] Ping not sent (shared {sharedTable}, state {session?.State}, applying {applying}).");
                return;
            }

            Debug.Log($"[Online] Ping sent ({(hosting ? "host" : "guest")}).");
            BeginMessage(MsgLive);
            writer.Write(LivePing);
            WriteGuid(player.Value);
            WriteVector(point);
            SendLive();
        }

        /// <summary>BOOK-SYNC: the table's one rulebook was put down at this place here; everyone else moves theirs there.</summary>
        internal void SendRulebookMoved(PlayerId player, Vector3 position)
        {
            if (!CanSendLive()) return;
            BeginMessage(MsgLive);
            writer.Write(LiveRulebook);
            WriteGuid(player.Value);
            WriteVector(position);
            SendLive();
        }

        /// <summary>CHAT-1: a message written here (already cleaned); the words are never logged.</summary>
        internal void SendChat(PlayerId player, string text)
        {
            if (!CanSendLive() || string.IsNullOrEmpty(text)) return;
            BeginMessage(MsgLive);
            writer.Write(LiveChat);
            WriteGuid(player.Value);
            writer.Write(text);
            SendLive();
        }

        // HANDS-1: this computer's pointer on the table, at the pose rate while it moves (and every few seconds while it
        // is still), and once when it leaves the table. Nothing goes while it stays off the table.
        private bool handSent;
        private bool handSentHolding;
        private Vector3 handSentPoint;
        private float handSentAt;

        private void SendHand()
        {
            if (!CanSendLive()) return;
            bool visible = host.TryGetLocalHand(out Vector3 point, out bool holding);
            float now = Time.unscaledTime;
            if (!visible)
            {
                if (!handSent) return;
                handSent = false;
            }
            else
            {
                bool changed = !handSent
                    || holding != handSentHolding
                    || (point - handSentPoint).sqrMagnitude > 0.0004f;
                if (!changed && now - handSentAt < HandKeepSeconds) return;
                handSent = true;
                handSentHolding = holding;
                handSentPoint = point;
            }

            handSentAt = now;
            BeginMessage(MsgLive);
            writer.Write(LiveHand);
            WriteGuid(host.LocalPlayerId.Value);
            writer.Write(visible);
            writer.Write(holding);
            WriteVector(point);
            SendLive();
        }

        // CHAT-1, as LIVE-HELD: the host shows and passes on a message only from the player it names (the sender's own
        // seat, never the message's word), so no one can write as someone else. HANDS-1: the same for a pointer.
        private static bool HostAllowsChat(Member from, byte[] data)
        {
            if (from == null || from.Player == Guid.Empty) return false;
            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                reader.ReadByte();
                return ReadGuid(reader) == from.Player;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>This player's measure (start and end on the table), or that it was put away.</summary>
        internal void SendMeasure(PlayerId player, Vector3 start, Vector3 end, bool visible)
        {
            if (!CanSendLive()) return;
            BeginMessage(MsgLive);
            writer.Write(LiveMeasure);
            WriteGuid(player.Value);
            writer.Write(visible);
            WriteVector(start);
            WriteVector(end);
            SendLive();
        }

        /// <summary>The points of the line being drawn here, from <paramref name="start"/> on.</summary>
        internal void SendStrokePoints(
            PlayerId player,
            int stroke,
            TableMarkKind kind,
            TableMarkStyle style,
            Color colour,
            float width,
            Vector3[] points,
            int start,
            int count)
        {
            if (!CanSendLive() || points == null) return;
            while (start < count)
            {
                int n = Math.Min(MaxLivePoints, count - start);
                BeginMessage(MsgLive);
                writer.Write(LiveStroke);
                WriteGuid(player.Value);
                writer.Write(stroke);
                writer.Write((byte)kind);
                writer.Write((byte)style);
                writer.Write(colour.r);
                writer.Write(colour.g);
                writer.Write(colour.b);
                writer.Write(colour.a);
                writer.Write(width);
                writer.Write(start);
                writer.Write((ushort)n);
                for (int i = 0; i < n; i++) WriteVector(points[start + i]);
                SendLive();
                start += n;
            }
        }

        /// <summary>The line drawn here was kept (<paramref name="kept"/>) or given up.</summary>
        internal void SendStrokeEnd(PlayerId player, int stroke, bool kept)
        {
            if (!CanSendLive()) return;
            BeginMessage(MsgLive);
            writer.Write(LiveStrokeEnd);
            WriteGuid(player.Value);
            writer.Write(stroke);
            writer.Write(kept);
            SendLive();
        }

        // LIVE-HELD (owner's online test, 2026-11-18: "while getting the cards out of the deck, it's not lively
        // updating"): a card held out of a deck, a hand or a Console stays in it on the table until it is dropped, so
        // the poses of loose pieces never named it and the others saw nothing until the drop. Sent at the pose rate
        // while it is carried over the table, and once more when it is let go (the drop's own command moves it; a
        // card put back returns to its place). Only where it is goes out: each computer shows the card's face or back
        // as it already shows it in that container (another seat's hand: its back).
        private TabletopObjectId liveHeldCard;

        private void SendHeldCard()
        {
            if (!CanSendLive()) return;
            ContainedCardDragCoordinator drag = host.ContainedCardDragCoordinator;
            TabletopObjectId card = TabletopObjectId.Empty;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool holding = drag != null && drag.TryGetCarriedPose(out card, out position, out rotation);
            if (holding)
            {
                liveHeldCard = card;
            }
            else
            {
                if (liveHeldCard.IsEmpty) return;
                card = liveHeldCard;
                liveHeldCard = TabletopObjectId.Empty;
            }

            BeginMessage(MsgLive);
            writer.Write(LiveHeldCard);
            WriteGuid(host.LocalPlayerId.Value);
            WriteGuid(card.Value);
            writer.Write(holding);
            WriteVector(position);
            writer.Write(rotation.x);
            writer.Write(rotation.y);
            writer.Write(rotation.z);
            writer.Write(rotation.w);
            SendLive();
        }

        // LIVE-HELD, as PHY-6 for streamed poses: the host shows and passes on a held card only from the player it
        // names (the sender's own seat, never the message's word), and while held only for a card its own table has
        // in a container, unlocked, and not in another seat's hand. The last message (let go) needs only the sender.
        private bool HostAllowsHeldCard(Member from, byte[] data)
        {
            if (from == null || from.Player == Guid.Empty || host.matchState == null) return false;
            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                reader.ReadByte();
                if (ReadGuid(reader) != from.Player) return false;
                TabletopObjectId card = new TabletopObjectId(ReadGuid(reader));
                if (!reader.ReadBoolean()) return true;
                if (!host.matchState.Cards.TryGetValue(card, out CardInstanceState state)
                    || state.BaseState.ContainerId.IsEmpty
                    || state.BaseState.IsUserLocked
                    || !host.matchState.Containers.TryGetValue(state.BaseState.ContainerId, out ContainerState container))
                {
                    return false;
                }

                if (container.Kind != ContainerKind.Hand || container.OwnerSeatId.IsEmpty) return true;
                return host.matchState.Seats.TryGetValue(container.OwnerSeatId, out SeatState seat)
                    && seat.OccupantPlayerId.Value == from.Player;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private bool CanSendLive() =>
            sharedTable && session != null && session.State == MultiplayerSessionState.InRoom && !applying;

        private void WriteVector(Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static Vector3 ReadVector(BinaryReader reader) =>
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        // The host shows its own to everyone; a joined computer sends to the host, which passes it on.
        private void SendLive()
        {
            Flush();
            if (!hosting)
            {
                session.SendToHost(outStream.GetBuffer(), 0, (int)outStream.Length);
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected || member.Connection < 0) continue;
                session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            }
        }

        // Host: shown here, then passed on to everyone but the sender.
        private void HostLive(Member from, byte[] data)
        {
            if (data.Length > 1 && data[1] == LiveHeldCard && !HostAllowsHeldCard(from, data)) return; // LIVE-HELD
            if (data.Length > 1 && (data[1] == LiveChat || data[1] == LiveHand) && !HostAllowsChat(from, data)) return; // CHAT-1, HANDS-1
            ApplyLive(data);
            for (int i = 0; i < members.Count; i++)
            {
                Member other = members[i];
                if (other == from || other.IsHost || !other.Connected || other.Connection < 0) continue;
                session.SendTo(other.Connection, data, 0, data.Length);
            }
        }

        private void ApplyLive(byte[] data)
        {
            if (!sharedTable || applying || !host.IsInitialized)
            {
                if (data.Length > 1 && data[1] == LivePing) Debug.Log($"[Online] Ping dropped (shared {sharedTable}, applying {applying}).");
                return;
            }

            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                byte kind = reader.ReadByte();
                PlayerId player = new PlayerId(ReadGuid(reader));
                if (kind == LivePing) Debug.Log($"[Online] Ping received; from this computer: {host.IsPlayerOnThisComputer(player)}.");
                // A computer never gets its own live messages back (the host passes them to the others only), so
                // nothing is dropped here: a seat the host also plays still shows the pings made for it elsewhere.
                switch (kind)
                {
                    case LivePing:
                        host.ShowRemotePing(player, ReadVector(reader));
                        break;
                    case LiveStroke:
                        {
                            int stroke = reader.ReadInt32();
                            TableMarkKind markKind = (TableMarkKind)reader.ReadByte();
                            TableMarkStyle style = (TableMarkStyle)reader.ReadByte();
                            Color colour = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            float width = reader.ReadSingle();
                            int start = reader.ReadInt32();
                            int n = Math.Min((int)reader.ReadUInt16(), MaxLivePoints);
                            for (int i = 0; i < n; i++) livePoints[i] = ReadVector(reader);
                            host.ShowRemoteStroke(player, stroke, markKind, style, colour, width, start, livePoints, n);
                            break;
                        }

                    case LiveStrokeEnd:
                        host.EndRemoteStroke(player, reader.ReadInt32(), reader.ReadBoolean());
                        break;
                    case LiveMeasure:
                        {
                            bool visible = reader.ReadBoolean();
                            Vector3 start = ReadVector(reader);
                            Vector3 end = ReadVector(reader);
                            host.ShowRemoteMeasure(player, start, end, visible);
                            break;
                        }

                    case LiveRulebook: // BOOK-SYNC
                        host.ShowRemoteRulebookMove(ReadVector(reader));
                        break;
                    case LiveChat: // CHAT-1
                        host.ShowRemoteChat(player, reader.ReadString());
                        break;
                    case LiveHand: // HANDS-1
                        {
                            bool visible = reader.ReadBoolean();
                            bool holding = reader.ReadBoolean();
                            host.ShowRemoteHand(player, ReadVector(reader), visible, holding);
                            break;
                        }

                    case LiveHeldCard:
                        {
                            TabletopObjectId card = new TabletopObjectId(ReadGuid(reader));
                            bool holding = reader.ReadBoolean();
                            Vector3 position = ReadVector(reader);
                            Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            host.ShowRemoteHeldCard(player, card, holding, position, rotation);
                            break;
                        }
                }
            }
            catch (EndOfStreamException)
            {
                // a cut message: nothing to show
            }
        }
    }
}
