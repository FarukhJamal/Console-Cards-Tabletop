# Console Cards — Coding Standards

**Document ID:** 13_Coding_Standards  
**Version:** 1.0 Draft  
**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. General

- Use clear, complete names.
- One primary public type per file.
- File name matches primary type.
- Use explicit access modifiers.
- Prefer `sealed` when inheritance is not intended.
- Prefer immutable value types for IDs and small values.
- Keep methods focused.
- Avoid speculative abstractions.

## 2. Naming

- Types and public members: `PascalCase`.
- Parameters and locals: `camelCase`.
- Private fields: `camelCase`.
- Serialized private fields: `[SerializeField] private`.
- Interfaces: `IName`.
- IDs: `SomethingId`.
- Runtime models: `SomethingState`.
- Static content: `SomethingDefinition`.
- Commands: `VerbNounCommand`.
- Results: `SomethingResult`.
- Domain Events: past tense where practical, such as `CardFlippedEvent`.
- Views: `SomethingView`.
- Adapters: `TechnologyPurposeAdapter`.

## 3. Fields and Properties

Prefer:

```csharp
[SerializeField] private CardView cardPrefab;
public CardId Id { get; }
```

Avoid public mutable fields.

Do not expose mutable `List<T>` from Domain models. Return read-only views or controlled methods.

## 4. Nullability

Enable and respect nullable reference types where project compatibility permits.

Validate required dependencies at construction or initialization.

Do not use null as an undocumented state.

## 5. Error Handling

Use explicit Results for expected failures.

Use exceptions for programmer errors, invalid construction, corrupted persistence, or truly exceptional conditions.

Do not swallow exceptions.

Logs must include relevant IDs and operation context without exposing private card data unnecessarily.

## 6. Guard Clauses

Validate at boundaries:

- Constructor arguments.
- Command payloads.
- Definition references.
- Snapshot schema.
- Content resolution.
- Authority requests.

Do not duplicate the same validation across every layer.

## 7. Unity MonoBehaviours

MonoBehaviours should focus on:

- View binding.
- Input adaptation.
- Unity lifecycle integration.
- Animation.
- Scene/prefab references.

Avoid large gameplay methods in `Update`.

Do not use `Update` on every Card View when a centralized interaction/update service can do the work.

## 8. Dependency Injection

Use constructor injection for plain C# classes.

For MonoBehaviours, use controlled initialization or serialized local presentation dependencies.

Forbidden:

- Static mutable service access.
- General service locator.
- `FindObjectOfType` for architecture wiring.
- Scene name searches for dependencies.

## 9. ScriptableObjects

Use for:

- Definitions.
- Authored configuration.
- Game Template assets where appropriate.

Do not use for:

- Current Match state.
- Current owner.
- Current deck order.
- Current object position.
- Connection state.

## 10. Async

Use `Task` and `CancellationToken` for operations that may wait, such as:

- Content loading.
- Networking.
- Persistence.
- Scene transitions.

Avoid `async void` except required Unity event signatures.

Handle cancellation explicitly.

## 11. Collections and Data Structures

Choose structures by operation:

- Ordered card collections: controlled list-like structure.
- Lookups by ID: dictionary.
- Unique membership: set where appropriate.
- Decks are not simple `Stack<T>`.
- Do not optimize with exotic structures without profiling.

## 12. Commands

Commands:

- Are immutable.
- Contain stable IDs and serializable values.
- Do not contain GameObject, Transform, Sprite, or vendor network references.
- Have unique Command ID.
- State requester identity.
- May include expected revision.

## 13. Events

Use typed events.

Avoid string event names and unrestricted global event buses.

Event subscribers must be cleaned up deterministically.

## 14. Comments

Comments explain intent, constraints, or non-obvious reasoning.

Do not narrate obvious code.

Public architectural contracts should have XML documentation.

TODO comments must include:

- Reason.
- Owner or milestone.
- Clear next action.

## 15. Magic Values

Use named configuration or constants.

Interaction thresholds belong in configuration.

Do not create a ScriptableObject merely to avoid one sensible constant.

## 16. Transactions

Multi-step state changes must be atomic.

Do not mutate state before all preconditions are validated unless rollback is guaranteed and tested.

## 17. Logging

Use structured logging where practical.

Include:

- Match ID.
- Command ID.
- Player ID or Seat ID.
- Object ID.
- Revision.
- Result.

Never log hidden card definitions to unauthorized client logs.

## 18. Performance

- Profile before optimizing.
- Avoid allocations in known high-frequency drag/update paths when measurable.
- Pool Views only when instantiation becomes a demonstrated issue.
- Do not introduce ECS prematurely.
- Keep authoritative state updates discrete.

## 19. Prohibited Patterns

- Giant `GameManager`.
- Public mutable singleton.
- Runtime Match State in ScriptableObjects.
- Game-specific checks in Platform modules.
- Direct networking calls from Views.
- `Resources.Load` as the general content strategy.
- Reflection-based auto-registration without approval.
- Deep object inheritance.
- Silent catches.
- Empty tests.
- Claiming tests ran when they did not.

## 20. Completion Standard

Code is not complete until:

- It compiles.
- Relevant tests pass.
- No unapproved warnings are introduced.
- Documentation affected by the change is updated.
- Implementation report states exactly what changed and what was not tested.
