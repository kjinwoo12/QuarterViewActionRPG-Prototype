using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>주먹·발차기 궤적(허리 높이 부채꼴). 부채꼴 모양은 사거리·각도에 맞춰 실행할 때 고른다.</summary>
    public sealed class SlashFx : TimedFx
    {
        [SerializeField] MeshFilter _filter;
        [SerializeField] MeshRenderer _renderer;
        [SerializeField, Range(0f, 1f)] float _alpha = 0.85f;
        [SerializeField, Tooltip("휘두른 캐릭터보다 앞에 그리도록 당기는 정렬 보정(단계)")] int _sortNudge = 30;
        Color _color;

        public void Play(Vector3 ground, Vector3 forward, float radius, float arc, Color color, float lifetime)
        {
            _filter.sharedMesh = Decals.Sector(radius, arc, radius * 0.6f, Mathf.Round(Decals.AngleOf(forward)), Color.white);
            transform.localPosition = Iso.ToScreen(ground + Vector3.up * 0.45f) + Iso.SortNudge(_sortNudge);
            _color = color;
            Begin(lifetime);
        }

        protected override void Tick(float t)
        {
            transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.05f, t);
            Vfx.Tint(_renderer, _color, _alpha * (1f - t));
        }
    }
}
