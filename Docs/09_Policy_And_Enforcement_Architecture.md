# Console Cards — Policy and Enforcement Architecture

**Document ID:** 09_Policy_And_Enforcement_Architecture  
**Version:** 1.0 Draft  
**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Principle

> **Freedom by default, enforcement by configuration.**

The first builds preserve physical-table freedom. The architecture must still allow future restrictions.

## 2. Policy Decision

A Policy evaluates context and returns an explicit decision.

```text
PolicyDecision
- Outcome
- Reason Code
- User Message
- Suggested Alternative?
- Can Override?
```

Outcomes:

- Allow.
- AllowWithGuidance.
- Warn.
- Deny.
- RequireAuthorityOverride.

## 3. Enforcement Levels

### Free

Game-rule actions are not blocked.

Technical Invariants still apply.

### Assisted

The Platform highlights, suggests, or warns but normally allows continuation.

### Restricted

Configured actions are blocked.

### Enforced

Implemented Game rules control complete relevant flow.

The existence of an `Enforced` level does not imply a generic Rule engine is being built now.

## 4. Policy Categories

### 4.1 Interaction Policy

Controls:

- Who may manipulate an object.
- Whether object is locked.
- Whether multi-selection is permitted.
- Whether complete Deck may be moved.

### 4.2 Placement Policy

Controls:

- Bounds.
- Grid occupancy.
- Snap bypass.
- Allowed object types.
- Slot capacity.
- Rotation restrictions.

### 4.3 Ownership Policy

Controls:

- Manipulation by owner, Seat, host, or any Player.
- Transfers between Players.
- Access to personal areas.

### 4.4 Visibility Policy

Controls:

- Definition data distribution.
- Face visibility.
- Hand privacy.
- Hidden Containers.
- Authority-only information.

Visibility Policy is a security boundary, not only a visual preference.

### 4.5 Draw Policy

Future policy for:

- Maximum draw count.
- Draw timing.
- Allowed destination.
- Deck access.

Initial free implementation permits structurally valid draws.

### 4.6 Turn Policy

Future policy for:

- Active Player.
- Turn sequence.
- Allowed actions by phase.

No universal turn system is required in the first build.

### 4.7 Rule Enforcement Policy

Future connection to optional implemented Game rules.

## 5. Technical Invariants

Policies cannot disable Technical Invariants.

Always enforced:

- Unique Object Instance.
- Valid Container membership.
- Duplicate Command protection.
- Valid snapshot schema.
- Authorized private data delivery.
- Atomic transaction behavior.
- Interaction conflict resolution.
- Valid stable IDs.

## 6. Evaluation Order

Recommended order:

1. Command structural validation.
2. Authority validation.
3. Technical Invariants.
4. Visibility/ownership security.
5. Configured Policies.
6. Transaction preparation.
7. Commit.

A Game Policy must never override security or state integrity.

## 7. Policy Sources

Policies may be selected by:

- Platform defaults.
- Game Template defaults.
- Match host configuration.
- Future Game automation.
- Seat-specific settings where approved.

Precedence must be explicit.

Proposed precedence:

```text
Technical Invariants
> Platform security policy
> Match authority configuration
> Game Template policy
> User preference
```

## 8. Overrides

Manual host override may be supported for selected warnings or restrictions.

Overrides:

- Are explicit.
- Are logged.
- Do not bypass Technical Invariants.
- May require confirmation.
- Are not assumed for private data access.

## 9. Policy Composition

Prefer small policies with deterministic composition.

Avoid one `RulePolicy` containing every possible Game decision.

If multiple Policies evaluate one Command:

- Any mandatory Deny wins.
- Warnings are aggregated.
- Allow requires no blocking decision.
- Conflicts produce explicit error.

## 10. Policy Data

Configuration may include:

- Enforcement level.
- Allowed categories/tags.
- Capacity.
- Bounds.
- Owner rules.
- Bypass permission.
- Warning text.

Do not store executable arbitrary expressions in the first version.

## 11. First-Build Defaults

- Game Rules: Free.
- Placement: Optional/Strong snapping depending on target.
- Hand visibility: Restricted to owner.
- Object interaction: Free unless locked or conflicting.
- Setup objects: Host may lock.
- Console slots: Structural capacity may apply.
- Turn rules: None.
- Draw rules: None beyond available cards.

## 12. Testing

Every Policy requires tests for:

- Free behavior.
- Assisted behavior.
- Deny behavior where implemented.
- Override behavior.
- Conflict composition.
- Technical Invariant precedence.
- Unauthorized visibility.

## 13. Non-Goal

This architecture is not a promise to implement a universal programmable Rule engine.

It is a controlled decision layer that keeps future enforcement possible.
