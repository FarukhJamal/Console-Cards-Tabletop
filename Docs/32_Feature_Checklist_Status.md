# 32 — Feature Checklist Status

Version 0.8 · 2026-10-20 · Status: checked against the build by Claude; owner to confirm

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
| A pad with A/B/X/Y slots that take ability cards | **Done** | The back of the Console mat is the controller mapping: A, B, X, Y take cards (P-CONSOLE). The owner confirmed it is enough (2026-10-20). | — |
| Arrow slots take dice | **Done** (to be checked) | The D-pad arrows take real dice set to a number; PAD-1: up to 3 dice per arrow (for example 2d6), the first on it and the next beside it. The dice stay; players change them (owner, 2026-10-20). | — |
| Playing an X card means the mapped ability | **Done** (to be checked) | PAD-1: a controller card put into a Console slot shows what it means: "X = Heavy Kick" (the card on that pad button) or "Right = 2d6 (4 + 3)" (the dice on that arrow). It never blocks play. | — |

## Checklist items

| # | Item | Status | Today | What is left / where planned |
|---|---|---|---|---|
| 1 | **Console slot icons** (top 3 Square, top-right Triangle, top-left Circle, bottom-right Minus, bottom-left Plus, bottom 3 Diamond) | **Done** | The Console front has exactly these, plus Star left and Cross right (P-CONSOLE). | — |
| 1 | **Cards with icons on any side or corner snap and connect to a matching slot icon, at any rotation** | **Done** (to be checked) | ICON-1: in the card editor each card gets up to 8 icons (4 sides, 4 corners), drawn small on its front. Brought near the Console, the card is pulled into the nearest slot whose symbol is one of its icons and turns so that icon points outward (PL-ICONS). | Cards connecting to other cards is not included (owner's choice: Console slots only). |
| 2 | **Grid snapping**: a card taken out of the grid and put back snaps neatly into its space | **Done** | A loose card that comes to rest on a board space snaps to its centre, squared to the board; Trap Floor's floor is such a board (P-BOARD). | Polish: snap while still carried (ghost of where it lands), POL-2. |
| 3 | **Piece snapping**: pawns snap onto the grid cards as they move tile to tile | **Done** (to be checked) | Pawns moved by the turn helper land on the tile; a pawn or token let go on a board space or a loose card glides onto its centre (POL-2), unless another piece already stands there. | — |
| 4 | **Multiplayer** (lobby, 2+ players taking turns) | **Partly** | Hot-seat turns for 2+ players on one computer; the code is ready for networking (one command door, changes announced, doc 30 A6, R1–R2). | MP-1 to MP-5 (doc 30 A6), only when the owner asks. |
| 5 | **Drawing tool** on the table, to label things | **Done** (to be checked) | DRAW-1: the Draw tool's panel: pen, highlighter, label, eraser (whole line), 8 colours starting with your seat's colour, Clear all; lines on the table, mats and boards; each one an Undo step; saved with "Save table as a game" (doc 31 P-DRAW). | — |
| 6 | **Deck building**: Ctrl-click cards one at a time, then a button groups them into a deck | **Done** (to be checked) | TOOLS-1: Ctrl-click (or the Select tool) adds or removes any component but boards and Consoles (a card in a pile picks the pile), a drag draws a selection box, and **Group into deck** (button or G) gathers the selected cards and piles into one deck; one Undo step. Decks can also be built in the deck editor. Dragging one selected piece moves the others with it (POL-7). | — |
| 7 | **Split and spread the deck** (as in Tabletop Simulator) | **Done** (to be checked) | Split Stack, Split into 2–6 piles, and **Spread** (POL-4: the cards laid out in rows beside the pile); gather them back with a Select box and Group into deck. | — |
| 8 | **Camera movement** (move, look, zoom) | **Done** | Wheel zoom, WASD / arrows / middle-drag pan, views 1–4, camera follows a seat switch (P-CAMERA). | Polish if wanted (smoothing) in POL. |

## Questions for the owner

1. Controller Pad: the back of the Console (as now), or a separate controller-shaped piece?
2. Arrow dice: does each game set the dice per arrow (Right = 2d6), or do players set it on the pad?
3. Drawing tool: sooner (before the visual list) or with T5?

## Owner's answers (2026-10-19)

1. Controller Pad: **the back of the Console** (as now).
2. Arrow dice: **players can set them** if they want (the arrow slot takes a dice set, shown as "→ 2d6").
3. Drawing tool: **with T5**. Changed 2026-10-20: **now**, before the visual list (DRAW-1).

## PAD-1 (written 2026-10-20, to be checked)

Owner's choices: real dice on the arrow (up to 3); the dice stay there and players change them; a label for
A/B/X/Y and arrow cards; a card counts as played when it goes into a Console slot. Code:
`TabletopComposition.Consoles.cs` (dice places and the label), `TabletopComposition.Frame.cs` (calls it).
