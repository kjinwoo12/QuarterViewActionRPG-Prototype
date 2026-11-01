using System;
using System.Collections.Generic;
using Gil.Core.Util;
using NUnit.Framework;

namespace Gil.Tests
{
    public class JsonTests
    {
        [Test]
        public void ParsesNestedValuesCommentsAndTrailingCommas()
        {
            var root = (Dictionary<string, object>)Json.Parse(@"
                // 기획 메모
                { ""name"": ""마고"", ""hp"": 12.5, ""tags"": [""a"", ""b"",], ""ok"": true, ""none"": null, }");

            Assert.That(root["name"], Is.EqualTo("마고"));
            Assert.That(root["hp"], Is.EqualTo(12.5));
            Assert.That((List<object>)root["tags"], Is.EqualTo(new object[] { "a", "b" }));
            Assert.That(root["ok"], Is.EqualTo(true));
            Assert.That(root["none"], Is.Null);
        }

        [Test]
        public void RoundTripsEscapedStrings()
        {
            var text = "줄바꿈\n\"따옴표\" \\ 탭\t";
            var json = Json.Serialize(new Dictionary<string, object> { ["t"] = text });
            Assert.That(((Dictionary<string, object>)Json.Parse(json))["t"], Is.EqualTo(text));
        }

        [Test]
        public void ReportsLineNumberOnError()
        {
            var e = Assert.Throws<FormatException>(() => Json.Parse("{\n\"a\": 1,\n\"b\" 2\n}"));
            StringAssert.Contains("3번째 줄", e.Message);
        }

        [Test]
        public void JsonObjectReportsMissingFieldWithPath()
        {
            var obj = new JsonObject(Json.Parse("{\"enemy\": {}}"), "enemies").Object("enemy");
            var e = Assert.Throws<FormatException>(() => obj.Int("maxHp"));
            StringAssert.Contains("enemies.enemy.maxHp", e.Message);
        }
    }
}
