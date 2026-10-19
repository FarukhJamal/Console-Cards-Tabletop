# 29 — Player-Made Games

Version 0.3 · 2026-10-19 · Status: L2-6 written; L2-6b polish approved by the owner (2026-10-16); Deal step added (DEAL-1, 2026-10-19)

## The owner's direction (2026-10-16)

- Players make their own games from the platform's components and tools, with their own cards and decks (doc 27
  List 2).
- **How:** set up the table, then save it as a game (owner's choice).
- **Rules:** a written rules card. Players follow the rules themselves; nothing is enforced.
- Built in one stage (owner's choice).

## Making a game

1. Start an **Empty Table** (New table… > Empty Table, possibly with one of your decks, L2-5).
2. Put out what the game needs with the Toolbox: boards, Consoles, decks (game cards or your own), loose cards,
   pawns, tokens, dice.
3. Table menu > **Save table as a game…**, in three steps (L2-6b, below). In step 3 (Save):
   - **Name** (up to 40 letters), **players** (fewest and most, 1–8) and a **description**.
   - **Rules:** one rule per line; a line starting with `#` is a heading (`# How to play`). On the rules card the
     headings become sections and the other lines numbered rules.
   - The panel says what of the table will be saved.
   - **Save as a new game**, or **Save over <name>** when the table was started from that game.

## L2-6b — in steps: Deck, Cards, Save (owner, 2026-10-16)

The owner asked for options to choose or create the deck, create or pick cards, and then save. The panel has
step buttons **1 · Deck**, **2 · Cards**, **3 · Save**, with Back and Next; any step can be opened at any time.

1. **Deck.** `<` `>` choose the game's deck: **No deck**, a game's own deck ("Trap Floor deck") or one of your
   decks for any game. **New deck…** opens the deck editor with a new deck; when it closes, that deck is chosen.
   **Put on table** places the deck where it should lie when the game starts (Esc or right-click cancels; the
   panel comes back either way).
2. **Cards.** The deck's cards ("3 x Fall, 2 x Key, …"). **Create a card…** opens the card editor with a new card
   already ticked into this deck. **Pick cards…** opens the deck editor on this deck. Closing an editor comes
   back to this step. Changing a game's own deck changes it for that game too.
3. **Save.** Name, players, description, rules, Save as a new game / Save over.

**The game's deck (owner's choice: "Game's deck, on table").** The deck is saved *by reference* (which game,
which deck) with where it lay, not as a frozen list of cards. Start puts it on the table there, shuffled and face
down, with the cards the deck has then, so changing the deck later changes the game. The deck on the table is not
saved again as an ordinary pile. Saving with a deck chosen but not on the table asks to put it there first.
Choosing another deck turns the one already on the table into an ordinary deck.

**Placing keeps going (owner's choice).** A Toolbox piece, a box tile's cards, a duplicate and a duplicated
Console are placed again and again with left-click until **Esc or right-click**. The Toolbox shows "· Esc
stops". Moving a deck or board, and putting the game's deck on the table, still end after one.

Notes:

- A card made while the table is open shows its picture from the next table you start (the table's card looks
  are refreshed at Start).
- The steps are added to an existing GameSavePanel by *Create Missing Gameplay UI Prefabs*; without them the
  panel shows the Save step only, with a line saying so.

## The Deal step (DEAL-1, 2026-10-19)

The save panel's steps are now **1 · Deck**, **2 · Cards**, **3 · Deal**, **4 · Save** (a panel made with three steps
gets the Deal button from *Create Missing Gameplay UI Prefabs*).

- `<` `>` choose **No dealing**, **Deal 1 to 13 cards to each player**, or **Deal all the cards, evenly**.
- The first button switches **To: their hands** / **To: the table**; the second switches **Face: down / up** (on the
  table) or **At a time: one card / 2 / 3** (into hands).
- Saved with the game as `deal` (for example `count=5;packet=1;face=down;to=hand;when=setup`). The rules card shows it
  under **Dealing**.
- **Start** deals from the game's deck after laying the table out; when the game allows several numbers of players
  it first asks how many play. Deal on the deck's menu (or T) deals the same way again.
- An Empty Table has no players' hands, so each player's cards are laid in front of the deck, one stack per player
  (with one player and the Hand on, into your hand).

## Playing and changing a game

- **New table…** lists your games after the platform's games, as "Your game · 2–4 players".
- **Start** begins an Empty Table and lays its table out again: boards first, then Consoles, piles, the game's
  deck (shuffled), then loose pieces. The session chip shows the game's name, and the rules card shows its rules.
- **To change a game:** start it, change the table (and the rules in the save panel), then Save table as a game… >
  **Save over**.
- **Delete game** in the menu asks for a second press.

## What is saved (the table save file)

Saved in your profile, `Profiles/<profile>/my-games/<id>.json` (doc 28), through `SaveFiles`:

| Saved | How |
|---|---|
| Boards | position and grid (columns, rows, space sizes) |
| Consoles | position and which side is up |
| Decks, stacks, discard piles | position, arrival style and their cards, bottom to top, by card ID |
| Loose cards | position, card ID and face |
| Pawns, tokens, dice | position (dice with their sides) |

Not saved in this first version:

- Hands, and cards in Console slots.
- A stack or discard pile that holds cards is laid out as a deck of those cards (an empty one keeps its kind).
- Pieces a game sets up itself (template pieces).

Cards look right when they are Trap Floor's (game or yours) or a game box's own cards.

## Notes

- Laying the table out uses the table's normal actions, so each piece is one Undo step. Reset goes back to the
  empty table; start the game again from New table… to lay it out again.
- A piece that cannot be laid out (for example an unknown card) is skipped, and a message says how many.

## Code

- **Data:** `Presentation/Saving/PlayerGame.cs`:
  - `PlayerGameFile` (name, players, description, `rulesText`, `table`, `deck`).
  - `SavedGameDeck` (game ID, deck ID — `game` for a game's own deck — name, pose).
  - `SavedTable` with `SavedPiece`, `SavedPile`, `SavedConsole`, `SavedBoard` and `SavedPose`.
  - `PlayerGameStore` (list, load, save, delete).
- **Folder:** `ProfileService.MyGamesFolder`.
- **Table side:** `TabletopPrototypeComposition.PlayerGames.cs`:
  - `CaptureTable` and `LayOutPlayerGame` (with `LayOutGameDeck`).
  - The steps: `FillSaveStep`, `SaveDeckChoices`, `SaveDeckCards` (the deck editor's counting),
    `PutSaveDeckOnTable`; editors opened from a step come back through `ReturnToGameSave`.
  - The save panel.
  - The rules card model.
  - The menu entry and Delete.
- **Menu:** `TabletopPrototypeComposition.GameSelect.cs` lists the games and starts them (`OnTableReplaced`).
- **Keep placing:** `TabletopComponentPlacementController.KeepPlacing` (L2-6b), set by
  `KeepToolboxPlacing` for Toolbox tiles and duplicates.
- **Shared creation paths:** `CommitRealCardsPlacement` (face, deck of one, arrival style, quiet) and
  `AddToolboxComponentAtPose` (Console side, quiet), so a laid-out table is made exactly like a placed one.
- **UI:**
  - `GameSavePanelView` (Final UI prefab `GameSavePanel`).
  - The Table menu row "Save table as a game…" (`SessionBarView.BindSaveGame`).
  - The menu's **Delete game** button (`GameSelectPanelView`, added to an existing prefab by Create Missing
    Gameplay UI Prefabs).
