# 32 — Feature Checklist Status

Version 0.4 · 2026-10-19 · Status: checked against the build by Claude; owner to confirm

The owner's client checklist "Console Cards — Feature Checklist" (received again 2026-10-19) lists what the virtual
game must support. This doc checks each item against what the platform has today (doc 31, the code) and says what
is left and where it is planned.

Legend: **Done** — built and approved · **Partly** — some of it is built · **Not yet** — planned or new.

## Button mapping (the checklist's disclaimer)

The checklist's final word: button mapping happens on a **Controller Pad**, a small pad like the back of the
Console, shaped like a controller. An ability card (for example "Heavy Kick") is placed on one of its A/B/X/Y
slots; from then on, an X controller card played onto the field means Heavy Kick. Arrow cards are not mapped to
abilities; they have dice mapped to them (for example Right Arrow = 2d6, so playing a Right Arrow card drops a
2d6 roll).

| Part | Status | Today | What is left |
|---|---|---|---|
| A pad with A/B/X/Y slots that take ability cards | **Partly** | The back of the Console mat is the controller mapping: A, B, X, Y take cards (P-CONSOLE). | Decide: is the back of the Console enough, or is the Controller Pad a separate, controller-shaped piece? (question for the owner) |
| Arrow slots take dice | **Partly** | The D-pad arrows take dice, set to a number. | The checklist means a dice *amount* (2d6) mapped to the arrow, rolled when the arrow card is played. New: the arrow slot holds a dice set (1–3 dice, any size) and shows it as "→ 2d6". |
| Playing an X card means the mapped ability | **Not yet** (helper) | Players read the pad themselves (free play is allowed). | Optional helper: when a controller card is played to the field, a small label shows what it means ("X = Heavy Kick"); playing an arrow card rolls its mapped dice next to the player. Never blocks free play. |

## Checklist items

| # | Item | Status | Today | What is left / where planned |
|---|---|---|---|---|
| 1 | **Console slot icons** (top 3 Square, top-right Triangle, top-left Circle, bottom-right Minus, bottom-left Plus, bottom 3 Diamond) | **Done** | The Console front has exactly these, plus Star left and Cross right (P-CONSOLE). | — |
| 1 | **Cards with icons on any side or corner snap and connect to a matching slot icon, at any rotation** | **Partly** | A card with one symbol snaps magnetically to a slot with the same symbol (symbol magnet). | Cards can carry icons on each side and corner (data per card side/corner); when a card is brought near the Console, the matching icon is found on whichever side it is, the card turns so that side faces the slot and clicks into place (the edge-to-edge connections in the client's picture). Planned: **ICON-1**. |
| 2 | **Grid snapping**: a card taken out of the grid and put back snaps neatly into its space | **Done** | A loose card that comes to rest on a board space snaps to its centre, squared to the board; Trap Floor's floor is such a board (P-BOARD). | Polish: snap while still carried (ghost of where it lands), POL-2. |
| 3 | **Piece snapping**: pawns snap onto the grid cards as they move tile to tile | **Partly** | Pawns moved by the turn helper land on the tile; a pawn dropped by hand does not snap. | A pawn (or token) let go over a grid card snaps to the card's centre (or the nearest free spot when several share it). Planned: **POL-2** (piece snap). |
| 4 | **Multiplayer** (lobby, 2+ players taking turns) | **Partly** | Hot-seat turns for 2+ players on one computer; the code is ready for networking (one command door, changes announced, doc 30 A6, R1–R2). | MP-1 to MP-5 (doc 30 A6), only when the owner asks. |
| 5 | **Drawing tool** on the table, to label things | **Not yet** | — | A pen, highlighter, text label and eraser on the table, in your colour, saved with the table; part of **T5** (paint layer), moved up if the owner wants it sooner. |
| 6 | **Deck building**: Ctrl-click cards one at a time, then a button groups them into a deck | **Done** (to be checked) | TOOLS-1: Ctrl-click (or the Select tool) adds or removes any component but boards and Consoles (a card in a pile picks the pile), a drag draws a selection box, and **Group into deck** (button or G) gathers the selected cards and piles into one deck; one Undo step. Decks can also be built in the deck editor. | Moving the whole selection together: **POL-7**. |
| 7 | **Split and spread the deck** (as in Tabletop Simulator) | **Partly** | Split Stack (in half) and Split into 2–6 piles are built (P-PILES). | **Spread**: lay a deck out in a row or fan to see every card, and gather it back. Planned: **POL-4**. |
| 8 | **Camera movement** (move, look, zoom) | **Done** | Wheel zoom, WASD / arrows / middle-drag pan, views 1–4, camera follows a seat switch (P-CAMERA). | Polish if wanted (smoothing) in POL. |

## Questions for the owner

1. Controller Pad: the back of the Console (as now), or a separate controller-shaped piece?
2. Arrow dice: does each game set the dice per arrow (Right = 2d6), or do players set it on the pad?
3. Drawing tool: sooner (before the visual list) or with T5?

## Owner's answers (2026-10-19)

1. Controller Pad: **the back of the Console** (as now).
2. Arrow dice: **players can set them** if they want (the arrow slot takes a dice set, shown as "→ 2d6").
3. Drawing tool: **with T5**.

