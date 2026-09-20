using NUnit.Framework;
using UnityEngine;

namespace VRBasketball.Tests
{
    /// <summary>
    /// The scoring rule, checked without a scene, a ball, or a headset. That is the point
    /// of keeping the rule in plain code: every case below is one a playtest would have to
    /// reproduce by hand, and several of them — a ball crossing the plane between two
    /// physics steps, a ball rattling at the rim — are ones a playtest reproduces by
    /// accident if at all.
    ///
    /// Positions are in the hoop's local space: the ring's centre is the origin, its plane
    /// is y = 0, and up is +y.
    /// </summary>
    public class ScoringTests
    {
        private const float Hole = 0.2286f;
        private const float Arm = 0.12f;

        /// <summary>Feeds a path through the detector and reports how many baskets it counted.</summary>
        private static int Count(BasketDetector detector, params Vector3[] path)
        {
            int baskets = 0;
            for (int i = 0; i < path.Length; i++)
                if (detector.Sample(path[i], Hole, Arm))
                    baskets++;

            return baskets;
        }

        private static Vector3 At(float x, float y) => new Vector3(x, y, 0f);

        [Test]
        public void ADropThroughTheHoleScoresOnce()
        {
            var detector = new BasketDetector();

            int baskets = Count(detector,
                At(0f, 0.9f), At(0f, 0.6f), At(0f, 0.3f), At(0f, -0.1f), At(0f, -0.5f), At(0f, -1f));

            Assert.AreEqual(1, baskets, "a ball dropped down the middle is one basket");
            Assert.IsFalse(detector.Armed, "and the shot is spent once it has been counted");
        }

        [Test]
        public void ABallRisingThroughTheHoleDoesNotScore()
        {
            var detector = new BasketDetector();

            int baskets = Count(detector,
                At(0f, -0.9f), At(0f, -0.5f), At(0f, -0.1f), At(0f, 0.3f), At(0f, 0.7f));

            Assert.AreEqual(0, baskets, "a basket is a downward passage; coming up through the ring is not one");
        }

        [Test]
        public void ABallPassingOutsideTheRingDoesNotScore()
        {
            var detector = new BasketDetector();

            // Clear of the ring altogether, dropping past the side of it.
            int baskets = Count(detector,
                At(0.6f, 0.9f), At(0.6f, 0.5f), At(0.6f, 0.1f), At(0.6f, -0.4f), At(0.6f, -1f));

            Assert.AreEqual(0, baskets, "falling past the outside of the ring is not a basket");
        }

        [Test]
        public void ABallThatHasNotBeenAboveTheRingCannotScore()
        {
            var detector = new BasketDetector();

            // Bounced up off the floor into the hole from underneath and fell back. It
            // crosses the plane downwards, but it was never above the ring to shoot from.
            int baskets = Count(detector,
                At(0f, -0.8f), At(0f, -0.3f), At(0f, 0.05f), At(0f, -0.3f), At(0f, -0.8f));

            Assert.AreEqual(0, baskets, "a ball that only reached the underside of the ring has not made a shot");
        }

        [Test]
        public void RattlingAtTheRimDoesNotScoreTwice()
        {
            var detector = new BasketDetector();

            int baskets = Count(detector,
                At(0f, 0.6f), At(0f, 0.2f), At(0f, -0.2f),
                // Back and forth across the plane, but never far enough above the ring to
                // be a new shot. This is the case a trigger volume gets wrong.
                At(0f, 0.08f), At(0f, -0.1f), At(0f, 0.1f), At(0f, -0.2f), At(0f, 0.05f), At(0f, -0.4f));

            Assert.AreEqual(1, baskets, "one ball through the ring is one basket, however long it lingers there");
        }

        [Test]
        public void GoingBackAboveTheRingArmsTheNextShot()
        {
            var detector = new BasketDetector();

            int baskets = Count(detector,
                At(0f, 0.6f), At(0f, -0.4f),
                At(0f, 0.9f), At(0f, -0.4f));

            Assert.AreEqual(2, baskets, "two shots from above the ring are two baskets");
        }

        [Test]
        public void AFastBallIsJudgedWhereItsPathCrossedThePlane()
        {
            var detector = new BasketDetector();

            // A shot moving fast enough that no sample lands near the plane: the step
            // before is barely above the ring and the step after is a metre below and well
            // outside it. The path between them went through the hole.
            int baskets = Count(detector, At(0f, 0.8f), At(0.2f, 0.1f), At(-0.5f, -0.9f));

            Assert.AreEqual(1, baskets,
                "the crossing is on the path between two steps, not at either end of it");
        }

        [Test]
        public void ABallSeenInsideTheHoleDoesNotScoreIfItsPathCrossedOutside()
        {
            var detector = new BasketDetector();

            // Ends up under the middle of the ring, which is what a check on the current
            // position alone would count. It got there by dropping past the outside and
            // swinging back in, so its path crossed the plane 0.25 m off the axis.
            int baskets = Count(detector, At(0.5f, 0.5f), At(0.5f, 0.3f), At(0f, -0.3f));

            Assert.AreEqual(0, baskets, "where the ball ended up is not where it went through");
        }

        [Test]
        public void ClearForgetsThePathSoAMovedBallIsNotAShot()
        {
            var detector = new BasketDetector();

            Count(detector, At(0f, 0.9f));
            Assert.IsTrue(detector.Armed, "the ball was above the ring");

            // A recall or a reset carries the ball across the court in one jump. The leap
            // from above the ring to below it is not a shot.
            detector.Clear();

            Assert.AreEqual(0, Count(detector, At(0f, -0.9f)), "a ball that was moved has not scored");
        }

        [Test]
        public void ArmingNeedsRealHeightAboveTheRing()
        {
            var detector = new BasketDetector();

            Count(detector, At(0f, Arm * 0.5f));
            Assert.IsFalse(detector.Armed, "hovering just over the ring is not a shot waiting to happen");

            Count(detector, At(0f, Arm * 1.5f));
            Assert.IsTrue(detector.Armed, "clearly above it is");
        }
    }

    /// <summary>
    /// What the readout shows for a given score. The bars are plain geometry driven from
    /// these numbers, so the display can be wrong in exactly two ways — the wrong bars lit,
    /// or bars in the wrong place — and both are checked here rather than by looking at it.
    /// </summary>
    public class SevenSegmentTests
    {
        private static int LitCount(int digit)
        {
            int lit = 0;
            for (int i = 0; i < SevenSegment.Count; i++)
                if (SevenSegment.IsLit(digit, i))
                    lit++;

            return lit;
        }

        [Test]
        public void EachDigitLightsTheRightNumberOfBars()
        {
            int[] expected = { 6, 2, 5, 5, 4, 5, 6, 3, 7, 6 };

            for (int digit = 0; digit <= 9; digit++)
                Assert.AreEqual(expected[digit], LitCount(digit), $"the digit {digit} is the wrong shape");
        }

        [Test]
        public void TheShapesAreTheOnesAReaderExpects()
        {
            Assert.IsFalse(SevenSegment.IsLit(0, SevenSegment.Middle), "a zero is an outline with nothing across it");
            Assert.IsTrue(SevenSegment.IsLit(1, SevenSegment.UpperRight));
            Assert.IsTrue(SevenSegment.IsLit(1, SevenSegment.LowerRight));
            Assert.IsFalse(SevenSegment.IsLit(1, SevenSegment.Top), "a one is the right-hand side and nothing else");
            Assert.IsFalse(SevenSegment.IsLit(7, SevenSegment.Middle), "a seven has no bar across the middle");
            Assert.IsFalse(SevenSegment.IsLit(9, SevenSegment.LowerLeft), "a nine has no tail on the left");

            for (int i = 0; i < SevenSegment.Count; i++)
                Assert.IsTrue(SevenSegment.IsLit(8, i), "an eight lights every bar");
        }

        [Test]
        public void ABlankCellLightsNothing()
        {
            for (int i = 0; i < SevenSegment.Count; i++)
                Assert.IsFalse(SevenSegment.IsLit(SevenSegment.Blank, i));
        }

        [Test]
        public void LeadingZerosAreBlankButAScoreOfNothingStillReadsZero()
        {
            Assert.AreEqual(SevenSegment.Blank, SevenSegment.DigitAt(0, 3, 0));
            Assert.AreEqual(SevenSegment.Blank, SevenSegment.DigitAt(0, 3, 1));
            Assert.AreEqual(0, SevenSegment.DigitAt(0, 3, 2), "the board should read 0, not nothing at all");

            Assert.AreEqual(SevenSegment.Blank, SevenSegment.DigitAt(42, 3, 0));
            Assert.AreEqual(4, SevenSegment.DigitAt(42, 3, 1));
            Assert.AreEqual(2, SevenSegment.DigitAt(42, 3, 2));

            Assert.AreEqual(1, SevenSegment.DigitAt(105, 3, 0), "a zero inside the number is not a leading zero");
            Assert.AreEqual(0, SevenSegment.DigitAt(105, 3, 1));
            Assert.AreEqual(5, SevenSegment.DigitAt(105, 3, 2));
        }

        [Test]
        public void AScoreTooLargeForTheBoardHoldsAtAllNines()
        {
            Assert.AreEqual(999, SevenSegment.Largest(3));

            // Wrapping would show a score smaller than the one that was actually made,
            // which is worse than admitting the board has run out of cells.
            for (int cell = 0; cell < 3; cell++)
                Assert.AreEqual(9, SevenSegment.DigitAt(1234, 3, cell));
        }

        [Test]
        public void EveryBarStaysInsideItsDigit()
        {
            const float Width = 0.2f;
            const float Height = 0.34f;
            const float Thickness = 0.04f;

            for (int i = 0; i < SevenSegment.Count; i++)
            {
                SevenSegment.Bar bar = SevenSegment.Layout(i, Width, Height, Thickness, 0.04f);

                Assert.LessOrEqual(Mathf.Abs(bar.Position.x) + bar.Size.x * 0.5f, Width * 0.5f + 0.0001f,
                    $"bar {i} hangs over the side of its digit");
                Assert.LessOrEqual(Mathf.Abs(bar.Position.y) + bar.Size.y * 0.5f, Height * 0.5f + 0.0001f,
                    $"bar {i} hangs over the top or bottom of its digit");
                Assert.Greater(bar.Size.x * bar.Size.y, 0f, $"bar {i} has no size");
            }
        }
    }

    /// <summary>
    /// Counting, which is deliberately separate from detecting. Nothing here needs a hoop.
    /// </summary>
    public class ScoreKeeperTests
    {
        private ScoreKeeper score;
        private int changes;
        private int lastTotal;
        private int lastChange;

        [SetUp]
        public void SetUp()
        {
            // Left inactive: counting is deliberately free of the component lifecycle, and
            // an enabled keeper would go looking for hoops there are none of here.
            var holder = new GameObject("Score");
            holder.SetActive(false);
            score = holder.AddComponent<ScoreKeeper>();
            score.PointsPerBasket = 2;
            score.Changed += Record;
            changes = 0;
            lastTotal = 0;
            lastChange = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (score != null)
                Object.DestroyImmediate(score.gameObject);
        }

        private void Record(int total, int change)
        {
            changes++;
            lastTotal = total;
            lastChange = change;
        }

        [Test]
        public void ABasketAddsItsPointsAndReportsTheChange()
        {
            score.AddBasket();

            Assert.AreEqual(2, score.Score);
            Assert.AreEqual(1, score.Baskets);
            Assert.AreEqual(1, changes, "one basket is one report");
            Assert.AreEqual(2, lastTotal);
            Assert.AreEqual(2, lastChange, "the change is what a celebration reads to know it was a basket");
        }

        [Test]
        public void PointsPerBasketDecidesWhatABasketIsWorth()
        {
            score.PointsPerBasket = 3;
            score.AddBasket();
            score.AddBasket();

            Assert.AreEqual(6, score.Score);
            Assert.AreEqual(2, score.Baskets, "baskets are counted whatever each was worth");
        }

        [Test]
        public void ResettingClearsTheScoreAndReportsItAsALoss()
        {
            score.AddBasket();
            score.AddBasket();
            score.ResetScore();

            Assert.AreEqual(0, score.Score);
            Assert.AreEqual(0, score.Baskets);
            Assert.AreEqual(-4, lastChange, "a reset must not be mistaken for a basket");
        }

        [Test]
        public void ResettingAnEmptyScoreReportsNothing()
        {
            score.ResetScore();

            Assert.AreEqual(0, changes, "there was nothing to reset, so nothing changed");
        }
    }
}
