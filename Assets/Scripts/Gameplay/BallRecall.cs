using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Calls a loose ball back to the player. Squeezing both hold controls with empty
    /// hands brings the ball in: it homes to the point between the hands and is put into
    /// them on arrival. While it is on its way no hand can snatch it, so it always lands
    /// centred between both controllers rather than stopping at whichever one it grazed
    /// first. It passes through anything standing in the way on the trip in and turns
    /// solid again on arrival. The gesture is ignored while a ball is held, so holding
    /// with both hands stays free to mean something else.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BallRecall : MonoBehaviour
    {
        [Tooltip("Source of the hold actions. Found on a parent when left empty.")]
        [SerializeField] private GameplayInput input;
        [Tooltip("Hands the ball is called back to. Found in children when left empty.")]
        [SerializeField] private HandGrabber[] hands;
        [Tooltip("Fallback delivery point, used only when no hands are available.")]
        [SerializeField] private Transform returnPoint;
        [Tooltip("How long the ball takes to fly back, in seconds.")]
        [SerializeField, Range(0.1f, 2f)] private float returnDuration = 0.6f;
        [Tooltip("Log each call and how it ended.")]
        [SerializeField] private bool logRecalls = true;

        private Ball returning;
        private Vector3 from;
        private float elapsed;
        private bool bothHeldLastFrame;

        /// <summary>The ball currently flying back, or null.</summary>
        public Ball Returning => returning;

        /// <summary>Fallback delivery point, used only when no hands are available.</summary>
        public Transform ReturnPoint
        {
            get => returnPoint;
            set => returnPoint = value;
        }

        /// <summary>Hands the ball is called back to.</summary>
        public HandGrabber[] Hands
        {
            get => hands;
            set => hands = value;
        }

        /// <summary>
        /// Where a called ball is heading right now: the point between the hands, which
        /// moves with them, or the fallback point when there are no hands.
        /// </summary>
        public bool TryGetDestination(out Vector3 destination)
        {
            destination = Vector3.zero;
            int counted = 0;

            if (hands != null)
            {
                for (int i = 0; i < hands.Length; i++)
                {
                    HandGrabber hand = hands[i];
                    if (hand == null || !hand.isActiveAndEnabled)
                        continue;

                    destination += hand.transform.position;
                    counted++;
                }
            }

            if (counted > 0)
            {
                destination /= counted;
                return true;
            }

            if (returnPoint == null)
                return false;

            destination = returnPoint.position;
            return true;
        }

        /// <summary>
        /// Calls the loose ball furthest from the return point. False when there is
        /// nowhere to deliver it, nothing loose to call, or a hand is holding a ball.
        /// </summary>
        public bool TryRecall()
        {
            if (AnyBallHeld() || !TryGetDestination(out Vector3 destination))
                return false;

            Ball ball = Ball.FurthestFree(destination);
            if (ball == null)
                return false;

            returning = ball;
            from = ball.Body.position;
            elapsed = 0f;
            // The ball coasts back under our control rather than falling on the way, and
            // is reserved so no hand takes it part way and leaves it off to one side.
            // Marking it in transit is also what lifts its colliders for the trip.
            ball.InTransit = true;
            ball.Body.useGravity = false;
            ball.Body.angularVelocity = Vector3.zero;

            if (logRecalls)
                Debug.Log($"[BallRecall] calling {ball.name} back from {from:F2}", this);

            return true;
        }

        private void OnEnable()
        {
            if (input == null)
                input = GetComponentInParent<GameplayInput>();
            if (hands == null || hands.Length == 0)
                hands = GetComponentsInChildren<HandGrabber>(true);
            if ((hands == null || hands.Length == 0) && returnPoint == null)
                Debug.LogError("BallRecall has neither hands nor a return point, so a ball cannot be called back.", this);
        }

        private void OnDisable()
        {
            if (returning != null)
                Finish("recall cancelled", true);
            bothHeldLastFrame = false;
        }

        private void Update()
        {
            if (input == null)
                return;

            // Both hands squeezing with nothing held is the call gesture, taken once per
            // squeeze rather than every frame it stays down.
            bool bothHeld = input.IsHolding(Hand.Left) && input.IsHolding(Hand.Right);
            if (bothHeld && !bothHeldLastFrame)
                TryRecall();

            bothHeldLastFrame = bothHeld;
        }

        private void FixedUpdate()
        {
            if (returning == null)
                return;

            if (!returning.isActiveAndEnabled || returning.IsHeld)
            {
                Finish("hold taken while returning");
                return;
            }

            // Read the destination every step so the ball follows the hands in.
            if (!TryGetDestination(out Vector3 destination))
            {
                Finish("nowhere to deliver to", true);
                return;
            }

            elapsed += Time.fixedDeltaTime;
            float progress = returnDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / returnDuration);
            Vector3 target = Vector3.Lerp(from, destination, Mathf.SmoothStep(0f, 1f, progress));

            Rigidbody body = returning.Body;
            body.linearVelocity = (target - body.position) / Time.fixedDeltaTime;

            if (progress >= 1f)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                Deliver();
            }
        }

        // Puts the ball into every hand still asking for it. The first takes ownership and
        // the second steadies it, which is what centres the ball between the controllers.
        // A hand that has let go by now simply does not take it, and the ball falls.
        private void Deliver()
        {
            Ball ball = returning;
            returning = null;
            ball.InTransit = false;

            int taken = 0;
            if (hands != null)
            {
                for (int i = 0; i < hands.Length; i++)
                {
                    HandGrabber hand = hands[i];
                    if (hand != null && hand.isActiveAndEnabled && hand.TryTakeDelivered(ball))
                        taken++;
                }
            }

            if (taken == 0)
                ball.RestoreFreeMotion();

            if (logRecalls)
                Debug.Log($"[BallRecall] {ball.name} delivered into {taken} hand(s)", this);
        }

        private void Finish(string reason, bool restoreGravity = false)
        {
            Ball ball = returning;
            returning = null;

            if (ball != null)
                ball.InTransit = false;

            if (ball != null && restoreGravity)
                ball.RestoreFreeMotion();

            if (logRecalls && ball != null)
                Debug.Log($"[BallRecall] {ball.name} {reason}", this);
        }

        private static bool AnyBallHeld()
        {
            for (int i = 0; i < Ball.Active.Count; i++)
            {
                Ball ball = Ball.Active[i];
                if (ball != null && ball.IsHeld)
                    return true;
            }

            return false;
        }
    }
}
