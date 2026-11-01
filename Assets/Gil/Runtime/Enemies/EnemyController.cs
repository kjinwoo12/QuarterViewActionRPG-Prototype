using System;
using Gil.Core.AI;
using Gil.Core.Combat;
using Gil.Core.Data;
using Gil.Core.Items;
using Gil.Runtime.Combat;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.Enemies
{
    /// <summary>
    /// 적 한 마리(황실 경비 로봇 계열). 판단은 Core의 EnemyBrain이 하고, 여기서는 이동·예고 표시·공격 판정·피격 연출을 붙인다.
    /// 근접형/원거리형 차이는 데이터(EnemyDef.Kind)와 공격 실행부 한 곳에만 있다.
    /// 이 오브젝트는 지면 좌표에서 움직이고, 화면 쪽 외형(IsoView)이 따라다닌다.
    /// </summary>
    public sealed class EnemyController : MonoBehaviour, IHittable
    {
        /// <summary>enemies.json의 id. 프리팹(Assets/Gil/Prefabs/Enemy_{id})마다 하나씩 정해 두고, 수치는 실행할 때 데이터에서 읽는다.</summary>
        [SerializeField] string _enemyId;

        // 외형·그림자·예고 범위는 프리팹 안에 배치돼 있다. 이 오브젝트는 바라보는 방향으로 회전하므로,
        // 화면 쪽 오브젝트는 깨어날 때 지역의 표현 루트로 옮겨 회전을 물려받지 않게 한다.
        [SerializeField] IsoView _view;
        [SerializeField] GroundShadow _shadow;
        [SerializeField] Telegraph _telegraph;
        [SerializeField, Tooltip("원거리형만: 쏘는 투사체 프리팹")] Projectile _projectile;
        [SerializeField, Tooltip("쓰러진 뒤 깜빡이며 사라지기까지(초)")] float _deathDuration = 0.6f;

        // 장애물 검사용 버퍼(할당 없이 재사용).
        static readonly Collider[] Overlaps = new Collider[16];

        EnemyDef _def;
        EnemyBrain _brain;
        Health _health;
        ICharacterAnimator _animator;
        Vector3 _knockback;
        Vector3 _aimDirection;
        float _deathTime = -1f;
        float _spawnTime;
        Rect _walkable;

        public string EnemyId => _enemyId;
        public EnemyDef Def => _def;
        public Health Health => _health;
        public EnemyState State => _brain.State;
        public Vector3 Position => transform.position;
        public bool IsHeavy => _def.Id == "riot_bot";
        public float Radius => _def.Kind == EnemyKind.Ranged ? 0.45f : IsHeavy ? 0.7f : 0.5f;
        public bool IsAlive => _health != null && !_health.IsDead;
        public int Defense => _def.Defense;
        /// <summary>체력바를 띄울 높이(지면 기준 m).</summary>
        public float BarHeight => _def.Kind == EnemyKind.Ranged ? 2.2f : IsHeavy ? 3.2f : 2.3f;

        public event Action<EnemyController> Killed;

        /// <summary>프리팹을 복제해 소환한다. 외형·판단은 데이터(def)로 채운다. walkable: 지역의 걷기 영역(지면 x, z).</summary>
        public static EnemyController Spawn(EnemyController prefab, EnemyDef def, Vector3 position, Transform parent, Rect walkable)
        {
            var enemy = Instantiate(prefab, position, Quaternion.LookRotation(new Vector3(-1, 0, -1)), parent);
            enemy.name = $"Enemy_{def.Id}";
            enemy.Init(def, walkable);
            return enemy;
        }

        void Init(EnemyDef def, Rect walkable)
        {
            _def = def;
            _walkable = walkable;
            _health = new Health(def.MaxHp);
            _brain = new EnemyBrain(def);
            _brain.AttackExecuted += ExecuteAttack;
            _brain.StateChanged += OnStateChanged;

            var arc = def.Kind == EnemyKind.Ranged ? 10f : 360f;
            _telegraph.Init(def.AttackRange, arc);

            Vfx.Ring(transform.position, 1.6f, Colors.Hex(def.Color), 0.4f);
            _spawnTime = Time.time;
            Combatants.Register(this);
        }

        void Awake()
        {
            _animator = _view.GetComponent<ICharacterAnimator>();
            var viewRoot = Iso.ViewRoot != null ? Iso.ViewRoot : transform.parent;
            _view.transform.SetParent(viewRoot, true);
            _shadow.transform.SetParent(viewRoot, true);
            _telegraph.transform.SetParent(viewRoot, false);
            _view.Sync();
        }

        void OnDestroy()
        {
            Combatants.Unregister(this);
            if (_telegraph != null) Destroy(_telegraph.gameObject);
            if (_view != null) Destroy(_view.gameObject);
            if (_shadow != null) Destroy(_shadow.gameObject);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (_deathTime >= 0)
            {
                UpdateDeath(dt);
                return;
            }

            var player = Combatants.Player;
            var playerAlive = player != null && player.IsAlive && !Game.GameplayLocked;
            var toPlayer = player != null ? player.transform.position - transform.position : Vector3.zero;
            toPlayer.y = 0;

            // 등장 직후 0.6초는 가만히 있는다(소환 연출 중 기습 방지).
            if (Time.time - _spawnTime > 0.6f) _brain.Tick(dt, toPlayer.magnitude, playerAlive);

            UpdateMovement(dt, toPlayer);
            UpdateTelegraph();
        }

        void UpdateMovement(float dt, Vector3 toPlayer)
        {
            var direction = Vector3.zero;
            switch (_brain.Move)
            {
                case MoveIntent.TowardTarget: direction = toPlayer.normalized; break;
                case MoveIntent.AwayFromTarget: direction = -toPlayer.normalized; break;
            }

            // 다른 적과 겹치지 않도록 살짝 밀어낸다(간단한 분리 조향).
            foreach (var other in Combatants.Targets)
            {
                if (ReferenceEquals(other, this) || !other.IsAlive) continue;
                var away = transform.position - other.Position;
                away.y = 0;
                var d = away.magnitude;
                var min = Radius + other.Radius;
                if (d > 0.001f && d < min) direction += away / d * (min - d) * 2f;
            }

            var delta = direction * _def.MoveSpeed * dt + _knockback * dt;
            _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, dt * 25f);
            transform.position = Constrain(transform.position + delta);
            _animator.SetMoveAmount(_brain.Move == MoveIntent.None ? 0f : 1f);

            // 공격 준비 중에는 조준 방향을 고정한다(예고와 실제 공격이 어긋나지 않게).
            var look = _brain.State == EnemyState.Windup || _brain.State == EnemyState.Attack ? _aimDirection : toPlayer;
            if (_brain.State != EnemyState.Stagger && look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 10f);
        }

        /// <summary>
        /// 걷기 영역 안으로 넣고, 상자·건물 같은 고정 콜라이더에서 밀어낸다. 로봇은 지면에서만 움직인다 — 파쿠르는 플레이어의 특권.
        /// 장애물은 씬에 배치된 콜라이더 그대로라 따로 등록할 것이 없다(플레이어를 막는 콜라이더가 적도 막는다).
        /// </summary>
        Vector3 Constrain(Vector3 p)
        {
            var r = Radius;
            p.x = Mathf.Clamp(p.x, _walkable.xMin + r, _walkable.xMax - r);
            p.z = Mathf.Clamp(p.z, _walkable.yMin + r, _walkable.yMax - r);
            p.y = 0;
            // 바닥에 닿지 않게 반지름보다 조금 높이 띄운 구로 검사한다.
            var center = p + Vector3.up * (r + 0.05f);
            var count = Physics.OverlapSphereNonAlloc(center, r, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var col = Overlaps[i];
                // 플레이어 몸(CharacterController)은 장애물이 아니다. 붙어서 때리는 거리 판정은 따로 한다.
                if (col is CharacterController) continue;
                var closest = col.ClosestPoint(center);
                var away = center - closest;
                if (away.sqrMagnitude < 1e-8f)
                {
                    // 중심이 콜라이더 안에 들어갔다: 가장 가까운 옆면으로 밀어낸다.
                    var b = col.bounds;
                    var left = p.x - (b.min.x - r);
                    var right = b.max.x + r - p.x;
                    var down = p.z - (b.min.z - r);
                    var up = b.max.z + r - p.z;
                    var min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                    if (min == left) p.x = b.min.x - r;
                    else if (min == right) p.x = b.max.x + r;
                    else if (min == down) p.z = b.min.z - r;
                    else p.z = b.max.z + r;
                    continue;
                }
                away.y = 0;
                var d = away.magnitude;
                // 위아래로만 닿은 경우(바닥 등)는 무시한다.
                if (d > 0.0001f && d < r) p += away / d * (r - d);
            }
            return p;
        }

        void UpdateTelegraph()
        {
            if (_brain.State == EnemyState.Windup) _telegraph.Show(transform.position, _aimDirection, _brain.WindupProgress);
            else _telegraph.Hide();
        }

        void OnStateChanged(EnemyState from, EnemyState to)
        {
            switch (to)
            {
                case EnemyState.Windup:
                    var player = Combatants.Player;
                    _aimDirection = player != null ? player.transform.position - transform.position : transform.forward;
                    _aimDirection.y = 0;
                    if (_aimDirection.sqrMagnitude < 0.01f) _aimDirection = transform.forward;
                    _aimDirection.Normalize();
                    _animator.PlayAction("windup", _def.Windup);
                    break;
                case EnemyState.Stagger:
                    _animator.PlayAction("hurt", Mathf.Max(0.2f, _def.StaggerTime));
                    break;
            }
        }

        void ExecuteAttack()
        {
            var player = Combatants.Player;
            if (_def.Kind == EnemyKind.Ranged)
            {
                _animator.PlayAction("fire", 0.3f);
                // 발사 순간의 플레이어 위치(높이 포함)를 겨눈다.
                var origin = transform.position + Vector3.up * 1.0f + _aimDirection * 0.4f;
                var target = player != null ? player.transform.position + Vector3.up * 0.7f : origin + _aimDirection;
                Projectile.Fire(_projectile, origin, target, _def.ProjectileSpeed, _def.Attack, Colors.Hex(_def.Color));
                return;
            }

            // 근접: 예고 원 안에 있고 같은 높이면 맞는다. 컨테이너 위로 올라가면 주먹이 닿지 않는다.
            _animator.PlayAction("smash", Mathf.Max(0.25f, _def.AttackDuration + 0.15f));
            Vfx.Ring(transform.position, _def.AttackRange, Vfx.Danger, 0.18f);
            Game.Camera?.AddTrauma(IsHeavy ? 0.3f : 0.1f);
            if (player == null || !player.IsAlive) return;
            var to = player.transform.position - transform.position;
            if (!ParkourRules.CanMeleeReach(Game.Session.Stats.Parkour, to.y)) return;
            to.y = 0;
            if (to.magnitude <= _def.AttackRange + 0.3f) player.ReceiveEnemyHit(_def.Attack, transform.position);
        }

        public void ReceiveHit(HitInfo hit)
        {
            if (!IsAlive) return;
            var applied = _health.Damage(hit.Amount);
            var staggered = _brain.OnDamaged(applied, _health.IsDead);
            if (hit.Stun > 0 && !_health.IsDead) _brain.Stun(hit.Stun);

            Game.Ui.Popups.Damage(transform.position + Vector3.up * (BarHeight - 1.6f), hit.Amount, hit.IsCritical, false);
            _animator.Flash(0.07f);

            var away = transform.position - hit.Source;
            away.y = 0;
            // 경직되지 않은 강한 적은 덜 밀린다.
            var push = staggered || _health.IsDead || hit.Stun > 0 ? hit.Knockback : hit.Knockback * 0.3f;
            _knockback = away.normalized * push;

            if (_health.IsDead) Die();
        }

        void Die()
        {
            _deathTime = 0;
            _telegraph.Hide();
            _animator.Die();
            Vfx.Ring(transform.position, 2f, Colors.Hex(_def.Color), 0.45f);
            Vfx.Impact(transform.position, Vfx.Gold, 1.1f);
            Game.Ui.Popups.Text(transform.position + Vector3.up * 0.5f, $"+{Money.Format(_def.Coins)}", new Color(1f, 0.85f, 0.35f), 0.9f);
            Combatants.Unregister(this);
            Killed?.Invoke(this);
        }

        void UpdateDeath(float dt)
        {
            _deathTime += dt;
            transform.position += _knockback * dt;
            _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, dt * 20f);
            var t = Mathf.Clamp01(_deathTime / _deathDuration);
            // 쓰러진 뒤 깜빡이며 사라진다(아케이드식 퇴장).
            if (t > 0.55f) _view.gameObject.SetActive(Mathf.Repeat(_deathTime * 20f, 1f) > 0.4f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
