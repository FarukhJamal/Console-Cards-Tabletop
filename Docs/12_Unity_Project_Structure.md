# Console Cards — Unity Project Structure

**Document ID:** 12_Unity_Project_Structure  
**Version:** 1.0 Draft  
**Status:** Approved

> **Contract note:** Code blocks, type names, interfaces, field lists, and diagrams in this document are illustrative unless explicitly labelled **Approved Contract**. Codex must not treat illustrative examples as fixed public APIs.


## 1. Approved Technical Baseline

- Fresh Unity project.
- Unity `6000.5.4f1`.
- URP.
- Windows desktop first.
- 3D tabletop with orthographic top-down Camera.
- Unity New Input System.
- Mouse and keyboard first.
- Git repository managed primarily through GitHub Desktop.

See `TECHNICAL_BASELINE.md`.

## 2. Root Structure

```text
Assets/
└── ConsoleCards/
    ├── Runtime/
    ├── Content/
    ├── Presentation/
    ├── Infrastructure/
    ├── Bootstrap/
    ├── Editor/
    └── Tests/
```

Do not scatter project code across `Assets/Scripts`, package demo folders, or scene-specific folders.

## 3. Runtime Structure

```text
Runtime/
├── Core/
│   ├── Domain/
│   ├── Identifiers/
│   ├── Coordinates/
│   ├── Results/
│   └── Events/
├── Application/
│   ├── Commands/
│   ├── UseCases/
│   ├── Transactions/
│   └── Policies/
├── TabletopObjects/
│   ├── Cards/
│   ├── Collections/
│   ├── Pieces/
│   ├── Boards/
│   └── Capabilities/
├── Interaction/
├── PlayAreas/
├── HandsAndConsoles/
├── GameTemplates/
├── Persistence/
│   └── Abstractions/
└── Networking/
    └── Abstractions/
```

## 4. Presentation Structure

```text
Presentation/
├── Views/
│   ├── Cards/
│   ├── Collections/
│   ├── Pieces/
│   ├── PlayAreas/
│   └── Consoles/
├── Input/
├── Camera/
├── UI/
├── Animation/
└── Feedback/
```

## 5. Infrastructure Structure

```text
Infrastructure/
├── Persistence/
├── Networking/
│   ├── Photon/   (only after decision)
│   ├── NGO/      (only after decision)
│   └── Mirror/   (only after decision)
├── Authentication/
├── Logging/
└── Content/
```

Only the selected networking adapter should be added to production assemblies.

## 6. Content Structure

```text
Content/
├── Definitions/
│   ├── Cards/
│   ├── Objects/
│   ├── Consoles/
│   └── PlayAreas/
├── GameTemplates/
├── Rulebooks/
├── Prefabs/
├── Materials/
├── Textures/
├── Models/
└── Audio/
```

Runtime State must not be stored in Content assets.

## 7. Bootstrap Structure

```text
Bootstrap/
├── AppBootstrapper.cs
├── CompositionRoot.cs
├── SceneFlow/
└── Configuration/
```

Bootstrap constructs dependencies and starts the application.

## 8. Editor Structure

```text
Editor/
├── DefinitionEditors/
├── TemplateValidation/
├── SetupTools/
└── Diagnostics/
```

Editor code must live in editor-only assemblies.

## 9. Tests

```text
Tests/
├── EditMode/
│   ├── Core/
│   ├── Application/
│   ├── Templates/
│   └── Persistence/
├── PlayMode/
│   ├── Interaction/
│   ├── Presentation/
│   └── SceneIntegration/
└── Multiplayer/
```

## 10. Assembly Definitions

Initial assemblies:

```text
ConsoleCards.Core
ConsoleCards.Application
ConsoleCards.TabletopObjects
ConsoleCards.Interaction
ConsoleCards.PlayAreas
ConsoleCards.HandsAndConsoles
ConsoleCards.GameTemplates
ConsoleCards.Persistence.Abstractions
ConsoleCards.Networking.Abstractions
ConsoleCards.Presentation
ConsoleCards.Infrastructure
ConsoleCards.Bootstrap
ConsoleCards.Editor
ConsoleCards.Tests.EditMode
ConsoleCards.Tests.PlayMode
```

Later:

```text
ConsoleCards.Networking.Fusion
or
ConsoleCards.Networking.NGO
or
ConsoleCards.Networking.Mirror
```

## 11. Assembly Dependency Rules

- `Core` references no project assembly.
- `Application` references `Core`.
- Platform modules reference `Core` and approved Application contracts.
- `Presentation` references Platform/Application contracts.
- `Infrastructure` references abstractions and vendor packages.
- `Bootstrap` references concrete outer assemblies.
- Tests reference only what they test.
- Core must never reference Presentation or Infrastructure.

## 12. Namespace Rules

Namespaces mirror modules, not every folder.

Examples:

```csharp
ConsoleCards.Core
ConsoleCards.Application.Commands
ConsoleCards.TabletopObjects.Cards
ConsoleCards.PlayAreas
ConsoleCards.Presentation.Views
```

Avoid namespace churn from minor folder rearrangement.

## 13. Scene Responsibilities

### Bootstrap Scene

- Composition startup.
- Persistent technical services.
- Scene flow.
- No Game Template content.

### Lobby/Session Scene

- Session creation/joining.
- Player membership.
- Seat selection if required.
- Template selection.

May be deferred until multiplayer milestone.

### Tabletop Scene

- Table Surface.
- Local Camera.
- Universal presentation roots.
- Interaction surfaces.
- No hardcoded official Game layout.

### Content

Game Template content is instantiated from data or loaded additively. It is not permanently authored into the universal Tabletop Scene.

## 14. Prefab Rules

- Prefabs are Views and presentation assets.
- Prefab names are not persistent IDs.
- Prefabs do not own authoritative Match State.
- Dependencies are serialized only when local and presentation-specific.
- Runtime services are injected/bound.

## 15. Package Rules

Codex must not add or update Unity packages without explicit approval.

Record:

- Package name.
- Version.
- Purpose.
- Alternatives.
- Impact.

## 16. Generated and Third-Party Content

Use clear folders:

```text
Assets/ThirdParty/
Assets/Generated/
```

Do not edit third-party package code unless explicitly approved.

## 17. File Placement Rule

Before creating a file, identify:

- Owning module.
- Assembly.
- Dependency direction.
- Whether it is Definition, State, View, Adapter, or Editor tooling.

If ownership is unclear, stop and ask.
