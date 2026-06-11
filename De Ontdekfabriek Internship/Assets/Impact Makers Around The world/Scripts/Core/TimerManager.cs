using UnityEngine;
using System;

namespace OvertakeGame
{
    public class TimerManager : MonoBehaviour
    {
        public float TimeRemaining   { get; private set; }
        public float SessionDuration { get; private set; }
        public bool  IsRunning       { get; private set; }
        public bool  IsLimited       { get; private set; }

        public event Action        OnTimerExpired;
        public event Action<float> OnTimerTick;

        public void StartTimer(float duration)
        {
            SessionDuration = duration;
            TimeRemaining   = duration > 0 ? duration : 0f;
            IsLimited       = duration > 0;
            IsRunning       = true;
        }

        public void StopTimer() => IsRunning = false;

        void Update()
        {
            if (!IsRunning || !IsLimited) return;
            TimeRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(TimeRemaining);
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                IsRunning     = false;
                OnTimerExpired?.Invoke();
                GameManager.Instance?.OnTimerExpired();
            }
        }

        public string GetFormattedTime()
        {
            if (!IsLimited) return "∞";
            int total = Mathf.CeilToInt(TimeRemaining);
            return $"{total / 60}:{total % 60:D2}";
        }
    }
}