using System;
using System.Collections.Generic;

namespace Gil.Core.Dialogue
{
    public enum ArgKind
    {
        Quest,
        Shop,
        Item,
        Area,
        Flag,
        Count,
        Cyberware,
    }

    /// <summary>
    /// 대화 데이터에 쓰는 액션/조건 문법을 한곳에서 정의한다.
    /// 액션: "accept_quest:first_hunt", 조건: "quest_ready:first_hunt", "!flag:met_mago".
    /// 검증기와 런타임이 같은 표를 보므로, 새 명령은 여기에 한 줄 추가하면 양쪽에 반영된다.
    /// </summary>
    public static class DialogueCommands
    {
        public static readonly IReadOnlyDictionary<string, ArgKind[]> Actions = new Dictionary<string, ArgKind[]>
        {
            ["accept_quest"] = new[] { ArgKind.Quest },
            ["turn_in_quest"] = new[] { ArgKind.Quest },
            ["open_shop"] = new[] { ArgKind.Shop },
            ["give_item"] = new[] { ArgKind.Item, ArgKind.Count },
            ["set_flag"] = new[] { ArgKind.Flag },
            ["heal_full"] = new ArgKind[0],
            ["travel"] = new[] { ArgKind.Area },
            ["end_demo"] = new ArgKind[0],
            ["give_coins"] = new[] { ArgKind.Count },
            ["open_cyberware"] = new ArgKind[0],
        };

        public static readonly IReadOnlyDictionary<string, ArgKind[]> Conditions = new Dictionary<string, ArgKind[]>
        {
            ["flag"] = new[] { ArgKind.Flag },
            ["quest_none"] = new[] { ArgKind.Quest },
            ["quest_active"] = new[] { ArgKind.Quest },
            ["quest_ready"] = new[] { ArgKind.Quest },
            ["quest_done"] = new[] { ArgKind.Quest },
            ["has_item"] = new[] { ArgKind.Item },
            ["has_cyberware"] = new[] { ArgKind.Cyberware },
        };

        public readonly struct Command
        {
            public readonly string Verb;
            public readonly string[] Args;
            public readonly bool Negated;

            public Command(string verb, string[] args, bool negated)
            {
                Verb = verb;
                Args = args;
                Negated = negated;
            }

            public string Arg(int index) => index < Args.Length ? Args[index] : null;
        }

        public static Command Parse(string text)
        {
            var negated = text.StartsWith("!", StringComparison.Ordinal);
            if (negated) text = text.Substring(1);
            var parts = text.Split(':');
            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);
            return new Command(parts[0].Trim(), args, negated);
        }

        /// <summary>조건 문자열을 평가한다. 빈 조건은 참이다.</summary>
        public static bool Evaluate(string condition, Func<Command, bool> evaluator)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            var command = Parse(condition);
            var result = evaluator(command);
            return command.Negated ? !result : result;
        }
    }
}
