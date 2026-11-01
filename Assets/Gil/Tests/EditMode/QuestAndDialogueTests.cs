using System.Collections.Generic;
using System.Linq;
using Gil.Core;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Items;
using Gil.Core.Quests;
using NUnit.Framework;

namespace Gil.Tests
{
    /// <summary>실제 데이터로 「진료소 → 장터 → 첫 사냥」 흐름 전체를 엔진 없이 끝까지 돌려 본다.</summary>
    public class QuestAndDialogueTests
    {
        GameSession _session;
        DialogueRunner _dialogue;

        [SetUp]
        public void SetUp()
        {
            _session = new GameSession(TestData.Real);
            _dialogue = new DialogueRunner(_session);
        }

        /// <summary>대화를 끝까지 넘긴다. 선택지는 주어진 순서대로 고른다.</summary>
        List<string> Talk(string npcId, params int[] choices)
        {
            var seen = new List<string>();
            var choiceQueue = new Queue<int>(choices);
            _session.OnTalkedTo(npcId);
            _dialogue.Start(_session.Db.Npcs[npcId].Dialogue);
            var guard = 0;
            while (_dialogue.IsRunning && guard++ < 100)
            {
                seen.Add(_dialogue.Current.Id);
                if (_dialogue.VisibleChoices.Count > 0) _dialogue.Choose(choiceQueue.Count > 0 ? choiceQueue.Dequeue() : 0);
                else _dialogue.Continue();
            }
            return seen;
        }

        [Test]
        public void FullDemoFlow()
        {
            var demoEnded = false;
            _session.DemoEndRequested += () => demoEnded = true;

            // 진료소: 마고 첫 대화 → 회복약 2개, 「장터로」 수락
            var nodes = Talk("mago", 1);
            Assert.That(nodes, Does.Contain("cost"));
            Assert.That(_session.Inventory.Count("potion"), Is.EqualTo(2));
            Assert.That(_session.Wallet.Coins, Is.EqualTo(1000));
            Assert.That(_session.Quests.StateOf("go_market"), Is.EqualTo(QuestState.Active));

            // 마고에게 다시 말 걸어 진찰을 고르면 장터 힌트
            Assert.That(Talk("mago", 1), Is.EqualTo(new[] { "checkup", "checkup_heal", "hint_market" }));

            // 장터 도착 + 문탁 대화 → 「장터로」 자동 완료, 「첫 사냥」 수락
            _session.EnterArea("market");
            Talk("muntak", 0);
            Assert.That(_session.Quests.StateOf("go_market"), Is.EqualTo(QuestState.Completed));
            Assert.That(_session.Quests.StateOf("first_hunt"), Is.EqualTo(QuestState.Active));

            // 뒷골목의 모든 웨이브 처치
            foreach (var wave in _session.Db.Areas["alley"].Waves)
                foreach (var entry in wave.Entries)
                    for (var i = 0; i < entry.Count; i++) _session.OnEnemyKilled(entry.EnemyId);
            Assert.That(_session.Quests.StateOf("first_hunt"), Is.EqualTo(QuestState.ReadyToTurnIn));
            var coinsBeforeReport = _session.Wallet.Coins;

            // 보고 → 보상 + 데모 종료
            var report = Talk("muntak");
            Assert.That(report.First(), Is.EqualTo("report_1"));
            Assert.That(_session.Quests.StateOf("first_hunt"), Is.EqualTo(QuestState.Completed));
            Assert.That(_session.Wallet.Coins, Is.EqualTo(coinsBeforeReport + 1200));
            Assert.That(_session.Inventory.Count("hi_potion"), Is.EqualTo(1));
            Assert.That(demoEnded, Is.True);
        }

        [Test]
        public void KillsBeforeAcceptingDoNotCount()
        {
            _session.OnEnemyKilled("guard_bot");
            _session.Quests.Accept("first_hunt");
            Assert.That(_session.Quests.Get("first_hunt").Counts[0], Is.EqualTo(0));
        }

        [Test]
        public void ChoiceWithShopActionRequestsShop()
        {
            string openedShop = null;
            _session.ShopRequested += id => openedShop = id;
            Talk("muntak", 1);
            Assert.That(openedShop, Is.EqualTo("muntak_shop"));
            Assert.That(_session.Quests.StateOf("first_hunt"), Is.EqualTo(QuestState.Active));
        }

        [Test]
        public void ShopChargesCoinsAndRejectsWhenPoor()
        {
            _session.Wallet.Set(50);
            Assert.That(_session.Buy("muntak_shop", "potion"), Is.EqualTo(BuyResult.Ok));
            Assert.That(_session.Wallet.Coins, Is.EqualTo(15));
            Assert.That(_session.Buy("muntak_shop", "potion"), Is.EqualTo(BuyResult.NotEnoughCoins));
            Assert.That(_session.Buy("muntak_shop", "nonexistent"), Is.EqualTo(BuyResult.NotSold));
            Assert.That(_session.Inventory.Count("potion"), Is.EqualTo(1));
        }

        [Test]
        public void PotionIsNotWastedAtFullHealth()
        {
            _session.Inventory.Add("potion");
            Assert.That(_session.UseItem("potion"), Is.False);
            _session.PlayerHealth.Damage(50);
            Assert.That(_session.UseItem("potion"), Is.True);
            Assert.That(_session.PlayerHealth.Current, Is.EqualTo(_session.PlayerHealth.Max - 10));
            Assert.That(_session.Inventory.Count("potion"), Is.EqualTo(0));
        }

        [Test]
        public void SaveRoundTripRestoresProgress()
        {
            Talk("mago");
            _session.EnterArea("market");
            Talk("muntak");
            _session.OnEnemyKilled("guard_bot");
            _session.OnEnemyKilled("drone");
            _session.PlayerHealth.Damage(33);
            _session.Heart.Add(40);

            var json = _session.Capture().ToJson();
            var restored = new GameSession(TestData.Real);
            restored.Restore(SaveData.FromJson(json));

            Assert.That(restored.CurrentArea, Is.EqualTo("market"));
            Assert.That(restored.PlayerHealth.Current, Is.EqualTo(_session.PlayerHealth.Current));
            Assert.That(restored.Heart.Value, Is.EqualTo(40f));
            Assert.That(restored.Inventory.Count("potion"), Is.EqualTo(2));
            Assert.That(restored.Flags, Does.Contain("met_mago"));
            Assert.That(restored.Quests.Get("first_hunt").Counts, Is.EqualTo(new[] { 1, 1, 0 }));
            Assert.That(restored.Quests.StateOf("go_market"), Is.EqualTo(QuestState.Completed));
        }

        [Test]
        public void RejectsSaveFromNewerVersion()
        {
            var json = _session.Capture().ToJson().Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 99");
            Assert.Throws<System.FormatException>(() => SaveData.FromJson(json));
        }
    }
}
