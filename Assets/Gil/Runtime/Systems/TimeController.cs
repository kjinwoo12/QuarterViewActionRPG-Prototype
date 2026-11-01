using UnityEngine;

namespace Gil.Runtime.Systems
{
    /// <summary>
    /// Time.timeScale을 한곳에서만 바꾼다. 일시정지·히트스톱·슬로모션 요청이 서로 덮어쓰지 않도록
    /// 요청을 모아 매 프레임 최종 값을 계산한다.
    /// </summary>
    public sealed class TimeController : MonoBehaviour
    {
        static TimeController _instance;

        [SerializeField, Range(0f, 0.2f), Tooltip("히트스톱 중 시간 배율(0이면 완전 정지)")] float _hitStopScale = 0.02f;

        int _pauseRequests;
        float _hitStopUntil;
        float _slowUntil;
        float _slowScale = 1f;

        public static bool IsPaused => _instance != null && _instance._pauseRequests > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        void Awake() => _instance = this;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Time.timeScale = 1f;
        }

        public static void PushPause()
        {
            if (_instance != null) _instance._pauseRequests++;
        }

        public static void PopPause()
        {
            if (_instance != null && _instance._pauseRequests > 0) _instance._pauseRequests--;
        }

        public static void ResetAll()
        {
            if (_instance == null) return;
            _instance._pauseRequests = 0;
            _instance._hitStopUntil = 0;
            _instance._slowUntil = 0;
        }

        /// <summary>타격감용 순간 정지(실시간 기준).</summary>
        public static void HitStop(float seconds)
        {
            if (_instance == null || seconds <= 0) return;
            _instance._hitStopUntil = Mathf.Max(_instance._hitStopUntil, Time.unscaledTime + seconds);
        }

        public static void SlowMotion(float scale, float seconds)
        {
            if (_instance == null) return;
            _instance._slowScale = scale;
            _instance._slowUntil = Mathf.Max(_instance._slowUntil, Time.unscaledTime + seconds);
        }

        void Update()
        {
            float scale;
            if (_pauseRequests > 0) scale = 0f;
            else if (Time.unscaledTime < _hitStopUntil) scale = _hitStopScale;
            else if (Time.unscaledTime < _slowUntil) scale = _slowScale;
            else scale = 1f;
            Time.timeScale = scale;
        }
    }
}
