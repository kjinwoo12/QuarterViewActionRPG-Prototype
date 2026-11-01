using System.Linq;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Quests;
using Gil.Runtime.Combat;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>지역 출입구(네온 게이트). 상호작용하면 다른 지역으로 이동한다. 잠긴 동안에는 이유를 보여 준다.</summary>
    public sealed class Portal : Interactable
    {
        [SerializeField] string _targetArea;
        [SerializeField] string _targetSpawn;
        [SerializeField] string _label;
        [SerializeField] PortalLock _lock;
        [SerializeField] string _lockArg;
        [SerializeField] string _lockMessage;
        [SerializeField] SpriteRenderer _arrow;

        /// <summary>
        /// 안내 화살표를 숨길 거리(지면 m). 게이트 바로 안쪽(도착 지점)에 서 있으면 화살표가 머리에 겹쳐 보이고,
        /// 이미 게이트 앞이라 안내도 필요 없다.
        /// </summary>
        [SerializeField, Tooltip("플레이어가 이 거리(m) 안에 있으면 안내 화살표를 숨긴다")] float _arrowHideDistance = 5f;

        public override string Prompt => _label;
        public string TargetArea => _targetArea;

        /// <summary>잠겨 있으면 그 이유, 열려 있으면 null.</summary>
        public string LockReason
        {
            get
            {
                var session = Game.Session;
                if (session == null) return null;
                switch (_lock)
                {
                    case PortalLock.RequireFlag:
                        return session.Flags.Contains(_lockArg) ? null : _lockMessage;
                    case PortalLock.RequireQuestStarted:
                        return session.Quests.StateOf(_lockArg) == QuestState.NotStarted ? _lockMessage : null;
                    case PortalLock.WavesCleared:
                        return Game.Areas != null && Game.Areas.Waves != null && Game.Areas.Waves.InProgress ? _lockMessage : null;
                    default:
                        return null;
                }
            }
        }

        void Update()
        {
            if (_arrow == null) return;
            var locked = !string.IsNullOrEmpty(LockReason);
            var player = Combatants.Player;
            var near = player != null && Vector3.Distance(player.transform.position, transform.position) < _arrowHideDistance;
            _arrow.enabled = !locked && !near;
            _arrow.transform.localPosition = Iso.ToScreen(transform.position + Vector3.up * (3.1f + Mathf.Sin(Time.time * 3f) * 0.12f));
        }

        public override void Interact()
        {
            var reason = LockReason;
            if (!string.IsNullOrEmpty(reason))
            {
                Game.Ui.Hud.Notice(reason);
                return;
            }
            Game.Areas.Travel(_targetArea, _targetSpawn);
        }
    }
}
