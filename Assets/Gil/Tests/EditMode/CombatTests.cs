using Gil.Core.Combat;
using Gil.Core.Data;
using NUnit.Framework;

namespace Gil.Tests
{
    public class CombatTests
    {
        [Test]
        public void DefenseReducesDamageProportionally()
        {
            var noDefense = DamageCalculator.Compute(20, 1f, 0, 0f, 2f, 0.5f);
            var withDefense = DamageCalculator.Compute(20, 1f, 25, 0f, 2f, 0.5f);
            Assert.That(noDefense.Amount, Is.EqualTo(20));
            Assert.That(withDefense.Amount, Is.EqualTo(16)); // 20 × 100/125
        }

        [Test]
        public void DamageIsAtLeastOne()
        {
            Assert.That(DamageCalculator.Compute(1, 0.1f, 999, 0f, 2f, 0.5f).Amount, Is.EqualTo(1));
        }

        [Test]
        public void CritAppliesWhenRollBelowChance()
        {
            var crit = DamageCalculator.Compute(10, 1f, 0, 0.2f, 1.5f, 0.1f);
            var normal = DamageCalculator.Compute(10, 1f, 0, 0.2f, 1.5f, 0.3f);
            Assert.That(crit.IsCritical, Is.True);
            Assert.That(crit.Amount, Is.EqualTo(15));
            Assert.That(normal.IsCritical, Is.False);
        }

        [Test]
        public void HealthClampsAndRaisesDiedOnce()
        {
            var hp = new Health(50);
            var died = 0;
            hp.Died += () => died++;
            Assert.That(hp.Damage(30), Is.EqualTo(30));
            Assert.That(hp.Damage(30), Is.EqualTo(20));
            Assert.That(hp.Damage(10), Is.EqualTo(0));
            Assert.That(hp.Heal(10), Is.EqualTo(0), "죽은 대상은 회복되지 않는다");
            Assert.That(died, Is.EqualTo(1));
        }

        [Test]
        public void HeartGaugeFillsOnHitsAndIsSpentByOverdrive()
        {
            var def = new HeartDef { Max = 100, GainPerHit = 30, GainPerDamageTaken = 10, SkillCost = 100 };
            var heart = new HeartGauge(def);
            heart.OnHitLanded();
            heart.OnHitLanded();
            heart.OnHitLanded();
            Assert.That(heart.CanOverdrive, Is.False);
            heart.OnDamageTaken();
            Assert.That(heart.CanOverdrive, Is.True);
            Assert.That(heart.TrySpend(), Is.True);
            Assert.That(heart.Value, Is.EqualTo(0f));
            Assert.That(heart.TrySpend(), Is.False);
        }

        [Test]
        public void RollingHasInvulnerabilityWindowAndCooldown()
        {
            var rolling = new RollingTimer(new RollingDef { Duration = 0.4f, Distance = 4f, InvulnerableTime = 0.25f, Cooldown = 0.6f });
            Assert.That(rolling.TryStart(), Is.True);
            Assert.That(rolling.IsInvulnerable, Is.True);
            rolling.Tick(0.3f);
            Assert.That(rolling.IsInvulnerable, Is.False, "무적은 구르기 동작보다 먼저 끝난다");
            Assert.That(rolling.IsRolling, Is.True);
            rolling.Tick(0.2f);
            Assert.That(rolling.IsRolling, Is.False);
            Assert.That(rolling.TryStart(), Is.False, "쿨타임 중에는 다시 구를 수 없다");
            rolling.Tick(0.2f);
            Assert.That(rolling.TryStart(), Is.True);
        }

        static StaminaDef StaminaDefaults => new StaminaDef { Max = 100f, RegenDelay = 1f, RegenPerSecond = 25f };

        [Test]
        public void RollingSpendsStaminaAndNeedsEnough()
        {
            var stamina = new Stamina(StaminaDefaults);
            Assert.That(stamina.Value, Is.EqualTo(100f));
            Assert.That(stamina.TrySpend(30f), Is.True);
            Assert.That(stamina.TrySpend(30f), Is.True);
            Assert.That(stamina.TrySpend(30f), Is.True);
            Assert.That(stamina.Value, Is.EqualTo(10f).Within(0.001f));
            Assert.That(stamina.TrySpend(30f), Is.False, "30 미만이면 구를 수 없다");
            Assert.That(stamina.Value, Is.EqualTo(10f).Within(0.001f), "실패하면 소모하지 않는다");
        }

        [Test]
        public void SprintDrainsPerSecondAndRegenStartsAfterDelay()
        {
            var stamina = new Stamina(StaminaDefaults);
            for (var i = 0; i < 20; i++)
            {
                stamina.Tick(0.1f);
                Assert.That(stamina.Sprint(true, true, 10f, 0.1f), Is.True);
            }
            Assert.That(stamina.Value, Is.EqualTo(80f).Within(0.01f), "2초 달리면 20 소모");

            stamina.Tick(0.9f);
            Assert.That(stamina.Value, Is.EqualTo(80f).Within(0.01f), "멈추고 1초가 지나기 전에는 회복하지 않는다");
            stamina.Tick(0.1f);
            stamina.Tick(0.4f);
            Assert.That(stamina.Value, Is.EqualTo(92.5f).Within(0.01f), "1초 뒤부터 초당 25 회복");
            stamina.Tick(1f);
            Assert.That(stamina.Value, Is.EqualTo(100f), "최대치를 넘지 않는다");
        }

        [Test]
        public void SprintStopsWhenEmptyUntilButtonReleased()
        {
            var stamina = new Stamina(StaminaDefaults);
            Assert.That(stamina.TrySpend(95f), Is.True);
            Assert.That(stamina.Sprint(true, true, 10f, 1f), Is.True);
            Assert.That(stamina.Value, Is.EqualTo(0f));
            Assert.That(stamina.Exhausted, Is.True);

            stamina.Tick(1.5f);
            Assert.That(stamina.Value, Is.GreaterThan(0f));
            Assert.That(stamina.Sprint(true, true, 10f, 0.1f), Is.False, "누른 채로는 회복돼도 다시 달리지 않는다");
            Assert.That(stamina.Sprint(false, true, 10f, 0.1f), Is.False);
            Assert.That(stamina.Exhausted, Is.False, "버튼을 떼면 지친 상태가 풀린다");
            Assert.That(stamina.Sprint(true, true, 10f, 0.1f), Is.True, "다시 누르면 달린다");
        }

        [Test]
        public void SprintDoesNotDrainWhileNotRunning()
        {
            var stamina = new Stamina(StaminaDefaults);
            Assert.That(stamina.Sprint(true, false, 10f, 1f), Is.False, "멈춰 있거나 공격 중이면 달리지 않는다");
            Assert.That(stamina.Value, Is.EqualTo(100f));
        }
    }
}
