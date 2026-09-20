using NUnit.Framework;
using UnityEngine;

namespace VRBasketball.Tests
{
    /// <summary>
    /// Covers the release-velocity estimate on its own, without XR or physics, so the
    /// smoothing window and sample bound can be checked with exact numbers.
    /// </summary>
    public class HandMotionEstimatorTests
    {
        [Test]
        public void NothingToMeasureWithFewerThanTwoSamples()
        {
            var estimator = new HandMotionEstimator(8);
            Assert.IsFalse(estimator.TryGetVelocity(0.1f, out _, out _), "an empty estimator has no motion");

            estimator.Add(Vector3.zero, Quaternion.identity, 0f);
            Assert.IsFalse(estimator.TryGetVelocity(0.1f, out _, out _), "one pose is not motion");
        }

        [Test]
        public void ReportsSteadyLinearVelocity()
        {
            var estimator = new HandMotionEstimator(16);
            for (int i = 0; i <= 8; i++)
                estimator.Add(new Vector3(0f, 0f, i * 0.05f), Quaternion.identity, i * 0.01f);

            Assert.IsTrue(estimator.TryGetVelocity(0.1f, out Vector3 linear, out Vector3 angular));
            Assert.That(linear.z, Is.EqualTo(5f).Within(0.001f), "0.05 m every 0.01 s is 5 m/s");
            Assert.That(angular.magnitude, Is.EqualTo(0f).Within(0.001f), "a hand that does not turn has no spin");
        }

        [Test]
        public void IgnoresMotionOlderThanTheWindow()
        {
            var estimator = new HandMotionEstimator(16);
            for (int i = 0; i <= 9; i++)
            {
                // A fast swing that slows to 1 m/s for the last half.
                float z = i <= 4 ? i * 0.1f : 0.4f + ((i - 4) * 0.01f);
                estimator.Add(new Vector3(0f, 0f, z), Quaternion.identity, i * 0.01f);
            }

            Assert.IsTrue(estimator.TryGetVelocity(0.03f, out Vector3 linear, out _));
            Assert.That(linear.z, Is.EqualTo(1f).Within(0.001f), "only the last 0.03 s counts");
        }

        [Test]
        public void KeepsOnlyTheNewestSamplesWhenFull()
        {
            var estimator = new HandMotionEstimator(4);
            for (int i = 0; i <= 19; i++)
                estimator.Add(new Vector3(0f, 0f, i * 0.02f), Quaternion.identity, i * 0.01f);

            Assert.AreEqual(4, estimator.Capacity, "the buffer stays bounded");
            Assert.AreEqual(4, estimator.Count);
            Assert.IsTrue(estimator.TryGetVelocity(10f, out Vector3 linear, out _));
            Assert.That(linear.z, Is.EqualTo(2f).Within(0.001f), "a long window cannot reach past the kept samples");
        }

        [Test]
        public void ReportsAngularVelocityAboutTheTurnAxis()
        {
            var estimator = new HandMotionEstimator(8);
            for (int i = 0; i <= 4; i++)
                estimator.Add(Vector3.zero, Quaternion.AngleAxis(i * 3f, Vector3.up), i * 0.01f);

            Assert.IsTrue(estimator.TryGetVelocity(0.1f, out Vector3 linear, out Vector3 angular));
            Assert.That(linear.magnitude, Is.EqualTo(0f).Within(0.001f));
            Assert.That(angular.magnitude, Is.EqualTo(12f * Mathf.Deg2Rad / 0.04f).Within(0.01f), "12 degrees over 0.04 s");
            Assert.That(Vector3.Dot(angular.normalized, Vector3.up), Is.EqualTo(1f).Within(0.001f), "the turn was about up");
        }

        [Test]
        public void ShorterWindowTracksAnAcceleratingHandMoreClosely()
        {
            // With x(t) = a t^2 / 2 the true speed at the last sample is a * T, and a
            // window of w averages that down by exactly a * w / 2. This is the knob that
            // trades a steady throw against a responsive one.
            const float acceleration = 20f;
            var estimator = new HandMotionEstimator(64);
            for (int i = 0; i <= 50; i++)
            {
                float t = i * 0.01f;
                estimator.Add(new Vector3(0f, 0f, 0.5f * acceleration * t * t), Quaternion.identity, t);
            }

            // The windows sit just off a sample boundary, so each spans a whole number of
            // 0.01 s steps rather than depending on float comparison at the edge.
            float instantaneous = acceleration * 0.5f;
            Assert.IsTrue(estimator.TryGetVelocity(0.045f, out Vector3 tight, out _));
            Assert.IsTrue(estimator.TryGetVelocity(0.205f, out Vector3 loose, out _));

            Assert.That(tight.z, Is.EqualTo(instantaneous - (acceleration * 0.04f / 2f)).Within(0.05f), "a 0.04 s span loses 0.4 m/s here");
            Assert.That(loose.z, Is.EqualTo(instantaneous - (acceleration * 0.20f / 2f)).Within(0.05f), "a 0.20 s span loses 2.0 m/s here");
            Assert.Less(loose.z, tight.z, "a longer window smooths more of the acceleration away");
        }

        [Test]
        public void ClearingDiscardsTheHistory()
        {
            var estimator = new HandMotionEstimator(8);
            estimator.Add(Vector3.zero, Quaternion.identity, 0f);
            estimator.Add(Vector3.forward, Quaternion.identity, 0.01f);
            estimator.Clear();

            Assert.AreEqual(0, estimator.Count);
            Assert.IsFalse(estimator.TryGetVelocity(0.1f, out _, out _), "a cleared hand reports no motion");
        }

        [Test]
        public void IdenticalPosesHaveNoAngularVelocity()
        {
            Vector3 angular = HandMotionEstimator.AngularVelocity(Quaternion.identity, Quaternion.identity, 0.01f);
            Assert.That(angular.magnitude, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
