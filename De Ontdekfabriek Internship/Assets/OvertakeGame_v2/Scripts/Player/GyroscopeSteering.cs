using UnityEngine;

namespace OvertakeGame
{
    /// <summary>
    /// Drives lateral steering from the iPad gyroscope.
    /// Counter-rotates the camera rig so the horizon stays level for the player's eyes.
    /// Toggle enableGyroSteering on/off per session.
    /// Enable simulateOnDesktop + move mouse horizontally to test on PC.
    /// </summary>
    public class GyroscopeSteering : MonoBehaviour
    {
        [Header("Master Toggle")]
        public bool enableGyroSteering = true;

        [Header("Counter-Rotation (Horizon Lock)")]
        public bool  enableHorizonLock      = true;
        [Range(0f,1f)]
        public float horizonLockStrength    = 1f;
        public float horizonLockSmoothing   = 12f;

        [Header("Steering Settings")]
        public float maxTiltAngle  = 25f;
        public float deadZone      = 3f;
        public float steeringSmoothing = 8f;

        [Header("Desktop Testing")]
        public bool  simulateOnDesktop           = true;
        public float mouseSimulationSensitivity  = 60f;

        [Header("References")]
        public PlayerController playerController;
        public Transform        cameraRig;

        [Header("Calibration")]
        public float manualTiltOffset = 0f;

        public float CurrentTiltDegrees  { get; private set; }
        public float CurrentSteeringInput{ get; private set; }

        private Gyroscope  _gyro;
        private bool       _gyroAvailable;
        private float      _smoothedTilt;
        private float      _currentCameraRollAngle;
        private float      _simulatedTilt;
        private Quaternion _calibrationOffset = Quaternion.identity;

        void Start()
        {
            bool isMobile = Application.isMobilePlatform;
            if (isMobile && SystemInfo.supportsGyroscope)
            {
                _gyro          = Input.gyro;
                _gyro.enabled  = true;
                _gyroAvailable = true;
            }
        }

        void Update()
        {
            if (!enableGyroSteering) return;

            float rawTilt      = GetRawTilt();
            float adjustedTilt = rawTilt - manualTiltOffset;
            if (Mathf.Abs(adjustedTilt) < deadZone)
                adjustedTilt = 0f;
            else
                adjustedTilt = Mathf.Sign(adjustedTilt) * (Mathf.Abs(adjustedTilt) - deadZone);

            _smoothedTilt        = Mathf.Lerp(_smoothedTilt, adjustedTilt, steeringSmoothing * Time.deltaTime);
            CurrentTiltDegrees   = _smoothedTilt;
            CurrentSteeringInput = Mathf.Clamp(_smoothedTilt / (maxTiltAngle - deadZone), -1f, 1f);

            if (playerController != null)
                playerController.mobileLateral = CurrentSteeringInput;

            if (enableHorizonLock && cameraRig != null)
            {
                float targetRoll = -rawTilt * horizonLockStrength;
                _currentCameraRollAngle = Mathf.LerpAngle(
                    _currentCameraRollAngle, targetRoll, horizonLockSmoothing * Time.deltaTime);
                Vector3 e = cameraRig.localEulerAngles;
                e.z = _currentCameraRollAngle;
                cameraRig.localEulerAngles = e;
            }
        }

        private float GetRawTilt()
        {
            if (_gyroAvailable)
            {
                Quaternion attitude = _calibrationOffset * ConvertGyroToUnity(_gyro.attitude);
                float roll = attitude.eulerAngles.z;
                if (roll > 180f) roll -= 360f;
                return -roll;
            }
            if (simulateOnDesktop && !Application.isMobilePlatform)
            {
                _simulatedTilt += Input.GetAxis("Mouse X") * mouseSimulationSensitivity * Time.deltaTime;
                _simulatedTilt  = Mathf.Clamp(_simulatedTilt, -maxTiltAngle, maxTiltAngle);
                if (Mathf.Abs(Input.GetAxis("Mouse X")) < 0.01f)
                    _simulatedTilt = Mathf.MoveTowards(_simulatedTilt, 0f, 15f * Time.deltaTime);
                return _simulatedTilt;
            }
            return 0f;
        }

        public void CalibrateToCurrentAngle()
        {
            if (_gyroAvailable)
                _calibrationOffset = Quaternion.Inverse(ConvertGyroToUnity(_gyro.attitude));
            else
            { _simulatedTilt = 0f; _smoothedTilt = 0f; manualTiltOffset = 0f; }
        }

        public void SetEnabled(bool enabled)
        {
            enableGyroSteering = enabled;
            if (!enabled)
            {
                CurrentSteeringInput = 0f;
                if (playerController != null) playerController.mobileLateral = 0f;
                if (cameraRig != null) { var e = cameraRig.localEulerAngles; e.z = 0f; cameraRig.localEulerAngles = e; }
            }
            if (enabled && _gyroAvailable && _gyro != null) _gyro.enabled = true;
        }

        void OnDisable() { if (_gyroAvailable && _gyro != null) _gyro.enabled = false; }

        private static Quaternion ConvertGyroToUnity(Quaternion q) =>
            new Quaternion(q.x, q.y, -q.z, -q.w);
    }
}
