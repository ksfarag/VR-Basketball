using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// The dimensions the court and hoop are built from. Kept in an asset so every number
    /// is in one place rather than spread through the scene, and so the court can be
    /// rebuilt after a change instead of being nudged by hand.
    ///
    /// Defaults are the regulation figures for a men's game. Bounce and friction are not
    /// here; they live in the physics material assets the builder assigns, so each value
    /// has one home.
    /// </summary>
    [CreateAssetMenu(fileName = "CourtSettings", menuName = "VR Basketball/Court Settings")]
    public sealed class CourtSettings : ScriptableObject
    {
        [Header("Ring")]
        [Tooltip("Height of the TOP of the ring above the floor, in metres. The rule is written this way, so the tube centre sits one tube radius below it.")]
        [SerializeField, Range(1f, 4f)] private float rimHeight = 3.048f;
        [Tooltip("Radius of the hole the ball must pass through, in metres. Regulation is 0.2286 (18 inch diameter).")]
        [SerializeField, Range(0.15f, 0.5f)] private float rimInnerRadius = 0.2286f;
        [Tooltip("Radius of the steel the ring is made from, in metres. Regulation is 0.01 (20 mm bar).")]
        [SerializeField, Range(0.005f, 0.05f)] private float rimTubeRadius = 0.01f;
        [Tooltip("How many capsules the ring collider is built from. More is rounder and costs more.")]
        [SerializeField, Range(8, 48)] private int rimSegments = 24;

        [Header("Backboard")]
        [SerializeField, Range(0.5f, 3f)] private float backboardWidth = 1.8f;
        [SerializeField, Range(0.5f, 2f)] private float backboardHeight = 1.05f;
        [SerializeField, Range(0.01f, 0.2f)] private float backboardThickness = 0.05f;
        [Tooltip("Height of the bottom edge of the board above the floor, in metres.")]
        [SerializeField, Range(1f, 4f)] private float backboardBottomHeight = 2.9f;
        [Tooltip("Gap between the board face and the near inside edge of the ring, in metres.")]
        [SerializeField, Range(0f, 0.5f)] private float rimGapFromBackboard = 0.15f;

        [Header("Court")]
        [Tooltip("Width of the floor across the court, in metres.")]
        [SerializeField, Range(4f, 30f)] private float courtWidth = 15.24f;
        [Tooltip("Length of the floor from the baseline back towards the player, in metres.")]
        [SerializeField, Range(4f, 30f)] private float courtLength = 15f;
        [Tooltip("How far the baseline sits behind the board face, in metres.")]
        [SerializeField, Range(0f, 3f)] private float baselineToBackboard = 1.2f;
        [Tooltip("Floor carried on past the baseline, in metres. It gives the post somewhere to stand and leaves space behind the hoop, which is what a shooter judges depth against.")]
        [SerializeField, Range(0f, 8f)] private float baselineRunOff = 2.5f;
        [SerializeField, Range(0.02f, 1f)] private float floorThickness = 0.2f;

        [Header("Bounds")]
        [Tooltip("Height of the temporary walls that keep a loose ball on the court, in metres. Proper recovery is plan item 5.")]
        [SerializeField, Range(0f, 6f)] private float wallHeight = 2.5f;
        [SerializeField, Range(0.02f, 1f)] private float wallThickness = 0.2f;

        public float RimHeight => rimHeight;
        public float RimInnerRadius => rimInnerRadius;
        public float RimTubeRadius => rimTubeRadius;
        public int RimSegments => rimSegments;

        public float BackboardWidth => backboardWidth;
        public float BackboardHeight => backboardHeight;
        public float BackboardThickness => backboardThickness;
        public float BackboardBottomHeight => backboardBottomHeight;
        public float RimGapFromBackboard => rimGapFromBackboard;

        public float CourtWidth => courtWidth;
        public float CourtLength => courtLength;
        public float BaselineToBackboard => baselineToBackboard;
        public float BaselineRunOff => baselineRunOff;
        public float FloorThickness => floorThickness;

        /// <summary>Local z of the baseline, measured from the ring's centre.</summary>
        public float Baseline => RimCentreToBackboardFace + baselineToBackboard;

        /// <summary>Local z of the floor's edge behind the hoop.</summary>
        public float FloorBackEdge => Baseline + baselineRunOff;

        /// <summary>Local z of the floor's edge furthest from the hoop.</summary>
        public float FloorFrontEdge => Baseline - courtLength;

        public float WallHeight => wallHeight;
        public float WallThickness => wallThickness;

        /// <summary>Height of the centre of the ring's tube above the floor.</summary>
        public float RimCentreHeight => rimHeight - rimTubeRadius;

        /// <summary>Radius of the circle the tube's centre follows.</summary>
        public float RimCentrelineRadius => rimInnerRadius + rimTubeRadius;

        /// <summary>
        /// Distance from the ring's centre to the face of the board, along the court.
        /// The rule fixes the gap to the ring's inside edge, so the hole radius adds to it.
        /// </summary>
        public float RimCentreToBackboardFace => rimInnerRadius + rimGapFromBackboard;
    }
}
