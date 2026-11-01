using System;
using System.Collections.Generic;
using Gil.Core.Data;

namespace Gil.Core.Items
{
    public sealed class Inventory
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public event Action<string, int> Changed;

        public IReadOnlyDictionary<string, int> Counts => _counts;

        public int Count(string itemId) => _counts.TryGetValue(itemId, out var n) ? n : 0;

        public void Add(string itemId, int amount = 1)
        {
            if (amount <= 0) return;
            _counts[itemId] = Count(itemId) + amount;
            Changed?.Invoke(itemId, _counts[itemId]);
        }

        public bool Remove(string itemId, int amount = 1)
        {
            var have = Count(itemId);
            if (amount <= 0 || have < amount) return false;
            if (have == amount) _counts.Remove(itemId);
            else _counts[itemId] = have - amount;
            Changed?.Invoke(itemId, have - amount);
            return true;
        }

        public void Clear()
        {
            var ids = new List<string>(_counts.Keys);
            _counts.Clear();
            foreach (var id in ids) Changed?.Invoke(id, 0);
        }
    }

    public sealed class Wallet
    {
        public int Coins { get; private set; }
        public event Action<int> Changed;

        public Wallet(int coins = 0) => Coins = Math.Max(0, coins);

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            Changed?.Invoke(Coins);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount;
            Changed?.Invoke(Coins);
            return true;
        }

        public void Set(int coins)
        {
            Coins = Math.Max(0, coins);
            Changed?.Invoke(Coins);
        }
    }

    public enum BuyResult
    {
        Ok,
        NotSold,
        NotEnoughCoins,
    }

    public static class Shop
    {
        public static BuyResult TryBuy(GameDatabase db, string shopId, string itemId, Inventory inventory, Wallet wallet)
        {
            if (!db.Shops.TryGetValue(shopId, out var shop) || !shop.Items.Contains(itemId)
                || !db.Items.TryGetValue(itemId, out var item))
                return BuyResult.NotSold;
            if (!wallet.TrySpend(item.Price)) return BuyResult.NotEnoughCoins;
            inventory.Add(itemId);
            return BuyResult.Ok;
        }
    }
}
