# Implementation plan

The project currently provides setup and verification tools. Gameplay is still to be implemented. Complete each numbered plan item as a small, reviewable change, record the actual checks in `AI_LOG.md`, and leave changes uncommitted for human review.

## 1. Custom rig and input

- Create the player root, tracked camera, and left/right controller transforms using OpenXR and the Input System.
- Define explicit input actions for holding the ball and reset. Document the selected bindings in the README when they work.
- Add simple hand or controller visuals. Keep tracking separate from gameplay decisions.
- Add a temporary forward target to establish the player start direction; replace it with the physical hoop in section 3.
- Start with a stationary player. If movement or turning is requested, implement it in project code without interaction or locomotion packages.

Accept when head and controller poses respond in Play mode, each action reports the intended hand/control, the player starts facing the target, and tracking/input work on the headset. Simulator checks can establish desktop behavior; record headset checks separately.

## 2. Custom grabbing and throwing

- Create a simple ball prefab with a Rigidbody and sphere collider for interaction checks.
- Select an eligible ball near the hand when the hold action begins.
- Keep one owner per ball and release ownership reliably when input ends or tracking is lost.
- Estimate release velocity from recent hand motion with bounded, configurable smoothing. Apply linear and angular motion to the Rigidbody on release.
- Apply sustained motion and forces at physics timing; sample input/poses and handle explicit grab/release transitions deliberately. Store useful tuning in small configuration assets.

Accept when either hand can pick up and release the ball, hands cannot both own it, a stationary release drops naturally, and different arm motions produce controllable throws. Add a PlayMode check for ownership and release behavior. Use real headset feedback to tune feel.

## 3. Court and physical hoop

- Build a readable court from simple geometry and materials, with a hoop, backboard, floor, and safe player start.
- Use colliders suited to the ball and rim. Configure ball mass, contact behavior, and collision detection to avoid visible tunneling.
- Keep useful dimensions and physics settings easy to find and adjust.

Accept when the player can reach a ball, throws can hit the backboard/rim, the ball bounces on the floor, and the scene is saved with all required references assigned.

## 4. Scoring and feedback

- Recognize a valid downward passage through the hoop and count it once.
- Keep scoring rules independent of XR/input so they can be checked without a headset.
- Show the score clearly and give brief visual/audio feedback when it changes.

Accept when a valid basket increments the score once, an upward or sideways entry does not award a basket, and lingering/repeated trigger contacts do not duplicate the score. Replace the setup smoke test with meaningful EditMode scoring checks and add a PlayMode check for the scene wiring.

## 5. Reset and recovery

- Provide a deliberate reset action and restore a usable ball position.
- Recover balls that leave the playable area or become inaccessible.
- Clear pending grab/scoring state safely during reset; define whether score is retained or reset and document that behavior.

Accept when reset works while holding the ball and after a throw, the ball is available again, old ownership cannot persist, and recovery cannot award a duplicate basket.

## 6. Feel and complete verification

- Adjust throw response, bounce, score readability, and feedback from actual human playtesting. Check movement comfort if movement has been requested and implemented.
- Verify every planned control, remove missing references, and run meaningful EditMode and PlayMode checks.
- Build the Android APK and verify launch, tracking, interaction, scoring, and reset on the intended Quest device.
- Update the README to match implemented behavior and record the results and remaining issues.

Accept when the verification report passes, the APK builds successfully, and the headset checklist has actual observed results. A passing setup smoke test or Simulator session alone does not establish gameplay readiness.
