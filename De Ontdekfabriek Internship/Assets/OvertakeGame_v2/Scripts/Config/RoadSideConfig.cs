using UnityEngine;

namespace OvertakeGame
{
    [CreateAssetMenu(fileName = "RoadSideConfig", menuName = "OvertakeGame/Road Side Config")]
    public class RoadSideConfig : ScriptableObject
    {
        [Tooltip("True = drives on the left (Kenya, UK). False = drives on the right (Netherlands, Vietnam).")]
        public bool driveOnLeft = true;

        // Positive steer-axis direction = right of travel.
        // Left-hand traffic: player lane is on the negative (left) steer side.
        public int PlayerLaneSideSign   => driveOnLeft ? -1 :  1;
        public int OncomingLaneSideSign => driveOnLeft ?  1 : -1;
    }
}
