using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Tuning for picking up, carrying, and throwing a ball. Kept in an asset so the feel
    /// can be adjusted from the Inspector without editing code or the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "GrabSettings", menuName = "VR Basketball/Grab Settings")]
    public sealed class GrabSettings : ScriptableObject
    {
        [Header("Grab")]
        [Tooltip("How far past the ball surface the hand may be and still pick it up, in metres.")]
        [SerializeField, Range(0.01f, 0.5f)] private float grabReach = 0.12f;
        [Tooltip("Drop a held ball while the hand is not tracked.")]
        [SerializeField] private bool dropOnTrackingLoss = true;

        [Header("Settle")]
        [Tooltip("How far the ball's centre ends up from the hand once it has settled, in metres.")]
        [SerializeField, Range(0f, 0.3f)] private float holdDistance = 0.11f;
        [Tooltip("How long the ball takes to settle into the hand after it is caught, in seconds.")]
        [SerializeField, Range(0f, 0.5f)] private float holdSettle = 0.15f;

        [Header("Two hands")]
        [Tooltip("Let a second hand take hold of a ball the other hand already has.")]
        [SerializeField] private bool allowTwoHandedHold = true;
        [Tooltip("How long the ball takes to settle between the hands, in seconds.")]
        [SerializeField, Range(0.02f, 0.5f)] private float twoHandBlend = 0.1f;

        [Header("Hold")]
        [Tooltip("Largest speed used to carry a held ball to the hand, in metres per second.")]
        [SerializeField, Range(1f, 50f)] private float maxHoldSpeed = 20f;
        [Tooltip("Largest turn rate used to carry a held ball, in radians per second.")]
        [SerializeField, Range(1f, 200f)] private float maxHoldSpin = 60f;

        [Header("Release")]
        [Tooltip("How much of the arm swing the throw is measured from, in seconds. A longer window follows more of the trajectory and less of the last instant.")]
        [SerializeField, Range(0.02f, 0.4f)] private float velocityWindow = 0.2f;
        [Tooltip("Where on the swing the throw is read. 0 averages the whole window, so only the arm decides it; 1 follows the swing to the instant of release, which is stronger but tracks the end of the motion more closely.")]
        [SerializeField, Range(0f, 1f)] private float releaseBias = 0.5f;
        [Tooltip("Largest number of hand poses kept for that window.")]
        [SerializeField, Range(4, 128)] private int velocitySamples = 48;
        [Tooltip("Multiplies the estimated throw speed. 1 releases at hand speed.")]
        [SerializeField, Range(0.25f, 3f)] private float throwSpeedScale = 1f;
        [Tooltip("Multiplies the estimated throw spin. 0 releases without spin.")]
        [SerializeField, Range(0f, 3f)] private float throwSpinScale = 1f;
        [Tooltip("Largest release speed, in metres per second.")]
        [SerializeField, Range(1f, 30f)] private float maxThrowSpeed = 12f;
        [Tooltip("Largest release spin, in radians per second.")]
        [SerializeField, Range(0f, 100f)] private float maxThrowSpin = 30f;

        [Header("Dribble")]
        [Tooltip("How long one bounce takes, from leaving the hand to coming back into it, in seconds.")]
        [SerializeField, Range(0.25f, 1.5f)] private float dribblePeriod = 0.5f;
        [Tooltip("How far the ball may fall to reach the floor, in metres. With nothing that close underneath it, the ball stays in the hand.")]
        [SerializeField, Range(0.3f, 3f)] private float dribbleReach = 2f;

        public float GrabReach => grabReach;
        public bool DropOnTrackingLoss => dropOnTrackingLoss;
        public float HoldDistance => holdDistance;
        public float HoldSettle => holdSettle;
        public bool AllowTwoHandedHold => allowTwoHandedHold;
        public float TwoHandBlend => twoHandBlend;
        public float MaxHoldSpeed => maxHoldSpeed;
        public float MaxHoldSpin => maxHoldSpin;
        public float VelocityWindow => velocityWindow;
        public float ReleaseBias => releaseBias;
        public int VelocitySamples => velocitySamples;
        public float ThrowSpeedScale => throwSpeedScale;
        public float ThrowSpinScale => throwSpinScale;
        public float MaxThrowSpeed => maxThrowSpeed;
        public float MaxThrowSpin => maxThrowSpin;
        public float DribblePeriod => dribblePeriod;
        public float DribbleReach => dribbleReach;
    }
}
