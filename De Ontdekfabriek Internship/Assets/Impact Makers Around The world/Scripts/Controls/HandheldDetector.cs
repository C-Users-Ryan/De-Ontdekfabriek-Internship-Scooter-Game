using UnityEngine;

namespace KenyaScooter.Controls
{
    /// <summary>
    /// Decides whether the tablet is being held in someone's HANDS (so the on-screen gas/brake buttons should
    /// show) or is sitting in a HOLDER / on a dock (so the screen stays clean and the player just tilts to steer
    /// and taps the bare touch zones). From the 25 June 2026 oplevering: the client plays the game BOTH in a
    /// holder and handheld, and asked for gas/brake indicators for the handheld case.
    ///
    /// The signal is the CHARGING STATE. A kiosk/exhibition tablet sitting in its holder is almost always on the
    /// charger, while a hand-held one runs on battery. It is the only holder-vs-handheld signal Unity exposes with
    /// no native plugin, and it degrades safely: if a particular tablet always reports "charging", the facilitator
    /// override (<see cref="Mode"/>) forces the buttons on or off. Orientation/proximity were considered and
    /// rejected — the game is orientation-locked to one landscape already (see OrientationLock), so device pose is
    /// not a reliable docked signal, and Unity has no proximity API.
    ///
    /// This is a plain static helper with no GameObject of its own; <see cref="Tick"/> is called once per frame by
    /// HandheldControlsHud (which always exists, self-bootstrapped). Keeping it static means the how-to overlay and
    /// the settings menu can read the same answer without a scene reference.
    /// </summary>
    public static class HandheldDetector
    {
        /// <summary>Facilitator override, stored in PlayerPrefs. 0 = auto-detect, 1 = always show, 2 = never show.</summary>
        public const string ModePrefKey = "ksg.controlsmode";
        public const int ModeAuto = 0, ModeAlways = 1, ModeNever = 2;

        // The raw charging signal must hold for this long before we flip the committed answer, so plugging /
        // unplugging (or a momentary "not charging" blip) does not make the buttons flash on and off.
        private const float DebounceSeconds = 1f;

        private static bool rawHandheld;
        private static bool stableHandheld = true; // default to "handheld" so the buttons are visible until proven docked
        private static float stableSince;
        private static bool initialised;

        /// <summary>Debounced best guess of "the tablet is in someone's hands", ignoring the override.</summary>
        public static bool IsHandheld => stableHandheld;

        /// <summary>The facilitator override (auto / always / never), read live from PlayerPrefs.</summary>
        public static int Mode => PlayerPrefs.GetInt(ModePrefKey, ModeAuto);

        /// <summary>Whether the on-screen gas/brake buttons should be visible right now, honouring the override.</summary>
        public static bool ShowControls
        {
            get
            {
                switch (Mode)
                {
                    case ModeAlways: return true;
                    case ModeNever:  return false;
                    default:         return stableHandheld;
                }
            }
        }

        /// <summary>
        /// Reads the charging state and debounces it into <see cref="IsHandheld"/>. Call once per frame.
        /// Uses unscaled time so it keeps working while the game is paused on a menu.
        /// </summary>
        public static void Tick()
        {
            bool raw = ReadHandheldRaw();
            float now = Time.unscaledTime;

            if (!initialised)
            {
                stableHandheld = raw;
                rawHandheld = raw;
                stableSince = now;
                initialised = true;
                return;
            }

            if (raw != rawHandheld)
            {
                rawHandheld = raw;   // signal just changed — (re)start the debounce window
                stableSince = now;
            }
            else if (raw != stableHandheld && now - stableSince >= DebounceSeconds)
            {
                stableHandheld = raw; // the new reading has held long enough — commit it
            }
        }

        // Discharging => running on battery => handheld. Charging / Full / NotCharging => connected to power =>
        // docked in the holder. Unknown (the editor and most desktops report this) => handheld, so the buttons are
        // visible while testing on a PC.
        private static bool ReadHandheldRaw()
        {
            switch (SystemInfo.batteryStatus)
            {
                case BatteryStatus.Charging:
                case BatteryStatus.Full:
                case BatteryStatus.NotCharging:
                    return false;
                default: // Discharging, Unknown
                    return true;
            }
        }
    }
}
