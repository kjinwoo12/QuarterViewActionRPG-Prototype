using System;
using System.Collections.Generic;
using Gil.Core.Util;

namespace Gil.Core.Data
{
    /// <summary>
    /// 모든 기획 데이터를 담는 저장소. Unity에서는 데이터 에셋(Gil.Runtime.Data.GameData)이 정의를 넣고 남은 JSON 텍스트를 읽힌다.
    /// 정의(플레이어·적·아이템·상점·의체)는 ScriptableObject에서 <see cref="SetPlayer"/>·Add*로, 대화·퀘스트·NPC·지역은 JSON 텍스트를
    /// <see cref="LoadJson"/>으로 받는다. JSON 형식 오류는 FormatException(경로 포함)으로, 참조 오류는 <see cref="DataValidator"/>가 보고한다.
    /// </summary>
    public sealed class GameDatabase
    {
        /// <summary>JSON으로 남아 있는 데이터 파일(확장자 제외).</summary>
        public static readonly string[] TextFileNames = { "quests", "dialogues", "npcs", "areas" };

        public PlayerDef Player { get; private set; }
        public readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>();
        public readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public readonly Dictionary<string, ShopDef> Shops = new Dictionary<string, ShopDef>();
        public readonly Dictionary<string, QuestDef> Quests = new Dictionary<string, QuestDef>();
        public readonly Dictionary<string, DialogueDef> Dialogues = new Dictionary<string, DialogueDef>();
        public readonly Dictionary<string, NpcDef> Npcs = new Dictionary<string, NpcDef>();
        public readonly Dictionary<string, AreaDef> Areas = new Dictionary<string, AreaDef>();
        /// <summary>의체 슬롯(데이터에 적힌 순서 유지).</summary>
        public readonly List<CyberSlotDef> CyberSlots = new List<CyberSlotDef>();
        public readonly Dictionary<string, CyberwareDef> Cyberware = new Dictionary<string, CyberwareDef>();
        /// <summary>의체 목록(데이터에 적힌 순서 유지, UI 표시용).</summary>
        public readonly List<CyberwareDef> CyberwareList = new List<CyberwareDef>();

        // ── 정의 넣기(데이터 에셋에서) ─────────────────────────

        public void SetPlayer(PlayerDef player) => Player = player;
        public void AddEnemy(EnemyDef def, string path) => AddUnique(Enemies, def.Id, def, path);
        public void AddItem(ItemDef def, string path) => AddUnique(Items, def.Id, def, path);
        public void AddShop(ShopDef def, string path) => AddUnique(Shops, def.Id, def, path);

        public void AddCyberSlot(CyberSlotDef slot, string path)
        {
            if (CyberSlots.Exists(x => x.Id == slot.Id)) throw new FormatException($"{path}: 중복된 슬롯 '{slot.Id}'");
            CyberSlots.Add(slot);
        }

        public void AddCyberware(CyberwareDef def, string path)
        {
            AddUnique(Cyberware, def.Id, def, path);
            CyberwareList.Add(def);
        }

        // ── JSON ──────────────────────────────────────────────

        /// <summary>
        /// JSON 데이터 파일 하나를 읽는다. name은 파일 이름(확장자 제외).
        /// player·enemies·items·cyberware는 예전 JSON 형식을 데이터 에셋으로 옮길 때만 쓴다.
        /// </summary>
        public void LoadJson(string name, string text)
        {
            if (text == null) throw new FormatException($"{name}.json 파일을 찾을 수 없습니다");
            JsonObject root;
            try
            {
                root = new JsonObject(Json.Parse(text), name);
            }
            catch (FormatException e)
            {
                throw new FormatException($"{name}.json: {e.Message}", e);
            }
            switch (name)
            {
                case "player": LoadPlayer(root); break;
                case "enemies": LoadEnemies(root); break;
                case "items": LoadItems(root); break;
                case "quests": LoadQuests(root); break;
                case "dialogues": LoadDialogues(root); break;
                case "npcs": LoadNpcs(root); break;
                case "areas": LoadAreas(root); break;
                case "cyberware": LoadCyberware(root); break;
                default: throw new FormatException($"알 수 없는 데이터 파일 '{name}'");
            }
        }

        void LoadPlayer(JsonObject root)
        {
            var p = new PlayerDef
            {
                MaxHp = root.Int("maxHp"),
                MoveSpeed = root.Float("moveSpeed"),
                Attack = root.Int("attack"),
                Defense = root.Int("defense", 0),
                CritChance = root.Float("critChance", 0f),
                CritMultiplier = root.Float("critMultiplier", 1.5f),
                HurtInvulnerableTime = root.Float("hurtInvulnerableTime", 0.4f),
            };
            foreach (var step in root.Objects("combo"))
            {
                p.Combo.Add(new ComboStepDef
                {
                    Name = step.String("name", $"step{p.Combo.Count + 1}"),
                    DamageMultiplier = step.Float("damageMultiplier"),
                    Range = step.Float("range"),
                    ArcDegrees = step.Float("arcDegrees"),
                    Windup = step.Float("windup"),
                    Active = step.Float("active"),
                    Recovery = step.Float("recovery"),
                    ComboWindow = step.Float("comboWindow"),
                    Lunge = step.Float("lunge", 0f),
                    HitStop = step.Float("hitStop", 0.05f),
                });
            }

            var rolling = root.Object("rolling");
            p.Rolling = new RollingDef
            {
                Duration = rolling.Float("duration"),
                Distance = rolling.Float("distance"),
                InvulnerableTime = rolling.Float("invulnerableTime"),
                Cooldown = rolling.Float("cooldown"),
                StaminaCost = rolling.Float("staminaCost", 30f),
            };
            if (root.Has("sprint"))
            {
                var sprint = root.Object("sprint");
                p.Sprint = new SprintDef
                {
                    SpeedMultiplier = sprint.Float("speedMultiplier", 1.6f),
                    StaminaPerSecond = sprint.Float("staminaPerSecond", 20f),
                };
            }
            if (root.Has("stamina"))
            {
                var stamina = root.Object("stamina");
                p.Stamina = new StaminaDef
                {
                    Max = stamina.Float("max", 100f),
                    RegenDelay = stamina.Float("regenDelay", 1f),
                    RegenPerSecond = stamina.Float("regenPerSecond", 25f),
                };
            }

            var heart = root.Object("heart");
            p.Heart = new HeartDef
            {
                Max = heart.Float("max"),
                GainPerHit = heart.Float("gainPerHit"),
                GainPerDamageTaken = heart.Float("gainPerDamageTaken", 0f),
                SkillCost = heart.Float("skillCost"),
                SkillDamageMultiplier = heart.Float("skillDamageMultiplier"),
                SkillRadius = heart.Float("skillRadius"),
            };
            var parkour = root.Object("parkour");
            p.Parkour = new ParkourDef
            {
                JumpHeight = parkour.Float("jumpHeight", 1.2f),
                VaultMaxHeight = parkour.Float("vaultMaxHeight"),
                ClimbMaxHeight = parkour.Float("climbMaxHeight"),
                VaultDuration = parkour.Float("vaultDuration"),
                ClimbDuration = parkour.Float("climbDuration"),
                VaultDistance = parkour.Float("vaultDistance"),
                Gravity = parkour.Float("gravity"),
                PlungeDamageMultiplier = parkour.Float("plungeDamageMultiplier"),
                PlungeRadius = parkour.Float("plungeRadius"),
                PlungeMinHeight = parkour.Float("plungeMinHeight"),
                MeleeHeightTolerance = parkour.Float("meleeHeightTolerance"),
            };
            Player = p;
        }

        void LoadEnemies(JsonObject root)
        {
            foreach (var e in root.Objects("enemies"))
            {
                var kind = e.String("kind");
                var def = new EnemyDef
                {
                    Id = e.String("id"),
                    Name = e.String("name"),
                    Kind = kind == "ranged" ? EnemyKind.Ranged
                        : kind == "melee" ? EnemyKind.Melee
                        : throw new FormatException($"{e.Path}.kind: melee 또는 ranged여야 합니다 ('{kind}')"),
                    MaxHp = e.Int("maxHp"),
                    Attack = e.Int("attack"),
                    Defense = e.Int("defense", 0),
                    MoveSpeed = e.Float("moveSpeed"),
                    AggroRange = e.Float("aggroRange"),
                    AttackRange = e.Float("attackRange"),
                    PreferredRange = e.Float("preferredRange", 0f),
                    Windup = e.Float("windup"),
                    AttackDuration = e.Float("attackDuration"),
                    Recovery = e.Float("recovery"),
                    Cooldown = e.Float("cooldown"),
                    StaggerTime = e.Float("staggerTime", 0.3f),
                    Poise = e.Int("poise", 0),
                    ProjectileSpeed = e.Float("projectileSpeed", 0f),
                    Coins = e.Int("coins", 0),
                    Color = e.String("color", "#ff4060"),
                };
                AddEnemy(def, e.Path);
            }
        }

        void LoadItems(JsonObject root)
        {
            foreach (var i in root.Objects("items"))
            {
                var def = new ItemDef
                {
                    Id = i.String("id"),
                    Name = i.String("name"),
                    Description = i.String("description", ""),
                    Price = i.Int("price", 0),
                    Heal = i.Int("heal", 0),
                };
                AddItem(def, i.Path);
            }
            foreach (var s in root.Objects("shops"))
            {
                var def = new ShopDef { Id = s.String("id"), Name = s.String("name") };
                def.Items.AddRange(s.Strings("items"));
                AddShop(def, s.Path);
            }
        }

        void LoadQuests(JsonObject root)
        {
            foreach (var q in root.Objects("quests"))
            {
                var def = new QuestDef
                {
                    Id = q.String("id"),
                    Title = q.String("title"),
                    Description = q.String("description", ""),
                    TurnInNpc = q.String("turnInNpc", ""),
                    RewardCoins = q.Int("rewardCoins", 0),
                };
                foreach (var o in q.Objects("objectives"))
                {
                    var type = o.String("type");
                    def.Objectives.Add(new ObjectiveDef
                    {
                        Type = type == "talk" ? ObjectiveType.Talk
                            : type == "kill" ? ObjectiveType.Kill
                            : type == "reach" ? ObjectiveType.Reach
                            : throw new FormatException($"{o.Path}.type: talk/kill/reach 중 하나여야 합니다 ('{type}')"),
                        Target = o.String("target"),
                        Count = o.Int("count", 1),
                        Label = o.String("label"),
                    });
                }
                foreach (var r in q.Objects("rewardItems"))
                    def.RewardItems.Add(new ItemStack { ItemId = r.String("id"), Count = r.Int("count", 1) });
                AddUnique(Quests, def.Id, def, q.Path);
            }
        }

        void LoadDialogues(JsonObject root)
        {
            foreach (var d in root.Objects("dialogues"))
            {
                var def = new DialogueDef { Id = d.String("id"), Start = d.String("start") };
                foreach (var n in d.Objects("nodes"))
                {
                    var node = new DialogueNodeDef
                    {
                        Id = n.String("id"),
                        Speaker = n.String("speaker", ""),
                        Text = n.String("text", ""),
                        Next = n.String("next", ""),
                    };
                    node.Actions.AddRange(n.Strings("actions"));
                    foreach (var c in n.Objects("choices"))
                    {
                        var choice = new DialogueChoiceDef
                        {
                            Text = c.String("text"),
                            Next = c.String("next", ""),
                            Condition = c.String("condition", ""),
                        };
                        choice.Actions.AddRange(c.Strings("actions"));
                        node.Choices.Add(choice);
                    }
                    foreach (var b in n.Objects("branches"))
                        node.Branches.Add(new DialogueBranchDef { Condition = b.String("condition", ""), Next = b.String("next") });
                    AddUnique(def.Nodes, node.Id, node, n.Path);
                }
                AddUnique(Dialogues, def.Id, def, d.Path);
            }
        }

        void LoadNpcs(JsonObject root)
        {
            foreach (var n in root.Objects("npcs"))
            {
                var def = new NpcDef
                {
                    Id = n.String("id"),
                    Name = n.String("name"),
                    Role = n.String("role", ""),
                    Dialogue = n.String("dialogue"),
                    Color = n.String("color", "#c8a060"),
                };
                AddUnique(Npcs, def.Id, def, n.Path);
            }
        }

        void LoadAreas(JsonObject root)
        {
            foreach (var a in root.Objects("areas"))
            {
                var def = new AreaDef
                {
                    Id = a.String("id"),
                    Name = a.String("name"),
                    Subtitle = a.String("subtitle", ""),
                };
                foreach (var w in a.Objects("waves"))
                {
                    var wave = new WaveDef();
                    foreach (var e in w.Objects("enemies"))
                        wave.Entries.Add(new WaveEntryDef { EnemyId = e.String("id"), Count = e.Int("count", 1) });
                    def.Waves.Add(wave);
                }
                AddUnique(Areas, def.Id, def, a.Path);
            }
        }

        void LoadCyberware(JsonObject root)
        {
            foreach (var s in root.Objects("slots"))
            {
                AddCyberSlot(new CyberSlotDef { Id = s.String("id"), Name = s.String("name") }, s.Path);
            }
            foreach (var c in root.Objects("cyberware"))
            {
                var def = new CyberwareDef
                {
                    Id = c.String("id"),
                    Name = c.String("name"),
                    Slot = c.String("slot"),
                    Price = c.Int("price", 0),
                    Load = c.Int("load", 0),
                    Description = c.String("description", ""),
                    Starter = c.Bool("starter", false),
                };
                foreach (var pair in c.FloatMap("stats")) def.Stats[pair.Key] = pair.Value;
                if (c.Has("gimmick"))
                {
                    var g = c.Object("gimmick");
                    def.Gimmick = new GimmickDef { Type = g.String("type") };
                    foreach (var key in g.Keys)
                        if (key != "type") def.Gimmick.Params[key] = g.Float(key);
                }
                AddCyberware(def, c.Path);
            }
        }

        static void AddUnique<T>(Dictionary<string, T> dict, string id, T value, string path)
        {
            if (dict.ContainsKey(id)) throw new FormatException($"{path}: 중복된 id '{id}'");
            dict[id] = value;
        }
    }
}
