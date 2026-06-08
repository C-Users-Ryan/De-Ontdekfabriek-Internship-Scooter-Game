using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Visual lean on the scooter mesh child. Attach to the MESH child, not the Rigidbody root.
    /// Responds to steering input and pothole wobble via PlayerController.ExternalLeanAngle.
    /// </summary>
    public class ScooterLean : MonoBehaviour
    {
        [Header("Lean")]
        public float maxLeanAngle  = 20f;
        public float leanSpeed     = 8f;
        public float returnSpeed   = 10f;

        [Header("Speed Influence")]
        public bool  scaleWithSpeed = true;
        public float fullLeanSpeed  = 15f;

        [Header("Optional Handlebar Bone")]
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

            if (playerController.HasExternalLean)
            {
                targetAngle = playerController.ExternalLeanAngle;
            }
            else
            {
                float lateral     = playerController.CurrentLateralInput;
                float speedFactor = scaleWithSpeed && fullLeanSpeed > 0f
                    ? Mathf.Clamp01(playerController.CurrentSpeedMs / fullLeanSpeed) : 1f;
                targetAngle = -lateral * maxLeanAngle * speedFactor;

                var state = GameManager.Instance?.CurrentState;
                if (state == GameManager.GameState.AtCheckpoint ||
                    state == GameManager.GameState.GameOver)
                    targetAngle = 0f;
            }

            float blendSpeed  = Mathf.Abs(targetAngle) > 0.01f ? leanSpeed : returnSpeed;
            _currentLeanAngle = Mathf.LerpAngle(_currentLeanAngle, targetAngle, blendSpeed * Time.deltaTime);

            Vector3 euler = transform.localEulerAngles;
            euler.z = _currentLeanAngle;
            transform.localEulerAngles = euler;

            if (handlebarBone != null)
            {
                Vector3 hEuler = handlebarBone.localEulerAngles;
                hEuler.z = playerController.CurrentLateralInput * handlebarAngle;
                handlebarBone.localEulerAngles = hEuler;
            }
        }
    }
}
