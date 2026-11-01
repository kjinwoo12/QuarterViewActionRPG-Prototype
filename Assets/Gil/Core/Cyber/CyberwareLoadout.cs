using System;
using System.Collections.Generic;
using System.Linq;
using Gil.Core.Data;

namespace Gil.Core.Cyber
{
    public enum InstallResult
    {
        Ok,
        AlreadyInstalled,
        NotEnoughCoins,
        OverCapacity,
        Unknown,
    }

    /// <summary>
    /// 부위별 장착 의체. 슬롯마다 하나씩, 부하 합은 심장 용량 이하.
    /// 새 의체를 넣으면 그 슬롯의 기존 의체는 빠진다(리퍼닥이 회수).
    /// </summary>
    public sealed class CyberwareLoadout
    {
        readonly GameDatabase _db;
        readonly Dictionary<string, CyberwareDef> _installed = new Dictionary<string, CyberwareDef>();

        public event Action Changed;

        public CyberwareLoadout(GameDatabase db) => _db = db;

        public IEnumerable<CyberwareDef> Installed => _db.CyberSlots
            .Where(s => _installed.ContainsKey(s.Id)).Select(s => _installed[s.Id]);

        public CyberwareDef InSlot(string slot) => _installed.TryGetValue(slot, out var c) ? c : null;
        public bool IsInstalled(string id) => _installed.Values.Any(c => c.Id == id);

        public int Load => _installed.Values.Sum(c => c.Load);
        public int Capacity => (int)Math.Round(_installed.Values.Sum(c => c.Stats.TryGetValue("capacity", out var v) ? v : 0f));

        /// <summary>이 의체로 바꿔 끼웠을 때의 부하/용량.</summary>
        public (int load, int capacity) Preview(CyberwareDef def)
        {
            var after = new Dictionary<string, CyberwareDef>(_installed) { [def.Slot] = def };
            return (after.Values.Sum(c => c.Load),
                (int)Math.Round(after.Values.Sum(c => c.Stats.TryGetValue("capacity", out var v) ? v : 0f)));
        }

        /// <summary>돈 계산 없이 장착 가능 여부만 본다.</summary>
        public InstallResult Check(CyberwareDef def)
        {
            if (def == null || _db.CyberSlots.All(s => s.Id != def.Slot)) return InstallResult.Unknown;
            if (IsInstalled(def.Id)) return InstallResult.AlreadyInstalled;
            var (load, capacity) = Preview(def);
            return load > capacity ? InstallResult.OverCapacity : InstallResult.Ok;
        }

        public InstallResult Install(CyberwareDef def)
        {
            var result = Check(def);
            if (result != InstallResult.Ok) return result;
            _installed[def.Slot] = def;
            Changed?.Invoke();
            return InstallResult.Ok;
        }

        /// <summary>새 게임: 기본 장착 의체만 남긴다.</summary>
        public void ResetToStarters()
        {
            _installed.Clear();
            foreach (var c in _db.CyberwareList.Where(c => c.Starter)) _installed[c.Slot] = c;
            Changed?.Invoke();
        }

        /// <summary>세이브에서 복원. 없어진 의체 id는 건너뛰고, 빈 슬롯은 기본 의체로 채운다.</summary>
        public void Restore(IEnumerable<string> ids)
        {
            _installed.Clear();
            foreach (var c in _db.CyberwareList.Where(c => c.Starter)) _installed[c.Slot] = c;
            foreach (var id in ids)
                if (_db.Cyberware.TryGetValue(id, out var c)) _installed[c.Slot] = c;
            Changed?.Invoke();
        }

        /// <summary>장착한 의체 중 해당 기믹을 가진 것(없으면 null).</summary>
        public GimmickDef Gimmick(string type) =>
            _installed.Values.Select(c => c.Gimmick).FirstOrDefault(g => g != null && g.Type == type);
    }
}
