using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.GameTemplates
{
    /// <summary>
    /// An in-memory, Unity-free deep baseline captured from the first successfully constructed Match.
    /// It is intentionally not a persistence or save-file format.
    /// </summary>
    public sealed class GameTemplateInitialSnapshot
    {
        private readonly ReadOnlyCollection<ObjectSnapshot> objects;
        private readonly ReadOnlyCollection<ContainerSnapshot> containers;
        private readonly ReadOnlyCollection<ContainerPlacementSnapshot> containerPlacements;
        private readonly ReadOnlyCollection<SeatSnapshot> seats;
        private readonly ReadOnlyCollection<PlayAreaSnapshot> playAreas;
        private readonly IReadOnlyCollection<CommandId> physicalCommands;

        private GameTemplateInitialSnapshot(
            MatchId matchId,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<ObjectSnapshot> objects,
            IEnumerable<ContainerSnapshot> containers,
            IEnumerable<ContainerPlacementSnapshot> containerPlacements,
            IEnumerable<SeatSnapshot> seats,
            IEnumerable<PlayAreaSnapshot> playAreas, IReadOnlyCollection<CommandId> physicalCommands)
        {
            MatchId = matchId;
            GameTemplateId = gameTemplateId;
            Revision = revision;
            this.physicalCommands = new List<CommandId>(physicalCommands).AsReadOnly();
            this.objects = new ReadOnlyCollection<ObjectSnapshot>(new List<ObjectSnapshot>(objects));
            this.containers = new ReadOnlyCollection<ContainerSnapshot>(new List<ContainerSnapshot>(containers));
            this.containerPlacements = new ReadOnlyCollection<ContainerPlacementSnapshot>(
                new List<ContainerPlacementSnapshot>(containerPlacements));
            this.seats = new ReadOnlyCollection<SeatSnapshot>(new List<SeatSnapshot>(seats));
            this.playAreas = new ReadOnlyCollection<PlayAreaSnapshot>(new List<PlayAreaSnapshot>(playAreas));
        }

        public MatchId MatchId { get; }

        public GameTemplateId GameTemplateId { get; }

        public long Revision { get; }

        public static GameTemplateInitialSnapshot Capture(MatchState matchState)
        {
            if (matchState == null)
            {
                throw new ArgumentNullException(nameof(matchState));
            }

            List<ObjectSnapshot> objectSnapshots = new List<ObjectSnapshot>(matchState.ObjectCount);
            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                objectSnapshots.Add(ObjectSnapshot.FromCard(card));
            }

            foreach (PawnState pawn in matchState.Pawns.Values)
            {
                objectSnapshots.Add(ObjectSnapshot.FromObject(pawn.BaseState));
            }

            foreach (TokenState token in matchState.Tokens.Values)
            {
                objectSnapshots.Add(ObjectSnapshot.FromObject(token.BaseState));
            }

            foreach (DieState die in matchState.Dice.Values)
            {
                objectSnapshots.Add(ObjectSnapshot.FromDie(die));
            }

            List<ContainerSnapshot> containerSnapshots = new List<ContainerSnapshot>(matchState.Containers.Count);
            foreach (ContainerState container in matchState.Containers.Values)
            {
                containerSnapshots.Add(new ContainerSnapshot(container));
            }

            List<ContainerPlacementSnapshot> placementSnapshots =
                new List<ContainerPlacementSnapshot>(matchState.ContainerPlacements.Count);
            foreach (ContainerPlacementState placement in matchState.ContainerPlacements.Values)
            {
                placementSnapshots.Add(new ContainerPlacementSnapshot(placement.ContainerId, placement.Pose, placement.SurfaceHeight));
            }

            List<SeatSnapshot> seatSnapshots = new List<SeatSnapshot>(matchState.Seats.Count);
            foreach (SeatState seat in matchState.Seats.Values)
            {
                seatSnapshots.Add(new SeatSnapshot(seat));
            }

            List<PlayAreaSnapshot> playAreaSnapshots = new List<PlayAreaSnapshot>(matchState.PlayAreas.Count);
            foreach (PlayAreaState playArea in matchState.PlayAreas.Values)
            {
                playAreaSnapshots.Add(new PlayAreaSnapshot(playArea.Id, playArea.Bounds, playArea.FocusRegion));
            }

            return new GameTemplateInitialSnapshot(
                matchState.Id,
                matchState.GameTemplateId,
                matchState.Revision,
                objectSnapshots,
                containerSnapshots,
                placementSnapshots,
                seatSnapshots,
                playAreaSnapshots, matchState.CopyPhysicalCommandHistory());
        }

        public MatchState Restore()
        {
            Dictionary<ContainerId, ContainerState> restoredContainers =
                new Dictionary<ContainerId, ContainerState>();
            for (int i = 0; i < containers.Count; i++)
            {
                ContainerSnapshot snapshot = containers[i];
                restoredContainers.Add(
                    snapshot.Id,
                    new ContainerState(
                        snapshot.Id,
                        snapshot.Kind,
                        snapshot.OwnerSeatId,
                        snapshot.Visibility,
                        snapshot.Capacity));
            }

            List<CardInstanceState> restoredCards = new List<CardInstanceState>();
            List<PawnState> restoredPawns = new List<PawnState>();
            List<TokenState> restoredTokens = new List<TokenState>();
            List<DieState> restoredDice = new List<DieState>();
            Dictionary<TabletopObjectId, TabletopObjectState> restoredObjectStates =
                new Dictionary<TabletopObjectId, TabletopObjectState>();
            for (int i = 0; i < objects.Count; i++)
            {
                ObjectSnapshot snapshot = objects[i];
                TabletopObjectState baseState = snapshot.CreateBaseState();
                restoredObjectStates.Add(baseState.Id, baseState);
                switch (snapshot.Kind)
                {
                    case TabletopObjectKind.Card:
                        restoredCards.Add(new CardInstanceState(baseState, snapshot.Face));
                        break;
                    case TabletopObjectKind.Pawn:
                        restoredPawns.Add(new PawnState(baseState));
                        break;
                    case TabletopObjectKind.Token:
                        restoredTokens.Add(new TokenState(baseState));
                        break;
                    case TabletopObjectKind.Die:
                        restoredDice.Add(new DieState(
                            baseState,
                            snapshot.DieSideCount,
                            snapshot.DieValue));
                        break;
                    default:
                        throw new InvalidOperationException("Initial Snapshot contains an unsupported Tabletop Object kind.");
                }
            }

            ContainerTransferService transferService = new ContainerTransferService();
            for (int containerIndex = 0; containerIndex < containers.Count; containerIndex++)
            {
                ContainerSnapshot containerSnapshot = containers[containerIndex];
                ContainerState destination = restoredContainers[containerSnapshot.Id];
                for (int objectIndex = 0; objectIndex < containerSnapshot.OrderedObjectIds.Count; objectIndex++)
                {
                    TabletopObjectId objectId = containerSnapshot.OrderedObjectIds[objectIndex];
                    ContainerTransferResult result = transferService.PlaceIntoContainer(
                        restoredObjectStates[objectId],
                        destination,
                        objectIndex);
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException($"Initial Snapshot membership restore failed: {result.Error}.");
                    }
                }
            }

            List<ContainerPlacementState> restoredPlacements =
                new List<ContainerPlacementState>(containerPlacements.Count);
            for (int i = 0; i < containerPlacements.Count; i++)
            {
                ContainerPlacementSnapshot placement = containerPlacements[i];
                restoredPlacements.Add(new ContainerPlacementState(placement.ContainerId, placement.Pose, placement.SurfaceHeight));
            }

            List<SeatState> restoredSeats = new List<SeatState>(seats.Count);
            for (int i = 0; i < seats.Count; i++)
            {
                restoredSeats.Add(seats[i].Restore());
            }

            List<PlayAreaState> restoredPlayAreas = new List<PlayAreaState>(playAreas.Count);
            for (int i = 0; i < playAreas.Count; i++)
            {
                PlayAreaSnapshot playArea = playAreas[i];
                restoredPlayAreas.Add(new PlayAreaState(playArea.Id, playArea.Bounds, playArea.FocusRegion));
            }

            MatchState restored = new MatchState(
                MatchId,
                GameTemplateId,
                Revision,
                restoredCards,
                restoredPawns,
                restoredTokens,
                restoredContainers.Values,
                restoredSeats,
                restoredPlacements,
                restoredPlayAreas,
                restoredDice);
            foreach (CommandId command in physicalCommands) restored.RecordPhysicalCommand(command);
            return restored;
        }

        private sealed class ObjectSnapshot
        {
            private ObjectSnapshot(
                TabletopObjectState state,
                CardFace cardFace,
                int dieSideCount,
                int dieValue)
            {
                Id = state.Id;
                DefinitionId = state.DefinitionId;
                Kind = state.Kind;
                Pose = state.Pose;
                PhysicalState = state.PhysicalState;
                PhysicalRevision = state.PhysicalRevision;
                OwnerPlayerId = state.OwnerPlayerId;
                Visibility = state.Visibility;
                IsUserLocked = state.IsUserLocked;
                Face = cardFace;
                DieSideCount = dieSideCount;
                DieValue = dieValue;
            }

            public TabletopObjectId Id { get; }
            public ObjectDefinitionId DefinitionId { get; }
            public TabletopObjectKind Kind { get; }
            public TabletopPose Pose { get; }
            public PhysicalObjectState PhysicalState { get; }
            public long PhysicalRevision { get; }
            public PlayerId OwnerPlayerId { get; }
            public ObjectVisibility Visibility { get; }
            public bool IsUserLocked { get; }
            public CardFace Face { get; }
            public int DieSideCount { get; }
            public int DieValue { get; }

            public static ObjectSnapshot FromCard(CardInstanceState card)
            {
                return new ObjectSnapshot(card.BaseState, card.Face, 0, 0);
            }

            public static ObjectSnapshot FromObject(TabletopObjectState state)
            {
                return new ObjectSnapshot(state, CardFace.FaceUp, 0, 0);
            }

            public static ObjectSnapshot FromDie(DieState die)
            {
                return new ObjectSnapshot(
                    die.BaseState,
                    CardFace.FaceUp,
                    die.SideCount,
                    die.CurrentValue);
            }

            public TabletopObjectState CreateBaseState()
            {
                TabletopObjectState state = new TabletopObjectState(
                    Id,
                    DefinitionId,
                    Kind,
                    Pose,
                    ContainerId.Empty,
                    OwnerPlayerId,
                    Visibility,
                    IsUserLocked, PhysicalState, PhysicalRevision);
                return state;
            }
        }

        private sealed class ContainerSnapshot
        {
            public ContainerSnapshot(ContainerState container)
            {
                Id = container.Id;
                Kind = container.Kind;
                OwnerSeatId = container.OwnerSeatId;
                Visibility = container.Visibility;
                Capacity = container.Capacity;
                OrderedObjectIds = new ReadOnlyCollection<TabletopObjectId>(
                    new List<TabletopObjectId>(container.ObjectIds));
            }

            public ContainerId Id { get; }
            public ContainerKind Kind { get; }
            public SeatId OwnerSeatId { get; }
            public ObjectVisibility Visibility { get; }
            public int Capacity { get; }
            public IReadOnlyList<TabletopObjectId> OrderedObjectIds { get; }
        }

        private sealed class ContainerPlacementSnapshot
        {
            public ContainerPlacementSnapshot(ContainerId containerId, TabletopPose pose, float? surfaceHeight)
            {
                ContainerId = containerId;
                Pose = pose;
                SurfaceHeight = surfaceHeight;
            }

            public ContainerId ContainerId { get; }
            public TabletopPose Pose { get; }
            public float? SurfaceHeight { get; }
        }

        private sealed class SeatSnapshot
        {
            public SeatSnapshot(SeatState seat)
            {
                Id = seat.Id;
                TablePose = seat.TablePose;
                HandContainerId = seat.HandContainerId;
                SlotContainerIds = new ReadOnlyCollection<ContainerId>(
                    new List<ContainerId>(seat.Console.SlotContainerIds));
                OccupantPlayerId = seat.OccupantPlayerId;
                Status = seat.Status;
            }

            public SeatId Id { get; }
            public TabletopPose TablePose { get; }
            public ContainerId HandContainerId { get; }
            public IReadOnlyList<ContainerId> SlotContainerIds { get; }
            public PlayerId OccupantPlayerId { get; }
            public SeatStatus Status { get; }

            public SeatState Restore()
            {
                return new SeatState(
                    Id,
                    TablePose,
                    HandContainerId,
                    new ConsoleState(Id, SlotContainerIds),
                    OccupantPlayerId,
                    Status);
            }
        }

        private sealed class PlayAreaSnapshot
        {
            public PlayAreaSnapshot(PlayAreaId id, TabletopBounds bounds, TabletopBounds focusRegion)
            {
                Id = id;
                Bounds = bounds;
                FocusRegion = focusRegion;
            }

            public PlayAreaId Id { get; }
            public TabletopBounds Bounds { get; }
            public TabletopBounds FocusRegion { get; }
        }
    }
}
