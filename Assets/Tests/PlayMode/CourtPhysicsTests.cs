using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// Runs the court's collision behaviour against real physics: that the floor gives the
    /// ball its bounce back, that a hard shot cannot pass through anything, and that the
    /// ring is solid metal rather than a hole with a picture of a ring around it.
    ///
    /// The court is built here from <see cref="CourtLayout"/> rather than loaded from the
    /// scene, so a broken scene reference fails the scene check instead of these. The
    /// surface values mirror the assets in Assets/Physics.
    /// </summary>
    public class CourtPhysicsTests
    {
        private const float BallRadius = 0.1195f;
        private const float BallMass = 0.62f;
        private const float BallBounciness = 0.85f;
        private const float FloorBounciness = 1f;
        private const float RingBounciness = 0.55f;

        private const float RimInnerRadius = 0.2286f;
        private const float RimTubeRadius = 0.01f;
        private const int RimSegments = 24;
        private const float RimHeight = 3.048f;
        private const float Centreline = RimInnerRadius + RimTubeRadius;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null)
                    Object.Destroy(go);
            spawned.Clear();
        }

        [UnityTest]
        public IEnumerator ABallDroppedOnTheFloorBouncesMostOfTheWayBack()
        {
            BuildFloor();
            Rigidbody ball = BuildBall(new Vector3(0f, 1.8f + BallRadius, 0f));

            bool bounced = false;
            float apex = 0f;
            for (int i = 0; i < 250; i++)
            {
                yield return new WaitForFixedUpdate();

                if (!bounced)
                {
                    bounced = ball.linearVelocity.y > 0.1f;
                    continue;
                }

                apex = Mathf.Max(apex, ball.position.y - BallRadius);
                if (ball.linearVelocity.y < 0f)
                    break;
            }

            Assert.IsTrue(bounced, "the ball never left the floor; a bounce threshold above the landing speed kills restitution entirely");

            // Restitution 0.85 returns the square of that in height, so 1.8 m comes back to
            // about 1.3 m. The band is wide because the solver loses a little at contact.
            Assert.That(apex, Is.InRange(0.9f, 1.6f),
                $"a ball dropped from 1.8 m should come back to roughly 1.3 m, not {apex:F2} m");
        }

        [UnityTest]
        public IEnumerator AVeryFastBallDoesNotPassThroughTheFloor()
        {
            BuildFloor();
            Rigidbody ball = BuildBall(new Vector3(0f, 3f, 0f));
            // 60 m/s covers 1.2 m in a physics step, six times the floor's thickness.
            // Nothing but continuous detection catches that.
            ball.linearVelocity = new Vector3(0f, -60f, 0f);

            float lowest = float.PositiveInfinity;
            for (int i = 0; i < 120; i++)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, ball.position.y);
            }

            Assert.Greater(lowest, -BallRadius, $"the ball reached y={lowest:F3} and went through the floor");
        }

        [UnityTest]
        public IEnumerator ABallDroppedThroughTheRingFallsCleanThrough()
        {
            BuildFloor();
            BuildRing();
            Rigidbody ball = BuildBall(new Vector3(0f, RimHeight + 0.6f, 0f));

            float lowest = float.PositiveInfinity;
            float widest = 0f;
            for (int i = 0; i < 90; i++)
            {
                yield return new WaitForFixedUpdate();
                lowest = Mathf.Min(lowest, ball.position.y);
                widest = Mathf.Max(widest, new Vector2(ball.position.x, ball.position.z).magnitude);
            }

            Assert.Less(lowest, 0.5f, $"a ball dropped down the middle should fall through to the floor, but it stopped at y={lowest:F3}");
            Assert.Less(widest, 0.15f, $"and should not be thrown sideways on the way, but it reached {widest:F3} m off centre");
        }

        [UnityTest]
        public IEnumerator ABallDroppedOnTheRingIsTurnedAside()
        {
            BuildFloor();
            BuildRing();

            // Onto the metal itself, not the hole, and two centimetres out from the tube's
            // crown. Dropped dead on the crown the ball balances there instead of rolling
            // off, which proves the ring is solid but leaves nothing to measure.
            const float Start = Centreline + 0.02f;
            Rigidbody ball = BuildBall(new Vector3(Start, RimHeight + 0.6f, 0f));

            for (int i = 0; i < 90; i++)
                yield return new WaitForFixedUpdate();

            float ended = new Vector2(ball.position.x, ball.position.z).magnitude;
            Assert.Greater(ended - Start, 0.03f,
                $"the ring should throw the ball outwards, but it fell at {ended:F3} m from the centre having started at {Start:F3} m");
        }

        [UnityTest]
        public IEnumerator RollingResistanceBringsARollingBallToRestSooner()
        {
            BuildFloor();

            Rigidbody free = BuildBall(new Vector3(0f, BallRadius, -2f));
            Rigidbody braked = BuildBall(new Vector3(0f, BallRadius, 2f), rollingResistance: true);
            Roll(free);
            Roll(braked);

            for (int i = 0; i < 150; i++)
                yield return new WaitForFixedUpdate();

            float freeSpeed = free.linearVelocity.magnitude;
            float brakedSpeed = braked.linearVelocity.magnitude;

            Assert.Less(brakedSpeed, freeSpeed * 0.85f,
                $"rolling resistance should slow the ball noticeably: {brakedSpeed:F2} m/s against {freeSpeed:F2} m/s untouched");
            Assert.Greater(Vector3.Dot(braked.linearVelocity, Vector3.right), -0.01f,
                "and must never drive the ball backwards");
        }

        private static void Roll(Rigidbody ball)
        {
            const float Speed = 4f;
            ball.linearVelocity = new Vector3(Speed, 0f, 0f);
            // Rolling, not skidding: the contact point is stationary against the floor.
            ball.angularVelocity = new Vector3(0f, 0f, -Speed / BallRadius);
        }

        private void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test Floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(40f, 0.2f, 40f);
            floor.GetComponent<BoxCollider>().sharedMaterial = Surface("Court", FloorBounciness, 0.6f);
            spawned.Add(floor);
        }

        private void BuildRing()
        {
            var ring = new GameObject("Test Ring");
            ring.transform.position = new Vector3(0f, RimHeight - RimTubeRadius, 0f);
            spawned.Add(ring);

            PhysicsMaterial surface = Surface("Ring", RingBounciness, 0.5f);

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
        }

        private Rigidbody BuildBall(Vector3 position, bool rollingResistance = false)
        {
            var go = new GameObject("Test Ball");
            go.transform.position = position;
            spawned.Add(go);

            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = BallRadius;
            sphere.sharedMaterial = Surface("Ball", BallBounciness, 0.6f);

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = BallMass;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (rollingResistance)
                go.AddComponent<RollingResistance>();

            return body;
        }

        private static PhysicsMaterial Surface(string name, float bounciness, float friction)
        {
            return new PhysicsMaterial(name)
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Multiply
            };
        }
    }
}
