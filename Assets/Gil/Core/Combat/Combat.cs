using System;
using Gil.Core.Data;

namespace Gil.Core.Combat
{
    public sealed class Health
    {
        public int Max { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        /// <summary>(이전 값, 현재 값)</summary>
        public event Action<int, int> Changed;
        public event Action Died;

        public Health(int max, int? current = null)
        {
            Max = Math.Max(1, max);
            Current = Math.Max(0, Math.Min(Max, current ?? Max));
        }

        /// <returns>실제로 깎인 양</returns>
        public int Damage(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            var before = Current;
            Current = Math.Max(0, Current - amount);
            Changed?.Invoke(before, Current);
            if (Current == 0) Died?.Invoke();
            return before - Current;
        }

        public int Heal(int amount)
        {
            if (IsDead || amount <= 0) return 0;
            var before = Current;
            Current = Math.Min(Max, Current + amount);
            if (Current != before) Changed?.Invoke(before, Current);
            return Current - before;
        }

        /// <summary>최대 체력 변경(의체 장착). 늘어난 만큼 현재 체력도 늘고, 줄면 최대치에 맞춰 깎는다.</summary>
        public void SetMax(int max)
        {
            max = Math.Max(1, max);
            if (max == Max) return;
            var before = Current;
            if (max > Max && !IsDead) Current += max - Max;
            Max = max;
            Current = Math.Min(Current, Max);
            Changed?.Invoke(before, Current);
        }

        public void Revive(int? hp = null)
        {
            var before = Current;
            Current = Math.Max(1, Math.Min(Max, hp ?? Max));
            Changed?.Invoke(before, Current);
        }
    }

    public readonly struct DamageResult
    {
        public readonly int Amount;
        public readonly bool IsCritical;

        public DamageResult(int amount, bool isCritical)
        {
            Amount = amount;
            IsCritical = isCritical;
        }
    }

    public static class DamageCalculator
    {
        /// <summary>
        /// 피해량 = 공격력 × 배율 × 100/(100+방어력), 치명타면 배율을 한 번 더 곱한다. 최소 1.
        /// 방어력이 높아져도 0으로 수렴하지 않게 비율 감소식을 쓴다.
        /// </summary>
        /// <param name="critRoll">0~1 난수. 테스트에서 결과를 고정할 수 있도록 밖에서 넣는다.</param>
        public static DamageResult Compute(int attack, float multiplier, int defense,
            float critChance, float critMultiplier, float critRoll)
        {
            var raw = attack * multiplier * 100f / (100f + Math.Max(0, defense));
            var crit = critRoll < critChance;
            if (crit) raw *= critMultiplier;
            return new DamageResult(Math.Max(1, (int)Math.Round(raw)), crit);
        }
    }

    /// <summary>
    /// 인공 심장 게이지. 공격 적중과 피격으로 차오르고 필살기 「과부하」에 소모된다.
    /// 「병으로 죽어가다 인공 심장으로 살아난」 주인공 설정을 전투 자원으로 옮긴 것이다.
    /// </summary>
    public sealed class HeartGauge
    {
        readonly HeartDef _def;
        public float Value { get; private set; }
        public float Max => _def.Max;
        public float Normalized => _def.Max <= 0 ? 0 : Value / _def.Max;
        public bool CanOverdrive => Value >= _def.SkillCost;

        public event Action<float> Changed;

        public HeartGauge(HeartDef def, float initial = 0)
        {
            _def = def;
            Value = Math.Max(0, Math.Min(def.Max, initial));
        }

        /// <summary>충전량 배율(의체 보정).</summary>
        public float GainMultiplier { get; set; } = 1f;

        public void OnHitLanded() => Add(_def.GainPerHit * GainMultiplier);
        public void OnDamageTaken() => Add(_def.GainPerDamageTaken * GainMultiplier);

        public void Add(float amount)
        {
            var before = Value;
            Value = Math.Max(0, Math.Min(_def.Max, Value + amount));
            if (Math.Abs(before - Value) > 0.0001f) Changed?.Invoke(Value);
        }

        public bool TrySpend()
        {
            if (!CanOverdrive) return false;
            Value -= _def.SkillCost;
            Changed?.Invoke(Value);
            return true;
        }
    }

    /// <summary>
    /// 구르기: 동작 시간, 무적 시간, 재사용 대기시간. 충전 횟수(charges)가 2 이상이면
    /// 연속 구르기가 가능하고 대기시간마다 한 번씩 다시 충전된다(의체 「제비 다리」).
    /// 스태미나 소모는 <see cref="Stamina"/>가 따로 판단한다.
    /// </summary>
    public sealed class RollingTimer
    {
        RollingDef _def;
        float _elapsed = float.MaxValue;
        float _rechargeLeft;
        int _charges = 1;

        public RollingTimer(RollingDef def, int charges = 1)
        {
            _def = def;
            _charges = Math.Max(1, charges);
            Available = _charges;
        }

        public int Charges => _charges;
        public int Available { get; private set; }
        public bool IsRolling => _elapsed < _def.Duration;
        public bool IsInvulnerable => _elapsed < _def.InvulnerableTime;
        public bool IsReady => Available > 0 && !IsRolling;
        public float Progress => IsRolling ? _elapsed / _def.Duration : 1f;
        /// <summary>구르는 중 초당 이동 거리.</summary>
        public float Speed => _def.Distance / _def.Duration;

        /// <summary>능력치가 바뀌었을 때(의체 장착) 정의와 충전 횟수를 교체한다.</summary>
        public void Configure(RollingDef def, int charges)
        {
            _def = def;
            var newCharges = Math.Max(1, charges);
            Available = Math.Min(newCharges, Available + Math.Max(0, newCharges - _charges));
            _charges = newCharges;
        }

        public bool TryStart()
        {
            if (!IsReady) return false;
            _elapsed = 0;
            if (Available == _charges) _rechargeLeft = _def.Cooldown;
            Available--;
            return true;
        }

        public void Tick(float dt)
        {
            if (_elapsed < float.MaxValue) _elapsed += dt;
            if (Available >= _charges) return;
            _rechargeLeft -= dt;
            if (_rechargeLeft > 0) return;
            Available++;
            if (Available < _charges) _rechargeLeft += _def.Cooldown;
        }
    }

    /// <summary>
    /// 스태미나. 달리기는 누르고 있는 동안 초당 소모, 구르기는 한 번에 소모한다.
    /// 마지막으로 쓴 뒤 <see cref="StaminaDef.RegenDelay"/>초가 지나면 초당 <see cref="StaminaDef.RegenPerSecond"/>씩 회복한다.
    /// 달리다 바닥나면 지친 상태가 되어, 달리기 버튼을 뗐다가 다시 눌러야 달릴 수 있다(누른 채로 회복분만큼 찔끔 달리는 것을 막는다).
    /// </summary>
    public sealed class Stamina
    {
        StaminaDef _def;
        float _sinceSpent = float.MaxValue;

        public float Value { get; private set; }
        public float Max { get; private set; }
        public float Normalized => Max <= 0 ? 0 : Value / Max;
        /// <summary>달리다 바닥나서, 달리기 버튼을 뗄 때까지 달리지 못하는 상태.</summary>
        public bool Exhausted { get; private set; }

        public Stamina(StaminaDef def)
        {
            _def = def;
            Max = Math.Max(0, def.Max);
            Value = Max;
        }

        /// <summary>능력치가 바뀌었을 때(의체 장착) 최대치·회복량을 교체한다. 최대치가 늘면 늘어난 만큼 채운다.</summary>
        public void Configure(StaminaDef def)
        {
            var oldMax = Max;
            _def = def;
            Max = Math.Max(0, def.Max);
            Value = Math.Max(0, Math.Min(Max, Value + Math.Max(0, Max - oldMax)));
        }

        /// <summary>가득 채운다(새 판, 부활).</summary>
        public void Refill()
        {
            Value = Max;
            Exhausted = false;
            _sinceSpent = float.MaxValue;
        }

        public bool CanSpend(float amount) => Value >= amount;

        /// <summary>한 번에 amount만큼 쓴다(구르기). 모자라면 쓰지 않고 false.</summary>
        public bool TrySpend(float amount)
        {
            if (Value < amount) return false;
            Value -= amount;
            _sinceSpent = 0;
            return true;
        }

        /// <summary>
        /// 이번 프레임에 달리는지 판단하고 소모한다. buttonHeld는 달리기 버튼 상태, wantsToRun은 실제로 달릴 수 있는 상황인지
        /// (움직이는 중이고 공격·구르기·공중이 아님). 달렸으면 true.
        /// </summary>
        public bool Sprint(bool buttonHeld, bool wantsToRun, float costPerSecond, float dt)
        {
            if (!buttonHeld)
            {
                Exhausted = false;
                return false;
            }
            if (!wantsToRun || Exhausted) return false;
            if (Value <= 0)
            {
                Exhausted = true;
                return false;
            }
            Value = Math.Max(0, Value - costPerSecond * dt);
            _sinceSpent = 0;
            if (Value <= 0) Exhausted = true;
            return true;
        }

        /// <summary>시간 경과. 쓰지 않은 채로 지연 시간이 지나면 회복한다. 같은 프레임의 소모(Sprint/TrySpend)보다 먼저 부른다.</summary>
        public void Tick(float dt)
        {
            if (_sinceSpent < float.MaxValue) _sinceSpent += dt;
            if (_sinceSpent < _def.RegenDelay || Value >= Max) return;
            Value = Math.Min(Max, Value + _def.RegenPerSecond * dt);
        }
    }
}
