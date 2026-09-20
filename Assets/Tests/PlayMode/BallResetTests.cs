using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// The reset, against real physics and a real hand. The cases that matter are the ones
    /// where the ball is not simply lying on the floor waiting to be moved: a hand is
    /// holding it, it is in mid-flight, or putting it back would carry it through the hoop.
    /// </summary>
    public class BallResetTests
    {
        private const float BallRadius = 0.1195f;
        private const float RimInnerRadius = 0.2286f;
        private const float RimTubeRadius = 0.01f;
        private const int RimSegments = 24;
        private const float PlaneHeight = 3.038f;
        private const float Centreline = RimInnerRadius + RimTubeRadius;

        private readonly List<GameObject> spawned = new List<GameObject>();

        private GrabSettings settings;
        private BallReset reset;
        private ScoreKeeper score;
        private BasketSensor sensor;
        private HandGrabber hand;
        private Ball ball;
        private Transform start;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<GrabSettings>();

            start = Spawn("Ball Start").transform;
            // Deliberately directly below the ring, which the real scene's ball start is
            // not. This is the arrangement in which putting the ball back drags it down
            // through the hole, so it is the arrangement that tests whether a reset can
            // award a basket. With the start off to one side the path crosses the ring's
            // plane well outside the hole and the question never arises.
            start.position = new Vector3(0f, BallRadius, 0f);

            sensor = BuildRing();
            ball = BuildBall(new Vector3(0f, 1.2f, 0f));
            hand = BuildHand(new Vector3(0f, 1.2f, 0f));

            GameObject keeper = Spawn("Score");
            keeper.SetActive(false);
            score = keeper.AddComponent<ScoreKeeper>();
            score.Sensors = new[] { sensor };
            score.PointsPerBasket = 2;
            keeper.SetActive(true);

            GameObject resetObject = Spawn("Reset");
            resetObject.SetActive(false);
            reset = resetObject.AddComponent<BallReset>();
            reset.BallStart = start;
            reset.Score = score;
            reset.Sensors = new[] { sensor };
            resetObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.Destroy(go);

            spawned.Clear();

            if (settings != null)
                Object.Destroy(settings);
        }

        [UnityTest]
        public IEnumerator ResetTakesTheBallOutOfAHandThatIsStillHoldingIt()
        {
            hand.BeginHold();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(hand.IsCarrying, "the hand should have picked the ball up to begin with");

            // The grip is never released: this is a reset pressed mid-hold, which is the
            // plan item's first acceptance criterion.
            reset.Restore();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsNull(ball.Holder, "no hand owns the ball after a reset");
            Assert.IsNull(ball.Support);
            Assert.IsFalse(hand.IsCarrying, "and the hand knows it is no longer carrying anything");
            Assert.That(Vector3.Distance(ball.Body.position, start.position), Is.LessThan(0.35f),
                "the ball went back to its start rather than staying in the hand");
        }

        [UnityTest]
        public IEnumerator ResetDoesNotThrowTheBallItTakesBack()
        {
            hand.BeginHold();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Moving the hand hard gives the grabber a throw to measure. Asking the hand to
            // let go here would apply that throw a step later, from the start point.
            for (int i = 0; i < 12; i++)
            {
                hand.transform.position += new Vector3(0f, 0f, 0.05f);
                yield return new WaitForFixedUpdate();
            }

            reset.Restore();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(ball.Body.linearVelocity.magnitude, Is.LessThan(1f),
                $"the ball was launched at {ball.Body.linearVelocity.magnitude:F2} m/s by a reset that should have set it down");
        }

        [UnityTest]
        public IEnumerator ResetWorksOnABallInMidFlight()
        {
            ball.Body.position = new Vector3(2f, 3f, 2f);
            ball.Body.linearVelocity = new Vector3(4f, 3f, 1f);

            yield return new WaitForFixedUpdate();

            reset.Restore();

            yield return new WaitForFixedUpdate();

            Assert.That(Vector3.Distance(ball.Body.position, start.position), Is.LessThan(0.35f),
                "a thrown ball comes back too");
            Assert.That(ball.Body.linearVelocity.magnitude, Is.LessThan(1f), "and it arrives at rest");
        }

        [UnityTest]
        public IEnumerator ResetCarryingTheBallPastTheRingDoesNotScore()
        {
            // Above the ring and armed, which is a shot waiting to be completed. Putting the
            // ball back drops it straight down past the ring's plane in one jump.
            ball.Body.position = new Vector3(0f, PlaneHeight + 0.5f, 0f);
            ball.Body.useGravity = false;
            ball.Body.linearVelocity = Vector3.zero;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, score.Score, "nothing has been scored yet");

            reset.Restore();

            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();

            Assert.Less(ball.Body.position.y, PlaneHeight, "the ball really did end up below the ring");
            Assert.AreEqual(0, score.Score, "putting the ball back is not a basket");
        }

        [UnityTest]
        public IEnumerator ResetKeepsTheScoreAndClearingItIsSeparate()
        {
            score.AddBasket();
            score.AddBasket();
            Assert.AreEqual(4, score.Score);

            reset.Restore();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(4, score.Score, "a reset puts the ball back; it does not wipe what was scored");

            score.ResetScore();
            Assert.AreEqual(0, score.Score, "clearing the score is its own action");
        }

        [UnityTest]
        public IEnumerator AShotStillScoresAfterAReset()
        {
            reset.Restore();
            yield return new WaitForFixedUpdate();

            // The reset forgets every pending shot, so this proves it forgot the path
            // rather than switching the sensor off.
            ball.Body.position = new Vector3(0f, PlaneHeight + 0.8f, 0f);
            ball.Body.linearVelocity = Vector3.zero;
            ball.Body.useGravity = true;

            for (int i = 0; i < 120; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, score.Baskets, "the hoop still works after a reset");
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        private BasketSensor BuildRing()
        {
            GameObject ring = Spawn("Test Ring");
            ring.transform.position = new Vector3(0f, PlaneHeight, 0f);

            for (int i = 0; i < RimSegments; i++)
            {
                CourtLayout.RingSegment segment = CourtLayout.Segment(i, RimSegments, Centreline, RimTubeRadius);
                var go = new GameObject("Segment " + i);
                go.transform.SetParent(ring.transform, false);
                go.transform.localPosition = segment.Position;
                go.transform.localRotation = segment.Rotation;

                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = RimTubeRadius;
                capsule.height = segment.Height;
            }

            BasketSensor built = ring.AddComponent<BasketSensor>();
            built.PassRadius = RimInnerRadius;
            return built;
        }

        private Ball BuildBall(Vector3 position)
        {
            GameObject go = Spawn("Test Ball");
            go.transform.position = position;
            go.AddComponent<SphereCollider>().radius = BallRadius;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.62f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            return go.AddComponent<Ball>();
        }

        private HandGrabber BuildHand(Vector3 position)
        {
            GameObject go = Spawn("Test Hand");
            go.SetActive(false);
            go.transform.position = position;

            HandGrabber grabber = go.AddComponent<HandGrabber>();
            grabber.Hand = Hand.Right;
            grabber.Settings = settings;
            go.SetActive(true);
            return grabber;
        }
    }
}
