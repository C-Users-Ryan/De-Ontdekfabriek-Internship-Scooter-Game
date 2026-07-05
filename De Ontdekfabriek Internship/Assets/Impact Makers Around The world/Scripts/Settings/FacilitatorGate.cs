using UnityEngine;
using UnityEngine.InputSystem;

namespace KenyaScooter.Settings
{
    /// <summary>
    /// The child-proof / staff-easy way INTO the settings menu, designed for a tablet with no keyboard.
    ///
    /// THE GESTURE (documented for the facilitator):
    ///   Press and HOLD the TOP-LEFT corner of the screen for 2 seconds.
    /// A corner press is something a child never does by accident mid-game (they steer by tilting and tap
    /// to start), and holding it for a full 2 s rules out a stray touch. The hit area is a small corner
    /// box, off to the side of every gameplay control. On desktop, F8 opens it too (for testing without a
    /// touchscreen) — Tab is already taken by the existing DebugPanel.
    ///
    /// The gesture is only the FIRST wall: it routes through <see cref="SettingsMenu.RequestOpen"/>, which then
    /// asks for the numeric ACCESS CODE (<see cref="FacilitatorLock"/>) before revealing the settings, so a child
    /// who happens to find the corner-hold still cannot change the game. A venue can turn the code off in Beheer.
    ///
    /// Self-bootstraps: one instance is created after the scene loads if none was placed by hand, the same
    /// pattern the scoring managers and HapticFeedback use, so the gate needs zero scene wiring. It finds
    /// the SettingsMenu the same way (creating one if absent) and opens it.
    /// </summary>
    public sealed class FacilitatorGate : MonoBehaviour
    {
        [Tooltip("Seconds the corner must be held before the menu opens.")]
        [SerializeField] private float holdSeconds = 2f;
        [Tooltip("Size of the square hot-corner as a fraction of the shorter screen side.")]
        [SerializeField, Range(0.05f, 0.3f)] private float cornerFraction = 0.12f;

        private float held;
        private SettingsMenu menu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<FacilitatorGate>() != null)
                return;
            var go = new GameObject("FacilitatorGate (auto)");
            go.AddComponent<FacilitatorGate>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            // Desktop shortcut for testing on a machine with no touchscreen.
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                Open();
                return;
            }

            if (IsHoldingHotCorner())
            {
                held += Time.unscaledDeltaTime;
                if (held >= holdSeconds)
                {
                    held = float.NegativeInfinity; // one-shot until released
                    Open();
                }
            }
            else if (!float.IsNegativeInfinity(held))
            {
                held = 0f;
            }
            else if (!AnyCornerTouch())
            {
                held = 0f; // re-arm once the finger leaves
            }
        }

        private bool IsHoldingHotCorner()
        {
            Vector2 box = CornerBox();

            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (!touches[i].press.isPressed) continue;
                    if (InTopLeft(touches[i].position.ReadValue(), box)) return true;
                }
            }

            // Mouse fallback (Editor / desktop), so the same gesture is testable with a click-hold.
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.isPressed && InTopLeft(m.position.ReadValue(), box))
                return true;

            return false;
        }

        private bool AnyCornerTouch()
        {
            Touchscreen ts = Touchscreen.current;
            if (ts != null)
            {
                var touches = ts.touches;
                for (int i = 0; i < touches.Count; i++)
                    if (touches[i].press.isPressed) return true;
            }
            Mouse m = Mouse.current;
            return m != null && m.leftButton.isPressed;
        }

        private Vector2 CornerBox()
        {
            float side = Mathf.Min(Screen.width, Screen.height) * cornerFraction;
            return new Vector2(side, side);
        }

        // Screen-space origin is bottom-left, so "top-left" is x small, y large.
        private static bool InTopLeft(Vector2 pos, Vector2 box)
            => pos.x <= box.x && pos.y >= Screen.height - box.y;

        private void Open()
        {
            if (menu == null)
                menu = SettingsMenu.GetOrCreate();
            if (menu != null)
                menu.RequestOpen(); // shows the access-code keypad first when a code is required
        }
    }
}
