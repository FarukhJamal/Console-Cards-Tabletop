using System;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-5 (owner, 2026-10-27): Undo per player, and a room that goes on when its host leaves.
    /// <list type="bullet">
    /// <item>Undo and Redo take back the table's latest change only when it is yours (or a seat played on this computer);
    /// a joined player's Undo is run by the host, who sends everyone the table as it was.</item>
    /// <item>When the host goes, the first player still connected in the room's seat order hosts the same room code
    /// with the table as it is on their computer; the others get back in with their own seats, as after a lost
    /// connection. The old host's seat waits for them (played by the new host meanwhile).</item>
    /// </list>
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const float NewHostWaitSeconds = 30f;

        private Guid undoRequestPlayer; // host: the joined player whose Undo or Redo is being run
        private string undoRefusal;
        private bool takingOver;
        private bool hostLossCheck;

        // ---------- Undo per player ----------

        /// <summary>A joined computer: asks the host to undo (or redo) this player's own latest change.</summary>
        internal void AskHostToUndo(bool redo)
        {
            if (!InRoom || hosting) return;
            BeginMessage(MsgUndo);
            writer.Write(redo);
            SendToHost();
            host.ShowMessage(redo ? "Asked the host to redo your move." : "Asked the host to undo your last move.");
        }

        /// <summary>
        /// True when this Undo (or Redo) may run: offline always; online only for a change made by a seat played on this
        /// computer, or, run for a joined player, by that player's seat. Otherwise says whose change it is.
        /// </summary>
        internal bool MayUndo(ActiveSessionUndoTransaction transaction, bool redo)
        {
            undoRefusal = null;
            if (!sharedTable || transaction == null || transaction.ActorPlayerId.IsEmpty) return true;
            PlayerId actor = transaction.ActorPlayerId;
            bool allowed = undoRequestPlayer != Guid.Empty ? actor.Value == undoRequestPlayer : IsLocalPlayer(actor);
            if (allowed) return true;
            string who = NameOf(actor) ?? "another player";
            undoRefusal = $"The latest change is {who}'s: only they can {(redo ? "redo" : "undo")} it.";
            if (undoRequestPlayer == Guid.Empty) host.ShowMessage(undoRefusal);
            return false;
        }

        // Host: a joined player's Undo or Redo, run for them; the table that results goes to everyone.
        private void HostUndo(Member member, byte[] data)
        {
            bool redo = data.Length > 1 && data[1] != 0;
            if (!sharedTable || member.Player == Guid.Empty)
            {
                SendNotice(member, "Only players with a seat can undo.");
                return;
            }

            undoRequestPlayer = member.Player;
            bool done;
            try
            {
                done = redo
                    ? host.SessionService.RedoLatestAction(new PlayerId(member.Player))
                    : host.SessionService.UndoLatestAction(new PlayerId(member.Player));
            }
            finally
            {
                undoRequestPlayer = Guid.Empty;
            }

            if (!done) SendNotice(member, undoRefusal ?? (redo ? "There is nothing of yours to redo." : "There is nothing of yours to undo."));
        }

        private void SendNotice(Member member, string text)
        {
            BeginMessage(MsgNotice);
            writer.Write(text ?? string.Empty);
            SendTo(member);
        }

        private void ReadNotice(byte[] data)
        {
            System.IO.BinaryReader reader = Reader(data);
            reader.ReadByte();
            host.ShowMessage(reader.ReadString());
        }

        // ---------- a new host ----------

        // The room's next host: the first player still connected, in seat order, who is not the host that went.
        private bool IsNextHost()
        {
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.IsHost || !member.Connected) continue;
                return member.Key == clientKey;
            }

            return false;
        }

        // A joined computer lost the room (its host went, or only its own connection): the next host takes over, the
        // others keep trying to get back in while that happens.
        private void HostMayHaveLeft()
        {
            if (hosting || members.Count < 2) return;
            if (IsNextHost())
            {
                takingOver = true;
                session.TakeOverRoom();
            }
            else
            {
                session.ExpectNewHost(NewHostWaitSeconds);
            }
        }

        // This computer now hosts the room it was in: the players stay, the table goes on from this computer's copy.
        private void TookOverRoom()
        {
            takingOver = false;
            hosting = true;
            float now = Time.unscaledTime;
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                bool me = member.Key == clientKey;
                member.IsHost = me;
                member.Connected = me;
                member.Connection = me ? session.LocalConnection : -1;
                member.LeftAt = me ? -1f : now;
                member.SentSeq = 0;
                member.FromClient.Reset();
                member.ContentHash = string.Empty;
                if (me) member.Ready = true;
            }

            waiting.Clear();
            if (sharedTable) StartSharing(true);
            session.UpdateRoom(roomGame, members.Count < MaxMembers);
            host.ShowMessage("The host left: you host the room now. The others are getting back in.");
            if (host.IsInitialized && sharedTable)
            {
                if (IsFrontEndOpen()) host.runtimeUi.KeepPanelsOnNextRefresh();
                host.SessionService.ShowActiveSessionUi(); // Undo and the seats' names
            }
        }
    }
}
