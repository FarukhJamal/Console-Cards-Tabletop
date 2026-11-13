#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// DEV-FIXTURE (2026-11-06, "stop relying on random floor layouts"): reproducible Trap Floor deals for testing Keys,
    /// exits and eliminations live.
    /// <list type="bullet">
    /// <item>Editor only: the whole file is inside <c>#if UNITY_EDITOR</c>, so no player build (development builds
    /// included) contains it. It is driven from the Editor menu Console Cards › Debug › Fixtures.</item>
    /// <item>It changes nothing but the deal's seed. The deal itself is the game's normal path (the same template
    /// factory, the game's own authored content and rules, the room's seed message), so a fixed seed gives the same
    /// layout for the same game, difficulty, win mode, floor and player count, on every computer in the room.</item>
    /// <item>Online, only the host's deal reads it (the seed then travels in the normal table setup), so the joined
    /// computers deal the same floor.</item>
    /// <item>The floor description names face-down tiles. It goes to the Editor's own Console only; it is never sent
    /// to another computer.</item>
    /// </list>
    /// </summary>
    public static class TrapFloorDevFixtures
    {
        private static int pendingSeed;
        private static bool armed;

        // ---------- P8A debug: peek at the floor (owner, 2026-11-13) ----------
        // "Flip all the floor cards … a little cheat … win it to check it." The floor's face-down tiles are DRAWN face up
        // on this Editor only (CardView.EditorPeek); the table, the rules and every other computer are unchanged, so the
        // floor is then played and won through the normal rules. Editor-only like the rest of this file.

        public static bool IsFloorPeekOn => CardView.EditorPeek != null;

        public static string ToggleFloorPeek(TabletopComposition host)
        {
            if (host == null || host.trapFloorTemplate == null) return "[Debug] No Trap Floor table in play.";
            if (IsFloorPeekOn)
            {
                CardView.EditorPeek = null;
            }
            else
            {
                CardView.EditorPeek = id =>
                    host != null && host.trapFloorTemplate != null && host.trapFloorTemplate.TryGetFloorCoordinate(id, out _);
            }

            int redrawn = 0;
            for (int i = 0; i < host.Registry.cardViews.Count; i++)
            {
                CardView card = host.Registry.cardViews[i];
                if (card == null || !card.IsBound || card.CardState == null || !card.IsFacePresentationConfigured) continue;
                card.ApplyAcceptedFacePresentation();
                redrawn++;
            }

            return IsFloorPeekOn
                ? $"[Debug] Floor peek ON: face-down floor tiles are drawn face up on this Editor only ({redrawn} cards redrawn). The table is unchanged."
                : $"[Debug] Floor peek OFF ({redrawn} cards redrawn).";
        }

        /// <summary>True while the next deal will use <see cref="PendingSeed"/>.</summary>
        public static bool IsArmed => armed;

        public static int PendingSeed => pendingSeed;

        /// <summary>The next deal made on this computer (offline, or as the online host) uses this seed, once.</summary>
        public static void ArmDealSeed(int seed)
        {
            pendingSeed = seed;
            armed = true;
            Debug.Log($"[Fixture] The next deal uses seed {seed} (Editor only).");
        }

        public static void Disarm()
        {
            armed = false;
        }

        /// <summary>Takes the armed seed for this deal (online host path). False when none is armed.</summary>
        internal static bool TryTakeDealSeed(out int seed)
        {
            seed = pendingSeed;
            if (!armed) return false;
            armed = false;
            Debug.Log($"[Fixture] Dealing with fixed seed {seed}.");
            return true;
        }

        /// <summary>Offline deals: the armed seed starts the table's random numbers for this deal.</summary>
        internal static void ApplyOfflineSeed(TabletopComposition host)
        {
            if (host == null || !TryTakeDealSeed(out int seed)) return;
            if (host.authoritativeRandomValueSource == null) host.authoritativeRandomValueSource = new SystemRandomValueSource(seed);
            else host.authoritativeRandomValueSource.Reseed(seed);
        }

        /// <summary>The floor in play on this computer as a grid (top row first), with the pawns' tiles.</summary>
        public static string DescribeFloor(TabletopComposition host)
        {
            if (host == null || host.trapFloorTemplate == null) return "[Fixture] No Trap Floor table in play.";
            Dictionary<TabletopObjectId, string> pawns = new Dictionary<TabletopObjectId, string>();
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            StringBuilder players = new StringBuilder();
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                string where = host.TrapFloorGame != null && host.TrapFloorGame.TryReadPawnTile(player.PawnId, out TrapFloorCoordinate tile)
                    ? tile.ToString()
                    : "off the floor";
                players.Append($"\n  seat {i + 1}: pawn at {where}");
            }

            return $"{DescribeTemplate(template)}{players}";
        }

        /// <summary>A floor's layout, top row first: each cell is "(x,y) name". Pure, so it is unit tested.</summary>
        public static string DescribeTemplate(TrapFloorTemplateDefinition template)
        {
            if (template == null) return "[Fixture] No floor.";
            StringBuilder text = new StringBuilder();
            text.Append($"[Fixture] Floor {template.FloorIndex + 1} of {template.FloorCount}, {template.GridColumns}×{template.GridRows}, "
                + $"mode {template.ActiveMode.StableId}, Keys needed {template.FloorRequiredKeyCount}:");
            for (int y = template.GridRows; y >= 1; y--)
            {
                text.Append("\n ");
                for (int x = 1; x <= template.GridColumns; x++)
                {
                    TrapFloorCoordinate cell = new TrapFloorCoordinate(x, y);
                    string name = template.TryGetFloorCardId(cell, out TabletopObjectId id)
                        && template.CardLabels.TryGetValue(id, out string label)
                            ? label
                            : "-";
                    text.Append($" ({x},{y}) {Short(name)} |");
                }
            }

            return text.ToString();
        }

        /// <summary>Where the floor's cards of one kind lie (for example "Key"), as cells.</summary>
        public static List<TrapFloorCoordinate> CellsNamed(TrapFloorTemplateDefinition template, string name)
        {
            List<TrapFloorCoordinate> cells = new List<TrapFloorCoordinate>();
            if (template == null || string.IsNullOrEmpty(name)) return cells;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> cell in template.FloorCardIds)
            {
                if (template.CardLabels.TryGetValue(cell.Value, out string label)
                    && label.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cells.Add(cell.Key);
                }
            }

            cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return cells;
        }

        // ---------- P7 (s159) SCENARIO SEAM: deterministic situations without random play ----------
        // Setup only. A scenario puts the turn player's pawn on a tile next to a named target (a Key, an exit, a Trap)
        // so the gameplay under test is the next step, made through the production path (a guest's private pawn
        // request, or the host's own). The setup is written straight into the host's table (no command, no landing
        // rules run, the tile it stands on stays as it was); the host's piece sweep then publishes the pawn's new
        // public place to every guest like any host-side change. Nothing hidden is sent: a pawn's place is public.
        // Editor only (this whole file), host only (it refuses on a joined computer), logged as "[Scenario] SETUP".

        /// <summary>Places the turn player's pawn next to the nearest tile named <paramref name="targetName"/>; returns the step to take.</summary>
        public static string StageActivePawnNextTo(TabletopComposition host, string targetName)
        {
            if (host == null || host.trapFloorTemplate == null || host.TrapFloorGame == null || host.trapFloorTurnState == null || host.matchState == null)
                return "[Scenario] FAIL setup: no Trap Floor table in play.";
            if (host.HasOnline && host.Online.InRoom && !host.Online.IsPrivateHost)
                return "[Scenario] FAIL setup: only the host may stage a scenario.";
            TrapFloorCartridge game = host.TrapFloorGame;
            TrapFloorTurnState turn = host.trapFloorTurnState;
            if (turn.IsCurrentFloorFailed || turn.Phase != TrapFloorTurnPhase.PlayerTurn)
                return "[Scenario] FAIL setup: not a player turn.";
            PlayerId active = turn.ActivePlayerId;
            if (!game.TryGetTrapFloorPlayerSetup(active, out TrapFloorPlayerSetupDefinition setup)
                || !host.matchState.Pawns.TryGetValue(setup.PawnId, out PawnState pawn))
                return "[Scenario] FAIL setup: the turn player has no pawn.";
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            game.TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate from);
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            TrapFloorCoordinate? bestTarget = null, bestStand = null;
            int bestDistance = int.MaxValue;
            int named = 0, collapsed = 0, taken = 0;
            foreach (TrapFloorCoordinate target in CellsNamed(template, targetName))
            {
                named++;
                if (!template.TryGetFloorCardId(target, out TabletopObjectId targetId)) continue;
                if (host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(targetId)) { collapsed++; continue; }
                if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.TryGetClaim(targetId, out _)) { taken++; continue; } // a Key already taken
                for (int d = 0; d < 4; d++)
                {
                    int x = target.X + dx[d], y = target.Y + dy[d];
                    if (x < 1 || y < 1 || x > template.GridColumns || y > template.GridRows || !game.IsWalkableTile(x, y)) continue;
                    int distance = Math.Abs(x - from.X) + Math.Abs(y - from.Y);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestTarget = target;
                    bestStand = new TrapFloorCoordinate(x, y);
                }
            }

            if (!bestStand.HasValue)
                return named == 0
                    ? $"[Scenario] FAIL setup: this floor has no '{targetName}' (floor {template.FloorIndex + 1}, {template.GridColumns}×{template.GridRows})."
                    : $"[Scenario] FAIL setup: '{targetName}' ×{named} on this floor: {collapsed} collapsed, {taken} already taken, the rest have no open neighbour.";
            if (!template.TryGetFloorCardId(bestStand.Value, out TabletopObjectId standId)
                || !host.matchState.Cards.TryGetValue(standId, out CardInstanceState standCard))
                return "[Scenario] FAIL setup: the tile to stand on is missing.";
            PawnView view = null;
            foreach (PawnView candidate in host.Registry.pawnViews)
                if (candidate != null && candidate.IsBound && candidate.ObjectId == setup.PawnId) view = candidate;
            TabletopObjectState state = pawn.BaseState;
            TabletopPose pose = new TabletopPose(standCard.BaseState.Pose.Position, state.Pose.RotationDegrees, state.Pose.Layer, state.Pose.LocalOrder);
            if (state.PhysicalState != null && view != null && view.PhysicalObject != null && host.physicalAuthority != null)
            {
                if (!host.physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(pose, active, view.PhysicalObject.PhysicalCollider, state.IsUserLocked, out PhysicalObjectState placed))
                    return "[Scenario] FAIL setup: the tile's place is not on the table.";
                state.SetPhysicalState(view.PhysicalObject.RestingOnTable(placed));
            }

            state.SetPose(pose);
            view?.ApplyAcceptedState();
            game.ForgetMoveStartForScenario(setup.PawnId, bestStand.Value); // this turn's move now counts from the staged tile
            int stepX = bestTarget.Value.X - bestStand.Value.X, stepY = bestTarget.Value.Y - bestStand.Value.Y;
            return $"[Scenario] SETUP (host-only, no rules run): {host.FormatPlayerName(active)}'s pawn {from} → {bestStand.Value}, next to "
                + $"'{targetName}' at {bestTarget.Value}. Production step to make now: Pawn ({stepX:+0;-0;0},{stepY:+0;-0;0}).";
        }

        /// <summary>The state every computer must agree on, from the host's table (Editor Console only; names no hidden tile).</summary>
        // ---------- Host-side dice seam (Editor only) ----------
        private static readonly Dictionary<TabletopObjectId, Queue<int>> queuedFaces = new Dictionary<TabletopObjectId, Queue<int>>();

        internal static bool TryTakeQueuedFace(TabletopObjectId dieId, int sides, out int face)
        {
            face = 0;
            if (!queuedFaces.TryGetValue(dieId, out Queue<int> q) || q.Count == 0) return false;
            face = q.Dequeue();
            if (face < 1 || face > sides) return false;
            Debug.Log($"[Scenario] DICE: the host's random face for this die was set by the scenario: {face}.");
            return true;
        }

        /// <summary>
        /// Setup only: the next host result for each floor die points at the named exit (its tile stays secret to the
        /// guests; only the host log names it). The throw, the reroll rule and the collapse are the production path.
        /// </summary>
        public static string AimFloorDiceAt(TabletopComposition host, string exitKind)
        {
            if (host == null || (host.HasOnline && host.Online.InRoom && !host.Online.IsPrivateHost)) return "[Scenario] FAIL setup: host only.";
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            if (template == null || host.matchState == null) return "[Scenario] FAIL setup: no Trap Floor table.";
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in template.FloorCardIds)
            {
                if (!template.TryGetFloorCardState(host.matchState, pair.Value, out TrapFloorFloorCardState floor)
                    || floor.Content.Category != TrapFloorFloorContentCategory.SecretExit) continue;
                bool grand = TrapFloorObjectiveUseCase.IsGrandExit(floor.Content);
                if (grand != (exitKind == "Grand Exit")) continue;
                if (host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(pair.Value)) continue;
                Enqueue(template.FloorfallXAxisDieId, pair.Key.X);
                Enqueue(template.FloorfallYAxisDieId, pair.Key.Y);
                return $"[Scenario] SETUP (host-only): the next floor dice will be {pair.Key.X}/{pair.Key.Y}, the {exitKind} "
                    + "(host log only). Now throw both floor dice in the floor turn.";
            }

            return $"[Scenario] FAIL setup: this floor has no standing {exitKind} (floor {template.FloorIndex + 1}, {template.GridColumns}×{template.GridRows}).";
        }

        private static void Enqueue(TabletopObjectId dieId, int face)
        {
            if (!queuedFaces.TryGetValue(dieId, out Queue<int> q)) queuedFaces[dieId] = q = new Queue<int>();
            q.Clear();
            q.Enqueue(face);
        }

        public static string Report(TabletopComposition host)
        {
            if (host == null || host.trapFloorTemplate == null || host.trapFloorTurnState == null) return "[Scenario] REPORT: no Trap Floor table in play.";
            TrapFloorTurnState turn = host.trapFloorTurnState;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            StringBuilder text = new StringBuilder();
            text.Append($"[Scenario] REPORT floor {template.FloorIndex + 1}/{template.FloorCount} ({template.GridColumns}×{template.GridRows}), round {turn.CurrentRound}, "
                + $"phase {turn.Phase}, floor failed {turn.IsCurrentFloorFailed}, won {host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon}, "
                + $"keys {host.trapFloorObjectiveState?.CollectedKeyCount ?? 0}/{host.trapFloorObjectiveState?.RequiredKeyCount ?? 0}, revision {host.matchState?.Revision ?? 0}");
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                PlayerId id = PlayerId.Empty;
                if (host.matchState != null && host.matchState.Seats.TryGetValue(player.SeatId, out ConsoleCards.Core.Domain.Seats.SeatState seat)) id = seat.OccupantPlayerId;
                string where = host.TrapFloorGame.TryReadPawnTile(player.PawnId, out TrapFloorCoordinate tile) ? tile.ToString() : "off the floor";
                string status = id.IsEmpty ? "empty" : host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(id) ? "ESCAPED"
                    : turn.GetPlayerState(id) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound ? "OUT" : "in";
                text.Append($"\n  seat {i + 1}: {status}, pawn {where}, keys held {host.TrapFloorGame.KeysHeldBy(id)}{(turn.ActivePlayerId == id ? " ← turn" : string.Empty)}");
            }

            return text.ToString();
        }

        private static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "-";
            return name.Length <= 16 ? name : name.Substring(0, 16);
        }
    }
}
#endif
