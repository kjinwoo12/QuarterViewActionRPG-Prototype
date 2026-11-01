using Gil.Runtime.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gil.Runtime.Combat
{
    /// <summary>
    /// 적 투사체(드론의 전격탄). 지면 좌표에서 일직선으로 날아가며 플레이어와의 3D 거리로 적중을 판정한다.
    /// 발사 순간 플레이어 높이를 겨누므로 컨테이너 위로 올라가도 안전하지 않다 — 대신 옆으로 피하면 된다.
    /// 프리팹(Assets/Gil/Prefabs/Fx/Projectile)은 논리 루트 + 화면 쪽 외형(IsoView 자식)으로 돼 있고, 쏘는 적의 인스펙터에 연결한다.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _core;
        [SerializeField] SpriteRenderer _halo;
        [SerializeField] Light2D _light;
        [SerializeField] float _lifetime = 3f;
        [SerializeField] float _hitRadius = 0.5f;
        Vector3 _velocity;
        int _damage;

        public static void Fire(Projectile prefab, Vector3 origin, Vector3 target, float speed, int damage, Color color)
        {
            if (prefab == null || Iso.LogicRoot == null) return;
            var p = Instantiate(prefab, origin, Quaternion.identity, Iso.LogicRoot);
            var direction = target - origin;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            p._velocity = direction.normalized * speed;
            p._damage = damage;
            p._core.color = Color.Lerp(color, Color.white, 0.35f);
            p._halo.color = new Color(color.r, color.g, color.b, p._halo.color.a);
            p._light.color = color;
        }

        void Update()
        {
            transform.position += _velocity * Time.deltaTime;
            _lifetime -= Time.deltaTime;

            var player = Combatants.Player;
            if (player != null && player.IsAlive)
            {
                var to = player.transform.position + Vector3.up * 0.7f - transform.position;
                if (to.magnitude <= _hitRadius + 0.2f)
                {
                    player.ReceiveEnemyHit(_damage, transform.position);
                    Vfx.Impact(transform.position - Vector3.up * 0.6f, Vfx.Danger, 0.5f);
                    Destroy(gameObject);
                    return;
                }
            }

            // 벽·장애물에 닿으면 사라진다.
            if (_lifetime <= 0 || Physics.CheckSphere(transform.position, 0.12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_lifetime > 0) Vfx.Impact(transform.position - Vector3.up * 0.6f, Vfx.Shock, 0.35f);
                Destroy(gameObject);
            }
        }
    }
}
