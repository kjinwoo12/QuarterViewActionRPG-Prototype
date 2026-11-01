using Gil.Core.Combat;
using Gil.Core.Cyber;
using Gil.Core.Data;
using Gil.Runtime.CameraRig;
using Gil.Runtime.Combat;
using Gil.Runtime.Systems;
using Gil.Runtime.Visual;
using Gil.Runtime.World;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gil.Runtime.Player
{
    /// <summary>
    /// 주인공 조작: 맨손 격투 + 파쿠르. 입력을 해석해 Core의 규칙 객체(ComboTracker, RollingTimer, Stamina, HeartGauge, Health,
    /// ParkourRules, PlayerStats)를 구동하고, 그 결과를 이동·판정·연출로 옮긴다. 수치 판단은 Core, "어떻게 보이고 움직이나"는 여기.
    ///
    /// 달리기: 달리기 버튼을 누르고 있는 동안 이동 속도 배율 적용, 스태미나 소모(공격·구르기·공중 중에는 달리지 않음).
    /// 파쿠르: 구르기로 ParkourSurface에 부딪히면 높이에 따라 넘기/오르기. 높은 곳에서 떨어지는 중 공격하면 내려찍기.
    /// 의체 기믹은 장착 여부만 확인해서 이 클래스의 정해진 지점(저스트 회피, 콤보 마무리, 타격, 착지, 사망)에서 발동한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        enum Mode
        {
            Normal,
            Rolling,
            Vault,
            Climb,
            Airborne,
            Plunge,
            Skill,
            Hurt,
            Dead,
        }

        // 화면 쪽 외형(스프라이트시트 또는 임시 SD 외형)과 그림자는 플레이어 씬(Player)의 Player 프리팹 안에 함께 배치돼 있다.
        [SerializeField] IsoView _view;
        [SerializeField] GroundShadow _shadow;

        // 조작감 조정값. 전투 수치(콤보·회피·체력 등)는 데이터 에셋(Data/Player.asset)에 있고, 여기는 연출·판정 타이밍이다.
        [Header("조작감")]
        [SerializeField, Tooltip("과부하(A 키) 동작 전체 시간(초)")] float _skillDuration = 0.5f;
        [SerializeField, Tooltip("과부하 시작 후 충격파가 나가는 시점(초)")] float _skillImpactTime = 0.18f;
        [SerializeField, Tooltip("피격·착지 경직 시간(초)")] float _hurtStunTime = 0.22f;
        [SerializeField, FormerlySerializedAs("_perfectDodgeHeartBonus"), Tooltip("저스트 회피(구르기 무적으로 피함) 때 채워지는 심장 게이지(%)")] float _perfectRollingHeartBonus = 12f;
        [SerializeField, Tooltip("구르는 중 앞의 파쿠르 표면을 찾는 거리(m, 몸 반지름에 더한다)")] float _parkourProbe = 0.75f;
        [SerializeField, Tooltip("내려찍기 낙하 속도(m/s)")] float _plungeSpeed = 16f;

        PlayerDef _def;
        CharacterController _controller;
        ICharacterAnimator _animator;
        ComboTracker _combo;
        RollingTimer _rolling;
        bool _sprinting;

        Mode _mode = Mode.Normal;
        float _modeTime;
        bool _skillFired;
        Vector3 _facing = new Vector3(1, 0, 1).normalized;
        Vector3 _rollingDirection;
        Vector3 _knockback;
        float _verticalSpeed;
        float _lungeSpeed;
        float _lungeTimeLeft;
        float _hurtInvulnerableUntil;
        float _lastPerfectRolling = -10f;
        float _airPeak;
        Vector3 _pendingMove;

        // 넘기/오르기 경로(지면 좌표)
        Vector3 _moveFrom;
        Vector3 _moveTo;
        float _moveArc;
        float _moveDuration;

        public bool IsAlive => _mode != Mode.Dead;
        public Vector3 Facing => _facing;
        /// <summary>이번 프레임에 달리는 중인지(SprintFx가 읽는다).</summary>
        public bool IsSprinting => _sprinting;
        public bool IsAirborne => _mode == Mode.Airborne || _mode == Mode.Plunge || _mode == Mode.Vault || _mode == Mode.Climb;

        static PlayerStats Stats => Game.Session.Stats;

        /// <summary>대상이 죽었을 때(사망 연출 후) GameRoot가 받는다.</summary>
        public event System.Action Died;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = _view.GetComponent<ICharacterAnimator>();
        }

        /// <summary>한 판(세션)을 시작할 때마다 부른다. 조작 상태와 외형 자세를 처음으로 되돌린다.</summary>
        public void Init(PlayerDef def)
        {
            _def = def;

            _combo = new ComboTracker(def.Combo);
            _combo.StepStarted += OnComboStep;
            _combo.HitFrame += OnHitFrame;
            _rolling = new RollingTimer(def.Rolling);

            _mode = Mode.Normal;
            _modeTime = 0;
            _skillFired = false;
            _knockback = Vector3.zero;
            _verticalSpeed = 0;
            _lungeTimeLeft = 0;
            _hurtInvulnerableUntil = 0;
            _lastPerfectRolling = -10f;
            _sprinting = false;
            _pendingMove = Vector3.zero;
            _animator.ResetPose();

            Combatants.Player = this;
        }

        void OnDestroy()
        {
            if (Combatants.Player == this) Combatants.Player = null;
        }

        /// <summary>의체가 바뀌면 구르기 충전 수(제비 다리)와 재사용 대기 등을 다시 맞춘다.</summary>
        public void ApplyStats()
        {
            var doubleRolling = Game.Session.Cyberware.Gimmick(Gimmicks.DoubleRolling);
            _rolling.Configure(Stats.Rolling, doubleRolling != null ? Mathf.Max(1, Mathf.RoundToInt(doubleRolling.Param("charges", 2))) : 1);
        }

        /// <summary>지역 이동 시 순간이동. CharacterController는 켜진 채로 위치를 바꾸면 덮어쓰므로 잠시 끈다.</summary>
        public void Teleport(Vector3 position, Vector3 facing)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _pendingMove = Vector3.zero;
            Face(facing);
            _combo.Cancel();
            _knockback = Vector3.zero;
            _verticalSpeed = 0;
            _lungeTimeLeft = 0;
            _animator.StopAction();
            _animator.SetAirborne(false);
            if (_mode != Mode.Dead) SetMode(Mode.Normal);
            _view.Sync();
        }

        /// <summary>사망 후 부활.</summary>
        public void Revive()
        {
            _animator.ResetPose();
            _hurtInvulnerableUntil = Time.time + 1.5f;
            Game.Session.Stamina.Refill();
            SetMode(Mode.Normal);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            var session = Game.Session;
            if (session == null || _def == null || _mode == Mode.Dead) return;

            _modeTime += dt;
            _combo.Tick(dt);
            _rolling.Tick(dt);
            session.Stamina.Tick(dt);
            _animator.SetHeartRate(1f + session.Heart.Normalized * 3f);

            var input = Game.Input;
            var locked = Game.GameplayLocked;
            var move = locked ? Vector2.zero : input.Move;
            var moveWorld = QuarterViewCamera.ScreenRight * move.x + QuarterViewCamera.ScreenUp * move.y;

            switch (_mode)
            {
                case Mode.Normal:
                    UpdateNormal(dt, moveWorld, locked);
                    break;
                case Mode.Rolling:
                    UpdateRolling(dt);
                    break;
                case Mode.Vault:
                case Mode.Climb:
                    UpdateTraverse();
                    break;
                case Mode.Airborne:
                    UpdateAirborne(dt, moveWorld, locked);
                    break;
                case Mode.Plunge:
                    UpdatePlunge();
                    break;
                case Mode.Skill:
                    UpdateSkill();
                    break;
                case Mode.Hurt:
                    if (_modeTime >= _hurtStunTime) SetMode(Mode.Normal);
                    break;
            }

            // 달리기는 Normal에서만. 다른 상태에서도 버튼을 뗐는지는 알려 줘야 지친 상태가 풀린다.
            if (_mode != Mode.Normal && _sprinting) StopSprint();
            if (_mode != Mode.Normal) session.Stamina.Sprint(!locked && input.SprintHeld, false, 0f, dt);

            if (_mode == Mode.Vault || _mode == Mode.Climb) return;

            // 넉백·전진(lunge) 공통 처리
            var extra = _knockback * dt;
            _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, dt * 30f);
            if (_lungeTimeLeft > 0)
            {
                extra += _facing * _lungeSpeed * dt;
                _lungeTimeLeft -= dt;
            }
            Move(extra, dt);
            ApplyMovement(dt);

            // 발판에서 걸어 나가면 떨어진다.
            if ((_mode == Mode.Normal || _mode == Mode.Hurt) && !_controller.isGrounded && GroundBelow() < transform.position.y - 0.4f)
                StartAirborne();

            _shadow.SetGround(GroundBelowOrSelf());
            if (!locked && input.InteractPressed && _mode == Mode.Normal) TryInteract();
        }

        void UpdateNormal(float dt, Vector3 moveWorld, bool locked)
        {
            var input = Game.Input;
            if (!locked)
            {
                if (input.RollingPressed && _rolling.IsReady && !_combo.IsCommitted)
                {
                    StartRolling(moveWorld);
                    if (_mode != Mode.Normal) return;
                }
                if (input.JumpPressed && !_combo.IsCommitted && _controller.isGrounded)
                {
                    StartJump();
                    return;
                }
                if (input.SkillPressed)
                {
                    if (!_combo.IsCommitted && Game.Session.Heart.TrySpend()) StartSkill();
                    else if (!Game.Session.Heart.CanOverdrive) Game.Ui.Hud.FlashHeartNotReady();
                }
                if (input.AttackPressed) _combo.PressAttack();
                if (input.PotionPressed) UsePotion();
            }

            var speed = moveWorld.magnitude;
            var sprintHeld = !locked && Game.Input.SprintHeld;
            var sprinting = Game.Session.Stamina.Sprint(sprintHeld, speed > 0.01f && !_combo.IsAttacking, Stats.Sprint.StaminaPerSecond, dt);
            if (sprinting != _sprinting)
            {
                _sprinting = sprinting;
                _animator.SetMoveSpeedScale(sprinting ? Stats.Sprint.SpeedMultiplier : 1f);
            }

            if (_combo.IsAttacking)
            {
                _animator.SetMoveAmount(0);
                return;
            }

            _animator.SetMoveAmount(speed);
            if (speed > 0.01f)
            {
                Face(moveWorld);
                Move(moveWorld * Stats.MoveSpeed * (sprinting ? Stats.Sprint.SpeedMultiplier : 1f) * dt, dt);
            }
        }

        // ── 공격(맨손) ─────────────────────────────────────────

        void OnComboStep(int index)
        {
            var step = _def.Combo[index];

            // 바라보는 방향으로 친다(자동 조준 없음). 방향키를 누르고 있으면 그쪽으로 돌아서 친다.
            var input = Game.Input.Move;
            if (input.sqrMagnitude > 0.01f)
                Face(QuarterViewCamera.ScreenRight * input.x + QuarterViewCamera.ScreenUp * input.y);

            _lungeSpeed = step.Lunge / Mathf.Max(0.01f, step.Windup + step.Active);
            _lungeTimeLeft = step.Windup + step.Active;
            _animator.PlayAction(string.IsNullOrEmpty(step.Name) ? "jab" : step.Name, step.Windup + step.Active + step.Recovery);
        }

        void OnHitFrame(int index)
        {
            var step = _def.Combo[index];
            var stats = Stats;
            var origin = transform.position;
            var isFinisher = index == _def.Combo.Count - 1;
            Vfx.Slash(origin, _facing, step.Range, step.ArcDegrees, isFinisher ? Vfx.HeartRed : Vfx.PlayerStrike);

            var targets = Combatants.InSector(origin, _facing, step.Range, step.ArcDegrees, stats.Parkour.MeleeHeightTolerance);
            foreach (var t in targets)
                Strike(t, step.DamageMultiplier, isFinisher ? 7f : 3f, 0f);

            // 충격 주먹 「벽력」: 콤보 마무리에 주변 충격파
            var shock = Game.Session.Cyberware.Gimmick(Gimmicks.ShockFinisher);
            if (isFinisher && shock != null)
            {
                var radius = shock.Param("radius", 2.8f);
                Vfx.Ring(origin, radius, Vfx.Shock, 0.3f);
                foreach (var t in Combatants.InSector(origin, _facing, radius, 360f, stats.Parkour.MeleeHeightTolerance))
                    Strike(t, shock.Param("multiplier", 1.2f), 6f, 0f, secondary: true);
                Game.Camera.AddTrauma(0.2f);
            }

            if (targets.Count == 0) return;
            TimeController.HitStop(step.HitStop);
            Game.Camera.AddTrauma(isFinisher ? 0.35f : 0.18f);
        }

        /// <summary>대상 하나를 때린다(치명타·심장 게이지·연쇄 전격 포함).</summary>
        void Strike(IHittable target, float multiplier, float knockback, float stun, bool secondary = false)
        {
            var stats = Stats;
            var damage = DamageCalculator.Compute(stats.Attack, multiplier, target.Defense, stats.CritChance, stats.CritMultiplier,
                Random.value);
            target.ReceiveHit(new HitInfo
            {
                Amount = damage.Amount,
                IsCritical = damage.IsCritical,
                Source = transform.position,
                Knockback = knockback,
                Stun = stun,
                IsSecondary = secondary,
            });
            Game.Session.Heart.OnHitLanded();
            Vfx.Impact(target.Position, damage.IsCritical ? Vfx.Gold : Vfx.PlayerStrike, damage.IsCritical ? 0.8f : 0.6f);
            if (!secondary) TryChainShock(target);
        }

        /// <summary>전격 너클: 일정 확률로 가까운 다른 적에게 번개가 옮겨 붙는다.</summary>
        void TryChainShock(IHittable from)
        {
            var chain = Game.Session.Cyberware.Gimmick(Gimmicks.ChainShock);
            if (chain == null || Random.value > chain.Param("chance", 0.3f)) return;
            var radius = chain.Param("radius", 3.5f);
            IHittable best = null;
            var bestDistance = radius;
            foreach (var t in Combatants.Targets)
            {
                if (ReferenceEquals(t, from) || !t.IsAlive) continue;
                var d = Vector3.Distance(t.Position, from.Position);
                if (d < bestDistance)
                {
                    best = t;
                    bestDistance = d;
                }
            }
            if (best == null) return;
            Vfx.Lightning(from.Position, best.Position, Vfx.Shock);
            Strike(best, chain.Param("multiplier", 0.6f), 2f, 0.3f, secondary: true);
        }

        // ── 구르기 · 파쿠르 ────────────────────────────────────

        /// <summary>테스트용: 입력 없이 그 방향으로 구르기(파쿠르 포함).</summary>
        internal void TestRolling(Vector3 direction) => StartRolling(direction);
        internal string ModeName => _mode.ToString();

        /// <summary>
        /// 구르기. 스태미나가 구르기 비용보다 적으면 구르지 않고 HUD 스태미나 바를 깜빡인다.
        /// 바로 앞이 넘거나 오를 수 있는 지형이면 구르기 대신 파쿠르(스태미나를 쓰지 않는다).
        /// </summary>
        void StartRolling(Vector3 moveWorld)
        {
            var stamina = Game.Session.Stamina;
            var cost = Stats.Rolling.StaminaCost;
            if (!stamina.CanSpend(cost))
            {
                Game.Ui.Hud.FlashStaminaLow();
                return;
            }
            var direction = moveWorld.sqrMagnitude > 0.01f ? moveWorld.normalized : _facing;
            Face(direction);
            if (TryParkour(direction)) return;
            if (!_rolling.TryStart()) return;
            stamina.TrySpend(cost);
            StopSprint();
            _combo.Cancel();
            _lungeTimeLeft = 0;
            _rollingDirection = direction;
            _animator.PlayAction("rolling", Stats.Rolling.Duration + 0.1f);
            _animator.Squash(0.15f);
            Vfx.Dust(transform.position, 0.8f);
            SetMode(Mode.Rolling);
        }

        void StopSprint()
        {
            if (!_sprinting) return;
            _sprinting = false;
            _animator.SetMoveSpeedScale(1f);
        }

        void UpdateRolling(float dt)
        {
            if (TryParkour(_rollingDirection)) return;
            Move(_rollingDirection * _rolling.Speed * dt, dt);
            if (!_rolling.IsRolling)
            {
                if (!_controller.isGrounded && GroundBelow() < transform.position.y - 0.4f) StartAirborne();
                else SetMode(Mode.Normal);
            }
        }

        /// <summary>앞쪽을 살펴 넘기/오르기를 시작한다. 시작했으면 true.</summary>
        bool TryParkour(Vector3 direction)
        {
            var parkour = Stats.Parkour;
            var feet = transform.position;
            var origin = feet + Vector3.up * 0.45f;
            if (!Physics.Raycast(origin, direction, out var hit, _controller.radius + _parkourProbe, ~0, QueryTriggerInteraction.Ignore))
                return false;
            var surface = hit.collider.GetComponent<ParkourSurface>();
            if (surface == null) return false;

            var top = surface.TopHeight;
            var relative = top - feet.y;

            // 넘을 때 착지할 자리: 장애물 반대편 면에서 조금 더 간 곳.
            var landing = Vector3.zero;
            var landingClear = false;
            if (surface.Vaultable)
            {
                var far = hit.point + direction * 4f;
                if (hit.collider.Raycast(new Ray(new Vector3(far.x, origin.y, far.z), -direction), out var back, 4f))
                {
                    landing = back.point + direction * (_controller.radius + 0.35f);
                    landing.y = feet.y;
                    landingClear = !Physics.CheckCapsule(landing + Vector3.up * 0.45f, landing + Vector3.up * 1.2f, 0.3f, ~0,
                        QueryTriggerInteraction.Ignore);
                }
            }

            var move = ParkourRules.Choose(parkour, relative, surface.Vaultable, surface.Climbable, landingClear);
            switch (move)
            {
                case ParkourMove.Vault:
                    BeginTraverse(Mode.Vault, landing, relative + 0.35f, parkour.VaultDuration);
                    _animator.PlayAction("vault", parkour.VaultDuration);
                    return true;
                case ParkourMove.Climb:
                    var onTop = hit.point + direction * (_controller.radius + 0.25f);
                    onTop.y = top + 0.02f;
                    BeginTraverse(Mode.Climb, onTop, 0.25f, parkour.ClimbDuration + relative * 0.05f);
                    _animator.PlayAction("climb", _moveDuration);
                    Vfx.Dust(feet, 0.6f);
                    return true;
                default:
                    if (_mode == Mode.Rolling && relative > parkour.ClimbMaxHeight)
                        Game.Ui.Hud.Notice("너무 높다 — 도약 다리 의체가 있으면 오를 수 있을지도");
                    return false;
            }
        }

        void BeginTraverse(Mode mode, Vector3 to, float arc, float duration)
        {
            _combo.Cancel();
            _lungeTimeLeft = 0;
            _knockback = Vector3.zero;
            _moveFrom = transform.position;
            _moveTo = to;
            _moveArc = arc;
            _moveDuration = Mathf.Max(0.1f, duration);
            _controller.enabled = false;
            Face(to - _moveFrom);
            SetMode(mode);
        }

        /// <summary>넘기/오르기 중 위치를 직접 보간한다(무적).</summary>
        void UpdateTraverse()
        {
            var t = Mathf.Clamp01(_modeTime / _moveDuration);
            Vector3 p;
            if (_mode == Mode.Vault)
            {
                p = Vector3.Lerp(_moveFrom, _moveTo, t);
                p.y = Mathf.Lerp(_moveFrom.y, _moveTo.y, t) + Mathf.Sin(t * Mathf.PI) * _moveArc;
            }
            else
            {
                // 오르기: 앞 60%는 벽을 타고 올라가고, 나머지는 윗면으로 몸을 끌어올린다.
                var up = Mathf.Clamp01(t / 0.6f);
                var over = Mathf.Clamp01((t - 0.6f) / 0.4f);
                p = Vector3.Lerp(_moveFrom, new Vector3(_moveTo.x, _moveFrom.y, _moveTo.z), over * 0.85f + up * 0.15f);
                p.y = Mathf.Lerp(_moveFrom.y, _moveTo.y + _moveArc, Mathf.SmoothStep(0, 1, up)) - _moveArc * over;
            }
            transform.position = p;
            _shadow.SetGround(GroundBelowOrSelf());

            if (t >= 1f)
            {
                transform.position = _moveTo;
                _controller.enabled = true;
                _verticalSpeed = 0;
                Vfx.Dust(_moveTo, 0.5f);
                SetMode(Mode.Normal);
            }
        }

        /// <summary>제자리·이동 점프. 공중에서 공격하면(높이 0.5m 이상) 내려찍기로 이어진다.</summary>
        void StartJump()
        {
            _verticalSpeed = Mathf.Sqrt(2f * Stats.Parkour.Gravity * Mathf.Max(0.1f, Stats.Parkour.JumpHeight));
            StartAirborne();
        }

        void StartAirborne()
        {
            _airPeak = transform.position.y;
            _animator.SetAirborne(true);
            SetMode(Mode.Airborne);
        }

        void UpdateAirborne(float dt, Vector3 moveWorld, bool locked)
        {
            _airPeak = Mathf.Max(_airPeak, transform.position.y);
            if (moveWorld.sqrMagnitude > 0.01f) Face(moveWorld);
            Move(moveWorld * Stats.MoveSpeed * 0.7f * dt, dt);

            // 높은 곳에서 떨어지는 중 공격 → 내려찍기
            if (!locked && Game.Input.AttackPressed && transform.position.y - GroundBelow() >= 0.5f)
            {
                _verticalSpeed = -_plungeSpeed;
                _animator.PlayAction("plunge", 2f);
                SetMode(Mode.Plunge);
                return;
            }
            if (_controller.isGrounded && _verticalSpeed <= 0) Land(false);
        }

        void UpdatePlunge()
        {
            if ((_controller.isGrounded && _modeTime > 0.02f) || _modeTime > 1.5f) Land(true);
        }

        void Land(bool plunge)
        {
            var fall = _airPeak - transform.position.y;
            _animator.SetAirborne(false);
            _verticalSpeed = 0;
            Vfx.Dust(transform.position, Mathf.Clamp(fall * 0.5f, 0.6f, 1.6f));
            if (!plunge)
            {
                if (fall > 1f) _animator.Squash(0.18f);
                SetMode(Mode.Normal);
                return;
            }

            var parkour = Stats.Parkour;
            var multiplier = ParkourRules.PlungeMultiplier(parkour, fall);
            _animator.PlayAction("land", 0.35f);
            if (multiplier > 0)
            {
                multiplier *= Stats.PlungeDamageMultiplier;
                var kinetic = Game.Session.Cyberware.Gimmick(Gimmicks.KineticLanding);
                var stun = kinetic != null ? kinetic.Param("stun", 1.2f) : 0f;
                Vfx.Ring(transform.position, parkour.PlungeRadius, Vfx.HeartRed, 0.35f);
                Vfx.Ring(transform.position, parkour.PlungeRadius * 0.6f, Color.white, 0.22f);
                if (kinetic != null) Vfx.Ring(transform.position, parkour.PlungeRadius * 1.1f, Vfx.Shock, 0.45f);
                foreach (var t in Combatants.InSector(transform.position, _facing, parkour.PlungeRadius, 360f, parkour.MeleeHeightTolerance))
                    Strike(t, multiplier, 8f, stun);
                TimeController.HitStop(0.08f);
                Game.Camera.AddTrauma(0.55f);
            }
            else
            {
                Game.Camera.AddTrauma(0.15f);
            }
            SetMode(Mode.Hurt); // 짧은 착지 경직(_hurtStunTime)
        }

        // ── 필살기: 과부하 ─────────────────────────────────────

        void StartSkill()
        {
            _combo.Cancel();
            _skillFired = false;
            _animator.PlayAction("overdrive", _skillDuration);
            _animator.Squash(-0.2f);
            Game.Ui.Hud.ShowBanner("과부하", 0.8f);
            TimeController.SlowMotion(0.35f, 0.12f);
            SetMode(Mode.Skill);
        }

        void UpdateSkill()
        {
            if (!_skillFired && _modeTime >= _skillImpactTime)
            {
                _skillFired = true;
                var heart = _def.Heart;
                var radius = Stats.SkillRadius;
                Vfx.Ring(transform.position, radius, Vfx.HeartRed, 0.4f);
                Vfx.Ring(transform.position, radius * 0.6f, Color.white, 0.25f);
                foreach (var t in Combatants.InSector(transform.position, _facing, radius, 360f, Stats.Parkour.MeleeHeightTolerance + 1f))
                    Strike(t, heart.SkillDamageMultiplier, 10f, 0f, secondary: true);
                TimeController.HitStop(0.12f);
                Game.Camera.AddTrauma(0.7f);
                _animator.Squash(0.3f);
            }
            if (_modeTime >= _skillDuration) SetMode(Mode.Normal);
        }

        // ── 피격 ───────────────────────────────────────────────

        /// <summary>적 공격이 닿았을 때. 무적 중이면 무시하고, 구르기 무적으로 피했다면 「저스트 회피」 보상을 준다.</summary>
        public void ReceiveEnemyHit(int rawAttack, Vector3 source)
        {
            if (_mode == Mode.Dead || _mode == Mode.Vault || _mode == Mode.Climb) return;

            if (_mode == Mode.Rolling && _rolling.IsInvulnerable)
            {
                if (Time.time - _lastPerfectRolling > 0.6f) PerfectRolling();
                return;
            }
            if (_mode == Mode.Skill || Time.time < _hurtInvulnerableUntil) return;

            var damage = DamageCalculator.Compute(rawAttack, 1f, Stats.Defense, 0f, 1f, 1f);
            var session = Game.Session;
            session.PlayerHealth.Damage(damage.Amount);
            session.Heart.OnDamageTaken();
            Game.Ui.Popups.Damage(transform.position, damage.Amount, false, true);
            Game.Camera.AddTrauma(0.45f);
            TimeController.HitStop(0.06f);
            _animator.Flash(0.1f);
            _hurtInvulnerableUntil = Time.time + _def.HurtInvulnerableTime;

            var away = transform.position - source;
            away.y = 0;
            _knockback = away.normalized * 9f;
            _combo.Cancel();
            _lungeTimeLeft = 0;

            if (session.PlayerHealth.IsDead)
            {
                // 제2 심실: 지역당 한 번, 쓰러지는 순간 다시 일어선다.
                if (session.TryUseSecondWind())
                {
                    Vfx.Ring(transform.position, 3f, Vfx.HeartRed, 0.5f);
                    Game.Ui.Hud.ShowBanner("제2 심실 가동", 1.2f);
                    TimeController.SlowMotion(0.25f, 0.5f);
                    _hurtInvulnerableUntil = Time.time + 2f;
                    SetMode(Mode.Hurt);
                    return;
                }
                _animator.Die();
                SetMode(Mode.Dead);
                Died?.Invoke();
            }
            else
            {
                _animator.PlayAction("hurt", 0.3f);
                if (!IsAirborne) SetMode(Mode.Hurt);
            }
        }

        void PerfectRolling()
        {
            _lastPerfectRolling = Time.time;
            Game.Session.Heart.Add(_perfectRollingHeartBonus);
            var neural = Game.Session.Cyberware.Gimmick(Gimmicks.NeuralSlowmo);
            if (neural != null)
            {
                // 신경 가속기 「번개」: 세상이 느려지는 동안 반격
                TimeController.SlowMotion(0.2f, neural.Param("duration", 0.9f));
                Game.Ui.Popups.Text(transform.position, "신경 가속!", Vfx.Shock, 1.2f);
                Vfx.Ring(transform.position, 2.4f, Vfx.Shock, 0.4f);
            }
            else
            {
                TimeController.SlowMotion(0.3f, 0.25f);
                Game.Ui.Popups.Text(transform.position, "저스트 회피!", new Color(0.4f, 1f, 0.9f), 1.1f);
            }
        }

        // ── 기타 ───────────────────────────────────────────────

        void UsePotion()
        {
            var session = Game.Session;
            if (session.PlayerHealth.Current >= session.PlayerHealth.Max)
            {
                Game.Ui.Hud.Notice("체력이 가득합니다");
                return;
            }
            foreach (var id in new[] { "potion", "hi_potion" })
            {
                if (session.UseItem(id))
                {
                    Game.Ui.Popups.Text(transform.position, $"+{session.Db.Items[id].Heal}", new Color(0.5f, 1f, 0.6f), 1f);
                    Vfx.Ring(transform.position, 1.4f, new Color(0.4f, 1f, 0.6f), 0.3f);
                    return;
                }
            }
            Game.Ui.Hud.Notice("회복약이 없습니다. 장터에서 살 수 있어요");
        }

        void TryInteract()
        {
            var target = Interactable.Nearest(transform.position);
            if (target == null) return;
            Face(target.transform.position - transform.position);
            target.Interact();
        }

        void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) return;
            _facing = direction.normalized;
            transform.rotation = Quaternion.LookRotation(_facing);
        }

        /// <summary>이번 프레임의 수평 이동을 쌓는다(실제 이동은 프레임 끝에 한 번).</summary>
        void Move(Vector3 delta, float dt) => _pendingMove += new Vector3(delta.x, 0, delta.z);

        /// <summary>쌓인 수평 이동 + 중력을 한 번에 적용한다. 땅에 있으면 바닥에 붙여 둔다.</summary>
        void ApplyMovement(float dt)
        {
            var delta = _pendingMove;
            _pendingMove = Vector3.zero;
            if (!_controller.enabled) return;
            if (_mode == Mode.Plunge) _verticalSpeed = -_plungeSpeed;
            else if (_controller.isGrounded && _verticalSpeed <= 0) _verticalSpeed = -2f;
            else _verticalSpeed -= Stats.Parkour.Gravity * dt;
            delta.y = _verticalSpeed * dt;
            _controller.Move(delta);
        }

        /// <summary>발밑 지면(바닥·발판 윗면)의 높이. 없으면 아주 낮은 값.</summary>
        float GroundBelow()
        {
            var origin = transform.position + Vector3.up * 0.2f;
            return Physics.Raycast(origin, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : -100f;
        }

        float GroundBelowOrSelf()
        {
            var g = GroundBelow();
            return g < -50f ? transform.position.y : g;
        }

        void SetMode(Mode mode)
        {
            _mode = mode;
            _modeTime = 0;
        }
    }
}
