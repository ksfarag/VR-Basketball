# AI work and verification log

This log records AI-assisted project changes and evidence in implementation order. For each new work item, record the request, decision, changed files, checks actually run, human feedback, and what remains unverified. Do not copy private conversation text or claim a playtest that did not happen. Developer observations belong in [HumanJournal.md](Docs/HumanJournal.md); AI-assisted personal drafts remain marked until reviewed.

## Foundation and project structure

**Request and decision.** Prepare instructions, required packages, project structure, and verification before gameplay. Keep Unity **6000.0.58f2**, use OpenXR and the Input System for tracking/input, and write grab, throw, and any movement behavior in project code. The developer chose to retain Unity AI Assistant and review changes before commits.

**Changes.** Started from the Universal 3D template (baseline `117d4ac`) rather than the VR template containing XR Interaction Toolkit. Configured Android and Standalone OpenXR, Meta Quest support, and Touch Plus controller input. Added the runtime and EditMode assemblies, a smoke test, `Assets/Scenes/Gameplay.unity`, the implementation plan, Claude project guidance, ignored build/log/signing paths, and Editor/host test, build, and deployment helpers. The scene remains a scaffold with no rig, ball, hoop, scoring, or reset.

**Checks.** Unity readback confirmed the required Editor, Android/IL2CPP/ARM64 settings, saved Gameplay scene, and no missing scripts. The smoke test passed **1/1**; PlayMode had **0 tests** and was correctly reported as incomplete. Results were written under ignored `Logs/Verification/`.

## Android build and automation corrections

**Observed issue.** The first scaffold build stopped because Active Input Handling was **Both**, which Unity rejected for Android. The build report recorded failure. The setting was changed to **Input System only**, Unity was restarted, and preflight checks were added so this state is rejected before another build.

**Verified result.** The corrected scaffold APK built successfully. The report is `Logs/Verification/quest-build.json`; the APK size and SHA-256 were checked against the fresh output, and its merged manifest was inspected for Quest launch, head-tracking, ARM64, and package-ID settings (`Logs/Verification/apk-manifest.txt`). Its 980 warnings were reviewed as existing package/toolchain diagnostics; there were zero build errors and no project C# compile error. The APK is **not a playable game**. No headset install, launch, performance, or human throwing-feel check has occurred.

**Other setup findings.** Unity MCP was connected, and its **Auto-Start Server on Editor Load** preference was enabled locally; startup after an Editor restart has not been confirmed. Unity AI Assistant remains installed but reported `NoSubscription`/connection errors; this did not block project compilation. Test/build outputs are local ignored evidence, so they must be shared separately if needed for review.

## Standalone desktop XR experiment

**Decision.** Meta XR Core 203/205 package requirements specify Unity **6000.0.66f2**, above this project's Editor. The inspected Core 201 package had no Operator. Core and Interaction SDKs stayed out of the original baseline. Official standalone Meta XR Operator and Simulator binaries were installed under ignored `.local-tools/` and connected through the existing OpenXR API-layer feature. [DesktopXR.md](Docs/DesktopXR.md) describes the local setup.

**Checks.** Simulator startup required a Windows App Runtime component; the missing x64 DDLM registration was repaired. In Unity Play mode, Operator reached `XR_SESSION_STATE_FOCUSED`, returned tracked head/controller poses, accepted controller pose and trigger input with readback, and captured the scaffold XR view. A complete automatic Play/stop cycle restored the previous desktop XR state. The project EditMode smoke test was rerun and passed **1/1** after the desktop helper compiled.

**Limit.** This verifies desktop inspection and simulated input only. The Core-based Unity AI Tools panel and Android/headset Operator workflow are absent. No gameplay or device performance is proven. The developer supplied Simulator diagnostics, requested concise documentation, and kept changes pending review.

## Documentation consolidation

**Request.** Reduce AI context cost and make review easier without losing requirements or useful evidence.

**Change.** Shortened the always-read instructions and README, reduced this log to decisions and actual checks, and moved task-specific procedures into their existing references. Removed duplicate workflow and folder-map documents. Existing gameplay and project settings were not changed in this pass.

**Check.** Verified the remaining Markdown links and searched for references to removed files.

## Meta XR Core SDK setup

**Change.** The developer installed Meta XR Core SDK **201.0.0**. Unity added XR Hands **1.7.2** and generated Quest configuration assets and changes to the Android manifest, OpenXR features, and player/render settings. The local standalone Operator remains optional; its machine-specific API-layer registration was removed from saved OpenXR settings.

**Check.** Unity loaded Core 201.0.0, the EditMode smoke test passed **1/1**, and a fresh scaffold APK built with its SHA-256 verified. The build report counted two errors from overlapping build requests; Console readback identified both as preflight errors, while the APK build itself succeeded. No headset or gameplay check has been done, and the custom interaction requirement has not changed.
