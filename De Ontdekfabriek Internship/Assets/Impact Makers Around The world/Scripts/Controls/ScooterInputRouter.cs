using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Controls
{
    /// <summary>
    /// Owns all input providers and combines them additively (Req §3.1): gyro + touch
    /// zones on device, keyboard/gamepad/mouse on desktop — simultaneously, so a
    /// developer can keyboard-drive an iPad build. Consumers read the combined values
    /// from ScooterInputRouter.Instance; nothing else touches input devices.
    /// The gyro auto-calibrates at every session start (M8): the student is holding
    /// the iPad naturally at that exact moment, so their grip becomes the zero point.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class ScooterInputRouter : MonoBehaviour
    {
        public static ScooterInputRouter Instance { get; private set; }

        [SerializeField] private InputConfig config;
        [Tooltip("Settings panel toggle (Req §3.4). When off, only touch/keyboard steering applies.")]
        [SerializeField] private bool gyroEnabled = true;

        private GyroTiltProvider gyro;
        private TouchZoneProvider touch;
        private DesktopProvider desktop;

        /// <summary>Combined steering, -1..1.</summary>
        public float Lateral { get; private set; }
        /// <summary>Combined gas, 0..1.</summary>
        public float Gas { get; private set; }
        /// <summary>Combined brake, 0..1.</summary>
        public float Brake { get; private set; }
        /// <summary>Device tilt when the gyro is live, otherwise simulated from steering — feeds Real Rider Mode (M7).</summary>
        public float TiltDegrees { get; private set; }
        /// <summary>Live readout for the settings panel (Req §3.4).</summary>
        public bool GyroActive => gyroEnabled && gyro != null && gyro.Available;

        public bool GyroEnabled
        {
            get => gyroEnabled;
            set => gyroEnabled = value;
        }

        private void Awake()
        {
            Instance = this;
            gyro = new GyroTiltProvider(config);
            touch = new TouchZoneProvider();
            desktop = new DesktopProvider(config);
        }

        private void OnEnable() => GameEvents.SessionStarted += Calibrate;
        private void OnDisable() => GameEvents.SessionStarted -= Calibrate;

        private void Update()
        {
            float dt = Time.deltaTime;
            gyro.Tick(dt);
            touch.Tick(dt);
            desktop.Tick(dt);

            float lateral = desktop.Lateral;
            float tilt = desktop.TiltDegrees;
            if (GyroActive)
            {
                lateral += gyro.Lateral;
                tilt = gyro.TiltDegrees * config.motionSensitivity + desktop.TiltDegrees;
            }

            Lateral = Mathf.Clamp(lateral, -1f, 1f);
            Gas = Mathf.Clamp01(touch.Gas + desktop.Gas);
            Brake = Mathf.Clamp01(touch.Brake + desktop.Brake);
            TiltDegrees = tilt;
        }

        /// <summary>Re-zeroes the gyro to the current grip (M8). Wired to the settings panel and fired on session start.</summary>
        public void Calibrate() => gyro.Calibrate();
    }
}
