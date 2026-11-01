using System.Linq;
using Gil.Core.Data;
using NUnit.Framework;

namespace Gil.Tests
{
    /// <summary>실제 기획 데이터가 항상 검증을 통과하는지 CI에서 확인한다.</summary>
    public class DataTests
    {
        [Test]
        public void RealDataHasNoValidationErrors()
        {
            var result = DataValidator.Validate(TestData.Real);
            Assert.That(result.HasErrors, Is.False, result.ToString());
        }

        [Test]
        public void RealDataHasNoValidationWarnings()
        {
            var result = DataValidator.Validate(TestData.Real);
            Assert.That(result.WarningCount, Is.EqualTo(0), result.ToString());
        }

        [Test]
        public void DetectsQuestTargetingUnknownEnemy()
        {
            var db = TestData.With("quests", json => json.Replace("\"target\": \"drone\"", "\"target\": \"dron\""));
            var result = DataValidator.Validate(db);
            Assert.That(result.Items.Any(d => d.Message.Contains("없는 적 'dron'")), result.ToString());
        }

        [Test]
        public void DetectsKillObjectiveThatCannotBeCompleted()
        {
            var db = TestData.With("quests", json => json.Replace("\"count\": 3", "\"count\": 9"));
            var result = DataValidator.Validate(db);
            Assert.That(result.Items.Any(d => d.Path.Contains("first_hunt") && d.Message.Contains("잡을 수 없습니다")),
                result.ToString());
        }

        [Test]
        public void DetectsDialogueActionWithUnknownQuest()
        {
            var db = TestData.With("dialogues", json => json.Replace("accept_quest:first_hunt", "accept_quest:frist_hunt"));
            var result = DataValidator.Validate(db);
            Assert.That(result.Items.Any(d => d.Message.Contains("frist_hunt")), result.ToString());
        }

        [Test]
        public void DetectsBrokenDialogueLink()
        {
            var db = TestData.With("dialogues", json => json.Replace("\"next\": \"intro_2\"", "\"next\": \"intro_9\""));
            var result = DataValidator.Validate(db);
            Assert.That(result.Items.Any(d => d.Message.Contains("없는 노드 'intro_9'")), result.ToString());
        }

        [Test]
        public void DetectsUnknownDialogueCommand()
        {
            var db = TestData.With("dialogues", json => json.Replace("heal_full", "heal_ful"));
            var result = DataValidator.Validate(db);
            Assert.That(result.Items.Any(d => d.Message.Contains("알 수 없는 명령 'heal_ful'")), result.ToString());
        }
    }
}
