using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 확정 외형(8방향 프레임 스프라이트시트) 한 캐릭터 분량. 시트 PNG는 임포트 설정에서 칸마다 스프라이트로 잘려 있고,
    /// 이 에셋은 동작(클립) → 방향 → 프레임 스프라이트를 묶는다. 스프라이트 베이커(Gil ▸ 스프라이트 베이크)가 굽고 나서 다시 만든다.
    /// 위치: Assets/Gil/Art/Characters/{id}/{id}.asset. <see cref="SpriteSheetAnimator"/> 인스펙터에 연결한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Gil/Character Art", fileName = "CharacterArt")]
    public sealed class CharacterArt : ScriptableObject
    {
        [Serializable]
        public sealed class Direction
        {
            [Tooltip("E, NE, N, NW, W, SW, S, SE 중 하나")] public string Name;
            public Sprite[] Frames;
        }

        [Serializable]
        public sealed class Clip
        {
            [Tooltip("동작 이름(idle, run, jab …)")] public string Name;
            [Tooltip("반복 동작의 초당 프레임. 한 번 재생하는 동작은 요청된 시간에 맞춰 늘이거나 줄인다")] public float Fps = 10f;
            public bool Loop;
            public List<Direction> Directions = new List<Direction>();
        }

        [Serializable]
        public sealed class Fallback
        {
            [Tooltip("게임이 요청하는 동작")] public string Action;
            [Tooltip("그 동작이 없을 때 앞에서부터 찾아 쓸 대체 동작")] public string[] Alternatives;
        }

        /// <summary>방향 이름 → 화면 각도(도). 화면 오른쪽 = 0, 위 = 90.</summary>
        public static readonly IReadOnlyDictionary<string, float> DirectionAngles = new Dictionary<string, float>
        {
            ["E"] = 0, ["NE"] = 45, ["N"] = 90, ["NW"] = 135, ["W"] = 180, ["SW"] = 225, ["S"] = 270, ["SE"] = 315,
        };

        [SerializeField, Tooltip("시트 한 칸 크기(px). 베이크 결과에서 채워진다")] Vector2Int _frameSize;
        [SerializeField, Range(0f, 2f), Tooltip("기본 색 배율. 밝은 옷이 2D 조명에 하얗게 날아가면 0.8 전후로 낮춘다")]
        float _brightness = 1f;
        [SerializeField, Tooltip("인공 심장 발광점을 쓰는 캐릭터인지")] bool _hasHeart;
        [SerializeField, Tooltip("오른쪽을 볼 때 심장 위치(발 기준, 유닛). 왼쪽을 보면 좌우 반전한다")] Vector2 _heart;
        [SerializeField] List<Clip> _clips = new List<Clip>();
        [SerializeField, Tooltip("에셋에 없는 동작을 비슷한 동작으로 대체하는 표")] List<Fallback> _fallbacks = DefaultFallbacks();

        public Vector2Int FrameSize => _frameSize;
        public float Brightness => _brightness;
        public Vector2? Heart => _hasHeart ? _heart : (Vector2?)null;
        public IReadOnlyList<Clip> Clips => _clips;

        /// <summary>베이커·빌더가 다시 만들 때 쓴다(대체 표는 사람이 고친 값을 유지한다).</summary>
        public void SetBakeResult(Vector2Int frameSize, Vector2? heart, List<Clip> clips)
        {
            _frameSize = frameSize;
            _hasHeart = heart.HasValue;
            _heart = heart ?? Vector2.zero;
            _clips = clips;
        }

        public void SetBrightness(float brightness) => _brightness = Mathf.Clamp(brightness, 0f, 2f);

        public Clip Get(string name)
        {
            foreach (var c in _clips)
                if (c.Name == name) return c;
            return null;
        }

        /// <summary>동작을 찾고, 없으면 대체 표를 따라 찾는다.</summary>
        public Clip Find(string name)
        {
            if (name == null) return null;
            var clip = Get(name);
            if (clip != null) return clip;
            foreach (var f in _fallbacks)
            {
                if (f.Action != name || f.Alternatives == null) continue;
                foreach (var alt in f.Alternatives)
                    if ((clip = Get(alt)) != null) return clip;
            }
            return null;
        }

        /// <summary>화면 방향 벡터에 가장 가까운, 이 클립이 가진 방향의 프레임. mirrored면 좌우 반전해서 써야 한다.</summary>
        public Sprite[] FramesFor(Clip clip, Vector2 screenDirection, out bool mirrored)
        {
            mirrored = false;
            var angle = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg;
            var best = Nearest(clip, angle);
            // 오른쪽(또는 왼쪽)만 그린 에셋: 반대쪽은 좌우 반전한 각도로 찾는다.
            var alt = Nearest(clip, 180f - angle);
            if (best.direction == null || (alt.direction != null && alt.error + 1f < best.error))
            {
                mirrored = true;
                best = alt;
            }
            return best.direction?.Frames;
        }

        static (Direction direction, float error) Nearest(Clip clip, float angle)
        {
            Direction best = null;
            var bestError = float.MaxValue;
            foreach (var d in clip.Directions)
            {
                if (d.Frames == null || d.Frames.Length == 0 || !DirectionAngles.TryGetValue(d.Name, out var a)) continue;
                var error = Mathf.Abs(Mathf.DeltaAngle(angle, a));
                if (error < bestError)
                {
                    best = d;
                    bestError = error;
                }
            }
            return (best, bestError);
        }

        static List<Fallback> DefaultFallbacks()
        {
            Fallback F(string action, params string[] alternatives) => new Fallback { Action = action, Alternatives = alternatives };
            return new List<Fallback>
            {
                F("jab", "attack", "punch"),
                F("straight", "jab", "attack", "punch"),
                F("spin_kick", "kick", "attack", "jab"),
                F("rolling", "dash", "roll", "slide", "run"),
                F("vault", "jump", "roll"),
                F("climb", "jump"),
                F("plunge", "fall", "jump"),
                F("land", "crouch", "idle"),
                F("overdrive", "special", "kick", "attack"),
                F("hurt", "hit", "idle"),
                F("windup", "charge", "idle"),
                F("smash", "attack", "punch"),
                F("fire", "shoot", "attack"),
                F("run", "walk", "idle"),
                F("air", "fall", "jump", "idle"),
                F("die", "death"),
            };
        }
    }
}
