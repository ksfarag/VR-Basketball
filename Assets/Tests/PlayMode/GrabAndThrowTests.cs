using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// Runs the grab and release rules against real physics, with the hands driven
    /// directly so no headset or controller is needed.
    /// </summary>
    public class GrabAndThrowTests
    {
        private GrabSettings settings;
        private Ball ball;
        private HandGrabber left;
        private HandGrabber right;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<GrabSettings>();
            ball = CreateBall(new Vector3(0f, 5f, 0f));
            left = CreateHand(Hand.Left, new Vector3(-0.02f, 5f, 0f));
            right = CreateHand(Hand.Right, new Vector3(0.02f, 5f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            if (ball != null) Object.Destroy(ball.gameObject);
            if (left != null) Object.Destroy(left.gameObject);
            if (right != null) Object.Destroy(right.gameObject);
            if (settings != null) Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator HandPicksUpABallWithinReach()
        {
            right.BeginHold();
            yield return Steps(2);

            Assert.AreSame(ball, right.Held, "a ball at the hand should be picked up");
            Assert.AreSame(right, ball.Holder);
            Assert.IsFalse(ball.Body.useGravity, "a carried ball must not sag under gravity");
        }

        [UnityTest]
        public IEnumerator HandIgnoresABallOutOfReach()
        {
            right.transform.position = new Vector3(0f, 5f, 2f);
            right.BeginHold();
            yield return Steps(3);

            Assert.IsNull(right.Held, "a ball two metres away is not eligible");
            Assert.IsNull(ball.Holder);
        }

        [UnityTest]
        public IEnumerator OnlyOneHandEverOwnsTheBall()
        {
            left.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, left.Held, "the left hand should have the ball");
            Assert.AreSame(left, ball.Holder);

            right.BeginHold();
            yield return Steps(3);
            Assert.AreSame(left, ball.Holder, "ownership must not change while the ball is held");
            Assert.AreSame(right, ball.Support, "the second hand steadies the ball");
            Assert.IsFalse(right.IsCarrying, "the second hand does not own it");
            Assert.IsTrue(left.IsCarrying, "the first hand still drives it");

            right.EndHold();
            left.EndHold();
            yield return Steps(2);
            Assert.IsNull(ball.Holder, "releasing both must clear ownership");
            Assert.IsNull(ball.Support);
            Assert.IsNull(left.Held);
        }

        [UnityTest]
        public IEnumerator ReleasingTheOwningHandHandsOverInsteadOfThrowing()
        {
            left.BeginHold();
            yield return Steps(2);
            right.BeginHold();
            yield return Steps(3);
            Assert.AreSame(right, ball.Support, "the handover needs both hands on the ball");

            left.EndHold();
            yield return Steps(2);

            Assert.AreSame(right, ball.Holder, "the remaining hand takes the ball over");
            Assert.IsNull(ball.Support);
            Assert.IsNull(left.Held, "the hand that let go is empty");
            Assert.IsFalse(ball.Body.useGravity, "a ball still in hand must not start falling");
        }

        [UnityTest]
        public IEnumerator BallIsOnlyThrownOnceBothHandsLetGo()
        {
            left.BeginHold();
            yield return Steps(2);
            right.BeginHold();
            yield return Steps(3);

            left.EndHold();
            yield return Steps(2);
            Assert.IsTrue(ball.IsHeld, "one hand still has it, so it is not thrown yet");

            right.EndHold();
            yield return Steps(2);
            Assert.IsFalse(ball.IsHeld, "the last hand to let go throws it");
            Assert.IsTrue(ball.Body.useGravity, "a thrown ball falls again");
        }

        [UnityTest]
        public IEnumerator BallSettlesBetweenBothHands()
        {
            left.transform.position = new Vector3(-0.15f, 5f, 0f);
            right.transform.position = new Vector3(0.15f, 5f, 0f);
            yield return Steps(1);

            left.BeginHold();
            yield return Steps(2);
            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(right, ball.Support, "the second hand should have taken hold");

            // Move the second hand, which moves the point between the hands with it.
            right.transform.position = new Vector3(0.45f, 5f, 0f);
            yield return Steps(15);

            Vector3 midpoint = (left.transform.position + right.transform.position) * 0.5f;
            Assert.Less(Vector3.Distance(ball.Body.position, midpoint), 0.05f, "the ball should ride between the hands");
        }

        [UnityTest]
        public IEnumerator TwoHandedHoldKeepsTheBallAtArmsLength()
        {
            // A head at the origin looking down +Z, so "toward the player" is -Z.
            GameObject head = new GameObject("Head");
            head.transform.position = Vector3.zero;
            head.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            left.ViewReference = head.transform;
            right.ViewReference = head.transform;

            try
            {
                // The controllers sit behind the ball, and off to one side of it.
                ball.Body.position = new Vector3(0f, 1.2f, 0.8f);
                ball.Body.linearVelocity = Vector3.zero;
                left.transform.position = new Vector3(-0.2f, 1.2f, 0.7f);
                right.transform.position = new Vector3(0.1f, 1.2f, 0.7f);
                yield return Steps(1);

                left.BeginHold();
                yield return Steps(2);
                Assert.AreSame(left, ball.Holder, "the first hand should own the ball");

                right.BeginHold();
                yield return Steps(15);
                Assert.AreSame(right, ball.Support, "the second hand should be steadying it");

                Vector3 held = ball.Body.position;
                float expectedLateral = (left.transform.position.x + right.transform.position.x) * 0.5f;

                Assert.That(held.x, Is.EqualTo(expectedLateral).Within(0.03f), "the ball should centre across the hands");
                Assert.That(held.z, Is.EqualTo(0.8f).Within(0.03f), "the ball must keep its reach, not be pulled back to the controllers");

                // Twisting the controllers must not shove the ball around.
                left.transform.rotation = Quaternion.Euler(0f, 90f, 40f);
                right.transform.rotation = Quaternion.Euler(30f, -60f, 0f);
                yield return Steps(10);

                Assert.Less(Vector3.Distance(ball.Body.position, held), 0.03f, "turning the controllers must not move the ball");
            }
            finally
            {
                Object.Destroy(head);
            }
        }

        [UnityTest]
        public IEnumerator TwoHandedHoldCanBeTurnedOff()
        {
            SetSetting("allowTwoHandedHold", false);

            left.BeginHold();
            yield return Steps(2);
            Assert.AreSame(left, ball.Holder);

            right.BeginHold();
            yield return Steps(3);

            Assert.IsNull(ball.Support, "the second hand is refused when two-handed hold is off");
            Assert.IsNull(right.Held);
            Assert.AreSame(left, ball.Holder);
        }

        [UnityTest]
        public IEnumerator ReleaseHandsTheBallToTheOtherHand()
        {
            left.BeginHold();
            yield return Steps(2);
            Assert.AreSame(left, ball.Holder);

            left.EndHold();
            right.BeginHold();
            yield return Steps(3);

            Assert.AreSame(right, ball.Holder, "a freed ball can be taken by the other hand");
            Assert.IsNull(left.Held);
        }

        [UnityTest]
        public IEnumerator StationaryReleaseDropsTheBall()
        {
            right.BeginHold();
            yield return Steps(4);
            Assert.AreSame(ball, right.Held);
            float heldHeight = ball.Body.position.y;

            right.EndHold();
            yield return Steps(1);

            Assert.IsNull(ball.Holder);
            Assert.IsTrue(ball.Body.useGravity, "gravity must return when the ball is free");
            Vector3 sideways = Vector3.ProjectOnPlane(ball.Body.linearVelocity, Vector3.up);
            Assert.Less(sideways.magnitude, 0.5f, "a still hand must not throw the ball sideways");

            yield return Steps(10);
            Assert.Less(ball.Body.linearVelocity.y, -1f, "the ball must accelerate downward");
            Assert.Less(ball.Body.position.y, heldHeight, "the ball must fall away from the hand");
        }

        [UnityTest]
        public IEnumerator MovingHandThrowsAtHandSpeed()
        {
            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, right.Held, "the throw needs a ball in hand");

            const float speed = 3f;
            yield return MoveHandForward(speed);

            right.EndHold();
            // The hand now stays still. The throw must still carry the speed it had when
            // the player let go.
            yield return Steps(3);

            // The grabber samples poses in Update and this coroutine moves the hand after
            // it, so a sampled position is one frame older than its timestamp. Even frame
            // pacing cancels that out, uneven pacing does not, so this asserts direction
            // and order of magnitude. Exact fidelity is measured in the Simulator against
            // real tracked poses instead, and recorded in AI_LOG.md.
            Vector3 released = ball.Body.linearVelocity;
            Assert.IsNull(ball.Holder, "a thrown ball is no longer owned");
            Assert.Greater(released.z, speed * 0.5f, "the throw should carry the hand speed");
            Assert.Less(released.z, speed * 2.5f, "the throw should not run away");
        }

        [UnityTest]
        public IEnumerator SpinningHandThrowsTheBallSpinning()
        {
            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, right.Held, "the throw needs a ball in hand");

            const float degreesPerSecond = 180f;
            yield return TurnHandAboutUp(degreesPerSecond);

            right.EndHold();
            yield return Steps(3);

            Vector3 spin = ball.Body.angularVelocity;
            float expected = degreesPerSecond * Mathf.Deg2Rad;
            Assert.IsNull(ball.Holder);
            Assert.Greater(spin.magnitude, expected * 0.75f, "release must carry the hand's spin");
            Assert.Less(spin.magnitude, expected * 1.25f, "release must not invent spin");
            Assert.Greater(Vector3.Dot(spin.normalized, Vector3.up), 0.9f, "the ball should spin about the axis the hand turned about");
        }

        [UnityTest]
        public IEnumerator BallHeldOffToTheSideGainsSpeedFromTheSwing()
        {
            // The hand pivots in place, so all of the ball's speed comes from the lever
            // arm: v = omega x r, which is why a wrist flick still throws.
            const float offset = 0.2f;
            ball.transform.position = right.transform.position + (Vector3.right * offset);
            yield return Steps(1);

            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, right.Held, "a ball 0.2 m out is still within reach of its surface");

            const float degreesPerSecond = 180f;
            yield return TurnHandAboutUp(degreesPerSecond);

            right.EndHold();
            yield return Steps(1);

            float expected = degreesPerSecond * Mathf.Deg2Rad * offset;
            Vector3 released = ball.Body.linearVelocity;
            Assert.Greater(released.magnitude, expected * 0.6f, "a pivoting hand should still throw the ball");
            Assert.Less(released.magnitude, expected * 1.6f, "the lever arm should not run away");
        }

        [UnityTest]
        public IEnumerator SettingsCapTheThrowSpeed()
        {
            SetSetting("maxThrowSpeed", 1f);

            right.BeginHold();
            yield return Steps(2);
            yield return MoveHandForward(4f);

            right.EndHold();
            yield return Steps(1);

            float horizontal = Vector3.ProjectOnPlane(ball.Body.linearVelocity, Vector3.up).magnitude;
            Assert.LessOrEqual(horizontal, 1.05f, "maxThrowSpeed must cap a fast hand");
            Assert.Greater(horizontal, 0.5f, "the cap must not zero the throw");
        }

        [UnityTest]
        public IEnumerator SettingsScaleTheThrowSpeed()
        {
            SetSetting("throwSpeedScale", 2f);

            right.BeginHold();
            yield return Steps(2);
            yield return MoveHandForward(1.5f);

            right.EndHold();
            yield return Steps(1);

            Assert.Greater(ball.Body.linearVelocity.z, 1.5f * 2f * 0.75f, "throwSpeedScale must multiply the throw");
            Assert.Less(ball.Body.linearVelocity.z, 1.5f * 2f * 1.25f);
        }

        [UnityTest]
        public IEnumerator SettingsCanRemoveThrowSpin()
        {
            SetSetting("throwSpinScale", 0f);

            right.BeginHold();
            yield return Steps(2);
            yield return TurnHandAboutUp(180f);

            right.EndHold();
            yield return Steps(1);

            Assert.That(ball.Body.angularVelocity.magnitude, Is.EqualTo(0f).Within(0.01f), "a zero spin scale must release without spin");
        }

        [UnityTest]
        public IEnumerator DisablingAHandDropsItsBall()
        {
            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, right.Held);

            right.gameObject.SetActive(false);
            yield return Steps(1);

            Assert.IsNull(ball.Holder, "a hand that goes away must not keep the ball");
            Assert.IsTrue(ball.Body.useGravity);
        }

        // Motion is a straight function of time, so the fitted swing reports exactly this
        // speed however the frames happen to land. It runs for longer than the release
        // window so the whole window is real motion; a hand that had been still for part
        // of it would be read as still accelerating and throw harder than it is moving.
        private IEnumerator MoveHandForward(float speed)
        {
            Vector3 start = right.transform.position;
            float began = Time.time;
            int frames = 0;
            while (Time.time - began < 0.25f || frames < 10)
            {
                right.transform.position = start + (Vector3.forward * (speed * (Time.time - began)));
                frames++;
                yield return null;
            }
        }

        private IEnumerator TurnHandAboutUp(float degreesPerSecond)
        {
            Quaternion start = right.transform.rotation;
            float began = Time.time;
            int frames = 0;
            while (Time.time - began < 0.25f || frames < 10)
            {
                right.transform.rotation = Quaternion.AngleAxis(degreesPerSecond * (Time.time - began), Vector3.up) * start;
                frames++;
                yield return null;
            }
        }

        // The tuning fields are private and serialized, so a test varies them directly
        // rather than widening the runtime API.
        private void SetSetting(string field, object value)
        {
            FieldInfo info = typeof(GrabSettings).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, "GrabSettings has no field named '" + field + "'");
            info.SetValue(settings, value);
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }

        private Ball CreateBall(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Test Ball";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.239f;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.62f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return go.AddComponent<Ball>();
        }

        // Built inactive so the settings are in place before the grabber enables.
        private HandGrabber CreateHand(Hand hand, Vector3 position)
        {
            GameObject go = new GameObject(hand + " Hand");
            go.SetActive(false);
            go.transform.position = position;
            HandGrabber grabber = go.AddComponent<HandGrabber>();
            grabber.Settings = settings;
            grabber.Hand = hand;
            go.SetActive(true);
            return grabber;
        }
    }
}
