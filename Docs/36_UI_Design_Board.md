# 36 — UI Design Board (UI-0)

Version 0.1 · 2026-10-23 · Status: draft for the owner's approval. Source: the designer's Figma file (owner's copy,
"🕹️Game: Console Cards (Copy)", page **UI Kit**: frame **V5** and section **UI Components**). Read only; nothing in the
file was changed.

This page turns the Figma UI Kit into the plan for the new in-game UI. Each later stage (UI-1 … UI-5) builds one part
of it through the builders, as Final UI prefabs (doc 26), so everything stays editable in Unity.

---

## 1. The look (from the Figma file)

| Token | Value | Used for |
|---|---|---|
| Font | **Oxanium** Bold (titles, buttons) and Medium (body) | All UI text. The project has Chakra Petch and Silkscreen; Oxanium must be added (free Google font, OFL). |
| Title size | 18 px Bold, upper case | Panel titles ("RULES • TRAP FLOOR", "RED PLAYER"), buttons |
| Body size | 14 px Medium | Rule lines, rows, log |
| Panel | `#222527` | Panel body |
| Row | `#292E2F`, 1 px border `#424547`, radius 4 | Rule lines, status rows |
| Highlight row | `#1C3C3D`, border `#01F7F9`, 4 px left bar `#06EFF0` | The current rule / selected item |
| Accent (cyan) | `#06EFF0` / `#01F7F9` | Hints, focus, selected, dots on buttons |
| Button | `#1C1C1C`, border `#3C4143`, radius 8, inner bevel | FOLD, END TURN, CONTINUE |
| Secondary button | flat grey, grey dot | SKIP TURN, BACK |
| Warning chip | amber fill, dark text | "OVERCHARGED +1" |
| Frame | dark plate with corner screws and hatched end caps; menus use **orange** end caps | Every panel and bar |
| Canvas | 1440 × 810 in Figma | Our UI scales from 1920 × 1080 (× 1.333); proportions stay the same. |

Player colours in text: Blue, Red, Green, Yellow (as in "Blue found a Key.").

## 2. The in-game screen (Figma frame V5)

| Place | Figma component | Replaces (today) | Holds |
|---|---|---|---|
| Top left | **Top** bar | SessionBar | Game name (dot + TRAP FLOOR), Undo, Redo, SHOP, TABLE |
| Top centre | **Round** bar | Turn strip (TrapFloorHud) | "ROUND 3", "RED'S TURN · 2 MOVES", the player dots (current one ringed) |
| Under it | **Hint** bar | Hint / NOW line | HINT chip + one line: what to do now (always shown, U3) |
| Left | **Rules** card | RulesCard | Title, rule lines (current one highlighted), "House rules: n change", FOLD |
| Top right | **Player Card** | Trap Floor status card | Player name, Avatar, Keys n / n, Status chips, In hand, Floor n · difficulty |
| Under it | **END TURN** / **SKIP TURN** | Turn panel buttons | The main button (changes with the state, U2) and the second one |
| Bottom left | **Log** | Activity log toasts | The last 2–3 game events, player names in their colour |
| Bottom left | **+ TOOLBOX** | ComponentToolbox button | Unchanged role |
| Bottom right | **Controls** bar | InteractionGuide strip | DRAG move · RIGHT-CLICK menu · SPACE inspect · L log · H help |
| Table | Seat labels "Player 1 … 5" on each edge | Seat name plates | The table in V5 has straight edges with one player per edge (fits the hexagon, T-3) |

## 3. The menu screens (Figma section UI Components)

| Figma component | Becomes |
|---|---|
| **Menu** bar: CONSOLE CARDS · PLAY · CREATE · SANDBOX · GUIDE · SETTINGS · EXIT | A new main menu bar (start screen). Answers U6: the Table menu's 20 rows split into these areas. |
| **Actions**: PLAY / CREATE / SANDBOX big buttons | Start screen choices. PLAY → game selection, CREATE → card editor, deck editor, save table as a game (the "Workshop"), SANDBOX → empty table. |
| **Game cards** (Frame 387): TRAP FLOOR, TOWER CLASH, COLOR CONTROL, with picture, text, "2–4 Players" | The game list in New table… (GameSelectPanel). Tower Clash and Color Control are placeholders until those games exist. |
| **Player Profile** + **Avatar** (ALEX, < > picker) + BACK / CONTINUE | The profile panel and Avatar choice, as one step before choosing a game. |

## 4. Not in the Figma file yet (to ask the designer, or we style them from the kit)

These screens exist in the game but have no design. Unless the designer sends them, they get the kit's look (panel,
rows, buttons, orange end caps for popups):
1. Centre popups: trap (NOW / NEXT ROUND), Key, Ability / Friend (Take it / Reject), collapse, STARTING NEXT PHASE,
   "THIS TURN" effects, Undo warnings.
2. Payment / "YOU LANDED ON" card fan (FocusedCardSelection).
3. Right-click menu (TabletopPopup) and the quantity popup.
4. Card inspect.
5. Shop.
6. Card editor, deck editor, save table as a game (CREATE).
7. Table mode panel (shape, size).
8. Settings and Guide pages (new in the menu bar).
9. Toasts for warnings (amber) and the turn-start banner.

## 5. What is needed from the owner

1. **Approve or change** §2 and §3 (especially: the start screen with PLAY / CREATE / SANDBOX replaces today's Table menu).
2. **Assets**: this session cannot download from figma.com. Export from the file, as PNG at 2× (or SVG): the panel
   frames ("Union" shapes), the corner **Plate** pieces (grey and orange), the hatched end caps, the small icons
   (undo, redo, shop, table, key, users, chevrons, edit pencil), and the Avatar picture. Put them in a folder in the
   project, for example `Assets/ConsoleCards/Content/UI/Figma/`, and tell me.
   (Alternatively allow `figma.com` under claude.ai Settings › Capabilities, and I fetch them myself.)
3. **Font**: add Oxanium (Bold, Medium) to `Content/UI/Fonts/`, or say if Chakra Petch should stay.
4. The missing designs in §4, if the designer has them.

## 6. Build order (unchanged plan, now with the kit)

| Stage | Builds |
|---|---|
| T-1 / T-2 / T-3 | Piece sizes, no shrinking; the table grows to fit; hexagon instead of star |
| UI-1 | Theme (font, colours, frames) + notifications: Hint bar, Log, toasts, centre popups |
| UI-2 | Round bar, Player Card, END TURN / SKIP TURN as one main button by state |
| UI-3 | Top bar, Menu bar and the start screen (PLAY / CREATE / SANDBOX), right-click menus that do not close panels, one confirmation pattern |
| UI-4 | Game cards, Player Profile and Avatar step; text pass (no code words, one glossary) |
| UI-5 | Table looks (card colours, dice, leaning die, camera framing) |
| CUT-2 | Remove the dead code |
