# Console Cards — Multiplayer Architecture

**Document ID:** 10_Multiplayer_Architecture  
**Version:** 1.0 Draft  
**Status:** Approved with Open Decisions

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Goals

Multiplayer must support:

- Shared authoritative Match State.
- Two-to-six Players initially.
- Stable Seats.
- Private Hands.
- Concurrent object interaction.
- Reconnection.
- Late joining where allowed.
- Snapshot recovery.
- Future host migration or dedicated authority.

## 2. Authority Model

One accepted authority owns official Match State at a time.

Possible topologies:

- Player-hosted authority.
- Dedicated authoritative server.
- Backend/serverless authority.

Authority and hosting are separate concepts.

The initial likely topology is player-hosted, but the vendor decision remains separate.

## 3. Stable Identity

```text
Player ID
→ Seat Assignment
→ Temporary Network Connection ID
```

Network IDs may change. Seat and Hand ownership remain bound to stable Player ID.

## 4. Command Flow

```text
Client Input
→ Local Preview
→ Command Request
→ Authority Validation
→ Policy Evaluation
→ State Commit
→ Revision Increment
→ Accepted Result/Event/Snapshot Delta
→ Client Reconciliation
```

Clients do not directly commit shared state.

## 5. Interaction Locking

Manipulable object or collection:

```text
InteractionLock
- Object/Container ID
- Player ID
- Interaction ID
- Acquired Time
- Last Update Time
```

Locks clear on:

- Successful release.
- Cancellation.
- Disconnect.
- Timeout.
- Authority recovery.

## 6. Transform Strategy

Do not blindly synchronize every transform every frame.

During drag:

- Local Player sees immediate preview.
- Rate-limited preview updates may be shared.
- Remote Views interpolate.
- Final accepted pose is committed as authoritative state.

## 7. Private Information

Authority may hold full state.

Each client receives a personalized projection:

- Own Hand: full authorized card data.
- Other Hands: count/back/placeholder only.
- Hidden Containers: filtered data.
- Public cards: full public data.

Hiding a renderer is insufficient if secret Definition data was already sent.

## 8. Seating

Seats remain stable through temporary disconnect.

Seat State includes:

- Seat ID.
- Player ID.
- Connection status.
- Hand Container.
- Console.
- Orientation.
- Camera bookmarks.

A reconnecting Player rebinds to the Seat rather than receiving a new Hand.

## 9. Reconnection

Normal reconnect flow:

1. Restore stable Player ID.
2. Rejoin Session.
3. Find reserved Seat.
4. Bind new network connection.
5. Compare client and authority revision.
6. Send personalized full Snapshot initially.
7. Rebuild Views.
8. Resume play.

Full Snapshot is preferred before delta optimization.

## 10. Late Join

If permitted:

- Validate Session and Match state.
- Assign or restore Seat.
- Send content dependency list.
- Send personalized Snapshot.
- Spawn/bind Views.
- Join at accepted revision.

Template or Policy may disallow late join.

## 11. Host Loss

Player-hosted authority requires a defined failure path.

### Required Foundation Behavior

- Detect authority loss.
- Freeze new state-changing input.
- Clear or expire temporary Interaction Locks.
- Preserve the latest available recovery information.
- Return Players to a controlled Session recovery or lobby flow with a clear message when migration is unavailable.

### Conditional Extension

Host migration is not automatically part of the Multiplayer Foundation. It is included only if the networking Architecture Decision and approved milestone explicitly add it.

Reconnection and Seat restoration for non-host Players remain required. Seamless host migration must never be claimed unless implemented and tested.

## 12. Host Migration Architecture

If included:

1. Elect new authority.
2. Obtain latest valid Snapshot.
3. Start new host/authority transport.
4. Restore Match State.
5. Clear temporary locks.
6. Rebind Players to Seats.
7. Redistribute personalized Snapshots.
8. Resume from committed revision.

Vendor-specific mechanics live in the adapter.

## 13. Duplicate and Stale Commands

Commands include:

- Command ID.
- Player ID.
- Match ID.
- Expected revision where useful.

Authority rejects or safely acknowledges:

- Duplicate Command.
- Stale object reference.
- Stale expected revision.
- Unauthorized action.
- Missing object.
- Conflicting lock.

## 14. Networking Abstractions

Conceptual contracts:

```csharp
public interface ITabletopAuthority
{
    Task<CommandResult> SubmitAsync(
        CommandEnvelope command,
        CancellationToken cancellationToken);
}

public interface IMultiplayerSession
{
    PlayerId LocalPlayerId { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public interface IMatchStatePublisher
{
    Task PublishSnapshotAsync(
        MatchSnapshot snapshot,
        CancellationToken cancellationToken);
}
```

Contracts will be refined during implementation. Do not expose vendor types.

## 15. Technology Evaluation Criteria

When selecting Fusion, NGO, Mirror, or another solution, score:

- Reconnection.
- Host migration.
- Private data filtering.
- Session/lobby support.
- NAT traversal/relay.
- Cost model.
- Unity version support.
- Documentation.
- Testing support.
- Dedicated-server path.
- Vendor lock-in.
- Team familiarity.
- Implementation time.

## 16. Security Position

Player-hosted authority cannot guarantee secrecy from a malicious host.

Acceptable for:

- Private friend sessions.
- Freeform tabletop.
- Non-ranked MVP.

Not acceptable for:

- Ranked competitive rewards.
- Valuable digital inventory.
- Strong anti-cheat claims.

## 17. Initial Multiplayer Completion Criteria

- Two-to-six Players join.
- Stable Seats.
- Shared object movement.
- Draw and shuffle consistency.
- Private Hand filtering.
- Interaction conflicts resolved.
- Reconnect restores Seat and state.
- Session reset synchronizes.
- No duplicated/lost Object Instances in test sessions.
