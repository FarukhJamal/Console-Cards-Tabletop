# Changelog

## [1.2.3-m1-closure]

### Added

- M1.12: Added immutable local `TabletopCameraBookmark`.
- M1.12: Added Camera bookmark capture and restoration.
- M1.12: Kept bookmark state Presentation-only, with no Seat, Play Area, persistence, UI, input binding, or networking integration.
- M1.12: Added 21 Edit Mode cases and 11 Play Mode cases.
- M1.13: Added `TabletopCameraViewBounds`.
- M1.13: Added `TabletopVisibilityEvaluator`.
- M1.13: Added orthographic view-bound calculation using Camera focus, orthographic size, aspect ratio, world scale, and culling margin.
- M1.13: Kept visibility evaluation as a local Presentation optimization aid, with no renderer toggling, object registry, pooling, security visibility, network visibility, or GameObject activation.
- M1.13: Added 95 Edit Mode cases.

### Verified

- M1 milestone closure totals: Edit Mode 381 passed, Play Mode 109 passed, Failed 0, Skipped 0, Unity compilation errors 0.
- M1 delivered direct logical-to-render coordinate conversion, an orthographic top-down Camera, keyboard and mouse pan/zoom, local Camera bookmarks, a camera-local Table Surface proxy, direct-coordinate precision characterization, and basic logical viewport visibility evaluation.
- Logical tabletop state and objects do not move during Camera panning.

## [1.2.2-m1.11]

### Changed

- Recorded M1 precision evidence for direct logical-to-render coordinate mapping.
- Accepted the MVP baseline of `1` table unit to `1` Unity world unit, with logical X mapped to Unity world X and logical Y mapped to Unity world Z.
- Documented the characterized MVP render range of +/-100,000 table units, where the measured `0.10` card gap remains within the approved `0.01` world-unit error tolerance.
- Recorded known limits outside the approved normal-use range: approximately `0.125` represented gap at 1,000,000 table units and collapse of a `0.10` separation at 2,097,152 table units.
- Rejected floating origin, render-origin rebasing, sectors, and chunks for the MVP because current evidence does not justify them.

## [1.2.1-m1.10]

### Changed

- Renamed `TabletopSurfaceFollower` to `TabletopSurfaceProxy`.
- Clarified the distinction between the effectively unbounded logical tabletop and the camera-local visual coverage proxy.
- Documented that future surface visuals must remain anchored to Tabletop Space or world-space X/Z coordinates.

## [1.2.0-approved-baseline]

### Resolved

- Fresh Unity project.
- Unity `6000.5.4f1`.
- URP.
- 3D orthographic top-down tabletop.
- Windows desktop target.
- New Input System with mouse and keyboard first.
- Git with GitHub Desktop.
- Card dimensions and configurable prototype spacing.

### Added

- `Docs/TECHNICAL_BASELINE.md`.

### Changed

- Marked the M0-relevant documentation as Approved.
- Marked architecture/networking documents as Approved with Open Decisions where later vendor choices remain.
- Marked ADRs Accepted, with networking topology still deferred.
- Updated `OPEN_DECISIONS.md` to show OD-001 through OD-008 as Resolved.
- Updated README, AGENTS, Unity structure, index, and roadmap.
- M0 is now unblocked.
- Corrected `Docs/05_Core_Data_Model.md` to explicitly defer `PlayAreaState` from M0 to M4, aligning the Core Data Model with the approved roadmap and completed M0 implementation.
- Simplified the Unity project root from the previous `_Project/ConsoleCards` location to `Assets/ConsoleCards` before M1.
- Corrected `Docs/12_Unity_Project_Structure.md` to align with the approved `Assets/ConsoleCards` root after the pre-M1 folder simplification.

### Deferred

- Networking vendor selection to M6.
- Host-migration inclusion to M6.
- Player-facing Game Template editor.
- Official Game Template production order.

## [1.1.0-approval-candidate]

### Added

- `README.md`
- `CHANGELOG.md`
- `Docs/OPEN_DECISIONS.md`
- `Docs/REQUIREMENTS_TRACEABILITY.md`
- Approval-status and illustrative-contract rules.

### Changed

- Normalized documents to `Approval Candidate`.
- Separated Foundation Requirements from future custom-content direction.
- Simplified initial coordinate model and deferred sector/floating-origin decision to M1 evidence.
- Removed vague generic object-state payload direction.
- Added explicit Card, Pawn, Token, and Container State scope.
- Added basic Tokens to M2.
- Clarified that M0 is serialization-compatible but does not implement persistence.
- Clarified non-host reconnect requirements and conditional host migration.
- Recalculated milestone hours and schedule ranges.
- Strengthened Codex no-hallucination and approval-gate instructions.
- Removed duplicate top-level document copies from ZIP structure.
- Expanded documentation index.

### Fixed

- Product-scope ambiguity around custom Game Templates.
- Timeline arithmetic mismatch.
- Host-loss expectation ambiguity.
- Source-of-truth ambiguity in ZIP packaging.

## [1.0.0-draft]

- Initial 18-document Console Cards Platform documentation pack.
