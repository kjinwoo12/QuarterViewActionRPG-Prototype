using System;
using System.Collections.Generic;
using Gil.Core.Data;

namespace Gil.Core.Dialogue
{
    /// <summary>
    /// 데이터로 정의된 대화를 한 노드씩 진행한다. 조건 분기와 액션 실행은 <see cref="GameSession"/>에 맡긴다.
    /// UI는 Current/VisibleChoices를 그리고, 입력에 따라 Continue/Choose만 부르면 된다.
    /// </summary>
    public sealed class DialogueRunner
    {
        const int MaxBranchHops = 32;

        readonly GameSession _session;
        DialogueDef _dialogue;
        readonly List<DialogueChoiceDef> _visibleChoices = new List<DialogueChoiceDef>();

        public DialogueNodeDef Current { get; private set; }
        public IReadOnlyList<DialogueChoiceDef> VisibleChoices => _visibleChoices;
        public bool IsRunning => Current != null;

        public event Action<DialogueNodeDef> NodeEntered;
        public event Action Ended;

        public DialogueRunner(GameSession session) => _session = session;

        public void Start(string dialogueId)
        {
            if (!_session.Db.Dialogues.TryGetValue(dialogueId, out _dialogue))
                throw new ArgumentException($"없는 대화 '{dialogueId}'");
            Enter(_dialogue.Start);
        }

        /// <summary>선택지가 없는 노드에서 다음으로 넘긴다.</summary>
        public void Continue()
        {
            if (Current == null) return;
            if (_visibleChoices.Count > 0) throw new InvalidOperationException("선택지가 있는 노드에서는 Choose를 불러야 합니다");
            Enter(Current.Next);
        }

        public void Choose(int index)
        {
            if (Current == null) return;
            var choice = _visibleChoices[index];
            foreach (var action in choice.Actions) _session.Execute(DialogueCommands.Parse(action));
            Enter(choice.Next);
        }

        /// <summary>
        /// 남은 대사를 건너뛴다(닫기 키). 건너뛰는 노드의 액션(퀘스트·보상 등)은 정상 진행과 똑같이 실행되고,
        /// 선택지가 있는 노드를 만나면 그 자리에서 멈춘다(선택은 건너뛰지 않는다). 대화가 끝났으면 true.
        /// </summary>
        public bool SkipToChoiceOrEnd()
        {
            for (var guard = 0; Current != null && _visibleChoices.Count == 0; guard++)
            {
                if (guard > 1000) throw new InvalidOperationException($"대화 '{_dialogue.Id}'가 끝나지 않습니다(순환 의심)");
                Enter(Current.Next);
            }
            return Current == null;
        }

        public void Stop()
        {
            if (Current == null) return;
            Current = null;
            _visibleChoices.Clear();
            Ended?.Invoke();
        }

        void Enter(string nodeId)
        {
            for (var hop = 0; hop < MaxBranchHops; hop++)
            {
                if (string.IsNullOrEmpty(nodeId) || !_dialogue.Nodes.TryGetValue(nodeId, out var node))
                {
                    Stop();
                    return;
                }

                foreach (var action in node.Actions) _session.Execute(DialogueCommands.Parse(action));

                if (node.IsBranchOnly)
                {
                    nodeId = FirstMatchingBranch(node);
                    continue;
                }

                Current = node;
                _visibleChoices.Clear();
                foreach (var choice in node.Choices)
                    if (DialogueCommands.Evaluate(choice.Condition, _session.Evaluate))
                        _visibleChoices.Add(choice);
                NodeEntered?.Invoke(node);
                return;
            }
            throw new InvalidOperationException($"대화 '{_dialogue.Id}'의 분기가 {MaxBranchHops}번 넘게 이어집니다(순환 의심)");
        }

        string FirstMatchingBranch(DialogueNodeDef node)
        {
            foreach (var branch in node.Branches)
                if (DialogueCommands.Evaluate(branch.Condition, _session.Evaluate))
                    return branch.Next;
            return null;
        }
    }
}
