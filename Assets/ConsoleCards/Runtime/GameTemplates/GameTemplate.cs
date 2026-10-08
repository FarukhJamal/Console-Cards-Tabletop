using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates
{
    /// <summary>
    /// Unity-free starting setup data. A Game Template contains no gameplay rules or live Match State.
    /// </summary>
    public sealed class GameTemplate
    {
        public const int CurrentSchemaVersion = 1;

        public GameTemplate(
            GameTemplateId id,
            int schemaVersion,
            string displayName,
            string description,
            PlayerLayoutId playerLayoutId,
            int requiredPlayerCount,
            IEnumerable<GameTemplateSeatDefinition> seats,
            IEnumerable<GameTemplateContainerDefinition> containers,
            IEnumerable<GameTemplateObjectInstanceDefinition> objects,
            IEnumerable<GameTemplateContainerMembership> memberships,
            IEnumerable<GameTemplatePlayAreaDefinition> playAreas,
            IEnumerable<GameTemplateCameraBookmarkDefinition> cameraBookmarks)
        {
            Id = id;
            SchemaVersion = schemaVersion;
            DisplayName = displayName;
            Description = description;
            PlayerLayoutId = playerLayoutId;
            RequiredPlayerCount = requiredPlayerCount;
            Seats = Copy(seats, nameof(seats));
            Containers = Copy(containers, nameof(containers));
            Objects = Copy(objects, nameof(objects));
            Memberships = Copy(memberships, nameof(memberships));
            PlayAreas = Copy(playAreas, nameof(playAreas));
            CameraBookmarks = Copy(cameraBookmarks, nameof(cameraBookmarks));
        }

        public GameTemplateId Id { get; }

        public int SchemaVersion { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public PlayerLayoutId PlayerLayoutId { get; }

        public int RequiredPlayerCount { get; }

        public IReadOnlyList<GameTemplateSeatDefinition> Seats { get; }

        public IReadOnlyList<GameTemplateContainerDefinition> Containers { get; }

        public IReadOnlyList<GameTemplateObjectInstanceDefinition> Objects { get; }

        public IReadOnlyList<GameTemplateContainerMembership> Memberships { get; }

        public IReadOnlyList<GameTemplatePlayAreaDefinition> PlayAreas { get; }

        public IReadOnlyList<GameTemplateCameraBookmarkDefinition> CameraBookmarks { get; }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return new ReadOnlyCollection<T>(new List<T>(values));
        }
    }

    public sealed class GameTemplateSeatDefinition
    {
        public GameTemplateSeatDefinition(
            SeatId seatId,
            int playerLayoutSeatIndex,
            ContainerId handContainerId,
            IEnumerable<ContainerId> consoleSlotContainerIds,
            TabletopPose? consolePose = null)
        {
            if (consoleSlotContainerIds == null)
            {
                throw new ArgumentNullException(nameof(consoleSlotContainerIds));
            }

            SeatId = seatId;
            PlayerLayoutSeatIndex = playerLayoutSeatIndex;
            HandContainerId = handContainerId;
            ConsoleSlotContainerIds = new ReadOnlyCollection<ContainerId>(
                new List<ContainerId>(consoleSlotContainerIds));
            HasConsolePose = consolePose.HasValue;
            ConsolePose = consolePose ?? TabletopPose.Default;
        }

        public SeatId SeatId { get; }

        public int PlayerLayoutSeatIndex { get; }

        public ContainerId HandContainerId { get; }

        public IReadOnlyList<ContainerId> ConsoleSlotContainerIds { get; }

        public bool HasConsolePose { get; }

        public TabletopPose ConsolePose { get; }
    }

    public sealed class GameTemplateContainerDefinition
    {
        public GameTemplateContainerDefinition(
            ContainerId id,
            ContainerKind kind,
            SeatId ownerSeatId,
            ObjectVisibility visibility,
            int capacity,
            bool hasTabletopPose,
            TabletopPose tabletopPose,
            float? extentWidth = null,
            float? extentDepth = null,
            GameTemplatePileStyle pileStyle = null)
        {
            Id = id;
            Kind = kind;
            OwnerSeatId = ownerSeatId;
            Visibility = visibility;
            Capacity = capacity;
            HasTabletopPose = hasTabletopPose;
            TabletopPose = tabletopPose;
            HasExtent = extentWidth.HasValue && extentDepth.HasValue;
            ExtentWidth = extentWidth ?? 0f;
            ExtentDepth = extentDepth ?? 0f;
            PileStyle = pileStyle;
        }

        public ContainerId Id { get; }

        public ContainerKind Kind { get; }

        public SeatId OwnerSeatId { get; }

        public ObjectVisibility Visibility { get; }

        public int Capacity { get; }

        public bool HasTabletopPose { get; }

        public TabletopPose TabletopPose { get; }

        /// <summary>True for a Hand zone: the pose and extent of the zone where cards can be dropped into the Hand.</summary>
        public bool HasExtent { get; }

        public float ExtentWidth { get; }

        public float ExtentDepth { get; }

        /// <summary>Declared pile presentation of a Deck or Stack; null uses the system default pile style.</summary>
        public GameTemplatePileStyle PileStyle { get; }
    }

    /// <summary>
    /// The system set of bay marks (doc 21): what a pile's spot is for. Games choose from this set; they do
    /// not add their own symbols or text. Playing a card means putting it into a Console slot, so there is
    /// no play mark.
    /// </summary>
    public enum GameTemplateBayMark
    {
        None = 0,
        Draw = 1,
        Discard = 2
    }

    /// <summary>
    /// Configurable pile parameters a Deck or Stack component declares, set by a template: the bay mark and
    /// the maximum pile height, which caps the per-card step so a large pile stays low. Every pile is
    /// plate-less (its cards rest on the table); there is no plate option. Presentation data only; not part
    /// of Match State.
    /// </summary>
    public sealed class GameTemplatePileStyle
    {
        public const float DefaultMaximumPileHeight = 0.25f;

        /// <summary>No mark, default maximum pile height.</summary>
        public static GameTemplatePileStyle Default { get; } = new GameTemplatePileStyle();

        public GameTemplatePileStyle(
            GameTemplateBayMark bayMark = GameTemplateBayMark.None,
            float maximumPileHeight = DefaultMaximumPileHeight)
        {
            if (float.IsNaN(maximumPileHeight) || float.IsInfinity(maximumPileHeight) || maximumPileHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPileHeight));
            }

            BayMark = bayMark;
            MaximumPileHeight = maximumPileHeight;
        }

        public GameTemplateBayMark BayMark { get; }

        public float MaximumPileHeight { get; }
    }

    public sealed class GameTemplateObjectInstanceDefinition
    {
        public GameTemplateObjectInstanceDefinition(
            TabletopObjectId id,
            ObjectDefinitionId definitionId,
            TabletopObjectKind kind,
            TabletopPose pose,
            SeatId ownerSeatId,
            ObjectVisibility visibility,
            bool isUserLocked,
            CardFace initialCardFace)
            : this(
                id,
                definitionId,
                kind,
                pose,
                ownerSeatId,
                visibility,
                isUserLocked,
                initialCardFace,
                0,
                0)
        {
        }

        public GameTemplateObjectInstanceDefinition(
            TabletopObjectId id,
            ObjectDefinitionId definitionId,
            TabletopObjectKind kind,
            TabletopPose pose,
            SeatId ownerSeatId,
            ObjectVisibility visibility,
            bool isUserLocked,
            CardFace initialCardFace,
            int dieSideCount,
            int initialDieValue)
        {
            Id = id;
            DefinitionId = definitionId;
            Kind = kind;
            Pose = pose;
            OwnerSeatId = ownerSeatId;
            Visibility = visibility;
            IsUserLocked = isUserLocked;
            InitialCardFace = initialCardFace;
            DieSideCount = dieSideCount;
            InitialDieValue = initialDieValue;
        }

        public TabletopObjectId Id { get; }

        public ObjectDefinitionId DefinitionId { get; }

        public TabletopObjectKind Kind { get; }

        public TabletopPose Pose { get; }

        public SeatId OwnerSeatId { get; }

        public ObjectVisibility Visibility { get; }

        public bool IsUserLocked { get; }

        public CardFace InitialCardFace { get; }

        public int DieSideCount { get; }

        public int InitialDieValue { get; }
    }

    public sealed class GameTemplateContainerMembership
    {
        public GameTemplateContainerMembership(
            ContainerId containerId,
            IEnumerable<TabletopObjectId> orderedObjectIds)
        {
            if (orderedObjectIds == null)
            {
                throw new ArgumentNullException(nameof(orderedObjectIds));
            }

            ContainerId = containerId;
            OrderedObjectIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(orderedObjectIds));
        }

        public ContainerId ContainerId { get; }

        /// <summary>Initial authoritative order from bottom to top.</summary>
        public IReadOnlyList<TabletopObjectId> OrderedObjectIds { get; }
    }

    public sealed class GameTemplatePlayAreaDefinition
    {
        public GameTemplatePlayAreaDefinition(
            PlayAreaId id,
            TabletopBounds bounds,
            TabletopBounds focusRegion)
        {
            Id = id;
            Bounds = bounds;
            FocusRegion = focusRegion;
        }

        public PlayAreaId Id { get; }

        public TabletopBounds Bounds { get; }

        public TabletopBounds FocusRegion { get; }
    }

    public sealed class GameTemplateCameraBookmarkDefinition
    {
        public GameTemplateCameraBookmarkDefinition(
            string name,
            TableCoordinate focusCoordinate,
            float orthographicSize)
        {
            Name = name;
            FocusCoordinate = focusCoordinate;
            OrthographicSize = orthographicSize;
        }

        public string Name { get; }

        public TableCoordinate FocusCoordinate { get; }

        public float OrthographicSize { get; }
    }
}
