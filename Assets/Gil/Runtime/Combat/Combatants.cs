using System.Collections.Generic;
using UnityEngine;

namespace Gil.Runtime.Combat
{
    public struct HitInfo
    {
        public int Amount;
        public bool IsCritical;
        public Vector3 Source;
        public float Knockback;
        public bool IsSkill;
        /// <summary>0보다 크면 이 시간(초) 동안 대상을 기절시킨다(충격 착지 의체 등).</summary>
        public float Stun;
        /// <summary>연쇄 전격 등 2차 피해. 다시 연쇄를 일으키지 않는다.</summary>
        public bool IsSecondary;
    }

    /// <summary>플레이어 공격을 받을 수 있는 대상(적, 연습용 골격).</summary>
    public interface IHittable
    {
        /// <summary>지면 좌표(y = 서 있는 높이).</summary>
        Vector3 Position { get; }
        float Radius { get; }
        bool IsAlive { get; }
        int Defense { get; }
        void ReceiveHit(HitInfo hit);
    }

    /// <summary>
    /// 전투 대상 등록부. 물리 레이어/콜라이더 설정 없이 거리·각도로 판정해서
    /// 씬 설정 실수로 판정이 사라지는 일을 막고, 판정 규칙을 코드 한 곳에 모은다.
    /// </summary>
    public static class Combatants
    {
        public static readonly List<IHittable> Targets = new List<IHittable>();
        public static Player.PlayerController Player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Targets.Clear();
            Player = null;
        }

        public static void Register(IHittable target)
        {
            if (!Targets.Contains(target)) Targets.Add(target);
        }

        public static void Unregister(IHittable target) => Targets.Remove(target);

        /// <summary>
        /// origin에서 forward 기준 arc(도) 부채꼴, range 안의 대상. 대상 반지름만큼 판정을 넓힌다.
        /// heightTolerance: 높이 차가 이보다 크면 제외(컨테이너 위와 아래는 주먹이 닿지 않는다).
        /// </summary>
        public static List<IHittable> InSector(Vector3 origin, Vector3 forward, float range, float arcDegrees,
            float heightTolerance = float.PositiveInfinity)
        {
            var result = new List<IHittable>();
            forward.y = 0;
            forward.Normalize();
            foreach (var t in Targets)
            {
                if (!t.IsAlive) continue;
                var to = t.Position - origin;
                if (Mathf.Abs(to.y) > heightTolerance) continue;
                to.y = 0;
                var distance = to.magnitude;
                if (distance > range + t.Radius) continue;
                if (distance > 0.01f && arcDegrees < 360f)
                {
                    // 가까이 붙은 대상은 각도와 무관하게 맞는다(몸에 붙은 적이 안 맞는 불쾌감 방지).
                    var angle = Vector3.Angle(forward, to);
                    if (distance > t.Radius + 0.4f && angle > arcDegrees * 0.5f) continue;
                }
                result.Add(t);
            }
            return result;
        }

        public static IHittable Nearest(Vector3 origin, float maxDistance)
        {
            IHittable best = null;
            var bestDistance = maxDistance;
            foreach (var t in Targets)
            {
                if (!t.IsAlive) continue;
                var d = Vector3.Distance(origin, t.Position);
                if (d < bestDistance)
                {
                    best = t;
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
