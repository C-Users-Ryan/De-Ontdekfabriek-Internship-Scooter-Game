using UnityEngine;
using System.Collections;

namespace OvertakeGame
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake Settings")]
        public float shakeDuration = 0.35f;
        public float shakeMagnitude = 0.15f;
        public float dampingSpeed = 4f;

        private Vector3 _originalLocalPos;
        private Coroutine _shakeCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void Shake()
        {
            // Capture CURRENT camera position
            _originalLocalPos = transform.localPosition;

            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);

            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                float strength = Mathf.Lerp(
                    shakeMagnitude,
                    0f,
                    elapsed / shakeDuration);

                transform.localPosition = _originalLocalPos + new Vector3(
                    Random.Range(-strength, strength),
                    Random.Range(-strength, strength),
                    0f);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Restore to position BEFORE shake
            transform.localPosition = _originalLocalPos;
        }
    }
}