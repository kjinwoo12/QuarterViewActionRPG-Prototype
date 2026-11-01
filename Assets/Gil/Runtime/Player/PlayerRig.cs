using Gil.Runtime.CameraRig;
using UnityEngine;

namespace Gil.Runtime.Player
{
    /// <summary>
    /// 플레이어 씬(Assets/Gil/Scenes/Player.unity)의 뿌리. 플레이어와 따라가는 카메라를 인스펙터로 들고 있다가
    /// 켜질 때 <see cref="Game.PlayerRig"/>에 자신을 등록한다. 시스템 씬의 GameRoot가 등록을 기다렸다가 받아 간다.
    /// 씬이 다르면 인스펙터 참조를 저장할 수 없어서, 씬 사이는 이 등록으로만 잇는다(같은 씬 안은 인스펙터로 직접 연결).
    /// </summary>
    public sealed class PlayerRig : MonoBehaviour
    {
        [SerializeField, Tooltip("이 씬의 플레이어(Player 프리팹 안의 PlayerController)")] PlayerController _player;
        [SerializeField, Tooltip("플레이어를 따라가는 카메라(QuarterViewCamera 프리팹)")] QuarterViewCamera _camera;

        public PlayerController Player => _player;
        public QuarterViewCamera Camera => _camera;

        void OnEnable()
        {
            Game.PlayerRig = this;
        }

        void OnDisable()
        {
            if (Game.PlayerRig == this) Game.PlayerRig = null;
        }
    }
}
