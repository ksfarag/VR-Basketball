using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    public class VisualEffectsSceneTests
    {
        private bool previousIgnoreFailingMessages;

        [SetUp]
        public void SetUp()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            // The Meta rig reports missing headset services during editor PlayMode tests.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
        }

        [UnityTest]
        public IEnumerator AShotPlaysTrailImpactAndHoopBurst()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;

            Ball ball = Object.FindFirstObjectByType<Ball>();
            BasketSensor sensor = Object.FindFirstObjectByType<BasketSensor>();
            ScoreKeeper score = Object.FindFirstObjectByType<ScoreKeeper>();
            Assert.IsNotNull(ball);
            Assert.IsNotNull(sensor);
            Assert.IsNotNull(score);
            Assert.IsNotNull(ball.GetComponent<BallEffects>());
            Assert.IsNotNull(sensor.GetComponent<HoopEffects>());
            AudioSource ballSpeaker = ball.GetComponent<AudioSource>();
            Assert.IsNotNull(ballSpeaker, "the ball needs an audio source for collision sounds");

            ParticleSystem trail = null;
            ParticleSystem impact = null;
            ParticleSystem burst = null;
            foreach (ParticleSystem system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (system.name == "Ball Trail") trail = system;
                else if (system.name == "Ball Impact") impact = system;
                else if (system.name == "Score Burst") burst = system;
            }

            Assert.IsNotNull(trail);
            Assert.IsNotNull(impact);
            Assert.IsNotNull(burst);

            ball.Body.position = sensor.transform.position + Vector3.up;
            ball.Body.linearVelocity = Vector3.zero;
            ball.Body.angularVelocity = Vector3.zero;

            bool sawTrail = false;
            bool sawImpact = false;
            bool sawBurst = false;
            bool heardImpact = false;
            for (int i = 0; i < 250 && !(sawTrail && sawImpact && sawBurst && heardImpact); i++)
            {
                yield return new WaitForFixedUpdate();
                sawTrail |= trail.particleCount > 0;
                sawImpact |= impact.particleCount > 0;
                sawBurst |= burst.particleCount > 0;
                heardImpact |= sawImpact && ballSpeaker.isPlaying;
            }

            Assert.AreEqual(1, score.Baskets, "the shot should score once");
            Assert.IsTrue(sawTrail, "the falling ball never emitted a trail");
            Assert.IsTrue(sawImpact, "the ball never emitted a collision puff");
            Assert.IsTrue(sawBurst, "the hoop never emitted a score burst");
            Assert.IsTrue(heardImpact, "the ball never played an impact sound");
        }

        [UnityTest]
        public IEnumerator OnlyTheFinalHandReleaseSignalsAThrow()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;

            Ball ball = Object.FindFirstObjectByType<Ball>();
            HandGrabber[] hands = Object.FindObjectsByType<HandGrabber>(FindObjectsSortMode.None);
            Assert.IsNotNull(ball);
            Assert.GreaterOrEqual(hands.Length, 2);
            int throwCount = 0;
            ball.Thrown += _ => throwCount++;

            ball.Body.position = Vector3.up * 5f;
            Assert.IsTrue(ball.TryHold(hands[0]));
            Assert.IsTrue(ball.TryHold(hands[1]));
            Assert.IsTrue(ball.ReleaseFrom(hands[0], Vector3.forward * 3f, Vector3.zero));
            Assert.AreEqual(0, throwCount, "passing the ball to the other hand is not a throw");

            Assert.IsTrue(ball.ReleaseFrom(hands[1], Vector3.forward * 3f, Vector3.zero));
            Assert.AreEqual(1, throwCount, "the final hand's release should signal one throw");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecallingTheBallPlaysTheWhoosh()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
            yield return null;

            Ball ball = Object.FindFirstObjectByType<Ball>();
            BallRecall recall = Object.FindFirstObjectByType<BallRecall>();
            Assert.IsNotNull(ball);
            Assert.IsNotNull(recall);
            AudioSource speaker = ball.GetComponent<AudioSource>();
            Assert.IsNotNull(speaker);

            Assert.IsTrue(recall.TryRecall());
            Assert.IsTrue(speaker.isPlaying, "starting a recall should play the whoosh on the ball");
        }
    }
}
