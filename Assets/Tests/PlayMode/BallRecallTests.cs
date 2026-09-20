using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// Covers calling a loose ball back, including the cases that must not fire it, with
    /// the gesture driven directly so no controller is needed.
    /// </summary>
    public class BallRecallTests
    {
        private GrabSettings settings;
        private Ball ball;
        private HandGrabber hand;
        private HandGrabber otherHand;
        private BallRecall recall;
        private Transform returnPoint;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<GrabSettings>();

            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Test Ball";
            ballObject.transform.position = new Vector3(0f, 3f, 12f);
            ballObject.transform.localScale = Vector3.one * 0.239f;
            ballObject.AddComponent<Rigidbody>().mass = 0.62f;
            ball = ballObject.AddComponent<Ball>();

            returnPoint = new GameObject("Return Point").transform;
            returnPoint.position = new Vector3(0f, 1.2f, 0f);

            hand = CreateHand(Hand.Right, new Vector3(0.2f, 1.2f, 0f));
            otherHand = CreateHand(Hand.Left, new Vector3(-0.2f, 1.2f, 0f));

            GameObject recallObject = new GameObject("Recall");
            recallObject.SetActive(false);
            recall = recallObject.AddComponent<BallRecall>();
            recall.Hands = new[] { hand, otherHand };
            recall.ReturnPoint = returnPoint;
            recallObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (ball != null) Object.Destroy(ball.gameObject);
            if (hand != null) Object.Destroy(hand.gameObject);
            if (otherHand != null) Object.Destroy(otherHand.gameObject);
            if (recall != null) Object.Destroy(recall.gameObject);
            if (returnPoint != null) Object.Destroy(returnPoint.gameObject);
            if (settings != null) Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator RecallBringsALooseBallToTheHand()
        {
            Assert.IsTrue(recall.TryRecall(), "a loose ball should answer the call");
            Assert.AreSame(ball, recall.Returning);

            // Measured on arrival: once delivered and untaken it falls, as it should.
            yield return WaitForDelivery();

            Assert.IsNull(recall.Returning, "the flight should have finished");
            Vector3 midpoint = (hand.transform.position + otherHand.transform.position) * 0.5f;
            Assert.Less(Vector3.Distance(ball.Body.position, midpoint), 0.1f, "the ball should arrive between the hands");
        }

        [UnityTest]
        public IEnumerator RecallFollowsAHandThatMoves()
        {
            Assert.IsTrue(recall.TryRecall());

            // Both hands move while the ball is on its way in.
            hand.transform.position = new Vector3(1.6f, 1.4f, -0.5f);
            otherHand.transform.position = new Vector3(1.4f, 1.4f, -0.5f);
            yield return WaitForDelivery();

            Vector3 midpoint = (hand.transform.position + otherHand.transform.position) * 0.5f;
            Assert.Less(Vector3.Distance(ball.Body.position, midpoint), 0.15f, "the ball should home to where the hands went");
        }

        [UnityTest]
        public IEnumerator RecallWithBothGripsHeldArrivesCentredInBothHands()
        {
            // The call gesture is both grips, so both are still down on arrival and the
            // ball is put into both: one owns it, the other steadies it.
            hand.BeginHold();
            otherHand.BeginHold();
            Assert.IsTrue(recall.TryRecall());

            yield return WaitForDelivery();
            yield return Steps(2);

            Assert.IsTrue(ball.IsHeld, "the ball should arrive already in hand");
            Assert.IsTrue(ball.IsHeldWithBothHands, "both hands were asking for it");
            Assert.IsFalse(ball.Body.useGravity, "a held ball does not fall");

            Vector3 midpoint = (hand.transform.position + otherHand.transform.position) * 0.5f;
            Assert.Less(Vector3.Distance(ball.Body.position, midpoint), 0.1f, "it should land centred, not off at one controller");
        }

        [UnityTest]
        public IEnumerator AReturningBallIsNotSnatchedPartWayIn()
        {
            // A hand squeezing right next to the flight path must not cut the delivery
            // short, which is what used to leave the ball lopsided.
            ball.Body.position = new Vector3(0f, 1.2f, 6f);
            yield return Steps(1);
            Assert.IsTrue(recall.TryRecall());

            hand.BeginHold();
            hand.transform.position = new Vector3(0f, 1.2f, 3f);
            yield return Steps(6);

            Assert.IsNull(hand.Held, "the ball is reserved while it is on its way");
            Assert.IsNotNull(recall.Returning, "the flight should still be running");
        }

        [UnityTest]
        public IEnumerator UncaughtBallFallsRatherThanHangingInTheAir()
        {
            Assert.IsTrue(recall.TryRecall());
            yield return WaitForDelivery();

            Assert.IsTrue(ball.Body.useGravity, "nothing took it, so gravity returns");

            float height = ball.Body.position.y;
            yield return Steps(10);
            Assert.Less(ball.Body.position.y, height, "it should fall once delivered and untaken");
        }

        [UnityTest]
        public IEnumerator RecallIsRefusedWhileAHandHoldsABall()
        {
            ball.Body.position = hand.transform.position;
            yield return Steps(1);
            hand.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, hand.Held, "the refusal needs a ball in hand");

            Assert.IsFalse(recall.TryRecall(), "holding a ball must leave the gesture free for something else");
            Assert.IsNull(recall.Returning);
            Assert.AreSame(hand, ball.Holder, "the held ball must not be disturbed");
        }

        [UnityTest]
        public IEnumerator RecallDoesNothingWithNowhereToDeliver()
        {
            recall.Hands = null;
            recall.ReturnPoint = null;
            yield return null;

            Assert.IsFalse(recall.TryRecall(), "there is nowhere to deliver the ball");
            Assert.IsNull(recall.Returning);
        }

        // Stops as soon as the flight ends, so arrival is measured before the ball has
        // had time to fall away from where it was delivered.
        private IEnumerator WaitForDelivery(int maxSteps = 80)
        {
            for (int i = 0; i < maxSteps && recall.Returning != null; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }

        private HandGrabber CreateHand(Hand which, Vector3 position)
        {
            GameObject go = new GameObject(which + " Hand");
            go.SetActive(false);
            go.transform.position = position;
            HandGrabber grabber = go.AddComponent<HandGrabber>();
            grabber.Settings = settings;
            grabber.Hand = which;
            go.SetActive(true);
            return grabber;
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }
    }
}
