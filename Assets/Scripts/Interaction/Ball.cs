using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// A ball a hand can pick up. Tracks its single holder and switches the Rigidbody
    /// between free and carried. Motion while carried belongs to the holder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Ball : MonoBehaviour
    {
        private static readonly List<Ball> active = new List<Ball>();
        private static readonly List<Collider> foundColliders = new List<Collider>();

        private readonly List<Collider> suspended = new List<Collider>();

        private Rigidbody body;
        private SphereCollider sphere;
        private bool gravityWhenFree;
        private bool inTransit;

        /// <summary>The hand that owns this ball, or null when it is free.</summary>
        public HandGrabber Holder { get; private set; }

        /// <summary>
        /// A second hand steadying the ball. It does not own the ball, but the owner
        /// carries it between the two hands and takes both into account on release.
        /// </summary>
        public HandGrabber Support { get; private set; }

        public bool IsHeld => Holder != null;

        /// <summary>Raised when the last hand releases this ball with its throw velocity.</summary>
        public event Action<Vector3> Thrown;

        /// <summary>True while both hands have hold of the ball.</summary>
        public bool IsHeldWithBothHands => Holder != null && Support != null;

        /// <summary>Whether <paramref name="grabber"/> has hold of this ball at all.</summary>
        public bool IsHeldBy(HandGrabber grabber) => grabber != null && (Holder == grabber || Support == grabber);

        /// <summary>
        /// True while something else is carrying the ball to a destination. Hands leave an
        /// in-transit ball alone rather than snatching it part way, so it can be delivered
        /// where it was headed. The ball's colliders are switched off for the trip, so a
        /// rim, a wall, or a player standing on the line cannot knock it off course or
        /// stop it short, and they come back however the trip ends.
        /// </summary>
        public bool InTransit
        {
            get => inTransit;
            set
            {
                if (inTransit == value)
                    return;

                inTransit = value;

                if (value)
                    SuspendColliders();
                else
                    RestoreColliders();
            }
        }

        public Rigidbody Body
        {
            get
            {
                EnsureComponents();
                return body;
            }
        }

        /// <summary>Collider radius in world units.</summary>
        public float Radius
        {
            get
            {
                EnsureComponents();
                Vector3 scale = transform.lossyScale;
                float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                return sphere.radius * largest;
            }
        }

        /// <summary>Every enabled ball, held or not.</summary>
        public static IReadOnlyList<Ball> Active => active;

        /// <summary>
        /// The free ball furthest from <paramref name="point"/>, or null when every ball
        /// is held. This is the one most worth calling back.
        /// </summary>
        public static Ball FurthestFree(Vector3 point)
        {
            Ball furthest = null;
            float bestDistance = -1f;

            for (int i = 0; i < active.Count; i++)
            {
                Ball ball = active[i];
                if (ball == null || ball.IsHeld)
                    continue;

                float distance = Vector3.Distance(point, ball.Body.position);
                if (distance <= bestDistance)
                    continue;

                furthest = ball;
                bestDistance = distance;
            }

            return furthest;
        }

        /// <summary>
        /// The free ball whose surface is closest to <paramref name="handPosition"/> and
        /// within <paramref name="reach"/> of it, or null when no ball is eligible.
        /// </summary>
        public static Ball FindEligible(Vector3 handPosition, float reach, bool allowSecondHand = false)
        {
            Ball best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < active.Count; i++)
            {
                Ball ball = active[i];
                if (ball == null)
                    continue;

                // A ball one hand already owns can still be taken up by the other hand,
                // as a support rather than a second owner.
                if (ball.InTransit)
                    continue;

                bool canSupport = allowSecondHand && ball.Holder != null && ball.Support == null;
                if (ball.IsHeld && !canSupport)
                    continue;

                float toSurface = Vector3.Distance(handPosition, ball.Body.position) - ball.Radius;
                if (toSurface > reach || toSurface >= bestDistance)
                    continue;

                best = ball;
                bestDistance = toSurface;
            }

            return best;
        }

        /// <summary>
        /// Gives this ball to <paramref name="holder"/>. False when another hand already
        /// holds it, which is how one owner per ball is kept.
        /// </summary>
        public bool TryHold(HandGrabber holder)
        {
            if (holder == null)
                return false;

            if (Holder == holder || Support == holder)
                return true;

            if (Holder == null)
            {
                Holder = holder;
                Body.useGravity = false;
                return true;
            }

            // One owner only; a second hand steadies the ball instead.
            if (Support == null)
            {
                Support = holder;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Lets go with one hand. The ball is only thrown once no hand has hold of it, so
        /// releasing the owning hand while the other still holds on hands the ball over
        /// rather than dropping it. False when <paramref name="holder"/> has no hold on
        /// this ball, so a stale hand cannot throw a ball it no longer owns.
        /// </summary>
        public bool ReleaseFrom(HandGrabber holder, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (holder == null)
                return false;

            if (Support == holder)
            {
                Support = null;
                return true;
            }

            if (Holder != holder)
                return false;

            if (Support != null)
            {
                Holder = Support;
                Support = null;
                return true;
            }

            Holder = null;
            Body.useGravity = gravityWhenFree;
            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
            Thrown?.Invoke(linearVelocity);
            return true;
        }

        /// <summary>
        /// Takes the ball off whatever hands have hold of it, without throwing it. This is
        /// how a reset gets a ball out of a fist that is still closed: no hand is asked to
        /// let go, the ball simply stops being theirs, and each hand notices on its next
        /// physics step that the ball it was carrying is no longer held by it.
        ///
        /// Asking the hand to let go instead would measure the throw the player was in the
        /// middle of and apply it a step later, from wherever the reset had just put the
        /// ball.
        /// </summary>
        public void ForceRelease()
        {
            Holder = null;
            Support = null;
            Body.useGravity = gravityWhenFree;
        }

        /// <summary>
        /// Puts a free ball back under gravity. Used by anything that moved the ball
        /// under its own control and has finished with it.
        /// </summary>
        public void RestoreFreeMotion()
        {
            if (IsHeld)
                return;

            Body.useGravity = gravityWhenFree;
        }

        private void Awake()
        {
            EnsureComponents();
            gravityWhenFree = body.useGravity;
        }

        // Children are included because the ball's art may bring colliders of its own.
        private void SuspendColliders()
        {
            GetComponentsInChildren(true, foundColliders);
            suspended.Clear();

            for (int i = 0; i < foundColliders.Count; i++)
            {
                Collider collider = foundColliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                suspended.Add(collider);
            }
        }

        // Only what this switched off is switched back on, so a collider disabled for its
        // own reasons is not turned on by a trip that had nothing to do with it.
        private void RestoreColliders()
        {
            for (int i = 0; i < suspended.Count; i++)
            {
                Collider collider = suspended[i];
                if (collider != null)
                    collider.enabled = true;
            }

            suspended.Clear();
        }

        // Awake does not run outside Play mode, so anything reading the ball resolves its
        // own components first.
        private void EnsureComponents()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (sphere == null)
                sphere = GetComponent<SphereCollider>();
        }

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
            // A disabled ball cannot be carried or delivered; holders notice the cleared
            // owner, and the trip ends here rather than leaving the ball to come back
            // without its colliders.
            Holder = null;
            Support = null;
            InTransit = false;
            Body.useGravity = gravityWhenFree;
        }
    }
}
