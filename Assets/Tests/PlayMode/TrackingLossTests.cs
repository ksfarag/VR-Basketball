using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.TestTools;

namespace VRBasketball.Tests.PlayMode
{
    /// <summary>
    /// Drives a virtual tracked controller through the project's own
    /// <c>Tracked Right</c> binding, so losing tracking is exercised the way the headset
    /// would report it rather than through a stand-in flag.
    /// </summary>
    public class TrackingLossTests
    {
        private GrabSettings settings;
        private Ball ball;
        private HandGrabber right;
        private XRController controller;
        private InputActionReference trackedRight;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            trackedRight = LoadTrackedRightReference();
#endif
            if (trackedRight == null)
                Assert.Ignore("The Tracked Right action reference is only reachable in the Editor.");

            // The binding is <XRController>{RightHand}/isTracked, so the device needs that
            // usage before the action resolves against it.
            controller = InputSystem.AddDevice<XRController>();
            InputSystem.SetDeviceUsage(controller, CommonUsages.RightHand);
            SetTracked(true);

            settings = ScriptableObject.CreateInstance<GrabSettings>();

            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Test Ball";
            ballObject.transform.position = new Vector3(0f, 5f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.239f;
            ballObject.AddComponent<Rigidbody>().mass = 0.62f;
            ball = ballObject.AddComponent<Ball>();

            GameObject hand = new GameObject("Right Hand");
            hand.SetActive(false);
            hand.transform.position = new Vector3(0f, 5f, 0f);
            right = hand.AddComponent<HandGrabber>();
            right.Settings = settings;
            right.Hand = Hand.Right;
            typeof(HandGrabber).GetField("tracked", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(right, trackedRight);
            hand.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (ball != null) Object.Destroy(ball.gameObject);
            if (right != null) Object.Destroy(right.gameObject);
            if (settings != null) Object.Destroy(settings);
            if (controller != null && controller.added) InputSystem.RemoveDevice(controller);
            if (trackedRight != null && trackedRight.action != null) trackedRight.action.Disable();
        }

        [UnityTest]
        public IEnumerator TrackedHandPicksUpTheBall()
        {
            Assert.IsTrue(right.IsTracked, "the virtual controller reports tracking");

            right.BeginHold();
            yield return Steps(2);

            Assert.AreSame(ball, right.Held, "a tracked hand should pick the ball up");
        }

        [UnityTest]
        public IEnumerator LosingTrackingDropsTheHeldBall()
        {
            right.BeginHold();
            yield return Steps(2);
            Assert.AreSame(ball, right.Held, "the drop needs a ball in hand first");

            SetTracked(false);
            yield return Steps(2);

            Assert.IsFalse(right.IsTracked, "the hand should see the lost tracking");
            Assert.IsNull(right.Held, "a hand that loses tracking must let go");
            Assert.IsNull(ball.Holder, "ownership must be released, not just forgotten");
            Assert.IsTrue(ball.Body.useGravity, "the dropped ball falls again");
        }

        [UnityTest]
        public IEnumerator UntrackedHandCannotPickUpTheBall()
        {
            SetTracked(false);
            yield return Steps(1);

            right.BeginHold();
            yield return Steps(3);

            Assert.IsNull(right.Held, "an untracked hand must not grab");
            Assert.IsNull(ball.Holder);
        }

        [UnityTest]
        public IEnumerator TrackingReturningLetsTheHandGrabAgain()
        {
            SetTracked(false);
            right.BeginHold();
            yield return Steps(2);
            Assert.IsNull(right.Held, "nothing is grabbed while untracked");

            SetTracked(true);
            yield return Steps(3);

            Assert.AreSame(ball, right.Held, "the hand recovers once tracking returns");
        }

        // isTracked is a single bit, so it is written through a full state event rather
        // than a delta, which would mismatch the control's size.
        private void SetTracked(bool tracked)
        {
            using (StateEvent.From(controller, out InputEventPtr eventPtr))
            {
                controller.isTracked.WriteValueIntoEvent(tracked ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }

            InputSystem.Update();
        }

        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }

#if UNITY_EDITOR
        // The visible reference, not the importer's hidden back-compat twin.
        private static InputActionReference LoadTrackedRightReference()
        {
            const string path = "Assets/Input/BasketballControls.inputactions";
            foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is InputActionReference reference
                    && (reference.hideFlags & HideFlags.HideInHierarchy) == 0
                    && reference.name == "Gameplay/Tracked Right")
                {
                    return reference;
                }
            }

            return null;
        }
#endif
    }
}
