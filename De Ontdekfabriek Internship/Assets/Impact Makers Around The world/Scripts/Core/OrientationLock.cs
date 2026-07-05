using UnityEngine;

namespace KenyaScooter.Core
{
    /// <summary>
    /// Locks the game to a single landscape orientation at startup. Added for the De Ontdekfabriek handover:
    /// on a tablet running unattended at an exhibition we never want the screen to rotate into portrait or to
    /// flip 180 degrees mid-session.
    ///
    /// WHY A SINGLE landscape and not "either landscape": the tilt steering in
    /// <see cref="KenyaScooter.Controls.GyroTiltProvider"/> derives its roll angle per <c>Screen.orientation</c>.
    /// Pinning one concrete orientation (LandscapeLeft) means that switch always takes the matching branch, so
    /// steering is deterministic. Allowing auto-rotation between both landscapes risks Screen.orientation
    /// reporting ambiguously, which would silently invert the steering when the device is physically the other
    /// way up. A fixed orientation removes that whole class of bug and is what a mounted exhibit tablet wants.
    ///
    /// This runs before the first scene loads and needs no scene object. The PROJECT-LEVEL default orientation
    /// (Player Settings &gt; Resolution and Presentation) should also be set to Landscape Left so the splash and
    /// the very first frames are landscape too; this code guarantees the in-game lock regardless of that setting.
    /// </summary>
    public static class OrientationLock
    {
        // Change here (and in Player Settings) if a particular tablet mount needs the other landscape.
        private const ScreenOrientation Target = ScreenOrientation.LandscapeLeft;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // Explicitly forbid every non-target orientation first, then force the target. Setting
            // Screen.orientation to a concrete value already disables auto-rotation; the flags make the intent
            // unmistakable and survive any later code that might re-enable auto-rotation.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Target == ScreenOrientation.LandscapeLeft;
            Screen.autorotateToLandscapeRight = Target == ScreenOrientation.LandscapeRight;
            Screen.orientation = Target;
        }
    }
}
