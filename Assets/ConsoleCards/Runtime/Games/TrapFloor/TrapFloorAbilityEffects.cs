using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Rules v1.151126, step 5 (owner, 2026-11-17): the Avatar whose special rule applies to one Ability. An Avatar is
    /// known by its card's stable id (the four authored Avatars); any other Avatar has no special rule.
    /// </summary>
    public enum TrapFloorAvatarSpecial
    {
        None = 0,
        /// <summary>Safe Search: reveals 3 adjacent tiles; only 1 stays face up, disarmed.</summary>
        Clairvoyant = 1,
        /// <summary>Jump: may jump over 2 missing tiles instead of 1.</summary>
        Athlete = 2,
        /// <summary>Arm and Disarm: disarms 2 adjacent tiles; rearms up to the 2 nearest triggered traps within 2 tiles.</summary>
        Engineer = 3,
        /// <summary>Shield: also saves them from falling (a collapse, or a Crane or Spring that drops them off the floor).</summary>
        Strategist = 4,
    }

    /// <summary>Rules v1.151126, step 5: the pure rules of the new Abilities, shared by every table and by the screen.</summary>
    public static class TrapFloorAbilityRules
    {
        private static readonly Guid ClairvoyantId = new Guid("54460014-4f4f-4000-8000-001400000005");
        private static readonly Guid AthleteId = new Guid("54460014-4f4f-4000-8000-001400000006");
        private static readonly Guid EngineerId = new Guid("54460014-4f4f-4000-8000-001400000007");
        private static readonly Guid StrategistId = new Guid("54460014-4f4f-4000-8000-001400000009");

        /// <summary>The Avatar in the player's Main slot, as far as a special rule goes.</summary>
        public static TrapFloorAvatarSpecial SpecialOf(MatchState match, TrapFloorTemplateDefinition template, PlayerId player)
        {
            if (match == null || template == null || player.IsEmpty) return TrapFloorAvatarSpecial.None;
            TrapFloorPlayerSetupDefinition setup = SetupOf(match, template, player);
            if (setup == null
                || setup.MainSlotContainerId.IsEmpty
                || !match.Containers.TryGetValue(setup.MainSlotContainerId, out ContainerState slot))
            {
                return TrapFloorAvatarSpecial.None;
            }

            for (int i = 0; i < slot.Count; i++)
            {
                if (!match.Cards.TryGetValue(slot.GetObjectAt(i), out CardInstanceState card)) continue;
                Guid id = card.BaseState.DefinitionId.Value;
                if (id == ClairvoyantId) return TrapFloorAvatarSpecial.Clairvoyant;
                if (id == AthleteId) return TrapFloorAvatarSpecial.Athlete;
                if (id == EngineerId) return TrapFloorAvatarSpecial.Engineer;
                if (id == StrategistId) return TrapFloorAvatarSpecial.Strategist;
            }

            return TrapFloorAvatarSpecial.None;
        }

        /// <summary>The seat setup of a seated player, or null.</summary>
        public static TrapFloorPlayerSetupDefinition SetupOf(MatchState match, TrapFloorTemplateDefinition template, PlayerId player)
        {
            if (match == null || template == null || player.IsEmpty) return null;
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition candidate = template.Players[i];
                if (match.GetSeat(candidate.SeatId).OccupantPlayerId == player) return candidate;
            }

            return null;
        }

        /// <summary>A tile a pawn may stand on: on the grid, with its floor card, not collapsed.</summary>
        public static bool IsStandable(MatchState match, TrapFloorTemplateDefinition template, TrapFloorCollapseState collapse, int x, int y)
        {
            if (!IsOnGrid(template, x, y)) return false;
            return template.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id)
                && match.Cards.ContainsKey(id)
                && (collapse == null || !collapse.IsCollapsed(id));
        }

        /// <summary>A missing floor tile inside the grid (collapsed, or its card gone).</summary>
        public static bool IsHole(MatchState match, TrapFloorTemplateDefinition template, TrapFloorCollapseState collapse, int x, int y) =>
            IsOnGrid(template, x, y) && !IsStandable(match, template, collapse, x, y);

        public static bool IsOnGrid(TrapFloorTemplateDefinition template, int x, int y) =>
            template != null
            && x >= TrapFloorCoordinate.MinimumAxisValue
            && y >= TrapFloorCoordinate.MinimumAxisValue
            && x <= template.GridColumns
            && y <= template.GridRows;

        private static readonly int[] StepX = { 0, 1, 0, -1 };
        private static readonly int[] StepY = { 1, 0, -1, 0 };

        /// <summary>
        /// Run (owner, 2026-11-15 and 2026-11-17): exactly 3 tiles in a straight line; every tile on the way is standable
        /// (a Run never crosses a hole).
        /// </summary>
        public static void RunTargets(
            MatchState match,
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState collapse,
            TrapFloorCoordinate origin,
            List<TabletopObjectId> targets)
        {
            for (int d = 0; d < 4; d++)
            {
                bool clear = true;
                for (int step = 1; step <= 3 && clear; step++)
                {
                    clear = IsStandable(match, template, collapse, origin.X + (StepX[d] * step), origin.Y + (StepY[d] * step));
                }

                if (!clear) continue;
                if (template.TryGetFloorCardId(
                        new TrapFloorCoordinate(origin.X + (StepX[d] * 3), origin.Y + (StepY[d] * 3)),
                        out TabletopObjectId target))
                {
                    targets.Add(target);
                }
            }
        }

        /// <summary>
        /// Jump: straight over 1 missing tile to the standable tile beyond it. The Athlete may also jump over 2 missing
        /// tiles in a row.
        /// </summary>
        public static void JumpTargets(
            MatchState match,
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState collapse,
            TrapFloorCoordinate origin,
            bool athlete,
            List<TabletopObjectId> targets)
        {
            for (int d = 0; d < 4; d++)
            {
                int maxHoles = athlete ? 2 : 1;
                for (int holes = 1; holes <= maxHoles; holes++)
                {
                    bool allHoles = true;
                    for (int step = 1; step <= holes && allHoles; step++)
                    {
                        allHoles = IsHole(match, template, collapse, origin.X + (StepX[d] * step), origin.Y + (StepY[d] * step));
                    }

                    if (!allHoles) break;
                    int x = origin.X + (StepX[d] * (holes + 1));
                    int y = origin.Y + (StepY[d] * (holes + 1));
                    if (IsStandable(match, template, collapse, x, y)
                        && template.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId target))
                    {
                        targets.Add(target);
                    }
                }
            }
        }

        /// <summary>Safe Search: the face-down standable tiles next to the pawn (no diagonals).</summary>
        public static void SafeSearchCandidates(
            MatchState match,
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState collapse,
            TrapFloorCoordinate origin,
            List<TabletopObjectId> candidates)
        {
            for (int d = 0; d < 4; d++)
            {
                int x = origin.X + StepX[d];
                int y = origin.Y + StepY[d];
                if (!IsStandable(match, template, collapse, x, y)
                    || !template.TryGetFloorCardState(match, IdAt(template, x, y), out TrapFloorFloorCardState state)
                    || state.IsRevealed)
                {
                    continue;
                }

                candidates.Add(state.ObjectId);
            }
        }

        /// <summary>
        /// Arm and Disarm (owner, 2026-11-17: any tile on the floor): the tiles a player may pick. Every standable tile;
        /// the Engineer's second tile must be next to the first (rulebook: "2 adjacent tiles").
        /// </summary>
        public static bool IsDisarmPickValid(
            MatchState match,
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState collapse,
            IReadOnlyList<TabletopObjectId> picks,
            bool engineer)
        {
            if (picks == null || picks.Count < 1 || picks.Count > (engineer ? 2 : 1)) return false;
            TrapFloorCoordinate first = default;
            for (int i = 0; i < picks.Count; i++)
            {
                if (!template.TryGetFloorCoordinate(picks[i], out TrapFloorCoordinate tile)
                    || !IsStandable(match, template, collapse, tile.X, tile.Y))
                {
                    return false;
                }

                if (i == 0)
                {
                    first = tile;
                    continue;
                }

                if (picks[i] == picks[0] || Distance(first, tile) != 1) return false;
            }

            return true;
        }

        /// <summary>
        /// The triggered traps that may be rearmed, nearest first (steps from <paramref name="from"/>): face up, a Trap,
        /// not collapsed, not disarmed, not just disarmed, and with no pawn on it. <paramref name="maxDistance"/> limits
        /// the distance (0: any). Ties keep the grid order (row, then column).
        /// </summary>
        public static void RearmCandidates(
            MatchState match,
            TrapFloorTemplateDefinition template,
            TrapFloorCollapseState collapse,
            TrapFloorAbilityResolutionState state,
            TrapFloorCoordinate from,
            int maxDistance,
            IReadOnlyList<TabletopObjectId> excluded,
            ICollection<TrapFloorCoordinate> pawnTiles,
            List<TabletopObjectId> candidates)
        {
            candidates.Clear();
            List<int> distances = new List<int>();
            for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= template.GridRows; y++)
            {
                for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= template.GridColumns; x++)
                {
                    if (!IsStandable(match, template, collapse, x, y)) continue;
                    TabletopObjectId id = IdAt(template, x, y);
                    if (!template.TryGetFloorCardState(match, id, out TrapFloorFloorCardState floor)
                        || !floor.IsRevealed
                        || floor.Content.Category != TrapFloorFloorContentCategory.Trap
                        || (state != null && state.IsDisarmed(id))
                        || Contains(excluded, id)
                        || (pawnTiles != null && pawnTiles.Contains(new TrapFloorCoordinate(x, y))))
                    {
                        continue;
                    }

                    int distance = Distance(from, new TrapFloorCoordinate(x, y));
                    if (maxDistance > 0 && distance > maxDistance) continue;
                    int at = distances.Count;
                    while (at > 0 && distances[at - 1] > distance) at--;
                    distances.Insert(at, distance);
                    candidates.Insert(at, id);
                }
            }
        }

        /// <summary>
        /// The traps a use of Arm and Disarm rearms: the nearest <paramref name="count"/> of <paramref name="candidates"/>
        /// (sorted nearest first). The player's own choice among traps at the same distance is used when it is a valid
        /// one (<paramref name="chosen"/>); otherwise the grid order decides.
        /// </summary>
        public static List<TabletopObjectId> PickRearm(
            TrapFloorTemplateDefinition template,
            TrapFloorCoordinate from,
            List<TabletopObjectId> candidates,
            int count,
            IReadOnlyList<TabletopObjectId> chosen)
        {
            List<TabletopObjectId> result = new List<TabletopObjectId>();
            if (count <= 0 || candidates.Count == 0) return result;
            int take = Math.Min(count, candidates.Count);
            // The distance of the last trap that must be taken: every closer trap is taken; at that distance, choose.
            int edge = DistanceOf(template, from, candidates[take - 1]);
            List<TabletopObjectId> tied = new List<TabletopObjectId>();
            for (int i = 0; i < candidates.Count; i++)
            {
                int distance = DistanceOf(template, from, candidates[i]);
                if (distance < edge) result.Add(candidates[i]);
                else if (distance == edge) tied.Add(candidates[i]);
            }

            int fromTie = take - result.Count;
            if (chosen != null)
            {
                for (int i = 0; i < chosen.Count && fromTie > 0; i++)
                {
                    if (tied.Contains(chosen[i]) && !result.Contains(chosen[i]))
                    {
                        result.Add(chosen[i]);
                        fromTie--;
                    }
                }
            }

            for (int i = 0; i < tied.Count && fromTie > 0; i++)
            {
                if (result.Contains(tied[i])) continue;
                result.Add(tied[i]);
                fromTie--;
            }

            return result;
        }

        /// <summary>The traps at the edge distance that the player may choose between (empty: no choice to make).</summary>
        public static List<TabletopObjectId> RearmTie(
            TrapFloorTemplateDefinition template,
            TrapFloorCoordinate from,
            List<TabletopObjectId> candidates,
            int count,
            out int chooseCount)
        {
            chooseCount = 0;
            List<TabletopObjectId> tied = new List<TabletopObjectId>();
            if (count <= 0 || candidates.Count == 0) return tied;
            int take = Math.Min(count, candidates.Count);
            int edge = DistanceOf(template, from, candidates[take - 1]);
            int closer = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                int distance = DistanceOf(template, from, candidates[i]);
                if (distance < edge) closer++;
                else if (distance == edge) tied.Add(candidates[i]);
            }

            chooseCount = take - closer;
            if (tied.Count <= chooseCount) tied.Clear(); // all of them are taken: nothing to choose
            return tied;
        }

        private static bool Contains(IReadOnlyList<TabletopObjectId> list, TabletopObjectId id)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id) return true;
            }

            return false;
        }

        public static int Distance(TrapFloorCoordinate a, TrapFloorCoordinate b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private static int DistanceOf(TrapFloorTemplateDefinition template, TrapFloorCoordinate from, TabletopObjectId id) =>
            template.TryGetFloorCoordinate(id, out TrapFloorCoordinate tile) ? Distance(from, tile) : int.MaxValue;

        private static TabletopObjectId IdAt(TrapFloorTemplateDefinition template, int x, int y) =>
            template.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id) ? id : TabletopObjectId.Empty;

        /// <summary>
        /// A card of this player's lying loose on the table: a bought or taken Ability (its owner), or a starting Ability
        /// of their seat or Avatar. The game lays these beside the player's Console.
        /// </summary>
        public static bool IsLooseAbilityOf(
            MatchState match,
            TrapFloorTemplateDefinition template,
            PlayerId player,
            TabletopObjectId cardId,
            CardInstanceState card)
        {
            if (match == null || template == null || card == null || player.IsEmpty || !card.BaseState.ContainerId.IsEmpty) return false;
            if (card.BaseState.OwnerPlayerId == player) return true;
            for (int seatIndex = 0; seatIndex < template.Players.Count; seatIndex++)
            {
                TrapFloorPlayerSetupDefinition setup = template.Players[seatIndex];
                if (match.GetSeat(setup.SeatId).OccupantPlayerId != player) continue;
                return card.BaseState.OwnerPlayerId.IsEmpty
                    && TrapFloorRunCarry.IsSeatsStartingAbility(cardId, seatIndex, TrapFloorRunCarry.SeatAvatarIndex(match, template, setup));
            }

            return false;
        }

        /// <summary>
        /// The card that saves this player from falling (owner, 2026-11-17 assumptions, doc 19 §17): Insurance first, then
        /// Shield when the player is the Strategist. The card must lie in the player's hand, their Ability area or their
        /// Console, or loose beside it (theirs), and not be used yet this round. Empty: nothing saves them.
        /// </summary>
        public static TabletopObjectId FindSaveCard(
            MatchState match,
            TrapFloorTemplateDefinition template,
            TrapFloorAbilityResolutionState state,
            Func<ObjectDefinitionId, CardDefinitionData> definitionOf,
            PlayerId player,
            int round)
        {
            if (match == null || template == null || definitionOf == null) return TabletopObjectId.Empty;
            TrapFloorPlayerSetupDefinition setup = SetupOf(match, template, player);
            if (setup == null) return TabletopObjectId.Empty;
            bool strategist = SpecialOf(match, template, player) == TrapFloorAvatarSpecial.Strategist;
            TabletopObjectId shield = TabletopObjectId.Empty;
            List<ContainerId> places = new List<ContainerId>(8)
            {
                setup.HandContainerId,
                setup.ActionAbilityAreaContainerId,
                setup.MainSlotContainerId,
            };
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++) places.Add(setup.SideSlotContainerIds[i]);
            for (int p = 0; p < places.Count; p++)
            {
                if (places[p].IsEmpty || !match.Containers.TryGetValue(places[p], out ContainerState container)) continue;
                for (int i = 0; i < container.Count; i++)
                {
                    TabletopObjectId cardId = container.GetObjectAt(i);
                    if (!match.Cards.TryGetValue(cardId, out CardInstanceState card)) continue;
                    if (state != null && state.HasAbilityActivatedThisTurn(cardId, player, round)) continue;
                    CardDefinitionData definition = definitionOf(card.BaseState.DefinitionId);
                    if (definition == null) continue;
                    TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
                    if (effect == TrapFloorAbilityEffect.Insurance) return cardId;
                    if (strategist && effect == TrapFloorAbilityEffect.Shield && shield.IsEmpty) shield = cardId;
                }
            }

            // The player's own Abilities lying beside their Console (bought, taken or starting ones), in a fixed order.
            TabletopObjectId looseShield = TabletopObjectId.Empty;
            TabletopObjectId looseInsurance = TabletopObjectId.Empty;
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in match.Cards)
            {
                if (!IsLooseAbilityOf(match, template, player, pair.Key, pair.Value)) continue;
                if (state != null && state.HasAbilityActivatedThisTurn(pair.Key, player, round)) continue;
                CardDefinitionData definition = definitionOf(pair.Value.BaseState.DefinitionId);
                if (definition == null) continue;
                TrapFloorAbilityEffect effect = TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
                if (effect == TrapFloorAbilityEffect.Insurance && (looseInsurance.IsEmpty || pair.Key.Value.CompareTo(looseInsurance.Value) < 0))
                    looseInsurance = pair.Key;
                if (strategist && effect == TrapFloorAbilityEffect.Shield && (looseShield.IsEmpty || pair.Key.Value.CompareTo(looseShield.Value) < 0))
                    looseShield = pair.Key;
            }

            if (!looseInsurance.IsEmpty) return looseInsurance;
            return shield.IsEmpty ? looseShield : shield;
        }
    }

    /// <summary>Rules v1.151126, step 5: Arm and Disarm, Safe Search and falls state (kept with the rules state).</summary>
    public sealed partial class TrapFloorAbilityResolutionState
    {
        private readonly HashSet<TabletopObjectId> disarmedFloors = new HashSet<TabletopObjectId>();
        private readonly HashSet<TabletopObjectId> rearmedFloors = new HashSet<TabletopObjectId>();
        private readonly List<TrapFloorSavedFromFall> savedFromFalls = new List<TrapFloorSavedFromFall>();

        /// <summary>A Trap tile disarmed by Arm and Disarm or the Clairvoyant's Safe Search: harmless, never rearmed.</summary>
        public bool IsDisarmed(TabletopObjectId floorCardId) => disarmedFloors.Contains(floorCardId);

        /// <summary>Every disarmed Trap tile (shown as harmless).</summary>
        public IReadOnlyCollection<TabletopObjectId> DisarmedFloors => disarmedFloors;

        /// <summary>A Trap tile turned back face down by Arm and Disarm: it triggers again when a pawn lands on it.</summary>
        public bool IsRearmed(TabletopObjectId floorCardId) => rearmedFloors.Contains(floorCardId);

        /// <summary>The player stands on that hole because an Ability saved them from falling there.</summary>
        public bool IsSavedOnHole(PlayerId player, TabletopObjectId holeFloorCardId)
        {
            for (int i = 0; i < savedFromFalls.Count; i++)
            {
                if (savedFromFalls[i].PlayerId == player && savedFromFalls[i].FloorCardId == holeFloorCardId) return true;
            }

            return false;
        }

        internal void MarkDisarmed(TabletopObjectId floorCardId)
        {
            disarmedFloors.Add(floorCardId);
            rearmedFloors.Remove(floorCardId);
        }

        internal void MarkRearmed(TabletopObjectId floorCardId) => rearmedFloors.Add(floorCardId);

        internal void MarkSavedOnHole(PlayerId player, TabletopObjectId holeFloorCardId)
        {
            if (!IsSavedOnHole(player, holeFloorCardId)) savedFromFalls.Add(new TrapFloorSavedFromFall(player, holeFloorCardId));
        }

        // A rearmed tile turned face up again by a landing triggers again (a new waiting record).
        private bool TakeRearmed(TabletopObjectId floorCardId) => rearmedFloors.Remove(floorCardId);

        internal TabletopObjectId[] CopyDisarmed()
        {
            TabletopObjectId[] copy = new TabletopObjectId[disarmedFloors.Count];
            disarmedFloors.CopyTo(copy);
            return copy;
        }

        internal TabletopObjectId[] CopyRearmed()
        {
            TabletopObjectId[] copy = new TabletopObjectId[rearmedFloors.Count];
            rearmedFloors.CopyTo(copy);
            return copy;
        }

        internal TrapFloorSavedFromFall[] CopySavedFromFalls() => savedFromFalls.ToArray();

        internal void RestoreArming(
            IEnumerable<TabletopObjectId> disarmed,
            IEnumerable<TabletopObjectId> rearmed,
            IEnumerable<TrapFloorSavedFromFall> saved)
        {
            disarmedFloors.Clear();
            rearmedFloors.Clear();
            savedFromFalls.Clear();
            if (disarmed != null) foreach (TabletopObjectId id in disarmed) disarmedFloors.Add(id);
            if (rearmed != null) foreach (TabletopObjectId id in rearmed) rearmedFloors.Add(id);
            if (saved != null) savedFromFalls.AddRange(saved);
        }
    }

    /// <summary>A player an Ability saved from falling onto a hole (they stand on it until they move off).</summary>
    public readonly struct TrapFloorSavedFromFall
    {
        public TrapFloorSavedFromFall(PlayerId playerId, TabletopObjectId floorCardId)
        {
            PlayerId = playerId;
            FloorCardId = floorCardId;
        }

        public PlayerId PlayerId { get; }
        public TabletopObjectId FloorCardId { get; }
    }

    /// <summary>Step 5: the tiles a player picked for Arm and Disarm or Safe Search (what they chose, never a result).</summary>
    public sealed class TrapFloorAbilityChoice
    {
        public TrapFloorAbilityChoice(IEnumerable<TabletopObjectId> tiles, IEnumerable<TabletopObjectId> rearmChoices = null)
        {
            Tiles = tiles == null ? Array.Empty<TabletopObjectId>() : new List<TabletopObjectId>(tiles).ToArray();
            RearmChoices = rearmChoices == null ? Array.Empty<TabletopObjectId>() : new List<TabletopObjectId>(rearmChoices).ToArray();
        }

        /// <summary>Arm and Disarm: the tile (the Engineer: 1 or 2 next to each other). Safe Search: the tile kept face up.</summary>
        public IReadOnlyList<TabletopObjectId> Tiles { get; }

        /// <summary>Arm and Disarm: which of the Traps at the same distance to rearm (empty: the grid order).</summary>
        public IReadOnlyList<TabletopObjectId> RearmChoices { get; }
    }

    /// <summary>Step 5: the floor tiles one use of an Ability turned over (so every screen turns the same tiles).</summary>
    public sealed class TrapFloorAbilityTileChanges
    {
        public List<TabletopObjectId> Revealed { get; } = new List<TabletopObjectId>(3);
        public List<TabletopObjectId> Disarmed { get; } = new List<TabletopObjectId>(2);
        public List<TabletopObjectId> Rearmed { get; } = new List<TabletopObjectId>(2);
        /// <summary>Picked for Arm and Disarm but not a Trap: no effect (a face-down tile stays face down).</summary>
        public List<TabletopObjectId> NotTrap { get; } = new List<TabletopObjectId>(2);
    }

    public sealed partial class TrapFloorAbilityResolutionState
    {
        /// <summary>Step 5 (Lasso): the player has used any Ability this round.</summary>
        public bool UsedAnyAbilityThisRound(PlayerId player, int round)
        {
            foreach (TrapFloorAbilityActivationRecord record in abilityActivations)
            {
                if (record.PlayerId == player && record.Round == round) return true;
            }

            return false;
        }
    }

    /// <summary>Rules v1.151126, step 5: Jump, Arm and Disarm, Safe Search, Lasso and the saves from a fall.</summary>
    public sealed partial class TrapFloorAbilityResolutionService
    {
        /// <summary>
        /// Step 5: the card that keeps a player from falling when a Spring or Crane drops them off the floor (Insurance,
        /// or the Strategist's Shield); empty: none. The executor sets it from this table's own cards.
        /// </summary>
        public Func<PlayerId, TabletopObjectId> FallSaver { get; set; }

        private TrapFloorAbilityActivationResult ActivateOnFloor(
            MatchState matchState,
            ConsoleCardInserted insertion,
            TrapFloorAbilityEffect effect,
            TrapFloorAbilityChoice choice)
        {
            PlayerId player = turnState.ActivePlayerId;
            if (template == null || collapseState == null)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);
            if (insertion.ConsoleOwnerPlayerId != player)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.ActorIsNotActivePlayer, effect);
            TrapFloorPlayerSetupDefinition setup = TrapFloorAbilityRules.SetupOf(matchState, template, player);
            if (setup == null || !matchState.Pawns.TryGetValue(setup.PawnId, out PawnState pawn))
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);
            bool onFloor = TryGetPawnFloorCoordinate(matchState, pawn, out TrapFloorCoordinate origin);
            TrapFloorAvatarSpecial special = TrapFloorAbilityRules.SpecialOf(matchState, template, player);
            TrapFloorAbilityTileChanges tiles = new TrapFloorAbilityTileChanges();
            TrapFloorTrapResolutionRecord trap = null;
            TrapFloorActivityKind kind;

            if (effect == TrapFloorAbilityEffect.Jump)
            {
                if (!onFloor)
                    return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);
                List<TabletopObjectId> targets = new List<TabletopObjectId>(8);
                TrapFloorAbilityRules.JumpTargets(
                    matchState, template, collapseState, origin, special == TrapFloorAvatarSpecial.Athlete, targets);
                if (targets.Count == 0)
                    return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.NoJumpDestination, effect);
                // The jump is a guided move to a lit tile, like Run.
                state.BeginRushAssistance(new TrapFloorRushAssistanceState(player, setup.PawnId, PawnPosition(pawn), origin, targets));
                tiles = null;
                kind = TrapFloorActivityKind.UsedJump;
            }
            else if (effect == TrapFloorAbilityEffect.SafeSearch)
            {
                if (!onFloor)
                    return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);
                List<TabletopObjectId> candidates = new List<TabletopObjectId>(4);
                TrapFloorAbilityRules.SafeSearchCandidates(matchState, template, collapseState, origin, candidates);
                if (choice == null || choice.Tiles.Count != 1 || !candidates.Contains(choice.Tiles[0]))
                    return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.NoTargetChosen, effect);
                TabletopObjectId tile = choice.Tiles[0];
                if (!template.TryGetFloorCardState(matchState, tile, out TrapFloorFloorCardState floor))
                    return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.NoTargetChosen, effect);
                matchState.Cards[tile].SetFace(CardFace.FaceUp);
                tiles.Revealed.Add(tile);
                // The Clairvoyant: the tile kept face up is disarmed (a Trap there never triggers).
                if (special == TrapFloorAvatarSpecial.Clairvoyant
                    && floor.Content.Category == TrapFloorFloorContentCategory.Trap)
                {
                    state.MarkDisarmed(tile);
                    tiles.Disarmed.Add(tile);
                }

                kind = TrapFloorActivityKind.UsedSafeSearch;
            }
            else
            {
                bool engineer = special == TrapFloorAvatarSpecial.Engineer;
                if (choice == null
                    || !TrapFloorAbilityRules.IsDisarmPickValid(matchState, template, collapseState, choice.Tiles, engineer))
                    return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.NoTargetChosen, effect);
                for (int i = 0; i < choice.Tiles.Count; i++)
                {
                    TabletopObjectId tile = choice.Tiles[i];
                    if (!template.TryGetFloorCardState(matchState, tile, out TrapFloorFloorCardState floor)
                        || floor.Content.Category != TrapFloorFloorContentCategory.Trap
                        || state.IsDisarmed(tile))
                    {
                        tiles.NotTrap.Add(tile); // no effect: the turn is used
                        continue;
                    }

                    if (!floor.IsRevealed)
                    {
                        matchState.Cards[tile].SetFace(CardFace.FaceUp);
                        tiles.Revealed.Add(tile);
                    }

                    state.MarkDisarmed(tile);
                    tiles.Disarmed.Add(tile);
                    if (state.TryGetTrap(tile, out TrapFloorTrapResolutionRecord waiting) && waiting.IsPending)
                    {
                        waiting.SetDisposition(TrapFloorTrapResolutionDisposition.Disarmed);
                        trap = waiting;
                    }
                }

                // Owner, 2026-11-17: Arm and Disarm work together: disarming a Trap rearms the nearest triggered one.
                if (tiles.Disarmed.Count > 0)
                {
                    TrapFloorCoordinate from = origin;
                    if (!onFloor) template.TryGetFloorCoordinate(choice.Tiles[0], out from);
                    List<TrapFloorCoordinate> pawnTiles = PawnTiles(matchState);
                    List<TabletopObjectId> candidates = new List<TabletopObjectId>(8);
                    TrapFloorAbilityRules.RearmCandidates(
                        matchState, template, collapseState, state, from, engineer ? 2 : 0, choice.Tiles, pawnTiles, candidates);
                    List<TabletopObjectId> rearm = TrapFloorAbilityRules.PickRearm(
                        template, from, candidates, engineer ? 2 : 1, choice.RearmChoices);
                    for (int i = 0; i < rearm.Count; i++)
                    {
                        matchState.Cards[rearm[i]].SetFace(CardFace.FaceDown);
                        state.MarkRearmed(rearm[i]);
                        tiles.Rearmed.Add(rearm[i]);
                    }
                }

                kind = TrapFloorActivityKind.UsedArmAndDisarm;
            }

            state.RecordAbilityActivation(insertion.CardInstanceId, player, turnState.CurrentRound);
            activityFeed.RecordAbilityActivity(insertion.AcceptedRevision, insertion.ActorPlayerId, kind);
            return TrapFloorAbilityActivationResult.AcceptedTiles(effect, trap, tiles);
        }

        // Every pawn on the floor (a rearmed Trap is never put back under a pawn).
        private List<TrapFloorCoordinate> PawnTiles(MatchState matchState)
        {
            List<TrapFloorCoordinate> result = new List<TrapFloorCoordinate>(template.Players.Count);
            for (int i = 0; i < template.Players.Count; i++)
            {
                if (matchState.Pawns.TryGetValue(template.Players[i].PawnId, out PawnState pawn)
                    && TryGetPawnFloorCoordinate(matchState, pawn, out TrapFloorCoordinate tile))
                {
                    result.Add(tile);
                }
            }

            return result;
        }

        /// <summary>
        /// Step 5, Lasso (Team Play): a teammate who used no other Ability this round pulls the turn player, whose Trap
        /// waits, to their own tile: the Trap does not apply, and the turn player's pawn is guided to that lit tile.
        /// </summary>
        private TrapFloorAbilityActivationResult ActivateLasso(MatchState matchState, ConsoleCardInserted insertion)
        {
            const TrapFloorAbilityEffect effect = TrapFloorAbilityEffect.Lasso;
            PlayerId user = insertion.ActorPlayerId;
            PlayerId active = turnState.ActivePlayerId;
            int round = turnState.CurrentRound;
            if (template == null || collapseState == null)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);
            if (turnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || turnState.IsCurrentFloorFailed
                || user.IsEmpty
                || user == active
                || insertion.ConsoleOwnerPlayerId != user
                || !turnState.ContainsPlayer(user)
                || turnState.GetPlayerState(user) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                || state.UsedAnyAbilityThisRound(user, round))
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.LassoNotAllowed, effect);
            if (!state.TryGetLatestPendingTrap(active, false, out TrapFloorTrapResolutionRecord trap))
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.NoUnresolvedTrap, effect);
            TrapFloorPlayerSetupDefinition userSetup = TrapFloorAbilityRules.SetupOf(matchState, template, user);
            TrapFloorPlayerSetupDefinition activeSetup = TrapFloorAbilityRules.SetupOf(matchState, template, active);
            if (userSetup == null
                || activeSetup == null
                || !matchState.Pawns.TryGetValue(userSetup.PawnId, out PawnState userPawn)
                || !matchState.Pawns.TryGetValue(activeSetup.PawnId, out PawnState activePawn)
                || !TryGetPawnFloorCoordinate(matchState, userPawn, out TrapFloorCoordinate userTile)
                || !TrapFloorAbilityRules.IsStandable(matchState, template, collapseState, userTile.X, userTile.Y)
                || !template.TryGetFloorCardId(userTile, out TabletopObjectId target))
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);
            if (!TryGetPawnFloorCoordinate(matchState, activePawn, out TrapFloorCoordinate origin)
                && !template.TryGetFloorCoordinate(trap.FloorCardId, out origin))
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.PawnFloorUnavailable, effect);

            trap.SetDisposition(TrapFloorTrapResolutionDisposition.Dodged);
            state.RecordAbilityActivation(insertion.CardInstanceId, user, round);
            state.BeginDodgeAssistance(new TrapFloorDodgeAssistanceState(
                active, activeSetup.PawnId, PawnPosition(activePawn), origin, new[] { target }));
            activityFeed.RecordAbilityActivity(insertion.AcceptedRevision, user, TrapFloorActivityKind.UsedLasso);
            return TrapFloorAbilityActivationResult.Accepted(effect, trap);
        }

        // Step 5: a Spring or Crane would drop the player off the floor, and a card saves them: the Trap is stopped and the
        // pawn stays where it is.
        private TrapFloorLaunchResult SaveFromLaunch(
            MatchState matchState,
            CommandContext context,
            TrapFloorTrapResolutionRecord trap,
            TrapFloorFloorCardState trapTile,
            TabletopObjectId card)
        {
            trap.SetDisposition(TrapFloorTrapResolutionDisposition.Shielded);
            state.RecordAbilityActivation(card, trap.AffectedPlayerId, turnState.CurrentRound);
            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            activityFeed.RecordAbilityActivity(revision, trap.AffectedPlayerId, TrapFloorActivityKind.SavedFromFall);
            return TrapFloorLaunchResult.Saved(revision, trapTile.ObjectId, trapTile.Coordinate, card);
        }

        /// <summary>
        /// Step 5: the tile under the player collapsed and <paramref name="card"/> (Insurance, or the Strategist's Shield)
        /// saves them: they stay on the hole and step off on their next turn. The caller checked the card and the hole on
        /// its own table. False when it does not apply.
        /// </summary>
        public bool SaveFromCollapse(
            MatchState matchState,
            CommandContext context,
            PlayerId player,
            TabletopObjectId holeFloorCardId,
            TabletopObjectId card)
        {
            if (matchState == null
                || template == null
                || collapseState == null
                || matchState.Id != state.MatchId
                || player.IsEmpty
                || card.IsEmpty
                || !collapseState.IsCollapsed(holeFloorCardId)
                || !turnState.ContainsPlayer(player)
                || turnState.GetPlayerState(player) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                || state.HasAbilityActivatedThisTurn(card, player, turnState.CurrentRound)
                || state.IsSavedOnHole(player, holeFloorCardId))
            {
                return false;
            }

            state.RecordAbilityActivation(card, player, turnState.CurrentRound);
            state.MarkSavedOnHole(player, holeFloorCardId);
            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified,
                AuthoritativeActionRecordMode.None);
            activityFeed.RecordAbilityActivity(revision, player, TrapFloorActivityKind.SavedFromFall);
            return true;
        }
    }
}
