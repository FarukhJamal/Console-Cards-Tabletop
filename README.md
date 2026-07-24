# Console Cards

Console Cards is a multiplayer, top-down virtual tabletop Platform centered around Button Cards, private Hands, player Consoles, and freeform tabletop interaction.

## Current Status

**M0, M1, and M2 are complete.**

- M0 completed the Core/Application foundation.
- M1 completed the tabletop visual and Camera foundation.
- M2 completed generic Card, Pawn, and Token interaction in the local Tabletop prototype.

The current prototype supports explicit Card, Pawn, and Token Views; selection with visible local feedback; mathematical tabletop pointer projection; collider-based object selection; drag preview; accepted movement; cancel/rollback; rotation; Card flipping with visible face projection; deterministic shared-frame input routing; local interaction locks; orthographic Camera pan/zoom; prototype composition; and an integrated `TabletopPrototype` scene.

M3, Decks, Stacks, Hands, and Consoles, is the next milestone.

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

## Documentation Reading Order

1. `Docs/00_Product_Vision.md`
2. `Docs/02_Terminology.md`
3. `Docs/03_Project_Principles.md`
4. `Docs/01_Platform_Architecture.md`
5. `Docs/TECHNICAL_BASELINE.md`
6. `Docs/OPEN_DECISIONS.md`
7. `Docs/16_Milestones_And_Roadmap.md`
8. `AGENTS.md`

See `DOCUMENTATION_INDEX.md` for the complete hierarchy.

## Next Action

Begin M3: Decks, Stacks, Hands, and Consoles.

## Networking

Networking technology and host migration remain intentionally deferred to M6.

## Tests

Latest verified M2 closure evidence:

- Edit Mode: 828 passed
- Play Mode: 793 passed
- Failed: 0
- Skipped: 0
- Unity compilation errors: 0
