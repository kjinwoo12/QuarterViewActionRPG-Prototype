using System.Linq;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Quests;
using Gil.Runtime.Combat;
using Gil.Runtime.Visual;
using TMPro;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>대화 가능한 NPC. 머리 위에 퀘스트 표시(! / ?)를 띄운다.</summary>
    public sealed class NpcController : Interactable
    {
        [SerializeField] string _npcId;
        [SerializeField, Tooltip("머리 위 퀘스트 표시(! / ?). 월드 TextMeshPro")] TMP_Text _markerText;
        [SerializeField, Tooltip("머리 위 표시 높이(화면 유닛)")] float _markerHeight = 1.45f;
        [SerializeField, Tooltip("받을 퀘스트가 있을 때(!) 색")] Color _questColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField, Tooltip("보고할 퀘스트가 있을 때(?) 색")] Color _turnInColor = new Color(0.4f, 1f, 0.7f);
        NpcDef _def;
        Quaternion _homeRotation;

        public string NpcId => _npcId;
        public NpcDef Def => _def;
        public override string Prompt => _def != null ? $"{_def.Name}에게 말 걸기" : "";
        public override bool CanInteract => _def != null;

        void Start()
        {
            var session = Game.Session;
            if (session == null || !session.Db.Npcs.TryGetValue(_npcId, out var def))
            {
                Debug.LogError($"[Gil] npcs.json에 없는 NPC: {_npcId} ({gameObject.scene.name})");
                enabled = false;
                return;
            }
            _def = def;
            _homeRotation = transform.rotation;
        }

        public override void Interact()
        {
            Game.Ui.Dialogue.Play(_def.Id, () => transform.rotation = _homeRotation);
        }

        void Update()
        {
            if (_def == null) return;
            var player = Combatants.Player;
            if (player != null && Game.Ui != null && Game.Ui.Dialogue.IsOpen)
            {
                var to = player.transform.position - transform.position;
                to.y = 0;
                if (to.sqrMagnitude > 0.01f && to.magnitude < 4f) transform.rotation = Quaternion.LookRotation(to);
            }

            var marker = MarkerFor(_def);
            _markerText.text = marker;
            _markerText.color = marker == "?" ? _turnInColor : _questColor;
            _markerText.transform.localPosition = new Vector3(0, _markerHeight + Mathf.Sin(Time.time * 3f) * 0.05f, 0);
        }

        /// <summary>
        /// 데이터만 보고 표시를 정한다. 보고할 퀘스트가 있으면 "?",
        /// 이 NPC의 대화에 아직 받지 않은 퀘스트의 accept_quest가 있으면 "!".
        /// </summary>
        static string MarkerFor(NpcDef npc)
        {
            var session = Game.Session;
            if (session == null) return "";
            if (session.Quests.Tracked.Any(q => q.State == QuestState.ReadyToTurnIn && q.Def.TurnInNpc == npc.Id)) return "?";

            var dialogue = session.Db.Dialogues[npc.Dialogue];
            foreach (var node in dialogue.Nodes.Values)
            {
                foreach (var action in node.Actions.Concat(node.Choices.SelectMany(c => c.Actions)))
                {
                    var c = DialogueCommands.Parse(action);
                    if (c.Verb == "accept_quest" && session.Quests.StateOf(c.Arg(0)) == QuestState.NotStarted) return "!";
                }
            }
            return "";
        }
    }
}
