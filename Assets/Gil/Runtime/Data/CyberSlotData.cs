using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>의체 슬롯 하나(심장·신경계·팔…). Assets/Gil/Data/Cyberware/Slots/{id}.asset. 표시 순서는 GameData의 목록 순서.</summary>
    [CreateAssetMenu(menuName = "Gil/Data/Cyber Slot", fileName = "CyberSlot")]
    public sealed class CyberSlotData : ScriptableObject
    {
        [SerializeField] CyberSlotDef _slot = new CyberSlotDef();

        public CyberSlotDef Def => _slot;

        internal void Set(CyberSlotDef def) => _slot = def;
    }
}
