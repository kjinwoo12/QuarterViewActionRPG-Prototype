using System;
using System.Collections.Generic;
using System.Globalization;
using Gil.Core.Combat;
using Gil.Core.Cyber;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Items;
using Gil.Core.Quests;

namespace Gil.Core
{
    /// <summary>
    /// 한 판의 게임 상태(도메인 모델). 씬·프리팹과 무관한 "규칙"은 모두 여기와 하위 시스템에 있고,
    /// Unity 쪽 컴포넌트는 이 객체의 이벤트를 구독해 화면에 보여 주기만 한다.
    /// </summary>
    public sealed class GameSession
    {
        public readonly GameDatabase Db;
        public readonly Inventory Inventory = new Inventory();
        public readonly Wallet Wallet = new Wallet();
        public readonly QuestLog Quests;
        public readonly HashSet<string> Flags = new HashSet<string>();
        public readonly Health PlayerHealth;
        public readonly HeartGauge Heart;
        /// <summary>달리기·구르기에 쓰는 스태미나. 저장하지 않는다(불러오면 가득 찬 채로 시작).</summary>
        public readonly Stamina Stamina;
        public readonly CyberwareLoadout Cyberware;
        /// <summary>기본 수치 + 의체 보정. 장착이 바뀌면 새로 계산된다.</summary>
        public PlayerStats Stats { get; private set; }
        bool _secondWindUsed;
        public string CurrentArea { get; private set; }

        /// <summary>이번 플레이에서 쓰러뜨린 적 수(결산 화면용, 저장하지 않음).</summary>
        public int EnemiesDefeated { get; private set; }

        // 대화 액션이 화면 쪽 처리를 요청할 때 쓰는 이벤트들
        public event Action<string> ShopRequested;
        public event Action<string> TravelRequested;
        public event Action DemoEndRequested;
        public event Action<string> Notice;
        public event Action<string> AreaEntered;
        public event Action CyberwareRequested;
        /// <summary>능력치가 다시 계산됐다(의체 장착/복원).</summary>
        public event Action StatsChanged;

        public GameSession(GameDatabase db)
        {
            Db = db;
            Quests = new QuestLog(db);
            PlayerHealth = new Health(db.Player.MaxHp);
            Heart = new HeartGauge(db.Player.Heart);
            Stamina = new Stamina(db.Player.Stamina);
            Quests.Completed += GrantRewards;
            Cyberware = new CyberwareLoadout(db);
            Cyberware.Changed += RecomputeStats;
            Cyberware.ResetToStarters();
        }

        void RecomputeStats()
        {
            Stats = PlayerStats.Compute(Db.Player, Cyberware.Installed);
            PlayerHealth.SetMax(Stats.MaxHp);
            Heart.GainMultiplier = Stats.HeartGainMultiplier;
            Stamina.Configure(Stats.Stamina);
            StatsChanged?.Invoke();
        }

        // ── 의체 ──────────────────────────────────────────────

        /// <summary>리퍼닥에게 의체 시술을 받는다(구매 + 장착). 기존 의체는 회수되고 환불은 없다.</summary>
        public InstallResult InstallCyberware(string id)
        {
            if (!Db.Cyberware.TryGetValue(id, out var def)) return InstallResult.Unknown;
            var check = Cyberware.Check(def);
            if (check != InstallResult.Ok) return check;
            if (!Wallet.TrySpend(def.Price)) return InstallResult.NotEnoughCoins;
            Cyberware.Install(def);
            Notice?.Invoke($"의체 시술 완료: {def.Name}");
            return InstallResult.Ok;
        }

        /// <summary>
        /// 「제2 심실」: 쓰러지는 순간 지역당 한 번 되살아난다. 쓰였으면 true.
        /// </summary>
        public bool TryUseSecondWind()
        {
            var g = Cyberware.Gimmick(Gimmicks.SecondWind);
            if (g == null || _secondWindUsed || !PlayerHealth.IsDead) return false;
            _secondWindUsed = true;
            PlayerHealth.Revive(Math.Max(1, (int)Math.Round(PlayerHealth.Max * g.Param("hpRatio", 0.3f))));
            Notice?.Invoke("제2 심실 가동 — 심장이 다시 뛴다");
            return true;
        }

        // ── 게임 쪽에서 알려 주는 사건들 ───────────────────────────────

        public void OnTalkedTo(string npcId) => Quests.Notify(ObjectiveType.Talk, npcId);

        public void OnEnemyKilled(string enemyId)
        {
            EnemiesDefeated++;
            if (Db.Enemies.TryGetValue(enemyId, out var def)) Wallet.Add(def.Coins);
            Quests.Notify(ObjectiveType.Kill, enemyId);
        }

        public void EnterArea(string areaId)
        {
            CurrentArea = areaId;
            _secondWindUsed = false;
            AreaEntered?.Invoke(areaId);
            Quests.Notify(ObjectiveType.Reach, areaId);
        }

        /// <summary>회복약 등 소모품 사용. 체력이 가득하면 쓰지 않는다.</summary>
        public bool UseItem(string itemId)
        {
            if (!Db.Items.TryGetValue(itemId, out var item) || item.Heal <= 0) return false;
            if (PlayerHealth.IsDead || PlayerHealth.Current >= PlayerHealth.Max) return false;
            if (!Inventory.Remove(itemId)) return false;
            PlayerHealth.Heal(item.Heal);
            return true;
        }

        public BuyResult Buy(string shopId, string itemId) => Shop.TryBuy(Db, shopId, itemId, Inventory, Wallet);

        // ── 대화 명령 ──────────────────────────────────────────────

        public bool Evaluate(DialogueCommands.Command c)
        {
            switch (c.Verb)
            {
                case "flag": return Flags.Contains(c.Arg(0));
                case "quest_none": return Quests.StateOf(c.Arg(0)) == QuestState.NotStarted;
                case "quest_active": return Quests.StateOf(c.Arg(0)) == QuestState.Active;
                case "quest_ready": return Quests.StateOf(c.Arg(0)) == QuestState.ReadyToTurnIn;
                case "quest_done": return Quests.StateOf(c.Arg(0)) == QuestState.Completed;
                case "has_item": return Inventory.Count(c.Arg(0)) > 0;
                case "has_cyberware": return Cyberware.IsInstalled(c.Arg(0));
                default: throw new InvalidOperationException($"알 수 없는 조건 '{c.Verb}'");
            }
        }

        public void Execute(DialogueCommands.Command c)
        {
            switch (c.Verb)
            {
                case "accept_quest":
                    if (Quests.Accept(c.Arg(0))) Notice?.Invoke($"퀘스트 수락: {Db.Quests[c.Arg(0)].Title}");
                    break;
                case "turn_in_quest":
                    Quests.TurnIn(c.Arg(0));
                    break;
                case "open_shop":
                    ShopRequested?.Invoke(c.Arg(0));
                    break;
                case "give_item":
                {
                    var count = int.Parse(c.Arg(1), CultureInfo.InvariantCulture);
                    Inventory.Add(c.Arg(0), count);
                    Notice?.Invoke($"{Db.Items[c.Arg(0)].Name} ×{count} 획득");
                    break;
                }
                case "set_flag":
                    Flags.Add(c.Arg(0));
                    break;
                case "heal_full":
                    PlayerHealth.Heal(PlayerHealth.Max);
                    break;
                case "travel":
                    TravelRequested?.Invoke(c.Arg(0));
                    break;
                case "end_demo":
                    DemoEndRequested?.Invoke();
                    break;
                case "give_coins":
                {
                    var amount = int.Parse(c.Arg(0), CultureInfo.InvariantCulture);
                    Wallet.Add(amount);
                    Notice?.Invoke($"{Money.Format(amount)} 받음");
                    break;
                }
                case "open_cyberware":
                    CyberwareRequested?.Invoke();
                    break;
                default:
                    throw new InvalidOperationException($"알 수 없는 액션 '{c.Verb}'");
            }
        }

        void GrantRewards(QuestProgress q)
        {
            Wallet.Add(q.Def.RewardCoins);
            foreach (var r in q.Def.RewardItems) Inventory.Add(r.ItemId, r.Count);
            Notice?.Invoke($"퀘스트 완료: {q.Def.Title}" + (q.Def.RewardCoins > 0 ? $"  (+{Money.Format(q.Def.RewardCoins)})" : ""));
        }

        // ── 세이브 ────────────────────────────────────────────────

        public SaveData Capture() => SaveData.Capture(this);

        public void Restore(SaveData save)
        {
            Inventory.Clear();
            foreach (var pair in save.Items) Inventory.Add(pair.Key, pair.Value);
            Wallet.Set(save.Coins);
            Flags.Clear();
            foreach (var f in save.Flags) Flags.Add(f);
            Quests.Clear();
            foreach (var q in save.Quests) Quests.Restore(q.Id, q.State, q.Counts);
            Cyberware.Restore(save.Cyberware);
            PlayerHealth.Revive(save.Hp);
            Heart.Add(save.Heart - Heart.Value);
            Stamina.Refill();
            CurrentArea = save.Area;
        }
    }
}
