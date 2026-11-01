# Quarter View Action RPG Prototype — 이름 없는 심장

쿼터뷰 액션 RPG의 **클라이언트 프로그래밍 기술 샘플**입니다.
**「진료소 → 장터 → 첫 사냥」 플레이 데모**를 한 판 끝까지 플레이할 수 있게 만들었습니다.
화면은 **2D 쿼터뷰(2:1 아이소메트릭) + SD 캐릭터**, 전투는 **맨손 격투 + 파쿠르**, 성장은 **인공 심장 기반 의체 개조**입니다.

[![플레이 영상](https://img.youtube.com/vi/RwrskdyvbpM/hqdefault.jpg)](https://youtu.be/RwrskdyvbpM)

▶ 플레이 영상: https://youtu.be/RwrskdyvbpM

> 영상은 원본 빌드입니다. 이 저장소는 저작권·IP 문제로 일부 아트와 문구를 빼거나 바꾼 수정본이라, 타이틀 화면 등이 영상과 다를 수 있습니다.

> 비상업 포트폴리오. 아트가 없는 곳은 임시 그래픽입니다. 캐릭터는 시트와 `art.json`만 바꿔서, 배경은 지역 씬에서 직접 바꿉니다.
> 일부 에셋은 공개 저장소에서 뺐습니다. 아래 [빠진 에셋](#빠진-에셋)을 보세요.

## 실행

1. Unity **6000.6.7f1** (URP, Input System, 2D Animation/Tilemap)로 이 폴더를 엽니다.
2. 처음 열면 2D 렌더러·머티리얼·빌드 목록(Title → Systems → Player → UI → 지역 씬)이 자동으로 설정됩니다.
3. 메뉴 **Gil ▸ 타이틀부터 플레이 ▶** 를 누르거나, `Assets/Gil/Scenes/Systems.unity`를 열고 플레이합니다(타이틀 없이 바로 새 게임). 지역·Player·UI 씬을 열어 둔 채 플레이해도 Systems부터 뜹니다.

## 씬 구성

오브젝트는 전부 씬과 프리팹에 배치돼 있고, 같은 씬 안의 참조는 인스펙터에서 연결합니다. 씬 사이에는 인스펙터 참조를 저장할 수 없으므로, Player·UI 씬 루트의 `PlayerRig`·`UiRig`가 켜질 때 `Game`에 자신을 등록하고 `GameRoot`가 받아 갑니다.

흐름: `Title` → (새로 시작·이어하기) `Systems`(Single) → `GameRoot`가 `Player`·`UI`를 Additive로 열고 두 리그가 등록되면 → 지역 씬 Additive.

| 에셋 | 내용 |
|---|---|
| `Scenes/Title.unity` | 타이틀. `TitleRoot`(+`GameInput`), 타이틀 화면·페이드, EventSystem. 메뉴를 고르면 `Systems`를 엽니다 |
| `Scenes/Systems.unity` | 한 판 동안 상주하는 매니저 씬(UI 없음). `GameRoot`(+`TimeController`, `GameInput`, 데이터 `GameData`·`VfxLibrary` 연결), `World`(`AreaManager`), `EventSystem` |
| `Scenes/Player.unity` | `PlayerRig` 아래 `Player` 프리팹(조작 `Controller` + 외형 `PlayerView` + `Shadow`)과 `QuarterViewCamera` 프리팹(후처리 볼륨 포함) |
| `Scenes/UI.unity` | `UiRig`(+`UiRoot`) 아래 Canvas와 게임 중 화면 프리팹 인스턴스(Hud·Popups·Dialogue·Shop·Cyberware·Death·DemoEnd·Pause·Fade, 데이터 오류 표시 Fatal) |
| `Scenes/Areas/Area_clinic·market·alley.unity` | 지역 씬. 환경광, 바닥 타일맵, 건물·소품, NPC·행인과 그 외형, 출입구(`Portal`), 출입 지점(`Spawns`), 뒷골목의 웨이브(`Waves`)와 적 소환 지점 |
| `Prefabs/Player.prefab`, `Prefabs/QuarterViewCamera.prefab` | 플레이어(조작·외형·그림자)와 따라가는 카메라. Player 씬에 인스턴스로 배치 |
| `Prefabs/Enemy_*.prefab` | 웨이브가 소환하는 적(외형·그림자·공격 예고 범위 포함). 수치는 `enemies.json`에서 읽습니다 |
| `Prefabs/Fx/*.prefab`, `Prefabs/Fx/VfxLibrary.asset` | 이펙트(휘두르기 궤적, 타격 섬광, 바닥 고리, 먼지, 번개)와 적 투사체. `VfxLibrary`는 `GameRoot`에 연결합니다 |
| `Prefabs/UI/Screens/*.prefab` | UI 화면 단위 프리팹(Hud, Title, Dialogue, Shop, Cyberware, Pause, Death, DemoEnd, Popups, Fade). 화면 루트에 해당 View 컴포넌트가 붙어 있습니다 |
| `Prefabs/UI/*.prefab` | 실행 중에 늘어나는 UI 항목(메뉴 항목, 알림, 떠오르는 숫자, 적 체력바) |
| `Art/UI/TitleBackground.png` | 타이틀 배경 일러스트(공개 저장소에는 없음, [빠진 에셋](#빠진-에셋) 참고). `TitleShade.png`는 글자 쪽을 어둡게 하는 그라데이션 |
| `Fonts/` | 한글 폰트 Noto Sans KR(OFL)과 TextMeshPro 폰트 에셋(동적). TMP 기본 폰트로 지정돼 있습니다 |
| `Generated/GilGenerated.asset` | 임시 그래픽(코드로 그렸던 메시·스프라이트·타일·후처리 프로필). 실제 아트로 바꾸면 비어 갑니다 |

지역은 `AreaManager`가 Additive로 불러오고 내립니다. 맵은 지역 씬에 있으므로 **Gil ▸ 지역 씬 열기**로 해당 지역 씬을 열어 보고 고칩니다(**Gil ▸ 게임 씬 열기**는 Systems·Player·UI 세 씬을 함께 엽니다). 지역 씬을 연 채로 Play를 눌러도 Systems부터 시작하고 그 지역에서 새 게임을 시작합니다.

| 조작 | 키보드 | 패드 |
|---|---|---|
| 이동 | 방향키 | 왼쪽 스틱 |
| 달리기(누르는 동안, 스태미나 소모) | Left Shift | 왼쪽 스틱 누름 |
| 맨손 공격(잽 → 스트레이트 → 회전 차기) | Z | X(□) |
| 구르기(무적, 스태미나 30, 장애물 쪽으로 구르면 넘기·오르기) | X | B(○) |
| 점프 | C | A(✕) |
| 내려찍기 | 공중에서 공격 | |
| 과부하(인공 심장 게이지 광역기) | A | Y(△) |
| 회복약 | S | LB |
| 상호작용 | Space | RB |
| 메뉴 | Esc | Start |
| 메뉴 결정·대화 넘기기 | Z / Enter / Space | A(✕) |
| 상점·의체·대화 닫기(대화는 남은 대사 건너뛰기, 선택지에서는 무시) | X / Esc(상점·의체) | B(○) |

## 데모 흐름

1. **마고의 정비소** (하층 7구역 · 무허가 의체 시술소, 리퍼닥 분위기) — 인공 심장을 이식받고 깨어남. 허수아비로 조작 연습, 마고에게 첫 의체 시술.
2. **그림자 장터** (네온 한옥 장터) — 거래상 문탁에게 일거리를 받고, 상점에서 회복약 구매.
3. **7구역 뒷골목** — 경비 로봇·감시 드론·진압 로봇 3웨이브. 바리케이드를 넘고 컨테이너에 올라가 내려찍기.
4. 장터로 돌아가 보고 → 결산 화면.

## 이 샘플에서 보여 주고 싶은 것

- **규칙과 표현의 분리**: 전투 수치·콤보 버퍼·적 AI·퀘스트·대화·의체 장착 규칙·세이브가 엔진 무관 C#(`Gil.Core`)에 있고 `dotnet test`로 검증됩니다(55개).
- **2D 아이소메트릭 구조**: 판정은 바닥 평면(x, z) + 높이(y) 3D 논리, 그리기는 2:1 투영 스프라이트. 높이 차가 있는 파쿠르와 정확한 앞뒤 정렬을 함께 얻습니다.
- **파쿠르 전투**: 넘기(Vault)·오르기(Climb)·낙하·내려찍기(낙하 높이에 비례한 광역 피해). 높이 차이에 따라 근접 판정이 갈립니다.
- **의체 개조(사이버웨어)**: 심장 용량·신체 부하·슬롯 교체 규칙과 6종 기믹(신경 가속 슬로모션, 충격파 마무리, 연쇄 전격, 2단 대시, 착지 기절, 2차 심박).
- **아트 파이프라인**: 8방향 프레임 시트를 `art.json` 하나로 붙이는 런타임 애니메이터, 그리고 3D 모델을 쿼터뷰 각도에서 찍어 64px 픽셀 시트로 굽는 **8방향 베이킹 툴**.
- **데이터 주도**: 적·아이템·상점·퀘스트·대화·NPC·지역·웨이브·의체가 JSON. 검증기가 끊긴 참조·도달 불가 노드까지 잡습니다(메뉴 **Gil ▸ 게임 데이터 검증**).
- **GUI 없는 검증**: 명령줄로 설정·데이터 검증·EditMode/PlayMode 테스트(데모 전체를 자동으로 플레이)·베이크·빌드.
- **에디터 친화 구성**: 시스템·지역·UI가 씬과 프리팹에 배치돼 있어 인스펙터만 보고 고칠 수 있습니다. 지역은 씬 하나씩으로 나눠 여러 사람이 동시에 다른 지역을 작업할 수 있습니다.

자세한 내용은 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), 데이터는 [docs/DATA_GUIDE.md](docs/DATA_GUIDE.md), 아트 교체와 베이킹은 [docs/ART_PIPELINE.md](docs/ART_PIPELINE.md). 3D 모델을 도트 시트로 굽는 Blender 툴과 튜토리얼은 [tools/sprite_bake](tools/sprite_bake/README.md), 주인공 원본은 `ArtSource/protagonist_v3_vita/`.

## 명령줄 검증

```bash
# 코어 테스트 (Unity 없이)
cd tools/Gil.Core.Tests && dotnet test
```

```bat
set U=F:\Unity\6000.6.7f1\Editor\Unity.exe
%U% -batchmode -nographics -projectPath . -executeMethod Gil.Editor.GilBatch.Setup -logFile -
%U% -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Temp\edit.xml -logFile -
%U% -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Temp\play.xml -logFile -
%U% -batchmode -projectPath . -executeMethod Gil.Editor.GilBatch.BakeSelfTest -logFile -
%U% -batchmode -projectPath . -executeMethod Gil.Editor.GilBatch.BuildWindows -logFile -
```

PlayMode 테스트는 실제 `Title`·`Systems` 씬을 열어 데모를 한 판 자동으로 플레이하고, 장면마다 스크린샷을 `Logs/GilScreens`에 남깁니다(그래픽이 있어야 하므로 `-nographics` 없이).
에디터를 열어 둔 채로 명령줄 검증을 하려면 `Library`·`Temp`·`Logs`·`UserSettings`를 뺀 복사본(예: `GilSample_ci`)에서 실행합니다.

## 빠진 에셋

아래 파일은 재배포 권한이 없어 공개 저장소에 넣지 않았습니다. `.meta`는 남겨 두었으므로, **Unity로 프로젝트를 열기 전에** 같은 경로에 같은 이름으로 넣으면 씬·프리팹 참조가 그대로 살아납니다. 없어도 실행은 되며, 타이틀 화면이 배경 없이 뜹니다.

| 경로 | 내용 | 비고 |
|---|---|---|
| `Assets/Gil/Art/UI/TitleBackground.png` | 타이틀 배경 일러스트 | 외부 제공 아트. 아무 16:9 이미지로 대체 가능 |
| `Assets/Gil/Art/UI/Title/TitleBackgroundMotionMask.png` | 타이틀 배경의 흔들림 마스크(위 일러스트 전용) | 대체 시 회색 단색 이미지면 정지 배경 |
| `Assets/Gil/Art/UI/GameIcon.png` | 게임 아이콘 | 아무 정사각 이미지로 대체 가능 |
| `ArtSource/.../model/Vita_original.vrm` | 주인공 개조 전 원본 모델 | [VRoid 샘플 "Vita" (CC0)](https://github.com/madjin/vrm-samples/tree/master/vroid/beta) |

주인공 동작은 Mixamo(https://www.mixamo.com) 애니메이션을 입혀 구운 스프라이트만 포함합니다. Mixamo FBX 원본은 Adobe 약관상 재배포할 수 없어 넣지 않았고, 다시 구우려면 각자 Mixamo 계정에서 받습니다([tools/sprite_bake](tools/sprite_bake/README.md)).
유료·재배포 제한 에셋을 추가로 쓰려면 `Assets/ThirdParty/Licensed/`에 넣습니다(이 폴더는 `.gitignore`로 제외됨).

## 라이선스

코드는 이 저장소의 것입니다. 포함된 외부 에셋:

- [KayKit Adventurers](https://kaylousberg.itch.io/kaykit-adventurers) — Kay Lousberg, CC0 (`Assets/ThirdParty/KayKit/Adventurers/LICENSE.txt`)
- [Noto Sans KR](https://fonts.google.com/noto/specimen/Noto+Sans+KR) — SIL Open Font License (`Assets/Gil/Fonts/OFL.txt`)
- VRoid 샘플 "Vita" 기반 주인공 모델 — CC0

기준은 [docs/ART_PIPELINE.md](docs/ART_PIPELINE.md#라이선스).
