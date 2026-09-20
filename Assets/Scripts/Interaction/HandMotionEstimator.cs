using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Keeps a bounded history of recent hand poses and reports the average linear and
    /// angular velocity across a time window. Poses are added where the rig refreshes its
    /// anchors; a throw reads the result once, at release.
    /// </summary>
    public sealed class HandMotionEstimator
    {
        private struct Sample
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Time;
        }

        private readonly Sample[] samples;
        private int count;
        private int next;

        public HandMotionEstimator(int capacity)
        {
            samples = new Sample[Mathf.Max(2, capacity)];
        }

        public int Capacity => samples.Length;

        public int Count => count;

        public void Clear()
        {
            count = 0;
            next = 0;
        }

        public void Add(Vector3 position, Quaternion rotation, float time)
        {
            samples[next] = new Sample { Position = position, Rotation = rotation, Time = time };
            next = (next + 1) % samples.Length;
            if (count < samples.Length)
                count++;
        }

        /// <summary>
        /// Averages motion over the newest samples no older than <paramref name="window"/>
        /// seconds. Always spans at least two samples, so a window shorter than one frame
        /// still reports motion. False when there is nothing to measure.
        /// </summary>
        public bool TryGetVelocity(float window, out Vector3 linear, out Vector3 angular)
        {
            linear = Vector3.zero;
            angular = Vector3.zero;
            if (count < 2)
                return false;

            Sample newest = At(count - 1);
            int oldestIndex = count - 2;
            for (int i = count - 2; i >= 0; i--)
            {
                if (newest.Time - At(i).Time > window)
                    break;
                oldestIndex = i;
            }

            Sample oldest = At(oldestIndex);
            float elapsed = newest.Time - oldest.Time;
            if (elapsed <= 1e-5f)
                return false;

            linear = (newest.Position - oldest.Position) / elapsed;
            angular = AngularVelocity(oldest.Rotation, newest.Rotation, elapsed);
            return true;
        }

        /// <summary>
        /// World-space angular velocity, in radians per second, that turns
        /// <paramref name="from"/> into <paramref name="to"/> over the given time.
        /// </summary>
        public static Vector3 AngularVelocity(Quaternion from, Quaternion to, float deltaTime)
        {
            if (deltaTime <= 0f)
                return Vector3.zero;

            Quaternion difference = Quaternion.Normalize(to * Quaternion.Inverse(from));
            difference.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || axis.sqrMagnitude < 1e-8f)
                return Vector3.zero;

            if (angle > 180f)
                angle -= 360f;

            return axis.normalized * (angle * Mathf.Deg2Rad / deltaTime);
        }

        // Logical index 0 is the oldest sample still kept.
        private Sample At(int index) => samples[(next - count + index + (2 * samples.Length)) % samples.Length];
    }
}
