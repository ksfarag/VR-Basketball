using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// The timing of a dribble, kept apart from the ball and the hand so it can be checked
    /// without physics. One bounce runs from the hand down to the floor and back up. The
    /// ball leaves the hand at rest, is fastest where it meets the floor, and slows to a
    /// stop as it comes back up, the way a bounce under gravity does, so the hand never has
    /// to take it back at speed.
    /// </summary>
    public sealed class DribbleMotion
    {
        private float phase;
        private float owed;

        /// <summary>True while the ball is out of the hand on a bounce.</summary>
        public bool IsBouncing { get; private set; }

        /// <summary>How far through the bounce: 0 leaving the hand, 0.5 on the floor, 1 back.</summary>
        public float Phase => phase;

        /// <summary>How far down the ball is, as a fraction of the way to the floor.</summary>
        public float Drop => IsBouncing ? DropAt(phase) : 0f;

        /// <summary>Puts the ball back in the hand at once.</summary>
        public void Stop()
        {
            IsBouncing = false;
            phase = 0f;
            owed = 0f;
        }

        /// <summary>
        /// Moves the bounce on by one step. A new bounce only starts while
        /// <paramref name="keepDribbling"/> is true, but one already under way always
        /// finishes, because a ball on its way to the floor cannot be called back mid-air;
        /// it ends in the hand. True on the step the ball reaches the floor.
        /// </summary>
        public bool Advance(float deltaTime, float period, bool keepDribbling)
        {
            if (deltaTime <= 0f)
                return false;

            if (!IsBouncing)
            {
                if (!keepDribbling)
                    return false;

                IsBouncing = true;
                phase = 0f;
                owed = 0f;
            }

            float before = phase;
            phase += (deltaTime / Mathf.Max(period, 0.01f)) + owed;
            owed = 0f;

            // The floor is always met on a step of its own, so every bounce visibly lands
            // however the steps happen to fall. The time spent waiting for it is taken back
            // on the way up, which keeps the rhythm at exactly one bounce per period.
            if (before < 0.5f && phase >= 0.5f)
            {
                owed = phase - 0.5f;
                phase = 0.5f;
                return true;
            }

            if (phase >= 1f)
            {
                if (keepDribbling)
                    phase -= 1f;
                else
                    Stop();
            }

            return false;
        }

        /// <summary>
        /// How far down the ball is at <paramref name="phase"/>, from 0 in the hand to 1 on
        /// the floor. The way down is a fall from rest at the hand and the way up is the same
        /// fall run backwards, so the ball's speed is zero in the hand and greatest at the
        /// floor.
        /// </summary>
        public static float DropAt(float phase)
        {
            float fromFloor = Mathf.Abs((2f * Mathf.Repeat(phase, 1f)) - 1f);
            float fallen = 1f - fromFloor;
            return fallen * fallen;
        }
    }
}
