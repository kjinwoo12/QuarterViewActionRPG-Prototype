using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>주인공 기본 능력치·콤보·회피·인공 심장·파쿠르 수치. Assets/Gil/Data/Player.asset 하나만 둔다.</summary>
    [CreateAssetMenu(menuName = "Gil/Data/Player", fileName = "Player")]
    public sealed class PlayerData : ScriptableObject
    {
        [SerializeField] PlayerDef _player = new PlayerDef();

        public PlayerDef Def => _player;

        internal void Set(PlayerDef def) => _player = def;
    }
}
