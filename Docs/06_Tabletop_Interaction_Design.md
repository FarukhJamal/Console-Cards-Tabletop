# Console Cards — Tabletop Interaction Design

**Document ID:** 06_Tabletop_Interaction_Design  
**Version:** 1.0 Draft  
**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Interaction Goal

Interaction should reproduce physical tabletop freedom while being more predictable than unrestricted physics.

Players must understand what will happen before releasing an object.

## 2. Input Independence

Interaction Intent must not depend directly on one device.

Input adapters translate:

- Mouse and keyboard.
- Controller later.
- Touch later.

into common intents.

The first implementation targets mouse and keyboard.

## 3. Interaction Pipeline

```text
Input Adapter
→ Pointer Context
→ Intent Resolver
→ Interaction State Machine
→ Local Preview
→ Placement Suggestion
→ Command
→ Result
→ Final View Update
```

## 4. Interaction States

Initial states:

- Idle.
- Hovering.
- Pressed.
- DraggingObject.
- DraggingCollection.
- Rotating.
- SelectingMultiple.
- AwaitingAuthority.
- Cancelling.

Avoid many overlapping Boolean fields.

## 5. Core Object Interaction

### 5.1 Select

- Click object to select.
- Click empty space to clear selection.
- Modifier-click adds or removes objects from multi-selection.
- Selection is local unless a shared highlight feature is explicitly enabled.

### 5.2 Move

- Press and drag an object.
- During drag, the local View follows the pointer.
- Placement Guides show suggested destinations.
- Release submits the final Move Command.
- Cancel restores the last accepted pose.

### 5.3 Rotate

Initial proposal:

- Mouse wheel over selected or dragged object rotates in configured increments.
- Modifier may enable fine rotation.
- Rotation increments are configuration, not magic constants.

### 5.4 Flip

Initial proposal:

- Double-click a flippable object.
- Alternative key binding may be added after usability testing.

A flip is a discrete Command.

### 5.5 Inspect

A zoomed local preview may show card text or object details without changing Match State.

## 6. Deck Interaction

The required distinction is:

- Drag the top card to draw one card.
- Hold and drag the Deck body to move the complete Deck.

Intent resolution uses:

- Pointer hit target.
- Press duration.
- Movement threshold.
- Deck visual affordance.
- Modifier input.

Ambiguous input must favor recoverability over speed.

## 7. Drawing Cards

Supported operations:

- Draw one.
- Draw a selected count.
- Deal to Seats.
- Draw to Hand.
- Draw to table.

Free mode does not enforce a Game’s draw limit.

The Platform still validates:

- Deck exists.
- Requested cards exist.
- Transfer can complete structurally.

## 8. Stack Interaction

Supported operations:

- Place card onto Stack.
- Remove top card.
- Move complete Stack.
- Split at selected index later.
- Merge compatible collections.
- Preserve logical order.

The first build may implement top-card removal and whole-Stack movement before arbitrary split UI.

## 9. Hand Interaction

- Drag card into Hand to transfer ownership/visibility.
- Drag card out to play on table.
- Reorder locally within Hand.
- Fan or row layout is presentation.
- Other Players see only authorized card information.
- A Hand remains distinct from a Console.

## 10. Console Interaction

- Drag compatible card to Console Slot.
- Drag card out when free interaction permits.
- Reorder within multi-capacity slots where configured.
- Focus-own-Console command moves Camera, not Match State.
- The first build does not enforce Move Card costs automatically.

## 11. Multi-Selection

Initial scope:

- Modifier-click selection.
- Drag selected group as one temporary manipulation.
- Preserve individual poses relative to group origin.
- Final command may be a batch transaction.

Marquee selection may be deferred.

## 12. Placement Suggestions

During drag:

1. Query nearby Play Areas, Zones, Slots, and Containers.
2. Calculate candidate placement.
3. Display highlight and preview.
4. Allow snap bypass.
5. Submit requested and suggested placement data as required.
6. Authority applies the accepted result.

## 13. Snap Bypass

An explicit modifier must permit freeform placement unless Policy blocks bypass.

Initial proposal:

- Hold `Alt` during drop.

Binding remains configurable.

## 14. Camera Controls

Initial top-down controls:

- Middle-mouse drag or edge-independent pan.
- Mouse wheel zoom when not rotating an object.
- Focus own Console.
- Focus primary Play Area.
- Focus selected object.
- Return to default Seat view.

Input conflict between zoom and rotate must be resolved through selection/drag context and user testing.

## 15. Object Locking

Players may lock setup objects against accidental movement.

Locking is separate from temporary Interaction Lock.

Types:

- User lock: persistent Match State.
- Interaction Lock: temporary concurrency control.

Free mode may allow host or owner to unlock manually.

## 16. Error Feedback

Rejected actions must provide:

- Clear visual rollback.
- Brief reason.
- No partial mutation.
- No duplicate sound or animation.
- Sufficient logging for debugging.

## 17. Accessibility and Usability

Design for:

- Adjustable drag threshold.
- Adjustable hold duration.
- Adjustable rotation increment.
- Clear selection outline.
- Distinct private/public areas.
- Large clickable Deck top.
- Reduced motion option later.
- Keyboard alternatives for critical actions.

## 18. Interaction Completion Criteria

An interaction is complete only when:

- Authority accepts the Command.
- Runtime State changes.
- Revision increments where applicable.
- Views reconcile to accepted state.
- Temporary locks clear.
- Failure leaves accepted state unchanged.

## 19. Deferred Interactions

Not required in the earliest build:

- Advanced measuring tools.
- Freehand drawing.
- Physics flicking.
- Arbitrary object scaling.
- Complex Stack splitting UI.
- Touch gestures.
- Controller radial menus.
- VR manipulation.
