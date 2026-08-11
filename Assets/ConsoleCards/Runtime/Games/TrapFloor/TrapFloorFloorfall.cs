using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Randomness;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Minimal rule context needed to apply the approved round-one starting-corner protection.
    /// </summary>
    public readonly struct TrapFloorFloorfallContext : IEquatable<TrapFloorFloorfallContext>
    {
        public TrapFloorFloorfallContext(int roundNumber)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roundNumber), "Round number must be at least one.");
            }

            RoundNumber = roundNumber;
        }

        public int RoundNumber { get; }

        public bool HasStartingCornerProtection => RoundNumber == 1;

        public bool Equals(TrapFloorFloorfallContext other)
        {
            return RoundNumber == other.RoundNumber;
        }

        public override bool Equals(object obj)
        {
            return obj is TrapFloorFloorfallContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RoundNumber;
        }
    }

    /// <summary>
    /// Successful authoritative result of one Floorfall targeting operation.
    /// </summary>
    public readonly struct TrapFloorFloorfallTarget : IEquatable<TrapFloorFloorfallTarget>
    {
        internal TrapFloorFloorfallTarget(
            DieRoll xAxisRoll,
            DieRoll yAxisRoll,
            TrapFloorCoordinate coordinate,
            TabletopObjectId floorCardId)
        {
            if (xAxisRoll.SideCount != TrapFloorFloorfallService.DieSideCount
                || yAxisRoll.SideCount != TrapFloorFloorfallService.DieSideCount)
            {
                throw new ArgumentException("Trap Floor Floorfall requires two d6 results.");
            }

            if (coordinate.X != xAxisRoll.Value || coordinate.Y != yAxisRoll.Value)
            {
                throw new ArgumentException("The Floorfall coordinate must match its X/Y die results.");
            }

            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("A Floorfall target must identify a Floor Card.", nameof(floorCardId));
            }

            XAxisRoll = xAxisRoll;
            YAxisRoll = yAxisRoll;
            Coordinate = coordinate;
            FloorCardId = floorCardId;
        }

        public DieRoll XAxisRoll { get; }

        public DieRoll YAxisRoll { get; }

        public TrapFloorCoordinate Coordinate { get; }

        public TabletopObjectId FloorCardId { get; }

        public bool Equals(TrapFloorFloorfallTarget other)
        {
            return XAxisRoll.Equals(other.XAxisRoll)
                && YAxisRoll.Equals(other.YAxisRoll)
                && Coordinate.Equals(other.Coordinate)
                && FloorCardId == other.FloorCardId;
        }

        public override bool Equals(object obj)
        {
            return obj is TrapFloorFloorfallTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(XAxisRoll, YAxisRoll, Coordinate, FloorCardId);
        }
    }

    /// <summary>
    /// Game-specific Runtime State for the current/most recent Floorfall target.
    /// It deliberately does not model a collapsed Floor Card.
    /// </summary>
    public sealed class TrapFloorFloorfallState
    {
        public TrapFloorFloorfallTarget? CurrentTarget { get; private set; }

        public bool HasCurrentTarget => CurrentTarget.HasValue;

        internal void SetCurrentTarget(TrapFloorFloorfallTarget target)
        {
            CurrentTarget = target;
        }

        public void Clear()
        {
            CurrentTarget = null;
        }
    }

    /// <summary>
    /// Applies the approved two-d6 coordinate targeting rule against the Template's Board mapping.
    /// </summary>
    public sealed class TrapFloorFloorfallService
    {
        public const int DieSideCount = 6;

        private readonly TrapFloorTemplateDefinition template;
        private readonly IRandomValueSource randomValueSource;
        private readonly TrapFloorFloorfallState state;
        private readonly MatchState matchState;
        private readonly DieState xAxisDieState;
        private readonly DieState yAxisDieState;
        private readonly HashSet<TrapFloorCoordinate> protectedStartingCorners;
        private readonly Die coordinateDie = new Die(DieSideCount);

        public TrapFloorFloorfallService(
            TrapFloorTemplateDefinition template,
            IRandomValueSource randomValueSource,
            TrapFloorFloorfallState state)
            : this(template, null, randomValueSource, state)
        {
        }

        public TrapFloorFloorfallService(
            TrapFloorTemplateDefinition template,
            MatchState matchState,
            IRandomValueSource randomValueSource,
            TrapFloorFloorfallState state)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.matchState = matchState;
            this.randomValueSource = randomValueSource ?? throw new ArgumentNullException(nameof(randomValueSource));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            protectedStartingCorners = new HashSet<TrapFloorCoordinate>();

            if (matchState != null)
            {
                if (!matchState.Dice.TryGetValue(template.FloorfallXAxisDieId, out DieState resolvedXAxisDie)
                    || !matchState.Dice.TryGetValue(template.FloorfallYAxisDieId, out DieState resolvedYAxisDie))
                {
                    throw new ArgumentException(
                        "Trap Floor Match is missing one or both Template-authored Floorfall Dice.",
                        nameof(matchState));
                }

                xAxisDieState = resolvedXAxisDie;
                yAxisDieState = resolvedYAxisDie;

                if (xAxisDieState.SideCount != DieSideCount
                    || yAxisDieState.SideCount != DieSideCount)
                {
                    throw new ArgumentException("Trap Floor Floorfall Dice must both be d6.", nameof(matchState));
                }
            }

            for (int i = 0; i < template.Players.Count; i++)
            {
                protectedStartingCorners.Add(template.Players[i].StartingCorner);
            }
        }

        public TrapFloorFloorfallTarget RollAndResolve(TrapFloorFloorfallContext context)
        {
            if (context.RoundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(context), "Floorfall requires a valid round context.");
            }

            if (matchState != null && matchState.Revision == long.MaxValue)
            {
                throw new InvalidOperationException("Trap Floor Match revision cannot advance for Floorfall.");
            }

            while (true)
            {
                DieRoll xAxisRoll = coordinateDie.Roll(randomValueSource);
                DieRoll yAxisRoll = coordinateDie.Roll(randomValueSource);
                TrapFloorCoordinate coordinate = new TrapFloorCoordinate(
                    xAxisRoll.Value,
                    yAxisRoll.Value);

                if (context.HasStartingCornerProtection
                    && protectedStartingCorners.Contains(coordinate))
                {
                    continue;
                }

                if (!template.TryGetFloorCardId(coordinate, out TabletopObjectId floorCardId))
                {
                    throw new InvalidOperationException(
                        $"Trap Floor Board mapping does not contain Floor Card coordinate {coordinate}.");
                }

                TrapFloorFloorfallTarget target = new TrapFloorFloorfallTarget(
                    xAxisRoll,
                    yAxisRoll,
                    coordinate,
                    floorCardId);
                if (matchState != null)
                {
                    xAxisDieState.SetAcceptedRoll(xAxisRoll);
                    yAxisDieState.SetAcceptedRoll(yAxisRoll);
                    matchState.AdvanceRevision();
                }

                state.SetCurrentTarget(target);
                return target;
            }
        }
    }
}
