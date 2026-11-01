using System;
using System.Collections.Generic;
using System.Linq;
using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>
    /// 의체 하나. Assets/Gil/Data/Cyberware/{id}.asset. 능력치 키와 기믹 종류·파라미터는 docs/DATA_GUIDE.md 참고
    /// (검증기가 모르는 키를 잡아 준다).
    /// </summary>
    [CreateAssetMenu(menuName = "Gil/Data/Cyberware", fileName = "Cyberware")]
    public sealed class CyberwareData : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public float Value;
        }

        [SerializeField] string _id;
        [SerializeField] string _displayName;
        [SerializeField] CyberSlotData _slot;
        [SerializeField, Min(0), Tooltip("시술비(푼). 1냥 = 10전 = 100푼")] int _price;
        [SerializeField, Min(0), Tooltip("신체 부하. 장착한 의체 부하의 합은 심장 용량(capacity)을 넘을 수 없다")] int _load;
        [SerializeField, TextArea(2, 4)] string _description;
        [SerializeField, Tooltip("새 게임 시작 시 기본 장착")] bool _starter;
        [SerializeField, Tooltip("능력치 보정(capacity, maxHp, moveSpeed …)")] List<Entry> _stats = new List<Entry>();
        [SerializeField, Tooltip("고유 기믹 종류. 없으면 비워 둔다")] string _gimmick;
        [SerializeField] List<Entry> _gimmickParams = new List<Entry>();

        public CyberwareDef ToDef()
        {
            var def = new CyberwareDef
            {
                Id = _id,
                Name = _displayName,
                Slot = _slot != null ? _slot.Def.Id : "",
                Price = _price,
                Load = _load,
                Description = _description ?? "",
                Starter = _starter,
            };
            foreach (var s in _stats) def.Stats[s.Key] = s.Value;
            if (!string.IsNullOrEmpty(_gimmick))
            {
                def.Gimmick = new GimmickDef { Type = _gimmick };
                foreach (var p in _gimmickParams) def.Gimmick.Params[p.Key] = p.Value;
            }
            return def;
        }

        internal void Set(CyberwareDef def, CyberSlotData slot)
        {
            _id = def.Id;
            _displayName = def.Name;
            _slot = slot;
            _price = def.Price;
            _load = def.Load;
            _description = def.Description;
            _starter = def.Starter;
            _stats = def.Stats.Select(p => new Entry { Key = p.Key, Value = p.Value }).ToList();
            _gimmick = def.Gimmick?.Type ?? "";
            _gimmickParams = def.Gimmick?.Params.Select(p => new Entry { Key = p.Key, Value = p.Value }).ToList() ?? new List<Entry>();
        }
    }
}
