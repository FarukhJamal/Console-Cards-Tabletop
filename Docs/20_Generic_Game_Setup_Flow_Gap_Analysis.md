# Console Cards - Generic Game Setup Flow: Gap Analysis

**Document ID:** 20_Generic_Game_Setup_Flow_Gap_Analysis
**Version:** 0.1
**Status:** Read-only analysis and stage proposal (2026-10-05). Nothing here is approved for implementation. It stays behind the console, hand and interaction work.
**Source:** Code and asset survey of `Assets/ConsoleCards` (Runtime, Presentation, Content) plus Docs 08, 16, 17 and OPEN_DECISIONS.
**Purpose:** Measure the current code against a generic, game-agnostic setup flow and propose the smallest stages that get there, marking what the second game (Super Leroy Sisters) needs before it can start.

## 1. Principle and target flow

**Principle:** a Game Template only **selects** components, **places** them, and sets **parameters a component declares configurable**. It never changes how a component behaves and contains no component code or prefab overrides.

**Target flow (every game and genre):**
1. Menu Games
2. Genre (e.g. Game Show)
3. Game (e.g. Trap Floor)
4. Variant (difficulty preset)
5. Player count (min/max set by the game)
6. Rules (paginated rule cards the player can open and close)
7. Components ("what's in the box", with counts)
8. Table setup (a layout preset)
9. Play menu (the game's action buttons)
10. Game space (grid, track, row or arena: Trap Floor floors, Super Leroy levels)

## 2. What exists today

"TPC" is `Presentation/Prototype/TabletopPrototypeComposition.cs` (about 11,700 lines; 959 Trap Floor references). `Runtime/GameTemplates` has no Trap Floor references.

| Step | Status | Generic or Trap Floor | Evidence |
|---|---|---|---|
| Menu / session entry | **Partial.** No start screen; the app always opens on an Empty Table (`TPC.Start` L2078). A toolbar "Game Templates" panel lists templates as name buttons. | Bootstrap and panel views are generic (`TabletopSessionBootstrap`, `GameTemplateCatalog`, `PrototypeGameTemplatesPanelView`). Filling the catalog is Trap Floor only. | One field `[SerializeField] GameDefinition trapFloorGameDefinition` (TPC L116); `RegisterFreshTrapFloorTemplates` (L3315-3340) |
| Genre | **Missing** | — | No genre or category on `GameDefinition` |
| Game selection | **Partial.** No game list; only Trap Floor can be registered. | Trap Floor only | As above |
| Variant / difficulty | **Partial.** Each mode becomes its own template ("Trap Floor — Easy / Hard / Impossible"); no separate variant step. | Schema is Trap Floor (keys, collapse, Team/Survival) | `ModeDefinition`; factory L154; mode assets Easy (1 key, 2 abilities, RoundBased, Team), Hard (3, 0, RoundBased, Team), Impossible (3, 0, RealTime, Survival) |
| Player count | **Missing.** Fixed at 4. `minimumPlayers 2` / `maximumPlayers 4` only checked to include 4. Other seats are hot-seat placeholders. | Trap Floor constant; validation is generic but demands an exact count | `PrototypePlayerCount = 4` (factory L21); `RequiredPlayerCount` exact match (`GameTemplateValidation` L72, L130, L159) |
| Rules display | **Missing.** `manualRules` flows into `GameTemplate.Description`, which Presentation never reads. `PrototypeInteractionGuide` is a single-page controls help, not rules. | — | GameDefinition L58; factory L155 |
| Components overview | **Missing** | — | Only HUD deck/discard counts (TPC L3495) and single-card inspect |
| Table setup | **Missing as a step.** `StandardFourPlayer` is hard-coded; consoles and hands are projected onto radii 6.1 / 4.15. | Layout model generic; choice and projection Trap Floor | Factory L80, L26-27, L750-773 |
| Play / action menu | **Exists, code only.** `BuildTrapFloorTurnActions` (L3574-3664) and `BuildTrapFloorAssistedActions` (L3517-3572); guidance strings L3666-3735, L3881. | The row list binding is generic (`BindActions(IReadOnlyList<PrototypePopupActionOption>)`); the HUD view (`PrototypeTrapFloorHudView`) and every action are Trap Floor | No action data in `GameDefinition` |
| Game space | **Partial: grid only.** `GameDefinition.playAreaDefinition` is a `GridDefinition`; one play area, no kind, no rotation. The board surface is sized from the play-area bounds (mostly generic). | Grid generic; board build, floor-card scale and Floorfall dice Trap Floor | GridDefinition; factory L470-508, L137-148; TPC L7728-7797 |

**Hard blockers in Presentation for any second game:**
- `InitializeActiveSession` sends every non-empty session to `InitializeTrapFloorSession`. A template without Trap Floor wiring throws (TPC L450, L4021).
- `BuildContainerViews` / `BindContainerViews` (L8156-8266) iterate `trapFloorTemplate.Players` (controller deck "P{n} CTRL", "ACTIONS" stack), not the generic `GameTemplate.Containers`.
- One `centralPlayAreaId` (L243, L7954); the board surface is forced to rotation 0 and must be a `BoxCollider` (L7736-7747).
- Trap Floor runtime services are built and called directly (`BuildTrapFloorRevealRuntime` L7237-7291 and others); there is no game-neutral module interface.

**Principle violations today (template-driven presentation overrides):**
- Floor cards scaled by `floorCardVisualScale` when `trapFloorTemplate.IsFloorCard` (L8973-8976).
- Coins scaled by `TrapFloorCoinVisualScale = 0.34`.
- Trap Floor tints (`TrapFloorContentColor`) and container labels set in TPC.
- To fix these, components must declare these as configurable parameters (scale, tint, label) and the template only sets values.

**Doc/code mismatch:** `08_Game_Template_Architecture.md` records M4.1 as completed with "Minimum local official Template format required to load Trap Floor and Super Leroy Sisters without Platform code changes". The Runtime template format can express a second game, but no generic builder exists and Presentation rejects any non-Trap-Floor template.

## 3. Data versus code

| Layer | Kind | Content | Built by |
|---|---|---|---|
| `Runtime/Definitions` ScriptableObjects | Authored data | `GameDefinition` (stableId, displayName, manualRules, min/max players, playAreaDefinition (grid), contentSets, modes, defaultModeStableId, avatars, consoleConfiguration, inputVocabulary, controllerConfiguration, controllerMappingKind, presentationReference, assistanceConfiguration), `ModeDefinition`, `GridDefinition`, `ConsoleConfiguration`, `CardDefinition`, `AvatarDefinition`, `InputCostDefinition` | Authors in Unity |
| `Runtime/GameTemplates/Definitions` `*Data` | Plain immutable copies | Same fields, validated in constructors (throw `ArgumentException`) | `ToData()` |
| `TrapFloorTemplateFactory` | **Code** | Turns `GameDefinitionData` into `GameTemplate` + catalog + `TrapFloorTemplateDefinition` | Static C#, the only producer of `GameTemplate` |
| `GameTemplate` | Generic data (schema version 1) | Seats, containers (kind, owner, visibility, capacity, pose), objects, memberships, play areas, camera bookmarks, player layout ID, required player count | Factory |
| `GameTemplateValidation`, `GameTemplateMatchFactory`, `GameTemplateInitialSnapshot` | Generic code | Structural validation (string issue codes), match construction, in-memory reset/undo snapshot | — |
| `Runtime/Games/TrapFloor/*` | Game code (Unity-free) | Round, turn, reveal, abilities, Floorfall, collapse, objective, snapshot | Called directly by TPC |

**Hard-coded in `TrapFloorTemplateFactory`:**

| Line | Item | Value |
|---|---|---|
| 21 | Player count | 4 |
| 22-24 | Content set IDs | `trap-floor-floor-pool`, `trap-floor-abilities`, `trap-floor-controller-inputs` |
| 26-29 | Console radius, hand radius, deck offset, actions-area offset | 6.1, 4.15, 3.2, −4.45 |
| 30-31 | Starting ability staging | −3.2, 1.15 |
| 32-34 | Floorfall dice | (3.45, 3.45), spacing 0.9 |
| 35, 746 | Camera size and reference dimensions | 7.35, 4.32 × 6 |
| 80 | Layout | `StandardFourPlayer` |
| 106-112 | Pawn starting corners | 4 grid corners |

Also hard-coded:
- **Seat loadout:** every seat gets the same set: hand, Main slot with `Avatars[0]`, N side slots, actions stack, and a 48-card controller deck (8 inputs × 6, unshuffled).
- **Starting abilities:** the first `StartingAbilityCount` entries, loose. `AvatarDefinition.startingAbilities` and `stats` are never read.
- **Floor:** a shuffled pool laid row-major, face-down, user-locked.
- **IDs:** categories 1, 2, 20, 30, 40-47, 60 (doc 19 §6).

## 4. Gap analysis against the proposed template format

| Item | Status | Evidence and gap |
|---|---|---|
| a. Variants as named parameter sets | **Partial** | Modes exist but with a fixed Trap Floor schema; `modeMetadata` / `objectiveConfiguration` are free text; `collapseSchedule` is only displayed. Needs: components and game modules **declare** parameters (key, type, range, default); a variant is a named set of overrides. |
| b. Per-player-count setups | **Missing** | Single `RequiredPlayerCount`, exact-match validation, factory fixed at 4; no 2- or 3-player layouts (OD-014). |
| c. Per-seat starting loadout (optional hand / controller deck) | **Missing in data, partial in code** | `GameTemplate` can express any per-seat containers, but no authored loadout type exists; the hand is mandatory (`HandIdEmpty`) and the controller deck is always created. Stage (a2c) makes the hand optional. |
| d. Actions with availability | **Missing** | Actions are C# lists in TPC. Ability and trap effects are chosen by `effectMetadata` strings; search costs are parsed from `assistanceConfiguration`. |
| e. Status track and win/lose definition | **Missing (generic)** | `FinalRoundNumber = 10` constant; win (keys + SecretExit) coded in `TrapFloorObjectiveUseCase`; statuses (Blind/Slow/Sticky) are C# classes; no lose definition in data. |
| f. Content packs | **Partial** | `GameContentSet` + `CardDefinition.quantity`; game-local only, looked up by fixed ID strings, not toggled per variant, no "draw a subset" rule. |
| g. Schema version and validation | **Partial** | `GameTemplate.CurrentSchemaVersion = 1` with `SchemaUnsupported`; no version on `GameDefinition` or the `*Data` classes; no migration; validation codes are strings (about 70); definition-level checks throw instead of reporting codes. |

## 5. Table setup presets

The layout model (`PlayerLayoutDefinition`, `PlayerSeatLayoutEntry`) accepts any 1-8 seats with any pose and facing, but layouts exist only as C# presets (`StandardFourPlayer`, `CompactFourPlayer`, `EightPlayer`). There are no geometric checks, and no teams, sides, seat groups or play-area kinds.

| Preset | Expressible today? | Blocker |
|---|---|---|
| Face-off (2 opposite) | New C# preset | Trap Floor 4-player constants (factory L21, L80, L106-114, L225) |
| Four sides | **Yes** (only one that works end to end) | — |
| Same side co-op | New C# preset, then broken | `ProjectToRadius` bends a straight row onto the 6.1 circle |
| Adjacent corner | New C# preset | 4-player constants |
| Round (N evenly around) | New C# preset, N ≤ 8 | No generator; `MaximumSeatCount = 8` |
| Team sides | Seats: C# preset (`EightPlayer` is already 2 per side). Teams: **blocked** | No team, side or seat-group field anywhere |
| Solo with side area | 1 seat: C# preset. Side area: **blocked** | Play areas have no kind or rotation; Presentation tracks one `centralPlayAreaId`; the camera takes one board bounds |
| Arena first | Board size: data. Seats at edges: **blocked** | Fixed radii 6.1 / 4.15 override authored distances; fixed camera reference size; fixed dice position |
| Track centre | **Blocked** | No track kind; board rotation forced to 0; the 6.1 console circle overlaps a long strip |
| Sandbox | **Exists as the Empty Table session** (no seats); not as a template preset | Templates need ≥ 1 seat, a local seat and a central play area; the Board camera preset fails without one |

## 6. Proposed stages (smallest first)

**SLS** marks prerequisites for Super Leroy Sisters to start at all. Independently, G2 is blocked by **OD-019** (minimum playable rules undefined: setup, card generation, obstacles, completion, failure); no stage below removes that.

| Stage | Contents | Size | SLS |
|---|---|---|---|
| GS1 Game catalog and genre | A catalog asset listing `GameDefinition`s with a `genre` field; replaces the single `trapFloorGameDefinition` field; menu steps Genre → Game on the existing generic panel views | Small | **Yes** (a second game must be registrable) |
| GS2 Rules and box contents | `GameDefinition` gains rule pages (ordered title/body list); a paginated rules panel that opens and closes; a components view generated from content sets and loadouts | Small-medium | Needed for G2 exit (the Rulebook), not to start |
| GS3 Presentation decoupling | Template sessions no longer require Trap Floor wiring; container and object views are built from `GameTemplate.Containers` / `Objects`; Trap Floor services become an optional game module attached by module ID; presentation overrides (floor-card scale, coin scale, tints, labels) become declared component parameters | Medium-large | **Yes** (the hard blocker) |
| GS4 Setup steps | Variant, player count and table setup as separate menu steps; the template is built at Start from (game, variant, player count, layout) instead of one template per mode | Medium | Partial (player counts depend on OD-019) |
| GS5 Generic setup format and builder | Authored setup data: per-seat loadout (optional hand and controller deck), shared components, placement via layout anchors and `ConsoleAdjacentPlacement` (doc 19 / a2b), per-player-count setups, content pack selection; a generic builder produces `GameTemplate`; Trap Floor's floor shuffle stays a module setup step | Large | **Yes**, unless SLS gets its own C# factory as a stopgap |
| GS6 Table layouts and play areas as data | Layout asset type; seat groups (teams, sides); a Round generator; remove `ProjectToRadius`; several play areas with kind (grid, track/row, zone, arena) and rotation; camera framing over all play areas | Large | **Yes** (the side-scroller needs a row/track play area) |
| GS7 Data-declared actions and generic HUD | The game module declares actions with availability; a generic HUD lists them and shows declared status tracks | Medium | No (G2 automation is optional) |
| GS8 Parameters, variants and schema | Declared parameters with ranges; variants as named overrides; schema version on `GameDefinition`, migration hook, definition errors reported as issue codes | Medium | No |
| GS9 Tracks, win/lose and content packs | Status track and win/lose definitions in data; cross-game content packs toggled per variant | Medium | No |

**Minimum path for SLS to start:** OD-019 resolved, then GS1 → GS3 → GS5 (or an SLS factory) → GS6 (row play area). GS2 is needed before G2 can exit.

## 7. Open items

- Correct the M4.1 statement in doc 08 once the stages are approved, or mark it partial.
- OD-014: 2- and 3-player layout mappings.
- OD-019: Super Leroy Sisters minimum playable rules.
- Asset names that no longer match their contents (e.g. `AbilitySecondChance.asset` holds Shield; `FloorEntry.asset` has category `SecretExit`).
- `TrapFloorActionHelpText` still says "Easy/Hard is not selected here".
- Camera framing uses unprojected seat anchors (TPC L7534-7546) while consoles use radius-projected poses.

## 8. Addendum principles (2026-10-05)

1. **Games never add, remove or restrict component slots.** Every console slot is always usable, as on a real tabletop.
   - A template may only give slots starting content; rule hooks may refer to slots by key.
   - Slot keys in a template exist for starting content and rule hooks only. There is no notion of "active slots" in the template format or in Stage (a) (doc 19 amended).
   - Component structure (accepted kinds, capacity) stays a component property. Loose pieces can always rest anywhere on the mat.
2. **Turns and phases are advisory.**
   - The platform tracks the current phase and active player as shared state, shows them, notifies players, and offers Next Phase / Pass Turn.
   - It never blocks any action by any player at any time.
   - A template may describe an optional turn structure:
     - order mode: fixed, free-form or simultaneous;
     - named phases with optional reminder text;
     - who may advance.
   - Changes go through the command layer (undoable, multiplayer-ready).
   - A game with no turns works unchanged.

## 9. Inventory: where actions are blocked today

**Summary:**
- No Runtime code blocks a generic physical action for a game rule. Every generic use case (Move, Transfer, Draw, Flip, Rotate, Shuffle, Reorder, Split, Merge, MoveContainer, Roll, Create, Duplicate, Delete, Populate) checks structure only.
- `Runtime/Application`, `Runtime/Core` and `Presentation/Interaction` never reference Trap Floor turn or phase state.
- Turn and phase exist only as Trap Floor state (`TrapFloorTurnState`, `TrapFloorRoundState`), documented as "never restricts freeform tabletop actions".

### 9.1 Component structure (keep; not game rules)

- **Revision and identity:**
  - errors: `RevisionConflict`, `MatchIdMismatch`, `ObjectNotFound` / `ObjectMissing`, `ObjectNotCard` / `ObjectNotToken`;
  - locks and placement: `ObjectUserLocked`, `TargetTablePoseMissing`, `PhysicalSurfaceRequired`;
  - same-location errors: `SameLocation` / `SameContainer` / `SameStack`.
- **Capacity and membership:** `DestinationCapacityExceeded`, `DestinationAlreadyContainsObject`, `SourceContainerMismatch`, `SourceMembershipMissing`, `ContainerTransferError.*`.
- **Container kind:**
  - `SourceContainerNotDeck`, `ContainerNotDeck`, `SourceContainerNotStack`, `ContainerNotMovable`;
  - `ConsoleNotEmpty` / `ContainerNotEmpty` / `TemplateComponentProtected` on delete;
  - `SourceMustBeLoose` / `SourceKindUnsupported` on duplicate.
- **Session membership:** `ActorNotActive` / `ActorNotParticipating` (the requester is not in the session; not turn order).
- **Seat and personal-container ownership:**
  - `ActorDoesNotOwnSeat` and `SourceDeckOwnedByAnotherSeat` in `ControllerInputHandService`;
  - `ActionAreaOwnedByAnotherSeat` in the purchase service.
- **Interaction locks:** held by another interaction, `LocalLockConflict`, preview not transferable.
- **Hidden content:** unrevealed floors and deck/stack interiors stay hidden.
- **UI overlay:** open popups block tabletop input through the generic pointer-over-UI rule, and dismissing them cancels the flow.
- **To add in Stage (d):** accepted kinds in Core. Today `TransferTokenUseCase` accepts a token into a ConsoleSlot (no kind check), and `ConsoleSlotView` then throws `KeyNotFoundException` on layout (§10 item 14).

### 9.2 Game rules that are enforced (affect a generic action) — Presentation only

| Location (TPC) | What is blocked | Workaround today | Change under principle 2 |
|---|---|---|---|
| `IsAssistedControllerDeck` L6485-6506 → `AddControllerDrawAction` L6040 | The deck menu's "Draw..." appears only for the **active player's** controller deck, only in `PlayerTurn`, not after a failed round | Drag a card out of the deck (not turn-gated); `DrawCardsUseCase` has no turn check | Always offer generic Draw on every deck; keep "Draw To Hand Limit" as the assist |
| Floor card menu, `ShowFloorCardContextMenu` L4859-4921 | No Flip entry on floor cards (reveal is meant to go through Search); right-clicking an unrevealed floor with an armed Search runs the Search | Keyboard Flip has no Trap Floor check (and counts as a reveal) | Offer Flip in the floor-card menu; Search stays an assist |
| Undo/Redo during a pending Floor Collapse (L1406-1408, `TrapFloorFloorCollapse.cs` L339, L386) | Undo and Redo are disabled until the collapse resolves | None (clears on resolve or cancel) | **Decision:** allow Undo to cancel the pending collapse, or keep it as a short transaction lock |

### 9.3 Game rules that gate an assist only (generic action stays available)

| Area | Identifiers | Status under principle 2 |
|---|---|---|
| Draw to hand limit | `TrapFloorTurnTracking.DrawForCurrentPlayer` (`ActorDoesNotOwnSeat` reused as a turn error), `DrawUpToDisabled`, `CarryOverRequiresHandResolution` | Assist. Turn gate should become a notice ("not your turn"), not a refusal |
| Skip / End Turn | `ActorIsNotActivePlayer`, `ActorIsNotFloorOperator`, `CurrentFloorFailed` | Becomes the generic Pass Turn / Next Phase with "who may advance" as data (advisory) |
| Purchase | `CardNotPurchasable`, `UnsupportedPaymentConfiguration`, `PhysicalPaymentInvalid`, `CannotAfford`, `InsufficientInput`, `CostMissing`; "Buy Ability" only on the active player's slots in `PlayerTurn` | Assist; payment and cost checks are the assist's own logic. The turn gate becomes advisory |
| Search / reveal | `ActorIsNotActivePlayer`, `FloorCollapsed`, `FloorAlreadyRevealed`, `PaymentCard*`, `PaymentDoesNotSatisfyCost` | Assist (a generic Flip still reveals). Turn gate becomes advisory |
| Ability on insert | `ActorIsNotActivePlayer`, `AbilityAlreadyUsed`, `NoUnresolvedTrap`, `NoPendingTrapConsequence`, `PawnFloorUnavailable`, `NoValid*Destination`, `UnsupportedAbility`; a slot drop is **never** rejected or undone, only messaged (TPC L9553-9628) | Assist. Needs scoping (§10 items 1-3) |
| Trap, blind direction | `ResolveSupportedTrap` / `ResolveBlindDirection`: `ActorIsNotActivePlayer`, `NoActiveBlindStatus`, `DieMustBeD4`, `DieNotSettled` | Assist |
| Objective | `FloorIsNotKey`, `KeyAlreadyClaimed`, `GameAlreadyWon`, `FloorIsNotExit`, `RequiredKeysMissing`, `PlayerAlreadyEscaped`, `FloorNotRevealed` (no turn check) | Assist |
| Collapse, round, Floormaster | `CollapseAlreadyPending`, `DiceNotSettled`, `BoardExhausted`, `PhaseMismatch`, `FloorfallRequired`, `PendingCardBlocksSearch`, and others | Assist; `PhaseMismatch` becomes advisory phase state |
| HUD and menus | Search / Purchase / Roll Blind / Collapse buttons disabled by pending state or phase; `BeginPhysicalFloorfall` and `BeginPhysicalFloorCollapse` refuse outside their phase; `GetAssistedTrapFloorPlayerSetup` throws outside `PlayerTurn` (assisted paths only) | Assist. Phase gating becomes advisory (show the phase, warn, allow) |
| Statuses | Blind / Slow / Sticky | Guidance text only; nothing enforced. Already advisory |
| Eliminated players | No input lock ("Freeform tabletop actions remain available") | Already advisory |

## 10. Cards or cubes on slots the game does not use

| # | Scenario | Result | Severity |
|---|---|---|---|
| 1 | Active player drops a Disarm / Shield / Check ability into **any** slot (own Main, another player's console, a Toolbox console) | The ability activates. `Activate` does not check that the slot is the owner's Side slot, or who owns the card (`TrapFloorAbilityResolution.cs` L922-1067) | Wrong state (assist mis-trigger) |
| 2 | Dodge / Rush dropped into the own Main slot | Activates (only the console owner is checked) | Wrong state (assist) |
| 3 | Another player's ability card inserted by the active player | Activates for the active player | Wrong state (assist) |
| 4 | Card dropped on the Main slot while the Avatar is there | Rejected: `DestinationCapacityExceeded` (capacity 1) | Fine (structure) |
| 5 | Avatar removed or Main left empty | Nothing depends on it | Fine |
| 6 | Controller, Avatar, Toolbox or another player's card dropped into a Side slot | Accepted; nothing activates (no `consoleBehavior`); category rules (`allowedCategories`) are never read anywhere | Fine |
| 7 | Payment cards placed in Main, another console or a Toolbox console | Not counted as payment; Confirm rechecks Side slots | Fine |
| 8 | Floor card moved anywhere | Impossible (floor cards are user-locked from setup) | Fine |
| 9 | Pending Floormaster card dragged into a slot | `CompleteResolvedCard` fails with `OfficialContentStateInvalid` until it is dragged back out | Wrong state (recoverable) |
| 10 | Toolbox console | Shows 6 of the prefab's 9 slot views (the rest are hidden by `SelectConsoleSlots`); no card can be orphaned, because state holds exactly 6 slot containers | Fine today; removed by Stage (a) (every console exposes all 9 card slots; cube cells become containers in (d)) |
| 11 | Index assumptions | Only `ConsoleView.Bind` checks the view order against `SlotContainerIds`; nothing reads the Avatar from the Main slot | Fine |
| 12 | Loose pieces resting on slot boxes | Treated as ordinary loose pieces; drops aimed there still resolve to the slot | Fine (physics changes in Stage (c)) |
| 13 | Token into a ConsoleSlot through the UI | Not possible (tokens only target token-container drop targets) | Fine |
| 14 | Token into a ConsoleSlot through the domain | Accepted, then `ConsoleSlotView` throws `KeyNotFoundException` and takes down the console layout | Throws (unreachable from UI); closed by Core accepted kinds in Stage (d) |
| 15 | Console-interaction handler exceptions | Caught and logged, never rethrown | Fine |

**Conclusion:** nothing breaks the platform when a card is put on an unused slot. The only defects are assist hooks that react too broadly (1-3, 9) and the token path (14).

**Fix direction under principle 1:**
- Assist hooks refer to slots **by key**. For example, "ability on insert" listens only to the inserting player's own side-slot keys and the player's own ability cards.
- An unmatched insert is simply a physical move, with no message.
- The pending Floormaster card stays an assist concern: the hook ignores it while contained, instead of blocking the move.

## 11. Effect on the stages

- **GS-T, advisory turn structure (new):**
  - Generic shared Runtime state: order mode, phases, active player, who may advance, reminder text.
  - Commands Next Phase and Pass Turn (Transaction, undoable) plus notifications.
  - Template data describes the optional structure; Trap Floor's turn and round states move onto it.
  - All turn and phase gates in §9.2-9.3 become notices.
  - Not a prerequisite for Super Leroy Sisters (a game with no turns works unchanged), but it should precede GS7 so data-declared actions are advisory from the start.
- **GS3 (decoupling):** includes the two §9.2 menu fixes (generic Draw on every deck, Flip on floor cards).
- **GS5 (setup format):** contains no slot selection and no capacity overrides. It has starting content by slot key and rule hooks by slot key.
- **Stage (a) (doc 19, done):** every console exposes all 9 card slots. **Stage (d):** cube cells become containers; Core accepted kinds closes §10 item 14.
- **Trap Floor assist scoping (§10 items 1-3, 9):** belongs with GS3 or GS-T; not before the console, hand and interaction work.

## 12. Second-template pipeline as its own stage (2026-10-07)

The game setup pipeline removes the Trap Floor-only wiring so a second template can load: GS1 catalog, GS3 decoupling, GS5 generic setup format and builder, and GS6 where a game needs it. It is its own stage, scheduled after (b) in the order of doc 19 §9.1. The GS contents above are unchanged; GS2, GS4, GS7–GS9 and GS-T stay unscheduled. Binding rules: doc 21.
