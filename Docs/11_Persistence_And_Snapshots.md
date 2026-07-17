# Console Cards — Persistence and Snapshots

**Document ID:** 11_Persistence_And_Snapshots  
**Version:** 1.0 Draft  
**Status:** Approved with Open Decisions

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Goals

Persistence supports:

- Initial Match reset.
- Save and load.
- Reconnection.
- Late join.
- Host migration.
- Debug recovery.
- Future custom Game Templates.

## 2. Snapshot Types

### 2.1 Initial Snapshot

Generated when a Game Template creates a Match.

Used for reset.

### 2.2 Match Snapshot

Captures current authoritative Match State.

Used for save/load and recovery.

### 2.3 Personalized Snapshot

Filtered view of Match State for one Player.

Used for private multiplayer synchronization.

### 2.4 Migration Snapshot

Compact authoritative recovery Snapshot used during host migration.

May be the same schema as Match Snapshot with different transport/storage rules.

## 3. Snapshot Contents

```text
Snapshot
- Snapshot ID
- Match ID
- Game Template ID?
- Schema Version
- Platform Version
- Revision
- Timestamp
- Players and Seats
- Object Instances
- Containers and ordering
- Play Areas
- Consoles
- Policies
- Match metadata
- Random state
- Processed Command window/checkpoint
- Content dependency IDs
- Integrity checksum
```

## 4. Excluded Temporary State

Do not persist:

- Pointer position.
- Hover.
- Selection outline.
- In-progress animation.
- Interpolation state.
- Uncommitted drag preview.
- Temporary Camera movement.
- Temporary audio.
- Expired Interaction Locks.

If recovery happens during a drag, restore the last committed pose.

## 5. Serialization

Requirements:

- Versioned schema.
- Stable IDs.
- No direct Unity object references.
- No vendor networking types.
- Explicit optional fields.
- Deterministic validation.
- Clear corruption errors.

Format may begin with JSON for development clarity, then move to a compact format if profiling justifies it.

## 6. Content Resolution

Snapshots store Definition IDs.

Loading requires a content resolver to map IDs to available Definitions and Views.

Missing required content fails before Match restoration.

## 7. Revision Model

Every committed authoritative transaction increments Match revision once.

Snapshots record revision.

A client may request:

- Full Snapshot.
- Future delta since revision.

Full Snapshot is the first implementation.

## 8. Save Pipeline

1. Freeze or read consistent state boundary.
2. Validate Match State.
3. Build Snapshot DTO.
4. Serialize.
5. Compute checksum.
6. Write to temporary target.
7. Verify where practical.
8. Replace previous save atomically.
9. Report result.

## 9. Load Pipeline

1. Read bytes.
2. Validate file/header.
3. Deserialize schema.
4. Validate version.
5. Resolve content dependencies.
6. Validate IDs and invariants.
7. Migrate schema if supported.
8. Build Runtime State.
9. Bind Views.
10. Publish accepted revision.

Failure must not partially replace the active Match.

## 10. Reset

Reset loads the Initial Snapshot.

Reset must:

- Clear temporary locks.
- Replace Runtime State atomically.
- Rebuild/reconcile Views.
- Notify all Players.
- Preserve Session membership unless specified otherwise.

## 11. Snapshot Frequency

Local saves are explicit initially.

Multiplayer recovery snapshots may occur:

- After critical Commands.
- At a debounced interval.
- Before scene/network transitions.

Do not serialize every drag preview.

## 12. Host Migration

Migration storage may be:

- Networking service migration API.
- Cloud object storage.
- Another Player’s replicated recovery copy.
- Host memory plus periodic upload.

Final choice depends on networking technology.

## 13. Versioning

Each Snapshot includes schema version.

Changes are:

- Backward compatible when possible.
- Migrated through explicit converters.
- Rejected clearly when unsupported.

Do not silently default missing critical state.

## 14. Integrity Validation

Validate:

- Unique IDs.
- Container uniqueness.
- Definition availability.
- Valid Seat references.
- Valid Play Area references.
- Valid revision.
- Policy compatibility.
- Checksum where applicable.

## 15. Privacy

Personalized Snapshots must exclude unauthorized hidden Definition data.

Saved local authority files may contain full state and require appropriate handling.

## 16. Template Creation from Match

Saving a current Match as a new Game Template is a separate operation.

It must decide:

- Which current objects become initial objects.
- Whether Player-specific private cards are included.
- Which temporary Match data is removed.
- Which Policies become defaults.
- New Template ID and version.

## 17. Testing

Mandatory tests:

- Round-trip equality.
- Deck order.
- Hand privacy.
- Reset consistency.
- Corrupt data rejection.
- Missing content rejection.
- Version migration when added.
- Atomic save replacement.
- Load failure leaves current Match unchanged.
