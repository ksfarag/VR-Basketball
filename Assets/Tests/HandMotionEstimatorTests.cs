using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VRBasketball.Tests
{
    /// <summary>
    /// Covers the release-velocity estimate on its own, without XR or physics, so the
    /// trajectory fit, its window, and the release bias can be checked with exact numbers.
    /// </summary>
    public class HandMotionEstimatorTests
    {
        // The whole swing is sampled at 0.01 s, and windows are asked for just off a
        // sample boundary so each spans a whole number of steps rather than depending on
        // a float comparison at the edge.
        private const float Step = 0.01f;
        private const float Window = 0.205f;

        [Test]
        public void NothingToMeasureWithFewerThanTwoSamples()
        {
            var estimator = new HandMotionEstimator(8);
            Assert.IsFalse(estimator.TryGetVelocity(0.1f, 0.5f, out _, out _), "an empty estimator has no motion");

            estimator.Add(Vector3.zero, Quaternion.identity, 0f);
            Assert.IsFalse(estimator.TryGetVelocity(0.1f, 0.5f, out _, out _), "one pose is not motion");
        }

        [Test]
        public void ReportsSteadyLinearVelocity()
        {
            var estimator = new HandMotionEstimator(16);
            for (int i = 0; i <= 8; i++)
                estimator.Add(new Vector3(0f, 0f, i * 0.05f), Quaternion.identity, i * Step);

            // A hand at a steady speed has no curve to read, so every bias agrees.
            foreach (float bias in new[] { 0f, 0.5f, 1f })
            {
                Assert.IsTrue(estimator.TryGetVelocity(0.1f, bias, out Vector3 linear, out Vector3 angular));
                Assert.That(linear.z, Is.EqualTo(5f).Within(0.001f), "0.05 m every 0.01 s is 5 m/s");
                Assert.That(angular.magnitude, Is.EqualTo(0f).Within(0.001f), "a hand that does not turn has no spin");
            }
        }

        [Test]
        public void IgnoresMotionOlderThanTheWindow()
        {
            var estimator = new HandMotionEstimator(16);
            for (int i = 0; i <= 9; i++)
            {
                // A fast swing that slows to 1 m/s for the last half.
                float z = i <= 4 ? i * 0.1f : 0.4f + ((i - 4) * 0.01f);
                estimator.Add(new Vector3(0f, 0f, z), Quaternion.identity, i * Step);
            }

            Assert.IsTrue(estimator.TryGetVelocity(0.03f, 0.5f, out Vector3 linear, out _));
            Assert.That(linear.z, Is.EqualTo(1f).Within(0.001f), "only the last 0.03 s counts");
        }

        [Test]
        public void KeepsOnlyTheNewestSamplesWhenFull()
        {
            var estimator = new HandMotionEstimator(4);
            for (int i = 0; i <= 19; i++)
                estimator.Add(new Vector3(0f, 0f, i * 0.02f), Quaternion.identity, i * Step);

            Assert.AreEqual(4, estimator.Capacity, "the buffer stays bounded");
            Assert.AreEqual(4, estimator.Count);
            Assert.IsTrue(estimator.TryGetVelocity(10f, 0.5f, out Vector3 linear, out _));
            Assert.That(linear.z, Is.EqualTo(2f).Within(0.001f), "a long window cannot reach past the kept samples");
        }

        [Test]
        public void ReportsAngularVelocityAboutTheTurnAxis()
        {
            var estimator = new HandMotionEstimator(8);
            for (int i = 0; i <= 4; i++)
                estimator.Add(Vector3.zero, Quaternion.AngleAxis(i * 3f, Vector3.up), i * Step);

            Assert.IsTrue(estimator.TryGetVelocity(0.1f, 0.5f, out Vector3 linear, out Vector3 angular));
            Assert.That(linear.magnitude, Is.EqualTo(0f).Within(0.001f));
            Assert.That(angular.magnitude, Is.EqualTo(3f * Mathf.Deg2Rad / Step).Within(0.01f), "3 degrees every 0.01 s is 300 deg/s");
            Assert.That(Vector3.Dot(angular.normalized, Vector3.up), Is.EqualTo(1f).Within(0.001f), "the turn was about up");
        }

        [Test]
        public void FollowsAnAcceleratingSwingToItsReleaseSpeed()
        {
            // With x(t) = a t^2 / 2 the hand is doing a * T when it lets go. The fit is
            // exact for that path, so the bias picks a known point on it: 1 reports the
            // release speed itself, 0 the average across the window, which is lower by
            // exactly a * w / 2. This is the knob that trades a steady throw against a
            // responsive one, and it is why a longer window no longer costs throw speed.
            const float acceleration = 20f;
            var estimator = new HandMotionEstimator(64);
            for (int i = 0; i <= 50; i++)
            {
                float t = i * Step;
                estimator.Add(new Vector3(0f, 0f, 0.5f * acceleration * t * t), Quaternion.identity, t);
            }

            Assert.IsTrue(estimator.TryGetVelocity(Window, 1f, out Vector3 atRelease, out _));
            Assert.IsTrue(estimator.TryGetVelocity(Window, 0f, out Vector3 acrossSwing, out _));

            Assert.That(atRelease.z, Is.EqualTo(acceleration * 0.5f).Within(0.05f), "a bias of 1 reports the speed the hand had when it let go");
            Assert.That(acrossSwing.z, Is.EqualTo((acceleration * 0.5f) - (acceleration * 0.2f / 2f)).Within(0.05f), "a bias of 0 reports the average over the 0.2 s window");
            Assert.Less(acrossSwing.z, atRelease.z, "averaging the swing gives up the speed gained at the end of it");
        }

        [Test]
        public void AFlickAtTheEndDoesNotSteerTheThrow()
        {
            // The reported problem: a hand that snaps as it lets go used to send the ball
            // wherever it snapped. The swing runs straight ahead the whole time, and only
            // the last 0.03 s drops away. The throw should still go where the arm went.
            List<(float time, Vector3 position)> path = FlickedSwing();
            HandMotionEstimator estimator = Replay(path);

            Assert.IsTrue(estimator.TryGetVelocity(Window, 0.5f, out Vector3 linear, out _));

            float fitted = Vector3.Angle(linear, Vector3.forward);
            float endpoints = Vector3.Angle(EndpointVelocity(path, 0.08f), Vector3.forward);

            Assert.Greater(endpoints, 10f, "the measure this replaced followed the flick by more than 10 degrees");
            Assert.Less(fitted, endpoints * 0.7f, "reading the whole swing must pull the throw back toward the arm");
            Assert.Less(fitted, 7f, "the throw should still be close to the way the arm swung");
            Assert.That(linear.z, Is.EqualTo(6f).Within(0.05f), "and the swing must keep its speed while the flick is rejected");
        }

        [Test]
        public void ReleaseBiasTradesTheSwingAgainstTheLastInstant()
        {
            // The same flicked swing, read at three points on the fitted path. This is
            // the knob to turn if the headset says throws feel either wayward or weak.
            HandMotionEstimator estimator = Replay(FlickedSwing());

            Assert.IsTrue(estimator.TryGetVelocity(Window, 0f, out Vector3 acrossSwing, out _));
            Assert.IsTrue(estimator.TryGetVelocity(Window, 0.5f, out Vector3 middle, out _));
            Assert.IsTrue(estimator.TryGetVelocity(Window, 1f, out Vector3 atRelease, out _));

            // The flick is downward, so how much of it survives shows in the drop.
            Assert.Less(Mathf.Abs(acrossSwing.y), Mathf.Abs(middle.y), "averaging the whole swing keeps the least of the flick");
            Assert.Less(Mathf.Abs(middle.y), Mathf.Abs(atRelease.y), "reading at the release keeps the most of it");
            Assert.Less(Mathf.Abs(acrossSwing.y), 0.5f, "a bias of 0 should barely notice a flick this short");
        }

        [Test]
        public void OneStraySampleBarelyMovesTheThrow()
        {
            // Tracking noise lands on single poses. The measure this replaced read two
            // ends of a window, so a stray pose on either end moved the whole throw; a
            // fit through every pose in the window is far harder to shift.
            List<(float time, Vector3 position)> clean = StraightSwing();
            List<(float time, Vector3 position)> stray = StraightSwing();
            (float time, Vector3 position) last = stray[stray.Count - 1];
            stray[stray.Count - 1] = (last.time, last.position + (Vector3.up * 0.01f));

            Assert.IsTrue(Replay(clean).TryGetVelocity(Window, 0.5f, out Vector3 before, out _));
            Assert.IsTrue(Replay(stray).TryGetVelocity(Window, 0.5f, out Vector3 after, out _));

            float fitted = Vector3.Angle(before, after);
            float endpoints = Vector3.Angle(EndpointVelocity(clean, 0.08f), EndpointVelocity(stray, 0.08f));

            Assert.Greater(endpoints, 1f, "a centimetre of noise moved the old measure by over a degree");
            Assert.Less(fitted, endpoints * 0.5f, "the fit should be shifted far less by the same stray pose");
        }

        [Test]
        public void ClearingDiscardsTheHistory()
        {
            var estimator = new HandMotionEstimator(8);
            estimator.Add(Vector3.zero, Quaternion.identity, 0f);
            estimator.Add(Vector3.forward, Quaternion.identity, Step);
            estimator.Clear();

            Assert.AreEqual(0, estimator.Count);
            Assert.IsFalse(estimator.TryGetVelocity(0.1f, 0.5f, out _, out _), "a cleared hand reports no motion");
        }

        [Test]
        public void IdenticalPosesHaveNoAngularVelocity()
        {
            Vector3 angular = HandMotionEstimator.AngularVelocity(Quaternion.identity, Quaternion.identity, Step);
            Assert.That(angular.magnitude, Is.EqualTo(0f).Within(0.0001f));
        }

        // 6 m/s straight ahead for 0.30 s.
        private static List<(float time, Vector3 position)> StraightSwing()
        {
            var path = new List<(float, Vector3)>();
            for (int i = 0; i <= 30; i++)
            {
                float t = i * Step;
                path.Add((t, new Vector3(0f, 0f, 6f * t)));
            }

            return path;
        }

        // The same swing, with the hand also dropping at 3 m/s for the last 0.03 s.
        private static List<(float time, Vector3 position)> FlickedSwing()
        {
            List<(float time, Vector3 position)> path = StraightSwing();
            for (int i = 0; i < path.Count; i++)
            {
                float drop = -3f * Mathf.Max(0f, path[i].time - 0.27f);
                path[i] = (path[i].time, path[i].position + (Vector3.up * drop));
            }

            return path;
        }

        private static HandMotionEstimator Replay(List<(float time, Vector3 position)> path)
        {
            var estimator = new HandMotionEstimator(64);
            foreach ((float time, Vector3 position) in path)
                estimator.Add(position, Quaternion.identity, time);

            return estimator;
        }

        // The measure this replaced: the two ends of a short window, and nothing between.
        private static Vector3 EndpointVelocity(List<(float time, Vector3 position)> path, float window)
        {
            (float time, Vector3 position) newest = path[path.Count - 1];
            int oldest = path.Count - 2;
            for (int i = path.Count - 2; i >= 0; i--)
            {
                if (newest.time - path[i].time > window)
                    break;

                oldest = i;
            }

            return (newest.position - path[oldest].position) / (newest.time - path[oldest].time);
        }
    }
}
