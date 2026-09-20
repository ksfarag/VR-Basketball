using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// Scoring against real physics: a shot that goes in, a shot that hits the metal and
    /// stays out, and a ball the game carried rather than the player threw.
    ///
    /// The ring is built here from <see cref="CourtLayout"/> rather than loaded from the
    /// scene, so that a broken scene reference fails the scene check instead of these.
    /// </summary>
    public class BasketSensorTests
    {
        private const float BallRadius = 0.1195f;
        private const float RimInnerRadius = 0.2286f;
        private const float RimTubeRadius = 0.01f;
        private const int RimSegments = 24;
        private const float RimHeight = 3.048f;
        private const float Centreline = RimInnerRadius + RimTubeRadius;
        private const float PlaneHeight = RimHeight - RimTubeRadius;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private BasketSensor sensor;
        private ScoreKeeper score;

        [SetUp]
        public void SetUp()
        {
            BuildFloor();
            sensor = BuildRing();

            var holder = new GameObject("Score");
            holder.SetActive(false);
            score = holder.AddComponent<ScoreKeeper>();
            score.Sensors = new[] { sensor };
            score.PointsPerBasket = 2;
            holder.SetActive(true);
            spawned.Add(holder);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.Destroy(go);

            spawned.Clear();
        }

        [UnityTest]
        public IEnumerator AShotThroughTheHoopScoresOnceAndStaysScored()
        {
            Ball ball = BuildBall(new Vector3(0f, RimHeight + 0.8f, 0f));

            // Long enough for the ball to drop three metres to the floor and bounce about
            // under the ring, which is where a second count would come from.
            for (int i = 0; i < 250; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, score.Baskets, "one ball through the hoop is one basket");
            Assert.AreEqual(2, score.Score, "and it is worth what the keeper says it is");
            Assert.Less(ball.Body.position.y, 1f, "the ball really did go through and reach the floor");
        }

        [UnityTest]
        public IEnumerator AShotThatHitsTheRimAndStaysOutScoresNothing()
        {
            // Onto the metal, two centimetres out from the crown of the tube, dropped from
            // the height the court physics check establishes is thrown clear of the ring.
            BuildBall(new Vector3(Centreline + 0.02f, RimHeight + 0.6f, 0f));

            for (int i = 0; i < 200; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, score.Score, "a ball that bounced off the ring never went through it");
        }

        [UnityTest]
        public IEnumerator ABallCarriedThroughTheRingByTheGameScoresNothing()
        {
            // What a recall does: the ball is flown to a destination under the game's own
            // control, passing through whatever is in the way. Here the path runs straight
            // down through the hole, which is the way a recall could award a free basket.
            Ball ball = BuildBall(new Vector3(0f, RimHeight + 0.8f, 0f));
            ball.InTransit = true;
            ball.Body.useGravity = false;

            for (int i = 0; i < 60; i++)
            {
                ball.Body.position = ball.Body.position + Vector3.down * 0.06f;
                yield return new WaitForFixedUpdate();
            }

            Assert.Less(ball.Body.position.y, PlaneHeight, "the ball was carried down past the ring");
            Assert.AreEqual(0, score.Score, "a ball the game moved has not been shot through the hoop");
        }

        [UnityTest]
        public IEnumerator TwoShotsScoreTwice()
        {
            BuildBall(new Vector3(0f, RimHeight + 0.8f, 0f));

            for (int i = 0; i < 120; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, score.Baskets, "the first shot went in");

            BuildBall(new Vector3(0f, RimHeight + 0.8f, 0f));

            for (int i = 0; i < 120; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(2, score.Baskets, "and so did the second");
            Assert.AreEqual(4, score.Score);
        }

        private BasketSensor BuildRing()
        {
            var ring = new GameObject("Test Ring");
            ring.transform.position = new Vector3(0f, PlaneHeight, 0f);
            spawned.Add(ring);

            var surface = new PhysicsMaterial("Ring")
            {
                bounciness = 0.55f,
                dynamicFriction = 0.5f,
                staticFriction = 0.5f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Multiply
            };

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
                capsule.sharedMaterial = surface;
            }

            BasketSensor built = ring.AddComponent<BasketSensor>();
            built.PassRadius = RimInnerRadius;
            return built;
        }

        private void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
            spawned.Add(floor);
        }

        private Ball BuildBall(Vector3 position)
        {
            var go = new GameObject("Test Ball");
            go.transform.position = position;
            spawned.Add(go);

            go.AddComponent<SphereCollider>().radius = BallRadius;

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = 0.62f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            return go.AddComponent<Ball>();
        }
    }
}
