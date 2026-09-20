using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Keeps a bounded history of recent hand poses and reports the linear and angular
    /// velocity of the swing they describe. Poses are added where the rig refreshes its
    /// anchors; a throw reads the result once, at release.
    ///
    /// The estimate fits a constant-acceleration path through every pose in the window
    /// rather than measuring the two ends, so the whole arm trajectory decides the throw
    /// and a snap of the wrist in the last frames cannot take it over. A release bias
    /// chooses where on that fitted path the throw is read.
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
        /// Fits the swing described by every pose no older than <paramref name="window"/>
        /// seconds and reports its velocity. <paramref name="releaseBias"/> chooses where
        /// the fitted path is read: 0 gives the average velocity across the window, so a
        /// throw follows the whole arm and nothing else; 1 gives the velocity the path
        /// reaches at the instant of release, which keeps a still-accelerating swing at
        /// full strength but follows the end of the motion more closely. Always spans at
        /// least two samples, so a window shorter than one frame still reports motion.
        /// False when there is nothing to measure.
        /// </summary>
        public bool TryGetVelocity(float window, float releaseBias, out Vector3 linear, out Vector3 angular)
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

            float span = newest.Time - At(oldestIndex).Time;
            if (span <= 1e-5f)
                return false;

            // Time is measured as -1 at the oldest kept pose rising to 0 at release. That
            // keeps the fit equally well conditioned whatever the window, and puts the
            // release itself at zero so reading the path there costs nothing.
            double m0 = 0d, m1 = 0d, m2 = 0d, m3 = 0d, m4 = 0d;
            Vector3 p0 = Vector3.zero, p1 = Vector3.zero, p2 = Vector3.zero;
            Vector3 t0 = Vector3.zero, t1 = Vector3.zero, t2 = Vector3.zero;

            Vector3 turn = Vector3.zero;
            Quaternion previous = At(oldestIndex).Rotation;
            int used = count - oldestIndex;

            // Positions are fitted relative to the release, so a hand far from the world
            // origin keeps its millimetres of travel instead of losing them to the
            // exponent of a large coordinate. A constant offset cannot change a velocity.
            Vector3 origin = newest.Position;

            for (int i = oldestIndex; i < count; i++)
            {
                Sample sample = At(i);

                // Turning is fitted exactly the way travelling is, by accumulating each
                // step of rotation into a running vector so the path stays continuous
                // even past half a turn.
                turn += AngularVelocity(previous, sample.Rotation, 1f);
                previous = sample.Rotation;

                double u = (sample.Time - newest.Time) / span;
                double uu = u * u;
                m0 += 1d;
                m1 += u;
                m2 += uu;
                m3 += uu * u;
                m4 += uu * uu;

                float fu = (float)u;
                float fuu = fu * fu;
                Vector3 travelled = sample.Position - origin;
                p0 += travelled;
                p1 += travelled * fu;
                p2 += travelled * fuu;
                t0 += turn;
                t1 += turn * fu;
                t2 += turn * fuu;
            }

            // A curve needs three distinct poses. With fewer, or with poses too bunched
            // in time to define one, a straight fit still reports the swing average.
            double det = (m0 * ((m2 * m4) - (m3 * m3)))
                       - (m1 * ((m1 * m4) - (m3 * m2)))
                       + (m2 * ((m1 * m3) - (m2 * m2)));

            if (used < 3 || System.Math.Abs(det) < 1e-6d * used * used * used)
            {
                linear = StraightFit(m0, m1, m2, p0, p1, span);
                angular = StraightFit(m0, m1, m2, t0, t1, span);
                return true;
            }

            float bias = Mathf.Clamp01(releaseBias);
            linear = CurvedFit(m0, m1, m2, m3, m4, det, p0, p1, p2, span, bias);
            angular = CurvedFit(m0, m1, m2, m3, m4, det, t0, t1, t2, span, bias);
            return true;
        }

        /// <summary>
        /// Velocity of the least-squares path a + bu + cu^2 through the window, read at
        /// the release for a bias of 1 and as the window average for a bias of 0.
        /// </summary>
        private static Vector3 CurvedFit(
            double m0, double m1, double m2, double m3, double m4, double det,
            Vector3 s0, Vector3 s1, Vector3 s2, float span, float bias)
        {
            // Rows of the inverted moment matrix that produce b and c. The constant term
            // is the position at release, which a velocity does not need.
            double b0 = ((m2 * m3) - (m1 * m4)) / det;
            double b1 = ((m0 * m4) - (m2 * m2)) / det;
            double b2 = ((m1 * m2) - (m0 * m3)) / det;
            double c0 = ((m1 * m3) - (m2 * m2)) / det;
            double c1 = ((m1 * m2) - (m0 * m3)) / det;
            double c2 = ((m0 * m2) - (m1 * m1)) / det;

            Vector3 b = (s0 * (float)b0) + (s1 * (float)b1) + (s2 * (float)b2);
            Vector3 c = (s0 * (float)c0) + (s1 * (float)c1) + (s2 * (float)c2);

            // b is the velocity at release and b - c the average across the window, so
            // the bias slides between them along the same fitted path.
            return (b - (c * (1f - bias))) / span;
        }

        /// <summary>Slope of the least-squares straight path through the window.</summary>
        private static Vector3 StraightFit(double m0, double m1, double m2, Vector3 s0, Vector3 s1, float span)
        {
            double spread = m2 - ((m1 * m1) / m0);
            if (System.Math.Abs(spread) < 1e-12d)
                return Vector3.zero;

            Vector3 numerator = s1 - (s0 * (float)(m1 / m0));
            return numerator / (float)(spread * span);
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
