using System;
using System.Collections.Generic;
using System.Linq;
using Gil.Core.Data;

namespace Gil.Core.Quests
{
    public enum QuestState
    {
        NotStarted,
        Active,
        /// <summary>목표는 모두 채웠고 보고(turnInNpc와 대화)만 남은 상태.</summary>
        ReadyToTurnIn,
        Completed,
    }

    public sealed class QuestProgress
    {
        public readonly QuestDef Def;
        public QuestState State;
        public readonly int[] Counts;

        public QuestProgress(QuestDef def)
        {
            Def = def;
            Counts = new int[def.Objectives.Count];
        }

        public bool ObjectivesDone => Def.Objectives.Select((o, i) => Counts[i] >= o.Count).All(done => done);
    }

    /// <summary>
    /// 퀘스트 진행 관리. 게임 쪽은 "누구와 대화했다 / 무엇을 잡았다 / 어디에 도착했다"만 알려 주고,
    /// 어떤 퀘스트가 몇 개 진행됐는지는 여기서 판단한다.
    /// </summary>
    public sealed class QuestLog
    {
        readonly GameDatabase _db;
        readonly Dictionary<string, QuestProgress> _quests = new Dictionary<string, QuestProgress>();

        public event Action<QuestProgress> Changed;
        /// <summary>보상 지급이 필요한 완료 시점.</summary>
        public event Action<QuestProgress> Completed;

        public QuestLog(GameDatabase db) => _db = db;

        public IEnumerable<QuestProgress> All => _quests.Values;
        public IEnumerable<QuestProgress> Tracked =>
            _quests.Values.Where(q => q.State == QuestState.Active || q.State == QuestState.ReadyToTurnIn);

        public QuestState StateOf(string questId) =>
            _quests.TryGetValue(questId, out var q) ? q.State : QuestState.NotStarted;

        public QuestProgress Get(string questId) => _quests.TryGetValue(questId, out var q) ? q : null;

        public bool Accept(string questId)
        {
            if (_quests.ContainsKey(questId)) return false;
            if (!_db.Quests.TryGetValue(questId, out var def)) throw new KeyException(questId);
            var progress = new QuestProgress(def) { State = QuestState.Active };
            _quests[questId] = progress;
            Changed?.Invoke(progress);
            Evaluate(progress); // 수락 전에 이미 도착해 있는 경우 등은 다음 Notify에서 처리된다.
            return true;
        }

        public void Notify(ObjectiveType type, string target)
        {
            foreach (var q in _quests.Values.Where(q => q.State == QuestState.Active).ToList())
            {
                var changed = false;
                for (var i = 0; i < q.Def.Objectives.Count; i++)
                {
                    var o = q.Def.Objectives[i];
                    if (o.Type != type || o.Target != target || q.Counts[i] >= o.Count) continue;
                    q.Counts[i]++;
                    changed = true;
                }
                if (!changed) continue;
                Changed?.Invoke(q);
                Evaluate(q);
            }
        }

        /// <summary>보고 가능한 상태면 완료 처리한다.</summary>
        public bool TurnIn(string questId)
        {
            if (!_quests.TryGetValue(questId, out var q) || q.State != QuestState.ReadyToTurnIn) return false;
            Complete(q);
            return true;
        }

        public void Clear() => _quests.Clear();

        /// <summary>세이브 복원용.</summary>
        public void Restore(string questId, QuestState state, IReadOnlyList<int> counts)
        {
            if (!_db.Quests.TryGetValue(questId, out var def)) return; // 데이터에서 지워진 퀘스트는 무시
            var q = new QuestProgress(def) { State = state };
            for (var i = 0; i < q.Counts.Length && i < counts.Count; i++) q.Counts[i] = counts[i];
            _quests[questId] = q;
            Changed?.Invoke(q);
        }

        void Evaluate(QuestProgress q)
        {
            if (q.State != QuestState.Active || !q.ObjectivesDone) return;
            if (string.IsNullOrEmpty(q.Def.TurnInNpc))
            {
                Complete(q);
            }
            else
            {
                q.State = QuestState.ReadyToTurnIn;
                Changed?.Invoke(q);
            }
        }

        void Complete(QuestProgress q)
        {
            q.State = QuestState.Completed;
            Changed?.Invoke(q);
            Completed?.Invoke(q);
        }

        public sealed class KeyException : Exception
        {
            public KeyException(string questId) : base($"없는 퀘스트 '{questId}'") { }
        }
    }
}
