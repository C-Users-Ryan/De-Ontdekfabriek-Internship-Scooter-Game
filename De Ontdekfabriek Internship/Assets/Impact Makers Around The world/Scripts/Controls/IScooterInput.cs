namespace KenyaScooter.Controls
{
    /// <summary>
    /// One source of input (gyro, touch zones, or keyboard/gamepad). Each provider is a plain C#
    /// class that ScooterInputRouter ticks and adds together, so several can run at once
    /// (Req §3.1 — e.g. keyboard and gyro both work while testing).
    /// </summary>
    public interface IScooterInput
    {
        /// <summary>Steering, -1 (left) to 1 (right).</summary>
        float Lateral { get; }

        /// <summary>Gas, 0–1 (M4: right touch zone on device).</summary>
        float Gas { get; }

        /// <summary>Brake, 0–1 (M4: left touch zone on device).</summary>
        float Brake { get; }

        /// <summary>Physical or simulated device tilt in degrees — feeds Real Rider Mode (M7).</summary>
        float TiltDegrees { get; }

        /// <summary>True when the underlying device exists this frame.</summary>
        bool Available { get; }

        void Tick(float deltaTime);

        /// <summary>Make the current pose the zero reference (M8). No-op for non-sensor providers.</summary>
        void Calibrate();
    }
}
