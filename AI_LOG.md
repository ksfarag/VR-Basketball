# AI Log

How this project was built with AI assistance: what was asked for, what the
agent did, and what had to be corrected. Newest entry last.

---

## 2026-09-16 — Toolchain and project setup

**Goal.** Get the machine and repo ready for agent-driven Unity development:
Python and `uv` for the MCP for Unity server, a git baseline, and the XR
packages the brief allows.

**Approach.** Environment first, then repo hygiene, then packages — so that
every later change is small and revertible.

**What changed.**

- Installed `uv` 0.12.15. Python 3.14.7 was already present, so it was left
  alone. Also installed Python 3.12.14 via `uv` as a fallback, since some
  MCP dependencies can lag on the newest Python release.
- Baseline commit `117d4ac` of the Unity 6000.0.58f2 Universal 3D template
  (64 files). The VR template was deliberately not used: it ships the XR
  Interaction Toolkit, which the brief bans.
- Extended `.gitignore` beyond the upstream Unity template: Android signing
  material (`*.keystore`, `*.jks`), OS cruft, IDE state, Python venv
  artifacts, and `.claude/settings.local.json`. The keystore rules matter
  most — the repo has a public remote, and a leaked keystore cannot be
  rotated without breaking installed builds.
- Added the XR stack in `734f989`: `com.unity.xr.management` 4.4.0,
  `com.unity.xr.openxr` 1.14.3, `com.unity.probuilder` 6.0.9. Versions were
  checked against Unity's package docs rather than assumed; ProBuilder was
  first written as 6.0.4 and corrected to 6.0.9.
- Wrote `CLAUDE.md` (project constraints) and a `code-reviewer` subagent that
  checks diffs against those constraints before a commit.

**Problem worth recording.** `uv python install 3.12` failed repeatedly with
`Missing expected target directory for Python minor version link`. The cause
was not uv: directory junctions created anywhere under `AppData\Local` or
`AppData\Roaming` on this machine can be path-resolved but not enumerated,
which is the signature of a filter driver (sync client or antivirus)
intercepting those folders. Fixed by setting
`UV_PYTHON_INSTALL_DIR=C:\Users\K\.uv\python`, outside the affected tree.
Worth remembering if another tool that relies on symlinks under AppData
fails oddly.

**Corrections from the human.**

- Asked why the commit was not visible in GitHub Desktop. Investigation
  showed `C:\Workspace\VR Basketball` was not in Desktop's repository list,
  so it had never been watching the folder — the commit was fine. The repo
  was also unpushed and `origin` had no branches at all.
- Asked for less verbose explanations. Noted.
- Asked whether the packages should have been installed through the Coplay
  MCP instead. They could not have been: MCP requires a running Unity
  Editor, and none was open. Editing the manifest directly also produced a
  reviewable diff, which an MCP-driven install would not have.
- Declined removing `com.unity.ai.assistant`; it stays.

**Still manual.** Restart Unity Hub so the Editor inherits `uv` on PATH;
enable OpenXR with Meta Quest Support and the Oculus Touch controller profile
under Project Settings (MCP cannot reliably drive Project Settings); confirm
Android Build Support with OpenJDK and SDK/NDK is installed.

---

## 2026-09-16 — XR configuration and MCP connection

**Goal.** Finish setup: OpenXR configured for Quest 3, MCP for Unity
connected, and a working test harness.

**What changed.**

- OpenXR enabled as the XR provider for Android and Standalone, with Meta
  Quest Support and the Meta Quest Touch Plus controller profile. Touch Plus
  is the Quest 3 controller; the Standalone entry covers Play mode over Quest
  Link. Switching the build target to Android raised min SDK 23 -> 24 and
  added `USE_INPUT_SYSTEM_POSE_CONTROL` and `USE_STICK_CONTROL_THUMBSTICKS`,
  which route controller pose and thumbstick data through the Input System —
  the mechanism interaction will be read through, without the XR Interaction
  Toolkit.
- Added `Assets/Scripts/VRBasketball.asmdef` (runtime, references the Input
  System) and `Assets/Tests/VRBasketball.Tests.asmdef` (EditMode, references
  the runtime assembly). Gameplay logic lives in the first so tests can reach
  it without a headset, per the architecture rules.
- Added a smoke test proving the harness compiles and runs. It passes (1/1)
  and should be deleted once real scoring tests replace it.
- Installed GitHub CLI 2.101.0 for the eventual release with the APK.

**Problems worth recording.**

- `com.unity.xr.oculus` was installed by a misclick in XR Plug-in Management.
  Investigation showed it had only been installed, never made the active
  provider: `XRGeneralSettingsPerBuildTarget.asset` still referenced
  OpenXRLoader for both targets and no Oculus loader asset existed. Removing
  the manifest line was the whole fix. Worth noting because the instinct was
  to revert every outstanding change, which would have destroyed the correct
  OpenXR setup Unity had just generated.
- MCP tools would not load despite the server being healthy. The cause was
  two entries for the same project in `~/.claude.json`, one with backslashes
  holding the server config and one with forward slashes holding none; the
  client resolved to the empty one. The duplicate came from the session
  changing directories earlier.
- A first attempt to patch that config with PowerShell's
  `ConvertFrom-Json`/`ConvertTo-Json` silently dropped 2 of 11 projects,
  caught by comparing counts against a backup and immediately restored. The
  round-trip cannot preserve keys that differ only by slash direction or
  case. A targeted text replacement worked, changing exactly 7 lines.

**Corrections from the human.** Asked for commits to stop: changes are now
left in the working tree for review, and `CLAUDE.md` was updated so the rule
survives across sessions.

**Still manual.** Meta's agentic-tools plugin is not installed yet (slash
commands run in the client, not through the agent). Headset tracking is still
unverified — that needs a scene with an XR rig, which the court blockout will
provide.
