using Gil.Core.Data;
using UnityEngine;

namespace Gil.Runtime.Data
{
    /// <summary>
    /// 적 한 종류의 수치. Assets/Gil/Data/Enemies/{id}.asset. Id는 적 프리팹(EnemyController)의 Enemy Id,
    /// 퀘스트 처치 목표(quests.json)·웨이브(areas.json)에서 쓰는 이름과 같아야 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Gil/Data/Enemy", fileName = "Enemy")]
    public sealed class EnemyData : ScriptableObject
    {
        [SerializeField] EnemyDef _enemy = new EnemyDef { Color = "#ff4060", StaggerTime = 0.3f };

        public EnemyDef Def => _enemy;

        internal void Set(EnemyDef def) => _enemy = def;
    }
}
