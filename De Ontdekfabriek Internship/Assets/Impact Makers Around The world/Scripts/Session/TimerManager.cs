using System;
using UnityEngine;
using KenyaScooter.Config;
using KenyaScooter.Core;

namespace KenyaScooter.Session
{
    /// <summary>
    /// The 120-second session countdown (M26). Runs only while Playing — it pauses
    /// during a rewind, so rewound seconds are not replayed (workshop throughput,
    /// decision in D17). Fires OnTimerExpired once; GameManager owns what happens
    /// next. The timer knows nothing about game flow (D16 decoupling rule).
    /// </summary>
    public sealed class TimerManager : MonoBehaviour
    {
        public static TimerManager Instance { get; private set; }

        [SerializeField] private SessionConfig config;

        public float Remaining { get; private set; }
        public float Duration => config.sessionSeconds;
        public float Elapsed => Duration - Remaining;
        /// <summary>0 at session start, 1 at expiry — drives density ramps and unlock gates.</summary>
        public float Normalized01 => Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 0f;

        public event Action OnTimerExpired;

        private bool expired;

        private void Awake()
        {
            Instance = this;
            Remaining = config.sessionSeconds;
        }

        private void OnEnable() => GameEvents.SessionReset += HandleSessionReset;
        private void OnDisable() => GameEvents.SessionReset -= HandleSessionReset;

        private void Update()
        {
            if (expired || GameManager.State != GameState.Playing)
                return;

            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                expired = true;
                OnTimerExpired?.Invoke();
            }
        }

        private void HandleSessionReset()
        {
            Remaining = config.sessionSeconds;
            expired = false;
        }
    }
}
