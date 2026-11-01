using UnityEngine;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// 화면을 가리고 조작을 막는 창(대화·상점·메뉴)의 공통 바탕.
    /// 창을 연 그 프레임의 입력은 무시한다 — 상호작용 키(Space)가 곧바로 "다음/선택"으로 처리되는 것을 막기 위함.
    /// </summary>
    public abstract class ModalView : MonoBehaviour
    {
        int _openedFrame = -1;

        /// <summary>마지막으로 창이 닫힌 프레임. 닫는 키 입력이 같은 프레임에 게임 조작으로 새는 것을 막는 데 쓴다.</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LastClosedFrame = -1;

        [SerializeField] RectTransform _root;

        protected RectTransform Root => _root;
        public bool IsOpen { get; private set; }

        protected bool InputReady => IsOpen && Time.frameCount > _openedFrame;

        protected void Show()
        {
            IsOpen = true;
            _openedFrame = Time.frameCount;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
        }

        protected void Hide()
        {
            IsOpen = false;
            LastClosedFrame = Time.frameCount;
            Root.gameObject.SetActive(false);
        }
    }
}
