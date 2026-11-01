using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>타격 지점의 섬광(가운데 흰 점 + 색 후광 + 한 줄 불꽃).</summary>
    public sealed class ImpactFx : TimedFx
    {
        [SerializeField] SpriteRenderer _halo;
        [SerializeField] SpriteRenderer _core;
        [SerializeField] SpriteRenderer _spark;
        [SerializeField] float _lifetime = 0.14f;
        [SerializeField, Tooltip("맞은 대상보다 앞에 그리도록 당기는 정렬 보정(단계)")] int _sortNudge = 60;
        Color _color;
        Color _sparkColor;
        float _size;

        public void Play(Vector3 ground, Color color, float size)
        {
            transform.localPosition = Iso.ToScreen(ground + Vector3.up * 0.6f) + Iso.SortNudge(_sortNudge);
            _spark.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 180f));
            _color = color;
            _sparkColor = Color.Lerp(color, Color.white, 0.5f);
            _size = size;
            Begin(_lifetime);
        }

        protected override void Tick(float t)
        {
            var k = (1f + t) * (1f - t);
            _halo.transform.localScale = Vector3.one * _size * 1.6f * (0.6f + t);
            _core.transform.localScale = Vector3.one * _size * 0.35f * k;
            _spark.transform.localScale = new Vector3(_size * 1.4f * (0.5f + t), 1f, 1f);
            _halo.color = new Color(_color.r, _color.g, _color.b, 1f - t);
            _spark.color = new Color(_sparkColor.r, _sparkColor.g, _sparkColor.b, 1f - t);
        }
    }
}
