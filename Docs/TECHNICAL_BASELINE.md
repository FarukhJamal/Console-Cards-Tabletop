# Console Cards — Technical Baseline

**Version:** 1.0  
**Status:** Approved  
**Purpose:** Define the exact implementation baseline for project creation and M0–M2 work.

## 1. Project Starting Point

- Fresh Unity project.
- No existing gameplay repository or legacy architecture.
- Create from the Unity Hub Universal 3D/URP project template available with the approved Editor version.
- Do not import prototype packages or third-party frameworks during project creation.

## 2. Unity Editor

- **Exact version:** Unity `6000.5.4f1`
- The repository must record this exact version in `ProjectSettings/ProjectVersion.txt`.
- Do not open, upgrade, or resave the project with another Unity version without approval.
- Do not manually upgrade template packages during project creation.

## 3. Rendering

- **Render pipeline:** Universal Render Pipeline (URP).
- **Presentation:** 3D tabletop scene.
- **Camera:** Orthographic, top-down.
- A slight visual angle may be evaluated later, but the approved foundation is orthographic top-down.
- Cards, Pawns, Tokens, Boards, and pieces are 3D objects with controlled deterministic movement.
- Unrestricted physics is not authoritative.
- The orthographic Tabletop Scene and Camera are M1 deliverables. The default Unity starter scene is not required to match the final presentation before M0 begins.
## 4. Initial Platform

- **Target:** Windows desktop.
- Other desktop, mobile, console, and WebGL targets are outside the initial foundation unless a later milestone adds them.

## 5. Input

- **Package/system:** Unity New Input System.
- **Initial devices:** Mouse and keyboard.
- Controller and touch support remain future adapters.
- Input code must produce device-independent Interaction Intents.

## 6. Source Control

- **Version control:** Git.
- **Primary client:** GitHub Desktop.
- Use a Unity-appropriate `.gitignore`.
- Commit `.meta` files.
- Keep Unity Version Control/Plastic-specific setup disabled unless separately approved.
- First commit should contain the fresh Unity project and approved documentation baseline only.

## 7. World and Card Dimensions

Approved prototype convention:

- Standard Card width: `1.0` Unity unit.
- Standard Card height: `1.4` Unity units.
- Standard Card thickness: approximately `0.02` Unity units.
- Allowed default spacing range: `0.08–0.12` Unity units.
- Initial prototype spacing value: `0.10` Unity units.
- Spacing must be configurable rather than duplicated as magic values.

The ratio is a project convention, not a requirement that all future cards share one size. Definitions may later support approved alternative dimensions.

## 8. Coordinate Baseline

- M0 begins with minimal logical two-dimensional Table Coordinates independent of Unity transforms.
- The M1 Presentation baseline uses direct logical-to-render mapping.
- `1` table unit maps to `1` Unity world unit.
- Logical X maps to Unity world X.
- Logical Y maps to Unity world Z.
- The characterized MVP render range is +/-100,000 table units.
- Within that range, the measured `0.10` card gap remains within the approved `0.01` world-unit error tolerance.
- Sectoring, chunking, floating-origin rebasing, and render-origin rebasing are not implemented in the Foundation because M1 evidence does not justify them.

## 9. Networking

- No networking package is selected for M0–M5.
- Do not add Photon Fusion, NGO, Mirror, Relay, Lobby, or authentication packages during M0.
- Networking abstractions may be defined only where required by an approved milestone.
- Vendor selection occurs in M6.

## 10. Foundation Scene Direction

The first Unity scenes should remain minimal:

- Bootstrap/technical startup scene only when required.
- Tabletop prototype scene for M1 onward.
- No official Game Template content hardcoded into universal scenes.
- No Lobby scene before the multiplayer milestone requires it.

## 11. Package Rule

Use the package versions created or recommended by the Unity `6000.5.4f1` URP template.

Before adding any package:

1. Confirm it is not already installed.
2. Record exact version.
3. State why it is required.
4. Obtain approval when outside the current milestone.

## 12. Approval Effect

This document resolves implementation blockers OD-001 through OD-008.

M0 may begin after the project version, URP, Input System, Windows profile, source control, and approved documentation are verified. Scene and Camera presentation requirements are verified during M1.
