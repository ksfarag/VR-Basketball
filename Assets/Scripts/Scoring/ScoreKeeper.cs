using System;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Counts the baskets. It listens to sensors and knows nothing about how a ball got
    /// through one, so the score can be driven and checked without a headset, a controller,
    /// or even a ball.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreKeeper : MonoBehaviour
    {
        [Tooltip("Hoops that count towards this score. Every sensor in the scene is used when left empty.")]
        [SerializeField] private BasketSensor[] baskets;
        [Tooltip("Points a basket is worth.")]
        [SerializeField, Range(1, 3)] private int pointsPerBasket = 2;
        [Tooltip("Log each change of score.")]
        [SerializeField] private bool logScore = true;

        /// <summary>Points scored so far.</summary>
        public int Score { get; private set; }

        /// <summary>How many baskets have been made, whatever each was worth.</summary>
        public int Baskets { get; private set; }

        /// <summary>Points a basket is worth.</summary>
        public int PointsPerBasket
        {
            get => pointsPerBasket;
            set => pointsPerBasket = value;
        }

        /// <summary>Hoops that count towards this score. Assign before enabling.</summary>
        public BasketSensor[] Sensors
        {
            get => baskets;
            set => baskets = value;
        }

        /// <summary>
        /// Raised whenever the score moves, with the new total and the change that made it.
        /// A reset reports a negative change, so anything celebrating a basket can tell the
        /// two apart.
        /// </summary>
        public event Action<int, int> Changed;

        /// <summary>Counts one basket.</summary>
        public void AddBasket()
        {
            Baskets++;
            Apply(pointsPerBasket);
        }

        /// <summary>Puts the score back to nothing.</summary>
        public void ResetScore()
        {
            if (Score == 0 && Baskets == 0)
                return;

            Baskets = 0;
            Apply(-Score);
        }

        private void OnEnable()
        {
            if (baskets == null || baskets.Length == 0)
                baskets = FindObjectsByType<BasketSensor>(FindObjectsSortMode.None);

            if (baskets.Length == 0)
            {
                Debug.LogError("ScoreKeeper has no basket sensor to listen to, so nothing can be scored.", this);
                return;
            }

            for (int i = 0; i < baskets.Length; i++)
                if (baskets[i] != null)
                    baskets[i].Scored += OnScored;
        }

        private void OnDisable()
        {
            if (baskets == null)
                return;

            for (int i = 0; i < baskets.Length; i++)
                if (baskets[i] != null)
                    baskets[i].Scored -= OnScored;
        }

        private void OnScored(Ball ball) => AddBasket();

        private void Apply(int change)
        {
            Score += change;

            if (logScore)
                Debug.Log($"[ScoreKeeper] {Score} ({change:+0;-0})", this);

            Changed?.Invoke(Score, change);
        }
    }
}
