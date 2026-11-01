using System;
using System.Collections.Generic;
using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>
    /// 게임 데이터 목록. Assets/Gil/Data/GameData.asset 하나를 시스템 씬(Systems) GameRoot의 Data에 연결한다.
    /// 플레이어·적·아이템·상점·의체는 데이터 에셋으로, 대화·퀘스트·NPC·지역은 JSON(TextAsset)으로 들고 있다가
    /// <see cref="Build"/>에서 <see cref="GameDatabase"/>로 묶는다. 새 적·아이템 에셋을 만들면 여기 목록에 넣는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Gil/Data/Game Data", fileName = "GameData")]
    public sealed class GameData : ScriptableObject
    {
        public const string AssetPath = "Assets/Gil/Data/GameData.asset";

        [SerializeField] PlayerData _player;
        [SerializeField] List<EnemyData> _enemies = new List<EnemyData>();
        [SerializeField] List<ItemData> _items = new List<ItemData>();
        [SerializeField] List<ShopData> _shops = new List<ShopData>();
        [SerializeField, Tooltip("표시 순서대로")] List<CyberSlotData> _cyberSlots = new List<CyberSlotData>();
        [SerializeField, Tooltip("표시 순서대로")] List<CyberwareData> _cyberware = new List<CyberwareData>();

        [Header("JSON")]
        [SerializeField] TextAsset _quests;
        [SerializeField] TextAsset _dialogues;
        [SerializeField] TextAsset _npcs;
        [SerializeField] TextAsset _areas;

        /// <summary>
        /// 데이터베이스를 만든다. 빈 칸·중복 id는 FormatException(에셋 이름 포함)으로, 참조 오류는 DataValidator가 잡는다.
        /// textOverride: JSON 파일 이름(quests …)을 받아 대신 쓸 텍스트를 돌려준다(null이면 에셋 그대로). 검증기 테스트용.
        /// </summary>
        public GameDatabase Build(Func<string, string> textOverride = null)
        {
            var db = new GameDatabase();
            db.SetPlayer(Required(_player, "Player").Def);
            foreach (var e in _enemies) db.AddEnemy(Required(e, "Enemies").Def, Path("Enemies", e));
            foreach (var i in _items) db.AddItem(Required(i, "Items").Def, Path("Items", i));
            foreach (var s in _shops) db.AddShop(Required(s, "Shops").ToDef(), Path("Shops", s));
            foreach (var s in _cyberSlots) db.AddCyberSlot(Required(s, "Cyber Slots").Def, Path("Cyber Slots", s));
            foreach (var c in _cyberware) db.AddCyberware(Required(c, "Cyberware").ToDef(), Path("Cyberware", c));

            foreach (var name in GameDatabase.TextFileNames)
                db.LoadJson(name, textOverride?.Invoke(name) ?? JsonText(name));
            return db;
        }

        /// <summary>JSON 데이터 파일(quests, dialogues, npcs, areas)의 텍스트. 없으면 null.</summary>
        public string JsonText(string name) => Text(name)?.text;

        TextAsset Text(string name) => name switch
        {
            "quests" => _quests,
            "dialogues" => _dialogues,
            "npcs" => _npcs,
            "areas" => _areas,
            _ => null,
        };

        T Required<T>(T asset, string list) where T : UnityEngine.Object =>
            asset != null ? asset : throw new FormatException($"{name}: {list} 목록에 빈 칸이 있습니다");

        static string Path(string list, UnityEngine.Object asset) => $"{list}/{asset.name}";

        internal void Set(PlayerData player, List<EnemyData> enemies, List<ItemData> items, List<ShopData> shops,
            List<CyberSlotData> slots, List<CyberwareData> cyberware, TextAsset quests, TextAsset dialogues, TextAsset npcs, TextAsset areas)
        {
            _player = player;
            _enemies = enemies;
            _items = items;
            _shops = shops;
            _cyberSlots = slots;
            _cyberware = cyberware;
            _quests = quests;
            _dialogues = dialogues;
            _npcs = npcs;
            _areas = areas;
        }
    }
}
