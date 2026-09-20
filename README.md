# VR Basketball

A Unity project for a small Meta Quest 3 basketball game with custom interaction code. **Gameplay is partly implemented.** `Assets/Scenes/Gameplay.unity` has a tracked player rig, input actions, controller models, a ball either hand can pick up and throw, a temporary floor, and a temporary forward target; it has no court, hoop, or scoring.

Tracking and controller visuals use Meta XR Core's `OVRCameraRig` and `OVRControllerPrefab`, which supply head and hand poses and the animated Quest 3 controller models. Grabbing, throwing, scoring, and any movement remain project code.

## Controls

| Action | Quest Touch control |
|---|---|
| Hold ball (left hand) | Left grip |
| Hold ball (right hand) | Right grip |
| Reset | Right **B** |
| Hold with both hands | Both grips, while one hand has the ball |
| Call the ball back | Both grips, with empty hands |

The bindings are in `Assets/Input/BasketballControls.inputactions`. `GameplayInput` on the Player Rig reports them by hand. The same asset also holds `Tracked Left` and `Tracked Right`, bound to each controller's `isTracked`; these are not player controls, they let a hand drop its ball when that controller stops being tracked, and they set `initialStateCheck` so a hand that starts up already tracked is recognised as such. The hold and reset bindings are verified in the Simulator and on a Quest 3 over Quest Link; an installed APK has not been tested yet.

## Grabbing and throwing

A `HandGrabber` under each hand anchor picks up the nearest eligible `Ball` when its hold action begins, carries it by Rigidbody velocity so it still collides while held, and throws it on release using the hand motion measured at the moment the player let go. Exactly one hand can own a ball. Tuning lives in `Assets/Config/GrabSettings.asset` — reach, smoothing window and sample bound, throw scales, and speed and spin caps. None of this uses `OVRGrabber`, `OVRGrabbable`, or any other pre-made grab behaviour; the runtime assembly does not reference Meta's `Oculus.VR` assembly at all.

A second hand can take hold of a ball the other is already holding. It becomes a *support* rather than a second owner, so exactly one hand ever owns the ball: the ball eases to the point between the hands, a throw is measured from that point, letting go with the owning hand hands the ball over instead of dropping it, and the ball is only thrown once both hands let go. Turn `allowTwoHandedHold` off in the settings asset for the old single-hand behaviour.

`BallRecall` on the Player Rig calls a loose ball back when both grips are squeezed with empty hands. It homes to the point between the hands and follows them as they move, and because the grips are still held it is picked up on arrival, ending in a two-handed hold. It can be caught early on the way in, and if nothing takes it, it falls. The gesture is ignored while a ball is held. This is a testing convenience for now; proper reset and recovery arrive in plan item 5.

Open with **Unity 6000.0.58f2** and its Android Build Support, SDK/NDK, and OpenJDK modules. OpenXR and the Input System target desktop Play mode and Android; Meta XR Core SDK 201.0.0 is installed for Quest setup. Package versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Start with [CLAUDE.md](CLAUDE.md) for the project requirements, then read only the relevant item of the [implementation plan](Docs/ImplementationPlan.md). Use Unity MCP for Editor work. The Editor's **VR Basketball > Automation** menu runs tests and builds; [automation commands](Docs/Automation.md) explain result files and device deployment.

The EditMode suite covers the release-velocity estimate; the PlayMode suite covers grab ownership, release behaviour, throw spin, the configuration caps, and dropping a held ball when a hand stops being tracked. An Android scaffold APK built successfully before the ball existed. Grabbing, one-hand ownership, carrying, and release are also confirmed in the Meta XR Simulator with real controller poses and grip input. No headset check of grabbing or throwing has been done yet, so throwing feel is untuned. [AI_LOG.md](AI_LOG.md) records decisions and actual setup evidence; [the human journal](Docs/HumanJournal.md) holds developer feedback.

For optional desktop testing, see [Desktop XR tools](Docs/DesktopXR.md). For a real device, use [headset checks](Docs/HeadsetChecks.md).
