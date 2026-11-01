using System;
using System.Collections.Generic;
using Gil.Core.Data;

namespace Gil.Core.Cyber
{
    public enum StatKind
    {
        /// <summary>그대로 더한다(체력 +20, 오르기 높이 +1m).</summary>
        Add,
        /// <summary>비율로 합산한 뒤 곱한다(이동 속도 +10% → ×1.1).</summary>
        Percent,
    }

    /// <summary>의체가 바꿀 수 있는 능력치 목록. 검증기·UI·계산이 모두 이 표를 본다.</summary>
    public static class StatKeys
    {
        public sealed class Info
        {
            public readonly StatKind Kind;
            public readonly string Label;

            public Info(StatKind kind, string label)
            {
                Kind = kind;
                Label = label;
            }
        }

        public static readonly IReadOnlyDictionary<string, Info> All = new Dictionary<string, Info>
        {
            ["capacity"] = new Info(StatKind.Add, "의체 용량"),
            ["maxHp"] = new Info(StatKind.Add, "최대 체력"),
            ["attack"] = new Info(StatKind.Add, "공격력"),
            ["defense"] = new Info(StatKind.Add, "방어력"),
            ["critChance"] = new Info(StatKind.Add, "치명타 확률"),
            ["critMultiplier"] = new Info(StatKind.Add, "치명타 배율"),
            ["moveSpeed"] = new Info(StatKind.Percent, "이동 속도"),
            ["heartGain"] = new Info(StatKind.Percent, "심장 박동 충전량"),
            ["rollingCooldown"] = new Info(StatKind.Percent, "구르기 재사용 대기"),
            ["rollingDistance"] = new Info(StatKind.Percent, "구르기 거리"),
            ["maxStamina"] = new Info(StatKind.Add, "최대 스태미나"),
            ["staminaRegen"] = new Info(StatKind.Percent, "스태미나 회복량"),
            ["vaultMaxHeight"] = new Info(StatKind.Add, "넘을 수 있는 높이"),
            ["climbMaxHeight"] = new Info(StatKind.Add, "오를 수 있는 높이"),
            ["plungeDamage"] = new Info(StatKind.Percent, "내려찍기 피해"),
            ["plungeRadius"] = new Info(StatKind.Add, "내려찍기 범위"),
            ["skillRadius"] = new Info(StatKind.Add, "과부하 범위"),
        };

        /// <summary>UI용 한 줄 설명: "공격력 +4", "이동 속도 +10%", "구르기 재사용 대기 -25%".</summary>
        public static string Describe(string key, float value)
        {
            if (!All.TryGetValue(key, out var info)) return $"{key} {value:+0.##;-0.##}";
            var sign = value >= 0 ? "+" : "-";
            var abs = Math.Abs(value);
            if (info.Kind == StatKind.Percent || key == "critChance")
                return $"{info.Label} {sign}{abs * 100:0.#}%";
            if (key.EndsWith("Height", StringComparison.Ordinal) || key.EndsWith("Radius", StringComparison.Ordinal))
                return $"{info.Label} {sign}{abs:0.##}m";
            return $"{info.Label} {sign}{abs:0.##}";
        }
    }

    /// <summary>의체 고유 기믹 종류와 필요한 파라미터.</summary>
    public static class Gimmicks
    {
        public const string NeuralSlowmo = "neural_slowmo";
        public const string ShockFinisher = "shock_finisher";
        public const string ChainShock = "chain_shock";
        public const string DoubleRolling = "double_rolling";
        public const string KineticLanding = "kinetic_landing";
        public const string SecondWind = "second_wind";

        public static readonly IReadOnlyDictionary<string, string[]> Required = new Dictionary<string, string[]>
        {
            [NeuralSlowmo] = new[] { "duration" },
            [ShockFinisher] = new[] { "radius", "multiplier" },
            [ChainShock] = new[] { "chance", "radius", "multiplier" },
            [DoubleRolling] = new[] { "charges" },
            [KineticLanding] = new[] { "stun" },
            [SecondWind] = new[] { "hpRatio" },
        };
    }

    /// <summary>
    /// 기본 수치(player.json) + 장착 의체 보정을 합친 최종 능력치. 장착이 바뀔 때마다 새로 계산한다.
    /// 원본 정의를 건드리지 않도록 구르기·달리기·스태미나·파쿠르 정의는 복사본을 만든다.
    /// </summary>
    public sealed class PlayerStats
    {
        public int Capacity;
        public int MaxHp;
        public int Attack;
        public int Defense;
        public float CritChance;
        public float CritMultiplier;
        public float MoveSpeed;
        public float HeartGainMultiplier;
        public float SkillRadius;
        public float PlungeDamageMultiplier;
        public RollingDef Rolling;
        public SprintDef Sprint;
        public StaminaDef Stamina;
        public ParkourDef Parkour;

        public static PlayerStats Compute(PlayerDef b, IEnumerable<CyberwareDef> installed)
        {
            var add = new Dictionary<string, float>();
            foreach (var c in installed)
            foreach (var pair in c.Stats)
                add[pair.Key] = (add.TryGetValue(pair.Key, out var v) ? v : 0f) + pair.Value;

            float A(string key) => add.TryGetValue(key, out var v) ? v : 0f;
            float P(string key) => Math.Max(0.1f, 1f + A(key));

            return new PlayerStats
            {
                Capacity = (int)Math.Round(A("capacity")),
                MaxHp = Math.Max(1, b.MaxHp + (int)Math.Round(A("maxHp"))),
                Attack = Math.Max(1, b.Attack + (int)Math.Round(A("attack"))),
                Defense = Math.Max(0, b.Defense + (int)Math.Round(A("defense"))),
                CritChance = Math.Min(1f, Math.Max(0f, b.CritChance + A("critChance"))),
                CritMultiplier = Math.Max(1f, b.CritMultiplier + A("critMultiplier")),
                MoveSpeed = b.MoveSpeed * P("moveSpeed"),
                HeartGainMultiplier = P("heartGain"),
                SkillRadius = b.Heart.SkillRadius + A("skillRadius"),
                PlungeDamageMultiplier = P("plungeDamage"),
                Rolling = new RollingDef
                {
                    Duration = b.Rolling.Duration,
                    Distance = b.Rolling.Distance * P("rollingDistance"),
                    InvulnerableTime = b.Rolling.InvulnerableTime,
                    Cooldown = b.Rolling.Cooldown * P("rollingCooldown"),
                    StaminaCost = b.Rolling.StaminaCost,
                },
                Sprint = new SprintDef
                {
                    SpeedMultiplier = b.Sprint.SpeedMultiplier,
                    StaminaPerSecond = b.Sprint.StaminaPerSecond,
                },
                Stamina = new StaminaDef
                {
                    Max = Math.Max(1f, b.Stamina.Max + A("maxStamina")),
                    RegenDelay = b.Stamina.RegenDelay,
                    RegenPerSecond = b.Stamina.RegenPerSecond * P("staminaRegen"),
                },
                Parkour = new ParkourDef
                {
                    JumpHeight = b.Parkour.JumpHeight,
                    VaultMaxHeight = b.Parkour.VaultMaxHeight + A("vaultMaxHeight"),
                    ClimbMaxHeight = b.Parkour.ClimbMaxHeight + A("climbMaxHeight"),
                    VaultDuration = b.Parkour.VaultDuration,
                    ClimbDuration = b.Parkour.ClimbDuration,
                    VaultDistance = b.Parkour.VaultDistance,
                    Gravity = b.Parkour.Gravity,
                    PlungeDamageMultiplier = b.Parkour.PlungeDamageMultiplier,
                    PlungeRadius = b.Parkour.PlungeRadius + A("plungeRadius"),
                    PlungeMinHeight = b.Parkour.PlungeMinHeight,
                    MeleeHeightTolerance = b.Parkour.MeleeHeightTolerance,
                },
            };
        }
    }
}
