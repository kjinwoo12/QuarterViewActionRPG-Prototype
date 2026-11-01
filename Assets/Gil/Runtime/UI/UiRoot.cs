using TMPro;
using UnityEngine;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// UI 씬(Scenes/UI) UI의 뿌리(타이틀은 별도 씬). 다른 씬에서는 같은 오브젝트의 <see cref="UiRig"/> 등록으로 찾아온다. 화면(View)마다 프리팹(Assets/Gil/Prefabs/UI/Screens)으로 Canvas 아래 배치돼 있고, 여기서 참조만 모은다.
    /// 게임 조작을 막아야 하는지(<see cref="BlocksGameplay"/>)도 여기서 판단한다.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        [SerializeField] TMP_Text _fatal;

        [SerializeField] HudView _hud;
        [SerializeField] PopupView _popups;
        [SerializeField] DialogueView _dialogue;
        [SerializeField] ShopView _shop;
        [SerializeField] CyberwareView _cyberware;
        [SerializeField] PauseView _pause;
        [SerializeField] DemoEndView _demoEnd;
        [SerializeField] DeathView _death;
        [SerializeField] FadeView _fade;

        public HudView Hud => _hud;
        public PopupView Popups => _popups;
        public DialogueView Dialogue => _dialogue;
        public ShopView Shop => _shop;
        public CyberwareView Cyberware => _cyberware;
        public PauseView Pause => _pause;
        public DemoEndView DemoEnd => _demoEnd;
        public DeathView Death => _death;
        public FadeView Fade => _fade;

        public bool BlocksGameplay =>
            Dialogue.IsOpen || Shop.IsOpen || Cyberware.IsOpen || Pause.IsOpen || DemoEnd.IsOpen || Death.IsOpen
            || ModalView.LastClosedFrame == Time.frameCount;

        /// <summary>데이터 오류 등으로 게임을 시작할 수 없을 때 화면에 이유를 띄운다.</summary>
        public void ShowFatal(string message)
        {
            _fatal.text = "<b><color=#ff8080>게임 데이터를 불러오지 못했습니다</color></b>\n\n" + message;
            _fatal.transform.parent.gameObject.SetActive(true);
            _fatal.transform.parent.SetAsLastSibling();
        }
    }
}
