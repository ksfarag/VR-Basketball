using UnityEngine;

namespace VRBasketball
{
    /// <summary>Plays the ball's moving trail and a small burst at each meaningful contact.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Ball))]
    public sealed class BallEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem trailPrefab;
        [SerializeField] private ParticleSystem impactPrefab;
        [SerializeField, Min(0f)] private float trailSpeed = 1f;
        [SerializeField, Min(0f)] private float impactSpeed = 0.7f;
        [SerializeField, Min(0f)] private float impactCooldown = 0.08f;
        [Header("Sound")]
        [SerializeField] private AudioClip impactSound;
        [SerializeField] private AudioClip recallSound;
        [SerializeField, Min(0f)] private float impactSoundCooldown = 0.18f;

        private Ball ball;
        private AudioSource speaker;
        private ParticleSystem trail;
        private ParticleSystem impact;
        private float lastImpactTime = float.NegativeInfinity;
        private float lastImpactSoundTime = float.NegativeInfinity;
        private Vector3 lastPosition;

        private void Awake()
        {
            ball = GetComponent<Ball>();
            speaker = GetComponent<AudioSource>();
            lastPosition = transform.position;

            if (trailPrefab != null)
            {
                trail = Instantiate(trailPrefab, transform.position, Quaternion.identity);
                trail.name = "Ball Trail";
                trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (impactPrefab != null)
            {
                impact = Instantiate(impactPrefab, transform.position, Quaternion.identity);
                impact.name = "Ball Impact";
                impact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void LateUpdate()
        {
            if (trail == null)
                return;

            Vector3 position = transform.position;
            // A reset or recall must not draw a streak across the court.
            if ((position - lastPosition).sqrMagnitude > 4f)
                trail.Clear();
            lastPosition = position;
            trail.transform.position = position;

            bool moving = !ball.IsHeld && !ball.InTransit && ball.Body.linearVelocity.sqrMagnitude >= trailSpeed * trailSpeed;
            if (moving && !trail.isEmitting)
                trail.Play();
            else if (!moving && trail.isEmitting)
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (ball.InTransit || collision.contactCount == 0 ||
                collision.relativeVelocity.sqrMagnitude < impactSpeed * impactSpeed ||
                Time.time - lastImpactTime < impactCooldown)
                return;

            lastImpactTime = Time.time;
            if (impact != null)
            {
                ContactPoint contact = collision.GetContact(0);
                impact.transform.SetPositionAndRotation(contact.point, Quaternion.FromToRotation(Vector3.up, contact.normal));
                if (!impact.isPlaying)
                    impact.Play();
                impact.Emit(new ParticleSystem.EmitParams { startColor = new Color32(24, 219, 255, 255) }, 3);
                impact.Emit(new ParticleSystem.EmitParams { startColor = new Color32(255, 67, 176, 255) }, 2);
                impact.Emit(new ParticleSystem.EmitParams { startColor = new Color32(255, 207, 47, 255) }, 2);
            }

            if (speaker != null && impactSound != null && Time.time - lastImpactSoundTime >= impactSoundCooldown)
            {
                lastImpactSoundTime = Time.time;
                float volume = Mathf.Lerp(0.2f, 0.65f, Mathf.InverseLerp(impactSpeed, 7f, collision.relativeVelocity.magnitude));
                speaker.PlayOneShot(impactSound, volume);
            }
        }

        public void PlayRecallSound()
        {
            if (speaker == null || recallSound == null)
                return;

            speaker.PlayOneShot(recallSound, 0.65f);
        }

        private void OnDisable()
        {
            if (trail != null)
                trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (impact != null)
                impact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            if (trail != null)
                Destroy(trail.gameObject);
            if (impact != null)
                Destroy(impact.gameObject);
        }
    }
}
