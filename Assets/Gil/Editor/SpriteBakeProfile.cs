using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gil.Editor
{
    /// <summary>
    /// 8방향 스프라이트 베이킹 설정. 3D 휴머노이드(또는 일반 리그) 모델 + 애니메이션 클립을 게임의 쿼터뷰 각도에서 찍어
    /// 시트(PNG) + art.json으로 굽고, <see cref="CharacterArtBuilder"/>가 시트를 칸마다 잘라 캐릭터 외형 에셋(CharacterArt)을 만든다.
    /// Assets 아무 곳에서 Create ▸ Gil ▸ Sprite Bake Profile로 만든다.
    /// </summary>
    [CreateAssetMenu(menuName = "Gil/Sprite Bake Profile", fileName = "SpriteBakeProfile")]
    public sealed class SpriteBakeProfile : ScriptableObject
    {
        [Serializable]
        public sealed class ClipEntry
        {
            [Tooltip("게임 쪽 동작 이름: idle, run, jab, straight, spin_kick, rolling, vault, climb, plunge, land, hurt, die …")]
            public string Name = "idle";
            public AnimationClip Clip;
            public float Fps = 12f;
            public bool Loop = true;
            [Tooltip("0이면 클립 길이 × fps")]
            public int FrameCount;
        }

        [Tooltip("Assets/Gil/Art/Characters/{id}로 출력. 예: player, guard_bot, npc_mago")]
        public string CharacterId = "player";
        public GameObject Model;
        public List<ClipEntry> Clips = new List<ClipEntry>();
        [Tooltip("찍을 때 숨길 메시 오브젝트 이름(무기·망토 등)")]
        public List<string> HiddenParts = new List<string>();
        [Tooltip("모든 재질의 기본 텍스처를 이것으로 바꿔 찍는다(재채색 외형). 비우면 모델 그대로")]
        public Texture2D TextureOverride;

        [Header("화면")]
        [Tooltip("최종 한 칸 크기(픽셀). 확정 에셋과 맞춰 64")]
        public int FrameSize = 64;
        [Tooltip("이 배율로 크게 찍은 뒤 줄여서 픽셀화한다(가장자리 계단 정리)")]
        public int Supersample = 4;
        [Tooltip("한 칸에 담을 세로 범위(m). 모델 키 + 여유")]
        public float CaptureHeight = 2.4f;
        [Tooltip("발 위치(칸 아래에서부터 비율)")]
        public float PivotY = 0.12f;
        [Tooltip("게임 화면에서의 크기 배율(1 = 실제 비율). SD 느낌을 내려면 1.1~1.25")]
        public float WorldScale = 1.15f;

        [Header("픽셀화")]
        [Range(0f, 1f)] public float AlphaCutoff = 0.45f;
        [Tooltip("색 단계 수(0 = 그대로). 4~6이면 셀 셰이딩 같은 느낌")]
        public int ColorSteps = 6;
        public bool Outline = true;
        public Color OutlineColor = new Color(0.08f, 0.06f, 0.1f, 1f);

        [Header("조명")]
        public Color KeyLight = new Color(1f, 0.96f, 0.9f);
        public Color Ambient = new Color(0.55f, 0.55f, 0.65f);
        [Tooltip("게임 안 기본 색 배율(art.json brightness). 밝은 옷이 2D 조명에 하얗게 날아가면 0.8 전후로 낮춘다")]
        [Range(0f, 2f)] public float Brightness = 1f;
        [Tooltip("가슴의 인공 심장 위치(칸 픽셀 기준, 오른쪽을 볼 때). 0이면 없음")]
        public Vector2 HeartPixel;
    }
}
