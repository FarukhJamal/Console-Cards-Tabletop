using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-2: what a table sent between computers is made of. The setup says how to deal the same table (the game, its
    /// mode, the players, the seed of its deal, the table's shape and the seat colours); the snapshot is the table as
    /// it is now, the same data an Undo step keeps. Plain data, written with <see cref="TableWire"/>.
    /// </summary>
    internal sealed class OnlineTableSetup
    {
        public int Kind; // 0: Empty Table, 1: a game
        public Guid TemplateId;
        public int DealPlayers;
        public int WinMode = -1;
        public int Anticlockwise = -1;
        public int FloorIndex;
        public int Seed;
        public Guid[] Players;
        public Guid MatchId;
        public int[] Colours;
        public TableSpec Table;
        public long Revision;
        public string ContentHash; // MP-4: the host content the table was dealt with
        public string RuleSetId; // MP-4: the host's house rules in play (empty: the game's default rules)
        public string PlayerGame; // MP-4: a game the host made (its save, JSON), else empty
        public Guid[] CarriedAvatars; // P8A: each seat's Avatar the floor was dealt with (Guid.Empty: none), else empty
        public int StageIndex; // P8A: the stage the floor belongs to
    }

    internal sealed class OnlineTablePacket
    {
        public OnlineTableSetup Setup;
        public SessionUndoSnapshot Snapshot;
    }

    /// <summary>
    /// MP-2: the shared table (doc 10 §4, doc 30 A6). Every computer runs every change on its own copy:
    /// <list type="bullet">
    /// <item>A change made here runs at once and is sent (a command, a few hundred bytes). The host runs the joined
    /// computers' changes on its table and sends every accepted change to everyone, in one order, each computer's
    /// messages numbered so nothing is applied twice or out of order.</item>
    /// <item>What a game does outside the command door (Trap Floor's turns, a floor collapsing) and drawings are sent
    /// as the whole table (the computer of the player who did it sends it, a moment after).</item>
    /// <item>Pieces held in a hand are shown moving on the other computers ten times a second; the piece's physics runs
    /// on the computer of the player who moves it, and its resting place is sent as a command.</item>
    /// <item>Every few seconds the host sends a check of its table; a joined computer whose table differs (two players
    /// grabbed the same card at the same moment, a lost message) asks for the whole table, so tables never stay apart.</item>
    /// </list>
    /// A computer that comes back after losing its connection gets the whole table. Allocation-free per frame; messages
    /// and tables allocate when they are sent or read.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const float PushDelaySeconds = 0.25f;
        private const float PoseSeconds = 0.1f;
        private const float CheckSeconds = 3f;
        private const float GapSeconds = 4f;
        private const float TableRequestRepeatSeconds = 6f;
        private const int MaxPoses = 32;

        private bool sharedTable;
        private bool applying;
        private OnlineTableSetup applySetup;
        private Guid myPlayer;
        private readonly List<Guid> seatPlayers = new List<Guid>(MaxMembers);
        private int tablePlayerCount;
        private int[] currentColours;
        private int lastSeed;
        private int lastFloorIndex;
        private readonly Inbox clientInbox = new Inbox();
        private long clientSeq;
        private MatchState trackedMatch;
        private long lastRevision;
        private bool matchReplaced;
        private bool dirty;
        private float dirtyAt;
        private bool awaitingTable;
        private float lastTableRequest = -100f;
        private float nextPoseAt;
        private float nextCheckAt;
        private int mismatches;
        private Action<AuthoritativeActionAcceptance> acceptanceHandler;
        private Action<object> localCommandHandler;
        private Func<PlayerId, bool> playerCheck;
        private readonly TabletopObjectId[] poseIds = new TabletopObjectId[MaxPoses];
        private readonly Vector3[] posePositions = new Vector3[MaxPoses];
        private readonly Quaternion[] poseRotations = new Quaternion[MaxPoses];
        private static readonly System.Random SeedRandom = new System.Random();

        /// <summary>True while this table is shared with the room.</summary>
        internal bool TableShared => sharedTable;

        /// <summary>True on a joined computer while the table is shared.</summary>
        internal bool IsClientTable => sharedTable && !hosting;

        /// <summary>True while another computer's table is being put on this one.</summary>
        internal bool ApplyingTable => applying;

        private ReplicatedTableAuthority Replicated => host.Replicated;

        // ---------- sharing on and off ----------

        private void StartSharing(bool asHost)
        {
            if (asHost) LinkHosting();
            else LinkJoined(); // P8A: a joined table is frozen until the host's table is applied
            sharedTable = true;
            host.hotSeatMode = false; // each computer plays its own seat
            ReplicatedTableAuthority door = Replicated;
            door.Mode = asHost ? TableReplicationMode.Host : TableReplicationMode.Client;
            if (playerCheck == null) playerCheck = host.IsPlayerOnThisComputer;
            door.IsLocalPlayer = playerCheck;
            if (localCommandHandler == null)
            {
                localCommandHandler = OnLocalCommand;
                door.LocalCommandAccepted += localCommandHandler;
            }

            door.ForgetAppliedCommands();
            awaitingHostDice.Clear(); // AUTH-DICE
            dirty = false;
            matchReplaced = false;
            awaitingTable = false;
            mismatches = 0;
            nextCheckAt = Time.unscaledTime + CheckSeconds;
        }

        private void StopSharing()
        {
            sharedTable = false;
            applying = false;
            applySetup = null;
            ReplicatedTableAuthority door = host.ReplicatedIfMade;
            if (door != null)
            {
                door.Mode = TableReplicationMode.Offline;
                door.IsLocalPlayer = null;
                door.ForgetAppliedCommands();
            }

            if (trackedMatch != null && acceptanceHandler != null) trackedMatch.AuthoritativeActionAccepted -= acceptanceHandler;
            trackedMatch = null;
            seatPlayers.Clear();
            myPlayer = Guid.Empty;
            dirty = false;
            matchReplaced = false;
            awaitingTable = false;
            host.ClearRemoteStrokes(); // SYNC-1: lines others were drawing go with the shared table
        }

        // ---------- starting a game (host) ----------

        /// <summary>
        /// The host's START: the room's players sit in their seat order (the game's seats; more players watch, fewer
        /// leave open seats the host plays), each in their colour, and the table is shared. False with the reason.
        /// </summary>
        internal bool PrepareHostedStart(int seats, string gameName, out string problem)
        {
            problem = null;
            if (!IsHosting)
            {
                problem = "Only the host starts the game.";
                return false;
            }

            if (!AllReady())
            {
                problem = "Waiting for everyone in the room to press READY.";
                return false;
            }

            seats = Mathf.Clamp(seats, 1, MaxMembers);
            seatPlayers.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (i < seats)
                {
                    if (member.Player == Guid.Empty) member.Player = Guid.NewGuid();
                    seatPlayers.Add(member.Player);
                }
                else
                {
                    member.Player = Guid.Empty; // watches this game
                }
            }

            while (seatPlayers.Count < seats) seatPlayers.Add(Guid.NewGuid()); // open seats: the host plays them
            tablePlayerCount = seats;
            currentColours = ColourOrder(seats);
            TabletopComposition.SetSeatColourOrder(currentColours);
            myPlayer = members.Count > 0 ? members[0].Player : Guid.Empty;
            Member me = Me();
            if (me != null) myPlayer = me.Player;
            roomGame = gameName ?? string.Empty;
            roomStarted = true;
            for (int i = 0; i < members.Count; i++) members[i].Ready = members[i].IsHost; // the next game asks again
            if (PrivateDevSession.Enabled && StartPrivateHosting())
            {
                BroadcastRoster(); // PRIVACY-P4: seats as usual; no sharing, no content push, no table
                return true;
            }

            StartSharing(true);
            BroadcastRoster();
            SendContentToRoom(); // MP-4: everyone gets the host's content before the table dealt with it
            return true;
        }

        // Seat i plays the colour its player chose; the colours no one chose follow in their usual order.
        private int[] ColourOrder(int seats)
        {
            int[] order = new int[TabletopComposition.SeatColourCount];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = 0; i < seats && i < members.Count && i < order.Length; i++)
            {
                int wanted = members[i].Colour;
                int at = Array.IndexOf(order, wanted);
                if (at < 0 || at == i) continue;
                order[at] = order[i];
                order[i] = wanted;
            }

            return order;
        }

        // An open seat for a player who joins a game in progress (null: none, they watch).
        private Guid OpenSeat()
        {
            for (int i = 0; i < seatPlayers.Count; i++)
            {
                Guid seat = seatPlayers[i];
                bool taken = false;
                for (int m = 0; m < members.Count && !taken; m++) taken = members[m].Player == seat;
                if (!taken) return seat;
            }

            return Guid.Empty;
        }

        // ---------- the table's set-up (called by TableSessionService while a table is made) ----------

        /// <summary>
        /// False when this computer may not make a new table now: a joined computer asks the host to deal the next
        /// floor or restart the game (the host deals it for everyone); any other table is the host's choice.
        /// </summary>
        internal bool AllowTableReplacement(TabletopSessionSelection selection)
        {
            if (!sharedTable || hosting || applying) return true;
            if (selection.Kind == TabletopSessionKind.GameTemplate
                && host.activeSession != null
                && host.activeSession.Selection.Kind == TabletopSessionKind.GameTemplate
                && host.activeSession.Selection.GameTemplateId.Equals(selection.GameTemplateId))
            {
                BeginMessage(MsgDeal);
                WriteGuid(selection.GameTemplateId.Value);
                writer.Write(host.trapFloorNextFloorIndex);
                SendToHost();
                host.ShowMessage("Asked the host to deal the table for everyone.");
            }
            else
            {
                host.ShowMessage("In online play the host chooses the table.");
            }

            return false;
        }

        /// <summary>The deal's random numbers: the host picks a seed and sends it; a joined computer deals with it.</summary>
        internal void PrepareTableBuild()
        {
            int seed;
            if (applying && applySetup != null)
            {
                seed = applySetup.Seed;
                lastFloorIndex = applySetup.FloorIndex;
            }
            else if (PreparePrivateHostSeed(out int privateDealSeed))
            {
                seed = privateDealSeed; // PRIVACY-P4: the host's own deal; the seed never leaves this computer
                lastFloorIndex = host.trapFloorNextFloorIndex;
            }
            else if (sharedTable && hosting)
            {
                lock (SeedRandom) seed = SeedRandom.Next();
#if UNITY_EDITOR
                // DEV-FIXTURE: an Editor host may fix the deal's seed for a reproducible test floor; it travels to the
                // joined computers in the normal table setup, so everyone deals the same floor.
                if (TrapFloorDevFixtures.TryTakeDealSeed(out int fixtureSeed)) seed = fixtureSeed;
#endif
                lastFloorIndex = host.trapFloorNextFloorIndex;
            }
            else
            {
                return;
            }

            lastSeed = seed;
            if (host.authoritativeRandomValueSource == null) host.authoritativeRandomValueSource = new SystemRandomValueSource(seed);
            else host.authoritativeRandomValueSource.Reseed(seed);
        }

        /// <summary>
        /// The table's players online: the room's seats (the host's), or the players of the table being copied from
        /// the host (a joined computer). <paramref name="players"/> comes in with the game's own count.
        /// </summary>
        internal void OverrideTablePlayers(TabletopSessionSelection selection, List<PlayerId> players, ref PlayerId requester, ref MatchId matchId)
        {
            if (applying && applySetup != null && applySetup.Players != null && applySetup.Players.Length > 0)
            {
                players.Clear();
                for (int i = 0; i < applySetup.Players.Length; i++) players.Add(new PlayerId(applySetup.Players[i]));
                requester = TablePlayerHere(players);
                matchId = new MatchId(applySetup.MatchId);
                return;
            }

            if ((!sharedTable && !privateHosting) || !hosting || seatPlayers.Count == 0) return; // PRIVACY-P4: the room's seats too
            int count = selection.Kind == TabletopSessionKind.EmptyCustom ? seatPlayers.Count : players.Count;
            players.Clear();
            for (int i = 0; i < count; i++)
            {
                if (i >= seatPlayers.Count) seatPlayers.Add(Guid.NewGuid());
                players.Add(new PlayerId(seatPlayers[i]));
            }

            tablePlayerCount = count;
            requester = TablePlayerHere(players);
        }

        // This computer's seat in the table's players (a watcher sees the table from the first seat).
        private PlayerId TablePlayerHere(List<PlayerId> players)
        {
            Member me = Me();
            Guid mine = me != null ? me.Player : myPlayer;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Value == mine) return players[i];
            }

            return players[0];
        }

        // ---------- every frame ----------

        private void TickTable(float now)
        {
            if (!sharedTable || !host.IsInitialized || applying || host.rebuildingFromUndo || Replicated.InsideCommand) return;
            TrackMatch();

            if (!hosting)
            {
                if (clientInbox.GapSince >= 0f && now - clientInbox.GapSince > GapSeconds)
                {
                    SkipGap(clientInbox, null);
                    RequestTable("some changes did not arrive");
                }

                if (awaitingTable && now - lastTableRequest > TableRequestRepeatSeconds) RequestTable("still waiting for the table");
            }
            else
            {
                for (int i = 0; i < members.Count; i++)
                {
                    Member member = members[i];
                    if (member.IsHost || member.FromClient.GapSince < 0f || now - member.FromClient.GapSince <= GapSeconds) continue;
                    SkipGap(member.FromClient, member);
                    SendTable(member);
                }
            }

            if (session.State != MultiplayerSessionState.InRoom) return; // getting back in: nothing goes out meanwhile
            // A change outside the command door that raised no event (a game's round step): the table is sent.
            if (host.matchState != null && host.matchState.Revision != lastRevision) MarkDirty();
            if (matchReplaced)
            {
                matchReplaced = false;
                dirty = false;
                SendWholeTable();
            }
            else if (dirty && now - dirtyAt >= PushDelaySeconds)
            {
                dirty = false;
                SendWholeTable();
            }

            if (now >= nextPoseAt)
            {
                nextPoseAt = now + PoseSeconds;
                SendPoses();
            }

            if (hosting && now >= nextCheckAt)
            {
                nextCheckAt = now + CheckSeconds;
                SendChecks();
            }
        }

        // Follows the match in play: its game steps (outside the command door) make the table be sent again.
        private void TrackMatch()
        {
            MatchState current = host.matchState;
            if (ReferenceEquals(current, trackedMatch)) return;
            if (acceptanceHandler == null) acceptanceHandler = OnAcceptance;
            if (trackedMatch != null) trackedMatch.AuthoritativeActionAccepted -= acceptanceHandler;
            trackedMatch = current;
            if (current == null) return;
            current.AuthoritativeActionAccepted += acceptanceHandler;
            lastRevision = current.Revision;
            if (!applying) matchReplaced = true; // a new table here (the host's Undo, Reset, a new game or floor)
        }

        private void OnAcceptance(AuthoritativeActionAcceptance acceptance)
        {
            if (trackedMatch != null) lastRevision = trackedMatch.Revision;
            if (applying || !sharedTable) return;
            if (host.CommandIds.IsRunning(acceptance.CommandId.Value)) return; // a command: it is sent as one
            if (Replicated.Replaying) return; // the computer that made the change sends what follows from it
            if (!host.IsPlayerOnThisComputer(acceptance.ActorPlayerId)) return; // that seat's computer sends it
            MarkDirty();
        }

        /// <summary>After each command (made here or replayed): the table's revision is where the commands left it.</summary>
        internal void OnCommandEnded()
        {
            if (trackedMatch != null && ReferenceEquals(trackedMatch, host.matchState)) lastRevision = trackedMatch.Revision;
        }

        /// <summary>A change that is not a command (a drawing, a game step): the whole table is sent a moment later.</summary>
        internal void MarkDirty()
        {
            if (!sharedTable || applying || dirty) return;
            dirty = true;
            dirtyAt = Time.unscaledTime;
        }

        private void SendWholeTable()
        {
            if (host.matchState == null) return;
            if (hosting) BroadcastTable(null);
            else if (!awaitingTable) PushTableToHost();
            lastRevision = host.matchState.Revision;
        }

        // ---------- commands ----------

        // A command made here was accepted: the host sends it to everyone, a joined computer to the host.
        private void OnLocalCommand(object command)
        {
            if (!sharedTable || applying || session == null) return;
            byte[] payload;
            try
            {
                payload = TableWire.Write(command, false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] {command.GetType().Name} could not be sent ({exception.Message}); the whole table is sent instead.");
                Replicated.ForgetCommand(command);
                MarkDirty();
                return;
            }

            if (hosting)
            {
                BeginOrdered(MsgCommand);
                writer.Write(payload);
                BroadcastPrepared(null);
            }
            else
            {
                if (command is CommitPhysicalObjectCommand thrown
                    && thrown.SettledDieValue.HasValue
                    && host.TrapFloorGame.IsRulesDie(thrown.ObjectId))
                {
                    // AUTH-DICE: the game reads this die only once the host's result is back (TickLate waits for it).
                    awaitingHostDice[thrown.Context.Id] = Time.unscaledTime;
                }

                SendOrderedToHost(MsgCommand, payload);
            }
        }

        // ---------- AUTH-DICE ----------

        private readonly Dictionary<CommandId, float> awaitingHostDice = new Dictionary<CommandId, float>();
        private const float HostDieWaitSeconds = 4f;

        /// <summary>
        /// A joined computer threw a die a rule reads (Full) and the host's result is not back yet. The game does not read
        /// the die meanwhile, so it never acts on a face the host then replaces.
        /// </summary>
        internal bool RulesDiceAwaitingHost
        {
            get
            {
                if (awaitingHostDice.Count == 0) return false;
                float now = Time.unscaledTime;
                List<CommandId> late = null;
                foreach (KeyValuePair<CommandId, float> pair in awaitingHostDice)
                {
                    if (now - pair.Value > HostDieWaitSeconds) (late ??= new List<CommandId>()).Add(pair.Key);
                }

                if (late != null)
                {
                    for (int i = 0; i < late.Count; i++) awaitingHostDice.Remove(late[i]);
                    Debug.LogWarning("[Authority] The host's die result did not come back in time; asking for the table.");
                    RequestTable("a die result from the host did not come back");
                }

                return awaitingHostDice.Count > 0;
            }
        }

        private void TakeHostDieResult(CommitPhysicalObjectCommand echoed)
        {
            bool waited = awaitingHostDice.Remove(echoed.Context.Id);
            if (!waited || !echoed.SettledDieValue.HasValue) return;
            host.TakeHostDieResult(echoed);
        }

        private void HostCommand(Member member, byte[] message)
        {
            if (!sharedTable) return;
            object command = TableWire.Read(message, 9, message.Length - 9, false);
            CommandContext context = Replicated.ContextOf(command);
            if (member.Player == Guid.Empty || context.RequestedByPlayerId.Value != member.Player)
            {
                SendTable(member); // a watcher, or a seat that is not theirs: their table is set right
                return;
            }

            // AUTH-DUP / AUTH-DRAW (2026-11-06): a command id runs once; a draw goes only into the sender's own Hand.
            HostCommandVerdict verdict = hostCommandPolicy.Check(
                host.matchState,
                new PlayerId(member.Player),
                command,
                controllerDeckCheck ??= new Func<ContainerId, bool>(host.TrapFloorGame.IsControllerDeck),
                gameCommandCheck ??= new Func<object, PlayerId, bool>(host.TrapFloorGame.HostAllowsCommand)); // AUTH-PRIZE
            if (verdict == HostCommandVerdict.Duplicate)
            {
                Debug.Log($"[Authority] Ignored a repeated {command.GetType().Name} from {member.Name}.");
                return; // it already ran and was sent; nothing changes
            }

            if (verdict != HostCommandVerdict.Allowed)
            {
                if (command is CommitPhysicalObjectCommand refusedCommit) NotePhysicsRefused(member, refusedCommit, verdict);
                else Debug.Log($"[Authority] Refused {command.GetType().Name} from {member.Name}: {verdict}.");
                // P8A (owner, 2026-11-13): a piece settling on its own is refused quietly and the whole table goes back at
                // most every few seconds (RefusalReconciliation); a player's own refused action is answered as before.
                if (RefusalReconciliation.NotifiesPlayer(command))
                {
                    string notice = verdict == HostCommandVerdict.RefusedByGame
                        ? host.TrapFloorGame.HostRefusal ?? "The host refused that against its own table."
                        : verdict == HostCommandVerdict.InvalidPosition
                            ? "The host refused that: the position is not on the table."
                            : "The host refused that: Controller cards are drawn into your own hand only.";
                    // P8A: one drop is refused several times (the release and each checkpoint after it): one notice for it.
                    if (!(lastNotice.TryGetValue(member.Player, out var said) && said.Text == notice && Time.unscaledTime - said.At < 3f))
                        SendNotice(member, notice);
                    lastNotice[member.Player] = (notice, Time.unscaledTime);
                }

                // P8A (owner QA, 2026-11-13: hand cards glitched while a defeated player's pawn was dragged and refused):
                // a refused move of a loose piece is set right with that piece alone (put back if it was left in the
                // sender's hand, then the host's record of it); the whole table, which rebuilds the sender's table, is only
                // for what one piece cannot set right.
                if (command is CommitPhysicalObjectCommand refusedHold)
                {
                    ReturnRefusedHold(member, refusedHold);
                    if (SendPieceCorrection(member, refusedHold.ObjectId)) return;
                }

                if (refusalReconciliation.SendsTable(member.Player, command, Time.unscaledTime)) SendTable(member);
                return;
            }

            // AUTH-DICE (owner, 2026-11-06): in Full, a die a rule reads gets the host's own result; the command that is
            // run and sent on (to the thrower too, which turns its die to match) carries it.
            byte[] rewrittenPayload = null;
            if (command is CommitPhysicalObjectCommand thrown
                && host.TrapFloorGame.TryHostRulesDie(thrown, out CommitPhysicalObjectCommand hostResult))
            {
                try
                {
                    rewrittenPayload = TableWire.Write(hostResult, false);
                    command = hostResult;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Authority] The host's die result could not be sent ({exception.Message}); the throw is refused.");
                    SendTable(member);
                    return;
                }
            }

            List<PlayerId> outBefore = host.TrapFloorGame.OutPlayersNow(); // NOTICE-REMOTE
            CommitPhysicalObjectCommand physicalCommit = command as CommitPhysicalObjectCommand;
            PhysicalObjectState physicalBefore = physicalCommit != null && host.matchState != null && host.matchState.ContainsObject(physicalCommit.ObjectId)
                ? host.matchState.GetObject(physicalCommit.ObjectId).PhysicalState
                : null;
            ReplayOutcome outcome = ReplayOutcome.Unknown;
            host.RunWithCardSlide(AbilityCardMovedBy(command), () => (outcome = Replicated.Replay(command)) == ReplayOutcome.Accepted);
            host.TrapFloorGame.AnnounceOutChanges(outBefore);
            if (outcome == ReplayOutcome.Accepted && physicalCommit != null) // P8A: where a held piece rested before its hold
                heldReturn.NoteAccepted(host.matchState, physicalCommit.ObjectId, physicalBefore, physicalCommit.State);
            switch (outcome)
            {
                case ReplayOutcome.Accepted:
                    BeginOrdered(MsgCommand);
                    if (rewrittenPayload != null) writer.Write(rewrittenPayload);
                    else writer.Write(message, 9, message.Length - 9);
                    BroadcastPrepared(null); // the sender too: it knows its own and skips it (a host die result it takes)
                    break;
                case ReplayOutcome.OtherTable:
                    break; // made on a table replaced since; the new one is already on its way to them
                default:
                {
                    if (physicalCommit != null) // P8A: one piece is set right with that piece alone
                    {
                        ReturnRefusedHold(member, physicalCommit);
                        if (SendPieceCorrection(member, physicalCommit.ObjectId))
                        {
                            LogReplayRefused(command, outcome, $"sending {member.Name} the host's record of that piece");
                            break;
                        }
                    }

                    bool sendTable = refusalReconciliation.SendsTable(member.Player, command, Time.unscaledTime); // P8A
                    LogReplayRefused(command, outcome, sendTable
                        ? $"sending {member.Name} the host's whole table"
                        : "a piece settling on its own: no whole table this time (one went to them in the last few seconds)"); // PHY-5
                    if (sendTable) SendTable(member); // it did not fit the host's table: theirs is set right
                    break;
                }
            }
        }

        // P8A STAGE-A diagnostics (live finding, 2026-11-13 host console: "Refused CommitPhysicalObjectCommand from
        // Player 1 (2): RefusedByGame" with "Pawn move refused (Full, PlayerIsOut)" several times a second): each refused
        // physics commit sends the guest the host's whole table again, which puts that piece back on the host's last
        // accepted state there; if the guest's physics then commits it again, the two computers loop. Counted per guest
        // and logged at most every 3 s with the rate, so the loop is visible without one line per refusal.
        private readonly Dictionary<Guid, (int Count, int Logged, float NextLog, float Since, float Last)> physicsRefusals =
            new Dictionary<Guid, (int Count, int Logged, float NextLog, float Since, float Last)>();

        private readonly RefusalReconciliation refusalReconciliation = new RefusalReconciliation();
        private readonly HeldReturnPolicy heldReturn = new HeldReturnPolicy();
        private readonly Dictionary<Guid, (string Text, float At)> lastNotice = new Dictionary<Guid, (string Text, float At)>();

        // P8A (owner, 2026-11-13 live test: a pawn another player tried to move floated in the air and its reach lights
        // stayed on): a refused move must never leave the piece held by the player who was refused. The host puts it back
        // where it rested before that hold, in the holder's name (the only player the table lets end a hold), and sends
        // that to every other computer; the refused sender gets the host's whole table (which holds it) right after.
        // Host → the refused sender, in order with the host's commands: the host's record of one loose piece.
        private int pieceCorrectionsSent;

        private bool SendPieceCorrection(Member member, TabletopObjectId objectId)
        {
            if (member == null || !PieceCorrection.TryCapture(host.matchState, objectId, out PieceCorrection correction)) return false;
            byte[] payload;
            try
            {
                payload = TableWire.Write(correction, false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] A piece's record could not be written ({exception.Message}); the whole table is sent.");
                return false;
            }

            BeginOrdered(MsgPiece);
            writer.Write(payload);
            SendPrepared(member);
            pieceCorrectionsSent++;
            if (pieceCorrectionsSent == 1 || pieceCorrectionsSent % 20 == 0)
                Debug.Log($"[Online] Sent {member.Name} the host's record of a refused piece instead of the whole table ({pieceCorrectionsSent} so far).");
            return true;
        }

        // Guest: the host refused a move of this piece made here; its record is the host's again. No rebuild.
        private void ClientPiece(byte[] message)
        {
            if (!sharedTable || awaitingTable || host.matchState == null) return;
            object read = TableWire.Read(message, 9, message.Length - 9, false);
            if (!(read is PieceCorrection correction) || !correction.ApplyTo(host.matchState))
            {
                RequestTable("a piece the host set right is not loose here");
                return;
            }

            host.PresentAcceptedObject(correction.ObjectId);
            Debug.Log($"[Online] The host set one piece right ({correction.State.Mode}); the rest of the table is unchanged.");
        }

        private bool ReturnRefusedHold(Member member, CommitPhysicalObjectCommand refused)
        {
            if (member == null || member.Player == Guid.Empty || host.matchState == null) return false;
            bool held = heldReturn.TryBuildReturn(host.matchState, new PlayerId(member.Player), refused.ObjectId, CommandId.New(),
                out CommitPhysicalObjectCommand putBack);
            // Audit T-2: a piece no longer held but still moving (a legal drop that rolled on): stopped where the host
            // last accepted it, so the sender's physics and the host's refusals do not loop.
            if (!held && !heldReturn.TryBuildSettle(host.matchState, new PlayerId(member.Player), refused.ObjectId, CommandId.New(),
                    out putBack))
                return false;
            if (Replicated.Replay(putBack) != ReplayOutcome.Accepted)
            {
                Debug.LogWarning($"[Authority] A refused move could not be set right here ({Replicated.LastReplayRefusal}); the table is sent.");
                return false;
            }

            if (!held)
            {
                byte[] settled = TableWire.Write(putBack, false);
                BeginOrdered(MsgCommand);
                writer.Write(settled);
                BroadcastPrepared(member);
                Debug.Log($"[Authority] {member.Name}'s piece rolled on to a refused place: stopped where the host last accepted it, on every computer.");
                return true;
            }

            heldReturn.NoteReturned(refused.ObjectId);
            byte[] payload = TableWire.Write(putBack, false);
            BeginOrdered(MsgCommand);
            writer.Write(payload);
            BroadcastPrepared(member); // every other computer; the refused sender gets the host's record of the piece
            string kind = host.matchState.GetObject(refused.ObjectId).Kind.ToString();
            Debug.Log($"[Authority] {member.Name}'s refused move left the {kind} in their hand on the host's table: put back where it "
                + $"rested before they picked it up, on every computer ({heldReturn.Returned} put back so far).");
            return true;
        }

        private void NotePhysicsRefused(Member member, CommitPhysicalObjectCommand commit, HostCommandVerdict verdict)
        {
            float now = Time.unscaledTime;
            Guid key = member.Player;
            physicsRefusals.TryGetValue(key, out var entry);
            if (entry.Count > 0 && now - entry.Last > 10f) entry = default; // a new episode
            if (entry.Count == 0) entry.Since = now;
            entry.Count++;
            entry.Last = now;
            if (now >= entry.NextLog)
            {
                int fresh = entry.Count - entry.Logged;
                float span = Mathf.Max(0.001f, now - entry.Since);
                string kind = host.matchState != null && host.matchState.ContainsObject(commit.ObjectId)
                    ? host.matchState.GetObject(commit.ObjectId).Kind.ToString()
                    : "unknown piece";
                Debug.LogWarning($"[PhysDiag] Host refused {fresh} physics commit(s) from {member.Name} since the last note "
                    + $"({entry.Count} in {span:0.0} s, {entry.Count / span:0.0}/s): latest {kind} {commit.State?.Mode}, {verdict}"
                    + $"{(verdict == HostCommandVerdict.RefusedByGame && host.TrapFloorGame.HostRefusal != null ? " — " + host.TrapFloorGame.HostRefusal : string.Empty)}. "
                    + (RefusalReconciliation.IsPhysicsOnly(commit)
                        ? $"Physics-only: refused quietly, their table re-sent at most every {RefusalReconciliation.PhysicsOnlyTableSeconds:0} s "
                            + $"({refusalReconciliation.SuppressedTables} whole tables not sent so far)."
                        : "A player's action: they get the notice and the host's whole table."));
                entry.Logged = entry.Count;
                entry.NextLog = now + 3f;
            }

            physicsRefusals[key] = entry;
        }

        private void ClientCommand(byte[] message)
        {
            if (!sharedTable || awaitingTable) return;
            object command = TableWire.Read(message, 9, message.Length - 9, false);
            List<PlayerId> outBefore = host.TrapFloorGame.OutPlayersNow(); // NOTICE-REMOTE
            ReplayOutcome outcome = ReplayOutcome.Unknown;
            host.RunWithCardSlide(AbilityCardMovedBy(command), () => (outcome = Replicated.Replay(command)) == ReplayOutcome.Accepted);
            host.TrapFloorGame.AnnounceOutChanges(outBefore);
            if (outcome == ReplayOutcome.AlreadyApplied && command is CommitPhysicalObjectCommand own)
                TakeHostDieResult(own); // AUTH-DICE: our own throw came back; a rules die shows the host's result
            if (outcome == ReplayOutcome.Rejected || outcome == ReplayOutcome.Unknown)
            {
                LogReplayRefused(command, outcome, "asking the host for the whole table");
                RequestTable("a change from the host did not fit this table");
            }
        }

        // P8A STAGE-A PHY-5 (owner, 2026-11-13): a replayed command that did not fit says what it was and why, before the
        // whole table is asked for or sent: its type, a short safe id (the first 8 hex digits of its command id), the
        // seat it came from and the handler's own result (a status or error code). No card identity, no table contents.
        private void LogReplayRefused(object command, ReplayOutcome outcome, string next)
        {
            CommandContext context = Replicated.ContextOf(command);
            string id = context.Id.IsEmpty ? "none" : context.Id.Value.ToString("N").Substring(0, 8);
            string reason = outcome == ReplayOutcome.Unknown
                ? "no handler for this command type on this table"
                : string.IsNullOrEmpty(Replicated.LastReplayRefusal) ? "refused (no reason given)" : Replicated.LastReplayRefusal;
            string mode = command is CommitPhysicalObjectCommand commit && commit.State != null ? $", physical mode {commit.State.Mode}" : string.Empty;
            Debug.LogWarning($"[Online] Replay {outcome}: {command.GetType().Name} {id} from {SeatLabel(context.RequestedByPlayerId)}{mode}: "
                + $"{reason} (table revision {host.matchState?.Revision ?? -1}); {next}.");
        }

        private string SeatLabel(PlayerId player)
        {
            if (player.IsEmpty) return "no seat";
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Player == player.Value) return $"{members[i].Name}{(members[i].IsHost ? " (host)" : string.Empty)}";
            }

            return "a seat with no member here";
        }

        // P8A (owner, 2026-11-10: "I saw the slide on the client, not on the host"): an Ability card another computer
        // dealt, bought, gave as a starting Ability or put back slides here too, as it did on that computer.
        private TabletopObjectId AbilityCardMovedBy(object command)
        {
            if (host.trapFloorTemplate == null || host.matchState == null) return TabletopObjectId.Empty;
            if (command is ConsoleCards.Games.TrapFloor.TrapFloorBuyFromShopCommand buy) return buy.SourceCardId;
            if (!(command is TransferCardCommand transfer)) return TabletopObjectId.Empty;
            ContainerId deck = host.trapFloorTemplate.AbilityDeckId;
            ContainerId starting = ConsoleCards.Games.TrapFloor.TrapFloorTemplateFactory.StartingAbilitiesId;
            bool abilityPile(ContainerId id) => !id.IsEmpty && (id == deck || id == starting);
            if (!host.matchState.Cards.TryGetValue(transfer.CardObjectId, out var card)) return TabletopObjectId.Empty;
            return abilityPile(card.BaseState.ContainerId) || abilityPile(transfer.DestinationContainerId)
                ? transfer.CardObjectId
                : TabletopObjectId.Empty;
        }

        // ---------- whole tables ----------

        private byte[] TablePayload()
        {
            if (host.activeSession == null || host.matchState == null) return null;
            try
            {
                // M1a filtering (owner, 2026-11-05): a payment in progress names cards chosen from a Hand. No other
                // computer uses it (each keeps its own, B01), so it never leaves this computer.
                return TableWire.Write(new OnlineTablePacket
                {
                    Setup = CaptureSetup(),
                    Snapshot = host.SessionService.CaptureUndoSnapshot().WithoutPayment(),
                }, true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Online] The table could not be packed to send: {exception}");
                return null;
            }
        }

        private OnlineTableSetup CaptureSetup()
        {
            TabletopSessionSelection selection = host.activeSession.Selection;
            bool game = selection.Kind == TabletopSessionKind.GameTemplate;
            IReadOnlyList<PlayerId> players = host.activeSession.Request.ActivePlayerIds;
            Guid[] ids = new Guid[players.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = players[i].Value;
            int deal = 0;
            if (game && host.Cartridges.TryGetTemplateSetup(selection.GameTemplateId, out IGameCartridge owner, out _)) deal = owner.DealPlayerCount;
            TableSpec table = host.CurrentTableSpec;
            return new OnlineTableSetup
            {
                Kind = game ? 1 : 0,
                TemplateId = game ? selection.GameTemplateId.Value : Guid.Empty,
                DealPlayers = deal,
                WinMode = (int)host.TrapFloorGame.WinMode,
                Anticlockwise = host.TrapFloorGame.Anticlockwise ? 1 : 0,
                FloorIndex = lastFloorIndex,
                Seed = lastSeed,
                Players = ids,
                MatchId = host.matchState.Id.Value,
                Colours = currentColours,
                Table = table != null ? table.Copy() : null,
                Revision = host.matchState.Revision,
                ContentHash = hosting ? hostContentHash : roomContentHash,
                RuleSetId = host.RulesCardPanel.ActiveRuleSetId,
                PlayerGame = host.PlayerGamesPanel.activePlayerGame != null ? JsonUtility.ToJson(host.PlayerGamesPanel.activePlayerGame) : string.Empty,
                CarriedAvatars = DealtAvatarsForSetup(),
                StageIndex = host.trapFloorStageIndex,
            };
        }

        // SEC-3 (owner decision B, 2026-11-05): a computer without a seat (watching) receives no table data at all,
        // until the authority migration can send it only what is public (doc 10). It is told why instead.
        private bool IsWatcher(Member member) =>
            sharedTable && member != null && !member.IsHost && member.Player == Guid.Empty;

        private void SendTable(Member member)
        {
            if (IsWatcher(member))
            {
                if (member.WatchNoticeSent) return;
                member.WatchNoticeSent = true;
                SendNotice(member, "This table is full. Watching a game is not available yet: wait for the next game, or ask the host for a seat.");
                return;
            }

            byte[] payload = TablePayload();
            if (payload == null || !member.Connected || member.Connection < 0) return;
            BeginOrdered(MsgTable);
            writer.Write(payload);
            Flush();
            SendPrepared(member);
            SendGameBoardGrid(member); // P-2a: the board grid is part of the table's setup
        }

        private void BroadcastTable(Member except)
        {
            if (!AnyClientConnected(except)) return;
            byte[] payload = TablePayload();
            if (payload == null) return;
            BeginOrdered(MsgTable);
            writer.Write(payload);
            BroadcastPrepared(except);
            if (hosting) BroadcastGameBoardGrid(); // P-2a: the board grid is part of the table's setup
        }

        private void PushTableToHost()
        {
            byte[] payload = TablePayload();
            if (payload != null) SendOrderedToHost(MsgTable, payload);
        }

        // A joined computer's table (after a game step it made): the host takes it and sends it to the others.
        private void HostPush(Member member, byte[] message)
        {
            if (!sharedTable) return;
            if (member == null || member.Player == Guid.Empty)
            {
                // AUTH-1 (authority audit, 2026-11-05): a computer that only watches never replaces the table.
                if (member != null) SendTable(member);
                return;
            }

            OnlineTablePacket packet = TableWire.Read(message, 9, message.Length - 9, true) as OnlineTablePacket;
            if (packet == null || packet.Setup == null) return;
            if (host.matchState == null || packet.Setup.MatchId != host.matchState.Id.Value)
            {
                SendTable(member); // made on an older table
                return;
            }

            int ignored = KeepHostPieces(packet, member); // AUTH-PUSH
            if (ApplyPacket(packet))
            {
                BroadcastTable(member);
                if (ignored > 0) SendTable(member); // its pieces are set back to the host's
            }
            else
            {
                SendTable(member);
            }
        }

        // AUTH-PUSH (2026-11-06): pieces and piles change only through commands the host checked. A joined computer's
        // pushed table brings only the game's rules state (turns, eliminations, Keys, statuses) and drawings; its pieces
        // and piles are replaced by the host's own before it is applied. Returns how many it tried to change.
        private int KeepHostPieces(OnlineTablePacket packet, Member member)
        {
            if (packet.Snapshot == null || host.matchState == null) return 0;
            int changes = 0;
            try
            {
                MatchState mine = GameTemplateInitialSnapshot.Capture(host.matchState).Restore(Math.Max(1L, host.matchState.Revision));
                MatchState theirs = packet.Snapshot.Match.Restore(Math.Max(1L, packet.Setup.Revision));
                List<string> notes = new List<string>(4);
                changes = TablePushGuard.CountPieceChanges(mine, theirs, notes);
                if (changes > 0)
                {
                    Debug.Log($"[Authority] A table pushed by {member.Name} changed {changes} piece(s) or pile(s) outside the host-checked "
                        + $"commands ({string.Join("; ", notes)}); the host's pieces were kept.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Authority] A pushed table could not be compared ({exception.Message}); the host's pieces were kept.");
            }

            int rulesRefused = 0;
            ICartridgeUndoState rules = KeepHostRules(packet.Snapshot.GameState, member, ref rulesRefused); // AUTH-RULES
            packet.Snapshot = SessionUndoSnapshot.Capture(
                host.matchState,
                rules,
                null,
                packet.Snapshot.TableMarks);
            packet.Setup.Revision = Math.Max(host.matchState.Revision, packet.Setup.Revision);
            return changes + rulesRefused;
        }

        // AUTH-RULES-2 (owner, 2026-11-06): the game's rules state in a pushed table is never taken. Every rules step comes
        // as a command the host replayed (AUTH-EFFECTS), so the host's own state already holds it; a push that differs is
        // logged and the sender is set right (TrapFloorRulesPushGuard).
        private ICartridgeUndoState KeepHostRules(ICartridgeUndoState pushed, Member member, ref int refusedCount)
        {
            if (host.trapFloorTemplate == null) return pushed; // another game: no rules state of Trap Floor's
            List<string> refused = new List<string>(4);
            ICartridgeUndoState merged;
            try
            {
                merged = host.TrapFloorGame.HostMergePushedRules(pushed, new PlayerId(member.Player), refused);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Authority] A pushed rules state could not be checked ({exception.Message}); the host's was kept.");
                refusedCount++;
                return host.TrapFloorGame.CaptureUndoState();
            }

            if (merged == null) return pushed;
            if (refused.Count > 0)
            {
                refusedCount += refused.Count;
                Debug.Log($"[Authority] A table pushed by {member.Name} tried to change rules state the host does not take from a push "
                    + $"({string.Join("; ", refused)}); the host's values were kept.");
            }

            return merged;
        }

        private void ClientTable(byte[] message)
        {
            OnlineTablePacket packet = TableWire.Read(message, 9, message.Length - 9, true) as OnlineTablePacket;
            if (packet == null || packet.Setup == null) return;
            if (!HasContentFor(packet)) return; // MP-4: dealt once the host's content is here
            if (!sharedTable) StartSharing(false);
            ApplyPacket(packet);
        }

        // Puts another computer's table here: dealt again when it is another table, then shown as it is now.
        private bool ApplyPacket(OnlineTablePacket packet)
        {
            OnlineTableSetup setup = packet.Setup;
            if (setup == null || packet.Snapshot == null) return false;
            bool ok;
            string error = null;
            applying = true;
            applySetup = setup;
            try
            {
                tablePlayerCount = setup.Players != null ? setup.Players.Length : 0;
                lastSeed = setup.Seed;
                lastFloorIndex = setup.FloorIndex;
                if (!hosting)
                {
                    seatPlayers.Clear();
                    if (setup.Players != null) seatPlayers.AddRange(setup.Players);
                    currentColours = setup.Colours;
                }

                bool another = host.activeSession == null || host.matchState == null || host.matchState.Id.Value != setup.MatchId;
                ok = true;
                if (another)
                {
                    if (!hosting && IsFrontEndOpen())
                    {
                        host.GameSelectPanel.returnToGameSelect = false;
                        host.runtimeUi.CloseGameSelect(); // the game starts: everyone goes to the table
                        // P-CAM (owner, 2026-11-14: the opening camera move "only plays on the host"): a guest coming from
                        // the room to a game's table gets the same opening as the host (CAM-1). A resync or a next floor,
                        // with the table already showing, does not replay it.
                        host.gameIntroWanted = setup.Kind == 1;
                    }

                    ok = BuildFromSetup(setup, out error);
                }

                // NOTICE-REMOTE (2026-11-06): eliminations and escapes made on another computer are announced here too.
                List<PlayerId> outBefore = another ? null : host.TrapFloorGame.OutPlayersNow();
                if (ok) ok = host.RestoreOnlineSnapshot(packet.Snapshot, setup.Revision, out error);
                if (ok) host.TrapFloorGame.AnnounceOutChanges(outBefore);
                if (ok && !hosting) host.TrapFloorGame.RevalidateHintsAfterHostTable(); // REFUSAL-CORRECTION
            }
            catch (Exception exception)
            {
                ok = false;
                error = exception.Message;
                Debug.LogException(exception);
            }
            finally
            {
                applySetup = null;
                TrackMatch(); // the new match is followed without being sent back
                applying = false;
            }

            Replicated.ForgetAppliedCommands();
            awaitingHostDice.Clear(); // AUTH-DICE: the table now holds the host's results
            dirty = false;
            matchReplaced = false;
            awaitingTable = false;
            mismatches = 0;
            if (host.matchState != null) lastRevision = host.matchState.Revision;
            if (ok && !hosting) LinkTableArrived(); // P8A: the host's whole table is here: play may resume
            if (!ok)
            {
                Debug.LogWarning($"[Online] The table from {(hosting ? "a player" : "the host")} could not be shown here: {error}");
                host.ShowMessage($"The shared table could not be shown here: {error}");
            }

            return ok;
        }

        private bool BuildFromSetup(OnlineTableSetup setup, out string error)
        {
            error = null;
            if (setup.Colours != null) TabletopComposition.SetSeatColourOrder(setup.Colours);
            host.UseOnlineTableSpec(setup.Table);
            TabletopSessionSelection selection;
            if (setup.Kind == 1)
            {
                GameTemplateId template = new GameTemplateId(setup.TemplateId);
                if (!host.Cartridges.TryGetTemplateSetup(template, out _, out _))
                {
                    error = "this computer does not have the host's game (or another version of it)";
                    return false;
                }

                selection = TabletopSessionSelection.FromGameTemplate(template);
                host.TrapFloorGame.PendingWinMode = setup.WinMode >= 0 ? (TrapFloorWinMode)setup.WinMode : (TrapFloorWinMode?)null;
                host.TrapFloorGame.PendingAnticlockwise = setup.Anticlockwise >= 0 ? setup.Anticlockwise == 1 : (bool?)null;
            }
            else
            {
                selection = TabletopSessionSelection.EmptyCustom;
            }

            host.trapFloorNextFloorIndex = setup.FloorIndex;
            host.trapFloorNextStageIndex = setup.FloorIndex == 0 ? Math.Max(0, setup.StageIndex) : host.trapFloorStageIndex;
            host.trapFloorCarriedAvatars = AvatarsFromSetup(setup.CarriedAvatars); // P8A: the same Avatars in place as the host's deal
            if (!hosting) host.RulesCardPanel.ReloadHouseRules(setup.RuleSetId); // MP-4: the host's house rules
            try
            {
                bool built = host.SessionService.TryReplaceTable(selection, setup.DealPlayers);
                if (!built) error = host.gameTemplatesPanelError ?? "the table could not be set up";
                if (built && !hosting) UseHostsPlayerGame(setup.PlayerGame);
                return built;
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
                host.trapFloorNextStageIndex = 0;
                host.trapFloorCarriedAvatars = null;
            }
        }

        private Guid[] DealtAvatarsForSetup()
        {
            ObjectDefinitionId[] dealt = host.trapFloorDealtAvatars;
            if (dealt == null) return new Guid[0];
            Guid[] ids = new Guid[dealt.Length];
            for (int i = 0; i < dealt.Length; i++) ids[i] = dealt[i].Value;
            return ids;
        }

        private static ObjectDefinitionId[] AvatarsFromSetup(Guid[] ids)
        {
            if (ids == null || ids.Length == 0) return null;
            ObjectDefinitionId[] avatars = new ObjectDefinitionId[ids.Length];
            for (int i = 0; i < ids.Length; i++) avatars[i] = new ObjectDefinitionId(ids[i]);
            return avatars;
        }

        // MP-4: a game the host made is shown as that game here too (its name and written rules); its pieces come from
        // the host's table, never laid out a second time.
        private void UseHostsPlayerGame(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                host.PlayerGamesPanel.activePlayerGame = JsonUtility.FromJson<PlayerGameFile>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] The host's game could not be read: {exception.Message}");
            }
        }

        // A joined computer asks the host to deal the next floor or restart the same game.
        private void HostDeal(Member member, byte[] message)
        {
            if (!sharedTable || host.activeSession == null) return;
            // M1a (T5): a joined computer may ask for the next deal only when the floor has ended by the rules (its Start
            // button on the floor-end chip). Free mode's manual End Floor is the host's. Anything else is refused.
            if (host.trapFloorTemplate != null && !host.TrapFloorGame.IsCurrentFloorEnded)
            {
                Debug.Log($"[Online] Refused a deal asked by {member?.Name}: the floor has not ended.");
                if (member != null) SendNotice(member, "The host deals the next floor once this one has ended.");
                return;
            }

            BinaryReader reader = Reader(message);
            reader.ReadByte();
            Guid template = ReadGuid(reader);
            int floor = reader.ReadInt32();
            TabletopSessionSelection current = host.activeSession.Selection;
            if (current.Kind != TabletopSessionKind.GameTemplate || current.GameTemplateId.Value != template) return;
            // P8A (owner, 2026-11-13): the host decides what follows from its own rules state (the next floor or stage of
            // the run with its Avatars and Abilities kept, or a fresh run after GAME WON / GAME OVER); the floor number a
            // joined computer asked for is only logged.
            Debug.Log($"[Online] {member?.Name} asked for the next deal (their floor {floor + 1}); the host deals what its rules say.");
            if (host.trapFloorTemplate != null)
            {
                host.TrapFloorGame.DealAfterFloorEnd();
                return;
            }

            host.TrapFloorGame.PendingWinMode = null;
            host.TrapFloorGame.PendingAnticlockwise = null;
            host.trapFloorNextFloorIndex = Mathf.Max(0, floor);
            try
            {
                host.SessionService.TryReplaceTable(current);
            }
            finally
            {
                host.trapFloorNextFloorIndex = 0;
            }
        }

        private void RequestTable(string reason)
        {
            if (hosting || session == null) return;
            float now = Time.unscaledTime;
            if (awaitingTable && now - lastTableRequest < TableRequestRepeatSeconds) return;
            awaitingTable = true;
            lastTableRequest = now;
            Debug.Log($"[Online] Asking the host for the whole table ({reason}).");
            SendSmall(MsgNeedTable);
        }

        // ---------- the check ----------

        private void SendChecks()
        {
            if (host.matchState == null || !AnyClientConnected(null)) return;
            ulong hash = TableHash();
            BeginOrdered(MsgCheck);
            WriteGuid(host.matchState.Id.Value);
            writer.Write(hash);
            BroadcastPrepared(null);
        }

        private void ClientCheck(byte[] message)
        {
            BinaryReader reader = Reader(message);
            reader.ReadBytes(9);
            Guid match = ReadGuid(reader);
            ulong hash = reader.ReadUInt64();
            if (awaitingTable || dirty || applying || Replicated.UnconfirmedCount > 0
                || host.matchState == null || host.matchState.Id.Value != match)
            {
                mismatches = 0;
                return;
            }

            if (TableHash() == hash)
            {
                mismatches = 0;
                return;
            }

            if (++mismatches >= 2)
            {
                mismatches = 0;
                RequestTable("this table differed from the host's");
            }
        }

        // AUTH-5 (authority audit, 2026-11-05): the check also covers the game's public state (Trap Floor: whose turn,
        // the phase, who is out, collapsed tiles, keys, floor won or lost), so tables whose turn went apart are
        // noticed and sent again. Nothing private is in it.
        private ulong TableHash() => MatchHash(host.matchState) + (host.GameStateHash() * 41UL);

        // The table's state as one number, whatever order its parts were added in on each computer.
        // P8A STAGE-A PHY-3: the same record hash the regression tests compare (Runtime TableStateHash).
        private static ulong MatchHash(MatchState match) => TableStateHash.Of(match);

        // ---------- pieces in a hand ----------

        private void SendPoses()
        {
            LocalPhysicalObjectAuthority physics = host.physicalAuthority;
            if (physics == null) return;
            int count = physics.CopyHeldPoses(poseIds, posePositions, poseRotations);
            if (count == 0) return;
            BeginMessage(MsgPoses);
            writer.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                WriteGuid(poseIds[i].Value);
                Vector3 p = posePositions[i];
                Quaternion r = poseRotations[i];
                writer.Write(p.x);
                writer.Write(p.y);
                writer.Write(p.z);
                writer.Write(r.x);
                writer.Write(r.y);
                writer.Write(r.z);
                writer.Write(r.w);
            }

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
                if (IsWatcher(member)) continue; // SEC-3
                session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            }
        }

        private void RefuseFromWatcher(Member member, byte type)
        {
            refusedFromWatchers++;
            if (refusedFromWatchers <= 5 || refusedFromWatchers % 50 == 0)
                Debug.Log($"[Online] Refused message {type} from {member?.Name}: it has no seat ({refusedFromWatchers} refused).");
        }

        private int refusedFromWatchers;

        private void ApplyPoses(byte[] message)
        {
            LocalPhysicalObjectAuthority physics = host.physicalAuthority;
            if (physics == null || !sharedTable || applying) return;
            BinaryReader reader = Reader(message);
            reader.ReadByte();
            int count = Math.Min((int)reader.ReadByte(), MaxPoses);
            for (int i = 0; i < count; i++)
            {
                TabletopObjectId id = new TabletopObjectId(ReadGuid(reader));
                Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                physics.FollowRemote(id, position, rotation);
            }
        }

        // P8A STAGE-A PHY-6 (owner, 2026-11-13: "MsgPoses relays are not validated against the actual controlling
        // peer"): the host checks every streamed pose against its own table before using it or passing it on. The
        // sender is the connection's member (its seat), never anything in the message. A pose is kept only for a loose
        // piece the host's table has held by that very player (PoseRelayPolicy); the rest are dropped, counted and
        // logged at most every few seconds. Only the kept poses are sent on to the other guests.
        private readonly TabletopObjectId[] relayIds = new TabletopObjectId[MaxPoses];
        private readonly Vector3[] relayPositions = new Vector3[MaxPoses];
        private readonly Quaternion[] relayRotations = new Quaternion[MaxPoses];
        private int refusedPoses;
        private int refusedPosesLogged;
        private float refusedPosesLogAt = -1f;
        private PoseRelayVerdict lastPoseRefusal;
        private string lastPoseRefusedFrom = string.Empty;

        private void HostRelayPoses(Member member, byte[] data)
        {
            LocalPhysicalObjectAuthority physics = host.physicalAuthority;
            if (physics == null || !sharedTable || applying || host.matchState == null) return;
            PlayerId sender = member == null || member.Player == Guid.Empty ? PlayerId.Empty : new PlayerId(member.Player);
            int kept = 0;
            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                int count = Math.Min((int)reader.ReadByte(), MaxPoses);
                for (int i = 0; i < count; i++)
                {
                    TabletopObjectId id = new TabletopObjectId(ReadGuid(reader));
                    float px = reader.ReadSingle(), py = reader.ReadSingle(), pz = reader.ReadSingle();
                    float rx = reader.ReadSingle(), ry = reader.ReadSingle(), rz = reader.ReadSingle(), rw = reader.ReadSingle();
                    PoseRelayVerdict verdict = PoseRelayPolicy.Check(host.matchState, sender, id, px, py, pz, rx, ry, rz, rw);
                    if (verdict != PoseRelayVerdict.Allowed)
                    {
                        NotePoseRefused(member, verdict);
                        continue;
                    }

                    relayIds[kept] = id;
                    relayPositions[kept] = new Vector3(px, py, pz);
                    relayRotations[kept] = new Quaternion(rx, ry, rz, rw);
                    kept++;
                }
            }
            catch (EndOfStreamException)
            {
                NotePoseRefused(member, PoseRelayVerdict.InvalidPose); // a cut-off message: what was read and checked is kept
            }

            if (kept == 0) return;
            for (int i = 0; i < kept; i++) physics.FollowRemote(relayIds[i], relayPositions[i], relayRotations[i]);
            BeginMessage(MsgPoses);
            writer.Write((byte)kept);
            for (int i = 0; i < kept; i++)
            {
                WriteGuid(relayIds[i].Value);
                writer.Write(relayPositions[i].x);
                writer.Write(relayPositions[i].y);
                writer.Write(relayPositions[i].z);
                writer.Write(relayRotations[i].x);
                writer.Write(relayRotations[i].y);
                writer.Write(relayRotations[i].z);
                writer.Write(relayRotations[i].w);
            }

            Flush();
            for (int i = 0; i < members.Count; i++)
            {
                Member other = members[i];
                if (other == member || other.IsHost || !other.Connected || other.Connection < 0) continue;
                if (IsWatcher(other)) continue; // SEC-3
                session.SendTo(other.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
            }
        }

        private void NotePoseRefused(Member member, PoseRelayVerdict verdict)
        {
            refusedPoses++;
            lastPoseRefusal = verdict;
            lastPoseRefusedFrom = member?.Name ?? "?";
            if (Time.unscaledTime < refusedPosesLogAt) return;
            refusedPosesLogAt = Time.unscaledTime + 5f;
            Debug.Log($"[Authority] PHY-6: dropped {refusedPoses - refusedPosesLogged} streamed pose(s) since the last note "
                + $"({refusedPoses} in all); latest from {lastPoseRefusedFrom}: {lastPoseRefusal}. Only the player who holds a "
                + "piece on the host's table may move it.");
            refusedPosesLogged = refusedPoses;
        }

        // ---------- reading the table's messages ----------

        private void HostReadTable(Member member, byte type, byte[] data)
        {
            // M1a (owner, 2026-11-05: "the authoritative peer must validate… accepted actions"): a computer without a game
            // seat changes nothing at the table: no commands, table pushes, piece positions, live moves or deals.
            if (IsWatcher(member) && type != MsgNeedTable)
            {
                RefuseFromWatcher(member, type);
                return;
            }

            switch (type)
            {
                case MsgCommand:
                case MsgTable:
                case MsgEndTurn: // AUTH-ET: in order with the sender's commands
                case MsgBoardGrid: // P-2a: in order with the sender's commands
                    ReceiveOrdered(member.FromClient, data, member);
                    break;
                case MsgNeedTable:
                    if (sharedTable) SendTable(member);
                    break;
                case MsgPoses:
                    HostRelayPoses(member, data); // PHY-6: only the poses this connection's player may stream
                    break;
                case MsgDeal:
                    HostDeal(member, data);
                    break;
                case MsgRuleBallot: // audit C-6
                    HostRoomRuleBallot(member, data);
                    break;
                case MsgOffer: // audit C-4: a guest's own cards, for the host to accept or refuse
                    HostOffer(member, data);
                    break;
                case MsgLive:
                    HostLive(member, data); // SYNC-1
                    break;
            }
        }

        private void ClientReadTable(byte type, byte[] data)
        {
            switch (type)
            {
                case MsgCommand:
                case MsgTable:
                case MsgCheck:
                case MsgPiece: // P8A: in order with the host's commands
                case MsgLevel: // ASSIST-ORDER: in order with the host's commands
                case MsgBoardGrid: // P-2a
                case MsgRuleSet: // audit C-6
                    ReceiveOrdered(clientInbox, data, null);
                    break;
                case MsgRuleVote: // audit C-6
                    ClientRoomRuleVote(data);
                    break;
                case MsgPoses:
                    ApplyPoses(data);
                    break;
                case MsgLive:
                    ApplyLive(data); // SYNC-1
                    break;
            }
        }

        // Messages numbered by their sender are used in that order; one that comes early waits for the ones before it.
        private void ReceiveOrdered(Inbox box, byte[] data, Member from)
        {
            if (data.Length < 9) return;
            long seq = BitConverter.ToInt64(data, 1);
            if (seq < box.Expected) return; // already used
            if (seq > box.Expected)
            {
                box.Early[seq] = data;
                if (box.GapSince < 0f) box.GapSince = Time.unscaledTime;
                return;
            }

            UseOrdered(data, from);
            box.Expected++;
            while (box.Early.Count > 0 && box.Early.TryGetValue(box.Expected, out byte[] next))
            {
                box.Early.Remove(box.Expected);
                UseOrdered(next, from);
                box.Expected++;
            }

            if (box.Early.Count == 0) box.GapSince = -1f;
        }

        // A message that never came: carry on from the next one (the whole table follows).
        private void SkipGap(Inbox box, Member from)
        {
            box.GapSince = -1f;
            if (box.Early.Count == 0) return;
            long first = long.MaxValue;
            foreach (long seq in box.Early.Keys)
            {
                first = seq;
                break;
            }

            box.Expected = first;
            while (box.Early.Count > 0 && box.Early.TryGetValue(box.Expected, out byte[] next))
            {
                box.Early.Remove(box.Expected);
                UseOrdered(next, from);
                box.Expected++;
            }
        }

        private void UseOrdered(byte[] data, Member from)
        {
            try
            {
                switch (data[0])
                {
                    case MsgCommand:
                        if (from != null) HostCommand(from, data);
                        else ClientCommand(data);
                        break;
                    case MsgTable:
                        if (from != null) HostPush(from, data);
                        else ClientTable(data);
                        break;
                    case MsgCheck:
                        if (from == null) ClientCheck(data);
                        break;
                    case MsgEndTurn:
                        if (from != null) HostEndTurn(from, data); // AUTH-ET
                        break;
                    case MsgPiece:
                        if (from == null) ClientPiece(data); // P8A
                        break;
                    case MsgLevel:
                        if (from == null && data.Length >= 10) UseRoomAssist(data[9]); // ASSIST-ORDER: only the host's counts
                        break;
                    case MsgBoardGrid:
                        if (from != null) HostBoardGrid(from, data); // P-2a: a request, checked by the host
                        else ClientBoardGrid(data);
                        break;
                    case MsgRuleSet:
                        if (from == null) ClientRoomRules(data); // audit C-6: only the host's counts
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] A change could not be used here: {exception.Message}");
                if (from != null) SendTable(from);
                else RequestTable("a change could not be used");
            }
        }

        // ---------- numbered sending ----------

        private void BeginOrdered(byte type)
        {
            BeginMessage(type);
            writer.Write(0L); // its number, written per computer
        }

        // ASSIST-ORDER: the host's new Assist level, numbered with its commands for every joined computer.
        private void BroadcastLevelInOrder()
        {
            BeginOrdered(MsgLevel);
            writer.Write((byte)ConsoleCards.Presentation.UI.PlayAssist.Level);
            BroadcastPrepared(null);
        }

        private void SendOrderedToHost(byte type, byte[] payload)
        {
            BeginOrdered(type);
            writer.Write(payload);
            Flush();
            WriteSeq(outStream.GetBuffer(), ++clientSeq);
            session.SendToHost(outStream.GetBuffer(), 0, (int)outStream.Length);
        }

        private void SendPrepared(Member member)
        {
            Flush();
            if (!member.Connected || member.Connection < 0) return;
            WriteSeq(outStream.GetBuffer(), ++member.SentSeq);
            session.SendTo(member.Connection, outStream.GetBuffer(), 0, (int)outStream.Length);
        }

        private void BroadcastPrepared(Member except)
        {
            Flush();
            byte[] buffer = outStream.GetBuffer();
            int length = (int)outStream.Length;
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member == except || member.IsHost || !member.Connected || member.Connection < 0) continue;
                if (IsWatcher(member)) continue; // SEC-3: no table, commands, checks or deals for a computer that watches
                WriteSeq(buffer, ++member.SentSeq);
                session.SendTo(member.Connection, buffer, 0, length);
            }
        }

        private bool AnyClientConnected(Member except)
        {
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member != except && !member.IsHost && member.Connected && member.Connection >= 0) return true;
            }

            return false;
        }

        private static void WriteSeq(byte[] buffer, long seq)
        {
            for (int i = 0; i < 8; i++) buffer[1 + i] = (byte)(seq >> (8 * i));
        }
    }
}
