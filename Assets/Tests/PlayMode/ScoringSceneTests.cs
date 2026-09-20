using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// The saved scene, running. The rule itself is checked elsewhere; what is checked here
    /// is that the scene actually has a sensor on the hoop, a keeper listening to it, and a
    /// board wired to show it — the failures that no amount of correct code prevents and
    /// that only turn up when someone puts a headset on.
    ///
    /// The scene brings Meta XR Core's rig with it, and that complains when it starts
    /// without a headset attached, so log failures are ignored for the run rather than
    /// letting somebody else's warning fail a check about scoring.
    /// </summary>
    public class ScoringSceneTests
    {
        private const string SceneName = "Gameplay";

        private bool logs;

        [SetUp]
        public void SetUp()
        {
            logs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = logs;
        }

        [UnityTest]
        public IEnumerator TheSceneIsWiredForScoring()
        {
            yield return LoadScene();

            BasketSensor sensor = Object.FindFirstObjectByType<BasketSensor>();
            ScoreKeeper score = Object.FindFirstObjectByType<ScoreKeeper>();
            Scoreboard board = Object.FindFirstObjectByType<Scoreboard>();

            Assert.IsNotNull(sensor, "the hoop has nothing watching for a shot through it");
            Assert.IsNotNull(score, "nothing in the scene counts baskets");
            Assert.IsNotNull(board, "nothing in the scene shows the score");

            Assert.Greater(sensor.PassRadius, 0f, "the sensor has no hole to look through");
            Assert.Greater(sensor.ArmHeight, 0f, "the sensor would count a ball resting at the ring over and over");

            CollectionAssert.Contains(score.Sensors, sensor, "the keeper is not listening to the hoop in the scene");
            Assert.AreSame(score, board.Score, "the board is not showing the score that is being kept");

            Assert.IsNotNull(board.Digits);
            Assert.Greater(board.Digits.Length, 0, "the board has no digits");
            for (int cell = 0; cell < board.Digits.Length; cell++)
            {
                Renderer[] segments = board.Digits[cell].Segments;
                Assert.AreEqual(SevenSegment.Count, segments.Length, $"digit {cell} does not have seven bars");

                for (int i = 0; i < segments.Length; i++)
                    Assert.IsNotNull(segments[i], $"bar {i} of digit {cell} is missing");
            }
        }

        [UnityTest]
        public IEnumerator AShotThroughTheSceneHoopScoresOnce()
        {
            yield return LoadScene();

            BasketSensor sensor = Object.FindFirstObjectByType<BasketSensor>();
            ScoreKeeper score = Object.FindFirstObjectByType<ScoreKeeper>();
            Ball ball = Object.FindFirstObjectByType<Ball>();

            Assert.IsNotNull(sensor, "the scene has no hoop sensor to shoot through");
            Assert.IsNotNull(ball, "the scene has no ball to shoot");
            Assert.AreEqual(0, score.Score, "the scene should start at nothing");

            // Dropped down the middle of the scene's own hoop, wherever the court has been
            // put, rather than at a position written down here that the court can drift
            // away from.
            ball.Body.position = sensor.transform.position + Vector3.up * 1f;
            ball.Body.linearVelocity = Vector3.zero;
            ball.Body.angularVelocity = Vector3.zero;

            for (int i = 0; i < 250; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, score.Baskets, "the shot should have scored exactly once");
            Assert.AreEqual(score.PointsPerBasket, score.Score);
            Assert.Less(ball.Body.position.y, sensor.transform.position.y,
                "and the ball should be below the ring, not sitting on it");
        }

        private static IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            // One frame for every OnEnable in the scene to have run and subscribed.
            yield return null;
        }
    }
}
