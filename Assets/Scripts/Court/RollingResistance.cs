using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Bleeds the spin out of a ball that is rolling on a surface and removes the last
    /// tiny rebound so a loose ball can settle.
    ///
    /// A Unity sphere has no rolling resistance of its own, so a thrown ball rolls until
    /// something stops it; the developer watched one roll fifteen metres off the edge of
    /// the floor. Raising the Rigidbody's angular damping would settle it, but that
    /// applies in flight too and would eat the backspin off a shot, so the drag is applied
    /// only while the ball is actually touching something.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RollingResistance : MonoBehaviour
    {
        [Tooltip("Rolling resistance coefficient. A real ball on hardwood is nearer 0.02, which still rolls tens of metres; this is deliberately higher so a missed shot settles. Raise it to stop the ball sooner.")]
        [SerializeField, Range(0f, 0.3f)] private float coefficient = 0.12f;

        [Tooltip("An upward rebound slower than this is removed on a supporting surface. One metre per second is a hop of about five centimetres; normal shot and dribble bounces are faster.")]
        [SerializeField, Min(0f)] private float settleBounceSpeed = 1f;

        private Rigidbody body;
        private Ball ball;
        private SphereCollider sphere;
        private bool touching;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ball = GetComponent<Ball>();
            sphere = GetComponent<SphereCollider>();
        }

        // Contact callbacks run after the step, so a touch is spent on the step after it
        // happened. One step of lag does not show at physics rates.
        private void OnCollisionEnter(Collision collision)
        {
            touching = true;
            SettleSmallBounce(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            touching = true;
            SettleSmallBounce(collision);
        }

        private void SettleSmallBounce(Collision collision)
        {
            // A held ball is driven by HandGrabber, including the deliberate dribble.
            if (settleBounceSpeed <= 0f || (ball != null && ball.IsHeld))
                return;

            Vector3 velocity = body.linearVelocity;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                float up = Vector3.Dot(normal, Vector3.up);
                if (Mathf.Abs(up) < 0.5f)
                    continue;
                if (up < 0f)
                    normal = -normal;

                float reboundSpeed = Vector3.Dot(velocity, normal);
                if (reboundSpeed <= 0f || reboundSpeed > settleBounceSpeed)
                    continue;

                // Keep rolling motion along the floor; remove only the small velocity
                // taking the ball away from its supporting surface.
                body.linearVelocity = velocity - normal * reboundSpeed;
                if (body.linearVelocity.sqrMagnitude < 0.01f && body.angularVelocity.sqrMagnitude < 1f)
                    body.Sleep();
                return;
            }
        }

        private void FixedUpdate()
        {
            bool wasTouching = touching;
            touching = false;

            if (!wasTouching || coefficient <= 0f)
                return;

            // A carried ball is driven by the hand, and braking it would fight the hold.
            if (ball != null && ball.IsHeld)
                return;

            Vector3 spin = body.angularVelocity;
            float rate = spin.magnitude;
            if (rate < 1e-4f)
                return;

            // The resisting torque is the coefficient times the normal force times the
            // radius. Taking the normal force as the ball's weight is the resting-contact
            // case, which is the one this exists for.
            float radius = sphere != null ? sphere.radius * MaxScale(transform.lossyScale) : 0.5f;
            float torque = coefficient * body.mass * Physics.gravity.magnitude * radius;

            // Never brake past a standstill; that would spin the ball back up the other way.
            Vector3 inertia = body.inertiaTensor;
            float smallest = Mathf.Min(inertia.x, Mathf.Min(inertia.y, inertia.z));
            float stoppingTorque = rate * smallest / Time.fixedDeltaTime;

            body.AddTorque(spin / rate * -Mathf.Min(torque, stoppingTorque), ForceMode.Force);
        }

        private static float MaxScale(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
    }
}
