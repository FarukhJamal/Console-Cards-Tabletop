# Console Cards — Product Vision

**Document ID:** 00_Product_Vision  
**Version:** 1.0 Draft  
**Status:** Approved
**Purpose:** Define what Console Cards is, what experience it must create, and which product boundaries must remain stable before architecture and implementation begin.

---

## 1. Product Summary

Console Cards is a multiplayer virtual tabletop platform designed to recreate the experience of sitting around a physical table with friends and playing card- and board-based games.

Players share a top-down virtual table, use private hands and personal Consoles, and manipulate cards, decks, pieces, dice, tokens, boards, tiles, and other tabletop objects.

The platform provides the digital table, object interaction, organization tools, visibility, saving, and multiplayer synchronization.

The players provide and follow the rules.

Console Cards has three requirement horizons:

### Foundation Requirements

- Official developer-authored Game Templates.
- Empty-table sessions where players arrange objects and define the rules socially.
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

The intended experience is the digital equivalent of two to six friends sitting around a physical table.

A typical session should allow players to:

1. Join a shared table.
2. Occupy a seat around that table.
3. Access their private hand and personal Console.
4. Place or load the cards, decks, pieces, boards, and other objects required for the game.
5. Read or explain the rules.
6. Draw, move, rotate, flip, stack, split, reveal, hide, and organize objects.
7. Play according to the agreed rules.
8. Resolve mistakes or disputes socially, as they would in person.
9. Save or reset the tabletop setup when required.

The software should make physical tabletop actions easier and clearer. It should not initially replace player judgment.

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

A template is not the same as hardcoded gameplay logic. It describes how a tabletop session starts.

### 4.3 Match

A Match is a running instance created from a Game Template or an empty table.

During a Match, players may rearrange objects, use house rules, or continue playing freely according to the available policies.

---

## 5. Freedom and Future Restrictions

The initial builds should prioritize player freedom.

Players should generally be able to:

- Move objects freely.
- Place cards outside suggested areas.
- Ignore turn order.
- Rearrange decks and piles.
- Correct accidental actions manually.
- Use house rules.
- Play games not understood by the software.

However, the architecture must allow restrictions and automated enforcement to be introduced later.

The governing principle is:

> **Freedom by default, enforcement by configuration.**

Possible future enforcement levels include:

- **Free:** The platform does not block game-rule violations.
- **Assisted:** The platform provides suggestions, warnings, highlights, and guidance.
- **Restricted:** Selected actions are blocked according to configured policies.
- **Enforced:** The platform validates and controls complete game rules where such logic has been implemented.

The first foundation version should use Free mode for game rules while always enforcing technical integrity, such as preventing one object from existing in two locations at the same time.

---

## 6. Player Count

The platform must not be fixed to four players.

The first supported target is:

- Two to six players.

The architecture should keep seat count configurable so future templates may support different numbers where technically practical.

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

The table should appear seamless and effectively unbounded for normal use.

Players should be able to continue placing objects without reaching an obvious visual table edge or seeing multiple separate table meshes placed beside each other.

The virtual tabletop should maintain stable logical coordinates while rendering only the nearby visible surface and objects.

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

---

## 10. Physical Tabletop Actions

The platform should eventually allow common physical tabletop actions, including:

- Selecting objects.
- Moving objects.
- Rotating objects.
- Flipping double-sided objects.
- Drawing one or several cards.
- Moving complete decks.
- Creating and separating stacks.
- Shuffling decks.
- Dealing cards.
- Passing cards between players.
- Placing cards into private hands.
- Revealing or hiding cards.
- Moving pieces across boards or Play Areas.
- Rolling dice.
- Moving tokens and counters.
- Grouping and organizing components.
- Locking setup objects when needed.
- Undoing or manually correcting accidental placement.
- Resetting a Match to its initial setup.

Interactions should be responsive, predictable, and easier to manage than unrestricted physical simulation.

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

Official games such as Super Leroy Sisters and Trap Floor may later be delivered as Game Templates.

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
- Optional future game-specific modules only where true automation is later required.

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

---

## 15. Initial Product Boundaries

The first foundation should focus on:

- Top-down virtual tabletop.
- Two-to-six-player seating model.
- Private hands.
- Player Consoles.
- Cards and decks.
- Stacks and discard piles.
- Basic boards or Play Areas.
- Pawns or meeples.
- Tokens.
- Optional placement guides.
- Freeform interaction.
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
- Advanced physical simulation.
- Production economy.
- Cosmetic store.
- Voice chat.
- Complete official game implementations.

---

## 16. Product Success Criteria

The foundation is successful when a group can:

1. Enter a shared top-down table.
2. Occupy configurable seats.
3. Use private hands and personal Consoles.
4. Load or arrange cards, decks, pieces, and Play Areas.
5. Manipulate objects naturally.
6. Follow their own written or spoken rules.
7. Complete a tabletop session without the platform needing to understand the game.
8. Reset or restore the session reliably.
9. Load a different Game Template without changing the universal foundation.

---

## 17. Product Statement

> **Console Cards is a multiplayer, top-down virtual tabletop platform centered around Button Cards and player Consoles. It allows groups to load official Game Templates, modify them, create custom setups, or use an empty table while following their own rules. The first builds prioritize physical-table freedom, while the architecture preserves the option to introduce assistance and restrictions later through configuration.**
