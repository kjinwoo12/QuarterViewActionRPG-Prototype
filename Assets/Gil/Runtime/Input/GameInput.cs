using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Gil.Runtime.Input
{
    /// <summary>
    /// 게임 입력(Input System). 키·패드 배치는 입력 액션 에셋(Assets/Gil/Settings/Gil.inputactions)에 있고, 여기는 그 액션을 인스펙터로 받아
    /// 게임 코드가 쓰기 쉬운 값(이번 프레임에 눌렸는지 등)으로 바꿔 준다. 키 배치를 바꾸려면 에셋을 열어 고친다.
    /// 시스템 씬(Systems)의 GameRoot, 타이틀 씬의 TitleRoot 오브젝트에 붙어 있다.
    /// </summary>
    public sealed class GameInput : MonoBehaviour
    {
        [Header("플레이(Gameplay 맵)")]
        [SerializeField] InputActionReference _move;
        [SerializeField] InputActionReference _attack;
        [SerializeField, FormerlySerializedAs("_dodge")] InputActionReference _rolling;
        [SerializeField, Tooltip("누르고 있는 동안 달리기(Gameplay/Sprint)")] InputActionReference _sprint;
        [SerializeField] InputActionReference _jump;
        [SerializeField] InputActionReference _skill;
        [SerializeField] InputActionReference _interact;
        [SerializeField] InputActionReference _potion;
        [SerializeField] InputActionReference _pause;
        [Header("메뉴(Menu 맵)")]
        [SerializeField] InputActionReference _navigate;
        [SerializeField] InputActionReference _submit;
        [SerializeField, Tooltip("상점·의체·대화창 닫기(Menu/Cancel)")] InputActionReference _cancel;

        InputActionReference[] All => new[] { _move, _attack, _rolling, _sprint, _jump, _skill, _interact, _potion, _pause, _navigate, _submit, _cancel };

        void OnEnable()
        {
            foreach (var a in All)
                if (a != null) a.action.Enable();
        }

        void OnDisable()
        {
            foreach (var a in All)
                if (a != null && a.action != null) a.action.Disable();
        }

        public Vector2 Move => Vector2.ClampMagnitude(_move.action.ReadValue<Vector2>(), 1f);
        public bool AttackPressed => _attack.action.WasPressedThisFrame();
        public bool RollingPressed => _rolling.action.WasPressedThisFrame();
        public bool SprintHeld => _sprint != null && _sprint.action.IsPressed();
        public bool JumpPressed => _jump != null && _jump.action.WasPressedThisFrame();
        public bool SkillPressed => _skill.action.WasPressedThisFrame();
        public bool InteractPressed => _interact.action.WasPressedThisFrame();
        public bool PotionPressed => _potion.action.WasPressedThisFrame();
        public bool PausePressed => _pause.action.WasPressedThisFrame();
        public bool CancelPressed => _cancel != null && _cancel.action.WasPressedThisFrame();
        public bool SubmitPressed => _submit.action.WasPressedThisFrame() || AttackPressed;

        /// <summary>메뉴 이동용: 이번 프레임에 눌린 세로 방향(-1 위, +1 아래, 0 없음).</summary>
        public int NavigateVerticalPressed
        {
            get
            {
                if (!_navigate.action.WasPressedThisFrame()) return 0;
                var v = _navigate.action.ReadValue<Vector2>().y;
                return v > 0.5f ? -1 : v < -0.5f ? 1 : 0;
            }
        }
    }
}
