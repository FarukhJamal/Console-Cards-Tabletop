# Changelog

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
