using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 8방향 프레임 스프라이트시트 애니메이터. 기본 루프(idle/run/air)에 동작(PlayAction)을 덮어 재생한다.
    /// 에셋에 없는 동작은 비슷한 동작으로 대체한다(예: straight → jab → attack).
    /// </summary>
    public sealed class SpriteSheetAnimator : MonoBehaviour, ICharacterAnimator
    {
        [SerializeField, Tooltip("캐릭터 외형 에셋(Assets/Gil/Art/Characters/{id}/{id}.asset)")] CharacterArt _art;
        [SerializeField, Tooltip("바라보는 방향을 읽어 올 시뮬레이션 오브젝트(지면 좌표)")] Transform _follow;
        [SerializeField] SpriteRenderer _body;
        [SerializeField, Tooltip("인공 심장 발광점(없으면 비워 둔다)")] Transform _heart;
        [SerializeField, Tooltip("피격 순간 하얗게 번쩍일 때 쓰는 조명 무시 머티리얼(Art/Materials/GilSpriteUnlit)")] Material _flashMaterial;

        Material _material;
        Color _color = Color.white;
        float _moveAmount;
        float _moveSpeedScale = 1f;
        bool _airborne;
        string _action;
        float _actionTime;
        float _actionDuration;
        float _loopTime;
        float _flashUntil;
        float _squash;
        bool _dead;
        float _deadTime;
        float _heartRate = 1.2f;
        Vector2 _screenFacing = new Vector2(0, -1);

        public bool IsPlaying => _action != null;

        void Awake()
        {
            if (_art == null || _art.Get("idle") == null)
            {
                Debug.LogError($"[GilArt] {name}: 캐릭터 외형 에셋이 없거나 idle 동작이 없습니다");
                enabled = false;
                return;
            }
            _color = new Color(_art.Brightness, _art.Brightness, _art.Brightness, 1f);
            _material = _body.sharedMaterial;
            _body.color = _color;
        }

        public void SetMoveAmount(float normalized) => _moveAmount = Mathf.Clamp01(normalized);
        public void SetMoveSpeedScale(float scale) => _moveSpeedScale = Mathf.Max(0.1f, scale);
        public void SetAirborne(bool airborne) => _airborne = airborne;
        public void SetHeartRate(float beatsPerSecond) => _heartRate = beatsPerSecond;
        public void Squash(float amount) => _squash = amount;

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
            _body.sharedMaterial = _flashMaterial;
            _body.color = Color.white;
        }

        public void Die()
        {
            _dead = true;
            _deadTime = 0;
        }

        public void ResetPose()
        {
            _dead = false;
            _action = null;
            _body.color = _color;
            _body.sharedMaterial = _material;
            _body.transform.localRotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            if (_art == null) return;
            var dt = Time.deltaTime;
            if (_follow != null)
            {
                var s = Iso.ToScreenDirection(_follow.forward);
                if (s.sqrMagnitude > 0.0001f) _screenFacing = s.normalized;
            }

            if (_flashUntil > 0 && Time.time >= _flashUntil)
            {
                _flashUntil = 0;
                _body.sharedMaterial = _material;
                _body.color = _color;
            }

            CharacterArt.Clip clip;
            float t;
            if (_dead)
            {
                _deadTime += dt;
                clip = _art.Find("die");
                if (clip == null)
                {
                    // 사망 동작이 없는 에셋: 어둡게 눕힌다.
                    clip = _art.Find("hurt") ?? _art.Get("idle");
                    _body.transform.localRotation = Quaternion.Euler(0, 0, 80f * Mathf.Clamp01(_deadTime / 0.3f));
                    _body.color = Color.Lerp(_color, new Color(0.3f, 0.3f, 0.35f), Mathf.Clamp01(_deadTime / 0.3f));
                }
                t = Mathf.Clamp01(_deadTime / 0.5f);
                Show(clip, t, false);
                return;
            }

            if (_action != null)
            {
                _actionTime += dt;
                clip = _art.Find(_action);
                if (clip != null)
                {
                    Show(clip, Mathf.Clamp01(_actionTime / _actionDuration), false);
                    if (_actionTime >= _actionDuration) _action = null;
                    UpdateExtras(dt);
                    return;
                }
                if (_actionTime >= _actionDuration) _action = null;
            }

            // 달리기 중: 전용 시트(sprint)가 있으면 그걸, 없으면 run을 배율만큼 빠르게 재생한다.
            var running = !_airborne && _moveAmount > 0.05f;
            var sprintClip = running && _moveSpeedScale > 1.01f ? _art.Find("sprint") : null;
            _loopTime += dt * (running && sprintClip == null ? _moveSpeedScale : 1f);
            clip = _airborne ? _art.Find("air") : sprintClip ?? (running ? _art.Find("run") : null);
            clip = clip ?? _art.Get("idle");
            Show(clip, _loopTime, true);
            UpdateExtras(dt);
        }

        /// <summary>loop면 time은 초, 아니면 0~1 진행도.</summary>
        void Show(CharacterArt.Clip clip, float time, bool loop)
        {
            var frames = _art.FramesFor(clip, _screenFacing, out var mirrored);
            if (frames == null || frames.Length == 0) return;
            int index;
            if (loop)
                index = Mathf.FloorToInt(time * clip.Fps) % frames.Length;
            else
                index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(time * frames.Length));
            _body.sprite = frames[Mathf.Max(0, index)];
            _body.flipX = mirrored;
            if (_heart != null && _art.Heart.HasValue)
                _heart.localPosition = new Vector3(_art.Heart.Value.x * (mirrored ? -1 : 1), _art.Heart.Value.y, 0);
        }

        void UpdateExtras(float dt)
        {
            _squash = Mathf.MoveTowards(_squash, 0, dt * 0.9f);
            _body.transform.localScale = new Vector3(1 + _squash, 1 - _squash, 1);
            if (_heart != null)
            {
                var beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * _heartRate)), 8f);
                _heart.localScale = Vector3.one * 0.07f * (1f + beat * 0.6f);
                // 뒤를 볼 때는 가슴이 가려진다.
                _heart.gameObject.SetActive(_screenFacing.y < 0.2f);
            }
        }
    }
}
