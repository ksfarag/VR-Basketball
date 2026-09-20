using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Watches the balls for a shot through this hoop and announces each one. Its own
    /// transform is the ring: the origin sits at the centre of the hole and local y points
    /// up through it, so moving or turning the hoop moves the rule with it.
    ///
    /// It samples positions at physics timing rather than waiting on trigger callbacks. A
    /// trigger is the usual way to do this and is the wrong tool twice over: a ball falling
    /// through a hoop covers more than a tenth of a metre in a physics step, so a trigger
    /// thin enough to sit in the ring is a trigger a shot can pass straight through, and a
    /// ball that comes to rest inside one reports itself over and over.
    ///
    /// Nothing here reads the headset or the controls, so a basket can be checked without
    /// either.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BasketSensor : MonoBehaviour
    {
        [Tooltip("How far off the hoop's axis the ball's path may cross the ring's plane and still count, in metres. The ring's inside radius is the value to use.")]
        [SerializeField, Range(0.05f, 1f)] private float passRadius = 0.2286f;
        [Tooltip("How far above the ring a ball must rise before a drop through counts, in metres. It is what stops one shot being counted twice.")]
        [SerializeField, Range(0.01f, 1f)] private float armHeight = 0.12f;
        [Tooltip("Log each basket.")]
        [SerializeField] private bool logBaskets = true;

        private readonly List<Ball> watched = new List<Ball>();
        private readonly List<BasketDetector> detectors = new List<BasketDetector>();

        /// <summary>Raised on the physics step a ball completes its passage through the ring.</summary>
        public event Action<Ball> Scored;

        /// <summary>How far off the axis a crossing may be and still count, in metres.</summary>
        public float PassRadius
        {
            get => passRadius;
            set => passRadius = value;
        }

        /// <summary>How far above the ring a ball must rise before a drop through counts, in metres.</summary>
        public float ArmHeight
        {
            get => armHeight;
            set => armHeight = value;
        }

        /// <summary>Forgets every ball's path, so nothing in flight can score from before now.</summary>
        public void Clear()
        {
            for (int i = 0; i < detectors.Count; i++)
                detectors[i].Clear();
        }

        private void OnDisable()
        {
            watched.Clear();
            detectors.Clear();
        }

        private void FixedUpdate()
        {
            Forget();

            IReadOnlyList<Ball> active = Ball.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Ball ball = active[i];
                if (ball == null)
                    continue;

                BasketDetector detector = DetectorFor(ball);

                // A ball the game itself is carrying somewhere is not a shot. The recall
                // flies it in a straight line through whatever is in the way, and the
                // hoop is as passable as everything else on that trip.
                if (ball.InTransit)
                {
                    detector.Clear();
                    continue;
                }

                // A held ball is still watched: pushing it down through the ring by hand
                // is a dunk, and it passed through the hole like any other shot.
                if (detector.Sample(transform.InverseTransformPoint(ball.Body.position), passRadius, armHeight))
                    Award(ball);
            }
        }

        private void Award(Ball ball)
        {
            if (logBaskets)
                Debug.Log($"[BasketSensor] {ball.name} through the hoop", this);

            Scored?.Invoke(ball);
        }

        private BasketDetector DetectorFor(Ball ball)
        {
            for (int i = 0; i < watched.Count; i++)
                if (watched[i] == ball)
                    return detectors[i];

            var detector = new BasketDetector();
            watched.Add(ball);
            detectors.Add(detector);
            return detector;
        }

        // A ball that has gone is dropped rather than kept, so a new ball in its place
        // starts with no path behind it and cannot inherit a half-finished shot.
        private void Forget()
        {
            for (int i = watched.Count - 1; i >= 0; i--)
            {
                Ball ball = watched[i];
                if (ball != null && ball.isActiveAndEnabled)
                    continue;

                watched.RemoveAt(i);
                detectors.RemoveAt(i);
            }
        }
    }
}
