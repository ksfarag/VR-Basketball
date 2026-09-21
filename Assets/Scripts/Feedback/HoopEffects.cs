using UnityEngine;

namespace VRBasketball
{
    /// <summary>Celebrates a completed basket at this ring's centre.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BasketSensor))]
    public sealed class HoopEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem scorePrefab;

        private BasketSensor sensor;
        private ParticleSystem scoreEffect;

        public ParticleSystem ScorePrefab
        {
            get => scorePrefab;
            set => scorePrefab = value;
        }

        private void Awake()
        {
            sensor = GetComponent<BasketSensor>();
            if (scorePrefab == null)
                return;

            scoreEffect = Instantiate(scorePrefab, transform);
            scoreEffect.name = "Score Burst";
            scoreEffect.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            scoreEffect.transform.localRotation = Quaternion.identity;
            scoreEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnEnable()
        {
            if (sensor == null)
                sensor = GetComponent<BasketSensor>();
            sensor.Scored += OnScored;
        }

        private void OnDisable()
        {
            if (sensor != null)
                sensor.Scored -= OnScored;
            if (scoreEffect != null)
                scoreEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnScored(Ball ball)
        {
            if (scoreEffect == null)
                return;

            if (!scoreEffect.isPlaying)
                scoreEffect.Play();
            scoreEffect.Emit(32);
        }
    }
}
