namespace ConsoleCards.Application.Authority
{
    /// <summary>P8A (owner, 2026-11-13): where a joined computer's shared table stands with its host.</summary>
    public enum TableLinkPhase
    {
        /// <summary>No shared table from a host here (offline, or this computer hosts): nothing to guard.</summary>
        NotJoined = 0,
        /// <summary>Joined and up to date: the player plays.</summary>
        Live = 1,
        /// <summary>The connection to the host is lost; getting back in. The table is stale.</summary>
        Reconnecting = 2,
        /// <summary>In the room (again) but the host's table has not arrived yet. The table is stale.</summary>
        Resyncing = 3,
        /// <summary>The session ended (the host left, or getting back in failed). The table is stale for good.</summary>
        Ended = 4,
    }

    /// <summary>
    /// P8A (owner, 2026-11-13: "Network disconnect"): a joined computer's table may only be played while it is the
    /// host's table. Pure: the online presenter tells it what happened; it says whether local play is allowed and what
    /// to show. Rules:
    /// <list type="bullet">
    /// <item>A lost connection makes the table stale at once (nothing made here counts).</item>
    /// <item>Back in the room is not enough: play resumes only when a whole table from the host arrives after that
    /// (so nothing old is replayed and nothing is played on an out-of-date table).</item>
    /// <item>An ended session (host gone, or getting back in failed) stays stale until this table is replaced
    /// (a new game or the main menu): never a frozen table that looks playable.</item>
    /// </list>
    /// </summary>
    public sealed class TableLinkGuard
    {
        public TableLinkPhase Phase { get; private set; }

        /// <summary>Why the session ended (when <see cref="Phase"/> is Ended).</summary>
        public MultiplayerLeaveReason EndReason { get; private set; }

        /// <summary>How many times the connection was lost since the table was last live.</summary>
        public int LostCount { get; private set; }

        /// <summary>The revision of the last whole table from the host (-1: none yet).</summary>
        public long LastHostTableRevision { get; private set; } = -1;

        /// <summary>True when this computer may change the table.</summary>
        public bool AcceptsLocalInput => Phase == TableLinkPhase.NotJoined || Phase == TableLinkPhase.Live;

        /// <summary>True while the table may differ from the host's (shown as such, played not at all).</summary>
        public bool IsStale => !AcceptsLocalInput;

        /// <summary>Joined a host's shared table: stale until the host's table arrives.</summary>
        public void Joined()
        {
            Phase = TableLinkPhase.Resyncing;
            EndReason = MultiplayerLeaveReason.None;
            LastHostTableRevision = -1;
        }

        /// <summary>The connection to the host broke (the session tries to get back in).</summary>
        public void ConnectionLost()
        {
            if (Phase != TableLinkPhase.Live && Phase != TableLinkPhase.Resyncing) return;
            Phase = TableLinkPhase.Reconnecting;
            LostCount++;
        }

        /// <summary>Back in the room: still stale until the host's table comes.</summary>
        public void BackInRoom()
        {
            if (Phase == TableLinkPhase.Reconnecting) Phase = TableLinkPhase.Resyncing;
        }

        /// <summary>A whole table from the host was applied. True when this made the table live again.</summary>
        public bool HostTableArrived(long revision)
        {
            if (Phase != TableLinkPhase.Resyncing && Phase != TableLinkPhase.Live) return false; // not while lost or ended
            LastHostTableRevision = revision;
            if (Phase == TableLinkPhase.Live) return false;
            Phase = TableLinkPhase.Live;
            LostCount = 0;
            return true;
        }

        /// <summary>The session ended while this table was joined.</summary>
        public void SessionEnded(MultiplayerLeaveReason reason)
        {
            if (Phase == TableLinkPhase.NotJoined) return;
            Phase = TableLinkPhase.Ended;
            EndReason = reason == MultiplayerLeaveReason.None ? MultiplayerLeaveReason.ConnectionFailed : reason;
        }

        /// <summary>The table is no longer the room's (a new game, the main menu, or this computer hosts).</summary>
        public void Reset()
        {
            Phase = TableLinkPhase.NotJoined;
            EndReason = MultiplayerLeaveReason.None;
            LostCount = 0;
            LastHostTableRevision = -1;
        }
    }
}
