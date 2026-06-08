using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    /// <summary>
    /// Attach to the follow camera. Adds a decaying shake on pothole/rock hits.
    /// Works additively with any follow script — shake is applied as local position offset.
    ///
    /// FIX: _originalLocalPos is now captured at the START of each shake, not in Awake.
    /// Capturing it in Awake stored the camera's position before the player ever moved,
    /// so every shake snapped the camera back to world origin.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        public float shakeDuration = 0.35f;
        public float shakeMagnitude = 0.15f;
        public float dampingSpeed = 4f;

        private Coroutine _shakeCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // Do NOT store localPosition here — the player hasn't moved yet.
        }

        public void Shake()
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            // Capture the resting local position RIGHT NOW, mid-game, not at scene load.
            Vector3 restingLocalPos = transform.localPosition;

            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                float strength = Mathf.Lerp(shakeMagnitude, 0f, elapsed / shakeDuration);
                transform.localPosition = restingLocalPos + new Vector3(
                    Random.Range(-strength, strength),
                    Random.Range(-strength, strength), 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = restingLocalPos;
        }
    }
}