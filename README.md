# VR Basketball

A Unity project for a small Meta Quest 3 basketball game with custom interaction code. **Gameplay is not implemented yet.** `Assets/Scenes/Gameplay.unity` has a tracked player rig, input actions, controller models, and a temporary forward target; it has no ball, hoop, or scoring.

Tracking and controller visuals use Meta XR Core's `OVRCameraRig` and `OVRControllerPrefab`, which supply head and hand poses and the animated Quest 3 controller models. Grabbing, throwing, scoring, and any movement remain project code.

## Controls

| Action | Quest Touch control |
|---|---|
| Hold ball (left hand) | Left grip |
| Hold ball (right hand) | Right grip |
| Reset | Right **B** |

The bindings are in `Assets/Input/BasketballControls.inputactions`. `GameplayInput` on the Player Rig reports them by hand. They are verified in the Simulator and on a Quest 3 over Quest Link; an installed APK has not been tested yet.

Open with **Unity 6000.0.58f2** and its Android Build Support, SDK/NDK, and OpenJDK modules. OpenXR and the Input System target desktop Play mode and Android; Meta XR Core SDK 201.0.0 is installed for Quest setup. Package versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Start with [CLAUDE.md](CLAUDE.md) for the project requirements, then read only the relevant item of the [implementation plan](Docs/ImplementationPlan.md). Use Unity MCP for Editor work. The Editor's **VR Basketball > Automation** menu runs tests and builds; [automation commands](Docs/Automation.md) explain result files and device deployment.

The setup smoke test passed, the PlayMode suite is empty, and an Android scaffold APK built successfully. Those checks do not verify basketball gameplay or a headset launch. [AI_LOG.md](AI_LOG.md) records decisions and actual setup evidence; [the human journal](Docs/HumanJournal.md) holds developer feedback.

For optional desktop testing, see [Desktop XR tools](Docs/DesktopXR.md). For a real device, use [headset checks](Docs/HeadsetChecks.md).
