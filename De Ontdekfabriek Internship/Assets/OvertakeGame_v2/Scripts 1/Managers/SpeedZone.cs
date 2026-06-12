using UnityEngine;
using System;
using TMPro;

namespace OvertakeGame
{
    /// <summary>
    /// Tracks the current speed limit and broadcasts changes.
    /// Listens to RoadSequencer.OnSequenceStarted; each RoadSequence carries
    /// speedLimitKmh (0 = use defaultSpeedLimitKmh).
    ///
    /// Typical limits: savanna 80 · township 50 · construction 40 · dirt road 60.
    /// </summary>
    public class SpeedZoneManager : MonoBehaviour
    {
        public static SpeedZoneManager Instance { get; private set; }

        [Tooltip("Speed limit used at session start and for sequences with speedLimitKmh = 0.")]
        public float defaultSpeedLimitKmh = 80f;

        public float CurrentLimit { get; private set; }
        public event Action<float> OnLimitChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance     = this;
            CurrentLimit = defaultSpeedLimitKmh;
        }

        void OnEnable()  => RoadSequencer.OnSequenceStarted += HandleSequenceStarted;
        void OnDisable() => RoadSequencer.OnSequenceStarted -= HandleSequenceStarted;

        private void HandleSequenceStarted(RoadSequence seq)
        {
            float newLimit = seq.speedLimitKmh > 0f ? seq.speedLimitKmh : defaultSpeedLimitKmh;
            if (!Mathf.Approximately(newLimit, CurrentLimit)) SetLimit(newLimit);
        }

        public void SetLimit(float kmh)
        {
            CurrentLimit = Mathf.Max(10f, kmh);
            OnLimitChanged?.Invoke(CurrentLimit);
        }

        public void ResetToDefault()
        {
            CurrentLimit = defaultSpeedLimitKmh;
            OnLimitChanged?.Invoke(CurrentLimit);
        }
    }

    /// <summary>
    /// HUD sign showing the posted speed limit. Flashes on zone change.
    /// Assign to a UI panel styled as a Kenyan speed-limit sign (circular, red border).
    /// </summary>
    public class SpeedLimitHUD : MonoBehaviour
    {
        [Header("References")]
        public TMP_Text       limitText;
        public RectTransform  signPanel;

        [Header("Flash on Zone Change")]
        public bool  flashOnChange  = true;
        public float flashDuration  = 0.35f;
        public float flashScalePeak = 1.25f;

        void Start()
        {
            var mgr = SpeedZoneManager.Instance;
            if (mgr == null) { Debug.LogWarning("[SpeedLimitHUD] SpeedZoneManager not found."); return; }
            mgr.OnLimitChanged += HandleLimitChanged;
            UpdateLabel(mgr.CurrentLimit, animate: false);
        }

        void OnDestroy()
        {
            if (SpeedZoneManager.Instance != null)
                SpeedZoneManager.Instance.OnLimitChanged -= HandleLimitChanged;
        }

        private void HandleLimitChanged(float limit) => UpdateLabel(limit, animate: flashOnChange);

        private void UpdateLabel(float limit, bool animate)
        {
            if (limitText != null) limitText.text = Mathf.RoundToInt(limit).ToString();
            if (animate && signPanel != null) StartCoroutine(FlashSign());
        }

        private System.Collections.IEnumerator FlashSign()
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float scale = 1f + (flashScalePeak - 1f) * Mathf.Sin((elapsed / flashDuration) * Mathf.PI);
                signPanel.localScale = Vector3.one * scale;
                yield return null;
            }
            signPanel.localScale = Vector3.one;
        }
    }
}
