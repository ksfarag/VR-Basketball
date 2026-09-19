# VR Basketball

A Unity project for a small Meta Quest basketball game with custom interaction code. **Gameplay is not implemented yet.** The saved `Assets/Scenes/Gameplay.unity` is a setup scaffold, not a playable court.

Open with **Unity 6000.0.58f2** and its Android Build Support, SDK/NDK, and OpenJDK modules. The existing OpenXR/Input System configuration targets desktop Play mode and Android. Project packages and versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Start with [CLAUDE.md](CLAUDE.md) for the project requirements, then read only the relevant item of the [implementation plan](Docs/ImplementationPlan.md). Use Unity MCP for Editor work. The Editor's **VR Basketball > Automation** menu runs tests and builds; [automation commands](Docs/Automation.md) explain result files and device deployment.

The setup smoke test passed, the PlayMode suite is empty, and an Android scaffold APK built successfully. Those checks do not verify basketball gameplay or a headset launch. [AI_LOG.md](AI_LOG.md) records decisions and actual setup evidence; [the human journal](Docs/HumanJournal.md) holds developer feedback.

For optional desktop testing, see [Desktop XR tools](Docs/DesktopXR.md). For a real device, use [headset checks](Docs/HeadsetChecks.md).
