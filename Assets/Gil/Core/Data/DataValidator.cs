using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gil.Core.Dialogue;
using Gil.Core.Util;

namespace Gil.Core.Data
{
    /// <summary>
    /// 기획 데이터 간 참조와 수치 범위를 검사한다. 에디터 메뉴, 플레이 시작 시점, 단위 테스트(CI)에서 같은 검사를 돌린다.
    /// "기획자가 데이터를 고쳤는데 게임에서 조용히 깨지는" 상황을 커밋 전에 잡는 것이 목적이다.
    /// </summary>
    public static class DataValidator
    {
        public static DiagnosticList Validate(GameDatabase db)
        {
            var d = new DiagnosticList();
            ValidatePlayer(db, d);
            ValidateEnemies(db, d);
            ValidateShops(db, d);
            ValidateQuests(db, d);
            ValidateDialogues(db, d);
            ValidateNpcs(db, d);
            ValidateAreas(db, d);
            ValidateCyberware(db, d);
            return d;
        }

        static void ValidatePlayer(GameDatabase db, DiagnosticList d)
        {
            var p = db.Player;
            if (p == null)
            {
                d.Error("player", "플레이어 데이터가 없습니다");
                return;
            }
            if (p.MaxHp <= 0) d.Error("player.maxHp", "0보다 커야 합니다");
            if (p.MoveSpeed <= 0) d.Error("player.moveSpeed", "0보다 커야 합니다");
            if (p.Combo.Count == 0) d.Error("player.combo", "콤보는 최소 1타가 필요합니다");
            for (var i = 0; i < p.Combo.Count; i++)
            {
                var step = p.Combo[i];
                var path = $"player.combo[{i}]";
                if (step.Range <= 0) d.Error(path + ".range", "0보다 커야 합니다");
                if (step.ArcDegrees <= 0 || step.ArcDegrees > 360) d.Error(path + ".arcDegrees", "0~360 사이여야 합니다");
                if (step.Active <= 0) d.Error(path + ".active", "판정 시간은 0보다 커야 합니다");
                if (step.ComboWindow > step.Recovery)
                    d.Warning(path + ".comboWindow", "후딜레이(recovery)보다 길면 입력 유예가 끝나기 전에 동작이 끝납니다");
            }
            var r = p.Rolling;
            if (r == null) d.Error("player.rolling", "구르기 데이터가 없습니다");
            else
            {
                if (r.Duration <= 0) d.Error("player.rolling.duration", "0보다 커야 합니다");
                if (r.Distance <= 0) d.Error("player.rolling.distance", "0보다 커야 합니다");
                if (r.InvulnerableTime > r.Duration)
                    d.Warning("player.rolling.invulnerableTime", "구르기 동작보다 무적 시간이 깁니다");
                if (r.Cooldown < 0) d.Error("player.rolling.cooldown", "0 이상이어야 합니다");
                if (r.StaminaCost < 0) d.Error("player.rolling.staminaCost", "0 이상이어야 합니다");
            }
            var sp = p.Sprint;
            if (sp == null) d.Error("player.sprint", "달리기 데이터가 없습니다");
            else
            {
                if (sp.SpeedMultiplier < 1f || sp.SpeedMultiplier > 3f)
                    d.Error("player.sprint.speedMultiplier", "1~3 사이여야 합니다(1이면 달려도 빨라지지 않습니다)");
                if (sp.StaminaPerSecond < 0) d.Error("player.sprint.staminaPerSecond", "0 이상이어야 합니다");
            }
            var st = p.Stamina;
            if (st == null) d.Error("player.stamina", "스태미나 데이터가 없습니다");
            else
            {
                if (st.Max <= 0) d.Error("player.stamina.max", "0보다 커야 합니다");
                if (st.RegenDelay < 0) d.Error("player.stamina.regenDelay", "0 이상이어야 합니다");
                if (st.RegenPerSecond <= 0) d.Warning("player.stamina.regenPerSecond", "0이면 스태미나가 회복되지 않습니다");
                if (r != null && r.StaminaCost > st.Max)
                    d.Error("player.rolling.staminaCost", "최대 스태미나보다 커서 구를 수 없습니다");
            }
            if (p.Heart.SkillCost > p.Heart.Max)
                d.Error("player.heart.skillCost", "게이지 최대치보다 커서 필살기를 쓸 수 없습니다");
            if (p.Heart.GainPerHit <= 0)
                d.Warning("player.heart.gainPerHit", "0이면 공격으로 게이지가 차지 않습니다");

            var k = p.Parkour;
            if (k.JumpHeight <= 0) d.Error("player.parkour.jumpHeight", "0보다 커야 합니다");
            if (k.VaultMaxHeight <= 0) d.Error("player.parkour.vaultMaxHeight", "0보다 커야 합니다");
            if (k.ClimbMaxHeight < k.VaultMaxHeight)
                d.Error("player.parkour.climbMaxHeight", "넘기 높이(vaultMaxHeight)보다 낮으면 오를 수 있는 구조물이 없습니다");
            if (k.VaultDuration <= 0 || k.ClimbDuration <= 0) d.Error("player.parkour", "넘기/오르기 시간은 0보다 커야 합니다");
            if (k.Gravity <= 0) d.Error("player.parkour.gravity", "0보다 커야 합니다");
            if (k.PlungeMinHeight <= k.MeleeHeightTolerance)
                d.Warning("player.parkour.plungeMinHeight", "근접 허용 높이보다 낮으면 내려찍기가 일반 공격과 겹칩니다");
        }

        static void ValidateCyberware(GameDatabase db, DiagnosticList d)
        {
            if (db.CyberSlots.Count == 0) d.Error("cyberware.slots", "의체 슬롯이 하나도 없습니다");
            var starterCapacity = 0f;
            var starterLoad = 0;
            foreach (var c in db.CyberwareList)
            {
                var path = $"cyberware.{c.Id}";
                if (db.CyberSlots.TrueForAll(s => s.Id != c.Slot)) d.Error(path + ".slot", $"없는 슬롯 '{c.Slot}'");
                if (c.Price < 0) d.Error(path + ".price", "0 이상이어야 합니다");
                if (c.Load < 0) d.Error(path + ".load", "0 이상이어야 합니다");
                foreach (var key in c.Stats.Keys)
                    if (!Cyber.StatKeys.All.ContainsKey(key)) d.Error(path + ".stats." + key, $"알 수 없는 능력치 '{key}'");
                if (c.Gimmick != null)
                {
                    if (!Cyber.Gimmicks.Required.TryGetValue(c.Gimmick.Type, out var required))
                        d.Error(path + ".gimmick.type", $"알 수 없는 기믹 '{c.Gimmick.Type}'");
                    else
                        foreach (var p in required)
                            if (!c.Gimmick.Params.ContainsKey(p)) d.Error(path + ".gimmick." + p, "필수 파라미터가 없습니다");
                }
                if (c.Starter)
                {
                    starterLoad += c.Load;
                    starterCapacity += c.Stats.TryGetValue("capacity", out var cap) ? cap : 0f;
                }
            }
            if (starterCapacity <= 0) d.Error("cyberware", "기본 장착(starter) 의체가 용량(capacity)을 제공해야 합니다");
            if (starterLoad > starterCapacity) d.Error("cyberware", "기본 장착 의체의 부하가 용량을 넘습니다");
            foreach (var slot in db.CyberSlots)
                if (db.CyberwareList.FindAll(c => c.Starter && c.Slot == slot.Id).Count > 1)
                    d.Error("cyberware", $"슬롯 '{slot.Id}'에 기본 장착 의체가 둘 이상입니다");

            // 기본 용량으로 장착할 수 없는 의체는 심장 업그레이드가 있어야만 쓸 수 있다 — 의도인지 확인하라는 경고.
            var maxCapacity = db.CyberwareList.Where(c => c.Slot == "heart")
                .Select(c => c.Stats.TryGetValue("capacity", out var cap) ? cap : 0f).DefaultIfEmpty(starterCapacity).Max();
            foreach (var c in db.CyberwareList)
                if (c.Load > maxCapacity)
                    d.Warning($"cyberware.{c.Id}.load", "어떤 심장으로도 장착할 수 없는 부하입니다");
        }

        static void ValidateEnemies(GameDatabase db, DiagnosticList d)
        {
            foreach (var e in db.Enemies.Values)
            {
                var path = $"enemies.{e.Id}";
                if (e.MaxHp <= 0) d.Error(path + ".maxHp", "0보다 커야 합니다");
                if (e.AttackRange > e.AggroRange) d.Warning(path + ".attackRange", "인식 거리보다 길어 플레이어를 보기 전에 공격합니다");
                if (e.Windup < 0.2f) d.Warning(path + ".windup", "예고 시간이 0.2초 미만이면 사실상 회피할 수 없습니다");
                if (e.Kind == EnemyKind.Ranged)
                {
                    if (e.ProjectileSpeed <= 0) d.Error(path + ".projectileSpeed", "원거리형은 투사체 속도가 필요합니다");
                    if (e.PreferredRange <= 0 || e.PreferredRange > e.AttackRange)
                        d.Warning(path + ".preferredRange", "원거리형의 유지 거리는 0보다 크고 공격 거리 이하여야 자연스럽습니다");
                }
                if (!IsColor(e.Color)) d.Error(path + ".color", $"#rrggbb 형식이 아닙니다 ('{e.Color}')");
            }
        }

        static void ValidateShops(GameDatabase db, DiagnosticList d)
        {
            foreach (var shop in db.Shops.Values)
            {
                foreach (var item in shop.Items)
                    if (!db.Items.ContainsKey(item)) d.Error($"shops.{shop.Id}.items", $"없는 아이템 '{item}'");
            }
            foreach (var item in db.Items.Values)
                if (item.Price < 0) d.Error($"items.{item.Id}.price", "음수일 수 없습니다");
        }

        static void ValidateQuests(GameDatabase db, DiagnosticList d)
        {
            foreach (var q in db.Quests.Values)
            {
                var path = $"quests.{q.Id}";
                if (q.Objectives.Count == 0) d.Error(path + ".objectives", "목표가 하나 이상 필요합니다");
                if (!string.IsNullOrEmpty(q.TurnInNpc) && !db.Npcs.ContainsKey(q.TurnInNpc))
                    d.Error(path + ".turnInNpc", $"없는 NPC '{q.TurnInNpc}'");
                foreach (var r in q.RewardItems)
                    if (!db.Items.ContainsKey(r.ItemId)) d.Error(path + ".rewardItems", $"없는 아이템 '{r.ItemId}'");

                for (var i = 0; i < q.Objectives.Count; i++)
                {
                    var o = q.Objectives[i];
                    var opath = $"{path}.objectives[{i}]";
                    if (o.Count <= 0) d.Error(opath + ".count", "1 이상이어야 합니다");
                    switch (o.Type)
                    {
                        case ObjectiveType.Talk:
                            if (!db.Npcs.ContainsKey(o.Target)) d.Error(opath + ".target", $"없는 NPC '{o.Target}'");
                            break;
                        case ObjectiveType.Reach:
                            if (!db.Areas.ContainsKey(o.Target)) d.Error(opath + ".target", $"없는 지역 '{o.Target}'");
                            break;
                        case ObjectiveType.Kill:
                            if (!db.Enemies.ContainsKey(o.Target))
                            {
                                d.Error(opath + ".target", $"없는 적 '{o.Target}'");
                                break;
                            }
                            // 처치 목표를 실제로 달성할 수 있는지(웨이브에 충분한 수가 나오는지) 확인한다.
                            var spawned = db.Areas.Values.SelectMany(a => a.Waves).SelectMany(w => w.Entries)
                                .Where(e => e.EnemyId == o.Target).Sum(e => e.Count);
                            if (spawned < o.Count)
                                d.Error(opath + ".count", $"'{o.Target}'은(는) 모든 지역을 합쳐 {spawned}마리만 등장해 {o.Count}마리를 잡을 수 없습니다");
                            break;
                    }
                }
            }
        }

        static void ValidateDialogues(GameDatabase db, DiagnosticList d)
        {
            foreach (var dlg in db.Dialogues.Values)
            {
                var path = $"dialogues.{dlg.Id}";
                if (!dlg.Nodes.ContainsKey(dlg.Start))
                {
                    d.Error(path + ".start", $"없는 노드 '{dlg.Start}'");
                    continue;
                }

                foreach (var node in dlg.Nodes.Values)
                {
                    var npath = $"{path}.{node.Id}";
                    if (string.IsNullOrEmpty(node.Text) && node.Branches.Count == 0)
                        d.Error(npath, "대사(text)나 분기(branches) 중 하나는 있어야 합니다");
                    CheckNext(dlg, node.Next, npath + ".next", d);
                    foreach (var a in node.Actions) CheckCommand(db, a, DialogueCommands.Actions, npath + ".actions", d);
                    for (var i = 0; i < node.Choices.Count; i++)
                    {
                        var c = node.Choices[i];
                        var cpath = $"{npath}.choices[{i}]";
                        CheckNext(dlg, c.Next, cpath + ".next", d);
                        if (!string.IsNullOrEmpty(c.Condition))
                            CheckCommand(db, c.Condition, DialogueCommands.Conditions, cpath + ".condition", d);
                        foreach (var a in c.Actions) CheckCommand(db, a, DialogueCommands.Actions, cpath + ".actions", d);
                    }
                    for (var i = 0; i < node.Branches.Count; i++)
                    {
                        var b = node.Branches[i];
                        var bpath = $"{npath}.branches[{i}]";
                        CheckNext(dlg, b.Next, bpath + ".next", d);
                        if (!string.IsNullOrEmpty(b.Condition))
                            CheckCommand(db, b.Condition, DialogueCommands.Conditions, bpath + ".condition", d);
                    }
                    if (node.IsBranchOnly && node.Branches.All(b => !string.IsNullOrEmpty(b.Condition)))
                        d.Warning(npath + ".branches", "모든 분기에 조건이 있어 아무 조건도 맞지 않으면 대화가 끝납니다. 마지막 분기는 조건 없이 두는 것을 권장합니다");
                }

                foreach (var unreachable in dlg.Nodes.Keys.Except(Reachable(dlg)))
                    d.Warning($"{path}.{unreachable}", "어디에서도 이어지지 않는 노드입니다");
            }
        }

        static void ValidateNpcs(GameDatabase db, DiagnosticList d)
        {
            foreach (var npc in db.Npcs.Values)
            {
                if (!db.Dialogues.ContainsKey(npc.Dialogue)) d.Error($"npcs.{npc.Id}.dialogue", $"없는 대화 '{npc.Dialogue}'");
                if (!IsColor(npc.Color)) d.Error($"npcs.{npc.Id}.color", $"#rrggbb 형식이 아닙니다 ('{npc.Color}')");
            }
        }

        static void ValidateAreas(GameDatabase db, DiagnosticList d)
        {
            foreach (var area in db.Areas.Values)
            {
                for (var w = 0; w < area.Waves.Count; w++)
                {
                    foreach (var e in area.Waves[w].Entries)
                    {
                        if (!db.Enemies.ContainsKey(e.EnemyId))
                            d.Error($"areas.{area.Id}.waves[{w}]", $"없는 적 '{e.EnemyId}'");
                        if (e.Count <= 0)
                            d.Error($"areas.{area.Id}.waves[{w}]", $"'{e.EnemyId}' 수량은 1 이상이어야 합니다");
                    }
                }
            }
        }

        static void CheckNext(DialogueDef dlg, string next, string path, DiagnosticList d)
        {
            if (!string.IsNullOrEmpty(next) && !dlg.Nodes.ContainsKey(next))
                d.Error(path, $"없는 노드 '{next}'");
        }

        static void CheckCommand(GameDatabase db, string text, IReadOnlyDictionary<string, ArgKind[]> table,
            string path, DiagnosticList d)
        {
            var command = DialogueCommands.Parse(text);
            if (!table.TryGetValue(command.Verb, out var kinds))
            {
                d.Error(path, $"알 수 없는 명령 '{command.Verb}' (사용 가능: {string.Join(", ", table.Keys)})");
                return;
            }
            if (command.Args.Length != kinds.Length)
            {
                d.Error(path, $"'{command.Verb}'의 인자는 {kinds.Length}개여야 합니다 ('{text}')");
                return;
            }
            for (var i = 0; i < kinds.Length; i++)
            {
                var arg = command.Args[i];
                bool ok;
                switch (kinds[i])
                {
                    case ArgKind.Quest: ok = db.Quests.ContainsKey(arg); break;
                    case ArgKind.Shop: ok = db.Shops.ContainsKey(arg); break;
                    case ArgKind.Item: ok = db.Items.ContainsKey(arg); break;
                    case ArgKind.Area: ok = db.Areas.ContainsKey(arg); break;
                    case ArgKind.Cyberware: ok = db.Cyberware.ContainsKey(arg); break;
                    case ArgKind.Count: ok = int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0; break;
                    default: ok = !string.IsNullOrEmpty(arg); break;
                }
                if (!ok) d.Error(path, $"'{text}'의 {i + 1}번째 인자 '{arg}'가 올바르지 않습니다 ({kinds[i]})");
            }
        }

        static HashSet<string> Reachable(DialogueDef dlg)
        {
            var seen = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(dlg.Start);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (string.IsNullOrEmpty(id) || !seen.Add(id) || !dlg.Nodes.TryGetValue(id, out var node)) continue;
                stack.Push(node.Next);
                foreach (var c in node.Choices) stack.Push(c.Next);
                foreach (var b in node.Branches) stack.Push(b.Next);
            }
            return seen;
        }

        static bool IsColor(string s) =>
            s != null && s.Length == 7 && s[0] == '#' &&
            int.TryParse(s.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }
}
