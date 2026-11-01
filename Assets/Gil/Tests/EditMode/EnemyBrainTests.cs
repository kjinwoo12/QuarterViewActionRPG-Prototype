using Gil.Core.AI;
using Gil.Core.Data;
using NUnit.Framework;

namespace Gil.Tests
{
    public class EnemyBrainTests
    {
        static EnemyDef Melee() => new EnemyDef
        {
            Kind = EnemyKind.Melee, AggroRange = 10, AttackRange = 2, Windup = 0.5f, AttackDuration = 0.2f,
            Recovery = 0.4f, Cooldown = 1f, StaggerTime = 0.3f, Poise = 10,
        };

        [Test]
        public void IdleUntilTargetInAggroRange()
        {
            var brain = new EnemyBrain(Melee());
            brain.Tick(0.1f, 15, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            brain.Tick(0.1f, 9, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            brain.Tick(0.1f, 9, true);
            Assert.That(brain.Move, Is.EqualTo(MoveIntent.TowardTarget));
        }

        [Test]
        public void FullAttackCycleWithTelegraphAndCooldown()
        {
            var brain = new EnemyBrain(Melee());
            var attacks = 0;
            brain.AttackExecuted += () => attacks++;

            brain.Tick(0.1f, 1.5f, true); // Idle → Chase
            brain.Tick(0.1f, 1.5f, true); // Chase → Windup
            Assert.That(brain.State, Is.EqualTo(EnemyState.Windup));
            Assert.That(attacks, Is.EqualTo(0), "예고 시간 동안은 공격하지 않는다");

            brain.Tick(0.5f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attack));
            Assert.That(attacks, Is.EqualTo(1));

            brain.Tick(0.2f, 1.5f, true);
            brain.Tick(0.4f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            brain.Tick(0.1f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase), "쿨타임 중에는 다시 공격하지 않는다");
            brain.Tick(1f, 1.5f, true);
            brain.Tick(0.01f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Windup));
        }

        [Test]
        public void HeavyHitDuringWindupStaggersAndCancelsAttack()
        {
            var brain = new EnemyBrain(Melee());
            var attacks = 0;
            brain.AttackExecuted += () => attacks++;
            brain.Tick(0.1f, 1.5f, true);
            brain.Tick(0.1f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Windup));

            Assert.That(brain.OnDamaged(5, false), Is.False, "강인도 미만 피해로는 끊기지 않는다");
            Assert.That(brain.OnDamaged(12, false), Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Stagger));
            brain.Tick(1f, 1.5f, true);
            Assert.That(attacks, Is.EqualTo(0));
        }

        [Test]
        public void StunIgnoresPoiseAndLastsItsDuration()
        {
            var brain = new EnemyBrain(Melee());
            var attacks = 0;
            brain.AttackExecuted += () => attacks++;
            brain.Tick(0.1f, 1.5f, true);
            brain.Tick(0.1f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Windup));

            brain.Stun(1.2f); // 충격 착지: 경직 저항(Poise)과 무관
            Assert.That(brain.State, Is.EqualTo(EnemyState.Stagger));
            brain.Tick(1.0f, 1.5f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Stagger), "기절 시간(1.2초)이 끝나기 전에는 풀리지 않는다");
            brain.Tick(0.3f, 1.5f, true);
            Assert.That(brain.State, Is.Not.EqualTo(EnemyState.Stagger));
            Assert.That(attacks, Is.EqualTo(0), "기절로 끊긴 공격은 나가지 않는다");
        }

        [Test]
        public void RangedBacksOffWhileOnCooldown()
        {
            var def = Melee();
            def.Kind = EnemyKind.Ranged;
            def.AttackRange = 8;
            def.PreferredRange = 6;
            var brain = new EnemyBrain(def);

            brain.Tick(0.1f, 9, true); // Idle → Chase
            brain.Tick(0.1f, 9, true);
            Assert.That(brain.Move, Is.EqualTo(MoveIntent.TowardTarget), "사거리 밖이면 다가간다");

            brain.Tick(0.1f, 5, true); // 사거리 안 → Windup
            brain.Tick(0.5f, 5, true); // → Attack
            brain.Tick(0.2f, 5, true); // → Recover
            brain.Tick(0.4f, 5, true); // → Chase (쿨타임 시작)
            brain.Tick(0.1f, 3, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.Move, Is.EqualTo(MoveIntent.AwayFromTarget), "쿨타임 중 너무 가까우면 물러난다");
        }

        [Test]
        public void DeathIsFinal()
        {
            var brain = new EnemyBrain(Melee());
            brain.OnDamaged(100, true);
            brain.Tick(1f, 1f, true);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Dead));
        }
    }
}
