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
