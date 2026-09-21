using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// The dribble against real physics, with a floor to bounce off and the hands driven
    /// directly so no headset or controller is needed. The timing itself is covered by the
    /// EditMode suite; what is checked here is that a dribbled ball actually reaches the
    /// floor, comes back to the hand, and stays the hand's the whole way.
    /// </summary>
    public class DribbleTests
    {
        private const float Radius = 0.1195f;
        private const float HandHeight = 1.2f;

        // One bounce is 0.5 s of 0.02 s steps, with a couple to spare.
        private const int Bounce = 27;

        private GrabSettings settings;
        private GameObject floor;
        private Ball ball;
        private HandGrabber left;
        private HandGrabber right;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<GrabSettings>();
            floor = BuildFloor();
            ball = CreateBall(new Vector3(0f, HandHeight, 0f));
            left = CreateHand(Hand.Left, new Vector3(-2f, HandHeight, 0f));
            right = CreateHand(Hand.Right, new Vector3(0.02f, HandHeight, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            if (floor != null) Object.Destroy(floor);
            if (ball != null) Object.Destroy(ball.gameObject);
            if (left != null) Object.Destroy(left.gameObject);
            if (right != null) Object.Destroy(right.gameObject);
            if (settings != null) Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator TheBallBouncesOffTheFloorAndComesBackToTheHand()
        {
            yield return Grab();

            right.BeginDribble();

            float lowest = float.PositiveInfinity;
            for (int i = 0; i < Bounce; i++)
            {
                yield return Steps(1);
                lowest = Mathf.Min(lowest, ball.Body.position.y);
                Assert.AreSame(right, ball.Holder, "the hand keeps the ball while it dribbles");
            }

            Assert.That(lowest, Is.LessThan(Radius + 0.04f), $"the ball got no closer to the floor than {lowest:F3} m");
            Assert.That(lowest, Is.GreaterThan(Radius - 0.05f), "the ball went through the floor");

            right.EndDribble();
            yield return Steps(Bounce);

            Assert.IsFalse(right.IsDribbling, "the dribble should be over");
            Assert.That(ToHand(), Is.LessThan(0.2f), "the ball should end up back at the hand");
        }

        [UnityTest]
        public IEnumerator StoppingTheDribbleLeavesTheBallInTheHand()
        {
            yield return Grab();

            right.BeginDribble();
            yield return Steps(12);
            Assert.IsTrue(right.IsDribbling, "the ball should be out on a bounce");

            right.EndDribble();
            yield return Steps(Bounce);

            // A ball that is merely held again would still be held if another bounce had
            // started, so the check is that it stays put for longer than a bounce lasts.
            for (int i = 0; i < Bounce; i++)
            {
                yield return Steps(1);
                Assert.That(ToHand(), Is.LessThan(0.2f), "the ball bounced again after the dribble was stopped");
            }

            Assert.AreSame(right, ball.Holder);
        }

        [UnityTest]
        public IEnumerator TheBallStaysInTheHandWithNothingUnderneathToBounceOff()
        {
            right.transform.position = new Vector3(0.02f, 5f, 0f);
            ball.Body.position = new Vector3(0f, 5f, 0f);
            ball.transform.position = ball.Body.position;
            yield return Grab();

            right.BeginDribble();

            for (int i = 0; i < Bounce; i++)
            {
                yield return Steps(1);
                Assert.That(ToHand(), Is.LessThan(0.2f),
                    "with the floor out of reach the ball has nothing to bounce off and should stay in the hand");
            }

            Assert.IsFalse(right.IsDribbling);
        }

        [UnityTest]
        public IEnumerator LettingGoMidBounceLeavesTheBallMovingTheWayItWas()
        {
            yield return Grab();

            right.BeginDribble();
            yield return Steps(10);

            Vector3 before = ball.Body.linearVelocity;
            Assert.That(before.y, Is.LessThan(-2f), "the ball should be well on its way down by now");

            // The hand has not moved, so a throw measured from the hand would be nothing.
            right.EndHold();
            yield return Steps(1);

            Assert.IsNull(ball.Holder, "letting go must free the ball");
            Assert.IsTrue(ball.Body.useGravity, "a freed ball falls again");
            Assert.That(ball.Body.linearVelocity.y, Is.LessThan(before.y * 0.5f),
                "the ball should keep the speed it had, not be dropped from a standstill");
        }

        [UnityTest]
        public IEnumerator ASecondHandTakingHoldEndsTheDribble()
        {
            yield return Grab();

            right.BeginDribble();
            yield return Steps(8);
            Assert.IsTrue(right.IsDribbling);

            left.transform.position = ball.Body.position;
            left.BeginHold();
            yield return Steps(3);

            Assert.AreSame(left, ball.Support, "the second hand should have taken hold");
            Assert.IsFalse(right.IsDribbling, "a ball held with two hands is not being dribbled");
            Assert.AreSame(right, ball.Holder, "the first hand still owns it");
        }

        [UnityTest]
        public IEnumerator AskingToDribbleWithNothingInHandDoesNothing()
        {
            right.BeginDribble();
            yield return Steps(5);

            Assert.IsNull(right.Held, "asking to dribble must not pick a ball up");
            Assert.IsFalse(right.IsDribbling);

            // The request stands, so the ball dribbles as soon as the hand has one.
            yield return Grab();
            yield return Steps(4);

            Assert.IsTrue(right.IsDribbling, "the ball should start bouncing once it is picked up");
        }

        private float ToHand() => Vector3.Distance(ball.Body.position, right.transform.position);

        private IEnumerator Grab()
        {
            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, right.Held, "the hand should have the ball before it can dribble it");
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }

        private static GameObject BuildFloor()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Test Floor";
            go.transform.position = new Vector3(0f, -0.5f, 0f);
            go.transform.localScale = new Vector3(10f, 1f, 10f);
            return go;
        }

        private static Ball CreateBall(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Test Ball";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (Radius * 2f);
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
