using System;
using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Shows the score on a seven-segment readout and marks each basket briefly. The bars
    /// are ordinary renderers built into the scene, so what the board shows is a choice of
    /// material per bar and nothing else has to run for it to be readable.
    ///
    /// Feedback is deliberately short: the lit bars change colour, the board swells a
    /// fraction, and a chime plays. A player watching the ball rather than the board still
    /// gets the sound, and a player with the sound off still sees the board change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Scoreboard : MonoBehaviour
    {
        /// <summary>One cell of the readout.</summary>
        [Serializable]
        public sealed class Digit
        {
            [Tooltip("The seven bars, in the order top, upper left, upper right, middle, lower left, lower right, bottom.")]
            [SerializeField] private Renderer[] segments;

            public Renderer[] Segments
            {
                get => segments;
                set => segments = value;
            }
        }

        [Tooltip("Score shown here. The first one in the scene is used when left empty.")]
        [SerializeField] private ScoreKeeper score;
        [Tooltip("Cells of the readout, most significant first.")]
        [SerializeField] private Digit[] digits;

        [Header("Look")]
        [SerializeField] private Material litMaterial;
        [SerializeField] private Material dimMaterial;
        [Tooltip("Colour the lit bars take for a moment after a basket.")]
        [SerializeField] private Material flashMaterial;

        [Header("Feedback")]
        [Tooltip("How long a basket is marked for, in seconds.")]
        [SerializeField, Range(0f, 3f)] private float flashDuration = 0.7f;
        [Tooltip("How much larger the board swells at the moment of a basket, as a fraction of its size.")]
        [SerializeField, Range(0f, 0.5f)] private float flashSwell = 0.08f;
        [Tooltip("Plays the basket sound. Left empty, the board is silent.")]
        [SerializeField] private AudioSource speaker;
        [Tooltip("Sound of a basket. A short chime is generated when left empty, so the board is not silent before there is any audio to use.")]
        [SerializeField] private AudioClip basketSound;

        [Header("Six Point Easter Egg")]
        [Tooltip("World-space canvas placed on the backboard and shown when the score reaches six points.")]
        [SerializeField] private CanvasGroup easterEggCanvas;
        [SerializeField] private AudioClip easterEggSound;
        [SerializeField, Min(0f)] private float easterEggHold = 3f;
        [SerializeField, Min(0.01f)] private float easterEggFade = 0.35f;

        private Vector3 restScale;
        private AudioClip chime;
        private float flashRemaining;
        private int shown = -1;
        private bool showingFlash;
        private float easterEggRemaining;

        /// <summary>Score shown here. Assign before enabling.</summary>
        public ScoreKeeper Score
        {
            get => score;
            set => score = value;
        }

        /// <summary>Cells of the readout, most significant first. Assign before enabling.</summary>
        public Digit[] Digits
        {
            get => digits;
            set => digits = value;
        }

        /// <summary>Plays the basket sound. Assign before enabling.</summary>
        public AudioSource Speaker
        {
            get => speaker;
            set => speaker = value;
        }

        /// <summary>True while a basket is still being marked.</summary>
        public bool IsFlashing => flashRemaining > 0f;

        public bool IsShowingEasterEgg => easterEggCanvas != null && easterEggCanvas.gameObject.activeSelf;

        public CanvasGroup EasterEggCanvas
        {
            get => easterEggCanvas;
            set => easterEggCanvas = value;
        }

        public AudioClip EasterEggSound
        {
            get => easterEggSound;
            set => easterEggSound = value;
        }

        /// <summary>Materials the bars take when lit, when unlit, and just after a basket.</summary>
        public void SetMaterials(Material lit, Material dim, Material flash)
        {
            litMaterial = lit;
            dimMaterial = dim;
            flashMaterial = flash;
        }

        private void Awake()
        {
            restScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (score == null)
                score = FindFirstObjectByType<ScoreKeeper>();

            if (digits == null || digits.Length == 0)
            {
                Debug.LogError("Scoreboard has no digits to show the score on.", this);
                return;
            }

            if (score == null)
            {
                Debug.LogError("Scoreboard has no ScoreKeeper to show, so it will stay blank.", this);
                return;
            }

            score.Changed += OnScoreChanged;

            // Show whatever the score already is, not whatever the bars were left showing
            // in the Editor.
            shown = -1;
            flashRemaining = 0f;
            HideEasterEgg();
            Render(score.Score, false);
        }

        private void OnDisable()
        {
            if (score != null)
                score.Changed -= OnScoreChanged;

            flashRemaining = 0f;
            HideEasterEgg();
            transform.localScale = restScale;
        }

        private void Update()
        {
            if (flashRemaining > 0f)
            {
                flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
                float left = flashDuration <= 0f ? 0f : flashRemaining / flashDuration;
                transform.localScale = restScale * (1f + flashSwell * left);
                if (flashRemaining <= 0f)
                    Render(shown, false);
            }

            if (easterEggRemaining > 0f)
            {
                easterEggRemaining = Mathf.Max(0f, easterEggRemaining - Time.deltaTime);
                if (easterEggRemaining <= 0f)
                    HideEasterEgg();
                else if (easterEggRemaining < easterEggFade)
                    SetEasterEggAlpha(easterEggRemaining / easterEggFade);
            }
        }

        private void OnScoreChanged(int total, int change)
        {
            bool basket = change > 0;
            Render(total, basket);

            if (!basket)
            {
                // A reset is a change, not a celebration: show it and stop marking.
                flashRemaining = 0f;
                HideEasterEgg();
                transform.localScale = restScale;
                return;
            }

            flashRemaining = flashDuration;
            transform.localScale = restScale * (1f + flashSwell);
            bool easterEgg = total == 6;
            if (easterEgg)
                ShowEasterEgg();
            Play(easterEgg);
        }

        private void Play(bool easterEgg)
        {
            if (speaker == null)
                return;

            AudioClip clip = easterEgg && easterEggSound != null ? easterEggSound : basketSound;
            if (clip == null)
            {
                if (chime == null)
                    chime = Chime();
                clip = chime;
            }
            if (clip != null)
                speaker.PlayOneShot(clip);
        }

        private void ShowEasterEgg()
        {
            if (easterEggCanvas == null)
                return;

            easterEggRemaining = easterEggHold + easterEggFade;
            SetEasterEggAlpha(1f);
            easterEggCanvas.gameObject.SetActive(true);
        }

        private void HideEasterEgg()
        {
            easterEggRemaining = 0f;
            if (easterEggCanvas != null)
                easterEggCanvas.gameObject.SetActive(false);
        }

        private void SetEasterEggAlpha(float alpha)
        {
            easterEggCanvas.alpha = Mathf.Clamp01(alpha);
        }

        private void Render(int value, bool flash)
        {
            if (value == shown && flash == showingFlash)
                return;

            shown = value;
            showingFlash = flash;

            Material on = flash && flashMaterial != null ? flashMaterial : litMaterial;

            for (int cell = 0; cell < digits.Length; cell++)
            {
                Renderer[] segments = digits[cell] == null ? null : digits[cell].Segments;
                if (segments == null)
                    continue;

                int digit = SevenSegment.DigitAt(value, digits.Length, cell);

                for (int segment = 0; segment < segments.Length; segment++)
                {
                    Renderer bar = segments[segment];
                    if (bar == null)
                        continue;

                    // sharedMaterial rather than material: this only chooses which of two
                    // existing materials a bar points at, where material would clone one
                    // per bar on the first frame and leave the clones behind.
                    bar.sharedMaterial = SevenSegment.IsLit(digit, segment) ? on : dimMaterial;
                }
            }
        }

        /// <summary>
        /// A short decaying chime, built rather than imported. There is no audio in the
        /// project yet and a basket that makes no sound is hard to judge on a headset, so
        /// the board brings its own until real audio replaces it.
        /// </summary>
        private static AudioClip Chime()
        {
            const int Rate = 44100;
            const float Seconds = 0.4f;
            const float Root = 880f;

            var samples = new float[(int)(Rate * Seconds)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / Rate;
                float envelope = Mathf.Exp(-8f * t);
                // The root and a fifth above it, which reads as a chime rather than a beep.
                samples[i] = 0.3f * envelope *
                             (Mathf.Sin(2f * Mathf.PI * Root * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * Root * 1.5f * t));
            }

            var clip = AudioClip.Create("Basket Chime", samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
