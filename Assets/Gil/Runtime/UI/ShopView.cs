using System.Collections.Generic;
using Gil.Core.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>상점 창. 구매 규칙(가격·잔액 확인)은 Core의 Shop.TryBuy, 여기서는 목록과 결과 표시만.</summary>
    public sealed class ShopView : ModalView
    {
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _coins;
        [SerializeField] TMP_Text _description;
        [SerializeField] TMP_Text _result;
        [SerializeField] VerticalMenu _menu;
        string _shopId;
        readonly List<string> _itemIds = new List<string>();

        public void Open(string shopId)
        {
            var db = Game.Session.Db;
            if (!db.Shops.TryGetValue(shopId, out var shop))
            {
                Debug.LogError($"[Shop] 없는 상점 '{shopId}'");
                return;
            }
            _shopId = shopId;
            _title.text = shop.Name;
            _result.text = "";
            _itemIds.Clear();
            _menu.Clear();
            foreach (var id in shop.Items)
            {
                var itemId = id;
                _itemIds.Add(itemId);
                _menu.Add("", () => Buy(itemId));
            }
            _menu.Add("나가기  (X / Esc)", Close);
            Refresh();
            Show();
        }

        void Buy(string itemId)
        {
            var session = Game.Session;
            var item = session.Db.Items[itemId];
            switch (session.Buy(_shopId, itemId))
            {
                case BuyResult.Ok:
                    _result.text = $"<color=#9df5b0>{item.Name}을(를) 샀다.</color>";
                    break;
                case BuyResult.NotEnoughCoins:
                    _result.text = "<color=#ff8080>엽전이 모자란다.</color>";
                    break;
                default:
                    _result.text = "<color=#ff8080>지금은 팔지 않는 물건이다.</color>";
                    break;
            }
            Refresh();
        }

        void Refresh()
        {
            var session = Game.Session;
            _coins.text = $"◆ {Money.Format(session.Wallet.Coins)}";
            for (var i = 0; i < _itemIds.Count; i++)
            {
                var item = session.Db.Items[_itemIds[i]];
                _menu.SetText(i, $"{item.Name}   <color=#ffd27a>{Money.Format(item.Price)}</color>   <color=#9aa0a8>보유 {session.Inventory.Count(item.Id)}</color>");
            }
        }

        void Update()
        {
            if (!InputReady) return;
            var input = Game.Input;
            if (input.PausePressed || input.CancelPressed)
            {
                Close();
                return;
            }
            _menu.HandleInput(input);
            if (!IsOpen) return;
            var selected = _menu.Selected;
            _description.text = selected < _itemIds.Count ? Game.Session.Db.Items[_itemIds[selected]].Description : "";
        }

        void Close() => Hide();
    }
}
