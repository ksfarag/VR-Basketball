using UnityEngine;
using UnityEngine.InputSystem;

namespace VRBasketball
{
    /// <summary>
    /// One hand's grabbing, carrying, and throwing. Its own transform is the hand pose, so
    /// it sits under the rig's hand anchor. Poses are sampled where the rig refreshes them,
    /// grab and release transitions are resolved once per physics step, and a carried ball
    /// is driven by Rigidbody motion so it still collides while held.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandGrabber : MonoBehaviour
    {
        [Tooltip("Which hand's hold action this grabber follows.")]
        [SerializeField] private Hand hand = Hand.Right;
        [Tooltip("Source of the hold action. Found on a parent when left empty.")]
        [SerializeField] private GameplayInput input;
        [SerializeField] private GrabSettings settings;
        [Tooltip("Reports whether this hand is tracked. The hand counts as tracked when empty.")]
        [SerializeField] private InputActionReference tracked;
        [Tooltip("Head used to tell 'toward the player' from 'across the hands' when both hands hold the ball. Falls back to the main camera.")]
        [SerializeField] private Transform viewReference;
        [Tooltip("Log each grab and release with the resulting throw.")]
        [SerializeField] private bool logGrabs = true;

        private HandMotionEstimator motion;
        private Ball held;
        private bool holdRequested;
        private Vector3 holdOffset;
        private Quaternion holdRotation;
        private Vector3 throwLinear;
        private Vector3 throwAngular;
        private float supportBlend;
        private Vector3 lastSupportPoint;
        private bool wasSupported;
        private float twoHandDepth;

        /// <summary>Which hand's hold action this grabber follows. Assign before enabling.</summary>
        public Hand Hand
        {
            get => hand;
            set => hand = value;
        }

        /// <summary>Tuning this hand uses. Assign before enabling to override the asset.</summary>
        public GrabSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>
        /// Head the two-handed hold measures depth against. Without one the ball simply
        /// centres between the hands.
        /// </summary>
        public Transform ViewReference
        {
            get => viewReference;
            set => viewReference = value;
        }

        /// <summary>The ball this hand has hold of, as owner or as support, or null.</summary>
        public Ball Held => held;

        /// <summary>True when this hand owns the ball rather than steadying it.</summary>
        public bool IsCarrying => held != null && held.Holder == this;

        public bool IsTracked => tracked == null || tracked.action == null || tracked.action.IsPressed();

        /// <summary>Asks to pick up a ball. Acted on at the next physics step.</summary>
        public void BeginHold() => holdRequested = true;

        /// <summary>
        /// Asks to let go. The ball leaves the hand at the next physics step, but the
        /// throw is measured here, from the motion the player had when they let go. By the
        /// next step the hand is often already slowing, which would weaken every throw.
        /// </summary>
        public void EndHold()
        {
            if (!holdRequested)
                return;

            holdRequested = false;
            MeasureThrow(out throwLinear, out throwAngular);
        }

        /// <summary>
        /// Takes a ball being delivered straight into this hand, without the usual reach
        /// test. Only succeeds while this hand is asking to hold and is tracked, so a ball
        /// is never forced into a hand that is not reaching for it.
        /// </summary>
        public bool TryTakeDelivered(Ball ball)
        {
            if (ball == null || held != null || !holdRequested)
                return false;

            if (settings != null && settings.DropOnTrackingLoss && !IsTracked)
                return false;

            if (!ball.TryHold(this))
                return false;

            CaptureHold(ball);
            return true;
        }

        private void OnEnable()
        {
            if (settings == null)
            {
                Debug.LogError("HandGrabber has no GrabSettings asset.", this);
                enabled = false;
                return;
            }

            motion = new HandMotionEstimator(settings.VelocitySamples);
            holdRequested = false;
            throwLinear = Vector3.zero;
            throwAngular = Vector3.zero;
            supportBlend = 0f;
            wasSupported = false;

            if (input == null)
                input = GetComponentInParent<GameplayInput>();

            if (input != null)
            {
                input.HoldStarted += OnHoldStarted;
                input.HoldEnded += OnHoldEnded;
            }
            else
            {
                Debug.LogWarning("HandGrabber found no GameplayInput; it will only respond to direct calls.", this);
            }

            if (tracked != null && tracked.action != null)
                tracked.action.Enable();

            if (viewReference == null && Camera.main != null)
                viewReference = Camera.main.transform;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.HoldStarted -= OnHoldStarted;
                input.HoldEnded -= OnHoldEnded;
            }

            if (held != null)
                ReleaseHeld(Vector3.zero, Vector3.zero, "hand disabled");

            if (tracked != null && tracked.action != null)
                tracked.action.Disable();

            holdRequested = false;
            motion?.Clear();
        }

        // The rig moves its hand anchors in Update, so the throw history is sampled here.
        private void Update()
        {
            motion.Add(ThrowReference(), transform.rotation, Time.time);
        }

        private void FixedUpdate()
        {
            // The ball may have been disabled or let go of since the last step. A hand
            // keeps its hold whether it owns the ball or is steadying it.
            if (held != null && (!held.IsHeldBy(this) || !held.isActiveAndEnabled))
                held = null;

            bool canHold = IsTracked || !settings.DropOnTrackingLoss;

            if (held != null && !canHold)
                ReleaseHeld(Vector3.zero, Vector3.zero, "tracking lost");
            else if (held != null && !holdRequested)
                ReleaseHeld(throwLinear, throwAngular, "hold ended");
            else if (held == null && holdRequested && canHold)
                TryGrab();

            // Only the owning hand drives the ball; the other just lends its pose.
            if (IsCarrying)
                CarryHeld(Time.fixedDeltaTime);
        }

        private void OnHoldStarted(Hand which)
        {
            if (which == hand)
                BeginHold();
        }

        private void OnHoldEnded(Hand which)
        {
            if (which == hand)
                EndHold();
        }

        private void TryGrab()
        {
            Ball ball = Ball.FindEligible(transform.position, settings.GrabReach, settings.AllowTwoHandedHold);
            if (ball == null || !ball.TryHold(this))
                return;

            CaptureHold(ball);

            if (logGrabs)
                Debug.Log($"[HandGrabber] {hand} grabbed {ball.name}", this);
        }

        // Takes the ball where it was caught, so it does not snap; it then settles in.
        private void CaptureHold(Ball ball)
        {
            held = ball;
            supportBlend = 0f;
            wasSupported = false;
            Quaternion handToWorld = Quaternion.Inverse(transform.rotation);
            holdOffset = handToWorld * (ball.Body.position - transform.position);
            holdRotation = handToWorld * ball.Body.rotation;
        }

        // Eases the ball in to rest against the hand. Caught out at full stretch it would
        // otherwise stay out there, and every wrist turn would swing it a long way.
        private void SettleHold(float deltaTime)
        {
            if (settings.HoldSettle <= 0f)
            {
                holdOffset = Vector3.ClampMagnitude(holdOffset, settings.HoldDistance);
                return;
            }

            float distance = holdOffset.magnitude;
            if (distance <= settings.HoldDistance)
                return;

            Vector3 settled = holdOffset * (settings.HoldDistance / distance);
            holdOffset = Vector3.Lerp(holdOffset, settled, 1f - Mathf.Exp(-deltaTime / settings.HoldSettle));
        }

        private void CarryHeld(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            SettleHold(deltaTime);
            AdvanceSupportBlend(deltaTime);

            Rigidbody body = held.Body;
            Vector3 target = CarryTarget();
            Quaternion targetRotation = transform.rotation * holdRotation;

            Vector3 linear = (target - body.position) / deltaTime;
            body.linearVelocity = Vector3.ClampMagnitude(linear, settings.MaxHoldSpeed);

            Vector3 angular = HandMotionEstimator.AngularVelocity(body.rotation, targetRotation, deltaTime);
            body.angularVelocity = Vector3.ClampMagnitude(angular, settings.MaxHoldSpin);
        }

        private void MeasureThrow(out Vector3 linear, out Vector3 angular)
        {
            linear = Vector3.zero;
            angular = Vector3.zero;

            if (held == null || !motion.TryGetVelocity(settings.VelocityWindow, settings.ReleaseBias, out Vector3 handLinear, out Vector3 handAngular))
                return;

            // A ball held in one hand swings with the wrist, so the wrist adds to the
            // throw. Held with two it rides on the hands' positions and does not swing, so
            // that contribution fades out as the second hand takes hold.
            Vector3 lever = (held.Body.position - ThrowReference()) * (1f - supportBlend);
            linear = Vector3.ClampMagnitude(
                (handLinear + Vector3.Cross(handAngular, lever)) * settings.ThrowSpeedScale,
                settings.MaxThrowSpeed);
            angular = Vector3.ClampMagnitude(handAngular * settings.ThrowSpinScale, settings.MaxThrowSpin);
        }

        private void ReleaseHeld(Vector3 linear, Vector3 angular, string reason)
        {
            Ball ball = held;
            held = null;
            supportBlend = 0f;
            ball.ReleaseFrom(this, linear, angular);

            if (!logGrabs)
                return;

            // The ball is only thrown once no hand has hold of it.
            if (ball.IsHeld)
                Debug.Log($"[HandGrabber] {hand} let go of {ball.name} ({reason}); the other hand still has it", this);
            else
                Debug.Log($"[HandGrabber] {hand} released {ball.name} ({reason}) at {linear.magnitude:F2} m/s, {angular.magnitude:F2} rad/s", this);
        }

        // Eases the carried point between this hand and the midpoint of both hands, so
        // taking hold with the second hand never pops the ball.
        private void AdvanceSupportBlend(float deltaTime)
        {
            bool supported = held != null && held.Support != null;

            if (supported)
            {
                if (!wasSupported)
                {
                    // Remember how far out the ball was when the second hand arrived, so
                    // it keeps that reach instead of being pulled back to the controllers.
                    Vector3 axis = FacingAxis();
                    twoHandDepth = axis == Vector3.zero
                        ? 0f
                        : Vector3.Dot(held.Body.position - Midpoint(), axis);
                }

                lastSupportPoint = held.Support.transform.position;
            }

            wasSupported = supported;

            float goal = supported ? 1f : 0f;
            float rate = deltaTime / Mathf.Max(0.0001f, settings.TwoHandBlend);
            supportBlend = Mathf.MoveTowards(supportBlend, goal, rate);
        }

        private Vector3 CarryTarget()
        {
            Vector3 single = transform.position + (transform.rotation * holdOffset);
            if (supportBlend <= 0f)
                return single;

            // Held with two hands the ball rides on where the hands ARE, never on how
            // they are turned, so twisting the controllers cannot shove it around.
            return Vector3.Lerp(single, Midpoint() + (FacingAxis() * twoHandDepth), supportBlend);
        }

        private Vector3 Midpoint()
        {
            Vector3 other = held != null && held.Support != null ? held.Support.transform.position : lastSupportPoint;
            return Vector3.Lerp(transform.position, other, 0.5f);
        }

        /// <summary>
        /// The direction the player faces, flattened, so looking down at the ball does not
        /// tilt what "toward the player" means and shift the ball as the head moves. Zero
        /// when there is no head to measure against, which leaves the ball simply centred.
        /// </summary>
        private Vector3 FacingAxis()
        {
            if (viewReference == null)
                return Vector3.zero;

            Vector3 axis = Vector3.ProjectOnPlane(viewReference.forward, Vector3.up);
            return axis.sqrMagnitude < 1e-6f ? Vector3.zero : axis.normalized;
        }

        // The point the throw is measured from: this hand normally, the point between
        // both hands while the ball is held with two.
        private Vector3 ThrowReference()
        {
            if (!IsCarrying || supportBlend <= 0f)
                return transform.position;

            return Vector3.Lerp(transform.position, Midpoint(), supportBlend);
        }
    }
}
