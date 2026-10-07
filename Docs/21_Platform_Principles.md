# Console Cards - Platform Principles

**Document ID:** 21_Platform_Principles
**Version:** 0.1
**Status:** Binding owner rules (recorded 2026-10-07). Every stage plan must comply; a plan that needs an exception must name the rule and get the owner's approval first.
**Source:** Owner decisions 2026-10-03 to 2026-10-07 (doc 19 §2.1, doc 20 §8, stage reviews).

## Product

Console Cards is a platform of generic components and console-card-specific components, not a Tabletop Simulator clone. Players play console games as card games, or build their own. The platform becomes multiplayer and gamified.

## Components

1. **The console is one generic component.** Games never add, remove or restrict its slots.
2. **Every slot is always usable.** Cards can be placed in either orientation and stacked; loose pieces can rest anywhere on the mat.
3. **Cube positions are usable.** The 9 big squares and 54 track cells are available for interaction in games that want them.
4. **Component structure is a component property.** Accepted kinds, capacity and footprints come from the component, never from a game.
5. **Resting on surfaces.** Every component rests on the surface according to its colliders, with an optional per-component rest offset.

## Games and templates

6. **Templates only select, place and set declared parameters.** Games and templates choose components, place them and set parameters a component declares configurable. They never change how a component behaves, and contain no component code or prefab overrides. Rule hooks may refer to slots by key.
7. **Turns and phases are advisory.** The platform tracks and shows them, but never blocks an action because of them.
8. **Generic setup flow.** Every game uses the same flow: Games → Genre (e.g. Game Show) → Game → Variant → Player count (set by the game) → Rules (paginated rule cards the player can open and close) → Components → Table setup (layout preset) → Play menu → Game space.

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
