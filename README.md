# Airball Arena VR

A Unity project for a small Meta Quest 3 basketball game with custom interaction code. **Gameplay is partly implemented.** `Assets/Scenes/Gameplay.unity` has a tracked player rig, input actions, controller models, a ball either hand can pick up and throw, a court and hoop, and scoring shown on a readout above the backboard; it has no reset or ball recovery beyond the recall gesture.

Tracking and controller visuals use Meta XR Core's `OVRCameraRig` and `OVRControllerPrefab`, which supply head and hand poses and the animated Quest 3 controller models. Grabbing, throwing, scoring, and any movement remain project code.

## Controls

| Action | Quest Touch control |
|---|---|
| Hold ball (left hand) | Left grip |
| Hold ball (right hand) | Right grip |
| Dribble the held ball | Left **X** (hold) |
| Put the ball back | Right **B** (tap) |
| Clear the score | Right **B** (hold ~1 s) |
| Hold with both hands | Both grips, while one hand has the ball |
| Call the ball back | Both grips, with empty hands |

The bindings are in `Assets/Input/BasketballControls.inputactions`. `GameplayInput` on the Player Rig reports them by hand. The same asset also holds `Tracked Left` and `Tracked Right`, bound to each controller's `isTracked`; these are not player controls, they let a hand drop its ball when that controller stops being tracked, and they set `initialStateCheck` so a hand that starts up already tracked is recognised as such. The hold and reset bindings are verified in the Simulator and on a Quest 3 over Quest Link; an installed APK has not been tested yet.

## Grabbing and throwing

A `HandGrabber` under each hand anchor picks up the nearest eligible `Ball` when its hold action begins, carries it by Rigidbody velocity so it still collides while held, and throws it on release using the hand motion measured at the moment the player let go. Exactly one hand can own a ball. Tuning lives in `Assets/Config/GrabSettings.asset` — reach, smoothing window and sample bound, throw scales, and speed and spin caps. None of this uses `OVRGrabber`, `OVRGrabbable`, or any other pre-made grab behaviour; the runtime assembly does not reference Meta's `Oculus.VR` assembly at all.

## Dribbling

Holding **X** dribbles the ball the carrying hand has: it bounces off whatever is under the hand and comes back up into it, over and over, until X is released. Either hand dribbles — X is one control and the hand that owns the ball is the one that acts on it — so the thumb that presses it is not the hand that has the ball.

The bounce is driven, not thrown. The hand drives a held ball by Rigidbody velocity anyway, and the dribble only moves the point it is driven to: down to the floor and back. So the ball still collides on the way, the hand never has to catch it, and it always comes home to where the hand is *now* rather than where it was when the bounce started — walk the hand sideways and the bounce follows. `DribbleMotion` holds the timing alone, with no ball or hand in it, so the rhythm is checked without physics. Each bounce is a fall from rest at the hand and the same fall run backwards, so the ball is slowest in the hand and fastest at the floor, and it is held for one step exactly on the floor so every bounce visibly lands however the physics steps happen to fall; the wait is taken back on the way up and the rhythm does not drift.

How far it falls is found each step by casting the ball's own shape straight down from where the hand holds it, so it bounces off the floor, and off anything else in the way, at the right height. With nothing within `dribbleReach` underneath — the ball held out over the edge of the court, or above head height — there is nothing to bounce off and the ball simply stays in the hand.

Releasing X finishes the bounce already under way and leaves the ball in the hand; a ball in mid-air cannot be called back. Letting go of the **grip** mid-bounce is different: the ball is out of the hand, so it keeps the speed it had and carries on rather than being thrown with the hand's swing. To shoot out of a dribble, let go of X first and take the ball back. Taking hold with the second hand ends the dribble where the ball is, because a ball held in two hands is not being dribbled. `dribblePeriod` (0.5 s per bounce) and `dribbleReach` (2 m) are in `Assets/Config/GrabSettings.asset`.

A second hand can take hold of a ball the other is already holding. It becomes a *support* rather than a second owner, so exactly one hand ever owns the ball: the ball eases to the point between the hands, a throw is measured from that point, letting go with the owning hand hands the ball over instead of dropping it, and the ball is only thrown once both hands let go. Turn `allowTwoHandedHold` off in the settings asset for the old single-hand behaviour.

## Reset

A tap of **B** puts every ball back at the `Ball Start` point in front of the player and **keeps the score**, so recovering from a fumble costs nothing. It works whatever the ball is doing: in mid-flight, being carried back by a recall, or held in a fist that is still closed — the ball stops being the hand's rather than the hand being asked to let go, which is what stops a reset launching the ball with the throw the player was in the middle of. Holding **B** for about a second *also* clears the score to 0; that is how a session is started fresh, and the long press is deliberate enough not to happen by accident. Every hoop's pending shot is forgotten on a reset, so carrying the ball back down past a ring is not a basket.

Reset and recall do different jobs: **B** puts the ball back on the court, both grips bring a loose ball to your hands. Automatic recovery of a ball that leaves the playable area is *not* implemented — the temporary bounds walls keep the ball in, and recall fetches it from anywhere if one escapes.

`BallRecall` on the Player Rig calls a loose ball back when both grips are squeezed with empty hands. It homes to the point between the hands and follows them as they move, and because the grips are still held it is picked up on arrival, ending in a two-handed hold. It can be caught early on the way in, and if nothing takes it, it falls. The gesture is ignored while a ball is held. This is a testing convenience for now; proper reset and recovery arrive in plan item 5.

## Scoring

A basket is a downward crossing of the ring's plane inside the hole, judged on the path the ball took between two physics steps rather than on what a trigger volume reports. A ball falling through a hoop covers more than a tenth of a metre per step, so a trigger thin enough to sit in the ring is one a shot can pass straight through, and a ball that comes to rest in one reports itself over and over. A shot only counts once the ball has been clearly above the ring, and counts once: rattling at the rim, rolling about underneath, or coming up through the hole from below awards nothing. A ball the game is carrying — the recall — is ignored, so calling the ball back through the hoop is not a free two points.

`BasketDetector` is the rule on its own, with no scene, physics, or input behind it. `BasketSensor` on the ring feeds it ball positions at physics timing and announces each basket; `ScoreKeeper` counts them, at `pointsPerBasket` each; `Scoreboard` shows the total on a three-digit seven-segment readout resting on top of the backboard, and marks a basket by turning the lit bars green, swelling the board briefly, and playing a chime. None of this reads the headset or the controls, so every scoring rule is checked in the EditMode suite without a device. Rebuild the court after changing a dimension and the sensor and readout are rebuilt and rewired with it.

The court is built from `Assets/Config/CourtSettings.asset` at regulation figures with two deliberate exceptions, made after a playtest where a regulation hoop proved too hard to score on standing still: `rimHeight` is **2.7 m** rather than 3.048, and `rimInnerRadius` is **0.32 m** rather than 0.2286, which makes the hole 2.65 ball-widths across against a regulation 1.91. `backboardBottomHeight` moved down with the ring so the board keeps the same relationship to it. Change either number and run **Airball Arena VR > Court > Rebuild Court**; the ring, board, post, scoring radius, and readout are all re-derived from the asset.

Open with **Unity 6000.0.58f2** and its Android Build Support, SDK/NDK, and OpenJDK modules. OpenXR and the Input System target desktop Play mode and Android; Meta XR Core SDK 201.0.0 is installed for Quest setup. Package versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Start with [CLAUDE.md](CLAUDE.md) for the project requirements, then read only the relevant item of the [implementation plan](Docs/ImplementationPlan.md). Use Unity MCP for Editor work. The Editor's **Airball Arena VR > Automation** menu runs tests and builds; [automation commands](Docs/Automation.md) explain result files and device deployment.

The EditMode suite covers the release-velocity estimate, the dribble's timing, the hoop geometry against the ball that has to fit through it, and every scoring rule; the PlayMode suite covers grab ownership, release behaviour, throw spin, the configuration caps, dropping a held ball when a hand stops being tracked, the court's collision behaviour, the dribble against a real floor, and scoring both against real physics and in the saved scene. An Android scaffold APK built successfully before the ball existed. Grabbing, one-hand ownership, carrying, and release are also confirmed in the Meta XR Simulator with real controller poses and grip input. No headset check of grabbing or throwing has been done yet, so throwing feel is untuned. [AI_LOG.md](AI_LOG.md) records decisions and actual setup evidence; [the human journal](Docs/HumanJournal.md) holds developer feedback.

For optional desktop testing, see [Desktop XR tools](Docs/DesktopXR.md). For a real device, use [headset checks](Docs/HeadsetChecks.md).
