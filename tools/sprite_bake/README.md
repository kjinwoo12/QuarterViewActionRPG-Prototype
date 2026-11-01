# sprite_bake — 3D 모델을 쿼터뷰 8방향 도트 시트로

3D 캐릭터 모델 하나와 Mixamo 동작으로 게임에 바로 넣을 수 있는 도트 스프라이트 시트(`{동작}.png` + `art.json`)를 만듭니다. 주인공 시트(`Assets/Gil/Art/Characters/player/`)가 이 방법으로 만들어졌습니다. 그 작업 기록은 `ArtSource/protagonist_v3_vita/`에 있습니다.

```
모델(.glb/.fbx/.blend) ──mixamo_prep.py──▶ 업로드용 FBX ──Mixamo──▶ 동작별 FBX(스킨 포함)
   ──render8.py──▶ 8방향 PNG 프레임 ──pixelize.py──▶ 도트 시트 + art.json ──▶ Assets/Gil/Art/Characters/{id}/
```

| 파일 | 실행 | 하는 일 |
|---|---|---|
| `mixamo_prep.py` | Blender | 뼈대 제거, 텍스처 내장, (선택) 얇은 옷에 두께 → Mixamo 업로드용 FBX |
| `render8.py` | Blender | 동작 FBX를 정사영 고각 30°, 45° 간격 8방향으로 렌더. 제자리 고정 |
| `shading.py` | (render8이 import) | 평평한 애니풍 셰이딩, 뒷면 어둡게, 테두리 조명 |
| `pixelize.py` | Python | 축소, 알파 컷, 색 단계, 1px 외곽선 → 시트, `sprites.json`, `preview.gif`, `art.json` |

## 준비

- **Blender 4.2 이상**(5.x에서 확인함). 아래 예시는 `blender`가 PATH에 있다고 가정합니다. 아니면 전체 경로로 씁니다. 예: `"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"`
- **Python 3.10 이상** + `pip install pillow numpy` (pixelize용)
- **Mixamo 계정**(Adobe, 무료)
- 명령은 모두 저장소 루트(`F:\git\GilSample`)에서 실행합니다. 원본은 `ArtSource/{캐릭터}/`에 두고(Assets 밖이라 Unity가 임포트하지 않음), 중간 결과는 `Temp/`에 둡니다.

## 튜토리얼: 캐릭터 `hero` 만들기

### 1. 모델 준비

정면이 보이는 T포즈나 A포즈 모델이 필요합니다. glTF(.glb), FBX, OBJ, .blend를 받습니다. VRoid(.vrm)는 파일 확장자만 `.glb`로 바꾸면 그대로 읽힙니다.

색을 바꾸거나 옷을 더하는 개조는 Blender에서 먼저 하고 `.blend`나 `.glb`로 저장해 둡니다. 스크립트로 개조한 예는 `ArtSource/protagonist_v3_vita/tools/vita_look.py`와 `vita_coat.py`에 있습니다.

라이선스를 확인하세요. CC0나 상업 이용 가능 모델만 씁니다. 원본(.vrm, 유료 팩)은 저장소에 커밋하지 않습니다(`docs/ART_PIPELINE.md#라이선스`).

### 2. Mixamo 업로드용 FBX 만들기

```bat
blender -b -P tools/sprite_bake/mixamo_prep.py -- --in ArtSource/hero/hero.glb --out ArtSource/hero/hero_for_mixamo.fbx
```

- 옷이 한 겹짜리라 뒤에서 보면 구멍이 나면 두께를 줍니다: `--solidify Coat_=0.008` (이름이 `Coat_`로 시작하는 메시에 8mm. 여러 번 쓸 수 있음)
- 렌더에 필요 없는 오브젝트는 지웁니다: `--drop Icosphere`
- 텍스처는 출력 폴더의 `textures/`에도 PNG로 풀립니다.
- 모델이 뒤를 보고 있으면(VRoid/VRM이 그렇습니다) `--turn 180`을 붙입니다. 정면이 Blender 앞쪽(-Y)을 봐야 Mixamo에서 바로 정면으로 보입니다.

### 3. Mixamo에서 리깅하고 동작 받기

1. https://www.mixamo.com 에서 **Upload Character**를 누르고 `hero_for_mixamo.fbx`를 올립니다.
2. 턱, 손목, 팔꿈치, 무릎, 사타구니에 마커를 찍습니다. 소매가 넓으면 손목 마커는 소매 끝이 아니라 실제 손목에 찍습니다. Skeleton LOD는 **Standard**(손가락 포함)를 고릅니다.
3. 동작을 고르고 **Download**를 누릅니다. 설정은 Format **FBX Binary**, Skin **With Skin**, Frames per Second **30**입니다. **In Place**가 있는 동작은 체크합니다(없어도 render8이 제자리로 고정함).
4. 파일 이름을 게임 동작 이름으로 바꿔 `ArtSource/hero/anims/`에 모읍니다. 예: `idle.fbx`, `run.fbx`, `jab.fbx`, `rolling.fbx`

게임이 부르는 동작 이름은 `docs/ART_PIPELINE.md`의 art.json 절에 있습니다. idle은 필수입니다.

> Mixamo에서 받은 FBX는 Adobe 약관상 재배포할 수 없습니다. 공개 저장소에 커밋하지 말고 로컬에만 두세요. 구운 시트는 게임에 써도 됩니다.

### 4. 8방향 렌더

```bat
blender -b -P tools/sprite_bake/render8.py -- --out Temp/hero_frames --clips-dir ArtSource/hero/anims --res 192 --step 3
```

- `--step 3`: 30fps 원본에서 3프레임마다 한 장씩 찍습니다(= 10fps).
- `--res 192`: 렌더 크기입니다. 다음 단계 칸 크기(96)의 2배로 찍어야 축소할 때 깔끔합니다.
- 한 동작만 다시 찍을 때: `--clip jab=ArtSource/hero/anims/jab.fbx`. 같은 동작의 이전 프레임은 지우고 새로 씁니다.
- 결과: `Temp/hero_frames/{동작}_{방향}_{번호}.png`, `meta.json`

자주 맞추는 값:

| 증상 | 옵션 |
|---|---|
| 캐릭터가 칸에 비해 너무 크거나 작음 | `--ortho` (화면 폭 m, 기본 3.2. 크게 하면 작게 찍힘) |
| 머리나 발이 잘림 | `--target-z` (카메라 겨눔 높이, 기본 0.75) |
| S줄에 뒷모습이 찍힘 | `--face 180` |
| 오르기처럼 위로 올라가는 동작이 칸을 벗어남 | `--lockz climb,wallrun` (높이 이동도 없앰) |
| 음영이 너무 진하거나 약함 | `--emission` (기본 0.6, 높을수록 평평) |
| 테두리 조명 끄기·바꾸기 | `--rim 0`, `--rim-color 1,0.6,0.6` |
| 두께를 준 옷의 뒷면이 검게 나옴 | `--flat-prefix Coat_` |
| 느림 | `--samples 4`, `--gpu` |

전체 옵션은 `blender -b -P tools/sprite_bake/render8.py -- --help`로 봅니다.

### 5. 도트 시트로 변환

```bat
python tools/sprite_bake/pixelize.py Temp/hero_frames Temp/hero_sheets --cell 96
```

`Temp/hero_sheets/preview.gif`로 8방향을 한 번에 확인합니다. 마음에 안 들면 4단계 옵션을 바꿔 다시 찍습니다.

- `--cell 96`: 칸 크기(px). 주인공은 96, 작은 적은 64 정도를 씁니다.
- `--outline none`: 외곽선 없음. `--outline 2a1a1a`: 외곽선 색 바꾸기
- `--color-step 1`: 색 단계를 줄이지 않음(기본 8 = 채널당 32단계)
- `--alpha 110`: 반투명 가장자리를 얼마나 잘라낼지 정함
- `--rename dash=rolling`: 시트 이름을 게임 동작 이름에 맞춤

### 6. 게임에 넣기

마음에 들면 게임 폴더로 바로 출력하고 `art.json`도 만듭니다.

```bat
python tools/sprite_bake/pixelize.py Temp/hero_frames Assets/Gil/Art/Characters/hero --cell 96 --art-json --fps 10 --no-gif
```

- `art.json`이 없으면 기본값으로 새로 만듭니다(`--ppu 40 --pivot-y 0.25 --height 1.25`, 반복 재생은 `--loop idle,run,...`).
- 이미 있으면 이번에 만든 동작의 `clips`만 넣거나 갱신하고, 손으로 맞춘 다른 값(pivot, brightness 등)은 그대로 둡니다.
- 발 위치(`pivotY`)는 칸 아래에서 발끝까지의 비율입니다. 기본 렌더 값이면 96px 칸에서 24px(0.25)입니다.

그다음은 Unity 쪽 작업입니다. `GilArtImporter`가 PNG를 픽셀 아트 설정으로 가져오고, 외형 에셋(`{id}.asset`)을 만들어 `SpriteSheetAnimator`에 연결합니다. 자세한 것은 `docs/ART_PIPELINE.md`를 봅니다.

## 시트 형식

- 줄(위 → 아래) = 방향 `S, SW, W, NW, N, NE, E, SE`. S는 카메라 쪽(화면 아래)입니다.
- 칸(왼 → 오) = 프레임, 칸 크기는 `--cell`
- `sprites.json`: 시트마다 `{frames, cell, rows}`

## 문제 해결

- **`bpy`가 없다고 나옴**: 일반 `python`이 아니라 `blender -b -P ...`로 실행해야 합니다.
- **캐릭터가 투명하거나 흰색으로 나옴**: Mixamo 다운로드 때 Skin이 With Skin이었는지 확인합니다. 텍스처가 FBX에 내장돼 있어야 합니다(mixamo_prep이 해 줌).
- **`방향마다 프레임 수가 다릅니다`**: 렌더가 중간에 끊긴 것입니다. 그 동작만 `--clip`으로 다시 찍습니다.
