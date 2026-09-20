using UnityEngine;

namespace VRBasketball
{
    /// <summary>
    /// Where the pieces of a hoop go. Plain geometry with no scene or Editor dependency,
    /// so the shape of the ring can be checked without building anything.
    ///
    /// The ring collider is a closed loop of capsules laid along the chords of a circle.
    /// Capsules are primitives, so contacts stay exact and cheap where a thin mesh
    /// collider would give a fast ball poor normals.
    /// </summary>
    public static class CourtLayout
    {
        /// <summary>One capsule of the ring, in the hoop's local space with the ring centred on the origin.</summary>
        public readonly struct RingSegment
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            /// <summary>Total capsule height along its local Z, caps included, as Unity measures it.</summary>
            public readonly float Height;

            public RingSegment(Vector3 position, Quaternion rotation, float height)
            {
                Position = position;
                Rotation = rotation;
                Height = height;
            }
        }

        /// <summary>
        /// The capsule that spans from one division of the ring to the next. Consecutive
        /// segments share an end point, so their rounded caps coincide there and the loop
        /// has no gap for a ball to slip through.
        /// </summary>
        public static RingSegment Segment(int index, int segments, float centrelineRadius, float tubeRadius)
        {
            float step = 2f * Mathf.PI / segments;
            Vector3 from = OnRing(index * step, centrelineRadius);
            Vector3 to = OnRing((index + 1) * step, centrelineRadius);

            Vector3 along = to - from;
            float chord = along.magnitude;

            // Unity's capsule height counts the caps, and the cylinder between them must
            // span the chord for the end caps to land on the ring's division points.
            return new RingSegment(
                (from + to) * 0.5f,
                Quaternion.LookRotation(along / chord, Vector3.up),
                chord + 2f * tubeRadius);
        }

        /// <summary>A point on the tube's centre circle, in the hoop's local space.</summary>
        public static Vector3 OnRing(float angleRadians, float centrelineRadius)
        {
            return new Vector3(Mathf.Cos(angleRadians) * centrelineRadius, 0f, Mathf.Sin(angleRadians) * centrelineRadius);
        }

        /// <summary>
        /// How far a chord's midpoint falls inside the true circle. This is the only way
        /// the built ring is tighter than the dimension asked for, so it is worth being
        /// able to measure rather than assume.
        /// </summary>
        public static float ChordSagitta(int segments, float centrelineRadius)
        {
            return centrelineRadius * (1f - Mathf.Cos(Mathf.PI / segments));
        }

        /// <summary>
        /// The narrowest the built hole ever gets, which is what a ball actually has to
        /// pass through. Smaller than the asked-for radius by the chord sagitta.
        /// </summary>
        public static float NarrowestHoleRadius(int segments, float innerRadius, float tubeRadius)
        {
            return innerRadius - ChordSagitta(segments, innerRadius + tubeRadius);
        }
    }
}
