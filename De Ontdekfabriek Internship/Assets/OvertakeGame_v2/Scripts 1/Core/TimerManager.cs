using UnityEngine;
using System;

namespace OvertakeGame
{
    /// <summary>
    /// Session timer. Fires OnTimerExpired when the countdown reaches zero.
    /// GameManager subscribes to OnTimerExpired — TimerManager never calls GameManager directly.
    ///
    /// StartTimer(duration > 0) = limited session, counts down to zero.
    /// StartTimer(duration ≤ 0) = unlimited session, counts up indefinitely, never expires.
    /// </summary>
    public class TimerManager : MonoBehaviour
    {
        public float TimeRemaining   { get; private set; }
        public float SessionDuration { get; private set; }
        public bool  IsLimited       { get; private set; }
        public bool  IsRunning       { get; private set; }

        public event Action        OnTimerExpired;
        public event Action<float> OnTimerTick;

        public void StartTimer(float duration)
        {
            SessionDuration = duration;
            IsLimited       = duration > 0f;
            TimeRemaining   = IsLimited ? duration : 0f;
            IsRunning       = true;
        }

        public void StopTimer() => IsRunning = false;

        void Update()
        {
            if (!IsRunning) return;

            if (IsLimited)
            {
                TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
                OnTimerTick?.Invoke(TimeRemaining);
                if (TimeRemaining <= 0f)
                {
                    IsRunning = false;
                    OnTimerExpired?.Invoke();
                }
            }
            else
            {
                TimeRemaining += Time.deltaTime;
                OnTimerTick?.Invoke(TimeRemaining);
            }
        }

        /// <summary>Returns "M:SS" for a countdown, or "∞" for unlimited sessions.</summary>
        public string GetFormattedTime()
        {
            if (!IsLimited) return "∞";
            int total = Mathf.CeilToInt(TimeRemaining);
            return $"{total / 60}:{total % 60:D2}";
        }
    }
}
