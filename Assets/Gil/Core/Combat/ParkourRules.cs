using System;
using Gil.Core.Data;

namespace Gil.Core.Combat
{
    public enum ParkourMove
    {
        None,
        Vault,
        Climb,
    }

    /// <summary>
    /// 파쿠르 판정 규칙. 장애물 정보(상대 높이, 넘을/오를 수 있는 표면인지, 착지 공간)를 받아 동작을 고른다.
    /// 레이캐스트 같은 엔진 쪽 조사는 Runtime이 하고, "무엇을 할지"는 여기서만 정한다.
    /// </summary>
    public static class ParkourRules
    {
        /// <param name="relativeHeight">장애물 윗면 높이 - 현재 발 높이(m)</param>
        /// <param name="vaultable">넘을 수 있는 얇은 장애물(난간, 상자)인지</param>
        /// <param name="climbable">올라설 수 있는 윗면이 있는지(컨테이너, 지붕)</param>
        /// <param name="landingClear">넘었을 때 착지할 공간이 비었는지</param>
        public static ParkourMove Choose(ParkourDef def, float relativeHeight, bool vaultable, bool climbable, bool landingClear)
        {
            if (relativeHeight <= 0.05f) return ParkourMove.None;
            if (vaultable && landingClear && relativeHeight <= def.VaultMaxHeight) return ParkourMove.Vault;
            if (climbable && relativeHeight <= def.ClimbMaxHeight) return ParkourMove.Climb;
            return ParkourMove.None;
        }

        /// <summary>높이 차가 허용치 이내일 때만 근접 공격이 닿는다(높은 곳은 근접형 적의 손이 닿지 않는 안전지대).</summary>
        public static bool CanMeleeReach(ParkourDef def, float heightDifference) =>
            Math.Abs(heightDifference) <= def.MeleeHeightTolerance;

        /// <summary>떨어진 높이에 따라 내려찍기 피해 배율이 커진다(최소 높이에서 기본 배율, 높이 2배면 1.5배까지).</summary>
        public static float PlungeMultiplier(ParkourDef def, float fallHeight)
        {
            if (fallHeight < def.PlungeMinHeight) return 0f;
            var bonus = Math.Min(0.5f, (fallHeight - def.PlungeMinHeight) / Math.Max(0.01f, def.PlungeMinHeight) * 0.5f);
            return def.PlungeDamageMultiplier * (1f + bonus);
        }
    }
}
