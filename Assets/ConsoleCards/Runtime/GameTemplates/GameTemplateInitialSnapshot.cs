using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
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
    /// An in-memory, Unity-free deep Match snapshot used by initial baselines and active-session Undo.
    /// It is intentionally not a persistence or save-file format.
    /// </summary>
    public sealed class GameTemplateInitialSnapshot
    {
        private readonly ReadOnlyCollection<ObjectSnapshot> objects;
        private readonly ReadOnlyCollection<ContainerSnapshot> containers;
        private readonly ReadOnlyCollection<ContainerPlacementSnapshot> containerPlacements;
        private readonly ReadOnlyCollection<PlacedConsoleSnapshot> placedConsoles;
        // Game board mats (doc 22 §15): copied after construction; empty for a template baseline.
        private IReadOnlyList<PlacedBoardSnapshot> placedBoards = Array.Empty<PlacedBoardSnapshot>();
        private readonly ReadOnlyCollection<SeatSnapshot> seats;
        private readonly ReadOnlyCollection<PlayAreaSnapshot> playAreas;
        private readonly ReadOnlyCollection<TabletopObjectId> templateObjectIds;
        private readonly ReadOnlyCollection<ContainerId> templateContainerIds;
        private readonly IReadOnlyCollection<CommandId> physicalCommands;

        private GameTemplateInitialSnapshot(
            MatchId matchId,
            GameTemplateId gameTemplateId,
            long revision,
            IEnumerable<ObjectSnapshot> objects,
            IEnumerable<ContainerSnapshot> containers,
            IEnumerable<ContainerPlacementSnapshot> containerPlacements,
            IEnumerable<PlacedConsoleSnapshot> placedConsoles,
            IEnumerable<SeatSnapshot> seats,
            IEnumerable<PlayAreaSnapshot> playAreas,
            IEnumerable<TabletopObjectId> templateObjectIds,
            IEnumerable<ContainerId> templateContainerIds,
            IReadOnlyCollection<CommandId> physicalCommands)
        {
            MatchId = matchId;
            GameTemplateId = gameTemplateId;
            Revision = revision;
            this.physicalCommands = new List<CommandId>(physicalCommands).AsReadOnly();
            this.objects = new ReadOnlyCollection<ObjectSnapshot>(new List<ObjectSnapshot>(objects));
            this.containers = new ReadOnlyCollection<ContainerSnapshot>(new List<ContainerSnapshot>(containers));
            this.containerPlacements = new ReadOnlyCollection<ContainerPlacementSnapshot>(
                new List<ContainerPlacementSnapshot>(containerPlacements));
            this.placedConsoles = new ReadOnlyCollection<PlacedConsoleSnapshot>(
                new List<PlacedConsoleSnapshot>(placedConsoles));
            this.seats = new ReadOnlyCollection<SeatSnapshot>(new List<SeatSnapshot>(seats));
            this.playAreas = new ReadOnlyCollection<PlayAreaSnapshot>(new List<PlayAreaSnapshot>(playAreas));
            this.templateObjectIds = new ReadOnlyCollection<TabletopObjectId>(new List<TabletopObjectId>(templateObjectIds));
            this.templateContainerIds = new ReadOnlyCollection<ContainerId>(new List<ContainerId>(templateContainerIds));
        }

        public MatchId MatchId { get; }

        public GameTemplateId GameTemplateId { get; }

        public long Revision { get; }

        public int ObjectCount => objects.Count;

        public IReadOnlyList<TabletopObjectId> CopyObjectIds()
        {
            List<TabletopObjectId> ids = new List<TabletopObjectId>(objects.Count);
            for (int i = 0; i < objects.Count; i++) ids.Add(objects[i].Id);
            return ids.AsReadOnly();
        }

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
                placementSnapshots.Add(new ContainerPlacementSnapshot(
                    placement.ContainerId,
                    placement.Pose,
                    placement.SurfaceHeight,
                    placement.HasExtent ? placement.ExtentWidth : (float?)null,
                    placement.HasExtent ? placement.ExtentDepth : (float?)null));
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

            List<PlacedConsoleSnapshot> placedConsoleSnapshots =
                new List<PlacedConsoleSnapshot>(matchState.PlacedConsoles.Count);
            foreach (PlacedConsoleState placedConsole in matchState.PlacedConsoles.Values)
            {
                placedConsoleSnapshots.Add(new PlacedConsoleSnapshot(placedConsole));
            }

            List<TabletopObjectId> protectedObjects = new List<TabletopObjectId>();
            foreach (TabletopObjectId objectId in matchState.Cards.Keys)
                if (matchState.IsTemplateObject(objectId)) protectedObjects.Add(objectId);
            foreach (TabletopObjectId objectId in matchState.Pawns.Keys)
                if (matchState.IsTemplateObject(objectId)) protectedObjects.Add(objectId);
            foreach (TabletopObjectId objectId in matchState.Tokens.Keys)
                if (matchState.IsTemplateObject(objectId)) protectedObjects.Add(objectId);
            foreach (TabletopObjectId objectId in matchState.Dice.Keys)
                if (matchState.IsTemplateObject(objectId)) protectedObjects.Add(objectId);
            List<ContainerId> protectedContainers = new List<ContainerId>();
            foreach (ContainerId containerId in matchState.Containers.Keys)
                if (matchState.IsTemplateContainer(containerId)) protectedContainers.Add(containerId);

            List<PlacedBoardSnapshot> boardSnapshots = new List<PlacedBoardSnapshot>(matchState.PlacedBoards.Count);
            foreach (PlacedBoardState board in matchState.PlacedBoards.Values)
            {
                boardSnapshots.Add(new PlacedBoardSnapshot(board));
            }

            GameTemplateInitialSnapshot snapshot = new GameTemplateInitialSnapshot(
                matchState.Id,
                matchState.GameTemplateId,
                matchState.Revision,
                objectSnapshots,
                containerSnapshots,
                placementSnapshots,
                placedConsoleSnapshots,
                seatSnapshots,
                playAreaSnapshots,
                protectedObjects,
                protectedContainers,
                matchState.CopyPhysicalCommandHistory());
            snapshot.placedBoards = boardSnapshots.AsReadOnly();
            return snapshot;
        }

        // PRIVACY-PROJECTION (doc 11 v2, slice P1; NOT wired to the network): a copy of this snapshot for one
        // recipient. Objects named in hiddenIdentity keep their place, pose and face but lose their identity: their id
        // becomes the recipient's opaque alias and their definition the neutral hidden one. Containers named in
        // hiddenOrder list their objects in alias order, so the real order cannot be read back. The physical command
        // history is dropped: command ids seed each command's random numbers, so they would let a shuffle be replayed.
        public GameTemplateInitialSnapshot ProjectFor(
            ICollection<TabletopObjectId> hiddenIdentity,
            Func<TabletopObjectId, TabletopObjectId> alias,
            ICollection<ContainerId> hiddenOrder,
            ObjectDefinitionId hiddenDefinition)
        {
            if (hiddenIdentity == null) throw new ArgumentNullException(nameof(hiddenIdentity));
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            if (hiddenOrder == null) throw new ArgumentNullException(nameof(hiddenOrder));
            TabletopObjectId Map(TabletopObjectId id) => hiddenIdentity.Contains(id) ? alias(id) : id;

            List<ObjectSnapshot> projectedObjects = new List<ObjectSnapshot>(objects.Count);
            foreach (ObjectSnapshot item in objects)
                projectedObjects.Add(hiddenIdentity.Contains(item.Id) ? item.Hidden(alias(item.Id), hiddenDefinition) : item);
            // The capture lists objects in the authority's creation order, which follows identity: list them by reference.
            projectedObjects.Sort((x, y) => x.Id.Value.CompareTo(y.Id.Value));

            List<ContainerSnapshot> projectedContainers = new List<ContainerSnapshot>(containers.Count);
            foreach (ContainerSnapshot container in containers)
            {
                List<TabletopObjectId> order = new List<TabletopObjectId>(container.OrderedObjectIds.Count);
                foreach (TabletopObjectId id in container.OrderedObjectIds) order.Add(Map(id));
                if (hiddenOrder.Contains(container.Id)) order.Sort((a, b) => a.Value.CompareTo(b.Value));
                projectedContainers.Add(container.WithOrder(order));
            }

            List<TabletopObjectId> projectedTemplateObjects = new List<TabletopObjectId>(templateObjectIds.Count);
            foreach (TabletopObjectId id in templateObjectIds) projectedTemplateObjects.Add(Map(id));
            projectedTemplateObjects.Sort((x, y) => x.Value.CompareTo(y.Value)); // template order follows identity

            GameTemplateInitialSnapshot projected = new GameTemplateInitialSnapshot(
                MatchId, GameTemplateId, Revision, projectedObjects, projectedContainers, containerPlacements,
                placedConsoles, seats, playAreas, projectedTemplateObjects, templateContainerIds, Array.Empty<CommandId>());
            projected.placedBoards = placedBoards;
            return projected;
        }

        // PRIVACY-SCENE (s146): the same table under another local session's ids (the development private view is
        // shown in an Empty Table session, which keeps its own Match and Template ids).
        public GameTemplateInitialSnapshot ForSession(MatchId matchId, GameTemplateId gameTemplateId)
        {
            GameTemplateInitialSnapshot copy = new GameTemplateInitialSnapshot(
                matchId, gameTemplateId, Revision, objects, containers, containerPlacements, placedConsoles, seats, playAreas,
                templateObjectIds, templateContainerIds, physicalCommands);
            copy.placedBoards = placedBoards;
            return copy;
        }

        public MatchState Restore()
        {
            return Restore(Revision);
        }

        public MatchState Restore(long revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            HashSet<ContainerId> placedConsoleSlotIds = new HashSet<ContainerId>();
            for (int i = 0; i < placedConsoles.Count; i++)
                for (int slotIndex = 0; slotIndex < placedConsoles[i].SlotContainerIds.Count; slotIndex++)
                    placedConsoleSlotIds.Add(placedConsoles[i].SlotContainerIds[slotIndex]);

            Dictionary<ContainerId, ContainerState> restoredContainers =
                new Dictionary<ContainerId, ContainerState>();
            for (int i = 0; i < containers.Count; i++)
            {
                ContainerSnapshot snapshot = containers[i];
                if (placedConsoleSlotIds.Contains(snapshot.Id)) continue;
                restoredContainers.Add(
                    snapshot.Id,
                    new ContainerState(
                        snapshot.Id,
                        snapshot.Kind,
                        snapshot.OwnerSeatId,
                        snapshot.Visibility,
                        snapshot.Capacity,
                        snapshot.ArrivalFace));
            }

            List<CardInstanceState> restoredCards = new List<CardInstanceState>();
            List<PawnState> restoredPawns = new List<PawnState>();
            List<TokenState> restoredTokens = new List<TokenState>();
            List<DieState> restoredDice = new List<DieState>();
            for (int i = 0; i < objects.Count; i++)
            {
                ObjectSnapshot snapshot = objects[i];
                TabletopObjectState baseState = snapshot.CreateBaseState();
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
            List<ContainerPlacementState> restoredPlacements =
                new List<ContainerPlacementState>(containerPlacements.Count);
            for (int i = 0; i < containerPlacements.Count; i++)
            {
                ContainerPlacementSnapshot placement = containerPlacements[i];
                restoredPlacements.Add(new ContainerPlacementState(
                    placement.ContainerId,
                    placement.Pose,
                    placement.SurfaceHeight,
                    placement.ExtentWidth,
                    placement.ExtentDepth));
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
                revision,
                restoredCards,
                restoredPawns,
                restoredTokens,
                restoredContainers.Values,
                restoredSeats,
                restoredPlacements,
                restoredPlayAreas,
                restoredDice);
            for (int i = 0; i < placedConsoles.Count; i++)
            {
                PlacedConsoleSnapshot consoleSnapshot = placedConsoles[i];
                List<ContainerState> slots = new List<ContainerState>(consoleSnapshot.SlotContainerIds.Count);
                for (int slotIndex = 0; slotIndex < consoleSnapshot.SlotContainerIds.Count; slotIndex++)
                {
                    ContainerSnapshot slotSnapshot = FindContainer(consoleSnapshot.SlotContainerIds[slotIndex]);
                    slots.Add(new ContainerState(
                        slotSnapshot.Id,
                        slotSnapshot.Kind,
                        slotSnapshot.OwnerSeatId,
                        slotSnapshot.Visibility,
                        slotSnapshot.Capacity));
                }

                restored.AddPlacedConsole(consoleSnapshot.Restore(), slots);
            }

            for (int i = 0; i < placedBoards.Count; i++)
            {
                restored.AddPlacedBoard(placedBoards[i].Restore());
            }

            for (int containerIndex = 0; containerIndex < containers.Count; containerIndex++)
            {
                ContainerSnapshot containerSnapshot = containers[containerIndex];
                ContainerState destination = restored.GetContainer(containerSnapshot.Id);
                for (int objectIndex = 0; objectIndex < containerSnapshot.OrderedObjectIds.Count; objectIndex++)
                {
                    TabletopObjectId objectId = containerSnapshot.OrderedObjectIds[objectIndex];
                    ContainerTransferResult result = transferService.PlaceIntoContainer(
                        restored.GetObject(objectId), destination, objectIndex);
                    if (!result.Succeeded)
                        throw new InvalidOperationException($"Initial Snapshot membership restore failed: {result.Error}.");
                }
            }

            restored.RestoreTemplateProtection(templateObjectIds, templateContainerIds);
            foreach (CommandId command in physicalCommands) restored.RecordPhysicalCommand(command);
            return restored;
        }

        private ContainerSnapshot FindContainer(ContainerId containerId)
        {
            for (int i = 0; i < containers.Count; i++)
                if (containers[i].Id == containerId) return containers[i];
            throw new InvalidOperationException("Placed Console snapshot references a missing Slot Container.");
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

            // PRIVACY-PROJECTION: the same object with an opaque id and the neutral definition.
            private ObjectSnapshot(ObjectSnapshot from, TabletopObjectId id, ObjectDefinitionId definitionId)
            {
                Id = id;
                DefinitionId = definitionId;
                Kind = from.Kind;
                Pose = from.Pose;
                PhysicalState = from.PhysicalState;
                PhysicalRevision = from.PhysicalRevision;
                OwnerPlayerId = from.OwnerPlayerId;
                Visibility = from.Visibility;
                IsUserLocked = from.IsUserLocked;
                Face = from.Face;
                DieSideCount = from.DieSideCount;
                DieValue = from.DieValue;
            }

            public ObjectSnapshot Hidden(TabletopObjectId id, ObjectDefinitionId definitionId) =>
                new ObjectSnapshot(this, id, definitionId);

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
            // PRIVACY-PROJECTION: the same container listing a recipient's view of its objects.
            private ContainerSnapshot(ContainerSnapshot from, IReadOnlyList<TabletopObjectId> order)
            {
                Id = from.Id;
                Kind = from.Kind;
                OwnerSeatId = from.OwnerSeatId;
                Visibility = from.Visibility;
                Capacity = from.Capacity;
                ArrivalFace = from.ArrivalFace;
                OrderedObjectIds = new ReadOnlyCollection<TabletopObjectId>(new List<TabletopObjectId>(order));
            }

            public ContainerSnapshot WithOrder(IReadOnlyList<TabletopObjectId> order) => new ContainerSnapshot(this, order);

            public ContainerSnapshot(ContainerState container)
            {
                Id = container.Id;
                Kind = container.Kind;
                OwnerSeatId = container.OwnerSeatId;
                Visibility = container.Visibility;
                Capacity = container.Capacity;
                ArrivalFace = container.ArrivalFace;
                OrderedObjectIds = new ReadOnlyCollection<TabletopObjectId>(
                    new List<TabletopObjectId>(container.ObjectIds));
            }

            public ContainerId Id { get; }
            public ContainerKind Kind { get; }
            public SeatId OwnerSeatId { get; }
            public ObjectVisibility Visibility { get; }
            public int Capacity { get; }
            public ContainerArrivalFace ArrivalFace { get; }
            public IReadOnlyList<TabletopObjectId> OrderedObjectIds { get; }
        }

        private sealed class ContainerPlacementSnapshot
        {
            public ContainerPlacementSnapshot(
                ContainerId containerId,
                TabletopPose pose,
                float? surfaceHeight,
                float? extentWidth,
                float? extentDepth)
            {
                ContainerId = containerId;
                Pose = pose;
                SurfaceHeight = surfaceHeight;
                ExtentWidth = extentWidth;
                ExtentDepth = extentDepth;
            }

            public ContainerId ContainerId { get; }
            public TabletopPose Pose { get; }
            public float? SurfaceHeight { get; }
            public float? ExtentWidth { get; }
            public float? ExtentDepth { get; }
        }

        private sealed class PlacedBoardSnapshot
        {
            public PlacedBoardSnapshot(PlacedBoardState state)
            {
                Id = state.Id;
                Pose = state.Pose;
                SurfaceHeight = state.SurfaceHeight;
                Grid = state.Grid;
            }

            public BoardId Id { get; }
            public TabletopPose Pose { get; }
            public float? SurfaceHeight { get; }
            public BoardGrid Grid { get; }

            public PlacedBoardState Restore() => new PlacedBoardState(Id, Pose, Grid, SurfaceHeight);
        }

        private sealed class PlacedConsoleSnapshot
        {
            public PlacedConsoleSnapshot(PlacedConsoleState state)
            {
                Id = state.Id;
                Pose = state.Pose;
                SurfaceHeight = state.SurfaceHeight;
                SlotContainerIds = new ReadOnlyCollection<ContainerId>(
                    new List<ContainerId>(state.Console.SlotContainerIds));
                ShowsBack = state.Console.ShowsBack;
            }

            public ConsoleId Id { get; }
            public TabletopPose Pose { get; }
            public float? SurfaceHeight { get; }
            public IReadOnlyList<ContainerId> SlotContainerIds { get; }
            public bool ShowsBack { get; }

            public PlacedConsoleState Restore()
            {
                ConsoleState console = ConsoleState.CreateUnowned(SlotContainerIds);
                console.SetShowsBack(ShowsBack);
                return new PlacedConsoleState(
                    Id,
                    Pose,
                    console,
                    SurfaceHeight);
            }
        }

        private sealed class SeatSnapshot
        {
            public SeatSnapshot(SeatState seat)
            {
                Id = seat.Id;
                TablePose = seat.TablePose;
                ConsolePose = seat.ConsolePose;
                ConsoleSurfaceHeight = seat.ConsoleSurfaceHeight;
                HandContainerId = seat.HandContainerId;
                SlotContainerIds = new ReadOnlyCollection<ContainerId>(
                    new List<ContainerId>(seat.Console.SlotContainerIds));
                ConsoleShowsBack = seat.Console.ShowsBack;
                OccupantPlayerId = seat.OccupantPlayerId;
                Status = seat.Status;
            }

            public SeatId Id { get; }
            public TabletopPose TablePose { get; }
            public TabletopPose ConsolePose { get; }
            public float? ConsoleSurfaceHeight { get; }
            public ContainerId HandContainerId { get; }
            public IReadOnlyList<ContainerId> SlotContainerIds { get; }
            public bool ConsoleShowsBack { get; }
            public PlayerId OccupantPlayerId { get; }
            public SeatStatus Status { get; }

            public SeatState Restore()
            {
                ConsoleState console = new ConsoleState(Id, SlotContainerIds);
                console.SetShowsBack(ConsoleShowsBack);
                return new SeatState(
                    Id,
                    TablePose,
                    HandContainerId,
                    console,
                    OccupantPlayerId,
                    Status,
                    ConsolePose,
                    ConsoleSurfaceHeight);
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
