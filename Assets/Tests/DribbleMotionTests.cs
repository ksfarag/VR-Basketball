using NUnit.Framework;
using UnityEngine;

namespace VRBasketball.Tests
{
    /// <summary>
    /// Covers the dribble's timing on its own, without a ball, a hand, or physics, so the
    /// shape of a bounce and its rhythm can be checked with exact numbers.
    /// </summary>
    public class DribbleMotionTests
    {
        private const float Period = 0.5f;
        private const float Step = 0.02f;

        [Test]
        public void NothingMovesUntilADribbleIsAskedFor()
        {
            var dribble = new DribbleMotion();
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(dribble.Advance(Step, Period, false));

            Assert.IsFalse(dribble.IsBouncing);
            Assert.AreEqual(0f, dribble.Drop, "a ball nobody is dribbling stays in the hand");
        }

        [Test]
        public void ABounceGoesFromTheHandToTheFloorAndBack()
        {
            Assert.AreEqual(0f, DribbleMotion.DropAt(0f), "a bounce starts in the hand");
            Assert.AreEqual(1f, DribbleMotion.DropAt(0.5f), "half way through it is on the floor");
            Assert.AreEqual(0f, DribbleMotion.DropAt(1f), "and it ends back in the hand");

            for (float phase = 0.05f; phase < 0.5f; phase += 0.05f)
                Assert.That(DribbleMotion.DropAt(phase), Is.EqualTo(DribbleMotion.DropAt(1f - phase)).Within(1e-5f),
                    "the way up retraces the way down");
        }

        [Test]
        public void TheBallIsSlowInTheHandAndFastAtTheFloor()
        {
            const float nudge = 0.01f;
            float leavingHand = DribbleMotion.DropAt(nudge) - DribbleMotion.DropAt(0f);
            float reachingFloor = DribbleMotion.DropAt(0.5f) - DribbleMotion.DropAt(0.5f - nudge);

            Assert.Greater(reachingFloor, leavingHand * 20f,
                "the hand should take the ball back nearly at rest, not at the speed it hits the floor");
        }

        [Test]
        public void EveryBounceLandsOnAStepOfItsOwn()
        {
            // A step that does not divide the period evenly, so the floor falls between
            // steps unless the dribble waits for it.
            const float awkward = 0.023f;
            var dribble = new DribbleMotion();
            int landings = 0;

            for (int i = 0; i < 200; i++)
            {
                bool landed = dribble.Advance(awkward, Period, true);
                if (!landed)
                    continue;

                landings++;
                Assert.AreEqual(1f, dribble.Drop, "the landing step must put the ball on the floor, not near it");
            }

            Assert.That(landings, Is.InRange(8, 10), "200 steps of 0.023 s is about nine bounces of 0.5 s");
        }

        [Test]
        public void WaitingForTheFloorDoesNotSlowTheRhythm()
        {
            const float awkward = 0.023f;
            var dribble = new DribbleMotion();
            float time = 0f;
            int landings = 0;

            for (int i = 0; i < 2000; i++)
            {
                time += awkward;
                if (!dribble.Advance(awkward, Period, true))
                    continue;

                landings++;
                // Landing n is due at (n - 0.5) periods and is taken on the first step at
                // or after that, so it is never early and never more than a step late,
                // however many bounces have gone before it.
                float due = (landings - 0.5f) * Period;
                Assert.That(time - due, Is.InRange(-1e-3f, awkward + 1e-3f),
                    $"landing {landings} came at {time:F3} s, due at {due:F3} s");
            }
        }

        [Test]
        public void LettingGoFinishesTheBounceInTheHand()
        {
            var dribble = new DribbleMotion();
            dribble.Advance(Step, Period, true);
            Assert.IsTrue(dribble.IsBouncing);

            int steps = 0;
            while (dribble.IsBouncing && steps < 100)
            {
                dribble.Advance(Step, Period, false);
                steps++;
            }

            Assert.IsFalse(dribble.IsBouncing, "a bounce with nobody asking for another must end");
            Assert.AreEqual(0f, dribble.Drop, "it ends with the ball in the hand");
            Assert.That(steps, Is.InRange(23, 25), "the bounce under way runs its full course first; a ball in the air cannot be called back");

            dribble.Advance(Step, Period, false);
            Assert.IsFalse(dribble.IsBouncing, "and no new bounce starts");
        }

        [Test]
        public void StopPutsTheBallBackAtOnce()
        {
            var dribble = new DribbleMotion();
            for (int i = 0; i < 8; i++)
                dribble.Advance(Step, Period, true);
            Assert.Greater(dribble.Drop, 0f);

            dribble.Stop();

            Assert.IsFalse(dribble.IsBouncing);
            Assert.AreEqual(0f, dribble.Drop);
        }
    }
}
