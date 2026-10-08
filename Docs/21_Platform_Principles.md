# Console Cards - Platform Principles

**Document ID:** 21_Platform_Principles
**Version:** 0.3
**Status:** Binding owner rules (recorded 2026-10-07). Every stage plan must comply; a plan that needs an exception must name the rule and get the owner's approval first.
**Source:** Owner decisions 2026-10-03 to 2026-10-08 (doc 19 §2.1, §12.1 and §12.2, doc 20 §8 and §12.1, doc 22, stage reviews).

## Product

Console Cards is a platform of generic components and console-card-specific components, not a Tabletop Simulator clone. Players play console games as card games, or build their own. The platform becomes multiplayer and gamified.

## Components

1. **The console is one generic component.** Games never add, remove or restrict its slots.
2. **Every slot is always usable.** Cards can be placed in either orientation and stacked; loose pieces can rest anywhere on the mat.
3. **Cube positions are usable.** The 9 big squares and 54 track cells are available for interaction in games that want them.
4. **Component structure is a component property.** Accepted kinds, capacity and footprints come from the component, never from a game.
5. **Resting on surfaces.** Every component rests on the surface according to its colliders, with an optional per-component rest offset.

## Games and templates

6. **Templates only select, place and set declared parameters.** Games and templates choose components, place them and set parameters a component declares configurable. They never change how a component behaves, and contain no component code or prefab overrides. Rule hooks may refer to slots by key. Example: a Deck or Stack declares its pile style (bay mark from the system set, maximum pile height); a template may set it (doc 19 §12.1).
7. **Turns and phases are advisory.** The platform tracks and shows them, but never blocks an action because of them.
8. **Generic setup flow.** Every game uses the same flow: Genre (e.g. Game Show) → Game (sets the player count) → Difficulty, if the game has any (a named rules preset) → Rules (the game's defaults or the player's own) → Layout (the game's default or the player's own, checked against the pieces the rules produce) → Start. The chosen combination becomes the match's game template. In game, a rules card at the side and the hints can each be turned on and off (doc 20 §12.1).

## Hand

9. **The hand is optional per game and has no fixed size.**
10. **Hand cards sit in a straight row facing their owner.** Players can draw any number of cards at once.
11. **Hand limits.** A game's own hand limit is an advisory game rule; the comfort cap is the local player's setting.

## Ownership and privacy

12. **Owner moves.** The hand and the controller deck are movable by their owner, through the command layer.
13. **Private by default.** Console slots, hand and decks are private to their owner. Owner-controlled sharing (grant and revoke for teammates or friends, held in authoritative state) comes later.

## Camera

14. **Undo and Redo never move the camera.**

## Persistence

15. **Per-player save and load comes later.** It will build on the in-memory snapshots, with a versioned save format.

## Design language

16. **One design language.** Components follow the platform's design language. Games choose from what it offers (for example the bay marks) and never restyle it or add their own visual vocabulary.
17. **Every pile is plate-less.** Every Deck, Stack and pile is a pile of cards resting on its surface; there are no plates. A fixed pile spot (declared by the template) shows a bay; a free pile shows one only while empty; Console slots are their own bays. Drop feedback tints the bay or slot. Visual hints for new players can be turned off (doc 19 §12.1). A pile kind behaves the same whether a template or the Toolbox places it, and a bay never overlaps anything in a layout (doc 19 §12.2).
18. **Components are registered in box catalogs.** Every component and its definitions are registered in a catalog for its product box (Base box, Controller box, a Game box) or the environment, with a stable ID, its 3D prefab, its UI face and its linked definitions; never wired one by one. One component library gathers the catalogs: Base box, Controller box, one Game box per game, environment (doc 22).
