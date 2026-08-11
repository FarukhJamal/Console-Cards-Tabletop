using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates
{
    public sealed class GameTemplateValidationIssue
    {
        public GameTemplateValidationIssue(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"{Code}: {Message}";
        }
    }

    public sealed class GameTemplateValidationResult
    {
        private readonly ReadOnlyCollection<GameTemplateValidationIssue> issues;

        internal GameTemplateValidationResult(IEnumerable<GameTemplateValidationIssue> issues)
        {
            this.issues = new ReadOnlyCollection<GameTemplateValidationIssue>(
                new List<GameTemplateValidationIssue>(issues));
        }

        public bool IsValid => issues.Count == 0;

        public IReadOnlyList<GameTemplateValidationIssue> Issues => issues;
    }

    public sealed class GameTemplateValidator
    {
        public GameTemplateValidationResult Validate(
            GameTemplate template,
            GameTemplateContentCatalog content,
            IReadOnlyList<PlayerId> activePlayerIds)
        {
            List<GameTemplateValidationIssue> issues = new List<GameTemplateValidationIssue>();
            if (template == null)
            {
                Add(issues, "TemplateRequired", "A Game Template is required.");
                return new GameTemplateValidationResult(issues);
            }

            if (content == null)
            {
                Add(issues, "ContentRequired", "A local content catalog is required.");
                return new GameTemplateValidationResult(issues);
            }

            ValidateHeader(template, activePlayerIds, issues);

            content.TryResolvePlayerLayout(template.PlayerLayoutId, out PlayerLayoutDefinition playerLayout);
            if (playerLayout == null)
            {
                Add(issues, "PlayerLayoutMissing", "The referenced Player Layout could not be resolved.");
            }
            else if (playerLayout.OccupiedSeatCount != template.RequiredPlayerCount)
            {
                Add(
                    issues,
                    "PlayerLayoutCountMismatch",
                    "The resolved Player Layout occupied Seat count must match the Template required Player count.");
            }

            Dictionary<SeatId, GameTemplateSeatDefinition> seats = IndexSeats(template, playerLayout, issues);
            Dictionary<ContainerId, GameTemplateContainerDefinition> containers = IndexContainers(template, seats, issues);
            ValidateSeatContainerRelationships(template, seats, containers, issues);

            Dictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition> objects =
                IndexObjects(template, content, seats, issues);
            ValidateMemberships(template, containers, objects, issues);
            ValidatePlayAreas(template, issues);
            ValidateCameraBookmarks(template, issues);

            return new GameTemplateValidationResult(issues);
        }

        private static void ValidateHeader(
            GameTemplate template,
            IReadOnlyList<PlayerId> activePlayerIds,
            List<GameTemplateValidationIssue> issues)
        {
            if (template.Id.IsEmpty)
            {
                Add(issues, "TemplateIdEmpty", "Game Template ID cannot be empty.");
            }

            if (template.SchemaVersion != GameTemplate.CurrentSchemaVersion)
            {
                Add(issues, "SchemaUnsupported", "The Game Template schema version is not supported.");
            }

            if (string.IsNullOrWhiteSpace(template.DisplayName))
            {
                Add(issues, "DisplayNameEmpty", "Game Template display name cannot be empty.");
            }

            if (template.PlayerLayoutId.IsEmpty)
            {
                Add(issues, "PlayerLayoutIdEmpty", "Player Layout ID cannot be empty.");
            }

            if (template.RequiredPlayerCount < PlayerLayoutDefinition.MinimumSeatCount
                || template.RequiredPlayerCount > PlayerLayoutDefinition.MaximumSeatCount)
            {
                Add(issues, "PlayerCountOutOfRange", "Required Player count must be between one and eight.");
            }

            if (activePlayerIds == null)
            {
                Add(issues, "PlayersRequired", "Active Player IDs are required for Match construction.");
                return;
            }

            if (activePlayerIds.Count != template.RequiredPlayerCount)
            {
                Add(issues, "PlayerCountMismatch", "Active Player count must match the Template required Player count.");
            }

            HashSet<PlayerId> seenPlayers = new HashSet<PlayerId>();
            for (int i = 0; i < activePlayerIds.Count; i++)
            {
                PlayerId playerId = activePlayerIds[i];
                if (playerId.IsEmpty)
                {
                    Add(issues, "PlayerIdEmpty", $"Active Player {i} has an empty ID.");
                }
                else if (!seenPlayers.Add(playerId))
                {
                    Add(issues, "PlayerIdDuplicate", "Active Player IDs must be unique.");
                }
            }
        }

        private static Dictionary<SeatId, GameTemplateSeatDefinition> IndexSeats(
            GameTemplate template,
            PlayerLayoutDefinition playerLayout,
            List<GameTemplateValidationIssue> issues)
        {
            Dictionary<SeatId, GameTemplateSeatDefinition> seats =
                new Dictionary<SeatId, GameTemplateSeatDefinition>();
            HashSet<int> layoutSeatIndices = new HashSet<int>();

            if (template.Seats.Count != template.RequiredPlayerCount)
            {
                Add(issues, "SeatCountMismatch", "Template Seat count must match the required Player count.");
            }

            for (int i = 0; i < template.Seats.Count; i++)
            {
                GameTemplateSeatDefinition seat = template.Seats[i];
                if (seat == null)
                {
                    Add(issues, "SeatNull", $"Seat entry {i} is null.");
                    continue;
                }

                if (seat.SeatId.IsEmpty)
                {
                    Add(issues, "SeatIdEmpty", $"Seat entry {i} has an empty ID.");
                }
                else if (seats.ContainsKey(seat.SeatId))
                {
                    Add(issues, "SeatIdDuplicate", "Seat IDs must be unique.");
                }
                else
                {
                    seats.Add(seat.SeatId, seat);
                }

                if (!layoutSeatIndices.Add(seat.PlayerLayoutSeatIndex))
                {
                    Add(issues, "LayoutSeatDuplicate", "Player Layout Seat indices must be unique within a Template.");
                }

                if (playerLayout != null
                    && !playerLayout.TryGetSeat(seat.PlayerLayoutSeatIndex, out _))
                {
                    Add(issues, "LayoutSeatMissing", "A Template Seat references a missing Player Layout Seat entry.");
                }

                if (seat.HandContainerId.IsEmpty)
                {
                    Add(issues, "HandIdEmpty", "Every Template Seat requires a Hand Container ID.");
                }

                HashSet<ContainerId> localSlots = new HashSet<ContainerId>();
                for (int slotIndex = 0; slotIndex < seat.ConsoleSlotContainerIds.Count; slotIndex++)
                {
                    ContainerId slotId = seat.ConsoleSlotContainerIds[slotIndex];
                    if (slotId.IsEmpty)
                    {
                        Add(issues, "ConsoleSlotIdEmpty", "Console Slot Container IDs cannot be empty.");
                    }
                    else if (!localSlots.Add(slotId))
                    {
                        Add(issues, "ConsoleSlotIdDuplicate", "A Console cannot reference the same Slot twice.");
                    }
                }
            }

            return seats;
        }

        private static Dictionary<ContainerId, GameTemplateContainerDefinition> IndexContainers(
            GameTemplate template,
            IReadOnlyDictionary<SeatId, GameTemplateSeatDefinition> seats,
            List<GameTemplateValidationIssue> issues)
        {
            Dictionary<ContainerId, GameTemplateContainerDefinition> containers =
                new Dictionary<ContainerId, GameTemplateContainerDefinition>();
            for (int i = 0; i < template.Containers.Count; i++)
            {
                GameTemplateContainerDefinition container = template.Containers[i];
                if (container == null)
                {
                    Add(issues, "ContainerNull", $"Container entry {i} is null.");
                    continue;
                }

                if (container.Id.IsEmpty)
                {
                    Add(issues, "ContainerIdEmpty", $"Container entry {i} has an empty ID.");
                }
                else if (containers.ContainsKey(container.Id))
                {
                    Add(issues, "ContainerIdDuplicate", "Container IDs must be unique.");
                }
                else
                {
                    containers.Add(container.Id, container);
                }

                if (!Enum.IsDefined(typeof(ContainerKind), container.Kind))
                {
                    Add(issues, "ContainerKindInvalid", "A Container has an invalid kind.");
                }

                if (!Enum.IsDefined(typeof(ObjectVisibility), container.Visibility))
                {
                    Add(issues, "ContainerVisibilityInvalid", "A Container has invalid visibility.");
                }

                if (container.Capacity < 0)
                {
                    Add(issues, "ContainerCapacityInvalid", "Container capacity cannot be below zero.");
                }

                bool requiresPlacement = IsPlacedContainer(container.Kind);
                if (requiresPlacement != container.HasTabletopPose)
                {
                    Add(
                        issues,
                        "ContainerPlacementInvalid",
                        "Deck, Stack, and Discard Pile Containers require a pose; other Container kinds use their authored layout anchors.");
                }

                if (container.HasTabletopPose && !IsFinite(container.TabletopPose))
                {
                    Add(issues, "ContainerPoseInvalid", "Container placement poses must be finite.");
                }

                if (!container.OwnerSeatId.IsEmpty && !seats.ContainsKey(container.OwnerSeatId))
                {
                    Add(issues, "ContainerOwnerMissing", "A Container references a missing owner Seat.");
                }
            }

            return containers;
        }

        private static void ValidateSeatContainerRelationships(
            GameTemplate template,
            IReadOnlyDictionary<SeatId, GameTemplateSeatDefinition> seats,
            IReadOnlyDictionary<ContainerId, GameTemplateContainerDefinition> containers,
            List<GameTemplateValidationIssue> issues)
        {
            Dictionary<ContainerId, int> referenceCounts = new Dictionary<ContainerId, int>();
            foreach (GameTemplateSeatDefinition seat in seats.Values)
            {
                ValidateOwnedContainer(
                    seat.HandContainerId,
                    seat.SeatId,
                    ContainerKind.Hand,
                    "Hand",
                    containers,
                    referenceCounts,
                    issues);

                for (int i = 0; i < seat.ConsoleSlotContainerIds.Count; i++)
                {
                    ValidateOwnedContainer(
                        seat.ConsoleSlotContainerIds[i],
                        seat.SeatId,
                        ContainerKind.ConsoleSlot,
                        "Console Slot",
                        containers,
                        referenceCounts,
                        issues);
                }
            }

            for (int i = 0; i < template.Containers.Count; i++)
            {
                GameTemplateContainerDefinition container = template.Containers[i];
                if (container == null
                    || (container.Kind != ContainerKind.Hand && container.Kind != ContainerKind.ConsoleSlot))
                {
                    continue;
                }

                if (!referenceCounts.TryGetValue(container.Id, out int count) || count != 1)
                {
                    Add(
                        issues,
                        "PersonalContainerReferenceInvalid",
                        "Every Hand and Console Slot Container must be referenced by exactly one owning Seat.");
                }
            }
        }

        private static void ValidateOwnedContainer(
            ContainerId containerId,
            SeatId seatId,
            ContainerKind expectedKind,
            string name,
            IReadOnlyDictionary<ContainerId, GameTemplateContainerDefinition> containers,
            IDictionary<ContainerId, int> referenceCounts,
            List<GameTemplateValidationIssue> issues)
        {
            if (!containers.TryGetValue(containerId, out GameTemplateContainerDefinition container))
            {
                Add(issues, $"{name.Replace(" ", string.Empty)}Missing", $"A Seat references a missing {name} Container.");
                return;
            }

            referenceCounts.TryGetValue(containerId, out int count);
            referenceCounts[containerId] = count + 1;
            if (container.Kind != expectedKind)
            {
                Add(issues, "PersonalContainerKindInvalid", $"A Seat {name} reference has the wrong Container kind.");
            }

            if (container.OwnerSeatId != seatId)
            {
                Add(issues, "PersonalContainerOwnerInvalid", $"A Seat {name} Container owner does not match the Seat.");
            }
        }

        private static Dictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition> IndexObjects(
            GameTemplate template,
            GameTemplateContentCatalog content,
            IReadOnlyDictionary<SeatId, GameTemplateSeatDefinition> seats,
            List<GameTemplateValidationIssue> issues)
        {
            Dictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition> objects =
                new Dictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition>();
            for (int i = 0; i < template.Objects.Count; i++)
            {
                GameTemplateObjectInstanceDefinition instance = template.Objects[i];
                if (instance == null)
                {
                    Add(issues, "ObjectNull", $"Object instance entry {i} is null.");
                    continue;
                }

                if (instance.Id.IsEmpty)
                {
                    Add(issues, "ObjectIdEmpty", $"Object instance entry {i} has an empty ID.");
                }
                else if (objects.ContainsKey(instance.Id))
                {
                    Add(issues, "ObjectIdDuplicate", "Tabletop Object instance IDs must be unique.");
                }
                else
                {
                    objects.Add(instance.Id, instance);
                }

                if (!Enum.IsDefined(typeof(TabletopObjectKind), instance.Kind))
                {
                    Add(issues, "ObjectKindInvalid", "A Tabletop Object instance has an invalid kind.");
                }

                if (!Enum.IsDefined(typeof(ObjectVisibility), instance.Visibility))
                {
                    Add(issues, "ObjectVisibilityInvalid", "A Tabletop Object instance has invalid visibility.");
                }

                if (!IsFinite(instance.Pose))
                {
                    Add(issues, "ObjectPoseInvalid", "Starting Tabletop Object poses must be finite.");
                }

                if (!instance.OwnerSeatId.IsEmpty && !seats.ContainsKey(instance.OwnerSeatId))
                {
                    Add(issues, "ObjectOwnerMissing", "A Tabletop Object instance references a missing owner Seat.");
                }

                if (!content.TryResolveObjectDefinition(instance.DefinitionId, out GameTemplateObjectDefinition definition))
                {
                    Add(issues, "ObjectDefinitionMissing", "A Tabletop Object Definition reference could not be resolved.");
                }
                else if (definition.Kind != instance.Kind)
                {
                    Add(issues, "ObjectDefinitionKindMismatch", "Object instance kind does not match its resolved Definition.");
                }

                if (instance.Kind == TabletopObjectKind.Card
                    && !Enum.IsDefined(typeof(CardFace), instance.InitialCardFace))
                {
                    Add(issues, "CardFaceInvalid", "A Card instance has an invalid initial face.");
                }

                if (instance.Kind == TabletopObjectKind.Die)
                {
                    if (instance.DieSideCount < 2)
                    {
                        Add(issues, "DieSideCountInvalid", "A Die instance must have at least two sides.");
                    }
                    else if (instance.InitialDieValue < 1
                        || instance.InitialDieValue > instance.DieSideCount)
                    {
                        Add(issues, "DieValueInvalid", "A Die instance value must be within its side count.");
                    }
                }
                else if (instance.DieSideCount != 0 || instance.InitialDieValue != 0)
                {
                    Add(issues, "NonDieConfigurationInvalid", "Only Die instances may define Die state.");
                }
            }

            return objects;
        }

        private static void ValidateMemberships(
            GameTemplate template,
            IReadOnlyDictionary<ContainerId, GameTemplateContainerDefinition> containers,
            IReadOnlyDictionary<TabletopObjectId, GameTemplateObjectInstanceDefinition> objects,
            List<GameTemplateValidationIssue> issues)
        {
            HashSet<ContainerId> seenContainers = new HashSet<ContainerId>();
            HashSet<TabletopObjectId> containedObjects = new HashSet<TabletopObjectId>();
            for (int i = 0; i < template.Memberships.Count; i++)
            {
                GameTemplateContainerMembership membership = template.Memberships[i];
                if (membership == null)
                {
                    Add(issues, "MembershipNull", $"Container membership entry {i} is null.");
                    continue;
                }

                if (!seenContainers.Add(membership.ContainerId))
                {
                    Add(issues, "MembershipContainerDuplicate", "A Container can have only one initial membership entry.");
                }

                if (!containers.TryGetValue(membership.ContainerId, out GameTemplateContainerDefinition container))
                {
                    Add(issues, "MembershipContainerMissing", "Initial membership references a missing Container.");
                }
                else if (container.Capacity > 0 && membership.OrderedObjectIds.Count > container.Capacity)
                {
                    Add(issues, "MembershipCapacityExceeded", "Initial Container membership exceeds its capacity.");
                }

                HashSet<TabletopObjectId> localObjects = new HashSet<TabletopObjectId>();
                for (int objectIndex = 0; objectIndex < membership.OrderedObjectIds.Count; objectIndex++)
                {
                    TabletopObjectId objectId = membership.OrderedObjectIds[objectIndex];
                    if (objectId.IsEmpty)
                    {
                        Add(issues, "MembershipObjectIdEmpty", "Container membership cannot contain an empty Object ID.");
                    }
                    else if (!localObjects.Add(objectId))
                    {
                        Add(issues, "MembershipObjectDuplicate", "A Container membership cannot contain duplicate Object IDs.");
                    }

                    if (!objects.ContainsKey(objectId))
                    {
                        Add(issues, "MembershipObjectMissing", "Container membership references a missing Object instance.");
                    }

                    if (!containedObjects.Add(objectId))
                    {
                        Add(issues, "ObjectInMultipleContainers", "An Object instance cannot begin in more than one Container.");
                    }
                }
            }
        }

        private static void ValidatePlayAreas(
            GameTemplate template,
            List<GameTemplateValidationIssue> issues)
        {
            HashSet<PlayAreaId> playAreaIds = new HashSet<PlayAreaId>();
            for (int i = 0; i < template.PlayAreas.Count; i++)
            {
                GameTemplatePlayAreaDefinition playArea = template.PlayAreas[i];
                if (playArea == null)
                {
                    Add(issues, "PlayAreaNull", $"Play Area entry {i} is null.");
                    continue;
                }

                if (playArea.Id.IsEmpty)
                {
                    Add(issues, "PlayAreaIdEmpty", "Play Area IDs cannot be empty.");
                }
                else if (!playAreaIds.Add(playArea.Id))
                {
                    Add(issues, "PlayAreaIdDuplicate", "Play Area IDs must be unique.");
                }

                if (!playArea.Bounds.Contains(playArea.FocusRegion))
                {
                    Add(issues, "PlayAreaFocusInvalid", "A Play Area focus region must be contained by its bounds.");
                }
            }
        }

        private static void ValidateCameraBookmarks(
            GameTemplate template,
            List<GameTemplateValidationIssue> issues)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < template.CameraBookmarks.Count; i++)
            {
                GameTemplateCameraBookmarkDefinition bookmark = template.CameraBookmarks[i];
                if (bookmark == null)
                {
                    Add(issues, "CameraBookmarkNull", $"Camera bookmark entry {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bookmark.Name))
                {
                    Add(issues, "CameraBookmarkNameEmpty", "Camera bookmark names cannot be empty.");
                }
                else if (!names.Add(bookmark.Name))
                {
                    Add(issues, "CameraBookmarkNameDuplicate", "Camera bookmark names must be unique.");
                }

                if (!IsFinite(bookmark.FocusCoordinate.X)
                    || !IsFinite(bookmark.FocusCoordinate.Y)
                    || !IsFinite(bookmark.OrthographicSize)
                    || bookmark.OrthographicSize <= 0f)
                {
                    Add(issues, "CameraBookmarkInvalid", "Camera bookmark focus and size must be finite and size must be positive.");
                }
            }
        }

        private static bool IsPlacedContainer(ContainerKind kind)
        {
            return kind == ContainerKind.Deck
                || kind == ContainerKind.Stack
                || kind == ContainerKind.DiscardPile;
        }

        private static bool IsFinite(TabletopPose pose)
        {
            return IsFinite(pose.Position.X)
                && IsFinite(pose.Position.Y)
                && IsFinite(pose.RotationDegrees);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void Add(
            ICollection<GameTemplateValidationIssue> issues,
            string code,
            string message)
        {
            issues.Add(new GameTemplateValidationIssue(code, message));
        }
    }
}
