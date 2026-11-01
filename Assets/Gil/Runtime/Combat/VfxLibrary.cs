using UnityEngine;

namespace Gil.Runtime.Combat
{
    /// <summary>이펙트 프리팹과 공용 이펙트 색 묶음(Assets/Gil/Prefabs/Fx/VfxLibrary.asset). GameRoot 인스펙터에 연결한다.</summary>
    [CreateAssetMenu(menuName = "Gil/Vfx Library", fileName = "VfxLibrary")]
    public sealed class VfxLibrary : ScriptableObject
    {
        public SlashFx Slash;
        public ImpactFx Impact;
        public RingFx Ring;
        public DustFx Dust;
        public LightningFx Lightning;

        [Header("공용 색")]
        [Tooltip("주먹 궤적(분홍)")] public Color PlayerStrike = new Color(1f, 0.55f, 0.75f);
        [Tooltip("인공 심장·콤보 마무리·내려찍기")] public Color HeartRed = new Color(1f, 0.22f, 0.32f);
        [Tooltip("적 공격 예고·적 타격")] public Color Danger = new Color(1f, 0.18f, 0.22f);
        [Tooltip("전격 의체·전격탄")] public Color Shock = new Color(0.45f, 0.85f, 1f);
        [Tooltip("치명타·처치 보상")] public Color Gold = new Color(1f, 0.85f, 0.3f);
    }
}
