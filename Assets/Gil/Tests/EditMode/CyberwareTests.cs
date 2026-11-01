using System.Linq;
using Gil.Core;
using Gil.Core.Combat;
using Gil.Core.Cyber;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using NUnit.Framework;

namespace Gil.Tests
{
    public class CyberwareTests
    {
        GameSession _session;

        [SetUp]
        public void SetUp() => _session = new GameSession(TestData.Real);

        [Test]
        public void NewGameStartsWithStarterHeartOnly()
        {
            Assert.That(_session.Cyberware.Installed.Select(c => c.Id), Is.EqualTo(new[] { "heart_mk1" }));
            Assert.That(_session.Cyberware.Capacity, Is.EqualTo(6));
            Assert.That(_session.Cyberware.Load, Is.EqualTo(0));
            Assert.That(_session.Stats.MaxHp, Is.EqualTo(TestData.Real.Player.MaxHp));
        }

        [Test]
        public void InstallChargesCoinsAndAppliesStats()
        {
            _session.Wallet.Set(1000);
            Assert.That(_session.InstallCyberware("subdermal"), Is.EqualTo(InstallResult.Ok));
            Assert.That(_session.Wallet.Coins, Is.EqualTo(300));
            Assert.That(_session.Stats.Defense, Is.EqualTo(TestData.Real.Player.Defense + 6));
            Assert.That(_session.PlayerHealth.Max, Is.EqualTo(TestData.Real.Player.MaxHp + 20));
            // 늘어난 최대 체력만큼 현재 체력도 는다.
            Assert.That(_session.PlayerHealth.Current, Is.EqualTo(_session.PlayerHealth.Max));
        }

        [Test]
        public void RejectsWhenPoorOrAlreadyInstalled()
        {
            _session.Wallet.Set(500);
            Assert.That(_session.InstallCyberware("subdermal"), Is.EqualTo(InstallResult.NotEnoughCoins));
            Assert.That(_session.Wallet.Coins, Is.EqualTo(500));
            Assert.That(_session.InstallCyberware("heart_mk1"), Is.EqualTo(InstallResult.AlreadyInstalled));
            Assert.That(_session.InstallCyberware("nope"), Is.EqualTo(InstallResult.Unknown));
        }

        [Test]
        public void CapacityLimitsLoadUntilHeartUpgrade()
        {
            _session.Wallet.Set(10000);
            Assert.That(_session.InstallCyberware("piston_arm"), Is.EqualTo(InstallResult.Ok));        // 부하 4/6
            Assert.That(_session.InstallCyberware("swallow_legs"), Is.EqualTo(InstallResult.OverCapacity)); // 7 > 6
            Assert.That(_session.InstallCyberware("heart_mk2"), Is.EqualTo(InstallResult.Ok));        // 용량 11
            Assert.That(_session.InstallCyberware("swallow_legs"), Is.EqualTo(InstallResult.Ok));      // 7/11
            Assert.That(_session.Cyberware.Load, Is.EqualTo(7));
        }

        [Test]
        public void ReplacingSlotSwapsItem()
        {
            _session.Wallet.Set(10000);
            _session.InstallCyberware("volt_knuckle");
            _session.InstallCyberware("piston_arm");
            Assert.That(_session.Cyberware.InSlot("arms").Id, Is.EqualTo("piston_arm"));
            Assert.That(_session.Cyberware.Gimmick(Gimmicks.ChainShock), Is.Null);
            Assert.That(_session.Cyberware.Gimmick(Gimmicks.ShockFinisher).Param("radius"), Is.EqualTo(2.8f));
        }

        [Test]
        public void PercentStatsMultiplyAndParkourCopiesAreIndependent()
        {
            _session.Wallet.Set(10000);
            _session.InstallCyberware("swallow_legs");
            var b = TestData.Real.Player;
            Assert.That(_session.Stats.MoveSpeed, Is.EqualTo(b.MoveSpeed * 1.08f).Within(0.001f));
            Assert.That(_session.Stats.Parkour.ClimbMaxHeight, Is.EqualTo(b.Parkour.ClimbMaxHeight + 1f).Within(0.001f));
            // 원본 정의는 바뀌지 않는다.
            Assert.That(b.Parkour.ClimbMaxHeight, Is.EqualTo(2.6f).Within(0.001f));
        }

        [Test]
        public void SecondWindRevivesOncePerArea()
        {
            _session.Wallet.Set(10000);
            _session.InstallCyberware("heart_mk2");
            _session.InstallCyberware("second_ventricle");
            _session.EnterArea("alley");

            _session.PlayerHealth.Damage(9999);
            Assert.That(_session.TryUseSecondWind(), Is.True);
            Assert.That(_session.PlayerHealth.IsDead, Is.False);
            Assert.That(_session.PlayerHealth.Current, Is.EqualTo((int)System.Math.Round(_session.PlayerHealth.Max * 0.35f)));

            _session.PlayerHealth.Damage(9999);
            Assert.That(_session.TryUseSecondWind(), Is.False, "같은 지역에서는 한 번뿐");

            _session.PlayerHealth.Revive();
            _session.EnterArea("market");
            _session.PlayerHealth.Damage(9999);
            Assert.That(_session.TryUseSecondWind(), Is.True, "지역을 옮기면 다시 충전");
        }

        [Test]
        public void SaveRoundTripKeepsCyberware()
        {
            _session.Wallet.Set(10000);
            _session.InstallCyberware("heart_mk2");
            _session.InstallCyberware("reflex_wire");
            var restored = new GameSession(TestData.Real);
            restored.Restore(SaveData.FromJson(_session.Capture().ToJson()));
            Assert.That(restored.Cyberware.Installed.Select(c => c.Id), Is.EquivalentTo(new[] { "heart_mk2", "reflex_wire" }));
            Assert.That(restored.Stats.Rolling.Cooldown, Is.EqualTo(_session.Stats.Rolling.Cooldown).Within(0.0001f));
        }

        [Test]
        public void VersionOneSaveLoadsWithStarterCyberware()
        {
            var v1 = "{ \"version\": 1, \"area\": \"clinic\", \"hp\": 80, \"coins\": 5 }";
            var restored = new GameSession(TestData.Real);
            restored.Restore(SaveData.FromJson(v1));
            Assert.That(restored.Cyberware.Installed.Select(c => c.Id), Is.EqualTo(new[] { "heart_mk1" }));
            Assert.That(restored.PlayerHealth.Current, Is.EqualTo(80));
        }

        [Test]
        public void DialogueCanOpenRipperdocAndGiveCoins()
        {
            var opened = false;
            _session.CyberwareRequested += () => opened = true;
            _session.Execute(DialogueCommands.Parse("give_coins:30"));
            _session.Execute(DialogueCommands.Parse("open_cyberware"));
            Assert.That(opened, Is.True);
            Assert.That(_session.Wallet.Coins, Is.EqualTo(30));
            Assert.That(_session.Evaluate(DialogueCommands.Parse("has_cyberware:heart_mk1")), Is.True);
        }

        [Test]
        public void HeartGainMultiplierApplies()
        {
            _session.Wallet.Set(10000);
            _session.InstallCyberware("heart_mk2");
            _session.Heart.OnHitLanded();
            Assert.That(_session.Heart.Value, Is.EqualTo(TestData.Real.Player.Heart.GainPerHit * 1.2f).Within(0.001f));
        }

        [Test]
        public void StatDescriptions()
        {
            Assert.That(StatKeys.Describe("attack", 4), Is.EqualTo("공격력 +4"));
            Assert.That(StatKeys.Describe("rollingCooldown", -0.3f), Is.EqualTo("구르기 재사용 대기 -30%"));
            Assert.That(StatKeys.Describe("climbMaxHeight", 1f), Is.EqualTo("오를 수 있는 높이 +1m"));
        }
    }

    public class ParkourTests
    {
        static ParkourDef Def => TestData.Real.Player.Parkour;

        [Test]
        public void LowObstacleIsVaulted()
        {
            Assert.That(ParkourRules.Choose(Def, 0.9f, vaultable: true, climbable: true, landingClear: true), Is.EqualTo(ParkourMove.Vault));
        }

        [Test]
        public void BlockedLandingFallsBackToClimb()
        {
            Assert.That(ParkourRules.Choose(Def, 0.9f, vaultable: true, climbable: true, landingClear: false), Is.EqualTo(ParkourMove.Climb));
        }

        [Test]
        public void TallStructureIsClimbedButTooTallIsNot()
        {
            Assert.That(ParkourRules.Choose(Def, 2.4f, vaultable: false, climbable: true, landingClear: true), Is.EqualTo(ParkourMove.Climb));
            Assert.That(ParkourRules.Choose(Def, 3.5f, vaultable: false, climbable: true, landingClear: true), Is.EqualTo(ParkourMove.None));
            Assert.That(ParkourRules.Choose(Def, 0f, vaultable: true, climbable: true, landingClear: true), Is.EqualTo(ParkourMove.None));
        }

        [Test]
        public void HighGroundIsOutOfMeleeReach()
        {
            Assert.That(ParkourRules.CanMeleeReach(Def, 0.3f), Is.True);
            Assert.That(ParkourRules.CanMeleeReach(Def, 2.0f), Is.False);
        }

        [Test]
        public void PlungeNeedsHeightAndScalesWithIt()
        {
            Assert.That(ParkourRules.PlungeMultiplier(Def, 0.5f), Is.EqualTo(0f));
            var low = ParkourRules.PlungeMultiplier(Def, Def.PlungeMinHeight);
            var high = ParkourRules.PlungeMultiplier(Def, Def.PlungeMinHeight * 3f);
            Assert.That(low, Is.EqualTo(Def.PlungeDamageMultiplier).Within(0.001f));
            Assert.That(high, Is.EqualTo(Def.PlungeDamageMultiplier * 1.5f).Within(0.001f));
        }

        [Test]
        public void DoubleRollingRechargesOneAtATime()
        {
            var rolling = new RollingDef { Duration = 0.3f, Distance = 4, InvulnerableTime = 0.2f, Cooldown = 1f };
            var timer = new RollingTimer(rolling, charges: 2);
            Assert.That(timer.TryStart(), Is.True);
            timer.Tick(0.31f);
            Assert.That(timer.TryStart(), Is.True, "두 번째 충전으로 연속 구르기");
            timer.Tick(0.31f);
            Assert.That(timer.TryStart(), Is.False, "충전 소진");
            timer.Tick(0.4f); // 첫 구르기 후 1.02초 → 하나 충전
            Assert.That(timer.Available, Is.EqualTo(1));
            timer.Tick(1.0f);
            Assert.That(timer.Available, Is.EqualTo(2));
        }
    }
}
