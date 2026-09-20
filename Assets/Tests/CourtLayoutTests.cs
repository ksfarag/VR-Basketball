using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VRBasketball.Tests
{
    /// <summary>
    /// Checks the shape of the ring the court builder lays down, and then checks the
    /// numbers actually shipped in the settings asset against the ball actually shipped in
    /// the prefab. A ring that is a closed loop in the abstract is no use if the asset it
    /// is built from describes a hoop the ball cannot fit through.
    /// </summary>
    public class CourtLayoutTests
    {
        private const float InnerRadius = 0.2286f;
        private const float TubeRadius = 0.01f;
        private const int Segments = 24;

        private static float Centreline => InnerRadius + TubeRadius;

        /// <summary>The two ends of a segment's cylinder, where its round caps are centred.</summary>
        private static void Ends(CourtLayout.RingSegment segment, float tubeRadius, out Vector3 near, out Vector3 far)
        {
            Vector3 axis = segment.Rotation * Vector3.forward;
            float halfCylinder = segment.Height * 0.5f - tubeRadius;
            near = segment.Position - axis * halfCylinder;
            far = segment.Position + axis * halfCylinder;
        }

        [Test]
        public void RingSegmentsMeetEndToEndAllTheWayRound()
        {
            for (int i = 0; i < Segments; i++)
            {
                Ends(CourtLayout.Segment(i, Segments, Centreline, TubeRadius), TubeRadius, out _, out Vector3 far);
                Ends(CourtLayout.Segment((i + 1) % Segments, Segments, Centreline, TubeRadius), TubeRadius, out Vector3 nextNear, out _);

                Assert.Less(Vector3.Distance(far, nextNear), 1e-4f,
                    $"segment {i} must end where segment {(i + 1) % Segments} begins, or a ball can slip between them");
            }
        }

        [Test]
        public void RingSegmentEndsSitOnTheTubeCentreline()
        {
            for (int i = 0; i < Segments; i++)
            {
                Ends(CourtLayout.Segment(i, Segments, Centreline, TubeRadius), TubeRadius, out Vector3 near, out Vector3 far);

                foreach (Vector3 end in new[] { near, far })
                {
                    Assert.AreEqual(Centreline, new Vector2(end.x, end.z).magnitude, 1e-4f, "ends belong on the ring");
                    Assert.AreEqual(0f, end.y, 1e-4f, "the ring is flat");
                }
            }
        }

        [Test]
        public void TheBuiltHoleIsTighterThanTheNominalOneByTheChordSagitta()
        {
            float sagitta = CourtLayout.ChordSagitta(Segments, Centreline);
            Assert.AreEqual(InnerRadius - sagitta, CourtLayout.NarrowestHoleRadius(Segments, InnerRadius, TubeRadius), 1e-6f);

            // Chords cut the corner, so the built hole is always a little tight. Two bounds
            // make that harmless rather than merely small: the faceting has to be finer than
            // the metal it is made of, and it has to be lost in the width of the hole.
            Assert.Greater(sagitta, 0f, "a chord always falls inside the arc it spans");
            Assert.Less(sagitta, TubeRadius,
                $"the flats between segments ({sagitta * 1000f:F2} mm) must be smaller than the tube itself ({TubeRadius * 1000f:F2} mm)");
            Assert.Less(sagitta / InnerRadius, 0.01f,
                $"the ring should lose under one percent of its radius to faceting, not {sagitta / InnerRadius:P2}");
        }

        [Test]
        public void FewerSegmentsMakeATighterHole()
        {
            Assert.Greater(CourtLayout.ChordSagitta(8, Centreline), CourtLayout.ChordSagitta(24, Centreline),
                "a coarser ring cuts more corner off the hole");
        }

        [Test]
        public void TheShippedHoopLetsTheShippedBallThrough()
        {
            CourtSettings settings = LoadSettings();
            float ballRadius = LoadBallRadius();

            float hole = CourtLayout.NarrowestHoleRadius(settings.RimSegments, settings.RimInnerRadius, settings.RimTubeRadius);
            Assert.Greater(hole, ballRadius,
                $"the ball ({ballRadius * 2f:F3} m across) must fit through the ring ({hole * 2f:F3} m across at its narrowest)");

            // Regulation is a hole 1.91 times the ball across. Much wider than that and the
            // game stops asking anything of the shot.
            float ratio = hole / ballRadius;
            Assert.That(ratio, Is.InRange(1.7f, 2.2f),
                $"the hole is {ratio:F2} times the ball across; regulation is about 1.91");
        }

        [Test]
        public void TheShippedRingSitsAtTheRegulationHeightAndDistanceFromTheBoard()
        {
            CourtSettings settings = LoadSettings();

            Assert.AreEqual(3.048f, settings.RimHeight, 0.01f, "the top of the ring is ten feet up");
            Assert.AreEqual(settings.RimHeight - settings.RimTubeRadius, settings.RimCentreHeight, 1e-6f,
                "the tube's centre hangs one tube radius below its top edge");

            // The rule fixes the gap from the board to the near inside edge of the ring, so
            // the distance to the ring's centre is that gap plus the hole's radius.
            Assert.AreEqual(settings.RimInnerRadius + settings.RimGapFromBackboard, settings.RimCentreToBackboardFace, 1e-6f);
            Assert.AreEqual(0.15f, settings.RimGapFromBackboard, 0.001f, "regulation leaves 15 cm between board and ring");
        }

        [Test]
        public void TheShippedBackboardCoversTheRing()
        {
            CourtSettings settings = LoadSettings();

            Assert.Less(settings.BackboardBottomHeight, settings.RimCentreHeight,
                "the board must reach below the ring or a shot can pass behind it");
            Assert.Greater(settings.BackboardBottomHeight + settings.BackboardHeight, settings.RimHeight,
                "the board must stand above the ring to bank a shot off");
            Assert.Greater(settings.BackboardWidth * 0.5f, settings.RimInnerRadius + settings.RimTubeRadius,
                "the board must be wider than the ring");
        }

        private static CourtSettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CourtSettings>("Assets/Config/CourtSettings.asset");
            Assert.IsNotNull(settings, "Assets/Config/CourtSettings.asset is what the court is built from");
            return settings;
        }

        private static float LoadBallRadius()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Basketball.prefab");
            Assert.IsNotNull(prefab, "Assets/Prefabs/Basketball.prefab is the ball the hoop has to pass");

            var sphere = prefab.GetComponent<SphereCollider>();
            Assert.IsNotNull(sphere, "the ball collides as a sphere");
            Vector3 scale = prefab.transform.localScale;
            return sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
    }
}
