using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Puts the ball back where the player can use it. A tap of the reset control returns
    /// every ball to its start and leaves the score alone, so recovering from a fumble
    /// costs nothing. Holding the control also clears the score, which is how a fresh
    /// session is started; the long press is deliberate enough not to happen by accident.
    ///
    /// This is not the same job as <see cref="BallRecall"/>. Reset puts the ball back on
    /// the court and takes it off any hand holding it, including the player's; recall
    /// brings a loose ball into the hands and refuses while one is held. One is for
    /// starting again, the other is for reaching something.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BallReset : MonoBehaviour
    {
        [Tooltip("Source of the reset action. Found on a parent when left empty.")]
        [SerializeField] private GameplayInput input;
        [Tooltip("Where a reset ball is put. Clear of the hands, so a reset visibly puts the ball down rather than back into a grip that is still closed.")]
        [SerializeField] private Transform ballStart;
        [Tooltip("Score cleared by a long press. The first one in the scene is used when left empty.")]
        [SerializeField] private ScoreKeeper score;
        [Tooltip("Hoops whose pending shots are forgotten on a reset. Every sensor in the scene is used when left empty.")]
        [SerializeField] private BasketSensor[] baskets;
        [Tooltip("How long the reset control must be held to also clear the score, in seconds.")]
        [SerializeField, Range(0.2f, 3f)] private float scoreClearHold = 1f;
        [Tooltip("Log each reset.")]
        [SerializeField] private bool logResets = true;

        private float held;
        private bool scoreCleared;

        /// <summary>Where a reset ball is put.</summary>
        public Transform BallStart
        {
            get => ballStart;
            set => ballStart = value;
        }

        /// <summary>Score cleared by a long press. Assign before enabling.</summary>
        public ScoreKeeper Score
        {
            get => score;
            set => score = value;
        }

        /// <summary>Hoops whose pending shots are forgotten on a reset. Assign before enabling.</summary>
        public BasketSensor[] Sensors
        {
            get => baskets;
            set => baskets = value;
        }

        /// <summary>How long the reset control must be held to also clear the score, in seconds.</summary>
        public float ScoreClearHold
        {
            get => scoreClearHold;
            set => scoreClearHold = value;
        }

        /// <summary>
        /// Returns every ball to its start, whoever is holding it and whatever it was in
        /// the middle of. The score is not touched.
        /// </summary>
        public void Restore()
        {
            for (int i = 0; i < Ball.Active.Count; i++)
            {
                Ball ball = Ball.Active[i];
                if (ball != null)
                    Restore(ball);
            }

            ForgetPendingShots();

            if (logResets)
                Debug.Log("[BallReset] ball returned to its start", this);
        }

        /// <summary>
        /// Returns one ball to its start. It is taken off any hand holding it, taken off
        /// any trip it was being carried on, and comes to rest.
        /// </summary>
        public void Restore(Ball ball)
        {
            if (ball == null || ballStart == null)
                return;

            // Order matters. Ending the trip first is what gives the ball its colliders
            // back, and letting go of it before moving it means no hand is left carrying a
            // ball that is suddenly somewhere else.
            ball.InTransit = false;
            ball.ForceRelease();

            Rigidbody body = ball.Body;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = ballStart.position;
            body.rotation = ballStart.rotation;
            // The Rigidbody is what physics reads, but the transform is what everything
            // else reads until the next step, interpolation included.
            ball.transform.SetPositionAndRotation(ballStart.position, ballStart.rotation);
        }

        private void OnEnable()
        {
            if (input == null)
                input = GetComponentInParent<GameplayInput>();
            if (score == null)
                score = FindFirstObjectByType<ScoreKeeper>();
            if (baskets == null || baskets.Length == 0)
                baskets = FindObjectsByType<BasketSensor>(FindObjectsSortMode.None);

            if (ballStart == null)
                Debug.LogError("BallReset has no ball start, so there is nowhere to put the ball back.", this);

            if (input != null)
                input.ResetPressed += OnResetPressed;

            held = 0f;
            scoreCleared = false;
        }

        private void OnDisable()
        {
            if (input != null)
                input.ResetPressed -= OnResetPressed;
        }

        private void Update()
        {
            if (input == null)
                return;

            if (!input.IsResetting)
            {
                held = 0f;
                scoreCleared = false;
                return;
            }

            held += Time.deltaTime;

            // Once per press, not once per frame the control stays down.
            if (scoreCleared || held < scoreClearHold)
                return;

            scoreCleared = true;

            if (score == null)
                return;

            score.ResetScore();

            if (logResets)
                Debug.Log("[BallReset] reset held; score cleared", this);
        }

        private void OnResetPressed() => Restore();

        // A reset carries the ball across the court in one jump, and that jump can pass
        // straight down through a hoop. Without this the trip home would be a basket.
        private void ForgetPendingShots()
        {
            if (baskets == null)
                return;

            for (int i = 0; i < baskets.Length; i++)
                if (baskets[i] != null)
                    baskets[i].Clear();
        }
    }
}
