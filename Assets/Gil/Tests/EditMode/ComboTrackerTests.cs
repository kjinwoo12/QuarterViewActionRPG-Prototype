using System.Collections.Generic;
using Gil.Core.Combat;
using Gil.Core.Data;
using NUnit.Framework;

namespace Gil.Tests
{
    public class ComboTrackerTests
    {
        static List<ComboStepDef> ThreeHits() => new List<ComboStepDef>
        {
            new ComboStepDef { Windup = 0.1f, Active = 0.1f, Recovery = 0.3f, ComboWindow = 0.2f },
            new ComboStepDef { Windup = 0.1f, Active = 0.1f, Recovery = 0.3f, ComboWindow = 0.2f },
            new ComboStepDef { Windup = 0.2f, Active = 0.1f, Recovery = 0.5f, ComboWindow = 0f },
        };

        [Test]
        public void SinglePressPlaysOneStepAndReturnsToIdle()
        {
            var combo = new ComboTracker(ThreeHits());
            var hits = new List<int>();
            combo.HitFrame += hits.Add;

            combo.PressAttack();
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Windup));
            combo.Tick(0.1f);
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Active));
            combo.Tick(0.1f);
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Recovery));
            combo.Tick(0.3f);
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Idle));
            Assert.That(hits, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void PressDuringWindupIsBufferedIntoNextStep()
        {
            var combo = new ComboTracker(ThreeHits());
            combo.PressAttack();
            combo.Tick(0.05f);
            combo.PressAttack(); // 선딜 중 입력 → 버퍼
            combo.Tick(0.15f);   // 판정 종료 시점에 2타로 이어짐
            Assert.That(combo.StepIndex, Is.EqualTo(1));
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Windup));
        }

        [Test]
        public void PressInsideComboWindowChains()
        {
            var combo = new ComboTracker(ThreeHits());
            combo.PressAttack();
            combo.Tick(0.2f);  // 후딜 진입
            combo.Tick(0.15f); // 후딜 0.15초 (창 0.2초 안)
            combo.PressAttack();
            Assert.That(combo.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void PressAfterComboWindowIsIgnoredUntilIdle()
        {
            var combo = new ComboTracker(ThreeHits());
            combo.PressAttack();
            combo.Tick(0.2f);
            combo.Tick(0.25f); // 후딜 0.25초 (창 밖)
            combo.PressAttack();
            Assert.That(combo.StepIndex, Is.EqualTo(0));
            combo.Tick(0.1f);
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Idle));
            combo.PressAttack();
            Assert.That(combo.StepIndex, Is.EqualTo(0), "콤보가 끊기면 1타부터 다시 시작한다");
        }

        [Test]
        public void LastStepDoesNotLoop()
        {
            var combo = new ComboTracker(ThreeHits());
            var hits = new List<int>();
            combo.HitFrame += hits.Add;
            // 1타(0.2초) + 2타(0.2초) + 3타 선딜·판정(0.3초) = 0.7초, 3타 후딜은 1.2초에 끝난다.
            // 1.0초 동안 연타해도 3타 후딜 중에는 1타가 다시 나오지 않아야 한다.
            for (var i = 0; i < 20; i++)
            {
                combo.PressAttack();
                combo.Tick(0.05f);
            }
            Assert.That(hits, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(combo.StepIndex, Is.EqualTo(2), "3타 후딜 중");
        }

        [Test]
        public void LongFrameDoesNotSkipHitFrame()
        {
            var combo = new ComboTracker(ThreeHits());
            var hits = 0;
            combo.HitFrame += _ => hits++;
            combo.PressAttack();
            combo.Tick(1f); // 렉으로 한 프레임이 1초
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(combo.Phase, Is.EqualTo(AttackPhase.Idle));
        }

        [Test]
        public void CancelReturnsToIdle()
        {
            var combo = new ComboTracker(ThreeHits());
            combo.PressAttack();
            combo.Cancel();
            Assert.That(combo.IsAttacking, Is.False);
        }
    }
}
