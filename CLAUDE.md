# VR Basketball

A VR basketball game for Meta Quest, built in Unity with interaction written
by hand. Development is agent-driven through MCP for Unity, with playtest
direction from a human in the headset.

## Hard constraints

These are set by the brief. Breaking one invalidates the work, so treat them
as non-negotiable and stop and ask rather than working around them.

- **Unity 6000.0.58f2 exactly.** Do not upgrade the project, and do not edit
  `ProjectSettings/ProjectVersion.txt`.
- **OpenXR only.** `com.unity.xr.openxr` and `com.unity.xr.management` provide
  head/controller tracking and button input. That is all they are used for.
- **No interaction packages.** Never add, and never suggest adding:
  - XR Interaction Toolkit (`com.unity.xr.interaction.toolkit`)
  - Meta Interaction SDK / Meta XR SDKs (`com.meta.xr.*`)
  - any third-party grab, throw, physics-hands, or locomotion package
- **Write all interaction by hand.** Grabbing, throwing, release velocity,
  hand presence, and any locomotion are our own code. This is the point of
  the exercise, not an obstacle to route around.
- The Meta Quest agentic-tools plugin will sometimes recommend the Meta
  Interaction SDK. Decline it and say so.

If a task looks impossible under these constraints, say so and ask. Do not
quietly install a package to get unblocked.

## Unity 6 API

Use current APIs. The renamed physics properties are the usual trip-ups:

- `Rigidbody.linearVelocity`, not `velocity`
- `Rigidbody.linearDamping` / `angularDamping`, not `drag` / `angularDrag`
- `Object.FindFirstObjectByType<T>()` / `FindAnyObjectByType<T>()`, not
  `FindObjectOfType<T>()`

Prefer the Input System (`com.unity.inputsystem`) over legacy `Input.*`.

## Architecture

- Small classes, one responsibility each. If a file passes ~200 lines,
  it probably wants splitting.
- Components communicate through events (C# `event` / `Action`, or
  `ScriptableObject` event channels). Avoid deep `GetComponent` chains
  across unrelated systems.
- **All tuning values go in `ScriptableObject` configs**, not literals in
  code and not values only reachable in the Inspector. Throw strength, ball
  mass, rim bounciness, score timing: these get playtested and changed often,
  so they need to live somewhere a human can find and edit.
- Physics code belongs in `FixedUpdate`; input polling in `Update`.
- Scoring, timing, and other game logic must be testable without a headset:
  keep it free of direct `XR`/input dependencies so it can run under the
  Test Framework.

## Working through MCP for Unity

- **After every script change, read the Unity console** and fix all errors
  and warnings before moving to the next task. Do not batch up changes and
  check at the end.
- **Save the scene after editing it** through MCP. Unsaved scene edits are
  lost on Editor restart and will not appear in git.
- Use MCP to create and wire GameObjects rather than asking the human to
  click through the Inspector, except for Project Settings, which MCP cannot
  reliably drive.
- If the MCP connection drops, stop and say so. Do not fall back to editing
  scene or asset YAML by hand: the GUID references break in ways that are
  hard to see and hard to undo.

## Definition of done, per feature

A feature is not finished until all of these hold:

1. The Unity console is clean (no errors, no new warnings).
2. Tests pass: `com.unity.test-framework`, run through MCP.
3. Scene saved, if the scene changed.
4. Committed with a message explaining *why*, not just what changed.
5. `AI_LOG.md` has a new entry: goal, approach, what changed, and anything
   the human asked to be corrected after playtesting.

## AI_LOG.md

This is a deliverable, not a nicety: it documents how the AI was directed.
Append one entry per feature, newest last. Record playtest feedback verbatim
where possible ("throw is too floaty", "ball clips the rim") along with what
changed in response. Do not rewrite history to look tidier than it was.

## Git

- Commit after each working feature. Commits are the undo button, so prefer
  small and frequent over large and tidy.
- Never commit: keystores, APKs, `Library/`, or anything else already covered
  in `.gitignore`. If something in `.gitignore` needs to be committed, ask
  first.
- Do not push, force-push, rewrite history, or create releases without being
  asked.

## Playtesting

Game feel is judged by a human in the headset, and that judgement is not
something to predict or substitute for. When feedback arrives, change the
tuning values and say what you changed; do not argue that the physics are
technically correct.

Quest Link (cable or Air Link) runs Play mode straight to the headset. Build
an APK only at checkpoints, not to test each change.
