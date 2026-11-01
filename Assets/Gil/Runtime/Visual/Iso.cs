using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 지면 좌표(시뮬레이션) → 아이소메트릭 화면 좌표(표현) 변환.
    ///
    /// 게임 규칙은 전부 지면 평면(x, z, 높이 y)에서 계산한다. 원형 사거리·넉백·충돌이 등방성으로 유지되고,
    /// 표현을 3D 쿼터뷰로 되돌리거나 다른 투영으로 바꿔도 규칙 코드는 그대로다.
    /// 화면은 2:1 아이소메트릭이며 Unity Isometric Grid(cellSize 1 × 0.5)와 정확히 맞물린다:
    /// 지면 1m × 1m = 타일 한 칸.
    /// </summary>
    public static class Iso
    {
        /// <summary>현재 지역의 표현(화면 좌표) 루트. 이펙트·장식은 여기 붙여 지역 전환 시 함께 정리된다.</summary>
        public static Transform ViewRoot;

        /// <summary>현재 지역의 시뮬레이션(지면 좌표) 루트. 투사체처럼 지역에 속한 논리 오브젝트를 붙인다.</summary>
        public static Transform LogicRoot;

        // 도메인 리로드 없이 플레이를 다시 시작해도 이전 판의 루트가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ViewRoot = null;
            LogicRoot = null;
        }

        /// <summary>높이 1m가 화면에서 차지하는 세로 길이(유닛).</summary>
        public const float HeightScale = 0.62f;

        // 앞뒤 정렬은 Unity 기본 기능에 맡긴다: 2D 렌더러(Gil2DRenderer)의 Transparency Sort Mode = Custom Axis (0, 1, 1).
        // 같은 Order in Layer끼리는 "화면 Y + Z"가 작을수록(화면 아래·앞쪽일수록) 나중에 그린다.
        // 바닥에 선 물체는 Z가 0이라 기준점(발 위치)의 화면 Y로 정렬된다. 높이 올라선 물체는 ToScreen이 Z를 앞으로 당긴다.
        // Order in Layer는 층 구분에만 쓴다: 바닥 → 바닥 장식(예고 범위 등) → 월드(0, 방 뒷벽은 -1) → 머리 위 표시
        public const int FloorOrder = -32000;
        public const int DecalOrder = -31000;
        public const int OverheadOrder = 30000;

        /// <summary>
        /// 높이 1m당 정렬 기준을 앞으로 당기는 양(화면 Z). 화면 Y가 높이만큼 올라간 것을 되돌리고(HeightScale),
        /// 발판 위에 선 대상이 발판보다 앞에 그려지도록 0.6만큼 더 당긴다.
        /// </summary>
        const float HeightSortZ = HeightScale + 0.6f;

        /// <summary>정렬 보정 한 단계(화면 Z). 같은 자리에서 앞뒤를 가를 때 이만큼씩 앞으로 당긴다.</summary>
        public const float SortStep = 1f / 160f;

        /// <summary>입력의 "위/오른쪽"에 해당하는 지면 방향. 화면 위 = 지면 (+x, +z) 대각선.</summary>
        public static readonly Vector3 ScreenUp = new Vector3(1, 0, 1).normalized;
        public static readonly Vector3 ScreenRight = new Vector3(1, 0, -1).normalized;

        public static Vector3 ToScreen(Vector3 ground) =>
            new Vector3((ground.x - ground.z) * 0.5f, (ground.x + ground.z) * 0.25f + ground.y * HeightScale, -ground.y * HeightSortZ);

        public static Vector3 ToScreen(float x, float y, float z) => ToScreen(new Vector3(x, y, z));

        /// <summary>지면 방향 벡터의 화면상 방향(정규화 안 함).</summary>
        public static Vector2 ToScreenDirection(Vector3 groundDirection) =>
            new Vector2((groundDirection.x - groundDirection.z) * 0.5f, (groundDirection.x + groundDirection.z) * 0.25f);

        /// <summary>정렬 보정: steps 단계만큼 앞(양수) 또는 뒤(음수)로 당기는 화면 오프셋. 보이는 위치는 바뀌지 않는다.</summary>
        public static Vector3 SortNudge(int steps) => new Vector3(0f, 0f, -steps * SortStep);
    }
}
