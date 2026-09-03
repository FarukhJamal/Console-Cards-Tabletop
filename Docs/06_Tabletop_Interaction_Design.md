# Console Cards — Tabletop Interaction Design

**Document ID:** 06_Tabletop_Interaction_Design  
**Version:** 1.3

**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Interaction Goal

Interaction should reproduce physical tabletop freedom with clear controlled holding and physical release (ADR-025).

Players must understand the intended placement or transfer and that physical release may collide, tumble, or fall beyond the Table.

Loose Cards, Pawns, Tokens, and Dice share Rigidbody/collider interaction. Holding is temporarily controlled/kinematic; release is dynamic. The authority accepts settled 3D state through Commands/Application Use Cases, not direct View mutation. Deck/Stack/Console bodies and contained Card layouts retain their existing non-physical positioning.

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

For loose physical objects, validated grab/release/Roll intent drives the authority's simulation. Settled pose/result is then committed through the Command/Application boundary and reconciled to Views. A local preview does not independently decide an accepted outcome; future host/server physics is authoritative.

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
- Click-hold-drag from empty tabletop space creates a marquee selection box for Cards.
- Every Card included in the current selection displays clear selected feedback.
- Selection is local unless a shared highlight feature is explicitly enabled.

### 5.2 Move

- Press and drag an object.
- While a loose physical object is held, it is temporarily kinematic with gravity off, lifted clear of surfaces/objects, and follows the pointer.
- Placement Guides show suggested destinations.
- A live indicator previews the intended surface placement or Container layout for a Card/group; it does not promise the final resting pose of a physical throw.
- Release restores dynamic physics/gravity and preserves release velocity/throw momentum. Collision, velocity, and torque determine the subsequent motion.
- Releasing or throwing off the Table is allowed to fall naturally, even without a valid surface hit. It is not an invalid movement release and must not snap back.
- Physics settlement commits the final 3D pose through the authoritative Application path; Dice also resolve and commit the settled value.
- Deck/Stack/Console bodies keep existing positioning. Invalid non-physical movement requests still leave accepted state unchanged and reconcile the View.
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

### 5.4.1 Roll Die

Roll validates actor intent, physically lifts/throws the Die, and applies randomized impulse and torque. The authority resolves the settled face using explicit authored mappings for d4/d6/d8/d10/d12/d20, including each variant's result-reading convention; mesh triangle order and object names are not value mappings. Manual grab/throw uses the same settle/value-resolution path. Final 3D pose and value commit together to Die State. Trap Floor's two d6 use this generic path.

### 5.5 Inspect

A zoomed local preview may show card text or object details without changing Match State.

### 5.6 High-Stakes Card Choice

When a Game asks a Player to choose between Cards as a strategic decision:

- Present the choices large, central, and readable.
- Provide distinct hover, candidate selection, confirmation, and registered-choice feedback.
- Allow the Player to hide the choice UI temporarily to inspect the Game Board.
- Preserve the pending choice while hidden and allow the Player to reopen it.
- Keep the choice UI isolated from tabletop and Camera input.

The Platform owns this Presentation capability. The Game or Game Template supplies the candidates and determines what a confirmed choice means.

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

Deck/Stack/Hand/Console/Slot and other Container layouts control contained Cards with loose physics disabled. Accepted extraction enables physical loose-object behavior; accepted insertion restores layout control. Membership and ordering remain authoritative and transfers remain atomic. Container bodies are not converted to physics in this pass.

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

Required scope:

- Modifier-click may add or remove Cards from the current selection.
- Click-hold-drag on empty tabletop space creates a visible marquee.
- Cards inside the accepted marquee become selected according to the approved inclusion rule.
- Every selected Card displays unambiguous local feedback.
- Drag the selected group as one temporary manipulation.
- Preserve individual poses relative to the group origin.
- Preview the group landing arrangement before release.
- Accepted group operations remain atomic; held previews never directly mutate Runtime State. Physical release may change relative resting poses through collision; settlement commits separate physical poses rather than imposing the preview as the final layout.

Detailed marquee inclusion, overlapping-Card, and mixed-Container behavior remain in `OPEN_DECISIONS.md`.

## 12. Placement Suggestions

During drag:

1. Query nearby Play Areas, Zones, Slots, and Containers.
2. Calculate candidate placement.
3. For loose physical placement, raycast valid Table/Board surfaces; handle explicit Container targets through the existing transfer/layout path.
4. Hide or invalidate new loose-placement preview when there is no valid surface hit. This does not cancel a held object's off-table release.
5. Display highlight and preview for a valid candidate.
6. Display the intended surface placement or Container layout for the current Card/group, not a guaranteed post-throw resting pose.
7. Allow bypass of optional layout suggestions without fabricating a physical surface hit.
8. Submit requested and suggested placement data as required.
9. Authority applies the accepted result.

### 12.1 Physical Surfaces and Loose Placement

The real fixed Table and Game Boards have explicitly authored valid collision surfaces, editable in Unity with their owning Table/Board and following its Transform/scale. Decorative geometry and mesh bounds are not gameplay authority.

Toolbox placement for loose Cards, Pawns, Tokens, and Dice raycasts only valid Table/Board physical surfaces. Preview uses the hit position; confirmation creates authoritative loose state slightly above the hit surface so physics can settle it. No valid hit means no valid preview and no commit. Card batches and duplicate placement apply this rule to every created loose object.

Normal loose placement/movement does not use the old mathematical placement plane or `TabletopSurfaceProxy`. `TabletopPose` remains unchanged for authored/template/container layout; separate authoritative 3D physical pose/state carries loose simulation outcomes.

Creation needs a surface, but release does not: objects thrown or released past the Table may fall naturally and must not be snapped back. Missing a surface is not grounds to reject a settled physical pose. Technical and actor/permission validation still apply.

Game Boards catch objects through their authored collision surfaces. Logical Play Areas, Zones, mats, and guides are not collision authority merely because they have bounds. Deck/Stack/Console bodies remain on the existing non-physical positioning path with the surviving ADR-024 authored-area rules; this pass does not add missing Container movement behavior.

## 13. Snap Bypass

An explicit modifier must permit bypass of placement suggestions or Play Area snapping unless Policy blocks bypass. It cannot make new loose placement valid without a physical Table/Board hit. Off-table physical release is normal behavior, not a bypass action. Non-physical Container placement retains ADR-024's boundary.

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

An accepted physical release that falls off the Table is not a rejected action and must not trigger rollback.

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
- Arbitrary object scaling.
- Complex Stack splitting UI.
- Touch gestures.
- Controller radial menus.
- VR manipulation.
