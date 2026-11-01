using Gil.Core;
using Gil.Runtime.CameraRig;
using Gil.Runtime.Input;
using Gil.Runtime.Player;
using Gil.Runtime.UI;
using Gil.Runtime.World;
using UnityEngine;

namespace Gil.Runtime
{
    /// <summary>
    /// 게임 전역 서비스 접근점. 시스템 씬(Systems)의 <see cref="GameRoot"/>가 인스펙터로 연결된 오브젝트와
    /// 플레이어·UI 씬이 등록한 리그(<see cref="PlayerRig"/>, <see cref="UiRig"/>)에서 꺼낸 오브젝트를 넣고, 씬이 내려가면 비운다.
    ///
    /// 샘플 규모에서는 DI 컨테이너 대신 정적 접근점 하나로 의존을 모았다. 대상 오브젝트는 전부 씬에 배치돼 있고, 여기는 찾아가는 길만 둔다.
    /// 규칙 로직(Core)은 이 클래스를 전혀 모르고, MonoBehaviour 층만 사용한다 — 테스트 가능한 부분과 엔진 접착부를 나누는 경계.
    /// </summary>
    public static class Game
    {
        public static GameSession Session;
        public static GameInput Input;
        public static QuarterViewCamera Camera;
        public static UiRoot Ui;
        public static AreaManager Areas;

        /// <summary>플레이어 씬(Player)이 켜질 때 스스로 등록한다. 씬 사이에는 인스펙터 참조를 저장할 수 없어서 이 등록으로 잇는다.</summary>
        public static PlayerRig PlayerRig;
        /// <summary>UI 씬(UI)이 켜질 때 스스로 등록한다.</summary>
        public static UiRig UiRig;

        /// <summary>타이틀의 「이어하기」로 게임(시스템 씬)을 열었는지. GameRoot가 시작할 때 한 번 읽고 지운다.</summary>
        public static bool ContinueRequested;

        /// <summary>대화·상점·메뉴·화면 전환 중에는 조작을 막는다.</summary>
        public static bool GameplayLocked => Ui == null || Ui.BlocksGameplay || Areas == null || Areas.IsTransitioning;

        public static void Clear()
        {
            Session = null;
            Input = null;
            Camera = null;
            Ui = null;
            Areas = null;
        }

        // 도메인 리로드 없이 플레이를 다시 시작해도 이전 판의 참조가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            PlayerRig = null;
            UiRig = null;
            ContinueRequested = false;
        }
    }
}
