using UnityEngine;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// UI 씬(Assets/Gil/Scenes/UI.unity)의 뿌리. 같은 오브젝트의 <see cref="UiRoot"/>(화면 참조 모음)를 인스펙터로 들고 있다가
    /// 켜질 때 <see cref="Game.UiRig"/>에 자신을 등록한다. 시스템 씬의 GameRoot가 등록을 기다렸다가 받아 간다.
    /// 씬이 다르면 인스펙터 참조를 저장할 수 없어서, 씬 사이는 이 등록으로만 잇는다(같은 씬 안은 인스펙터로 직접 연결).
    /// </summary>
    public sealed class UiRig : MonoBehaviour
    {
        [SerializeField, Tooltip("이 씬의 UI 뿌리(화면 참조 모음)")] UiRoot _ui;

        public UiRoot Ui => _ui;

        void OnEnable()
        {
            Game.UiRig = this;
        }

        void OnDisable()
        {
            if (Game.UiRig == this) Game.UiRig = null;
        }
    }
}
