using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Visual lean for the scooter mesh child object.
    /// Responds to lateral steering input and to external wobble events
    /// (such as pothole hits) via PlayerController.ExternalLeanAngle.
    ///
    /// Attach to the scooter MESH child, not the Rigidbody root.
    /// </summary>
    public class ScooterLean : MonoBehaviour
    {
        [Header("Steering Lean")]
        [Tooltip("Maximum lean angle in degrees when steering at full speed.")]
        public float maxLeanAngle = 20f;
        [Tooltip("How fast the lean responds to input.")]
        public float leanSpeed    = 8f;
        [Tooltip("How fast the scooter returns upright.")]
        public float returnSpeed  = 10f;

        [Header("Speed Influence")]
        [Tooltip("Scale lean with forward speed so slow speeds lean less.")]
        public bool  scaleWithSpeed = true;
        [Tooltip("Speed in m/s at which full lean is reached.")]
        public float fullLeanSpeed  = 15f;

        [Header("Handlebar Counter-Steer (optional)")]
        public Transform handlebarBone;
        public float     handlebarAngle = 8f;

        [Header("References")]
        public PlayerController playerController;

        private float _currentLeanAngle;

        void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<PlayerController>();
        }

        void Update()
        {
            if (playerController == null) return;

            float targetAngle;

            // External wobble (pothole) takes priority over steering lean
            if (playerController.HasExternalLean)
            {
                targetAngle = playerController.ExternalLeanAngle;
            }
            else
            {
                float lateral = playerController.CurrentLateralInput;

                float speedFactor = 1f;
                if (scaleWithSpeed && fullLeanSpeed > 0f)
                    speedFactor = Mathf.Clamp01(playerController.CurrentSpeedMs / fullLeanSpeed);

                targetAngle = -lateral * maxLeanAngle * speedFactor;

                var state = GameManager.Instance?.CurrentState;
                if (state == GameManager.GameState.AtCheckpoint ||
                    state == GameManager.GameState.GameOver)
                    targetAngle = 0f;
            }

            float blendSpeed   = Mathf.Abs(targetAngle) > 0.01f ? leanSpeed : returnSpeed;
            _currentLeanAngle  = Mathf.LerpAngle(_currentLeanAngle, targetAngle, blendSpeed * Time.deltaTime);

            Vector3 euler = transform.localEulerAngles;
            euler.z = _currentLeanAngle;
            transform.localEulerAngles = euler;

            if (handlebarBone != null)
            {
                float lateral  = playerController.CurrentLateralInput;
                Vector3 hEuler = handlebarBone.localEulerAngles;
                hEuler.z       = lateral * handlebarAngle;
                handlebarBone.localEulerAngles = hEuler;
            }
        }
    }
}
