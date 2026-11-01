using System.Linq;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Quests;
using Gil.Runtime.Combat;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>대화할 수 없는 장터 사람들. 가끔 몸을 돌린다(외형은 지역 씬의 View 아래에 배치돼 있고, 이 방향을 따라 돈다).</summary>
    public sealed class Extra : MonoBehaviour
    {
        float _nextTurn;

        void Start() => _nextTurn = Time.time + Random.Range(2f, 6f);

        void Update()
        {
            if (Time.time < _nextTurn) return;
            _nextTurn = Time.time + Random.Range(3f, 7f);
            transform.rotation = Quaternion.Euler(0, Random.Range(0, 4) * 90f + 45f, 0);
        }
    }
}
