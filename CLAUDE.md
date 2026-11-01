# CLAUDE.md

이 프로젝트는 아래 공통 지침을 따른다. (MyClaudeDocs에서 복사한 사본, 원본: https://github.com/kjinwoo12/MyClaudeDocs)

@docs/claude/general.md
@docs/claude/unity.md

## 프로젝트 정보

- Unity 버전: 6000.6.7f1 (URP 2D, Input System)
- Unity 실행 파일: `F:\Unity\6000.6.7f1\Editor\Unity.exe`
- 일상 작업은 열린 에디터에 붙는 Unity MCP로, 원본 프로젝트에서 직접 한다. batchmode는 빌드·CI용.
- 씬: `Assets/Gil/Scenes/` 아래 Title → Systems(매니저) + Player(플레이어·카메라) + UI(게임 중 UI) → 지역 씬 `Areas/Area_{id}`

## 명령줄 진입점

- 코어 규칙 테스트(Unity 없이): `cd tools/Gil.Core.Tests && dotnet test`
- `-executeMethod` 목록은 `Assets/Gil/Editor/GilBatch.cs` 상단 주석을 본다(Setup, ValidateData, BuildWindows, BakeSprites, BakeSelfTest 등).
- EditMode/PlayMode 테스트와 빌드 명령은 `README.md`의 「명령줄 검증」을 본다.

## 프로젝트별 규칙

- 엔진 무관 규칙은 `Gil.Core`(UnityEngine 참조 없음)에 두고 `dotnet test`로 검증한다. 화면·입력·연출은 `Gil.Runtime`.
- 게임 데이터(적·아이템·퀘스트·대화 등)의 형식은 `docs/DATA_GUIDE.md`를 따른다.
- 씬/프리팹 직접 배치 구조다(2026-10-29 부트스트랩 구조에서 전환 완료). `Systems`가 `Player`·`UI`를 Additive로 열고(씬 사이 참조는 `PlayerRig`·`UiRig` 등록 방식), 지역 씬 `Scenes/Areas/Area_{id}`를 Additive로 로드한다. 플레이어·카메라·적·UI 항목은 `Prefabs/`. 구성은 `README.md`의 「씬 구성」과 `docs/ARCHITECTURE.md`를 본다.
