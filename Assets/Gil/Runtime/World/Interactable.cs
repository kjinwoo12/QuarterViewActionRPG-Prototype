using System.Collections.Generic;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>상호작용 키(Space)로 상호작용하는 대상(NPC, 문). 플레이어가 가장 가까운 하나를 고른다.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public float Radius = 2.2f;

        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact();

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public static Interactable Nearest(Vector3 position)
        {
            Interactable best = null;
            var bestDistance = float.MaxValue;
            foreach (var i in All)
            {
                if (!i.CanInteract) continue;
                var to = i.transform.position - position;
                to.y = 0;
                var d = to.magnitude;
                if (d <= i.Radius && d < bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
