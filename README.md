# Console Cards

Console Cards is a multiplayer, top-down virtual tabletop Platform centered around Button Cards, private Hands, player Consoles, and freeform tabletop interaction.

## Current Status

**M0 through M4.1 are implemented. The current local prototype also contains the Template-driven Trap Floor tabletop foundation and Floorfall targeting foundation; current M3 closure verification evidence remains separately tracked.**

- M0 completed the Core/Application foundation.
- M1 completed the tabletop visual and Camera foundation.
- M2 completed generic Card, Pawn, and Token interaction in the local Tabletop prototype.
- M4 added the Unity-free Player Layout and central Play Area foundation.
- M4.1 added the minimum Game Template schema, validation, atomic Match construction, and in-memory reset baseline.

The current prototype supports explicit Card, Pawn, and Token Views; selection with visible local feedback; mathematical tabletop pointer projection; collider-based object selection; drag preview; accepted movement; cancel/rollback; rotation; Card flipping; Deck draw/shuffle; Stack merge/split; Hand reorder; Discard and Console Slot transfer; local interaction locks; orthographic Camera pan/zoom; prototype composition; and an integrated `TabletopPrototype` scene.

The immediate next slice is the shared **Session Entry + Component Toolbox Foundation**: choose Empty/Custom Table or an available Game Template, stop automatically forcing Trap Floor at Play startup, add authoritative generic tabletop pieces, and promote Dice to first-class objects. Delivery then continues with Trap Floor playable, Super Leroy Sisters playable, and remaining shared Phase 1 closure requirements. The approved Trap Floor direction is defined in `Docs/18_Trap_Floor_Game_Requirements.md`.

## Approved Technical Baseline

- Fresh Unity project.
- Unity `6000.5.4f1`.
- Universal Render Pipeline.
- Windows desktop first.
- 3D tabletop with orthographic top-down Camera.
- Controlled movement for Cards and pieces.
- Unity New Input System.
- Mouse and keyboard first.
- Git using GitHub Desktop.
- Standard Card dimensions: `1.0 × 1.4 × ~0.02` Unity units.
- Prototype Card spacing: configurable, default `0.10` units.

See `Docs/TECHNICAL_BASELINE.md`.

The authoritative PDF-derived layout and interaction requirements are traced in `Docs/17_Layout_Design_Requirements_Matrix.md`.

## Documentation Reading Order

1. `Docs/00_Product_Vision.md`
2. `Docs/02_Terminology.md`
3. `Docs/03_Project_Principles.md`
4. `Docs/01_Platform_Architecture.md`
5. `Docs/TECHNICAL_BASELINE.md`
6. `Docs/17_Layout_Design_Requirements_Matrix.md`
7. `Docs/OPEN_DECISIONS.md`
8. `Docs/16_Milestones_And_Roadmap.md`
9. `AGENTS.md`

See `DOCUMENTATION_INDEX.md` for the complete hierarchy.

## Next Action

Implement the approved Session Entry + Component Toolbox Foundation inside G1, then continue the remaining Trap Floor playable flow without resolving deferred Game rules by inference.

## Networking

Networking technology and host migration remain intentionally deferred to M6.

## Tests

Latest verified M2 closure evidence:

- Edit Mode: 828 passed
- Play Mode: 793 passed
- Failed: 0
- Skipped: 0
- Unity compilation errors: 0
