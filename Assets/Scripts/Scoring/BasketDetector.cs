using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// One ball's progress through one hoop. Plain logic with no scene, physics, or input
    /// dependency: it is fed the ball's position in the hoop's local space, step by step,
    /// and reports the step on which the ball completed a basket.
    ///
    /// A basket is a downward crossing of the ring's plane inside the hole. That is a
    /// property of the path between two samples, not of any single position, which is why
    /// nothing here asks whether the ball is touching or overlapping anything: a trigger
    /// volume answers "is it inside", and a ball that lingers, jitters, or rolls around in
    /// one keeps answering yes. A crossing happens once.
    /// </summary>
    public sealed class BasketDetector
    {
        private Vector3 previous;
        private bool hasPrevious;
        private bool armed;

        /// <summary>
        /// Whether a downward crossing would currently count. False until the ball has
        /// been clearly above the ring, and false again from the moment it scores until
        /// it goes back up there.
        /// </summary>
        public bool Armed => armed;

        /// <summary>
        /// Forgets the path so far. Used when the ball is moved rather than thrown — a
        /// recall or a reset jumps it across the court, and the jump must not be read as
        /// a shot on the way past.
        /// </summary>
        public void Clear()
        {
            hasPrevious = false;
            armed = false;
        }

        /// <summary>
        /// Takes the ball's position in the hoop's local space, with the ring's centre at
        /// the origin and its plane at y = 0. Returns true on the one step that completes
        /// a basket.
        /// </summary>
        /// <param name="hoopLocal">Centre of the ball, in the hoop's local space.</param>
        /// <param name="passRadius">
        /// How far off the hoop's axis the crossing may be. The ring's inside radius is the
        /// value to use: a ball that fits through the hole has its centre within about half
        /// of it at the plane, and a ball outside the ring cannot have its centre nearer
        /// than about one and a half times it, so anything in between is a hole the ball
        /// went through and there is no need to be precise inside that gap.
        /// </param>
        /// <param name="armHeight">How far above the ring the ball must rise to arm.</param>
        public bool Sample(Vector3 hoopLocal, float passRadius, float armHeight)
        {
            bool scored = false;

            if (hasPrevious && armed && previous.y > 0f && hoopLocal.y <= 0f)
            {
                // Where the ball's path met the plane, not where it happened to be looked
                // at. Samples are a physics step apart and a shot drops more than a tenth
                // of a metre in one, so either end of the step can be well clear of the
                // hole while the path between them went straight down the middle of it.
                float t = previous.y / (previous.y - hoopLocal.y);
                float x = Mathf.LerpUnclamped(previous.x, hoopLocal.x, t);
                float z = Mathf.LerpUnclamped(previous.z, hoopLocal.z, t);

                if (x * x + z * z <= passRadius * passRadius)
                {
                    scored = true;
                    // Spent. Only going back above the ring can make it count again, so a
                    // ball that drops through and rattles about below scores once.
                    armed = false;
                }
            }

            if (hoopLocal.y >= armHeight)
                armed = true;

            previous = hoopLocal;
            hasPrevious = true;
            return scored;
        }
    }
}
