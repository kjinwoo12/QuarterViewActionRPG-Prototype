using System.Collections.Generic;
using System.Linq;
using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>상점 하나와 파는 물건(아이템 에셋 참조). Assets/Gil/Data/Shops/{id}.asset. Id는 대화 명령 open_shop:{id}에서 쓴다.</summary>
    [CreateAssetMenu(menuName = "Gil/Data/Shop", fileName = "Shop")]
    public sealed class ShopData : ScriptableObject
    {
        [SerializeField] string _id;
        [SerializeField] string _displayName;
        [SerializeField] List<ItemData> _items = new List<ItemData>();

        public ShopDef ToDef()
        {
            var def = new ShopDef { Id = _id, Name = _displayName };
            // 빈 칸은 빈 id로 남겨 검증기가 「없는 아이템」으로 잡게 한다.
            def.Items.AddRange(_items.Select(i => i != null ? i.Def.Id : ""));
            return def;
        }

        internal void Set(string id, string displayName, IEnumerable<ItemData> items)
        {
            _id = id;
            _displayName = displayName;
            _items = items.ToList();
        }
    }
}
