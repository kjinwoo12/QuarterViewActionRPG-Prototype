using System.Collections.Generic;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>두 지점을 잇는 지그재그 번개. 번개 모양은 매번 무작위라 실행할 때 메시를 만든다.</summary>
    public sealed class LightningFx : TimedFx
    {
        [SerializeField] MeshFilter _filter;
        [SerializeField] MeshRenderer _renderer;
        [SerializeField, Range(2, 16)] int _segments = 7;
        [SerializeField] float _jitter = 0.18f;
        [SerializeField] float _width = 0.035f;
        [SerializeField, Tooltip("이어진 대상들보다 앞에 그리도록 당기는 정렬 보정(단계)")] int _sortNudge = 70;
        Color _color;
        Mesh _mesh;

        public void Play(Vector3 fromGround, Vector3 toGround, Color color, float lifetime)
        {
            var a = Iso.ToScreen(fromGround + Vector3.up * 0.6f);
            var b = Iso.ToScreen(toGround + Vector3.up * 0.6f);
            var data = new MeshData();
            var points = new List<Vector3> { a };
            var normal = Vector3.Cross(b - a, Vector3.forward).normalized;
            for (var i = 1; i < _segments; i++)
                points.Add(Vector3.Lerp(a, b, i / (float)_segments) + normal * Random.Range(-_jitter, _jitter));
            points.Add(b);
            for (var i = 0; i < points.Count - 1; i++)
            {
                var n = Vector3.Cross(points[i + 1] - points[i], Vector3.forward).normalized * _width;
                data.AddPolygon(new[] { points[i] - n, points[i] + n, points[i + 1] + n, points[i + 1] - n }, Color.white);
            }
            _mesh = data.ToMesh("Bolt");
            _filter.sharedMesh = _mesh;
            // 정렬은 메시 중심(두 끝의 가운데) 기준이라, 앞쪽 끝보다 뒤에 그려지지 않도록 그 차이만큼 더 당긴다.
            var front = Mathf.Min(a.y + a.z, b.y + b.z);
            var center = (a.y + a.z + b.y + b.z) * 0.5f;
            transform.localPosition = new Vector3(0f, 0f, front - center) + Iso.SortNudge(_sortNudge);
            _color = color;
            Begin(lifetime);
        }

        protected override void Tick(float t) => Vfx.Tint(_renderer, Color.Lerp(Color.white, _color, t), 1f - t);

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
