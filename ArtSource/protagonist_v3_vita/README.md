# 주인공 시안 3 — 늘씬한 애니풍

- 베이스 모델: VRoid 샘플 "Vita" (CC0, VRoid Project). 출처: github.com/madjin/vrm-samples `vroid/beta/Vita.vrm` (메타 licenseName=CC0)
- 외형: tools/vita_look.py(3D 위치 기반 재채색: 분홍 단발, 검은 크롭탑·핫팬츠, 맨다리+흰 양말, 검은 손 장갑, 원본 하이힐 부츠는 삭제), tools/vita_coat.py(절차 생성 흰 두루마기: 넓은 소매, 트인 앞섶, 붉은 단) + 납작한 흰 어글리 슈즈(검은 밑창·옆 패널, 빨간 발목 띠)
- 동작: Mixamo(사용자 계정에서 export, 스킨 없음) → tools/mx_retarget.py로 VRM 리그에 리타깃. 루트 이동은 제자리(XY 제거), climb·wallrun은 높이도 고정. 골반 폭 차이로 다리가 겹쳐 보여 다리 사슬을 6° 벌림(ABD_DEG=6)
- 굽기: 정사영 고각 30°, 45° 단위 8방향, 192px 렌더 → 96px 칸 픽셀화(1px 외곽선). 렌더·픽셀화 스크립트는 범용으로 고쳐 `tools/sprite_bake/`로 옮김(사용법은 그 폴더 README). 여기 tools/에는 이 캐릭터 전용 스크립트(vita_look, vita_coat, export_model, 옛 리타깃 mx_retarget·vrm_render)만 남김
- 구운 시트는 `Assets/Gil/Art/Characters/player/`에 있음(dash는 rolling.png로 들어감). sheets/에는 형식 설명 sprites.json만 남김. 시트 형식: 행 S,SW,W,NW,N,NE,E,SE / 열 프레임, 10fps, 칸 96px, 발 기준점 = 칸 아래에서 24px(발끝 기준, 구현 쪽 확정값), 가로 48px. sprites.json 참고
- 임시 매핑: air=run_jump(달리며 점프), plunge=roll(낙하 후 구르기) — 맞는 동작이 따로 있으면 교체
- extra/: 게임 이름에 없는 동작(jump, dodge_back, slide, vault2, wallrun, wall_flip, flip_kick)
- 주의: Mixamo FBX 원본은 공개 저장소에 커밋하지 말 것(Adobe 약관). 구운 스프라이트는 게임에 사용 가능

## 2026-10-29 갱신: 믹사모 오토리깅 버전
- model/gil_protagonist_for_mixamo.fbx를 사용자 믹사모 계정에 올려 오토리깅 → 21개 동작을 스킨 포함으로 내보내 리타깃 없이 그대로 렌더(당시 tools/mxs_render.py, 지금은 `tools/sprite_bake/render8.py`). 다리 꺾임 등 리타깃 오차 제거
- idle은 Fighting Idle 전체(34프레임)로 바뀜(이전 5프레임)
- 이전 리타깃 버전(tools/mx_retarget.py) 결과물은 삭제함
