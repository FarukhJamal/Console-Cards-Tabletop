# 26 — Final UI

Version 0.1 · Status: Implemented (UI-F, 2026-10-13)

## What it is

Every UI prefab the game shows lives in `Assets/ConsoleCards/Content/Prefabs/Final/UI/` and is yours to edit
in Unity. The UI prefab catalog (`Real/UI/RealUiPrefabCatalog.asset`) points at these prefabs; the game loads
nothing else.

| Prefab | Shows |
|---|---|
| SessionBar | Top-left game button, undo/redo and the Table menu |
| ComponentToolbox | + TOOLBOX button and panel |
| RulesCard, RulesHeading, RulesLine | Rules card (left column) and its rows |
| TrapFloorHud | Turn strip (top centre) and Trap Floor status card (top right) |
| InteractionGuide | Controls strip and Help card (bottom right) |
| TabletopPopup, ActionRow | Right-click menu and its rows |
| QuantityPopup | Draw count and merge panels |
| CardInspect | Card inspection |
| AbilityPurchase | Ability purchase |
| FocusedCardSelection, FocusedCardOption | Payment and "YOU LANDED ON" popup and its cards |
| GameTemplateRow | Game list row |

## Rules

- **Builders never overwrite.** The editor commands under Console Cards > UI and > Toolbox now only *create a
  missing* prefab in Final UI. To get a built default back, delete that prefab and run the command.
  Toolbox > Sync Tiles only refreshes the tile list from the component library.
- **No layout from code.** Panels, anchors and sizes come from the prefab. Code only places cards along the
  payment/reveal fan (spacing, height, tilt and the reveal card size are Inspector fields on
  FocusedCardSelection; the fan card size is on FocusedCardOption) and moves the right-click menu to the cursor.
- **Keep the references.** Each prefab root has its view script with references to its parts. Restyle, move,
  re-anchor and resize freely; do not delete referenced parts.
- **Canvas.** The UI root scales from 1920 × 1080, matching width and height equally (0.5).
- The older copies in `Real/UI/` are no longer used and can be deleted.
