using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>바닥에 퍼지는 고리. 메시는 반지름 1m 고리이고, 크기로 반지름을 맞춘다.</summary>
    public sealed class RingFx : TimedFx
    {
        [SerializeField] MeshRenderer _renderer;
        Color _color;
        float _radius;

        public void Play(Vector3 ground, float radius, Color color, float lifetime)
        {
            var onPlatform = ground.y > 0.05f;
            transform.localPosition = Iso.ToScreen(ground) + (onPlatform ? Iso.SortNudge(-2) : Vector3.zero);
            // 발판 위 고리는 발판보다 위에 그려야 보인다.
            _renderer.sortingOrder = onPlatform ? 0 : Iso.DecalOrder + 30;
            _color = color;
            _radius = radius;
            Begin(lifetime);
        }

        protected override void Tick(float t)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, _radius, Vfx.Ease(t));
            Vfx.Tint(_renderer, _color, 1f - t * t);
        }
    }
}
