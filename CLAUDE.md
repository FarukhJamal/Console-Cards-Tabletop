# CLAUDE.md — Console-Cards-Tabletop

> Put this file in the repo root (`D:/Projects/Console-Cards-Tabletop/CLAUDE.md`).
> If the repo already has an `AGENTS.md` written for Codex, keep it and add this line near the top so both are read:
> `@AGENTS.md`

## Project
Unity 6 tabletop game (Rigidbody API uses `linearVelocity` / `linearDamping` / `angularDamping`).
Windows, project root `D:/Projects/Console-Cards-Tabletop`. Code lives under `Assets/ConsoleCards/`.
All work up to now was written by another AI agent (Codex). Treat the existing architecture as intentional: **do not restructure it**. Fix and extend it.

## Goal of the current work
Make physical tabletop objects (Card, Die, Pawn, Token) feel like **Tabletop Simulator**:
- picking up lifts the object; it follows the cursor with slight lag and leans into its motion
- held objects collide with and shove other pieces (they are not teleported)
- release keeps the real velocity the body had; a quick flick throws it
- dice tumble and bounce on a flick, then settle and report their face
- the existing authority / Undo / container rules keep working unchanged

Root cause found in review: `PhysicalLooseObject` makes the held body **kinematic** and writes `body.position` + `transform` every frame, so it behaves like a UI drag. Release spin is effectively zero, caps are low, gravity/materials are not tuned.

## Architecture (verified by reading the code)
- `Presentation/Input/TabletopInputFrameCoordinator` reads input each frame and drives `TabletopObjectInputAdapter.ApplyInputFrame(...)`. The adapter's own `Update()` is a fallback and is inactive when the coordinator drives it.
- `TabletopInteractionRouter.TryBegin` picks a route: contained Card → `ContainedCardDragCoordinator`; everything else → `TabletopMoveInteractionCoordinator`.
- Both coordinators call `view.PhysicalObject.PreparePointerAnchor` on press, `Follow(screenPos)` while dragging (after `TabletopInteractionStateMachine` drag threshold), and `Release()` / `ReleaseState()` on release.
- `PhysicalLooseObject` (per object, MonoBehaviour) is the Rigidbody adapter. It talks to `LocalPhysicalObjectAuthority`, which commits physical state through `CommitPhysicalObjectUseCase` into `MatchState` (revision, Undo records).
- Physical commit record modes: Begin hold = `Held`+`Intermediate`; release and 0.25 s checkpoints = `Dynamic`+`Intermediate`; final sleep = `Sleeping`/`SleepingUnresolved`+`Transaction`. `TabletopPrototypeComposition.HandleAuthoritativeActionAccepted` (~line 1370) turns `Transaction` into exactly one Undo entry; `Intermediate` records nothing.
- `PhysicalInteractionConfig` / `PhysicalObjectInteractionProfile` (per kind: Card, Die, Pawn, Token) hold the tuning. Fields are private `[SerializeField]` with public getters. The composition holds `physicalInteraction` as a serialized field, so **scene/inspector values override the code defaults**.
- `PhysicalReleaseMotion` (internal, in `PhysicalInteractionConfig.cs`) samples linear release velocity (16-sample ring buffer, window + timeout) and deliberate-rotation angular velocity.
- `LocalPhysicalObjectAuthority.SetContainedCollisions` uses `Physics.IgnoreCollision` so contained pieces do not collide with loose pieces. A held contained card calls it with `false`, but still ignores other **contained** physical objects.
- `TabletopPrototypeComposition.cs` is ~11.6k lines. Never open it whole; use grep and line ranges. Key anchors: `Start()` ~2054, `Update()` ~2093 (`physicalAuthority?.Tick()`), `BuildToolboxRuntime()` ~7291 (creates `LocalPhysicalObjectAuthority` ~7297), Undo handling ~1370, physical registration ~8477.

## Hard rules
1. Keep authority/Undo contracts: all physical state changes go through `Commit(...)` with the right `AuthoritativeActionRecordMode`. No direct `MatchState` mutation from presentation.
2. A held physical object is **dynamic** (`isKinematic=false`, `useGravity=false`, `detectCollisions=true`). Kinematic only when user-locked or frozen after out-of-bounds recovery. Never reintroduce teleporting (`body.position`/`transform` writes every frame) to "fix" a symptom.
3. `Follow()` runs in `Update` and only computes the target; movement happens by velocity in `FixedUpdate`. Release velocity must come from samples of the real body.
4. Do not weaken collisions or remove the Undo/lock/container logic.
5. Per-frame code must not allocate (use the existing NonAlloc buffers).
6. Preserve each file's existing line endings (files have mixed CRLF/LF). Make minimal diffs; never reformat or re-save whole files. Check `git diff --stat` before finishing.
7. Do not edit generated files, `.meta` files, or scene/prefab YAML by hand unless the task says so. If a scene value must change, edit only the exact serialized lines and tell me which.
8. If something conflicts with these rules or you are unsure, stop and ask. Do not guess.
9. No test-runner dependency. I verify behavior myself in the Editor/Play mode. Do not add new automated tests,
do not make the game depend on test code, and do not edit or delete existing tests unless they block compilation
(then tell me first). At the end of every stage, give me a short numbered manual verification checklist
(what to click, what I should see, what would indicate a problem).
10. Frozen pieces are solid like locked pieces and must never be stack/deck merge targets (enforce this in the stacking stage).

## Known issues (do not fix unless asked)
- `MissingReferenceException` on Play-mode stop in `PrototypeRuntimeUiController.ReleaseModalViews` (`GameObject.SetActive` on an already-destroyed object; reached via `TabletopPrototypeComposition.OnDestroy` -> `Shutdown` -> `CloseContextMenu`).
- `MissingReferenceException` on Play-mode stop in `PrototypeFixedContainerVisual.ClearFeedback` (`Renderer.sharedMaterial` on an already-destroyed `MeshRenderer`; reached via `TabletopPrototypeComposition.Shutdown` -> `ClearFeedback`).
- The camera pivot height (`tabletopHeight` 14.3) is stale vs the active table top (11.6); at the default view the far and near table edges are not framed, so pieces recovered outside those sides are off screen until the player zooms out or pans. Needs a separate camera-framing stage.
- Grabbing a piece hovering outside the table drops it about 2 units quickly when its centre crosses the edge (polish).

## Files already prepared (drop-in, written against the real code)
- `PhysicalLooseObject.cs` → replaces `Assets/ConsoleCards/Presentation/Interaction/PhysicalLooseObject.cs`
- `TabletopPhysicsSettings.cs` → new file in `Assets/ConsoleCards/Presentation/Interaction/`
They have not been compiled in Unity. Compile first, fix mismatches, keep behavior.

## Build / test
- Compile check: ask me to focus the Unity Editor and paste console errors, or run Unity in batch mode if a Unity path is configured: `Unity.exe -batchmode -quit -projectPath . -logFile -` (and `-runTests -testPlatform EditMode` for EditMode tests).
- You cannot judge game "feel" from code. After each stage, tell me exactly what to test in Play mode; I report back and we tune.

## Definition of done for the TTS-feel work
- Quick card drag trails the cursor slightly, leans, never snaps.
- Dragging a card into a pawn/die pushes it.
- Slow drop: lands where released, no spin.
- Die flick: flies, bounces, tumbles ~1–2 s, reports a face. Different flick speeds travel visibly different distances.
- Cocked dice get nudged and resolve.
- Held object stays under the cursor across height changes.
- Scroll / Q-E rotation works while holding.
- Extracting a card from a hand/deck/console and dropping it on the table creates **one** Undo entry.
- No per-frame GC from new code. EditMode tests added for the points that can be tested.
