using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>
    /// 이펙트 진입점. 이펙트마다 프리팹(Assets/Gil/Prefabs/Fx)이 있고, <see cref="VfxLibrary"/> 에셋이 그 프리팹들을 묶어
    /// GameRoot 인스펙터로 연결된다. 여기서는 프리팹을 복제해 위치·색·크기만 넘긴다.
    /// 위치는 전부 지면 좌표로 받는다. 연출 담당이 파티클/VFX Graph로 바꿀 때는 프리팹만 바꾸면 된다.
    /// </summary>
    public static class Vfx
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>GameRoot가 Awake에서 넣는다.</summary>
        public static VfxLibrary Library;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Library = null;

        // 공용 이펙트 색. 값은 VfxLibrary 에셋 인스펙터에서 고친다(에셋이 아직 없으면 흰색).
        public static Color PlayerStrike => Library != null ? Library.PlayerStrike : Color.white;
        public static Color HeartRed => Library != null ? Library.HeartRed : Color.white;
        public static Color Danger => Library != null ? Library.Danger : Color.white;
        public static Color Shock => Library != null ? Library.Shock : Color.white;
        public static Color Gold => Library != null ? Library.Gold : Color.white;

        /// <summary>주먹·발차기 궤적: 허리 높이의 부채꼴이 빠르게 펼쳐졌다 사라진다.</summary>
        public static void Slash(Vector3 ground, Vector3 forward, float radius, float arc, Color color, float lifetime = 0.14f)
        {
            if (Spawn(Library?.Slash, out var fx)) fx.Play(ground, forward, radius, arc, color, lifetime);
        }

        /// <summary>타격 지점의 섬광(가운데 흰 점 + 색 후광).</summary>
        public static void Impact(Vector3 ground, Color color, float size = 0.6f)
        {
            if (Spawn(Library?.Impact, out var fx)) fx.Play(ground, color, size);
        }

        /// <summary>바닥에 퍼지는 고리(과부하, 적 소환, 사망, 내려찍기 등).</summary>
        public static void Ring(Vector3 ground, float radius, Color color, float lifetime = 0.35f)
        {
            if (Spawn(Library?.Ring, out var fx)) fx.Play(ground, radius, color, lifetime);
        }

        /// <summary>착지·대시 먼지.</summary>
        public static void Dust(Vector3 ground, float size = 1f)
        {
            for (var i = 0; i < 5; i++)
            {
                if (!Spawn(Library?.Dust, out var fx)) return;
                var a = Random.Range(0f, Mathf.PI * 2f);
                fx.Play(ground, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), size);
            }
        }

        /// <summary>전격(연쇄 전격 의체): 두 지점 사이를 지그재그 번개가 잇는다.</summary>
        public static void Lightning(Vector3 fromGround, Vector3 toGround, Color color, float lifetime = 0.16f)
        {
            if (Spawn(Library?.Lightning, out var fx)) fx.Play(fromGround, toGround, color, lifetime);
            Impact(toGround, color, 0.5f);
        }

        /// <summary>지역 씬의 표현 루트 아래에 프리팹을 복제한다(지역을 내리면 함께 정리된다).</summary>
        static bool Spawn<T>(T prefab, out T instance) where T : Component
        {
            instance = null;
            if ((Object)prefab == null || Iso.ViewRoot == null) return false;
            instance = Object.Instantiate(prefab, Iso.ViewRoot, false);
            return true;
        }

        internal static void Tint(Renderer r, Color color, float alpha)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)));
            r.SetPropertyBlock(block);
        }

        internal static float Ease(float t) => 1f - (1f - t) * (1f - t);
    }
}
