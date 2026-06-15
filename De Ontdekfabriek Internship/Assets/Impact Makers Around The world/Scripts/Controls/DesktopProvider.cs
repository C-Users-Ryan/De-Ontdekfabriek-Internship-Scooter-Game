using UnityEngine;
using UnityEngine.InputSystem;
using KenyaScooter.Config;

namespace KenyaScooter.Controls
{
    /// <summary>
    /// Desktop test controls (Req §3.1 — keyboard fallback MUST work for PC testing).
    /// Keyboard: A/D or arrows steer, W/Up gas, S/Down brake. Held keys ramp the steer
    /// value so the feel approximates gradual gyro tilt. Mouse buttons mimic the tablet
    /// touch zones (left half = brake, right half = gas) so PC user tests exercise the
    /// same two-zone layout as the iPad. A gamepad (Bluetooth steering wheel) maps
    /// left stick = steer, right trigger = gas, left trigger = brake.
    /// TiltDegrees is simulated from the steer value so Real Rider Mode can be
    /// previewed on desktop (M7).
    /// </summary>
    public sealed class DesktopProvider : IScooterInput
    {
        private readonly InputConfig config;
        private float steer;

        public float Lateral => steer;
        public float Gas { get; private set; }
        public float Brake { get; private set; }
        public float TiltDegrees => steer * config.gyroMaxTiltDegrees;
        public bool Available => Keyboard.current != null || Gamepad.current != null || Mouse.current != null;

        public DesktopProvider(InputConfig config)
        {
            this.config = config;
        }

        public void Tick(float deltaTime)
        {
            float steerTarget = 0f;
            float gas = 0f;
            float brake = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steerTarget -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steerTarget += 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) gas = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) brake = 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                float stick = gamepad.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.1f) steerTarget = Mathf.Clamp(steerTarget + stick, -1f, 1f);
                gas = Mathf.Max(gas, gamepad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, gamepad.leftTrigger.ReadValue());
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                if (mouse.position.ReadValue().x >= Screen.width * 0.5f) gas = 1f;
                else brake = 1f;
            }

            // Ramp toward the held direction, return to centre when released —
            // digital keys get an analogue feel comparable to tilting.
            float rate = Mathf.Approximately(steerTarget, 0f) ? config.keyboardCentreRate : config.keyboardSteerRate;
            steer = Mathf.MoveTowards(steer, steerTarget, rate * deltaTime);

            Gas = gas;
            Brake = brake;
        }

        public void Calibrate() { }
    }
}
