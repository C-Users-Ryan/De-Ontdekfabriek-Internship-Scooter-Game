using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Shakes the camera when the player hits a pothole.
    /// Attach to the Camera GameObject (or a camera rig parent).
    ///
    /// SETUP: Attach to your follow camera. The shake is additive to whatever
    /// position the camera is already at, so it works with any follow script.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        [Tooltip("How long the shake lasts in seconds.")]
        public float shakeDuration  = 0.35f;

        [Tooltip("Maximum positional offset during shake (world units).")]
        public float shakeMagnitude = 0.15f;

        [Tooltip("How quickly the shake decays to zero.")]
        public float dampingSpeed   = 4f;

        private Vector3   _originalLocalPos;
        private Coroutine _shakeCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _originalLocalPos = transform.localPosition;
        }

        /// <summary>Trigger a shake. Called by Pothole on hit.</summary>
        public void Shake()
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed  = 0f;
            float duration = shakeDuration;

            while (elapsed < duration)
            {
                float strength = Mathf.Lerp(shakeMagnitude, 0f, elapsed / duration);

                transform.localPosition = _originalLocalPos + new Vector3(
                    Random.Range(-strength, strength),
                    Random.Range(-strength, strength),
                    0f);

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = _originalLocalPos;
        }
    }
}
