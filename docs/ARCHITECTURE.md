# 아키텍처

## 어셈블리 구성

```
Gil.Core      (엔진 참조 없음)  규칙·데이터·상태. 단위 테스트 대상.
   ▲
Gil.Runtime   (UnityEngine, InputSystem, uGUI, URP 2D)  입력·이동·연출·UI. Core 이벤트를 구독해 보여 준다.
   ▲
Gil.Editor    2D 렌더러/머티리얼/빌드 목록 자동 설정, 데이터 검증, 명령줄 진입점(GilBatch), 8방향 스프라이트 베이커.
Gil.Tests.EditMode → Gil.Core          (같은 소스를 tools/Gil.Core.Tests에서 dotnet으로도 돌린다)
Gil.Tests.PlayMode → Gil.Runtime       (Title·Systems 씬을 열어 데모 한 판 자동 플레이하는 스모크 테스트)
```

## Core 주요 객체

| 영역 | 클래스 | 역할 |
|---|---|---|
| 데이터 | `GameDatabase`, `DataValidator`, `Json` | JSON 로드(주석·후행 쉼표 허용, 줄 번호 포함 오류), 참조 무결성 검증 |
| 전투 | `DamageCalculator`, `Health`, `HeartGauge`, `RollingTimer`, `Stamina` | 피해 공식, 치명타, 인공 심장 게이지, 구르기 무적 구간·충전 횟수, 달리기·구르기 스태미나 |
| 콤보 | `ComboTracker` | 선딜/판정/후딜, 입력 버퍼, 콤보 유예 |
| 적 AI | `EnemyBrain` | Idle→Chase→Windup→Attack→Recover, 경직(Stagger)·기절(Stun) |
| 파쿠르 | `ParkourRules` | 높이·거리로 넘기/오르기 선택, 낙하 높이별 내려찍기 배율, 높이 차 근접 판정 |
| 의체 | `CyberwareLoadout`, `PlayerStats`, `StatKeys`, `Gimmicks` | 심장 용량·부하·슬롯 교체, 능력치 합산, 기믹 목록 |
| 진행 | `QuestLog`, `DialogueRunner`, `DialogueCommands` | 퀘스트 목표, 조건 분기 대화, 대화 액션 |
| 세션 | `GameSession`, `SaveData` | 한 판의 상태와 버전이 붙은 세이브 |

## 2D 아이소메트릭: 논리와 그림의 분리

```
논리(보이지 않음)                         그림(View)
  바닥 평면 x,z + 높이 y                    2:1 투영 스프라이트
  CharacterController, BoxCollider    →    IsoView가 매 프레임 위치 투영
  판정·파쿠르·발사체 전부 여기서            Isometric Tilemap, SpriteRenderer, Light2D
```

- 투영 `Iso.ToScreen`: `x' = (x − z)·0.5`, `y' = (x + z)·0.25 + y·0.62`, `z' = −1.22·y`. Unity Isometric Grid(셀 1 × 0.5)와 칸이 정확히 맞습니다.
- 앞뒤 정렬은 Unity 기본 기능을 씁니다: `Settings/Gil2DRenderer`의 Transparency Sort Mode = Custom Axis (0, 1, 1). 같은 Order in Layer끼리는 `y' + z'`가 작을수록 앞에 그립니다.
  - 정적 소품(벽·가구·간판)은 기준점(오브젝트 위치)을 바닥에 닿는 점에 둡니다. 옮기면 정렬이 따라옵니다. 여러 조각으로 된 소품은 SortingGroup으로 한 덩어리가 되고, 자식의 Order in Layer는 그 안에서의 앞뒤만 뜻합니다. 공중에 매달린 소품(등불 등)은 기준점을 옮기는 대신 위치 Z에 "발까지의 화면 거리(음수)"를 넣습니다.
  - 높이 올라선 캐릭터·이펙트는 `z'`가 앞으로 당겨져 발판보다 앞에 그려집니다. 같은 자리 앞뒤 보정은 `Iso.SortNudge`(IsoView의 SortBias, 이펙트의 `_sortNudge`)로 합니다.
  - Order in Layer는 층 구분에만 씁니다: 바닥 −32000, 바닥 데칼 −31000, 방 뒷벽 −1, 월드 0, 머리 위 표시 30000.
  - 머티리얼: 스프라이트(SpriteRenderer)는 `GilSpriteLit`/`GilSpriteUnlit`, 메시(MeshRenderer: 벽·소품·이펙트)는 `GilMeshLit`/`GilMeshUnlit`을 씁니다. 같은 머티리얼을 쓰면 연달아 그려질 때 Unity가 스프라이트와 메시를 한 번에 묶어 그리면서 메시가 스프라이트의 텍스처로 칠해져 사라집니다.
- 장점: 높이(컨테이너 위, 낙하)를 진짜 3D 물리로 처리하면서도 화면은 순수 2D라 아트 제작이 쉽습니다. 정렬 숫자를 손으로 관리하지 않습니다.

## 씬 구성과 Runtime 흐름

오브젝트는 씬·프리팹에 배치하고, 같은 씬 안의 참조는 인스펙터(`[SerializeField]`)로 연결합니다. 씬 사이에는 인스펙터 참조를 저장할 수 없으므로 Player·UI 씬 루트의 리그(`PlayerRig`, `UiRig`)가 `OnEnable`에서 `Game.PlayerRig`/`Game.UiRig`에 자신을 등록하고, `GameRoot`는 두 씬을 Additive로 연 뒤 등록된 리그에서 플레이어·카메라·UI를 받아 `Game`과 `AreaManager.Bind`에 넘깁니다. 런타임 생성은 인스펙터로 연결된 프리팹 복제(웨이브 적, 투사체, 이펙트, 늘어나는 UI 항목)뿐입니다. 캐릭터 외형도 씬·프리팹에 배치돼 있습니다.

```
Scenes/Title.unity (타이틀)
 └─ TitleRoot         (+ GameInput) 타이틀 화면 → 새로 시작·이어하기 → Systems를 Single로 연다

Scenes/Systems.unity (매니저 씬, 한 판 동안 계속 열려 있음. UI 없음)
 ├─ GameRoot          (+ GameData·VfxLibrary 연결) 데이터 로드·검증 → Player·UI 씬 Additive 로드 → 리그 등록 대기 → 세션 시작/종료
 │   ├ TimeController timeScale 단일 소유(일시정지·히트스톱·슬로모션 합성)
 │   └ GameInput      입력 액션(InputActionReference) → 이번 프레임 입력 값
 ├─ World             AreaManager: 지역 씬 Additive 로드/언로드, 플레이어 배치, 웨이브 시작, 자동 저장 신호
 └─ EventSystem       uGUI 입력(InputSystemUIInputModule)

Scenes/Player.unity (Additive)
 └─ PlayerRig         PlayerRig: 플레이어·카메라를 들고 Game.PlayerRig에 등록
     ├─ Player        [Prefabs/Player] Controller(PlayerController: 입력 → Core 규칙 → 이동·파쿠르·판정·기믹)
     │                + PlayerView(스프라이트시트 + 인공 심장 조명) + Shadow. 외형은 회전·크기를 따로 다루므로 Controller의 형제
     └─ QuarterViewCamera [Prefabs/QuarterViewCamera] 2D 정사영, 걷기 영역 경계 제한, 흔들림 (+ PostFx Volume: Bloom·Tonemapping)

Scenes/UI.unity (Additive)
 └─ UiRig             UiRig + UiRoot(각 View 참조). UiRig가 Game.UiRig에 등록
     └─ Canvas        (Canvas Scaler) 화면 프리팹 인스턴스: Popups·Hud·Dialogue·Shop·Cyberware·Death·DemoEnd·Pause·Fade, Fatal

Scenes/Areas/Area_{id}.unity (한 번에 하나만 로드)
 └─ Area_{id}         AreaScene: 걷기 영역·배경색·출입 지점(Spawns/*) 인스펙터 값
     ├─ Ambient       환경광(Global Light2D). 지역 씬만 열어도 게임과 같은 밝기로 보인다
     ├─ View          바닥 Tilemap, 건물·소품 메시, 네온 간판, 점광원, NPC·행인 외형(SD 뼈대 + ChibiAnimator) (화면 좌표)
     ├─ 충돌체·ArenaObstacle·ParkourSurface, TrainingDummy, NpcController, Extra, Portal (지면 좌표)
     └─ Waves         WaveDirector: 소환 지점(자식 Transform)과 적 프리팹 목록 (뒷골목만)

Prefabs/Enemy_{id}    EnemyController + View(외형·눈 조명) + Shadow + Telegraph(꺼진 채). 소환되면 화면 쪽 자식을 지역의 View로 옮긴다
Prefabs/Fx/*          SlashFx·ImpactFx·RingFx·DustFx·LightningFx(VfxLibrary로 묶음), Projectile(드론 프리팹에 연결)
```

- 지역 전환: 페이드 → 이전 지역 씬 언로드 → 새 지역 씬 Additive 로드(활성 씬으로 지정해 이펙트도 그 씬에 생기게) → `Spawns`의 지점으로 플레이어 이동 → 페이드 인.
- `AreaScene.Awake`가 `Iso.ViewRoot`/`LogicRoot`와 걷기 영역을 잡고, 소품의 `ArenaObstacle`은 켜질 때 적 이동 장애물로 등록·꺼질 때 해제합니다. 씬을 내리면 저절로 비워집니다.
- 출입구 잠금은 인스펙터의 `PortalLock`(플래그 필요/퀘스트 수락 필요/웨이브 정리 필요) + 인자 + 안내 문구로 정합니다.
- NPC는 씬에 `npcId`만 두고 이름·대화는 `npcs.json`에서 찾습니다. 적 프리팹도 `enemyId`만 두고 수치는 `enemies.json`에서 읽습니다.
- UI는 uGUI(Canvas + Canvas Scaler) + TextMeshPro로 만들고, 화면마다 `Prefabs/UI/Screens/*.prefab`으로 관리합니다(UI Toolkit은 에디터 도구 전용). 화면 루트에 그 화면의 View가 붙어 있고 `UiRoot`가 참조합니다.
- 타이틀은 별도 씬(`Title`)이고 메뉴 항목 3개(`VerticalMenu._placed`)도 미리 배치돼 있어, Title 씬을 열면 Play 없이 시작 화면이 보입니다. 배경은 화면을 꽉 채우는 일러스트(`Art/UI/TitleBackground.png`, 비율 유지·넘치는 부분 잘림)이고 글자·메뉴는 왼쪽 열에 있습니다. 타이틀이 떠 있는 동안에는 지역 씬을 불러오지 않고 세션도 없습니다(새로 시작·이어하기를 누르면 그때 만든다).
- HUD 숫자 칸의 글자는 자리 표시이며 실행하면 실제 값으로 바뀝니다. HUD는 UI 씬을 열면 바로 보입니다.
- 글자는 `Fonts/NotoSansKR-Regular SDF`(동적 TMP 폰트 에셋, 굵게는 Bold 에셋)로 그립니다. 간판·NPC 머리 위의 월드 글자(TextMesh)만 OS 한글 폰트를 씁니다.
- 처음 씬을 만들 때(예전 구조에서 옮길 때) 코드로 그렸던 임시 메시·스프라이트·타일은 `Generated/GilGenerated.asset`의 하위 에셋으로 저장했습니다. 씬은 이 에셋을 참조합니다.

- 피격 판정은 `Combatants` 레지스트리의 거리·각도·높이 검사로 처리합니다. 판정 모양을 데이터(range/arc)로 바로 조절할 수 있습니다.
- 파쿠르 이동 중에는 CharacterController를 끄고 정해진 궤적으로 움직이며(무적), 착지 지점은 장애물 반대편 면을 레이캐스트로 찾고 캡슐 검사로 확인합니다.

## 캐릭터 그림 교체

`ICharacterAnimator` 하나로 두 구현을 갈아 끼웁니다.

| 구현 | 언제 |
|---|---|
| `SpriteSheetAnimator` | 확정 아트. `Resources/GilArt/Characters/{id}/art.json`의 8방향 프레임 시트(현재 플레이어) |
| `ChibiAnimator` | 임시 캐릭터. 씬·프리팹에 배치된 SD 뼈대(`ChibiRig`)를 절차적으로 움직인다 |

외형 컴포넌트는 각 캐릭터의 외형 오브젝트(IsoView)에 붙어 있고, 컨트롤러는 그것을 `GetComponent<ICharacterAnimator>()`로 받아 `PlayAction("jab", 0.3)`처럼 동작 이름만 부르고, 시트에 없는 동작은 대체 동작(예: straight → jab → attack)으로 넘어갑니다. 형식과 베이킹은 [ART_PIPELINE.md](ART_PIPELINE.md).
