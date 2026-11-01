using System;
using System.Collections.Generic;

namespace Gil.Core.Data
{
    // 기획 데이터의 불변에 가까운 정의 객체들. 플레이어·적·아이템·의체 슬롯 정의는 [Serializable]이라
    // 데이터 에셋(Gil.Runtime.Data의 ScriptableObject)이 그대로 담아 인스펙터에서 고친다. 대화·퀘스트·NPC·지역은 JSON에서 읽는다.
    // 런타임 상태(현재 체력, 남은 쿨타임 등)는 여기 두지 않고 각 시스템이 따로 가진다. 게임 중에 정의를 고치지 말 것(에디터에서는 에셋이 바뀐다).

    [Serializable]
    public sealed class ComboStepDef
    {
        /// <summary>동작 이름(잽·스트레이트·돌려차기 등). 애니메이션 선택 키로도 쓴다.</summary>
        public string Name;
        public float DamageMultiplier;
        public float Range;
        public float ArcDegrees;
        public float Windup;
        public float Active;
        public float Recovery;
        /// <summary>Recovery 중 이 시간 안에 다시 공격을 누르면 다음 타로 이어진다.</summary>
        public float ComboWindow;
        public float Lunge;
        public float HitStop;
    }

    /// <summary>구르기(회피). 동작 중 일부 시간 무적이고, 바로 앞이 넘거나 오를 수 있는 지형이면 파쿠르로 바뀐다.</summary>
    [Serializable]
    public sealed class RollingDef
    {
        public float Duration;
        public float Distance;
        public float InvulnerableTime;
        public float Cooldown;
        /// <summary>한 번 구를 때 쓰는 스태미나. 남은 양이 이보다 적으면 구를 수 없다.</summary>
        public float StaminaCost = 30f;
    }

    /// <summary>달리기(Sprint). 버튼을 누르고 있는 동안 이동 속도가 빨라지고 스태미나가 계속 줄어든다.</summary>
    [Serializable]
    public sealed class SprintDef
    {
        /// <summary>달리는 동안 이동 속도 배율(걷기 속도 × 이 값).</summary>
        public float SpeedMultiplier = 1.6f;
        /// <summary>달리는 동안 초당 스태미나 소모량.</summary>
        public float StaminaPerSecond = 20f;
    }

    /// <summary>스태미나. 달리기·구르기에 쓰고, 쓰지 않은 채로 잠시 지나면 회복된다.</summary>
    [Serializable]
    public sealed class StaminaDef
    {
        public float Max = 100f;
        /// <summary>마지막으로 쓴 뒤 회복이 시작될 때까지(초).</summary>
        public float RegenDelay = 1f;
        /// <summary>회복 중 초당 회복량.</summary>
        public float RegenPerSecond = 25f;
    }

    /// <summary>주인공의 인공 심장. 공격을 맞히면 차오르고, 가득 차면 필살기 「과부하」를 쓸 수 있다.</summary>
    [Serializable]
    public sealed class HeartDef
    {
        public float Max;
        public float GainPerHit;
        public float GainPerDamageTaken;
        public float SkillCost;
        public float SkillDamageMultiplier;
        public float SkillRadius;
    }

    /// <summary>
    /// 파쿠르 이동. 구르기로 낮은 장애물은 넘고(vault) 높은 구조물에는 올라선다(climb).
    /// 높은 곳에서 떨어지며 공격하면 「내려찍기」 광역기가 된다.
    /// </summary>
    [Serializable]
    public sealed class ParkourDef
    {
        /// <summary>점프(C) 높이(m).</summary>
        public float JumpHeight = 1.2f;
        public float VaultMaxHeight;
        public float ClimbMaxHeight;
        public float VaultDuration;
        public float ClimbDuration;
        /// <summary>넘기 착지 지점까지의 수평 거리.</summary>
        public float VaultDistance;
        public float Gravity;
        public float PlungeDamageMultiplier;
        public float PlungeRadius;
        /// <summary>이 높이 이상에서 떨어질 때만 내려찍기가 나간다.</summary>
        public float PlungeMinHeight;
        /// <summary>높이 차가 이보다 크면 근접 공격이 서로 닿지 않는다.</summary>
        public float MeleeHeightTolerance;
    }

    [Serializable]
    public sealed class PlayerDef
    {
        public int MaxHp;
        public float MoveSpeed;
        public int Attack;
        public int Defense;
        public float CritChance;
        public float CritMultiplier;
        public float HurtInvulnerableTime;
        public List<ComboStepDef> Combo = new List<ComboStepDef>();
        public RollingDef Rolling = new RollingDef();
        public SprintDef Sprint = new SprintDef();
        public StaminaDef Stamina = new StaminaDef();
        public HeartDef Heart;
        public ParkourDef Parkour;
    }

    public enum EnemyKind
    {
        Melee,
        Ranged,
    }

    [Serializable]
    public sealed class EnemyDef
    {
        public string Id;
        public string Name;
        public EnemyKind Kind;
        public int MaxHp;
        public int Attack;
        public int Defense;
        public float MoveSpeed;
        public float AggroRange;
        public float AttackRange;
        /// <summary>원거리형이 유지하려는 거리. 이보다 가까우면 뒤로 물러난다.</summary>
        public float PreferredRange;
        /// <summary>공격 예고(바닥 표시)가 보이는 시간. 플레이어가 회피할 여유를 준다.</summary>
        public float Windup;
        public float AttackDuration;
        public float Recovery;
        public float Cooldown;
        public float StaggerTime;
        /// <summary>이 양 이상의 피해를 한 번에 받아야 경직된다. 0이면 매번 경직.</summary>
        public int Poise;
        public float ProjectileSpeed;
        /// <summary>처치 보상(푼). 1냥 = 10전 = 100푼.</summary>
        public int Coins;
        public string Color;
    }

    [Serializable]
    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>가격(푼). 1냥 = 10전 = 100푼.</summary>
        public int Price;
        public int Heal;
    }

    public sealed class ShopDef
    {
        public string Id;
        public string Name;
        public readonly List<string> Items = new List<string>();
    }

    public enum ObjectiveType
    {
        Talk,
        Kill,
        Reach,
    }

    public sealed class ObjectiveDef
    {
        public ObjectiveType Type;
        public string Target;
        public int Count;
        public string Label;
    }

    public sealed class ItemStack
    {
        public string ItemId;
        public int Count;
    }

    public sealed class QuestDef
    {
        public string Id;
        public string Title;
        public string Description;
        /// <summary>목표를 모두 채운 뒤 이 NPC에게 말을 걸어야 완료된다. 비어 있으면 즉시 완료.</summary>
        public string TurnInNpc;
        public readonly List<ObjectiveDef> Objectives = new List<ObjectiveDef>();
        /// <summary>보상(푼).</summary>
        public int RewardCoins;
        public readonly List<ItemStack> RewardItems = new List<ItemStack>();
    }

    public sealed class DialogueChoiceDef
    {
        public string Text;
        public string Next;
        public string Condition;
        public readonly List<string> Actions = new List<string>();
    }

    public sealed class DialogueBranchDef
    {
        public string Condition;
        public string Next;
    }

    public sealed class DialogueNodeDef
    {
        public string Id;
        public string Speaker;
        public string Text;
        public string Next;
        public readonly List<string> Actions = new List<string>();
        public readonly List<DialogueChoiceDef> Choices = new List<DialogueChoiceDef>();
        /// <summary>대사 없이 조건에 따라 분기만 하는 노드. 위에서부터 처음 참인 조건으로 이동한다.</summary>
        public readonly List<DialogueBranchDef> Branches = new List<DialogueBranchDef>();
        public bool IsBranchOnly => Branches.Count > 0 && string.IsNullOrEmpty(Text);
    }

    public sealed class DialogueDef
    {
        public string Id;
        public string Start;
        public readonly Dictionary<string, DialogueNodeDef> Nodes = new Dictionary<string, DialogueNodeDef>();
    }

    public sealed class NpcDef
    {
        public string Id;
        public string Name;
        public string Role;
        public string Dialogue;
        public string Color;
    }

    public sealed class WaveEntryDef
    {
        public string EnemyId;
        public int Count;
    }

    public sealed class WaveDef
    {
        public readonly List<WaveEntryDef> Entries = new List<WaveEntryDef>();
    }

    public sealed class AreaDef
    {
        public string Id;
        public string Name;
        public string Subtitle;
        public readonly List<WaveDef> Waves = new List<WaveDef>();
    }

    // ── 의체(사이버웨어) ─────────────────────────────────────

    [Serializable]
    public sealed class CyberSlotDef
    {
        public string Id;
        public string Name;
    }

    /// <summary>의체 고유 기믹. type별 필요한 파라미터는 Cyber.Gimmicks 표에 정의돼 있다.</summary>
    public sealed class GimmickDef
    {
        public string Type;
        public readonly Dictionary<string, float> Params = new Dictionary<string, float>();

        public float Param(string key, float fallback = 0f) => Params.TryGetValue(key, out var v) ? v : fallback;
    }

    public sealed class CyberwareDef
    {
        public string Id;
        public string Name;
        public string Slot;
        /// <summary>시술비(푼).</summary>
        public int Price;
        /// <summary>신체 부하. 장착한 의체 부하의 합은 심장 용량(capacity)을 넘을 수 없다.</summary>
        public int Load;
        public string Description;
        /// <summary>새 게임 시작 시 기본 장착(기본형 인공 심장).</summary>
        public bool Starter;
        public readonly Dictionary<string, float> Stats = new Dictionary<string, float>();
        public GimmickDef Gimmick;
    }
}
