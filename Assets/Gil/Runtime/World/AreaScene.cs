using System.Collections.Generic;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>
    /// 지역 씬(Assets/Gil/Scenes/Areas/Area_{id}.unity)의 루트. 씬마다 하나 두고, 걷기 영역·배경색·출입 지점을 인스펙터에 적는다.
    /// 환경광은 자식 "Ambient"(Global Light2D)가 맡는다. 그래서 지역 씬만 열어도 씬 뷰에서 게임과 같은 밝기로 보인다.
    /// 출입 지점은 자식 Transform(이름 = 지점 이름, 앞 방향 = 바라볼 방향)이라 씬 뷰에서 바로 옮길 수 있다.
    /// <see cref="AreaManager"/>가 이 씬을 Additive로 불러오고 내린다.
    /// </summary>
    public sealed class AreaScene : MonoBehaviour
    {
        public string AreaId;
        /// <summary>걷기 가능 영역(지면 x, z). 카메라 경계와 적 이동 제한에 쓴다.</summary>
        [Tooltip("걷기 가능 영역(지면 x, z). 카메라 경계와 적 이동 제한에 쓴다")] public Rect Walkable;
        public Color Background = Color.black;
        /// <summary>표현(화면 좌표) 루트. 이펙트·외형이 여기 붙어 씬을 내릴 때 함께 정리된다.</summary>
        public Transform View;
        public List<Transform> Spawns = new List<Transform>();
        /// <summary>적 웨이브가 있는 지역만 채운다.</summary>
        public WaveDirector Waves;

        public static string SceneName(string areaId) => "Area_" + areaId;

        void Awake()
        {
            // 씬 안의 다른 오브젝트가 Start에서 이펙트·외형을 만들기 전에 루트를 잡아 둔다.
            Iso.ViewRoot = View;
            Iso.LogicRoot = transform;
        }

        void OnDestroy()
        {
            if (Iso.ViewRoot == View) Iso.ViewRoot = null;
            if (Iso.LogicRoot == transform) Iso.LogicRoot = null;
        }

        /// <summary>이름으로 출입 지점을 찾는다. 없으면 "start".</summary>
        public Transform Spawn(string name)
        {
            Transform fallback = null;
            foreach (var s in Spawns)
            {
                if (s == null) continue;
                if (s.name == name) return s;
                if (s.name == "start") fallback = s;
            }
            if (fallback == null) Debug.LogError($"[Gil] {AreaId}: 출입 지점 '{name}'도 'start'도 없습니다");
            return fallback;
        }

    }
}
