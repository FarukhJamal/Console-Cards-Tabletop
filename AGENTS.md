# Console Cards — Codex Instructions

## 1. Authority

This file controls how Codex works in this repository.

Read the following before changing code:

1. `Docs/00_Product_Vision.md`
2. `Docs/02_Terminology.md`
3. `Docs/03_Project_Principles.md`
4. `Docs/01_Platform_Architecture.md`
5. The documents relevant to the requested task
6. `Docs/15_Non_Goals.md`
7. `Docs/16_Milestones_And_Roadmap.md`

When documents conflict, stop and report the conflict. Do not choose silently.

Codex may implement only from documents marked `Approved` or `Approved with Open Decisions`. Documents marked `Draft`, `Approval Candidate`, `Proposed`, or `Superseded` are not implementation authority.

Code blocks, type names, interfaces, field lists, and diagrams are illustrative unless explicitly labelled `Approved Contract`. Do not copy illustrative APIs into production merely because they appear in documentation.

## 1.1 Approved Technical Baseline

Before M0 work, verify:

- Fresh Unity project.
- Unity `6000.5.4f1`.
- URP.
- Windows desktop target.
- 3D orthographic top-down tabletop.
- New Input System, mouse and keyboard first.
- Git/GitHub Desktop.
- `Docs/TECHNICAL_BASELINE.md` is present.

Do not add a networking package before M6.

## 2. Scope Discipline

- Implement only the explicitly requested task.
- Do not implement future milestones.
- Do not add “helpful” systems outside scope.
- Do not create placeholder architectures for hypothetical features.
- Do not add or update packages without approval.
- Do not modify scenes, prefabs, assets, or settings unless the task requires it.
- Do not introduce Game-specific logic into Platform modules.
- Do not assume Super Leroy Sisters or Trap Floor define universal behavior.

## 3. No Hallucination Rule

When a requirement, Unity version, render pipeline, package, scene, prefab, identifier, coordinate model, or workflow is unknown:

1. Search the repository and documentation.
2. State what was found.
3. Ask for clarification if the answer is still missing.
4. Do not invent names, APIs, files, assets, or completed tests.

Never claim:

- A file exists when it was not found.
- A test passed when it was not run.
- A scene was verified when it was not opened.
- A package API behaves a certain way without checking the installed version.
- A requirement is approved when it is only inferred.

## 4. Architecture Rules

- Runtime State is authoritative; Views represent it.
- Static Definitions are separate from mutable Object Instances.
- Meaningful state changes use Commands or approved Application Use Cases.
- Technical Invariants are always enforced.
- Game Rules are Free by default unless a Policy says otherwise.
- Networking types must not enter Core Domain models.
- ScriptableObjects must not hold live Match State.
- Avoid public mutable collections.
- Avoid giant manager classes.
- Prefer composition over inheritance.
- Use interfaces at boundaries, not mechanically.
- Keep Game Templates data-driven.
- Keep Play Areas optional.

## 5. Repository Rules

Before creating a file, identify:

- Owning module.
- Assembly.
- Namespace.
- Dependency direction.
- Whether it is Definition, State, Command, View, Adapter, Policy, or Editor tooling.

Do not create duplicate systems.

Search for existing implementations before adding new ones.

Do not move or rename public files or types without approval and documentation updates.

## 6. Unity Rules

- Use the project’s exact Unity version.
- Do not upgrade packages or project settings unless requested.
- Do not use scene searches as dependency injection.
- Do not put business logic into `Update` on each object.
- Do not use prefab names or hierarchy paths as persistent IDs.
- Do not mutate ScriptableObject Definitions during play.
- Do not edit third-party package code unless explicitly approved.

## 7. Testing

For each task:

- Add or update tests required by `Docs/14_Testing_Strategy.md`.
- Run the relevant available tests.
- Report exact results.
- If tests cannot run, state why.
- Do not weaken tests to make implementation pass.
- Do not delete failing tests without approval.

## 8. Change Safety

Keep the project compiling after each logical change.

Prefer small, reviewable changes.

For risky work:

1. Audit current implementation.
2. State the plan.
3. Implement the smallest coherent change.
4. Run tests.
5. Report risks and follow-up items.

## 9. Documentation

Update authoritative documentation when implementation changes an approved contract.

Do not rewrite Product Vision to justify implementation.

Architecture changes require an Architecture Decision update.

Use terms from `Docs/02_Terminology.md`.

## 10. Implementation Report

After every implementation task, provide:

### Summary
What was implemented.

### Files Changed
Each file and why.

### Architecture Impact
Which modules/contracts were affected.

### Tests
Exact tests run and results.

### Manual Verification
What was manually checked.

### Assumptions
Only assumptions actually used.

### Risks and Limitations
Known issues, deferred cases, or migration concerns.

### Unresolved Questions
Anything requiring user or designer decision.

### Scope Confirmation
State what was deliberately not implemented.

## 11. Stop Conditions

Stop and ask before proceeding when:

- Requirements conflict.
- A package choice is required but not approved.
- A scene or asset change is necessary but outside the task.
- The requested change violates an approved Architecture Decision.
- A migration may destroy existing data.
- A dependency direction would be reversed.
- The only solution requires implementing a Non-Goal.
