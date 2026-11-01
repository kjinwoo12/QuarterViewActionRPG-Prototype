using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// 타이틀 배경 위로 꽃잎 몇 장을 흩날린다. 꽃잎은 프리팹을 시작할 때 미리 만들어 두고
    /// 화면 밖으로 나가면 반대편에서 다시 들어오게 해 끊김 없이 반복한다. 일시정지와 무관하게 실시간으로 움직인다.
    /// </summary>
    public sealed class TitlePetals : MonoBehaviour
    {
        [SerializeField, Tooltip("꽃잎이 날리는 영역. 비우면 이 오브젝트의 RectTransform")]
        RectTransform _area;
        [SerializeField, Tooltip("꽃잎 한 장 프리팹 (Image)")]
        Image _petalPrefab;

        [Header("양")]
        [SerializeField, Range(0, 40), Tooltip("동시에 보이는 꽃잎 수")]
        int _count = 7;

        [Header("움직임 (캔버스 기준 픽셀)")]
        [SerializeField, Tooltip("바람 속도 (x 오른쪽, y 위). 꽃잎마다 이 값에 아래 배율을 곱한다")]
        Vector2 _wind = new Vector2(-80f, -35f);
        [SerializeField, Tooltip("꽃잎마다 다른 속도 배율 (최소, 최대)")]
        Vector2 _speedScale = new Vector2(0.6f, 1.3f);
        [SerializeField, Range(0f, 80f), Tooltip("좌우로 살랑이는 폭")]
        float _swayAmplitude = 24f;
        [SerializeField, Range(0f, 3f), Tooltip("살랑임 빠르기 (회/초)")]
        float _swayFrequency = 0.35f;
        [SerializeField, Range(0f, 360f), Tooltip("회전 속도 최대값 (도/초)")]
        float _spinSpeed = 90f;
        [SerializeField, Range(0f, 3f), Tooltip("뒤집히듯 납작해지는 빠르기 (회/초)")]
        float _flipFrequency = 0.5f;

        [Header("모양")]
        [SerializeField, Tooltip("꽃잎 크기 (최소, 최대 픽셀)")]
        Vector2 _sizeRange = new Vector2(18f, 34f);
        [SerializeField, Tooltip("꽃잎 불투명도 (최소, 최대)")]
        Vector2 _alphaRange = new Vector2(0.45f, 0.85f);

        struct Petal
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public float Speed, Phase, Spin, Angle, Size;
        }

        Petal[] _petals;

        RectTransform Area => _area != null ? _area : (RectTransform)transform;

        void Start()
        {
            if (_petalPrefab == null) return;
            _petals = new Petal[_count];
            var rect = Area.rect;
            for (var i = 0; i < _count; i++)
            {
                var image = Instantiate(_petalPrefab, Area);
                image.raycastTarget = false;
                _petals[i] = new Petal { Rect = image.rectTransform, Image = image };
                Respawn(ref _petals[i], rect, scatter: true);
            }
        }

        void Update()
        {
            if (_petals == null) return;
            var dt = Time.unscaledDeltaTime;
            var time = Time.unscaledTime;
            var rect = Area.rect;
            var margin = _sizeRange.y * 2f;
            for (var i = 0; i < _petals.Length; i++)
            {
                ref var p = ref _petals[i];
                p.Position += _wind * (p.Speed * dt);
                p.Angle += p.Spin * dt;
                var sway = Mathf.Sin((time * _swayFrequency + p.Phase) * Mathf.PI * 2f) * _swayAmplitude;
                var flip = Mathf.Lerp(0.25f, 1f, Mathf.Abs(Mathf.Cos((time * _flipFrequency + p.Phase) * Mathf.PI * 2f)));
                p.Rect.anchoredPosition = p.Position + new Vector2(sway, 0f);
                p.Rect.localRotation = Quaternion.Euler(0f, 0f, p.Angle);
                p.Rect.localScale = new Vector3(1f, flip, 1f);

                if (p.Position.x > rect.xMax + margin || p.Position.x < rect.xMin - margin ||
                    p.Position.y < rect.yMin - margin || p.Position.y > rect.yMax + margin)
                    Respawn(ref p, rect, scatter: false);
            }
        }

        /// <summary>바람이 불어오는 쪽 가장자리(처음엔 영역 전체)에 꽃잎을 다시 놓는다.</summary>
        void Respawn(ref Petal p, Rect rect, bool scatter)
        {
            p.Speed = Random.Range(_speedScale.x, _speedScale.y);
            p.Phase = Random.value;
            p.Spin = Random.Range(-_spinSpeed, _spinSpeed);
            p.Angle = Random.Range(0f, 360f);
            p.Size = Random.Range(_sizeRange.x, _sizeRange.y);
            p.Rect.anchorMin = p.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            p.Rect.sizeDelta = new Vector2(p.Size, p.Size * 0.62f);
            var c = p.Image.color;
            c.a = Random.Range(_alphaRange.x, _alphaRange.y);
            p.Image.color = c;

            var margin = _sizeRange.y;
            if (scatter)
            {
                p.Position = new Vector2(Random.Range(rect.xMin, rect.xMax), Random.Range(rect.yMin, rect.yMax));
                return;
            }
            // 바람이 오는 방향의 가장자리 두 변 중 하나에서 들어온다.
            var fromSide = Random.value < Mathf.Abs(_wind.x) / (Mathf.Abs(_wind.x) + Mathf.Abs(_wind.y) + 1e-4f);
            if (fromSide)
                p.Position = new Vector2(_wind.x >= 0f ? rect.xMin - margin : rect.xMax + margin, Random.Range(rect.yMin, rect.yMax));
            else
                p.Position = new Vector2(Random.Range(rect.xMin, rect.xMax), _wind.y <= 0f ? rect.yMax + margin : rect.yMin - margin);
        }
    }
}
