# VR Basketball — project instructions

Use the agreed item in [Docs/ImplementationPlan.md](Docs/ImplementationPlan.md). Read only the references needed for that item; do not load the entire history log at session start.

## Requirements

- Keep **Unity 6000.0.58f2** and the existing OpenXR, XR Plug-in Management, and Input System stack. Retain Unity AI Assistant.
- Implement grabbing, holding, release/throw physics, and any movement in project code. Do not add XR Interaction Toolkit, Meta Interaction SDK, or samples/frameworks that provide these behaviors. Judge other packages by what they do and whether they support this Editor.
- Keep scoring and other game rules independent of headset input. Start with one stationary player, ball, and hoop; add scope only when requested.
- Meta XR Core 201.0.0 is installed. Core 205 declares Unity 6000.0.66f2 as its minimum, above this project's Editor. Do not change package metadata to evade compatibility checks. The optional standalone desktop Operator setup is described in [Docs/DesktopXR.md](Docs/DesktopXR.md).

## Working loop

1. Inspect the current project and preserve unrelated edits. Use Unity MCP for scene/prefab work; save assets through Unity rather than editing serialized YAML or guessing GUIDs.
2. Make one small, reviewable change. Let Unity compile, inspect new console errors, and fix issues relevant to the change.
3. Run meaningful checks. The Editor's **VR Basketball > Automation** menus work while the project is open; command-line Unity verification requires the Editor closed. See [Docs/Automation.md](Docs/Automation.md). An empty test suite or old APK is not a pass.
4. Add one concise [AI_LOG.md](AI_LOG.md) entry per meaningful work item: decision, change, actual checks, limitations, and human feedback. Update it as evidence arrives. Do not invent prompts, results, or playtests. Keep private context out of the repository. Put personal observations in [Docs/HumanJournal.md](Docs/HumanJournal.md), marking AI-assisted drafts until reviewed.
5. Leave changes uncommitted unless the human explicitly authorizes Git actions for that change. Never commit builds, recordings, signing material, credentials, or local tool settings.

For real-device testing, use [Docs/HeadsetChecks.md](Docs/HeadsetChecks.md). Simulator results are desktop evidence only; headset launch, performance, and throwing feel require actual device checks.
