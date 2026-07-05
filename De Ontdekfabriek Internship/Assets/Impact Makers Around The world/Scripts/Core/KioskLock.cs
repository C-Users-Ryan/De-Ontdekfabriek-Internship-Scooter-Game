using UnityEngine;

namespace KenyaScooter.Core
{
    /// <summary>
    /// Stops the Android hardware BACK button from dropping a child out of the game on an unattended
    /// exhibition tablet. Companion to <see cref="OrientationLock"/>, added for the De Ontdekfabriek
    /// handover (kiosk-lockdown theme).
    ///
    /// TWO LAYERS, and only the first is a HARD guarantee:
    ///
    ///   1. OS screen-pinning ("App pinning" / "Vastzetten" in Android Settings &gt; Security) is the REAL
    ///      kiosk lock. While the app is pinned, Android hides and neutralises HOME, RECENTS and BACK at the
    ///      system level, so no app code is needed and a child cannot leave. This is set up per device (see
    ///      the tablet-setup checklist in the handover docs) and CANNOT be replaced by code. Always turn it on.
    ///
    ///   2. This script is the in-app belt-and-braces for a device where staff have NOT pinned the app yet:
    ///      it tells Android never to quit or background the app on BACK
    ///      (<c>Input.backButtonLeavesApp = false</c>). On Android, BACK is otherwise delivered as the Escape
    ///      key and, depending on the build, can minimise the app. Forcing this off means a stray BACK press
    ///      keeps the child in the game.
    ///
    /// WHY A STATIC, RUN-BEFORE-SCENE-LOAD HOOK (and not a MonoBehaviour): the only reliable, additive code
    /// action is this one player-level flag, set once at startup. It mirrors <see cref="OrientationLock"/>
    /// exactly, needs no scene object and no per-frame work, and is purely SUPPRESSIVE (it can only ever
    /// prevent an exit), so it cannot change gameplay or put the liked straight build at any risk.
    ///
    /// KNOWN INTERACTION, documented for the next developer (not a bug, and harmless under screen-pinning):
    /// because BACK arrives as Escape and <c>GameManager.StartPressed()</c> treats "any key" as a start, a
    /// BACK press on the title screen could begin a game when the app is NOT pinned. With screen-pinning on
    /// (the recommended setup) the OS swallows BACK before it reaches the app, so this never arises. If a
    /// future team wants BACK fully inert without pinning, the minimal change is to exclude the Escape key
    /// from that one "any key" check in <c>GameManager</c>; it was deliberately left untouched here to keep
    /// this change additive.
    /// </summary>
    public static class KioskLock
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            // The documented Android switch for exactly this purpose: never leave/minimise the app on BACK.
            // Available because Active Input Handling is "Both" on this project; guarded so the file still
            // compiles unchanged if a future team switches the project to the New Input System only (the
            // legacy Input class is then absent). Losing this line is safe: screen-pinning (layer 1) remains
            // the real lock.
            Input.backButtonLeavesApp = false;
#endif
        }
    }
}
