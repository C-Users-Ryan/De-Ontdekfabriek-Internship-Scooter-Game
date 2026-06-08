using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Attach this to any world object that needs to move toward the player.
    /// Reads RoadDirection.Current each frame so it automatically follows turns.
    ///
    /// REPLACES the manual Translate calls in TrafficVehicle, Pothole, and
    /// RockObstacle. You can either:
    ///   (a) Add this component to those prefabs and remove their own Translate call, or
    ///   (b) Keep the one-liner migration in each script (see RoadDirection.cs header).
    ///
    /// Option (b) is one line per script and cleaner for the existing codebase.
    /// This component is provided as an alternative for new prefabs that don't
    /// have their own movement script.
    ///
    /// USAGE:
    ///   - Attach to any road prop, decorative object, or tile that should scroll.
    ///   - Leave speedMultiplier at 1.0 to move at WorldSpeed.Current.
    ///   - Set speedMultiplier > 1.0 for foreground elements (parallax effect).
    ///   - Set speedMultiplier < 1.0 for background/skybox elements.
    /// </summary>
    public class WorldMover : MonoBehaviour
    {
        [Tooltip("1.0 = moves at WorldSpeed.Current. Adjust for parallax layers.")]
        public float speedMultiplier = 1.0f;

        [Tooltip("If true, this object stops moving when GameState is not Playing " +
                 "(e.g. at checkpoint or game over).")]
        public bool pauseWhenNotPlaying = true;

        void Update()
        {
            if (WorldSpeed.Instance == null || RoadDirection.Instance == null) return;

            if (pauseWhenNotPlaying &&
                GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

            float speed = WorldSpeed.Instance.Current * speedMultiplier;
            transform.Translate(RoadDirection.Current * speed * Time.deltaTime, Space.World);
        }
    }
}
