using System.Collections.Generic;
using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>정점 색만 가진 2D 메시 누적기.</summary>
    public sealed class MeshData
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();

        public int Count => _triangles.Count;

        /// <summary>볼록 다각형을 부채꼴로 삼각분할해 추가한다. 감는 방향은 상관없다(양면 렌더링).</summary>
        public void AddPolygon(IReadOnlyList<Vector3> points, Color color)
        {
            var start = _vertices.Count;
            foreach (var p in points)
            {
                _vertices.Add(p);
                _colors.Add(color);
            }
            for (var i = 1; i < points.Count - 1; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + i);
                _triangles.Add(start + i + 1);
            }
        }

        public void AddQuadStrip(IReadOnlyList<Vector3> outer, IReadOnlyList<Vector3> inner, Color color)
        {
            for (var i = 0; i < outer.Count - 1; i++)
                AddPolygon(new[] { outer[i], outer[i + 1], inner[i + 1], inner[i] }, color);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[_vertices.Count]));
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>바닥에 깔리는 평면 장식(예고 범위, 고리, 베기 궤적)의 메시. 지면 도형을 투영해 화면 메시로 만든다.</summary>
    public static class Decals
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Cache.Clear();

        /// <summary>
        /// 지면 부채꼴. angle은 지면 기준 진행 방향(도, +x에서 +z로 반시계), arc는 전체 각도, inner는 안쪽 반지름.
        /// 결과 메시의 원점은 부채꼴 중심이다.
        /// </summary>
        public static Mesh Sector(float radius, float arc, float inner, float angle, Color color, int segments = 40)
        {
            var key = $"{radius:0.00}_{arc:0.0}_{inner:0.00}_{angle:0}_{ColorUtility.ToHtmlStringRGBA(color)}_{segments}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var data = new MeshData();
            var outer = new List<Vector3>();
            var innerPoints = new List<Vector3>();
            var full = arc >= 359.9f;
            for (var i = 0; i <= segments; i++)
            {
                var a = (angle - arc * 0.5f + arc * i / segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                outer.Add(Iso.ToScreen(dir * radius));
                innerPoints.Add(Iso.ToScreen(dir * inner));
            }
            if (inner > 0.001f || full && inner > 0)
                data.AddQuadStrip(outer, innerPoints, color);
            else
                for (var i = 0; i < segments; i++) data.AddPolygon(new[] { Vector3.zero, outer[i], outer[i + 1] }, color);

            var mesh = data.ToMesh("Decal");
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>지면 방향 벡터 → Sector의 angle(도).</summary>
        public static float AngleOf(Vector3 groundDirection) => Mathf.Atan2(groundDirection.z, groundDirection.x) * Mathf.Rad2Deg;
    }
}
