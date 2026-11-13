using System;
using System.IO;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P-2a (owner, 2026-11-13: "board grid size: a replicated command, part of the table setup for joins and
    /// reconnects"): the game's own board grid is a room setting. The host sends it, in order with its commands, after
    /// every whole table it sends and whenever it changes. A joined computer asks the host for a change (Free only; in
    /// Reminders and Full only the host changes it), and takes the host's grid when it arrives.
    /// Payload after the order number: columns, rows (int), space width, space depth, gap, frame (float).
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const byte MsgBoardGrid = 22; // P-2a: both ways, in order (guest → host: a request; host → guest: the grid)

        /// <summary>A joined computer: sends the change to the host instead of making it. False on the host or offline.</summary>
        internal bool SendsGameBoardGrid(BoardGrid grid)
        {
            if (!sharedTable || hosting || session == null) return false;
            SendOrderedToHost(MsgBoardGrid, GridPayload(grid));
            Debug.Log($"[Online] Game board grid {grid.Columns} x {grid.Rows}: asked the host.");
            return true;
        }

        /// <summary>The host: sends the game board's grid to every joined computer, in order.</summary>
        internal void BroadcastGameBoardGrid()
        {
            if (!sharedTable || !hosting || !host.TryGetGameBoardGrid(out BoardGrid grid) || !AnyClientConnected(null)) return;
            BeginOrdered(MsgBoardGrid);
            WriteGrid(grid);
            BroadcastPrepared(null);
        }

        // After a whole table to one computer: its board grid follows it.
        private void SendGameBoardGrid(Member member)
        {
            if (!sharedTable || !hosting || member == null || !member.Connected || member.Connection < 0) return;
            if (!host.TryGetGameBoardGrid(out BoardGrid grid)) return;
            BeginOrdered(MsgBoardGrid);
            WriteGrid(grid);
            SendPrepared(member);
        }

        // The host: a joined computer asks for a grid change.
        private void HostBoardGrid(Member member, byte[] data)
        {
            if (!TryReadGrid(data, out BoardGrid grid)) return;
            if (!PlayAssist.IsFree)
            {
                Debug.Log($"[Authority] Game board grid from {member.Name}: refused (Reminders / Full: host only).");
                SendNotice(member, "Only the host can change the board's grid in Reminders and Full.");
                SendGameBoardGrid(member); // their table shows the host's grid again
                return;
            }

            host.ApplyGameBoardGridFromRoom(grid);
            BroadcastGameBoardGrid(); // everyone, the sender too
        }

        private void ClientBoardGrid(byte[] data)
        {
            if (TryReadGrid(data, out BoardGrid grid)) host.ApplyGameBoardGridFromRoom(grid);
        }

        private void WriteGrid(BoardGrid grid)
        {
            writer.Write(grid.Columns);
            writer.Write(grid.Rows);
            writer.Write(grid.SpaceWidth);
            writer.Write(grid.SpaceDepth);
            writer.Write(grid.Gap);
            writer.Write(grid.Frame);
        }

        private static byte[] GridPayload(BoardGrid grid)
        {
            using (MemoryStream stream = new MemoryStream(24))
            using (BinaryWriter payload = new BinaryWriter(stream))
            {
                payload.Write(grid.Columns);
                payload.Write(grid.Rows);
                payload.Write(grid.SpaceWidth);
                payload.Write(grid.SpaceDepth);
                payload.Write(grid.Gap);
                payload.Write(grid.Frame);
                payload.Flush();
                return stream.ToArray();
            }
        }

        private static bool TryReadGrid(byte[] data, out BoardGrid grid)
        {
            grid = default;
            if (data == null || data.Length < 9 + 24) return false;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(data, 9, data.Length - 9)))
                {
                    int columns = reader.ReadInt32();
                    int rows = reader.ReadInt32();
                    float width = reader.ReadSingle();
                    float depth = reader.ReadSingle();
                    float gap = reader.ReadSingle();
                    float frame = reader.ReadSingle();
                    if (columns < 1 || rows < 1 || columns > BoardGrid.MaximumColumns || rows > BoardGrid.MaximumRows) return false;
                    if (!(width > 0f) || !(depth > 0f) || float.IsInfinity(width) || float.IsInfinity(depth)
                        || float.IsNaN(gap) || float.IsInfinity(gap) || float.IsNaN(frame) || float.IsInfinity(frame)) return false;
                    grid = new BoardGrid(columns, rows, width, depth, Mathf.Max(0f, gap), Mathf.Max(0f, frame));
                    return grid.IsValid;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
