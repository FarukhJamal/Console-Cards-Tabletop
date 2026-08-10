# Console Cards — Game Template Architecture

**Document ID:** 08_Game_Template_Architecture  
**Version:** 1.2

**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.

## 1. Purpose

A Game Template prepares a table without hardcoding a complete Game into the Platform.

It is a reusable starting configuration, not a running Match.

## 2. Template Categories

- Official Game Template.
- Modified Game Template.
- Custom Game Template.
- Empty Table Template.

## 3. Template Contents

```text
GameTemplate
- Template ID
- Schema Version
- Title
- Description
- Author/Source
- Recommended Player Range
- Rulebook Reference
- Seat Definitions
- Player Layout Definition/Reference
- Console Definitions
- Game Board Definition/Reference
- Play Area Definitions
- Object Definitions/References
- Initial Object Instances
- Container Definitions
- Initial Container Membership
- Starting Poses
- Default Policies
- Camera Bookmarks
- Default Central Focus Region
- Content Dependencies
- Initial Snapshot Metadata
```

## 4. Template Loading

Loading follows an explicit pipeline:

1. Resolve Template.
2. Validate schema and dependencies.
3. Resolve content Definitions.
4. Validate stable IDs.
5. Create Match ID.
6. Instantiate Seats and Containers.
7. Apply the Player Layout selected for the active one-to-eight Player count.
8. Instantiate the Game-specific Game Board and Play Areas.
9. Instantiate Object Instances.
10. Apply starting membership and poses.
11. Apply default Policies.
12. Generate Initial Snapshot.
13. Bind Views.
14. Frame the default central focus and required interactive areas.
15. Report completion or full failure.

Template loading must be atomic from the Match perspective.

## 5. Template Validation

Validation checks:

- Unique IDs.
- Available Definition references.
- Valid Seat range.
- Supported one-to-eight Player count and valid Player Layout reference.
- Required standard four-Player, eight-Player, and compact four-Player layout availability.
- Valid Container membership.
- No Object Instance in multiple Containers.
- Valid Play Area references.
- Valid Policy references.
- Valid starting poses.
- Supported schema version.
- Content availability.

Warnings may cover non-fatal design concerns.

## 6. Empty Table Template

The Empty Table Template provides:

- Table Surface.
- Configurable Seats.
- Hands.
- Consoles.
- Basic object library access.
- No mandatory Play Area.
- Free Policies.

It is a first-class workflow, not an error case.

## 7. Official Templates

Official templates are signed or identified as developer-maintained content.

They may be duplicated but should not be overwritten directly by user changes.

The approved Phase 1 production order is:

1. Trap Floor.
2. Super Leroy Sisters.

Each is a separate official Game Template and Game-specific Board type. Their Game content must not be embedded in universal Platform modules.

## 7.1 Universal Console and Game-Specific Game Board

The Console contract is universal and persists across Games. A Game Template may configure Console contents, Slot usage, labels, and allowed Card Definitions, but it must not replace the universal Console concept with a Game-specific Board.

The central Game Board is Game-specific Template content. It may reference one or more Boards and Play Areas, and it may choose a Player Layout compatible with the active Player count.

Template validation must treat Console configuration and Game Board configuration as separate concerns.

## 8. Modified Templates

When a Player changes an official or custom template:

- Save as a new Template ID.
- Record parent Template ID optionally.
- Preserve original template.
- Store only deltas later if beneficial; full copies are acceptable initially.

## 9. Custom Templates

Future custom creation may allow:

- Selecting objects.
- Defining decks.
- Configuring Seats.
- Creating Play Areas.
- Writing Rulebooks.
- Saving initial arrangements.
- Defining default Policies.

The foundation must support serialized custom data but does not need a complete editor in the first milestone.

## 10. Content References

Templates reference content by stable IDs, not direct scene-object references.

A content resolver maps IDs to:

- ScriptableObject Definitions.
- Prefabs.
- Textures.
- Models.
- Rulebook assets.

Future uploaded content requires validation and distribution but must not change the Runtime State model.

## 11. Rulebooks

A Rulebook is human-readable content associated with the Template.

Initial formats may include:

- Structured Markdown.
- Localized text assets.
- Images or diagrams.

Rulebook text does not execute.

## 12. Default Policies

Templates may select defaults such as:

- Free Interaction.
- Optional Snapping.
- Owner-only Hand visibility.
- Host-managed setup locks.

Players or host settings may override permitted defaults.

## 13. Match Independence

After Match creation:

- Runtime changes belong to Match State.
- Editing a Definition asset must not mutate the live Match.
- Saving the current table as a new Template is an explicit operation.
- Reset restores the Initial Snapshot, not a freshly modified source asset.

## 14. Future Automation

A future Template may reference an optional Game-specific automation module.

That module:

- Is not required for free play.
- Depends on Platform contracts.
- Must have a version.
- Must not place Game-specific code inside universal modules.

## 15. Template Versioning

Each Template includes:

- Template schema version.
- Content version.
- Optional parent Template ID/version.
- Minimum Platform version where needed.

Unsupported templates fail clearly.

## 16. Security Boundary

Future user content must not include executable code by default.

Allowed initial custom content:

- Data.
- Text.
- Images.
- Approved object definitions.
- Layout configuration.

Disallowed without a separate secure system:

- Arbitrary C#.
- Native plugins.
- Runtime assemblies.
- Untrusted shaders.
- Network code.

## 17. Initial Scope

Implement:

- Minimum local official/empty Template format required to load Trap Floor and Super Leroy Sisters without Platform code changes.
- Validation.
- Template loading.
- Player Layout data structurally capable of one to eight Players, with authored selection limited to the confirmed standard four-Player, eight-Player, and compact four-Player layouts until OD-014 resolves the remaining mappings.
- Universal Console configuration separate from Game-specific Game Board and Play Area content.
- Initial Snapshot creation.
- Reset.
- Save-as-new-template contract.

Defer:

- Workshop.
- Online sharing.
- Arbitrary uploads.
- Template marketplace.
- Executable rule scripting.
