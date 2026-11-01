using System;
using System.Collections.Generic;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// 대화창. 진행 규칙은 Core의 <see cref="DialogueRunner"/>가 갖고, 여기서는 타자기 효과·선택지·화자 표시만 한다.
    /// 대화 중 요청된 상점 열기·데모 종료 등은 <see cref="AfterClose"/>로 대화가 끝난 뒤에 처리한다.
    /// </summary>
    public sealed class DialogueView : ModalView
    {
        [SerializeField, Tooltip("코드가 칠하는 색(Assets/Gil/Settings/UiTheme)")] UiTheme _theme;
        [SerializeField, Tooltip("한 글자씩 찍히는 속도(초당 글자 수)")] float _charsPerSecond = 48f;
        [SerializeField] TMP_Text _speaker;
        [SerializeField] Image _speakerTag;
        [SerializeField] TMP_Text _body;
        [SerializeField] TMP_Text _hint;
        [SerializeField] RectTransform _choiceBox;
        [SerializeField] VerticalMenu _choices;

        DialogueRunner _runner;

        string _fullText = "";
        float _typedChars;
        string _npcId;
        Action _onClosed;
        readonly List<Action> _afterClose = new List<Action>();

        bool Typing => _typedChars < _fullText.Length;

        /// <summary>NPC와 대화를 시작한다. onClosed는 대화가 끝난 직후 불린다.</summary>
        public void Play(string npcId, Action onClosed)
        {
            if (IsOpen) return;
            var session = Game.Session;
            if (!session.Db.Npcs.TryGetValue(npcId, out var npc))
            {
                Debug.LogError($"[Dialogue] 없는 NPC '{npcId}'");
                return;
            }

            _npcId = npcId;
            _onClosed = onClosed;
            _runner = new DialogueRunner(session);
            _runner.NodeEntered += OnNodeEntered;
            _runner.Ended += Close;
            Show();
            Game.Ui.Hud.SetVisible(false);
            session.OnTalkedTo(npcId);
            _runner.Start(npc.Dialogue);
        }

        /// <summary>대화가 열려 있으면 닫힌 뒤에, 아니면 즉시 실행한다.</summary>
        public void AfterClose(Action action)
        {
            if (IsOpen) _afterClose.Add(action);
            else action();
        }

        void OnNodeEntered(DialogueNodeDef node)
        {
            var db = Game.Session.Db;
            if (node.Speaker == "player")
            {
                _speaker.text = "나";
                _speaker.color = new Color(0.95f, 0.45f, 0.5f);
            }
            else if (db.Npcs.TryGetValue(node.Speaker, out var npc))
            {
                _speaker.text = npc.Name;
                _speaker.color = Visual.Colors.Hex(npc.Color);
            }
            else
            {
                _speaker.text = node.Speaker;
                _speaker.color = _theme.Accent;
            }
            _speakerTag.gameObject.SetActive(!string.IsNullOrEmpty(_speaker.text));

            _fullText = node.Text ?? "";
            _typedChars = 0;
            _body.text = "";
            _choices.Clear();
            _choiceBox.gameObject.SetActive(false);
            _hint.enabled = false;
        }

        void Update()
        {
            if (!IsOpen || _runner == null || !_runner.IsRunning) return;
            var input = Game.Input;

            // 닫기 키: 남은 대사를 건너뛰고 끝낸다. 건너뛴 노드의 액션(퀘스트·보상)은 정상 진행과 똑같이 실행되고,
            // 선택지가 있는 노드에서는 무시하며 건너뛰다 선택지를 만나면 거기서 멈춘다.
            if (InputReady && input.CancelPressed && _runner.VisibleChoices.Count == 0)
            {
                _runner.SkipToChoiceOrEnd();
                return;
            }

            if (Typing)
            {
                _typedChars = Mathf.Min(_fullText.Length, _typedChars + _charsPerSecond * Time.unscaledDeltaTime);
                _body.text = _fullText.Substring(0, (int)_typedChars);
                if (InputReady && input.SubmitPressed)
                {
                    // 첫 입력은 문장을 한 번에 펼치기만 한다.
                    _typedChars = _fullText.Length;
                    _body.text = _fullText;
                }
                if (!Typing) ShowChoicesOrHint();
                return;
            }

            if (_runner.VisibleChoices.Count > 0)
            {
                if (InputReady) _choices.HandleInput(input);
            }
            else if (InputReady && input.SubmitPressed)
            {
                _runner.Continue();
            }
        }

        /// <summary>테스트용: 입력 없이 한 단계 진행(타이핑 끝내기 → 선택지 고르기 또는 다음).</summary>
        internal void TestAdvance(int choice = 0)
        {
            if (!IsOpen || _runner == null || !_runner.IsRunning) return;
            if (Typing)
            {
                _typedChars = _fullText.Length;
                _body.text = _fullText;
                ShowChoicesOrHint();
                return;
            }
            if (_runner.VisibleChoices.Count > 0) _runner.Choose(Mathf.Clamp(choice, 0, _runner.VisibleChoices.Count - 1));
            else _runner.Continue();
        }

        void ShowChoicesOrHint()
        {
            var choices = _runner.VisibleChoices;
            if (choices.Count == 0)
            {
                _hint.enabled = true;
                return;
            }

            _choices.Clear();
            for (var i = 0; i < choices.Count; i++)
            {
                var index = i;
                _choices.Add($"{i + 1}.  {choices[i].Text}", () => _runner.Choose(index));
            }
            _choiceBox.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(380, 310), new Vector2(620, _choices.TotalHeight));
            _choiceBox.gameObject.SetActive(true);
        }

        void Close()
        {
            _runner.NodeEntered -= OnNodeEntered;
            _runner.Ended -= Close;
            _runner = null;
            _choices.Clear();
            Hide();
            Game.Ui.Hud.SetVisible(true);

            var closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();

            // 대화 중 쌓인 후속 처리(상점 열기, 데모 종료 등)
            var pending = _afterClose.ToArray();
            _afterClose.Clear();
            foreach (var action in pending) action();
        }
    }
}
