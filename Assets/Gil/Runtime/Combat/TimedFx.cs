using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>수명 동안 진행도(0~1)를 <see cref="Tick"/>으로 넘기고, 끝나면 스스로 파괴되는 단발 이펙트의 공통 부분.</summary>
    public abstract class TimedFx : MonoBehaviour
    {
        // 자식 이펙트가 인스펙터에 노출하는 _lifetime과 이름이 겹치면 Unity 직렬화가 오류를 낸다.
        float _duration = 0.2f;
        float _time;

        protected void Begin(float lifetime)
        {
            _duration = Mathf.Max(0.01f, lifetime);
            _time = 0;
            Tick(0);
        }

        protected abstract void Tick(float t);

        void Update()
        {
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / _duration);
            Tick(t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
