using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRBasketball
{
    public enum Hand
    {
        Left,
        Right
    }

    /// <summary>
    /// Enables the gameplay input actions and reports hold and reset input by hand.
    /// Controller tracking is handled separately by the rig's TrackedPoseDrivers.
    /// </summary>
    public sealed class GameplayInput : MonoBehaviour
    {
        [SerializeField] private InputActionReference holdLeft;
        [SerializeField] private InputActionReference holdRight;
        [SerializeField] private InputActionReference reset;
        [Tooltip("Log each hold and reset change with the control that caused it.")]
        [SerializeField] private bool logInput = true;

        public event Action<Hand> HoldStarted;
        public event Action<Hand> HoldEnded;
        public event Action ResetPressed;

        public bool IsHolding(Hand hand) => HoldAction(hand).IsPressed();

        /// <summary>
        /// Whether the reset control is down right now. <see cref="ResetPressed"/> reports
        /// the press; this is how long it is being held for, so a press and a long press can
        /// mean different things without the action itself having to know about either.
        /// </summary>
        public bool IsResetting => reset != null && reset.action != null && reset.action.IsPressed();

        private InputAction HoldAction(Hand hand) => (hand == Hand.Left ? holdLeft : holdRight).action;

        private void OnEnable()
        {
            if (holdLeft == null || holdRight == null || reset == null)
            {
                Debug.LogError("GameplayInput is missing an input action reference.", this);
                enabled = false;
                return;
            }

            holdLeft.action.performed += OnHoldLeft;
            holdLeft.action.canceled += OnHoldLeft;
            holdRight.action.performed += OnHoldRight;
            holdRight.action.canceled += OnHoldRight;
            reset.action.performed += OnReset;
            holdLeft.action.Enable();
            holdRight.action.Enable();
            reset.action.Enable();
        }

        private void OnDisable()
        {
            if (holdLeft == null || holdRight == null || reset == null)
                return;

            holdLeft.action.performed -= OnHoldLeft;
            holdLeft.action.canceled -= OnHoldLeft;
            holdRight.action.performed -= OnHoldRight;
            holdRight.action.canceled -= OnHoldRight;
            reset.action.performed -= OnReset;
            holdLeft.action.Disable();
            holdRight.action.Disable();
            reset.action.Disable();
        }

        private void OnHoldLeft(InputAction.CallbackContext context) => ReportHold(Hand.Left, context);

        private void OnHoldRight(InputAction.CallbackContext context) => ReportHold(Hand.Right, context);

        private void ReportHold(Hand hand, InputAction.CallbackContext context)
        {
            bool pressed = context.performed;
            if (logInput)
                Debug.Log($"[GameplayInput] Hold {hand} {(pressed ? "pressed" : "released")} ({context.control.path})", this);
            if (pressed)
                HoldStarted?.Invoke(hand);
            else
                HoldEnded?.Invoke(hand);
        }

        private void OnReset(InputAction.CallbackContext context)
        {
            if (logInput)
                Debug.Log($"[GameplayInput] Reset pressed ({context.control.path})", this);
            ResetPressed?.Invoke();
        }
    }
}
