using System;
using System.IO;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Identifiers;
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
                }
            }
            catch (EndOfStreamException)
            {
                // a cut message: nothing to show
            }
        }
    }
}
