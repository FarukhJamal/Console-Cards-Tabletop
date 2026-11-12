using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "Network disconnect & unwinnable floor", issue 1). A joined computer's table is played
    /// only while it is the host's table (<see cref="TableLinkGuard"/>):
    /// <list type="bullet">
    /// <item>connection lost: the table freezes (the command door refuses everything made here) and says so;</item>
    /// <item>back in the room: still frozen until a whole table from the host is applied (round, turn, floor, pawns,
    /// cards, owners, hands, pending effects and discards all come from it; nothing old is replayed);</item>
    /// <item>the session ended (the host left, or getting back in failed): the table stays frozen until it is replaced,
    /// with Return to Main Menu (and Rejoin when the room may still be there). Never a frozen table that looks
    /// playable, never a stale copy that plays on by itself.</item>
    /// </list>
    /// A joined computer never takes over a shared table (MP-5 off for shared tables): its own dropped connection
    /// looked exactly like a departed host, and the takeover then ran on a stale copy.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private readonly TableLinkGuard link = new TableLinkGuard();
        private MatchState endedTable;
        private string endedRoomCode;
        private TableLinkPhase shownPhase = TableLinkPhase.NotJoined;
        private int shownRefusals;
        private float nextRefusalNoticeAt;

        /// <summary>P8A: true while this computer's table must not be played (a joined table out of touch with its host).</summary>
        internal bool TableIsStale
        {
            get
            {
                if (link.Phase == TableLinkPhase.Ended) return endedTable != null && ReferenceEquals(host.matchState, endedTable);
                return !hosting && sharedTable && !InPrivateSession && link.IsStale; // the private session has its own guard
            }
        }

        internal TableLinkPhase LinkPhase => link.Phase;

        private void LinkJoined()
        {
            endedTable = null;
            endedRoomCode = null;
            link.Joined();
            LogLink("joined: waiting for the host's table");
        }

        private void LinkHosting()
        {
            if (link.Phase == TableLinkPhase.NotJoined) return;
            link.Reset();
            endedTable = null;
            LogLink("this computer hosts the table");
            ShowLinkState();
        }

        private void LinkLost()
        {
            if (hosting || !sharedTable || InPrivateSession) return;
            link.ConnectionLost();
            LogLink($"connection lost (time {link.LostCount}): the table is frozen");
            ShowLinkState();
        }

        private void LinkBackInRoom()
        {
            if (hosting || !sharedTable || InPrivateSession) return;
            link.BackInRoom();
            LogLink("back in the room: waiting for the host's table");
            ShowLinkState();
            RequestTable("back in the room after a lost connection");
        }

        private void LinkTableArrived()
        {
            if (hosting) return;
            long revision = host.matchState != null ? host.matchState.Revision : -1;
            if (!link.HostTableArrived(revision)) return;
            LogLink("the host's table is applied: play resumes");
            ShowLinkState();
        }

        // The room was left while a joined table was in play. On purpose: the table stays here offline (as before).
        // Otherwise it is frozen for good and says why.
        private void LinkLeft(bool wasJoinedTable)
        {
            if (link.Phase == TableLinkPhase.Ended) return; // already ended (Return to Main Menu from a frozen table)
            if (!wasJoinedTable)
            {
                link.Reset();
                return;
            }

            MultiplayerLeaveReason reason = session != null ? session.LastLeaveReason : MultiplayerLeaveReason.ConnectionFailed;
            if (reason == MultiplayerLeaveReason.LeftOnPurpose)
            {
                link.Reset();
                LogLink("left the room on purpose: the table stays on this computer, offline");
                ShowLinkState();
                return;
            }

            endedTable = host.matchState;
            link.SessionEnded(reason);
            LogLink($"session ended ({link.EndReason}): the table is frozen until it is replaced");
            ShowLinkState();
        }

        // Every frame (cheap): a replaced table ends the freeze; a refused move says why.
        private void TickLink(float now)
        {
            if (link.Phase == TableLinkPhase.Ended && !ReferenceEquals(host.matchState, endedTable))
            {
                link.Reset();
                endedTable = null;
                LogLink("the frozen table was replaced");
                ShowLinkState();
                return;
            }

            ReplicatedTableAuthority door = host.ReplicatedIfMade;
            if (door == null || !TableIsStale || door.RefusedWhileFrozen == shownRefusals || now < nextRefusalNoticeAt) return;
            shownRefusals = door.RefusedWhileFrozen;
            nextRefusalNoticeAt = now + 3f;
            shownPhase = TableLinkPhase.NotJoined; // shown again (the player may have closed it)
            ShowLinkState();
        }

        private void ShowLinkState()
        {
            TableLinkPhase phase = link.Phase;
            if (phase == shownPhase && phase != TableLinkPhase.Ended) return;
            shownPhase = phase;
            if (host.runtimeUi == null) return;
            switch (phase)
            {
                case TableLinkPhase.Reconnecting:
                    Chip("CONNECTION LOST", TableNoticeTone.Danger, "Getting back into the room…",
                        "Your table is frozen until the host's table is back. Nothing you do here counts meanwhile.",
                        "Main Menu", ReturnToMainMenu, "Hide", CloseChip);
                    break;
                case TableLinkPhase.Resyncing:
                    Chip("RECONNECTING", TableNoticeTone.Caution, "Back in the room",
                        "Getting the host's table… play resumes as soon as it is here.",
                        "Main Menu", ReturnToMainMenu, "Hide", CloseChip);
                    break;
                case TableLinkPhase.Ended:
                    bool hostLeft = link.EndReason == MultiplayerLeaveReason.HostClosedRoom;
                    endedRoomCode = string.IsNullOrEmpty(endedRoomCode) ? lastRoomCode : endedRoomCode;
                    bool canRejoin = !hostLeft && !string.IsNullOrEmpty(endedRoomCode);
                    bool leftByChoice = link.EndReason == MultiplayerLeaveReason.LeftOnPurpose;
                    Chip("SESSION ENDED", TableNoticeTone.Danger,
                        hostLeft ? "The host left the room" : leftByChoice ? "You left the room" : "Connection lost",
                        hostLeft
                            ? "The game on this table has ended for this computer. This table can no longer be played."
                            : leftByChoice
                            ? "This table was the room's and is frozen here. Rejoin to play it again, or start a new game."
                            : "Could not get back into the room (the network failed; the host may still be playing). "
                              + "This table is frozen: rejoin to get the host's table, or return to the main menu.",
                        "Return to Main Menu", ReturnToMainMenu,
                        canRejoin ? "Rejoin" : "Hide",
                        canRejoin ? (System.Action)Rejoin : CloseChip);
                    break;
                default:
                    CloseChip();
                    if (link.LastHostTableRevision >= 0 && sharedTable && !hosting)
                        host.ShowMessage("Back in sync with the host: play resumes.");
                    break;
            }
        }

        private void Chip(string tag, TableNoticeTone tone, string title, string detail,
            string primary, System.Action primaryAction, string secondary, System.Action secondaryAction)
        {
            host.runtimeUi.ShowTableMilestone(new TableMilestoneModel(
                tag, tone, title, detail, primary, primaryAction, secondary, secondaryAction, 0f));
        }

        private void CloseChip() => host.runtimeUi?.CloseTableMilestone();

        private void ReturnToMainMenu()
        {
            CloseChip();
            Debug.Log("[Online] Table link: Return to Main Menu.");
            if (TableIsStale && link.Phase != TableLinkPhase.Ended)
            {
                // Leaving a frozen table: it stays frozen (never a stale copy played on offline).
                endedTable = host.matchState;
                link.SessionEnded(MultiplayerLeaveReason.LeftOnPurpose);
                shownPhase = TableLinkPhase.Ended;
                LogLink("left a frozen table for the main menu");
            }

            if (InRoom) LeaveRoom(); // a frozen table is never left half in a room
            host.GameSelectPanel.OpenFrontEnd(FrontEndPage.Home);
        }

        private void Rejoin()
        {
            CloseChip();
            string code = endedRoomCode;
            if (string.IsNullOrEmpty(code) || session == null) return;
            Debug.Log("[Online] Table link: Rejoin pressed.");
            shownPhase = TableLinkPhase.NotJoined;
            JoinRoom(code); // the same profile key: the host gives back the same seat and sends its table
        }

        // Diagnostics: phase, why it ended, this computer's seat number, the table's revision (no card names, no keys).
        private void LogLink(string what)
        {
            int seat = myPlayer == System.Guid.Empty ? -1 : seatPlayers.IndexOf(myPlayer);
            long revision = host.matchState != null ? host.matchState.Revision : -1;
            Debug.Log($"[Online] Table link: {what} (phase {link.Phase}, end {link.EndReason}, seat {(seat < 0 ? "none" : (seat + 1).ToString())}, "
                + $"table revision {revision}, host table revision {link.LastHostTableRevision}, session {(session != null ? session.State.ToString() : "none")}).");
        }
    }
}
