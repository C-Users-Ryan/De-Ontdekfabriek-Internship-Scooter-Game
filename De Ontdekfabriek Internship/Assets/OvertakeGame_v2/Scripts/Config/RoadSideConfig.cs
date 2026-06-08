using UnityEngine;

namespace OvertakeGame
{
    [CreateAssetMenu(fileName = "RoadSideConfig", menuName = "OvertakeGame/Road Side Config")]
    public class RoadSideConfig : ScriptableObject
    {
        [Tooltip("TRUE = Right-hand traffic (Netherlands, USA). FALSE = Left-hand traffic (UK).")]
        public bool driveOnRight = true;

        public float PlayerLaneSideSign   =>  driveOnRight ?  1f : -1f;
        public float OncomingLaneSideSign =>  driveOnRight ? -1f :  1f;
    }
}
