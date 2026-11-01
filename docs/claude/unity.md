# Unity 작업 지침

`general.md`와 함께 적용한다. 대원칙은 같다: 디자이너·기획자·프로그래머가 Unity 에디터를 열었을 때 씬과 인스펙터만 보고 이해하고 고칠 수 있어야 한다.

## 씬 구성: 에디터에 직접 배치한다

- 게임 오브젝트는 씬과 프리팹에 직접 배치한다. 런타임 코드가 씬을 조립하는 부트스트랩 구조(빈 씬 + `GameRoot`가 전부 생성)를 만들지 않는다.
- 런타임 생성(`Instantiate`)은 동적 스폰에만 쓴다. 예: 웨이브 적, 발사체, 이펙트, 데미지 숫자. 이때도 생성할 대상은 프리팹으로 만들어 인스펙터에서 참조한다.
- UI도 Canvas와 프리팹으로 배치한다. 코드로 uGUI 계층을 만들지 않는다.
- 컴포넌트 간 참조는 인스펙터에서 연결한다(`[SerializeField]`). `Find`, `GetComponent` 연쇄, 이름 문자열 검색으로 찾지 않는다.

## 조정값: 인스펙터에 노출한다

- 수치·색·시간·거리 같은 조정값은 `[SerializeField] private` 필드나 `ScriptableObject`로 노출한다. 코드 상수로 박지 않는다.
- 여러 오브젝트가 공유하는 값, 밸런스 데이터는 `ScriptableObject` 에셋으로 뺀다.
- 필드에 `[Tooltip]`, `[Range]`, `[Header]`를 붙여 인스펙터만 보고도 의미를 알 수 있게 한다.

## 씬·프리팹·에셋 수정은 Unity API로

- `.unity`, `.prefab`, `.asset`, `.mat` 등의 YAML을 텍스트로 직접 편집하지 않는다. GUID·fileID 참조가 깨지기 쉽고 사람이 리뷰할 수도 없다.
- 씬·프리팹을 만들거나 고쳐야 하면 Unity MCP나 에디터 스크립트로 Unity API를 거친다.
  - 씬: `EditorSceneManager.OpenScene` → 수정 → `EditorSceneManager.SaveScene`
  - 프리팹: `PrefabUtility.LoadPrefabContents` → 수정 → `PrefabUtility.SaveAsPrefabAsset` → `UnloadPrefabContents`
  - 직렬화 필드 값·참조: `SerializedObject` / `SerializedProperty` → `ApplyModifiedPropertiesWithoutUndo`
  - 에셋 생성: `AssetDatabase.CreateAsset`, 마지막에 `AssetDatabase.SaveAssets`
- 이런 스크립트는 한 번 쓰고 버리는 배치 작업이다. 결과물(씬·프리팹)이 정본이고, 스크립트를 매번 실행해 씬을 재생성하는 구조로 만들지 않는다.

## 테스트·자동화는 사람이 쓰는 구조 위에

- PlayMode 테스트는 사람이 배치한 실제 씬을 로드해서 검증한다. 테스트를 위해 씬을 코드로 조립하지 않는다.
- 테스트 전용 훅이 필요하면 런타임 구조를 바꾸지 말고 테스트 어셈블리 쪽에 둔다.
- 엔진 무관 규칙(전투 공식, 데이터 검증 등)은 순수 C# 어셈블리로 분리해 `dotnet test`로도 돌릴 수 있게 하는 것은 권장한다. 단, 그 때문에 에디터 작업 흐름이 불편해지면 안 된다.

## 검증 주기

- 수정할 때마다는 컴파일 에러·콘솔 에러·빈 참조(Missing/None)만 MCP로 빠르게 확인한다.
- Play 실행이나 테스트로 실제 동작을 확인하는 것은 피처 하나가 끝났을 때 한 번만 한다.
- 사용자가 직접 확인하겠다고 하면 그 동작 확인도 생략한다.

## Unity 조작: 일상 작업은 MCP, 빌드·CI는 CLI

- 씬·프리팹 편집, 에셋 처리, 반복 수정 같은 일상 작업은 **열린 에디터에 붙는 Unity MCP**로 한다. Unity를 매번 새로 띄우지 않는다.
- 원본 프로젝트에서 직접 작업한다. 검증용 복사본을 따로 두고 동기화하지 않는다.
- 사용자에게 맡길지는 `general.md`의 「사용자에게 맡길 일: 하나의 기준」으로 판단한다. Unity에서는 이렇게 적용된다.
  - Claude가 직접: 에디터 저장·종료·실행(명령줄로 `Unity.exe -projectPath` 실행 포함), 새로고침, 리컴파일, 재시작.
  - 재시작은 최후 수단이다. 패키지 설치·스크립트 변경·에셋 임포트는 MCP로 새로고침(`AssetDatabase.Refresh`)·리컴파일을 요청해 반영하고, 그래도 안 될 때만 저장 후 재시작한다.
  - 사용자에게 부탁: MCP 서버 시작처럼 에디터 GUI 버튼으로만 가능한 것, 패키지·라이선스 로그인 등. MCP에 연결이 안 되면 기다리지 말고 즉시 어느 창의 어떤 버튼인지 알린다.
- Unity 에디터 화면을 클릭·키 입력으로 조작하지 않는다.
- batchmode CLI는 빌드·CI처럼 깨끗한 환경이 필요한 일에만 쓴다.
  ```bat
  Unity.exe -batchmode -nographics -projectPath <경로> -executeMethod <Class.Method> -logFile -
  Unity.exe -batchmode -nographics -projectPath <경로> -runTests -testPlatform EditMode -testResults <xml> -logFile -
  Unity.exe -batchmode -projectPath <경로> -runTests -testPlatform PlayMode -testResults <xml> -logFile -
  ```
  (`-quit`는 `-runTests`와 같이 쓰지 않는다. 그래픽이 필요한 PlayMode/스크린샷은 `-nographics`를 뺀다.)
  - CLI를 쓸 때는 여러 단계를 한 번의 실행(하나의 `-executeMethod`)에 묶어 Unity 재실행 횟수를 최소화한다.
  - 같은 프로젝트를 에디터가 열고 있으면 batchmode는 프로젝트 잠금으로 실패한다. 이때는 Claude가 에디터를 저장·종료하고 실행한 뒤 다시 연다.
- 로그(`-logFile`)나 MCP 콘솔에서 `error CS`, `Exception`, 테스트 결과의 `failed` 수를 확인하고 보고한다.

## 버전 관리

- `.meta` 파일은 반드시 원본 파일과 함께 커밋한다. 파일을 옮기거나 지울 때도 `.meta`를 같이 처리한다.
- `.gitignore`: `Library/`, `Temp/`, `Obj/`, `Build(s)/`, `Logs/`, `UserSettings/`, `MemoryCaptures/`, IDE 생성 파일(`*.csproj`, `*.sln`, `.vs/` 등). `*.meta`는 절대 무시하지 않는다.
- `.gitattributes`: Unity YAML(`*.unity`, `*.prefab`, `*.asset`, `*.mat`, `*.anim`, `*.controller`)은 `text eol=lf merge=unityyamlmerge`, `*.meta`는 `text eol=lf`, 이미지·모델·오디오·폰트는 `binary`. 대용량 바이너리가 많으면 Git LFS를 검토한다.
- 씬 머지 충돌을 줄이려면 씬을 기능 단위 프리팹으로 나누고, 여러 사람이 같은 씬을 동시에 고치지 않게 한다. 머지 충돌을 피하려고 씬을 코드로 생성하는 방식은 쓰지 않는다.
