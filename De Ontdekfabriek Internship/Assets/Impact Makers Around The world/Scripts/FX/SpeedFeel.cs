using UnityEngine;
using KenyaScooter.Core;
using KenyaScooter.Config;
using KenyaScooter.Settings;

namespace KenyaScooter.FX
{
    /// <summary>
    /// The single shared "how fast does it FEEL" signal that the speed-driven dust layers read (the Levend
    /// Kenia SlipstreamDust, the scooter contact dust and the truck wash), so they all agree on one number
    /// and one on/off switch instead of each re-deriving speed and drifting apart.
    ///
    /// The signal is OVER-CRUISE, not absolute speed: 0 at or below the cruising (base) speed and 1 at
    /// max. The effect therefore stays calm while coasting and blooms as the child opens the throttle —
    /// the same "measured from base" idea the camera FOV kick already uses, so all the speed cues move
    /// together. It also returns 0 unless a turn is actually live, so framing screens stay still.
    ///
    /// Two global dampeners keep it child-safe and accessible, both owned here so one switch calms the
    /// whole composition:
    ///   - <see cref="EffectsEnabled"/> — the facilitator "Snelheidsbeleving" toggle (PlayerPrefs, ON by
    ///     default), which hard-zeroes every layer.
    ///   - the existing motion-sensitivity dial (InputConfig.motionSensitivity — the
    ///     "Bewegingsgevoeligheid" slider and the Prikkelarm preset), which scales every layer DOWN for
    ///     motion-sensitive players, exactly as it already tempers steering and the Real Rider camera.
    /// </summary>
    public static class SpeedFeel
    {
        public const string EnabledPrefKey = "ksg.speedfx";

        private static int _enabled = -1; // lazy PlayerPrefs cache (-1 = not yet read)

        /// <summary>Facilitator master switch for the speed-rush visuals. ON by default; persisted in PlayerPrefs
        /// (the same mechanism the haptics and Real Rider toggles use), so the choice survives an app restart.</summary>
        public static bool EffectsEnabled
        {
            get
            {
                if (_enabled < 0)
                    _enabled = PlayerPrefs.GetInt(EnabledPrefKey, 1);
                return _enabled != 0;
            }
            set
            {
                _enabled = value ? 1 : 0;
                PlayerPrefs.SetInt(EnabledPrefKey, _enabled);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Raw over-cruise fraction (0 at or below base speed, 1 at max) while a turn is live, else 0.</summary>
        public static float OverCruise
        {
            get
            {
                if (GameManager.State != GameState.Playing && GameManager.State != GameState.AtCheckpoint)
                    return 0f;
                WorldSpeed ws = WorldSpeed.Instance;
                if (ws == null)
                    return 0f;
                return Mathf.Clamp01(Mathf.InverseLerp(ws.BaseSpeed, ws.MaxSpeed, ws.Current));
            }
        }

        /// <summary>How much motion the player asked for (1 = full, down to 0.3 on the calm slider). 1 if unknown.</summary>
        public static float MotionScale
        {
            get
            {
                InputConfig input = ConfigLocator.Input;
                return input != null ? Mathf.Clamp01(input.motionSensitivity) : 1f;
            }
        }

        /// <summary>
        /// The master 0..1 drive every layer multiplies into its own peak. It is the over-cruise fraction
        /// eased to a gentle S-curve (no hard knee at cruise), then gated by the toggle and damped by motion
        /// sensitivity. 0 means "draw nothing"; each layer eases toward this so it never snaps.
        /// </summary>
        public static float Drive
        {
            get
            {
                if (!EffectsEnabled)
                    return 0f;
                return Mathf.SmoothStep(0f, 1f, OverCruise) * MotionScale;
            }
        }
    }
}
