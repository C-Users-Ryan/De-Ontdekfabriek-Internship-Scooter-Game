using UnityEngine;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// The ACCESS CODE that keeps students out of the facilitator menu. The hot-corner gesture
    /// (<see cref="FacilitatorGate"/>) is what a child never triggers by accident; this PIN is the second
    /// wall, so even a child who discovers the gesture cannot change the game. Staff type a short numeric
    /// code on an on-screen keypad (no hardware keyboard needed on a tablet).
    ///
    /// Stored as DATA in PlayerPrefs, exactly like every other facilitator override (see GameSettings), so it
    /// survives app restarts and needs no asset edit:
    ///   - "ksg.lock.on"  : 1/0  — whether the code is required at all (a trusted, locked-down venue can turn
    ///                      it off; default ON because the brief is "put a code on it").
    ///   - "ksg.lock.pin" : the current code as a string of digits.
    ///
    /// FOOL-PROOFING / lock-out recovery (documented for staff, never shown to students):
    ///   - The code can be changed from inside the menu (Beheer > Toegangscode wijzigen).
    ///   - If the code is ever forgotten at a live event, hold the padlock on the code screen for
    ///     <see cref="RecoveryHoldSeconds"/> seconds to put it back to the default. The menu wires that hold.
    ///   - On a dev machine, Tools > Kenya Scooter > Facilitator > Reset Access Code clears it back to the default too.
    ///
    /// The default code is <see cref="DefaultPin"/> — "3683" spells D-O-T-F (De Ontdek-Fabriek) on a phone
    /// keypad, so staff can remember it, but it is not an obvious 1-2-3-4 a curious student would try first.
    /// Change it on first use for a real venue.
    ///
    /// THREAT MODEL (a deliberate, documented choice — not an oversight): the adversary here is a curious CHILD on a
    /// kiosk tablet, not a remote attacker. The code is stored as PLAINTEXT in PlayerPrefs on purpose:
    ///   - A 4-digit code has only 10 000 values, so hashing buys almost nothing against anyone determined.
    ///   - The real defence is layered and physical: the hidden corner-hold gesture, a locked-down kiosk tablet,
    ///     and staff supervision — not the storage format.
    ///   - On-device, no-developer RECOVERY must always be possible (a forgotten code at a live event), which a
    ///     one-way hash actively fights. Plaintext + a documented reset gesture is the safer real-world trade-off.
    /// If this game were ever repurposed to gate something that actually mattered, this is the line to revisit.
    /// </summary>
    public static class FacilitatorLock
    {
        public const string DefaultPin = "3683"; // D-O-T-F on a phone keypad; documented, change on first use
        public const int PinLength = 4;
        public const float RecoveryHoldSeconds = 4f;

        private const string EnabledKey = "ksg.lock.on";
        private const string PinKey = "ksg.lock.pin";

        /// <summary>True when a code is required to open the facilitator menu. Default ON.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The current code, falling back to the documented default if none was ever set.</summary>
        public static string CurrentPin => PlayerPrefs.GetString(PinKey, DefaultPin);

        /// <summary>True if <paramref name="entered"/> matches the current code.</summary>
        public static bool Verify(string entered) => !string.IsNullOrEmpty(entered) && entered == CurrentPin;

        /// <summary>True for a well-formed candidate code (exactly the required number of digits).</summary>
        public static bool IsValidPin(string pin)
        {
            if (string.IsNullOrEmpty(pin) || pin.Length != PinLength) return false;
            for (int i = 0; i < pin.Length; i++)
                if (pin[i] < '0' || pin[i] > '9') return false;
            return true;
        }

        /// <summary>Stores a new code. Ignored unless it is well-formed, so a half-typed code can never lock staff out.</summary>
        public static bool SetPin(string newPin)
        {
            if (!IsValidPin(newPin)) return false;
            PlayerPrefs.SetString(PinKey, newPin);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Puts the code back to the documented default (the lock-out recovery and the editor reset both call this).</summary>
        public static void ResetToDefault()
        {
            PlayerPrefs.SetString(PinKey, DefaultPin);
            PlayerPrefs.Save();
        }
    }
}
