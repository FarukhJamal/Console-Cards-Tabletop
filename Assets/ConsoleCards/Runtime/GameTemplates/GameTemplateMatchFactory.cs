using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.GameTemplates
{
    public sealed class GameTemplateMatchBuildResult
    {
        private GameTemplateMatchBuildResult(
            GameTemplateMatchSession session,
            IEnumerable<GameTemplateValidationIssue> issues)
        {
            Session = session;
            Issues = new ReadOnlyCollection<GameTemplateValidationIssue>(
                new List<GameTemplateValidationIssue>(issues));
        }

        public bool Succeeded => Session != null;

        public GameTemplateMatchSession Session { get; }

        public IReadOnlyList<GameTemplateValidationIssue> Issues { get; }

        internal static GameTemplateMatchBuildResult Success(GameTemplateMatchSession session)
        {
            return new GameTemplateMatchBuildResult(
                session ?? throw new ArgumentNullException(nameof(session)),
                Array.Empty<GameTemplateValidationIssue>());
        }

        internal static GameTemplateMatchBuildResult Failure(
            IEnumerable<GameTemplateValidationIssue> issues)
        {
            return new GameTemplateMatchBuildResult(null, issues);
        }
    }

    /// <summary>
    /// Validates and resolves the complete Template before exposing a constructed Match.
    /// </summary>
    public sealed class GameTemplateMatchFactory
    {
        private readonly GameTemplateValidator validator = new GameTemplateValidator();

        public GameTemplateMatchBuildResult TryCreate(
            GameTemplate template,
            GameTemplateContentCatalog content,
            IReadOnlyList<PlayerId> activePlayerIds,
            MatchId matchId)
        {
            GameTemplateValidationResult validation = validator.Validate(template, content, activePlayerIds);
            if (!validation.IsValid)
            {
                return GameTemplateMatchBuildResult.Failure(validation.Issues);
            }

            if (matchId.IsEmpty)
            {
                return GameTemplateMatchBuildResult.Failure(
                    new[]
                    {
                        new GameTemplateValidationIssue("MatchIdEmpty", "Match ID cannot be empty."),
                    });
            }

            try
            {
                content.TryResolvePlayerLayout(template.PlayerLayoutId, out PlayerLayoutDefinition playerLayout);
                MatchState matchState = ConstructMatch(
                    template,
                    playerLayout,
                    activePlayerIds,
                    matchId);
                GameTemplateInitialSnapshot baseline = GameTemplateInitialSnapshot.Capture(matchState);
                GameTemplateMatchSession session = new GameTemplateMatchSession(
                    template,
                    playerLayout,
                    matchState,
                    baseline);
                return GameTemplateMatchBuildResult.Success(session);
            }
            catch (ArgumentException exception)
            {
                return ConstructionFailure(exception);
            }
            catch (InvalidOperationException exception)
            {
                return ConstructionFailure(exception);
            }
            catch (KeyNotFoundException exception)
            {
                return ConstructionFailure(exception);
            }
            catch (OverflowException exception)
            {
                return ConstructionFailure(exception);
            }
        }

        private static MatchState ConstructMatch(
            GameTemplate template,
            PlayerLayoutDefinition playerLayout,
            IReadOnlyList<PlayerId> activePlayerIds,
            MatchId matchId)
        {
            Dictionary<ContainerId, ContainerState> containers =
                new Dictionary<ContainerId, ContainerState>();
            List<ContainerPlacementState> placements = new List<ContainerPlacementState>();
            for (int i = 0; i < template.Containers.Count; i++)
            {
                GameTemplateContainerDefinition definition = template.Containers[i];
                ContainerState container = new ContainerState(
                    definition.Id,
                    definition.Kind,
                    definition.OwnerSeatId,
                    definition.Visibility,
                    definition.Capacity);
                containers.Add(container.Id, container);
                if (definition.HasTabletopPose)
                {
                    placements.Add(new ContainerPlacementState(container.Id, definition.TabletopPose));
                }
            }

            List<CardInstanceState> cards = new List<CardInstanceState>();
            List<PawnState> pawns = new List<PawnState>();
            List<TokenState> tokens = new List<TokenState>();
            Dictionary<TabletopObjectId, TabletopObjectState> objectStates =
                new Dictionary<TabletopObjectId, TabletopObjectState>();

            Dictionary<SeatId, PlayerId> playersBySeatId = MapPlayersToSeats(template, activePlayerIds);
            for (int i = 0; i < template.Objects.Count; i++)
            {
                GameTemplateObjectInstanceDefinition definition = template.Objects[i];
                PlayerId ownerPlayerId = definition.OwnerSeatId.IsEmpty
                    ? PlayerId.Empty
                    : playersBySeatId[definition.OwnerSeatId];
                TabletopObjectState baseState = new TabletopObjectState(
                    definition.Id,
                    definition.DefinitionId,
                    definition.Kind,
                    definition.Pose,
                    ContainerId.Empty,
                    ownerPlayerId,
                    definition.Visibility,
                    definition.IsUserLocked);
                objectStates.Add(baseState.Id, baseState);

                switch (definition.Kind)
                {
                    case TabletopObjectKind.Card:
                        cards.Add(new CardInstanceState(baseState, definition.InitialCardFace));
                        break;
                    case TabletopObjectKind.Pawn:
                        pawns.Add(new PawnState(baseState));
                        break;
                    case TabletopObjectKind.Token:
                        tokens.Add(new TokenState(baseState));
                        break;
                    default:
                        throw new InvalidOperationException("Template contains an unsupported Tabletop Object kind.");
                }
            }

            ContainerTransferService transferService = new ContainerTransferService();
            for (int membershipIndex = 0; membershipIndex < template.Memberships.Count; membershipIndex++)
            {
                GameTemplateContainerMembership membership = template.Memberships[membershipIndex];
                ContainerState destination = containers[membership.ContainerId];
                for (int objectIndex = 0; objectIndex < membership.OrderedObjectIds.Count; objectIndex++)
                {
                    ContainerTransferResult result = transferService.PlaceIntoContainer(
                        objectStates[membership.OrderedObjectIds[objectIndex]],
                        destination,
                        objectIndex);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException($"Initial membership construction failed: {result.Error}.");
                    }
                }
            }

            List<GameTemplateSeatDefinition> orderedSeatDefinitions =
                new List<GameTemplateSeatDefinition>(template.Seats);
            orderedSeatDefinitions.Sort(
                (left, right) => left.PlayerLayoutSeatIndex.CompareTo(right.PlayerLayoutSeatIndex));
            List<SeatState> seats = new List<SeatState>(orderedSeatDefinitions.Count);
            for (int i = 0; i < orderedSeatDefinitions.Count; i++)
            {
                GameTemplateSeatDefinition definition = orderedSeatDefinitions[i];
                playerLayout.TryGetSeat(definition.PlayerLayoutSeatIndex, out PlayerSeatLayoutEntry layoutSeat);
                seats.Add(new SeatState(
                    definition.SeatId,
                    layoutSeat.PlayerZonePose,
                    definition.HandContainerId,
                    new ConsoleState(definition.SeatId, definition.ConsoleSlotContainerIds),
                    playersBySeatId[definition.SeatId],
                    SeatStatus.Occupied));
            }

            List<PlayAreaState> playAreas = new List<PlayAreaState>(template.PlayAreas.Count);
            for (int i = 0; i < template.PlayAreas.Count; i++)
            {
                GameTemplatePlayAreaDefinition definition = template.PlayAreas[i];
                playAreas.Add(new PlayAreaState(definition.Id, definition.Bounds, definition.FocusRegion));
            }

            return new MatchState(
                matchId,
                template.Id,
                0,
                cards,
                pawns,
                tokens,
                containers.Values,
                seats,
                placements,
                playAreas);
        }

        private static Dictionary<SeatId, PlayerId> MapPlayersToSeats(
            GameTemplate template,
            IReadOnlyList<PlayerId> activePlayerIds)
        {
            List<GameTemplateSeatDefinition> orderedSeats = new List<GameTemplateSeatDefinition>(template.Seats);
            orderedSeats.Sort((left, right) => left.PlayerLayoutSeatIndex.CompareTo(right.PlayerLayoutSeatIndex));
            Dictionary<SeatId, PlayerId> playersBySeatId = new Dictionary<SeatId, PlayerId>();
            for (int i = 0; i < orderedSeats.Count; i++)
            {
                playersBySeatId.Add(orderedSeats[i].SeatId, activePlayerIds[i]);
            }

            return playersBySeatId;
        }

        private static GameTemplateMatchBuildResult ConstructionFailure(Exception exception)
        {
            return GameTemplateMatchBuildResult.Failure(
                new[]
                {
                    new GameTemplateValidationIssue(
                        "MatchConstructionFailed",
                        $"Validated Template construction failed without exposing a partial Match: {exception.Message}"),
                });
        }
    }
}
