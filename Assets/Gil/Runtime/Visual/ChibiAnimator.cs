using System.Collections.Generic;
using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 애니메이션 클립 없이 뼈를 직접 움직이는 절차적 애니메이터.
    /// 확정 에셋에서는 같은 계약(<see cref="ICharacterAnimator"/>)의 <see cref="SpriteSheetAnimator"/>가 대신한다.
    /// 바라보는 방향은 따라가는 시뮬레이션 오브젝트의 forward에서 읽는다(좌우 반전 + 앞/뒤 모습).
    /// </summary>
    public sealed class ChibiAnimator : MonoBehaviour, ICharacterAnimator
    {
        struct Pose
        {
            public float ArmL, ArmR, LegL, LegR;      // 도. 0 = 아래, + = 앞
            public float ReachL, ReachR, KickR;       // 팔·다리 뻗기(길이 배율 - 1)
            public float Lean;                        // + = 앞으로 숙임
            public float Head;
            public float Bob;                         // 위아래(유닛)
            public float Forward;                     // 앞뒤 이동(유닛)
            public float Spin;                        // 0~1: 몸을 한 바퀴 돌리는 진행(좌우 폭으로 표현)
        }

        [SerializeField] ChibiRig _rig = new ChibiRig();
        [SerializeField, Tooltip("바라보는 방향을 읽어 올 시뮬레이션 오브젝트(지면 좌표)")] Transform _follow;
        [SerializeField, Tooltip("켜면 기본 자세가 주먹을 든 가드 자세")] bool _fighter;
        [SerializeField, Tooltip("피격 순간 하얗게 번쩍일 때 쓰는 조명 무시 머티리얼(Art/Materials/GilSpriteUnlit)")] Material _flashMaterial;
        readonly List<(SpriteRenderer renderer, Color color, Material material)> _parts =
            new List<(SpriteRenderer, Color, Material)>();

        float _moveAmount;
        float _moveSpeedScale = 1f;
        float _phase;
        bool _airborne;
        string _action;
        float _actionTime;
        float _actionDuration;
        float _flashUntil;
        float _squash;
        bool _dead;
        float _deadTime;
        float _heartRate = 1.2f;
        bool _facingLeft;
        bool _facingAway;
        float _heartBase = 1f;

        public ChibiRig Rig => _rig;
        public bool IsPlaying => _action != null;

        /// <summary>배치할 때 뼈대·따라갈 대상을 정한다. 씬 뷰에서도 바라보는 방향대로 보이게 바로 맞춘다.</summary>
        public void Setup(ChibiRig rig, Transform follow, bool fighter)
        {
            _rig = rig;
            _follow = follow;
            _fighter = fighter;
            ApplyFacing(true);
        }

        void Awake()
        {
            foreach (var r in _rig.Root.GetComponentsInChildren<SpriteRenderer>(true))
                _parts.Add((r, r.color, r.sharedMaterial));
            _heartBase = _rig.Heart != null ? _rig.Heart.localScale.x : 1f;
            ApplyFacing(true);
        }

        public void SetMoveAmount(float normalized) => _moveAmount = Mathf.Clamp01(normalized);
        public void SetMoveSpeedScale(float scale) => _moveSpeedScale = Mathf.Max(0.1f, scale);
        public void SetAirborne(bool airborne) => _airborne = airborne;
        public void Squash(float amount) => _squash = amount;
        public void SetHeartRate(float beatsPerSecond) => _heartRate = beatsPerSecond;

        /// <summary>
        /// 동작 재생. 이름: jab / straight / spin_kick / rolling / vault / climb / plunge / land / overdrive / hurt /
        /// windup / smash / fire. 모르는 이름이면 가볍게 몸만 앞으로 기울인다.
        /// </summary>
        public void PlayAction(string name, float duration)
        {
            _action = name;
            _actionTime = 0;
            _actionDuration = Mathf.Max(0.05f, duration);
        }

        public void StopAction() => _action = null;

        public void Flash(float seconds = 0.08f)
        {
            _flashUntil = Time.time + seconds;
            var white = _flashMaterial;
            foreach (var (r, c, _) in _parts)
            {
                if (r == null) continue;
                r.sharedMaterial = white;
                r.color = new Color(1, 1, 1, c.a);
            }
        }

        public void Die()
        {
            _dead = true;
            _deadTime = 0;
        }

        /// <summary>쓰러진 자세 등을 원래대로(부활).</summary>
        public void ResetPose()
        {
            _dead = false;
            _action = null;
            _squash = 0;
            foreach (var (r, c, m) in _parts)
            {
                if (r == null) continue;
                r.color = c;
                r.sharedMaterial = m;
            }
            if (_rig.Visual != null) _rig.Visual.localRotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            var dt = Time.deltaTime;

            if (_flashUntil > 0 && Time.time >= _flashUntil)
            {
                _flashUntil = 0;
                foreach (var (r, c, m) in _parts)
                {
                    if (r == null) continue;
                    r.color = c;
                    r.sharedMaterial = m;
                }
            }

            if (_dead)
            {
                _deadTime += dt;
                var t = Mathf.Clamp01(_deadTime / 0.35f);
                _rig.Visual.localRotation = Quaternion.Euler(0, 0, 80f * t);
                _rig.Visual.localPosition = _rig.Rest(_rig.Visual) + new Vector3(-0.25f * t, 0, 0);
                return;
            }

            ApplyFacing(false);

            var pose = Locomotion(dt);
            if (_action != null)
            {
                _actionTime += dt;
                var t = Mathf.Clamp01(_actionTime / _actionDuration);
                pose = Action(_action, t, pose);
                if (_actionTime >= _actionDuration) _action = null;
            }
            Apply(pose, dt);

            if (_rig.Heart != null)
            {
                var beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * _heartRate)), 8f);
                _rig.Heart.localScale = Vector3.one * _heartBase * (1f + beat * 0.6f);
            }
        }

        void ApplyFacing(bool force)
        {
            if (_follow == null) return;
            var screen = Iso.ToScreenDirection(_follow.forward);
            if (screen.sqrMagnitude < 0.0001f) return;
            var left = screen.x < -0.01f || (Mathf.Abs(screen.x) <= 0.01f && _facingLeft);
            var away = screen.y > 0.02f;
            if (force || left != _facingLeft)
            {
                _facingLeft = left;
                var s = _rig.Root.localScale;
                s.x = Mathf.Abs(s.x) * (left ? -1 : 1);
                _rig.Root.localScale = s;
            }
            if (force || away != _facingAway)
            {
                _facingAway = away;
                foreach (var go in _rig.FrontOnly) if (go != null) go.SetActive(!away);
                foreach (var go in _rig.BackOnly) if (go != null) go.SetActive(away);
            }
        }

        Pose Locomotion(float dt)
        {
            var pose = new Pose();
            if (_rig.Hovering)
            {
                pose.Bob = Mathf.Sin(Time.time * 3f + (GetHashCode() & 1023)) * 0.06f;
                pose.Lean = _moveAmount * 12f;
                // 날개 축이 빠르게 떨린다.
                pose.ArmL = Mathf.Sin(Time.time * 40f) * 4f;
                pose.ArmR = -pose.ArmL;
                return pose;
            }

            if (_airborne)
            {
                pose.ArmL = 140f;
                pose.ArmR = 120f;
                pose.LegL = -15f;
                pose.LegR = 40f;
                pose.Lean = 8f;
                return pose;
            }

            _phase += dt * Mathf.Lerp(0f, 13f, _moveAmount) * _moveSpeedScale;
            var swing = Mathf.Sin(_phase);
            pose.LegL = swing * 30f * _moveAmount;
            pose.LegR = -swing * 30f * _moveAmount;
            pose.Bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.035f * _moveAmount + Mathf.Sin(Time.time * 2.4f) * 0.006f;
            pose.Lean = _moveAmount * 8f;
            if (_fighter)
            {
                // 가드: 두 주먹을 턱 앞에. 달릴 때도 가드를 유지한다.
                pose.ArmR = 115f + swing * 8f * _moveAmount;
                pose.ArmL = 100f - swing * 8f * _moveAmount;
            }
            else
            {
                pose.ArmR = 6f + swing * 22f * _moveAmount;
                pose.ArmL = 4f - swing * 22f * _moveAmount;
            }
            return pose;
        }

        static float Ease(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>앞 구간(0~peak)에 빠르게 뻗고 나머지 구간에 천천히 돌아오는 곡선(0→1→0).</summary>
        static float Strike(float t, float peak = 0.35f) =>
            t < peak ? Ease(t / peak) : 1f - Mathf.SmoothStep(0, 1, (t - peak) / (1f - peak));

        Pose Action(string name, float t, Pose p)
        {
            switch (name)
            {
                case "jab":
                {
                    var k = Strike(t, 0.3f);
                    p.ArmR = Mathf.Lerp(p.ArmR, 90f, k);
                    p.ReachR = 0.45f * k;
                    p.Lean += 8f * k;
                    p.Forward = 0.04f * k;
                    break;
                }
                case "straight":
                {
                    var k = Strike(t, 0.35f);
                    p.ArmL = Mathf.Lerp(p.ArmL, 92f, k);
                    p.ReachL = 0.6f * k;
                    p.ArmR = Mathf.Lerp(p.ArmR, 60f, k);
                    p.Lean += 16f * k;
                    p.Forward = 0.07f * k;
                    break;
                }
                case "spin_kick":
                {
                    var k = Strike(t, 0.5f);
                    p.Spin = Mathf.Clamp01(t / 0.55f);
                    p.LegR = Mathf.Lerp(p.LegR, 95f, k);
                    p.KickR = 0.35f * k;
                    p.LegL = Mathf.Lerp(p.LegL, -12f, k);
                    p.ArmL = Mathf.Lerp(p.ArmL, 150f, k);
                    p.ArmR = Mathf.Lerp(p.ArmR, -40f, k);
                    p.Lean -= 12f * k;
                    p.Bob += 0.05f * k;
                    break;
                }
                case "rolling":
                {
                    var k = Strike(t, 0.2f);
                    p.Lean += 26f * k;
                    p.ArmL = Mathf.Lerp(p.ArmL, -45f, k);
                    p.ArmR = Mathf.Lerp(p.ArmR, -30f, k);
                    p.LegL = Mathf.Lerp(p.LegL, -35f, k);
                    p.LegR = Mathf.Lerp(p.LegR, 45f, k);
                    break;
                }
                case "vault":
                {
                    // 한 손으로 장애물을 짚고 다리를 접어 넘는다.
                    var k = Mathf.Sin(t * Mathf.PI);
                    p.Lean = 30f * k;
                    p.ArmR = Mathf.Lerp(p.ArmR, 150f, k);
                    p.ArmL = Mathf.Lerp(p.ArmL, 60f, k);
                    p.LegL = 75f * k;
                    p.LegR = 55f * k;
                    p.Bob += 0.04f * k;
                    break;
                }
                case "climb":
                {
                    var k = Mathf.Sin(Mathf.Clamp01(t * 1.3f) * Mathf.PI);
                    var alt = Mathf.Sin(t * Mathf.PI * 3f);
                    p.ArmL = 165f - alt * 20f * k;
                    p.ArmR = 165f + alt * 20f * k;
                    p.LegL = 70f * k + alt * 20f;
                    p.LegR = 30f * k - alt * 20f;
                    p.Lean = 10f * k;
                    break;
                }
                case "plunge":
                {
                    // 공중에서 두 주먹을 모아 위로 → 내리찍기 직전 자세(낙하하는 동안 유지)
                    p.ArmL = 170f;
                    p.ArmR = 175f;
                    p.LegL = 30f;
                    p.LegR = 60f;
                    p.Lean = -6f;
                    break;
                }
                case "land":
                {
                    var k = Strike(t, 0.15f);
                    p.ArmL = Mathf.Lerp(p.ArmL, 40f, k);
                    p.ArmR = Mathf.Lerp(p.ArmR, 35f, k);
                    p.ReachR = 0.3f * k;
                    p.LegL = Mathf.Lerp(p.LegL, -40f, k);
                    p.LegR = Mathf.Lerp(p.LegR, 70f, k);
                    p.Lean += 25f * k;
                    p.Bob -= 0.08f * k;
                    break;
                }
                case "overdrive":
                {
                    var k = Strike(t, 0.3f);
                    p.ArmL = Mathf.Lerp(p.ArmL, 155f, k);
                    p.ArmR = Mathf.Lerp(p.ArmR, 155f, k);
                    p.Lean -= 18f * k;
                    p.Head = 12f * k;
                    p.Bob += 0.06f * k;
                    break;
                }
                case "hurt":
                {
                    var k = Strike(t, 0.2f);
                    p.Lean -= 20f * k;
                    p.Head = 15f * k;
                    p.Forward = -0.05f * k;
                    break;
                }
                case "windup":
                {
                    // 적: 크게 들어 올리는 예비 동작(예고 원이 차오르는 동안)
                    var k = Ease(t);
                    p.ArmR = Mathf.Lerp(p.ArmR, 165f, k);
                    p.ArmL = Mathf.Lerp(p.ArmL, 120f, k);
                    p.Lean -= 14f * k;
                    p.Bob += Mathf.Sin(t * 60f) * 0.006f * k;
                    break;
                }
                case "smash":
                {
                    var k = Strike(t, 0.2f);
                    p.ArmR = Mathf.Lerp(165f, 30f, k);
                    p.ArmL = Mathf.Lerp(120f, 20f, k);
                    p.ReachR = 0.3f * k;
                    p.Lean += 22f * k;
                    p.Bob -= 0.05f * k;
                    break;
                }
                case "fire":
                {
                    var k = Strike(t, 0.15f);
                    p.Lean -= 18f * k;
                    p.Forward = -0.06f * k;
                    break;
                }
                default:
                    p.Lean += 10f * Strike(t);
                    break;
            }
            return p;
        }

        void Apply(Pose p, float dt)
        {
            _squash = Mathf.MoveTowards(_squash, 0, dt * 0.9f);
            // 2D에서 몸을 한 바퀴 도는 표현: 가로 폭을 cos로 줄였다 뒤집었다 되돌린다.
            var spinWidth = p.Spin > 0 && p.Spin < 1 ? Mathf.Cos(p.Spin * Mathf.PI * 2f) : 1f;
            if (Mathf.Abs(spinWidth) < 0.15f) spinWidth = spinWidth < 0 ? -0.15f : 0.15f;

            var visual = _rig.Visual;
            visual.localPosition = _rig.Rest(visual) + new Vector3(p.Forward, p.Bob, 0);
            visual.localRotation = Quaternion.Euler(0, 0, -p.Lean * 0.35f);
            visual.localScale = new Vector3((1 + _squash) * spinWidth, 1 - _squash, 1);

            if (_rig.Torso != null) _rig.Torso.localRotation = Quaternion.Euler(0, 0, -p.Lean * 0.65f);
            if (_rig.Head != null) _rig.Head.localRotation = Quaternion.Euler(0, 0, p.Head + p.Lean * 0.3f);
            Limb(_rig.ArmL, _rig.HandL, p.ArmL, p.ReachL);
            Limb(_rig.ArmR, _rig.HandR, p.ArmR, p.ReachR);
            Limb(_rig.LegL, null, p.LegL, 0);
            Limb(_rig.LegR, null, p.LegR, p.KickR);
        }

        /// <summary>팔·다리를 돌리고 뻗는다. 손은 늘어나지 않게 반대로 보정한다.</summary>
        static void Limb(Transform bone, Transform end, float angle, float reach)
        {
            if (bone == null) return;
            bone.localRotation = Quaternion.Euler(0, 0, angle);
            bone.localScale = new Vector3(1, 1 + reach, 1);
            if (end != null) end.localScale = new Vector3(1, 1f / (1 + reach), 1);
        }
    }
}
