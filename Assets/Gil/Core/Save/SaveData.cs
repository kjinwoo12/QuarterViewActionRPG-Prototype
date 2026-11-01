using System;
using System.Collections.Generic;
using System.Linq;
using Gil.Core.Quests;
using Gil.Core.Util;

namespace Gil.Core
{
    /// <summary>
    /// 세이브 파일 한 개의 내용. 엔진 직렬화(JsonUtility)에 기대지 않고 Core의 Json으로 읽고 써서,
    /// 형식 변경을 단위 테스트로 지킬 수 있게 한다.
    /// </summary>
    public sealed class SaveData
    {
        /// <summary>v2: 의체(cyberware) 추가. v1 세이브는 기본 의체로 읽는다.</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public string Area;
        public int Hp;
        public float Heart;
        public int Coins;
        public readonly SortedDictionary<string, int> Items = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> Flags = new List<string>();
        public readonly List<QuestSave> Quests = new List<QuestSave>();
        public readonly List<string> Cyberware = new List<string>();
        public string SavedAt;

        public sealed class QuestSave
        {
            public string Id;
            public QuestState State;
            public List<int> Counts = new List<int>();
        }

        public static SaveData Capture(GameSession session)
        {
            var save = new SaveData
            {
                Area = session.CurrentArea,
                Hp = session.PlayerHealth.Current,
                Heart = session.Heart.Value,
                Coins = session.Wallet.Coins,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
            foreach (var pair in session.Inventory.Counts) save.Items[pair.Key] = pair.Value;
            save.Flags.AddRange(session.Flags.OrderBy(f => f, StringComparer.Ordinal));
            save.Cyberware.AddRange(session.Cyberware.Installed.Select(c => c.Id));
            foreach (var q in session.Quests.All)
                save.Quests.Add(new QuestSave { Id = q.Def.Id, State = q.State, Counts = q.Counts.ToList() });
            return save;
        }

        public string ToJson()
        {
            var items = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in Items) items[pair.Key] = pair.Value;

            var root = new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = Version,
                ["area"] = Area,
                ["hp"] = Hp,
                ["heart"] = Heart,
                ["coins"] = Coins,
                ["items"] = items,
                ["flags"] = Flags.Cast<object>().ToList(),
                ["cyberware"] = Cyberware.Cast<object>().ToList(),
                ["quests"] = Quests.Select(q => (object)new SortedDictionary<string, object>(StringComparer.Ordinal)
                {
                    ["id"] = q.Id,
                    ["state"] = q.State.ToString(),
                    ["counts"] = q.Counts.Cast<object>().ToList(),
                }).ToList(),
                ["savedAt"] = SavedAt,
            };
            return Json.Serialize(root, pretty: true);
        }

        public static SaveData FromJson(string text)
        {
            var root = new JsonObject(Json.Parse(text), "save");
            var version = root.Int("version");
            if (version > CurrentVersion)
                throw new FormatException($"더 새 버전의 세이브입니다 (v{version}). 게임을 업데이트해 주세요");

            var save = new SaveData
            {
                Version = version,
                Area = root.String("area", ""),
                Hp = root.Int("hp"),
                Heart = root.Float("heart", 0f),
                Coins = root.Int("coins", 0),
                SavedAt = root.String("savedAt", ""),
            };

            foreach (var pair in root.IntMap("items")) save.Items[pair.Key] = pair.Value;
            save.Flags.AddRange(root.Strings("flags"));
            save.Cyberware.AddRange(root.Strings("cyberware"));
            foreach (var q in root.Objects("quests"))
            {
                var state = q.String("state");
                if (!Enum.TryParse(state, out QuestState parsed))
                    throw new FormatException($"{q.Path}.state: 알 수 없는 상태 '{state}'");
                save.Quests.Add(new QuestSave { Id = q.String("id"), State = parsed, Counts = q.Ints("counts").ToList() });
            }
            return save;
        }
    }
}
