# 아트 파이프라인

캐릭터 시트는 폴더에 파일을 넣고, 외형이 배치된 곳(플레이어는 `Prefabs/Player.prefab`의 `PlayerView`, 적은 `Prefabs/Enemy_{id}`의 `View`, NPC는 지역 씬)에서 `SpriteSheetAnimator`를 쓰게 바꿉니다. 배경(바닥·건물·소품)은 지역 씬(`Scenes/Areas/Area_{id}.unity`)에 배치돼 있으니 씬에서 직접 바꿉니다.

임시 SD 외형을 시트로 바꾸는 방법: 외형 오브젝트 아래의 `root`(뼈대)와 `ChibiAnimator`를 지우고, 자식으로 SpriteRenderer `Body`(머티리얼 `GilSpriteLit`)를 만든 뒤 `SpriteSheetAnimator`를 붙여 Art Id(폴더 이름), Follow(따라갈 캐릭터 본체), Body를 연결합니다. 인공 심장 발광점이 있으면 Heart에 연결합니다.

| 무엇 | 넣는 곳 | 형식 |
|---|---|---|
| 캐릭터·적·NPC | `Assets/Gil/Resources/GilArt/Characters/{id}/` | 동작별 PNG 시트 + `art.json` |
| 바닥 타일 | 지역 씬의 `View/Floor/Tilemap`에 타일 팔레트로 칠하기 | 아이소메트릭 타일(가로 = 1칸, Isometric Grid 1 × 0.5) |
| 건물·소품 | 지역 씬의 `View` 아래에 배치(충돌체는 같은 위치의 지면 좌표 오브젝트) | 쿼터뷰 스프라이트·프리팹 |

`{id}`는 `player`, 적 id(`guard_bot`, `drone`, `riot_bot`), NPC id(`mago`, `muntak`)입니다. `GilArtImporter`가 이 폴더의 PNG를 자동으로 픽셀 아트 설정(Point 필터, 비압축, 밉맵 없음)으로 가져옵니다.

## art.json

```jsonc
{
  "frameWidth": 64, "frameHeight": 64,
  "pixelsPerUnit": 38,          // 화면 크기
  "pivotX": 0.5, "pivotY": 0.12, // 발 위치(칸 비율)
  "height": 1.9,                 // 머리 위 체력바·말풍선 높이(m)
  "directions": ["S","SW","W","NW","N","NE","E","SE"], // 줄 순서(위 → 아래). 빠진 방향은 좌우 반전으로 채움
  "layout": "rows",              // rows: 시트 한 장에 줄 = 방향 / files: 방향마다 {sheet}_{dir}.png
  "heartX": 34, "heartY": 36,    // 인공 심장 발광 위치(선택)
  "clips": {
    "idle": { "sheet": "idle", "fps": 8, "loop": true },   // idle은 필수
    "run":  { "sheet": "run",  "fps": 12, "loop": true },
    "jab":  { "sheet": "jab",  "fps": 14, "loop": false }
  }
}
```

게임이 부르는 동작: `idle run air jab straight spin_kick rolling vault climb plunge land overdrive hurt die` (적: `windup smash fire`). 없는 동작은 비슷한 동작으로 대체되고, 동작 길이는 게임 판정 시간에 맞춰 늘이거나 줄입니다.

## 8방향 베이킹 툴

3D 모델 + 애니메이션을 게임과 같은 쿼터뷰 각도(30°/45°)에서 8방향으로 찍고, 64px 픽셀 아트로 줄여(알파 컷, 색 단계, 1px 외곽선) 위 형식으로 바로 출력합니다.

1. **Create ▸ Gil ▸ Sprite Bake Profile** — 모델, 동작 이름 ↔ 클립, 크기·색 단계·외곽선·조명.
2. **Gil ▸ 스프라이트 베이크 ▸ 모든 프로필** 또는 `-executeMethod Gil.Editor.GilBatch.BakeSprites`.
3. 결과가 `Resources/GilArt/Characters/{id}`에 생기고 다음 플레이부터 적용됩니다.

KayKit(CC0) 팩을 `Assets/ThirdParty/KayKit`에 풀면 **Gil ▸ 스프라이트 베이크 ▸ KayKit 주인공 프로필 만들기**(또는 `GilBatch.BakeKayKitPlayer -gilModel Rogue`)가 클립 이름으로 동작을 자동 매칭해 주인공 프로필을 만들고 굽습니다.
외형을 바꿀 때(VRoid 모델 + Mixamo 동작, Humanoid 리타깃)는 프로필의 Model/Clip만 갈아 끼우면 됩니다.

`GilBatch.BakeSelfTest`는 외부 에셋 없이 기본 도형 인형으로 베이크 → art.json → 런타임 로드까지 검증합니다.

## 3D → 도트 (Blender 툴, Unity 밖)

현재 주인공 시트는 Unity 베이킹 툴이 아니라 Blender 스크립트로 구웠습니다. 범용 스크립트와 단계별 튜토리얼은 [`tools/sprite_bake/`](../tools/sprite_bake/README.md)에 있습니다(모델 → Mixamo 업로드용 FBX → Mixamo 동작 → 8방향 렌더 → 도트 시트 + art.json).

### 주인공 작업 기록

주인공 원본과 전용 스크립트는 `ArtSource/protagonist_v3_vita/`(Assets 밖이라 Unity가 임포트하지 않음)에 있고, 자세한 값은 그 폴더의 `README.md`를 봅니다.

1. **모델**: VRoid 샘플 "Vita"(CC0)를 개조(전용 `tools/vita_look.py` 재채색, `tools/vita_coat.py` 두루마기) → `model/`(.blend, .glb, `gil_protagonist_for_mixamo.fbx`, `gil_protagonist_rigged.fbx`). 내보내기 `tools/export_model.py`(범용판은 `tools/sprite_bake/mixamo_prep.py`).
2. **동작**: `gil_protagonist_for_mixamo.fbx`를 Mixamo 오토리깅 → 동작을 스킨 포함으로 내보냄(Mixamo FBX는 저장소에 두지 않음).
3. **렌더**: `tools/sprite_bake/render8.py --res 192 --step 3` — 정사영, 고각 30°, 45° 단위 8방향.
4. **픽셀화**: `tools/sprite_bake/pixelize.py --cell 96` — 96px 칸으로 축소, 1px 외곽선.
5. **출력**: 게임 동작 이름별 PNG(행 = S,SW,W,NW,N,NE,E,SE, 10fps, 발 기준 칸 아래 24px, 형식은 `sheets/sprites.json`). 구운 시트는 `Assets/Gil/Art/Characters/player/`(`art.json`)에 들어가 있고, 게임에 아직 없는 동작은 `extra/`에 있습니다.

`ArtSource/protagonist_v3_vita/tools/mx_retarget.py`·`vrm_render.py`는 오토리깅 전 쓰던 리타깃 방식입니다(결과물은 삭제).

## 라이선스

| 에셋 | 커밋 |
|---|---|
| CC0 (KayKit 등)와 거기서 구운 시트 | O |
| Mixamo FBX, VRoid 원본(.vrm/.vroid) | X — 원본 재배포 불가. 구운 시트만 |
| 유료·재배포 제한 팩(Mekai, Engvee 등) | X — 각자 받아서 `Assets/ThirdParty/Licensed`에. 공개 저장소에는 올리지 않음 |
