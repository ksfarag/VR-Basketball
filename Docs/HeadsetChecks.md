# Headset checks

This checklist applies to an implemented scene and Android build. No headset result is implied by its presence. Record the device model, APK or scene version, observed behavior, and any issue in `AI_LOG.md`.

The target is a **standalone Meta Quest 3** (Android) build. Most day-to-day testing uses Quest Link with normal Unity Play, which is valid evidence for tracking, input, and scene checks; APK launch, Android performance, and standalone behavior still need an installed build.

Before device actions, enumerate connected devices and select the intended serial. For more than one device, keep that selection explicit throughout install, launch, logs, and capture.

## Tracking and comfort

- The app launches on the intended device and presents the expected scene.
- Head movement updates the camera, and left/right controller poses match the correct hands.
- The player starts at a sensible height and distance facing the hoop.
- If movement or turning has been implemented, it responds to the documented controls with comfortable defaults.
- Tracking loss or pausing the app does not leave a ball permanently held or the player in a broken state.

## Ball interaction

- Either hand can reach, pick up, hold, and release the ball.
- A ball has one owner at a time; transfers or simultaneous input do not duplicate it.
- A stationary release drops the ball; a deliberate throw produces controllable speed and direction.
- Holding the dribble control bounces the ball off the floor and back into the hand at a rhythm that feels like dribbling, and letting go leaves the ball in the hand.
- Throws respond across gentle, quick, and short arm motions without unexpected velocity spikes.
- Floor, rim, and backboard contacts look and sound appropriate; the ball does not routinely pass through geometry.

## Scoring and reset

- A valid downward basket changes the visible score once.
- Upward entry, rim contact, and prolonged trigger overlap do not award false or duplicate scores.
- Score feedback is readable/audible without obscuring play.
- Reset works while holding the ball, after a miss, and after a basket.
- A lost or inaccessible ball becomes available again without stale grab or scoring state.

## Runtime quality

- Inspect device logs for exceptions and repeated errors during actual play.
- Check frame stability on the headset while moving and throwing; desktop frame rate is separate evidence.
- Restart the Android app, then complete at least one basket and one reset.
- Record actual human feedback and what changed in response, then recheck the affected behavior.

Use Simulator for desktop input and scene checks. It models supported OpenXR behavior and does not emulate Android or device hardware, so it cannot establish APK launch or headset performance.
