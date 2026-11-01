namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 캐릭터 외형 애니메이터의 공통 계약. 게임플레이 코드는 이것만 부른다 —
    /// 임시 외형(뼈 구조 절차 애니메이션)이든 확정 에셋(8방향 프레임 스프라이트시트)이든 같은 호출로 움직인다.
    /// 동작 이름: jab / straight / spin_kick / rolling / vault / climb / plunge / land / overdrive / hurt / windup / smash / fire.
    /// </summary>
    public interface ICharacterAnimator
    {
        bool IsPlaying { get; }
        void SetMoveAmount(float normalized);
        /// <summary>이동 루프 재생 속도 배율(달리기 중 1보다 크다).</summary>
        void SetMoveSpeedScale(float scale);
        void SetAirborne(bool airborne);
        void SetHeartRate(float beatsPerSecond);
        void Squash(float amount);
        /// <summary>동작을 duration(초) 동안 재생한다. 프레임 애니메이션은 이 시간에 맞춰 늘이거나 줄인다(판정 타이밍이 기준).</summary>
        void PlayAction(string name, float duration);
        void StopAction();
        void Flash(float seconds = 0.08f);
        void Die();
        void ResetPose();
    }
}
