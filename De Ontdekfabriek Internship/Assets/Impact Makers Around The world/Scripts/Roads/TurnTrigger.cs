using UnityEngine;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// OBSOLETE as of the 2026-06-17 turn rebuild. Turns are no longer a collider event that snaps a
    /// global compass — a turn is now just a road tile with a non-zero <c>curveAngle</c>, and the road
    /// bends around the player on its own (see RoadSequencer). This component is kept only so existing
    /// turn-tile prefabs do not show a missing-script error; it does nothing. Safe to remove from any
    /// prefab. Set the tile's RoadTile.curveAngle instead of using this.
    /// </summary>
    public sealed class TurnTrigger : MonoBehaviour
    {
        [Tooltip("Obsolete — set the tile's RoadTile.curveAngle instead. This field is no longer read.")]
        [SerializeField] private float turnDegrees = 90f;
    }
}
