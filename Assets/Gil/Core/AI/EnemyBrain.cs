using System;
using Gil.Core.Data;

namespace Gil.Core.AI
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Windup,
        Attack,
        Recover,
        Stagger,
        Dead,
    }

    public enum MoveIntent
    {
        None,
        TowardTarget,
        AwayFromTarget,
    }

    /// <summary>
    /// 적 AI 상태머신. 물리·애니메이션은 모르고, "플레이어까지의 거리"만 보고 상태와 이동 의도를 결정한다.
    /// Unity 쪽 EnemyController가 매 프레임 Tick을 부르고, 이벤트에 맞춰 예고 표시·공격 판정·연출을 붙인다.
    ///
    /// Idle → (인식) → Chase → (사거리 + 쿨타임) → Windup(예고) → Attack → Recover → Chase
    /// 일정 이상 피해를 받으면 어느 상태에서든 Stagger로 끊긴다. 선딜(Windup) 중 경직은 플레이어의 반격 보상이다.
    /// </summary>
    public sealed class EnemyBrain
    {
        readonly EnemyDef _def;
        float _stateTime;
        float _staggerDuration;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public MoveIntent Move { get; private set; } = MoveIntent.None;
        public float CooldownLeft { get; private set; }
        public float StateTime => _stateTime;
        /// <summary>0~1. 예고 표시가 차오르는 정도.</summary>
        public float WindupProgress => State == EnemyState.Windup ? Math.Min(1f, _stateTime / _def.Windup) : 0f;

        public event Action<EnemyState, EnemyState> StateChanged;
        /// <summary>공격 판정 시점(근접 타격 / 투사체 발사).</summary>
        public event Action AttackExecuted;

        public EnemyBrain(EnemyDef def) => _def = def;

        public void Tick(float dt, float distanceToTarget, bool targetAlive)
        {
            if (State == EnemyState.Dead) return;
            _stateTime += dt;
            if (CooldownLeft > 0) CooldownLeft = Math.Max(0, CooldownLeft - dt);

            switch (State)
            {
                case EnemyState.Idle:
                    Move = MoveIntent.None;
                    if (targetAlive && distanceToTarget <= _def.AggroRange) Enter(EnemyState.Chase);
                    break;

                case EnemyState.Chase:
                    if (!targetAlive || distanceToTarget > _def.AggroRange * 1.5f)
                    {
                        Enter(EnemyState.Idle);
                        break;
                    }
                    if (distanceToTarget <= _def.AttackRange && CooldownLeft <= 0)
                    {
                        Enter(EnemyState.Windup);
                        break;
                    }
                    Move = ChooseMove(distanceToTarget);
                    break;

                case EnemyState.Windup:
                    Move = MoveIntent.None;
                    if (_stateTime >= _def.Windup)
                    {
                        Enter(EnemyState.Attack);
                        AttackExecuted?.Invoke();
                    }
                    break;

                case EnemyState.Attack:
                    if (_stateTime >= _def.AttackDuration) Enter(EnemyState.Recover);
                    break;

                case EnemyState.Recover:
                    if (_stateTime >= _def.Recovery)
                    {
                        CooldownLeft = _def.Cooldown;
                        Enter(EnemyState.Chase);
                    }
                    break;

                case EnemyState.Stagger:
                    if (_stateTime >= _staggerDuration) Enter(EnemyState.Chase);
                    break;
            }
        }

        /// <summary>피해를 받았을 때 호출. 경직 여부를 돌려준다.</summary>
        public bool OnDamaged(int amount, bool killed)
        {
            if (State == EnemyState.Dead) return false;
            if (killed)
            {
                Enter(EnemyState.Dead);
                return false;
            }
            if (amount < _def.Poise) return false;

            _staggerDuration = _def.StaggerTime;
            // 경직이 끝나면 바로 반격하지 않도록 쿨타임을 일부 채워 둔다.
            CooldownLeft = Math.Max(CooldownLeft, _def.Cooldown * 0.5f);
            Enter(EnemyState.Stagger);
            return true;
        }

        /// <summary>경직 저항과 무관하게 일정 시간 기절시킨다(착지 충격기 등).</summary>
        public void Stun(float seconds)
        {
            if (State == EnemyState.Dead || seconds <= 0) return;
            _staggerDuration = Math.Max(seconds, State == EnemyState.Stagger ? _staggerDuration - _stateTime : 0f);
            CooldownLeft = Math.Max(CooldownLeft, _def.Cooldown * 0.5f);
            Enter(EnemyState.Stagger);
        }

        MoveIntent ChooseMove(float distance)
        {
            if (_def.Kind == EnemyKind.Ranged)
            {
                if (distance < _def.PreferredRange * 0.7f) return MoveIntent.AwayFromTarget;
                if (distance > _def.PreferredRange) return MoveIntent.TowardTarget;
                return MoveIntent.None;
            }
            return distance > _def.AttackRange * 0.8f ? MoveIntent.TowardTarget : MoveIntent.None;
        }

        void Enter(EnemyState next)
        {
            var previous = State;
            State = next;
            _stateTime = 0;
            if (next != EnemyState.Chase) Move = MoveIntent.None;
            StateChanged?.Invoke(previous, next);
        }
    }
}
