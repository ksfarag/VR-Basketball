using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace VRBasketball
{
    /// <summary>
    /// Check aid for headset input. Logs every button and stick change on the tracked
    /// controllers, including controls no gameplay action uses, so a manual pass over the
    /// controllers shows which physical control maps to which path. Disable for normal play.
    /// </summary>
    public sealed class XrInputProbe : MonoBehaviour
    {
        [Tooltip("Stick or analog travel needed before a change is logged.")]
        [SerializeField, Range(0.1f, 0.9f)] private float axisThreshold = 0.5f;

        private readonly HashSet<InputControl> active = new HashSet<InputControl>();
        private readonly HashSet<InputDevice> announced = new HashSet<InputDevice>();

        private void Update()
        {
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!(device is XRController) || !device.added)
                    continue;

                Announce(device);

                foreach (InputControl control in device.allControls)
                {
                    if (control.noisy || control.synthetic)
                        continue;

                    if (control is ButtonControl button)
                        ReportButton(device, button);
                    else if (control is Vector2Control stick)
                        ReportAxis(device, stick, stick.ReadValue().magnitude, stick.ReadValue().ToString("F2"));
                    else if (control is AxisControl axis && !(axis.parent is Vector2Control))
                        ReportAxis(device, axis, Mathf.Abs(axis.ReadValue()), axis.ReadValue().ToString("F2"));
                }
            }
        }

        private void OnDisable()
        {
            active.Clear();
            announced.Clear();
        }

        // Lists what the runtime offers, so a control that never logs can be told from one that is missing.
        private void Announce(InputDevice device)
        {
            if (!announced.Add(device))
                return;

            var names = new List<string>();
            foreach (InputControl control in device.allControls)
            {
                if (control.noisy || control.synthetic)
                    continue;
                if (control is ButtonControl || control is Vector2Control || (control is AxisControl && !(control.parent is Vector2Control)))
                    names.Add(control.name);
            }

            Debug.Log($"[XrInputProbe] {Hand(device)} controller '{device.name}' offers: {string.Join(", ", names)}", this);
        }

        private void ReportButton(InputDevice device, ButtonControl button)
        {
            bool pressed = button.isPressed;
            if (pressed == active.Contains(button))
                return;

            if (pressed)
                active.Add(button);
            else
                active.Remove(button);

            Debug.Log($"[XrInputProbe] {Hand(device)} {button.name} {(pressed ? "pressed" : "released")} ({button.path})", this);
        }

        private void ReportAxis(InputDevice device, InputControl control, float magnitude, string value)
        {
            bool moved = magnitude >= axisThreshold;
            if (moved == active.Contains(control))
                return;

            if (moved)
                active.Add(control);
            else
                active.Remove(control);

            Debug.Log($"[XrInputProbe] {Hand(device)} {control.name} {(moved ? "moved to " + value : "back to rest")} ({control.path})", this);
        }

        private static string Hand(InputDevice device)
        {
            foreach (var usage in device.usages)
            {
                if (usage == CommonUsages.LeftHand)
                    return "Left";
                if (usage == CommonUsages.RightHand)
                    return "Right";
            }

            return "Unassigned";
        }
    }
}
