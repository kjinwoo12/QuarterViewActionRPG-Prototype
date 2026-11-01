using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>먼지 한 덩이. 착지·대시 때 여러 개를 사방으로 흩뿌린다.</summary>
    public sealed class DustFx : TimedFx
    {
        [SerializeField] SpriteRenderer _puff;
        [SerializeField] float _lifetime = 0.4f;
        [SerializeField, Tooltip("발밑 물체보다 앞에 그리도록 당기는 정렬 보정(단계)")] int _sortNudge = 20;
        [SerializeField] Color _color = new Color(0.75f, 0.72f, 0.7f, 0.6f);
        Vector3 _start;
        Vector3 _direction;
        float _size;

        public void Play(Vector3 ground, Vector3 direction, float size)
        {
            _start = ground + direction * 0.2f;
            _direction = direction;
            _size = size;
            Begin(_lifetime);
        }

        protected override void Tick(float t)
        {
            transform.localPosition = Iso.ToScreen(_start + _direction * Vfx.Ease(t) * 0.7f * _size + Vector3.up * t * 0.3f) + Iso.SortNudge(_sortNudge);
            transform.localScale = Vector3.one * 0.3f * _size * (1f + t);
            _puff.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
        }
    }
}
