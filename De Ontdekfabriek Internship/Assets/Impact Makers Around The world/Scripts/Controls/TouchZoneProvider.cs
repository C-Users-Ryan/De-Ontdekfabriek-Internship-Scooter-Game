using UnityEngine.InputSystem;

namespace KenyaScooter.Controls
{
    /// <summary>
    /// Gas and brake by touch (M4): right half of the screen is gas, left half is brake. Any active
    /// touch counts, so a student resting a second finger doesn't break it. Steering on the tablet
    /// comes from GyroTiltProvider, not here.
    /// </summary>
    public sealed class TouchZoneProvider : IScooterInput
    {
        public float Lateral => 0f;
        public float Gas { get; private set; }
        public float Brake { get; private set; }
        public float TiltDegrees => 0f;
        public bool Available => Touchscreen.current != null;

        public void Tick(float deltaTime)
        {
            Gas = 0f;
            Brake = 0f;

            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return;

            float halfWidth = UnityEngine.Screen.width * 0.5f;
            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (!touch.press.isPressed)
                    continue;

                if (touch.position.ReadValue().x >= halfWidth)
                    Gas = 1f;
                else
                    Brake = 1f;
            }
        }

        public void Calibrate() { }
    }
}
