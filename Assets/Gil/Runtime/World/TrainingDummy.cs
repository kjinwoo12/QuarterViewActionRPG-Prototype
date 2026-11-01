using System.Linq;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Quests;
using Gil.Runtime.Combat;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>정비소의 연습용 의체 골격. 맞아도 쓰러지지 않고, 콤보·필살기 감각을 익히는 용도.</summary>
    public sealed class TrainingDummy : MonoBehaviour, IHittable
    {
        [SerializeField] Transform _body;
        float _wobble;

        public Vector3 Position => transform.position;
        public float Radius => 0.45f;
        public bool IsAlive => true;
        public int Defense => 0;

        void OnEnable() => Combatants.Register(this);
        void OnDisable() => Combatants.Unregister(this);

        public void ReceiveHit(HitInfo hit)
        {
            Game.Ui.Popups.Damage(transform.position, hit.Amount, hit.IsCritical, false);
            _wobble = 1f;
        }

        void Update()
        {
            _wobble = Mathf.MoveTowards(_wobble, 0, Time.deltaTime * 2.5f);
            if (_body != null) _body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 25f) * 12f * _wobble);
        }
    }
}
