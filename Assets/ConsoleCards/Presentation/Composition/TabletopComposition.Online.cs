using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-2 (owner, 2026-10-27: "complete the multiplayer"): what online play needs from the table. The command door is
    /// shared (<see cref="ReplicatedTableAuthority"/>), new IDs and random numbers inside a command come from the
    /// command (<see cref="CommandScopedIdentitySource"/>), the physics of a piece runs on the computer of the player
    /// who moves it, and a whole table can be taken from (or given to) another computer through the same path Undo
    /// uses. The room, the messages and the seats are <see cref="OnlinePlayPresenter"/>'s. Offline nothing here changes
    /// anything: the door passes every command straight through, as before.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private OnlinePlayPresenter online;
        private ReplicatedTableAuthority replicatedAuthority;
        private readonly CommandScopedIdentitySource commandIds = new CommandScopedIdentitySource();
        private Func<PlayerId, bool> playerHereCheck;
        private Func<bool> initialPhysicsCheck;

        /// <summary>Online play: the room, its players and the shared table (made the first time it is needed).</summary>
        internal OnlinePlayPresenter Online => online ?? (online = new OnlinePlayPresenter(this));

        /// <summary>True when online play has been opened in this run (the presenter exists).</summary>
        internal bool HasOnline => online != null;

        /// <summary>True while this table is shared with other computers.</summary>
        internal bool IsOnlineTable => online != null && online.TableShared;

        /// <summary>The shared command door (made with the table's door).</summary>
        internal ReplicatedTableAuthority Replicated
        {
            get
            {
                if (replicatedAuthority == null) _ = TableAuthority;
                return replicatedAuthority;
            }
        }

        /// <summary>The shared command door, or null when the table's door has not been made yet.</summary>
        internal ReplicatedTableAuthority ReplicatedIfMade => replicatedAuthority;

        internal CommandScopedIdentitySource CommandIds => commandIds;

        /// <summary>
        /// In a room every computer deals from the game's own cards, rules and layout (your cards, decks, house rules and
        /// saved layout are not shared yet), so all of them deal the same table.
        /// </summary>
        internal bool UseStockContent => online != null && online.InRoom;

        // ---------- the door's command scope ----------

        // Online only (the door calls these only when the table is shared): the command's own IDs and random numbers.
        private void BeginCommandScope(CommandId id)
        {
            commandIds.Begin(id.Value);
            authoritativeRandomValueSource?.Reseed(CommandScopedIdentitySource.SeedOf(id.Value));
        }

        private void EndCommandScope()
        {
            commandIds.End();
            online?.OnCommandEnded();
        }

        // ---------- who plays here ----------

        // The physics authority asks before committing a piece: only the computer of the player who moves it does. A
        // new piece's first resting place is decided by the host alone (every computer would otherwise send one).
        private void ConnectPhysicsToOnline()
        {
            if (physicalAuthority == null) return;
            if (playerHereCheck == null) playerHereCheck = IsPlayerOnThisComputer;
            if (initialPhysicsCheck == null) initialPhysicsCheck = MayCommitFirstPhysicalState;
            physicalAuthority.IsLocalPlayer = playerHereCheck;
            physicalAuthority.MayCommitInitialState = initialPhysicsCheck;
        }

        private bool MayCommitFirstPhysicalState() => online == null || !online.IsClientTable;

        /// <summary>The name a player plays under online (their profile name), else null.</summary>
        internal string OnlinePlayerName(PlayerId player) => online != null ? online.NameOf(player) : null;

        /// <summary>
        /// False (with the reason shown) when this computer may not change the whole table now: a joined player's
        /// Undo, Redo, Reset and new tables are the host's in online play.
        /// </summary>
        internal bool MayChangeWholeTable(string what)
        {
            if (online == null || !online.IsClientTable || online.ApplyingTable) return true;
            ShowMessage($"In online play the host does {what} for everyone.");
            return false;
        }

        // ---------- the table's shape ----------

        internal TableSpec CurrentTableSpec => tableSpec;

        /// <summary>The host's table shape, for this run only (your own saved table is not changed).</summary>
        internal void UseOnlineTableSpec(TableSpec spec)
        {
            if (spec == null || SameTable(spec, tableSpec) || !FindClassicTable()) return;
            TableSpec copy = spec.Copy();
            copy.Clamp();
            if (ShowTable(copy, out string error))
            {
                tableSpec = copy;
                return;
            }

            Debug.LogWarning($"[Online] The host's {TableShapeName(copy)} table could not be built here ({error}); this computer keeps its own.", this);
        }

        private static bool SameTable(TableSpec a, TableSpec b)
        {
            return b != null
                && a.shape == b.shape
                && string.Equals(a.modelId ?? string.Empty, b.modelId ?? string.Empty, StringComparison.Ordinal)
                && Mathf.Approximately(a.width, b.width)
                && Mathf.Approximately(a.depth, b.depth)
                && Mathf.Approximately(a.cornerRadius, b.cornerRadius)
                && a.edge == b.edge
                && Mathf.Approximately(a.edgeHeight, b.edgeHeight)
                && Mathf.Approximately(a.edgeWidth, b.edgeWidth)
                && Mathf.Approximately(a.thickness, b.thickness);
        }

        // ---------- a whole table from another computer ----------

        /// <summary>
        /// Puts another computer's table (the same data an Undo step keeps) on this table, through the path Undo uses:
        /// the views are built again, the camera stays. The Undo history starts from it. False (with the reason) when
        /// it could not be shown; the table before it is put back.
        /// </summary>
        internal bool RestoreOnlineSnapshot(SessionUndoSnapshot snapshot, long revision, out string error)
        {
            error = null;
            if (snapshot == null || activeSession == null)
            {
                error = "there is no table here yet";
                return false;
            }

            MatchState replacement;
            object restoredGameState;
            PendingControllerPurchaseState restoredPurchase;
            try
            {
                replacement = snapshot.Match.Restore(Math.Max(1L, revision));
                restoredGameState = snapshot.GameState?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            rebuildingFromUndo = true;
            MatchState previousMatch = activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = SessionService.CaptureUndoSnapshot();
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(replacement);
                pendingRestoredGameState = restoredGameState;
                pendingRestoredControllerPurchaseState = restoredPurchase;
                ShowTableMarks(snapshot.TableMarks);
                InitializeActiveSession(false);
                undoHistory.EstablishBaseline(SessionService.CaptureUndoSnapshot());
                SessionService.ShowActiveSessionUi();
                return true;
            }
            catch (Exception exception)
            {
                ShowTableMarks(previousSnapshot.TableMarks);
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Online table");
                Debug.LogError($"[Online] The table from the other computer could not be shown: {exception}", this);
                error = exception.Message;
                return false;
            }
            finally
            {
                pendingRestoredGameState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                SessionService.RefreshUndoUi();
            }
        }
    }
}
