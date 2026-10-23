# 36 — UI Design Board (UI-0)

Version 0.3 · 2026-10-23 · Status: UI-1 started (start screen approved; Oxanium added; restyle in place; owner exports the frame art). Source: the designer's Figma file (owner's copy,
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

## 4. Screens the designer did not draw (now designed from the kit, §7)

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

## 7. Screens added in Figma from the kit (2026-10-23)

Owner, 2026-10-23: "Can you design them by using UI Kit?" Added on the **UI Kit** page of the owner's copy, to the right
of the kit, in four sections. Everything reuses the kit's own pieces (the corner plates, buttons, panel colours, the
Oxanium font, the menu bar, game cards, profile and avatar panels) and new local components: **Button/Primary**,
**Button/Secondary** (from END TURN and SKIP TURN), **Chip/Amber, Cyan, Red, Dark**, **Row/Default**, **Row/Selected**
and **Card/Slot**. Card pictures are the kit's cyan placeholders.

| Section | Frames |
|---|---|
| Kit components (Claude) | The components above |
| Centre popups (Claude) | Trap NOW, Trap NEXT ROUND, Key, Ability, Friend, Collapse, Starting next phase, This turn, Warning with Undo, Confirm |
| Notifications, menus, cards and shop (Claude) | Turn banner, event toast (kit Log), warning toast, loss toast, right-click menu, amount popup, payment card fan, card inspect, Shop |
| Screens (Claude) | Start (PLAY / CREATE / SANDBOX), Play — Players (profile + avatar), Play — Choose game (game cards + setup: difficulty, win mode, players, turns, deck), Settings, Guide, in-game Table panel (shapes with **Hexagon**, size, "fit the players") |

Rules for these frames:
- **Orange frame** = a popup that asks something or a menu screen; **grey frame** = a tool panel (right-click menu,
  amount, card inspect, table, guide).
- The main button is always **Button/Primary** on the right; the way out (Cancel, Reject, Back, Not yet) is
  **Button/Secondary** on its left.
- Popups are 520 wide (payment 940, shop 760); text 16 body, 24 title, 14 muted subtitle.

Still to design: the CREATE screens (card editor, deck editor, save table as a game). They come with UI-3.

Assets: the picture parts are taken from Figma as PNG renders when each stage needs them (figma.com downloads are not
allowed in this session; renders through the Figma connection work).

## 8. UI-1 — the kit look and the notifications (2026-10-23)

Owner's choices (2026-10-23): **restyle the existing Final UI prefabs in place** (layout and hand edits stay);
**the owner exports the frame art from Figma** (the Figma connection is at its monthly limit); **notifications move to
the V5 layout**.

**Built: Console Cards > UI > Apply Kit Look (UI-1)** (`Editor/UI/KitLookApplier.cs`). One click restyles every
prefab in `Content/Prefabs/Final/UI/`; running it again changes nothing.

| Part | Old | Kit |
|---|---|---|
| Labels | Silkscreen Bold, Chakra Petch Bold / Medium, Arial | Oxanium Bold (titles, bold) / Medium (body) |
| Panels, rows, buttons, chips, pills | cream sprites with a thick black outline (`Toolbox/Sprites/*Outlined`) | `Content/UI/Kit/Kit*.png`: dark fill with a thin light border (panel `#222527`, row `#292E2F`, button `#1C1F1F`) |
| Selected row | pale yellow | `#1C3C3D` |
| Orange headers, amber chips | — | stay orange / amber; dark words stay dark on amber |
| Teal accents | `#2FA58A` | kit cyan `#06EFF0` |
| Main buttons (Go green) | `#1F7A66` | `#138A8C` with light words |
| Words | ink on cream | `#E8ECED` on dark; muted `#9BA4A7` |
| Drop shadows | solid ink, offset | soft black (50 %) |
| Icons drawn in ink | dark | light |

Colours set by code at run time (player colours, state colours that are not the old palette) are kept.

**V5 places:** the Log (tab and panel) and the toasts move to the **bottom left**; the banner moves under the hint
bar (the hint bar already sits under the round bar).

A builder that makes a missing prefab still makes it in the old look: run *Apply Kit Look* after it.

### Frames from the owner's Figma exports (2026-10-24)

The owner exported the kit's components into `Content/UI/Figma/Components/` (2x, each with an empty version and a
`_reference` version with words). *Apply Kit Look* now also puts the frames on (`Editor/UI/KitFrames.cs`):

| Frame (export) | Goes on |
|---|---|
| `Rules.png` (grey, corner plates with screws) | the outer panel of table cards: Rules card, Trap Floor status card, Help card, Toolbox panel |
| `Player Profile.png` (orange frame) | the outer panel of popups and menus: right-click menu, amount, card inspect, Shop, payment and reveal popups, profile, persona, card and deck editors, New table…, Save table as a game, table panel, rule proposals, hot-seat handover, the Table menu |
| `Top.png` (end-capped bar) | the round strip (turn strip) and the banner |
| `Controls.png` | the controls strip |
| `Hint.png` | the hint bar |
| `Log.png` (cyan) | the Log panel and its tab |

A framed panel loses its drop shadow, and the old orange header band and cream body turn clear (the words stay). Nested
panels inside a popup keep the plain dark kit panel. Buttons, rows and chips keep the kit shapes because the game
colours them while playing (selected, active, warnings).

The screens Claude designed in Figma are saved as reference pictures in `Content/UI/Figma/Screens/` (renders made
while designing on 2026-10-23); the owner does not need to export them.

Still from the kit, for UI-2 and UI-3: the Player Card (`Container_Player_card`, `Status`, `Red`), the Top menu bar
(`Top_Console`), menu items and game entries (`Menu`, `Menu_Selected`, `Game Entry`, `Selected Game Entry`), the
profile rows and Avatar, the icons (Undo, Redo, Shop, Table, players, arrows, edit, add) and the pointers and dots.

### UI-1c — the table screen follows the Figma V5 frame (2026-10-24)

The owner compared the game with V5 and asked to follow the Figma. `Editor/UI/V5Hud.cs` (part of *Apply Kit Look*)
now places and skins each table piece as in `Figma/Components/Reference.png`, using the exports at their drawn size
(28 px margins on the 1920 canvas):

| V5 piece | Prefab | Export | Place |
|---|---|---|---|
| Top bar: dot, game, Undo, Redo, SHOP, TABLE | SessionBar/Bar | Top, Undo, Redo, ShopIcon, TableIcon, cyan_on ellipse | top left, 87 high |
| Round bar: ROUND over the turn, Keys | TrapFloorHud/TurnStrip | Top, V5_Dot | top, 728 wide, centre +195 |
| Hint bar | HintStrip/Card | V5_HintBar (Hint without the baked badge) | under the round bar, 72 high |
| Rules card | RulesCard/Card, RulesLine | Rules, Rule_Entry, V5_Bullet, Status | left, from y 127, 461 wide |
| Player card | TrapFloorHud/StatusCard | Rules, Container_Player_card | top right, 358 wide |
| END TURN / actions | ActionRow | Button_Enabled | in the player card |
| Log | ActivityLog/Panel | Log | bottom left, 413 x 96 (grows to 300) |
| + TOOLBOX | ComponentToolbox/OpenButton | Toolbox_Button | bottom left, 196 x 55 |
| Controls | ToolBar/Bar | Controls, Frame_move, Line | bottom right, 87 high |

Where our game has more than V5 shows, it keeps it in the same style: the tool buttons sit at the end of the Controls
bar; Later / Got it stay on the hint bar as dark chips. Not in V5, so hidden: the status line ("Empty Table | Objects 0")
and the Log's tab. Still to do (UI-2): the round bar's player list (Blue, Red, Green, Yellow) and the player card's rows
(Avatar, Keys, Status, In hand, Floor), which need the game to report those values.

### UI-1d — every V5 detail (2026-10-24)

The owner asked to match everything the reference shows. Now in the game: the round bar lists the players with
coloured dots (turn player ringed) and reads "RED'S TURN - 2 MOVES"; the player card has the Title_Label header with
an orange ///, the rows Avatar, Keys, Status chip, In hand chip and Floor, and END TURN / SKIP TURN as screwed buttons
under it; the Rules card has "RULES · TRAP FLOOR ///", rows that light cyan on hover (click proposes a change), and the
footer "House rules: 1 change" with < FOLD >; the Log and + TOOLBOX use small screwed frames; the Controls bar holds
only the keys. Kept though not drawn in V5: the table tools (a small screwed bar above the Controls bar), the
Floorfall details (above the buttons while a Floorfall is on), and rule headings in the list. "In hand" shows the
number of cards (a hand is private, so card names are not shown).

## 9. UI-2 — the front end (the Milanote user journey, 2026-10-24)

The owner's Milanote board sets the flow: Main menu → Player profile → Game library → Game details → Game setup (1) →
Players (2) → Review (3) → the table (V5). Built as one prefab, `Final/UI/FrontEnd.prefab` (FrontEndView), from the
kit exports: Top_Console (top bar), Menu / Menu_Selected (menu entries and player rows), Player Profile and Avatar
(frames), Profile_entry / Selected_Profile_entry (profiles, setup rows), Game Entry / Selected Game Entry (library),
Button_Enabled / Button_disabled (CONTINUE / BACK), Frame_move (chips and pickers), Square (icon boxes, player badges).
The background picture is the owner's export `Content/UI/Figma/Components/Background.png`; until it is there the
screens sit on the kit's dark colour.

What each screen does with the game: PLAY → Player profile (click a profile to play as it; the avatar arrows change
your avatar) → CONTINUE → library (click a game) → SELECT → details → START SETUP → the New table… choices as rows
(mode, players, win mode, turns, deck) → players (seat colours; add or remove a player) → review → START GAME deals the
table. CREATE opens Card art, Card editor or Deck editor. SANDBOX starts an Empty Table. TABLE (top bar) or Esc on the
main menu goes back to the table as it is; GUIDE opens the help; EXIT leaves the game. Not wired yet: SETTINGS (not on
the bar), per-player names and pawn colours (the seats keep "Player n" and their colour), the details page's
screenshots and feature tiles (no data yet; it shows the game's picture, rules and deck).
