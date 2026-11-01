using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>
    /// 적 공격 예고 범위(바닥 부채꼴). 적 프리팹 안에 꺼진 채로 배치돼 있다.
    /// 바깥 윤곽은 바로 보이고, 안쪽 채움이 progress에 맞춰 차오른다 — 채움이 가장자리에 닿는 순간 공격이 나간다.
    /// </summary>
    public sealed class Telegraph : MonoBehaviour
    {
        [SerializeField] MeshRenderer _outline;
        [SerializeField] MeshRenderer _fill;
        [SerializeField] MeshFilter _outlineFilter;
        [SerializeField] MeshFilter _fillFilter;
        float _radius;
        float _arc;
        float _angle = float.NaN;

        /// <summary>사거리·각도(360이면 원)로 모양을 정한다. 적이 소환될 때 데이터로 부른다.</summary>
        public void Init(float radius, float arc)
        {
            _radius = radius;
            _arc = arc;
            _angle = float.NaN;
            _outlineFilter.sharedMesh = Decals.Sector(radius, arc, radius * 0.92f, 0, Color.white);
            _fillFilter.sharedMesh = Decals.Sector(radius, arc, 0f, 0, Color.white);
            Vfx.Tint(_outline, Vfx.Danger, 0.9f);
            Vfx.Tint(_fill, Vfx.Danger, 0.35f);
        }

        /// <summary>ground: 공격 중심(지면), direction: 부채꼴 방향(360°면 무시), progress: 0~1.</summary>
        public void Show(Vector3 ground, Vector3 direction, float progress)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            // 발판 위에서는 월드 층(0)에서 발판 바로 앞에, 바닥에서는 바닥 장식 층에 그린다. 테두리는 채움보다 위.
            var onPlatform = ground.y > 0.05f;
            transform.localPosition = Iso.ToScreen(ground) + (onPlatform ? Iso.SortNudge(-3) : Vector3.zero);
            _fill.sortingOrder = onPlatform ? 0 : Iso.DecalOrder + 20;
            _outline.sortingOrder = onPlatform ? 0 : Iso.DecalOrder + 21;
            _outline.transform.localPosition = onPlatform ? Iso.SortNudge(1) : Vector3.zero;

            if (_arc < 360f)
            {
                var angle = Mathf.Round(Decals.AngleOf(direction));
                if (!Mathf.Approximately(angle, _angle))
                {
                    _angle = angle;
                    _outlineFilter.sharedMesh = Decals.Sector(_radius, _arc, _radius * 0.92f, angle, Color.white);
                    _fillFilter.sharedMesh = Decals.Sector(_radius, _arc, 0f, angle, Color.white);
                }
            }
            var p = Mathf.Clamp01(progress);
            _fill.transform.localScale = Vector3.one * p;
            Vfx.Tint(_fill, Vfx.Danger, 0.25f + 0.35f * p);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
