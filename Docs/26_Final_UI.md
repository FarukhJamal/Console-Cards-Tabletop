# 26 — Final UI

Version 0.3 · Status: Implemented (UI-F, 2026-10-13); Shop entry and Shop panel added 2026-10-15; profile panel and Profile row added 2026-10-16; card editor added 2026-10-16

## What it is

Every UI prefab the game shows lives in `Assets/ConsoleCards/Content/Prefabs/Final/UI/` and is yours to edit
in Unity. The UI prefab catalog (`Real/UI/RealUiPrefabCatalog.asset`) points at these prefabs; the game loads
nothing else.

| Prefab | Shows |
|---|---|
| SessionBar | Top-left game button, undo/redo, the Shop entry (shop icon and the word Shop; Trap Floor while the Shop is on) and the Table menu (Card art, then Card editor…, then "Profile: name…") |
| ComponentToolbox | + TOOLBOX button and panel |
| RulesCard, RulesHeading, RulesLine | Rules card (left column) and its rows |
| TrapFloorHud | Turn strip (top centre) and Trap Floor status card (top right) |
| InteractionGuide | Controls strip and Help card (bottom right) |
| TabletopPopup, ActionRow | Right-click menu and its rows |
| QuantityPopup | Draw count and merge panels |
| CardInspect | Card inspection |
| AbilityPurchase | The Shop: every Ability with its kind, cost and picture; Buy, or "Not for sale" without a price (header and list titles set from code) |
| FocusedCardSelection, FocusedCardOption | Payment and "YOU LANDED ON" popup and its cards |
| GameTemplateRow | Game list row |
| CardEditorPanel, CardEditorRow | Card editor (L2-2): the card list with + New card; the preview (front and back on the white base card, and the card's words); the fields (code, name, description, cost, copies, works like, pictures); Delete, Reset, Save |
| ProfilePanel, ProfileRow | Player profile panel (doc 28): who is playing, every profile with Play as, a name box with New profile and Rename this profile, and Open profiles folder |

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
- **Adding to existing prefabs.** *Console Cards > UI > Add Missing Table Menu Rows (Final UI)* also adds the Shop entry to an existing SessionBar (before the Table button; icon `Content/UI/Session/Icons/IconShop.png`). *Create Missing Gameplay UI Prefabs (Final UI)* wires the Shop title labels in an existing AbilityPurchase. Nothing else in those prefabs changes.
- **Profile and card editor (2026-10-16).** *Add Missing Table Menu Rows* adds the "Card editor…" and "Profile: name…" rows under Card art in an existing SessionBar. *Create Missing Gameplay UI Prefabs* makes ProfilePanel, ProfileRow, CardEditorPanel and CardEditorRow when they are missing.
