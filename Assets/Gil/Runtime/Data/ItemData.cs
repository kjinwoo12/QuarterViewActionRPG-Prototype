using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>소모품 하나. Assets/Gil/Data/Items/{id}.asset. Id는 대화 명령·퀘스트 보상·세이브에서 쓰는 이름이다.</summary>
    [CreateAssetMenu(menuName = "Gil/Data/Item", fileName = "Item")]
    public sealed class ItemData : ScriptableObject
    {
        [SerializeField] ItemDef _item = new ItemDef();

        public ItemDef Def => _item;

        internal void Set(ItemDef def) => _item = def;
    }
}
