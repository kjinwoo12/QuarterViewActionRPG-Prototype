# 데이터 수정 가이드 (기획용)

모든 게임 데이터는 `Assets/Gil/Resources/GilData/*.json`에 있습니다. `//` 주석과 마지막 쉼표를 허용합니다.
수정한 뒤 메뉴 **Gil ▸ 게임 데이터 검증**을 누르면 오류(빨강)와 경고(노랑)를 알려 줍니다. 오류가 있으면 게임이 시작되지 않고 화면에 이유가 표시됩니다.

| 파일 | 내용 |
|---|---|
| `player.json` | 체력·이동 속도·공격력, 콤보 단계별 판정(거리 `range`, 각도 `arcDegrees`, 선딜 `windup`, 판정 `active`, 후딜 `recovery`, 히트스톱), 회피, 인공 심장 게이지 |
| `enemies.json` | 적 종류(`melee`/`ranged`), 체력, 인식 거리, 공격 예고 시간(`windup`, 0.2초 미만이면 경고), 경직 저항(`poise`), 처치 보상(푼) |
| `items.json` | 아이템(가격, 회복량)과 상점 목록 |
| `quests.json` | 목표 `talk`/`kill`/`reach`, 보고할 NPC(`turnInNpc`, 비우면 자동 완료), 보상 |
| `npcs.json` | 이름·역할·대화 ID·색 |
| `dialogues.json` | 대화 노드 |
| `areas.json` | 지역 이름·부제, 웨이브 구성 |
| `cyberware.json` | 의체 슬롯, 의체(슬롯·부하·가격·능력치 `stats`·기믹 `gimmick`), 심장은 `capacity`로 부하 한도를 늘림 |

**화폐**: 조선 후기 상평통보를 따라 **1냥 = 10전 = 100푼**입니다. 가격·보상·`give_coins` 값은 모두 **푼 단위 정수**로 적고, 화면에는 `Money.Format`이 "3냥 5전 2푼"처럼 0인 단위를 빼고 보여 줍니다. 기준: 회복약 3전 5푼(35), 고급 회복약 1냥 5전(150), 의체 시술 6~13냥 5전(650~1350), 경비 로봇 1냥 2전 5푼(125), 첫 사냥 보상 12냥(1200), 마고 용돈 10냥(1000).

## 대화 쓰는 법

```jsonc
{ "id": "first_4", "speaker": "muntak", "text": "7구역 뒷골목 일, 해 볼 텐가?",
  "choices": [
    { "text": "하겠습니다", "next": "accept", "actions": ["accept_quest first_hunt"] },
    { "text": "물건부터 보죠", "next": "shop", "actions": ["open_shop muntak_shop"] },
    { "text": "(아직 준비가 안 됐다)", "next": "", "condition": "!quest_active first_hunt" }
  ] }
```

- `speaker`: NPC id 또는 `"player"`(화면에는 "나").
- `next`가 비어 있으면 대화 종료.
- 조건만 있는 분기 노드: `"branches": [{ "condition": "...", "next": "..." }, …]`, 위에서부터 처음 맞는 것으로 이동. 마지막 하나는 조건 없이 두세요(전부 조건이면 경고).

**조건**: `flag 이름`, `quest_none/quest_active/quest_ready/quest_done 퀘스트`, `has_item 아이템`. 앞에 `!`를 붙이면 부정.

**액션**: `accept_quest`, `turn_in_quest`, `open_shop`, `give_item 아이템 개수`, `set_flag`, `heal_full`, `travel 지역`, `open_cyberware`, `end_demo`.
잘못된 이름·인자 개수·없는 ID는 검증기가 줄 단위로 알려 줍니다.

## 의체(cyberware.json)

```jsonc
{ "id": "swallow_legs", "name": "도약 다리 「제비」", "slot": "legs", "price": 750, "load": 3,
  "stats": { "climbMaxHeight": 1.0, "vaultMaxHeight": 0.4, "moveSpeed": 0.08 },
  "gimmick": { "type": "double_rolling", "charges": 2 },
  "description": "…" }
```

- 슬롯마다 하나. 같은 슬롯에 새로 시술하면 이전 의체는 회수(환불 없음). 장착 부하 합은 심장의 `capacity` 이하.
- **stats** — 더하기: `capacity maxHp attack defense critMultiplier maxStamina vaultMaxHeight climbMaxHeight plungeRadius skillRadius`, 비율(0.1 = +10%): `critChance moveSpeed heartGain rollingCooldown rollingDistance staminaRegen plungeDamage`.
- **gimmick** — `neural_slowmo {duration}` 저스트 회피 슬로모션 · `shock_finisher {radius, multiplier}` 콤보 마무리 충격파 · `chain_shock {chance, radius, multiplier}` 연쇄 전격 · `double_rolling {charges}` 연속 구르기 · `kinetic_landing {stun}` 내려찍기 기절 · `second_wind {hpRatio}` 지역당 1회 부활.
- 모르는 키, 빠진 기믹 파라미터는 검증기가 잡습니다.
