# Console Cards — Product Vision

**Document ID:** 00_Product_Vision  
**Version:** 1.7

**Status:** Approved
**Purpose:** Define what Console Cards is, what experience it must create, and which product boundaries must remain stable before architecture and implementation begin.

The layout and interaction requirements derived from `Consolecards_LayoutRef_doc.pdf` are canonically traced in `17_Layout_Design_Requirements_Matrix.md`.

---

## 1. Product Summary

Console Cards is a multiplayer virtual tabletop platform designed to recreate the experience of sitting around a physical table with friends and playing card- and board-based games.

Players share a top-down virtual table, use private hands and personal Consoles, and manipulate cards, decks, pieces, dice, tokens, boards, tiles, and other tabletop objects.

The platform provides the digital table, object interaction, organization tools, visibility, saving, and multiplayer synchronization.

The players provide, interpret, follow, challenge, and correct the rules.

> **Authoritative product principle:** Console Cards is primarily a freeform Virtual Tabletop, not a generic rules engine. It simulates people playing a physical tabletop game together: the Platform supplies the table and physical capabilities, while Players normally decide what the Game Rules require and enforce those rules socially.

Console Cards has three requirement horizons:

### Foundation Requirements

- Explicit Session Entry between Empty/Custom Table and available Game Templates.
- Official developer-authored Game Templates.
- Empty-table sessions where players arrange objects and define the rules socially.
- An in-session component toolbox whose pieces become authoritative tabletop objects.
- First-class physical/interactable Dice with authoritative results.
- Freeform tabletop interaction.
- Future-compatible Policy boundaries.

### Architecture Must Not Block

- Modified copies of official Game Templates.
- Custom Game Templates.
- User-provided card and layout content.
- Future assistance or restrictions.

### Future Product Direction

- Player-facing template creation tools.
- Uploading and sharing custom content.
- Community distribution and Workshop-like systems.

Items in the second and third groups are not automatically part of the first foundation milestone.

---

## 2. Core Experience

The intended experience is the digital equivalent of one to eight friends sitting around a physical table.

A typical session should allow players to:

1. Enter a session and explicitly choose an Empty/Custom Table or an available Game Template.
2. Join the resulting table.
3. Occupy a seat around that table.
4. Access their private hand and personal Console.
5. Place or load the cards, decks, pieces, boards, and other objects required for the game.
6. Read or explain the rules.
7. Draw, move, rotate, flip, stack, split, reveal, hide, and organize objects.
8. Play according to the agreed rules.
9. Resolve mistakes or disputes socially, as they would in person.
10. Save or reset the tabletop setup when required.

The software should make physical tabletop actions easier and clearer. It does not need to replace player judgment or determine every legal and illegal move. As at a physical table, Players may question, correct, or reverse an action when the group believes a rule was applied incorrectly.

---

## 3. Universal Console Cards Identity

Console Cards is not only a generic tabletop simulator. Its identity is built around universal Console Cards systems.

These include:

- Button Cards.
- Player hands.
- Button Card decks.
- Discard piles.
- Player Consoles.
- Move Cards or other cards stored on Consoles.
- Shared tabletop interaction.

The universal Button Cards are:

- Up
- Down
- Left
- Right
- A
- B
- X
- Y

Their meaning may change between games. The physical Button Cards remain universal.

### 3.1 Shared Visual and Physical Framework

System Cards use poker-card proportions. The universal Console is configurable, not permanently defined as six Slots: its visual framework supports a Main Slot, optional Side Slots, Cube Slots, and Dice Slots. A Game Template may use only the Slots it needs, and a Console may be horizontal or vertical.

Preserve the Slot-symbol visual language, including the approved newer Milanote revisions: teal Slot-symbol fill, Plus / Diamond / Minus bottom-slot symbols, and nested-shape combinations. Physical reference sizing is approximately 16 mm for Dice, chits, and meeples, and 8 mm for cubes. These are visual/physical authoring references, not a change to Runtime coordinates or physics authority.

ADR-026 records this framework; `17_Layout_Design_Requirements_Matrix.md` traces it separately from Game-specific content. It does not change the approved Trap Floor setup or Floormaster Deck composition.

---

## 4. Platform, Game Template, and Match

Console Cards must distinguish between three concepts.

### 4.1 Platform

The Platform provides:

- The virtual table.
- Camera controls.
- Player seats.
- Hands and Consoles.
- Generic tabletop objects.
- Object interaction.
- Placement assistance.
- Visibility and ownership.
- Saving and restoration.
- Multiplayer synchronization.
- Configurable policies.

The Platform must not depend on one specific game.

The Platform supplies the ordinary physical capabilities needed to play supported tabletop Games. These capabilities remain available without a Game-specific rule module: Players can place and move Components; draw, shuffle, flip, reorder, stack, and transfer Cards; use Decks, Stacks, Hands, discard piles, Consoles, Slots, and other Containers; move Pawns and Tokens; manipulate coins and resources; roll and reposition Dice; arrange Boards; and add supported generic Components through the toolbox.

### 4.2 Game Template

A Game Template is a reusable starting configuration.

It may contain:

- Rulebook.
- Recommended player count.
- Seat configuration.
- Play Areas.
- Cards and decks.
- Boards and tiles.
- Pawns, miniatures, tokens, and dice.
- Console layout.
- Starting object arrangement.
- Camera bookmarks.
- Default interaction and enforcement policies.

A Game Template may be:

- Official.
- Modified from an official template.
- Fully custom.
- Empty or minimal.

A template is not the same as hardcoded gameplay logic. It describes how a tabletop session starts. Loading a Game Template prepares the physical Game but does not imply that Console Cards automatically executes its rules.

### 4.3 Match

A Match is a running instance created from a Game Template or an empty table.

During a Match, players may rearrange objects, use house rules, or continue playing freely according to the available policies.

### 4.4 Session Entry

Application startup does not imply a selected Game. Before authoritative Match construction, the player explicitly chooses:

- **Empty / Custom Table**, with no mandatory Game-specific Board or Game-specific rules; or
- an available **Game Template**, beginning with Trap Floor and later including Super Leroy Sisters.

The chosen setup is then validated and used to construct authoritative Runtime State before the player enters the tabletop. Final UI styling is not defined here.

---

## 5. Freedom and Future Restrictions

The initial builds should prioritize player freedom.

Within the authored usable surface of the physical Table, Players should generally be able to:

- Move objects freely.
- Place cards outside suggested Play Areas, Boards, Zones, and guides.
- Ignore turn order.
- Rearrange decks and piles.
- Correct accidental actions manually.
- Use house rules.
- Play games not understood by the software.

This freedom includes adding or removing generic pieces, using different Dice, creating extra Decks or Stacks, and rearranging official setups for house rules. Optional official Game automation may stop recognizing or assisting a modified setup; that is acceptable. Unless an approved Policy explicitly restricts an action, loss of automation understanding must not make the generic tabletop pieces Presentation-only or prevent free manual manipulation. New loose placement requires a valid physical Table/Board surface, but objects released or thrown beyond the Table may fall naturally without snapping back. These physical capabilities are Platform behavior, not Game-rule enforcement.

However, the architecture must allow restrictions and automated enforcement to be introduced later.

The governing principle is:

> **Freedom by default, enforcement by configuration.**

Possible future enforcement levels include:

- **Free:** The platform does not block game-rule violations.
- **Assisted:** The platform provides suggestions, warnings, highlights, and guidance.
- **Restricted:** Selected actions are blocked according to configured policies.
- **Enforced:** The platform validates and controls complete game rules where such logic has been implemented.

The first foundation version should use Free mode for game rules while always enforcing technical integrity, such as preventing one object from existing in two locations at the same time.

### 5.1 Freeform and Assisted Actions

A **Freeform Action** is a Player directly manipulating a physical tabletop Component, such as dragging a Pawn, drawing or flipping a Card, moving a Card to a discard pile, rolling a Die, or moving coins. Freeform Actions use authoritative Runtime State and protect Technical Invariants, but they do not require Game-specific legality validation.

An **Assisted Action** is optional Game-specific automation that interprets, guides, or helps perform an action. For example, Trap Floor assistance may interpret its two official d6 as `(3,5)` and highlight Floor Card `(3,5)`. Assistance must not replace or disable the underlying Freeform Actions. It may decline to assist when Players change an official setup beyond what the automation understands.

---

## 6. Player Count and Seating

The Platform must support one to eight Players and must not be fixed to four Players.

The table does not grow as Player count increases. It provides eight available Seat positions around a stable central play space. Occupied Seats are arranged for the active Player count so smaller groups remain visually close to the core action rather than occupying distant edge positions.

Required reusable Seat layouts include:

- Standard four-Player layout, one Seat per side.
- Eight-Player layout, two Seats per side.
- Compact/alternate four-Player layout with Seats pulled toward the central action.

The exact Seat assignment for other Player counts and the selection rule between the standard and compact four-Player layouts remain open in `OPEN_DECISIONS.md`.

Player count may be defined by:

- The selected Game Template.
- The host during custom setup.
- The limits of the current platform version.

---

## 7. Top-Down Virtual Table

The primary presentation is a top-down shared tabletop.

Each player should have:

- An independent local camera.
- Pan controls.
- Zoom controls.
- Quick focus on their own Console.
- Quick focus on the shared Play Area.
- Optional focus on specific objects or player areas.

One player's camera movement should not move every other player's camera.

Console Cards uses one real, fixed physical Table. The Table remains fixed while each local Camera pans and zooms independently; Camera movement must not reposition or resize the Table.

The Table and Game Boards provide real physical collision surfaces. Usable surface colliders are explicitly authored independently from decorative geometry, are editable with the Table/Board in Unity, and follow its Transform and scale. Normal freeform and house-rule placement is available on valid Table/Board surfaces.

Loose Card/Pawn/Token/Die placement raycasts those physical surfaces rather than the mathematical placement plane. The existing `TableCoordinate` and `TabletopPose` model remains for authored/template/container layout; separate authoritative 3D physical pose/state represents loose objects. Boards sit on the Table and can catch objects physically. Logical Play Areas, mats, and guides do not create collision surfaces merely by defining bounds. ADR-025 supersedes the former loose-object plane/boundary model.

Player-count adaptation must not enlarge the table or reduce Card readability. Important usable and interactable areas should be within the default Game Template framing, while local Camera pan and zoom remain available.

---

## 8. Play Areas

A Play Area is an optional structured region placed on the freeform table.

A Game Template or custom setup may contain:

- No Play Area.
- One Play Area.
- Multiple Play Areas.

Possible Play Area types include:

- Freeform area.
- Rectangular grid.
- Hex grid.
- Side-scroller layout.
- Linear or curved track.
- Zone-based card field.
- Tile-built map.
- Board-based area.

A Play Area may provide:

- Visual organization.
- Object snapping.
- Grid coordinates.
- Slots.
- Zones.
- Camera framing.
- Suggested placement.
- Layering assistance.

A Play Area does not automatically enforce game rules unless a configured policy or future game-specific system explicitly does so.

For official Game Templates, the required Play Area and its layout should load automatically with the rest of the template.

For custom games, players may create or configure their own Play Areas.

### 8.1 Universal Console and Game-Specific Game Board

The Console is universal. It is the persistent personal interaction and storage system that Players learn once and reuse across Games.

The central Game Board is Game-specific. A Game Template defines the Board, Play Areas, layout, and supporting content required by that Game. The Console and Game Board are separate even when they are shown in one tabletop composition.

The core Game action remains centered. Player tools, personal resources, Seats, and controls are arranged around that center. Reference screenshots are design examples, not universal layouts that every Game Template must reproduce.

---

## 9. Tabletop Objects

The long-term platform should represent common tabletop components through reusable object types and capabilities.

Target object categories include:

- Cards.
- Decks.
- Card stacks.
- Hands.
- Discard piles.
- Consoles.
- Boards.
- Tiles.
- Pawns and meeples.
- Miniatures.
- Tokens.
- Counters.
- Dice.
- Bags and hidden containers.
- Trays and player mats.
- Notes and rule references.
- Tracks, zones, and placement guides.
- Other future randomizers or organizational objects.

The architecture should use composition and reusable capabilities rather than creating a separate hardcoded system for every possible game component.

Not every object category must be completed in the first milestone.

### 9.1 Tabletop Component Toolbox

An active tabletop session provides a Platform-owned component toolbox for adding supported generic pieces. The initial MVP categories are:

- Card.
- Deck.
- Stack or pile.
- Pawn or meeple.
- Token or counter.
- Die.

A toolbox-created component is a first-class authoritative tabletop component with stable identity and Runtime State, using Object State or Container State/placement as appropriate to the existing architecture. It is not merely a disposable Presentation GameObject. The same generic component types are available to official Game Templates, Empty/Custom Tables, and house-rule play.

---

## 10. Physical Tabletop Actions

The Platform must provide the common physical tabletop actions required by supported Games. The shared capability set includes:

- Selecting objects.
- Selecting multiple Cards with a marquee and clearly highlighting every selected Card.
- Moving objects.
- Rotating objects.
- Flipping double-sided objects.
- Drawing one or several cards.
- Drawing Cards manually from Decks.
- Moving complete decks.
- Creating and separating stacks.
- Shuffling decks.
- Reordering Deck or Stack contents where physical interaction allows.
- Dealing cards.
- Passing cards between players.
- Placing cards into private hands.
- Moving Cards between Hands, the table, Console Slots, Decks, Stacks, discard piles, and other Containers.
- Revealing or hiding cards.
- Moving pieces across boards or Play Areas.
- Rolling dice.
- Repositioning Dice after a roll.
- Moving tokens and counters.
- Manipulating coin and resource Tokens.
- Creating supported generic Components through the toolbox.
- Arranging Game-specific Boards and shared tabletop pieces.
- Grouping and organizing components.
- Previewing the surface placement or Container layout of a Card or selected Card group; a physical throw's final resting pose is determined by simulation, not guaranteed by the preview.
- Locking setup objects when needed.
- Undoing or manually correcting accidental placement.
- Resetting a Match to its initial setup.

Interactions should be responsive and clear: controlled while held, physical when released.

New loose Cards, Pawns, Tokens, and Dice, including Card batches and duplicate placement, require valid Table/Board surface hits. No valid surface means invalid/hidden preview and no creation commit. Released loose objects use gravity, collision, velocity, and torque; an off-table release is not invalid and must not snap back. Deck/Stack/Console bodies retain their existing non-physical positioning and the applicable authored Table-area validation under ADR-024.

Loose objects may use Rigidbody/collider physics. Held objects are temporarily kinematic with gravity disabled, lift clear of surfaces/objects, and follow the pointer; release restores dynamic motion and preserves throw momentum. Containers control contained Cards through their layouts with loose physics disabled; accepted extraction restores loose physical behavior. Settled 3D poses commit through actor-aware Commands/Application Use Cases to Runtime State. Future host/server physics determines accepted simulation outcomes; clients do not independently decide them.

High-stakes Card choices require a large, central, readable selection UI. Players must be able to hide it temporarily to inspect the Board, reopen it without losing the pending choice, and receive clear hover, selection, and confirmation feedback.

Hand visibility, personal Play Area visibility, and individual Card face/identity visibility are separate concerns. Hiding one must not implicitly hide or reveal the others.

### 10.1 Dice Authority

A Die is a physical, interactable Tabletop Object with stable identity, side count, current authoritative value, authored/layout Tabletop Pose, and separate loose 3D physical pose/state. Common initial toolbox options include d4, d6, d8, d10, d12, and d20, each with explicit authored face/value mappings and a result-reading convention.

Rolling follows the authoritative state-change path: a Player requests Roll, authority validates and physically throws the Die using randomized impulse/torque, then resolves the settled physical face through its authored mapping and commits the final 3D pose and value to Die State. Manual grab/throw uses the same settlement path. Values must not be inferred from mesh triangle order or names. Future host/server physics decides the result, not independent client simulations; IDs, Match State, Commands, actor context, and revisions remain authoritative.

Roll is exposed through the normal player-facing object interaction, with a Die context-menu action acceptable for the initial implementation. This does not prescribe final UI styling.

---

## 11. Custom Games and User Content

The product direction includes future support for players bringing or creating their own content. This is not an immediate foundation deliverable.

This may eventually include:

- Uploading card artwork.
- Creating card definitions.
- Building decks.
- Adding rules.
- Creating Play Areas.
- Arranging initial setups.
- Saving custom Game Templates.
- Duplicating and modifying official templates.
- Sharing templates with other players.

This is a product direction, not an immediate requirement for the first foundation milestone.

The initial architecture must avoid blocking it, but the first implementation should not attempt to build a complete Workshop, scripting language, or unrestricted content-upload platform.

---

## 12. Official Games

Official games such as Trap Floor and Super Leroy Sisters are delivered as separate Game Templates and Game-specific Board content after the shared Play Area and minimum Game Template foundations exist. Trap Floor's approved Game-specific contract is defined in `18_Trap_Floor_Game_Requirements.md`.

They are validation cases and content packages, not architectural foundations.

The Platform must not contain hardcoded checks such as:

- `if currentGame is SuperLeroySisters`
- `if currentGame is TrapFloor`

Instead, those games should be represented through combinations of:

- Game Template data.
- Play Areas.
- Cards.
- Decks.
- Pieces.
- Consoles.
- Rulebooks.
- Policies.
- Optional Game-specific assistance modules where a demonstrated convenience is valuable.

Optional assistance may highlight a target, display reference/status information, or help with a known setup or lifecycle operation. It is additive convenience, not a prerequisite for manual play and not the owner of generic physical actions.

---

## 13. What the Platform Owns

The Platform owns:

- Shared tabletop space.
- Object state.
- Object interaction.
- Containers and ordering.
- Hands and Consoles.
- Ownership and visibility.
- Seats.
- Play Area support.
- Camera and presentation.
- Saving and snapshots.
- Session synchronization.
- Configurable policies.
- Technical state integrity.

---

## 14. What the Players Own

In the first builds, players own:

- The interpretation of the rules.
- Turn order.
- Legal and illegal move decisions.
- House rules.
- Dispute resolution.
- Scoring where not automated.
- Victory declaration.
- Manual correction of mistakes.
- Custom arrangement of the tabletop.
- Applying written Card costs and effects by physically moving the required Components.
- Challenging and correcting illegal moves socially.

---

## 15. Initial Product Boundaries

The first foundation should focus on:

- Explicit Session Entry without automatic official-Game loading.
- Empty/Custom Table as a legitimate Match path.
- Top-down virtual tabletop.
- One-to-eight-Player seating model.
- Private hands.
- Player Consoles.
- Cards and decks.
- Stacks and discard piles.
- Basic boards or Play Areas.
- Pawns or meeples.
- Tokens.
- Physical Dice with authoritative settled-face values and explicit face/value mappings.
- A component toolbox for adding supported generic authoritative objects.
- Optional placement guides.
- Freeform interaction.
- Marquee multi-selection and live landing indicators.
- High-stakes Card-choice Presentation.
- Game Template loading architecture.
- Saving and reset architecture.
- Networking-ready state design.

The first foundation should not attempt to deliver:

- Full game-rule automation.
- Anti-cheat.
- Ranked competition.
- Public matchmaking.
- AI opponents.
- Workshop distribution.
- User scripting.
- Arbitrary runtime plugins.
- Physical simulation beyond the scoped loose Card/Pawn/Token/Die system of ADR-025, including physical Container bodies and physics-driven contained Card layouts.
- Production economy.
- Cosmetic store.
- Voice chat.
- Production-complete or fully automated official Games beyond the approved minimum playable Trap Floor and Super Leroy Sisters scope.

---

## 16. Product Success Criteria

The foundation is successful when a group can:

1. Explicitly choose an Empty/Custom Table or available Game Template before Match construction.
2. Enter the resulting shared top-down table.
3. Occupy configurable seats.
4. Use private hands and personal Consoles.
5. Load or arrange cards, decks, pieces, and Play Areas.
6. Add supported generic components through the toolbox as authoritative tabletop objects.
7. Manipulate objects and roll first-class Dice naturally.
8. Follow their own written or spoken rules.
9. Complete a tabletop session without the Platform needing to understand the Game.
10. Reset or restore the session reliably.
11. Load a different Game Template without changing the universal foundation.
12. Keep the universal Console stable while loading a Game-specific central Game Board and Player Layout.
13. Play the approved minimum Trap Floor and Super Leroy Sisters flows manually using their separate Game-specific Boards and readable instructions, without requiring comprehensive Game-rule automation.

---

## 17. Product Statement

> **Console Cards is a multiplayer, top-down freeform Virtual Tabletop platform centered around Button Cards and Player Consoles. It allows groups to load official Game Templates, modify them, create custom setups, or use an empty table while interpreting and enforcing their own rules. The Platform synchronizes authoritative physical tabletop state; optional assistance and configured restrictions may be added without replacing manual play.**
