using System;
using System.Collections.Generic;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;

namespace ConsoleCards.Application.Authority
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "fix the confirmed source of repeated rejected commits and unnecessary full-table
    /// restoration"). Live trace: the host refused a guest's physics settle (an eliminated player's pawn, PlayerIsOut)
    /// about 3–4 times a second and answered EVERY refusal with a notice and its whole table. A whole table rebuilds the
    /// guest's entire table (TabletopComposition.RestoreOnlineSnapshot: Shutdown + InitializeActiveSession): every loose
    /// piece is made again at its accepted state (any piece accepted as still moving is thrown again with its speed and
    /// spin), the turn cartridge is hidden and hops back in, and the guest's physics settles the pawn and commits again:
    /// one shared loop that moved the eliminated pawn, the other pawn, loose cards and dice and hid the turn marker.
    /// <para>
    /// A refusal is still a refusal (the host's table does not change). What changes is the answer to a PHYSICS-ONLY
    /// commit (a piece settling or moving on its own: record mode None, not a hold): no notice to the player, and the
    /// guest's table is sent again at most once every <see cref="PhysicsOnlyTableSeconds"/> per guest. A player's own
    /// action (a drag, a release, a throw) that is refused gets the notice and the host's table every time, as before.
    /// </para>
    /// </summary>
    public sealed class RefusalReconciliation
    {
        public const float PhysicsOnlyTableSeconds = 5f;

        private readonly Dictionary<Guid, float> lastPhysicsTable = new Dictionary<Guid, float>();

        /// <summary>How many physics-only refusals were answered without a whole table (diagnostics).</summary>
        public int SuppressedTables { get; private set; }

        /// <summary>A piece settling or moving on its own (no player's action, not held in a hand).</summary>
        public static bool IsPhysicsOnly(object command) =>
            command is CommitPhysicalObjectCommand commit
            && commit.State != null
            && commit.State.Mode != PhysicalObjectMode.Held
            && commit.RecordMode == AuthoritativeActionRecordMode.None;

        /// <summary>True when the refused sender is told why (a player's action only).</summary>
        public static bool NotifiesPlayer(object command) => !IsPhysicsOnly(command);

        /// <summary>True when the host's whole table goes to the sender after this refusal.</summary>
        public bool SendsTable(Guid sender, object command, float now)
        {
            if (!IsPhysicsOnly(command)) return true;
            if (lastPhysicsTable.TryGetValue(sender, out float last) && now - last < PhysicsOnlyTableSeconds)
            {
                SuppressedTables++;
                return false;
            }

            lastPhysicsTable[sender] = now;
            return true;
        }

        public void Clear()
        {
            lastPhysicsTable.Clear();
            SuppressedTables = 0;
        }
    }
}
