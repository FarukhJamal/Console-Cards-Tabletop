# Console Cards — Testing Strategy

**Document ID:** 14_Testing_Strategy  
**Version:** 1.2
**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Goals

Testing must prove:

- Runtime State integrity.
- Correct Command behavior.
- Correct serialization.
- Predictable interaction.
- Template validity.
- Visibility security.
- Multiplayer continuity when implemented.

## 2. Test Layers

### 2.1 Edit Mode

Test plain C# Domain and Application logic.

Examples:

- Object uniqueness.
- Container transfers.
- Deck ordering.
- Shuffle determinism.
- Hand privacy decisions.
- Console capacity.
- Atomic transactions.
- Duplicate Command rejection.
- Revision behavior.
- Policy decisions.
- Template validation.
- Snapshot round-trip.

### 2.2 Play Mode

Test Unity integration.

Examples:

- State-to-View binding.
- Card selection.
- Drag preview.
- Flip and rotation.
- Deck draw intent.
- Whole-Deck movement.
- Snap suggestions.
- Camera pan/zoom.
- Console focus.
- Reset presentation.

### 2.3 Multiplayer

After networking selection:

- Two Players manipulating different objects.
- Two Players requesting the same object.
- Draw synchronization.
- Shuffle synchronization.
- Private Hand filtering.
- Seat reconnect.
- Late join.
- Snapshot restoration.
- Host loss behavior.
- Duplicate/retried Commands.

## 3. Test Naming

Use behavior-focused names.

```text
MoveObject_WhenObjectIsInDeck_TransfersAtomically
DrawCards_WhenDeckHasInsufficientCards_ReturnsFailureWithoutMutation
SnapshotRoundTrip_PreservesContainerOrder
```

## 4. Test Data

Use builders or fixtures for readable setup.

Avoid sharing mutable global fixtures between tests.

Use deterministic IDs and random seeds.

## 5. Invariant Test Matrix

Mandatory Technical Invariants:

- Object belongs to at most one Container.
- Container contains unique Object IDs.
- Missing references fail validation.
- Duplicate Command is not applied twice.
- Revision increments once per committed transaction.
- Failed transaction does not partially mutate.
- Unauthorized viewer does not receive hidden Definition data.
- Snapshot load validates schema and references.

## 6. Template Tests

- Valid template loads.
- Duplicate IDs fail.
- Missing Definition fails.
- Invalid Container membership fails.
- Unsupported schema fails.
- Session Entry does not construct a Match before a valid choice.
- Empty/Custom Table constructs without Game-specific Template, Board, or rules.
- Selecting Trap Floor uses the Game Template pipeline exactly once.
- Application startup does not force Trap Floor.
- Initial Snapshot matches template.
- Reset restores initial state.

## 6.1 Component Toolbox and Dice Tests

- Toolbox-created Card, Deck, Stack/pile, Pawn, Token, and Die receive stable IDs and authoritative Runtime State.
- Created pieces bind Presentation Views without making GameObjects authoritative.
- Common d4, d6, d8, d10, d12, and d20 options validate their side counts.
- ADR-025 Roll preserves actor context, launches the authority's physical Die, and commits settled pose/value once through its authored face mapping. Throw randomness must not preselect the result. The older RNG use case is not the physical Dice acceptance path.
- Loose physical state preserves full 3D pose/motion, object identity, revisions, stale/duplicate rejection, and snapshot/reset state separately from `TabletopPose`.
- No Table/Board surface hit rejects new loose creation, including every Card in a batch and duplicate placement; failed batches remain atomic.
- Held/released transitions, off-table falling without snap-back, Container physics disable/restore, and all six Dice mappings require physics integration checks.
- Trap Floor's X/Y dice use the same generic Die state and Roll path as toolbox d6.
- Reset and object removal leave no stale View or ownership records.

## 7. Interaction Tests

- Click selects correct object.
- Drag commits final pose.
- Cancel restores accepted pose.
- Top-card drag draws one card.
- Hold-drag moves Deck.
- Flip changes face once.
- Snap bypass uses requested pose.
- Rejected placement rolls back clearly.
- Temporary Interaction Lock clears.

## 8. Persistence Tests

- Serialize/deserialize Match Snapshot.
- Stable IDs preserved.
- Deck order preserved.
- Private visibility preserved.
- Unknown optional fields handled according to schema policy.
- Corrupt snapshot rejected.
- Version migration tested when added.

## 9. Policy Tests

- Free permits Game-rule action.
- Assisted returns guidance.
- Restricted denies selected action.
- Technical Invariant overrides Free.
- Multiple Policies compose deterministically.
- Host override cannot bypass security invariant.

## 10. Regression Tests

Every fixed defect affecting state integrity should receive a regression test where practical.

## 11. Manual Test Charters

Some interaction quality requires manual testing:

- Drag feel.
- Hold threshold.
- Card readability.
- Table navigation.
- Multi-object manipulation.
- Private/public clarity.

Record:

- Build/version.
- Devices.
- Steps.
- Result.
- Defects.

## 12. Performance Tests

Later, measure:

- Large object count.
- Many visible cards.
- Drag update cost.
- Snapshot size.
- Template load time.
- Network payload rate.

Do not set arbitrary performance claims before baseline profiling.

## 13. Test Evidence

Implementation reports must state:

- Test command or Unity Test Runner suite.
- Passed count.
- Failed count.
- Skipped count.
- Manual tests performed.
- Tests not run and why.

## 14. Definition of Done

A milestone is not done because the scene appears to work once.

It is done when:

- Completion criteria are met.
- Required automated tests pass.
- Manual charter is completed where required.
- Known limitations are documented.
- No unresolved state-corruption defect remains.
