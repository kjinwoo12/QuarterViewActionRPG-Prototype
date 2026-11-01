using System;
using System.Collections.Generic;
using Gil.Core.Data;

namespace Gil.Core.Combat
{
    public enum AttackPhase
    {
        Idle,
        Windup,
        Active,
        Recovery,
    }

    /// <summary>
    /// 연속 공격(콤보)의 타이밍 상태머신. 프레임/애니메이션과 분리돼 있어 단위 테스트로 손맛 수치를 검증할 수 있다.
    ///
    /// 규칙
    /// - Idle에서 공격 입력 → 1타 시작.
    /// - 선딜/판정 중에 들어온 입력은 버퍼에 저장했다가, 후딜(Recovery)에 들어서는 순간 다음 타로 잇는다.
    /// - 후딜 중 comboWindow 안에 들어온 입력도 다음 타로 잇는다.
    /// - 마지막 타 뒤나 입력 없이 후딜이 끝나면 Idle로 돌아가고 콤보는 1타부터 다시 시작한다.
    /// </summary>
    public sealed class ComboTracker
    {
        readonly IReadOnlyList<ComboStepDef> _steps;
        float _phaseTime;
        bool _buffered;

        public int StepIndex { get; private set; } = -1;
        public AttackPhase Phase { get; private set; } = AttackPhase.Idle;
        public ComboStepDef CurrentStep => StepIndex >= 0 ? _steps[StepIndex] : null;
        public bool IsAttacking => Phase != AttackPhase.Idle;
        /// <summary>선딜·판정 중에는 회피로도 끊을 수 없다(커밋형 공격). 후딜은 회피로 캔슬 가능.</summary>
        public bool IsCommitted => Phase == AttackPhase.Windup || Phase == AttackPhase.Active;

        public event Action<int> StepStarted;
        /// <summary>판정이 켜지는 순간. 여기서 히트박스를 검사한다.</summary>
        public event Action<int> HitFrame;
        public event Action Finished;

        public ComboTracker(IReadOnlyList<ComboStepDef> steps)
        {
            if (steps == null || steps.Count == 0) throw new ArgumentException("콤보 단계가 비어 있습니다");
            _steps = steps;
        }

        public void PressAttack()
        {
            switch (Phase)
            {
                case AttackPhase.Idle:
                    StartStep(0);
                    break;
                case AttackPhase.Windup:
                case AttackPhase.Active:
                    _buffered = true;
                    break;
                case AttackPhase.Recovery:
                    if (_phaseTime <= CurrentStep.ComboWindow && HasNext) StartStep(StepIndex + 1);
                    break;
            }
        }

        public void Tick(float dt)
        {
            if (Phase == AttackPhase.Idle) return;
            _phaseTime += dt;

            // 한 프레임이 길어도 단계를 건너뛰지 않도록 반복 처리한다.
            while (Phase != AttackPhase.Idle)
            {
                var step = CurrentStep;
                if (Phase == AttackPhase.Windup && _phaseTime >= step.Windup)
                {
                    Enter(AttackPhase.Active, _phaseTime - step.Windup);
                    HitFrame?.Invoke(StepIndex);
                }
                else if (Phase == AttackPhase.Active && _phaseTime >= step.Active)
                {
                    var carry = _phaseTime - step.Active;
                    if (_buffered && HasNext)
                    {
                        StartStep(StepIndex + 1);
                        _phaseTime = carry;
                    }
                    else
                    {
                        Enter(AttackPhase.Recovery, carry);
                    }
                }
                else if (Phase == AttackPhase.Recovery && _phaseTime >= step.Recovery)
                {
                    Reset();
                    Finished?.Invoke();
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>피격·회피 등으로 공격을 끊는다.</summary>
        public void Cancel()
        {
            if (Phase == AttackPhase.Idle) return;
            Reset();
            Finished?.Invoke();
        }

        bool HasNext => StepIndex + 1 < _steps.Count;

        void StartStep(int index)
        {
            StepIndex = index;
            _buffered = false;
            Enter(AttackPhase.Windup, 0);
            StepStarted?.Invoke(index);
        }

        void Enter(AttackPhase phase, float carry)
        {
            Phase = phase;
            _phaseTime = carry;
        }

        void Reset()
        {
            Phase = AttackPhase.Idle;
            StepIndex = -1;
            _phaseTime = 0;
            _buffered = false;
        }
    }
}
