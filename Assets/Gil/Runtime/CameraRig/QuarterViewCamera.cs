using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.CameraRig
{
    /// <summary>
    /// 쿼터뷰(2:1 아이소메트릭) 2D 카메라. 화면 좌표계(XY)를 정면으로 보는 직교 카메라이고,
    /// 지면 좌표 대상을 투영해서 따라간다. 지역 경계 제한과 트라우마 방식 화면 흔들림을 담당한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] float _orthoSize = 3.9f;
        [SerializeField] float _followSmoothTime = 0.12f;
        [SerializeField] float _maxShakeOffset = 0.18f;
        /// <summary>캐릭터 발보다 조금 위(가슴 높이)를 화면 중앙에 둔다.</summary>
        [SerializeField] float _focusHeight = 0.9f;
        /// <summary>
        /// 경계에서 화면 사각형을 얼마나 영역 안에 넣을지(1 = 화면 모서리까지 전부 영역 안, 작을수록 가장자리 허공 허용).
        /// 마름모 영역에서 1이면 카메라가 거의 움직이지 못하므로 그보다 낮게 둔다.
        /// </summary>
        [SerializeField, Range(0f, 1f)] float _edgeTolerance = 0.55f;
        /// <summary>뒤쪽 벽·건물이 화면 위로 차지하는 높이(화면 유닛). 이만큼은 걷기 영역 밖이어도 보여 준다.</summary>
        [SerializeField] float _backWallHeight = 2.4f;

        Transform _target;
        Vector2 _focus;
        Vector2 _velocity;
        float _trauma;
        /// <summary>카메라 중심의 지면 좌표 범위(x = 지면 x, y = 지면 z).</summary>
        Rect? _groundBounds;

        public Camera Camera { get; private set; }

        /// <summary>입력의 "위/오른쪽"에 해당하는 지면 방향(화면 위 = 지면 +x+z 대각선).</summary>
        public static Vector3 ScreenUp => Iso.ScreenUp;
        public static Vector3 ScreenRight => Iso.ScreenRight;

        void Awake()
        {
            Camera = GetComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = _orthoSize;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100f;
            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(0, 0, -20f);
        }

        public void SetTarget(Transform target, bool snap)
        {
            _target = target;
            if (snap && target != null)
            {
                _focus = Clamp(Project(target.position));
                _velocity = Vector2.zero;
                Apply(Vector2.zero);
            }
        }

        /// <summary>
        /// 지역의 걷기 영역(지면 사각형)을 받아 카메라 중심의 이동 범위를 계산한다.
        /// 화면에서 걷기 영역은 마름모라 외접 사각형으로 막으면 모서리 쪽에서 허공이 크게 보인다.
        /// 그래서 지면 좌표(x, z) 축마다 화면 사각형이 차지하는 폭만큼 안쪽으로 줄여 막고,
        /// 영역이 화면보다 좁은 축은 가운데에 고정한다.
        /// </summary>
        public void SetBounds(Rect? walkable)
        {
            if (!walkable.HasValue)
            {
                _groundBounds = null;
                return;
            }
            var w = walkable.Value;
            var halfH = _orthoSize;
            var halfW = halfH * (Camera != null ? Camera.aspect : 16f / 9f);
            // 화면 사각형(±halfW, ±halfH)을 지면 축 x = sx + 2sy, z = 2sy - sx로 옮긴 반폭.
            var reach = (halfW + 2f * halfH) * _edgeTolerance;
            // 뒤쪽(화면 위)은 벽·건물이 서 있으니 그 높이만큼 더 보여도 된다.
            var back = 2f * _backWallHeight;
            _groundBounds = MinMax(w.xMin + reach, w.xMax + back - reach, w.yMin + reach, w.yMax + back - reach);
        }

        static Rect MinMax(float xMin, float xMax, float zMin, float zMax)
        {
            if (xMin > xMax) xMin = xMax = (xMin + xMax) * 0.5f;
            if (zMin > zMax) zMin = zMax = (zMin + zMax) * 0.5f;
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        public void SetBackground(Color color)
        {
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = color;
        }

        /// <summary>흔들림 추가(0~1). 여러 번 맞으면 누적되고 시간이 지나면 감쇠한다.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        /// <summary>지면 좌표 → 스크린 픽셀 좌표(UI 배치용).</summary>
        public Vector3 GroundToScreenPoint(Vector3 ground) => Camera.WorldToScreenPoint(Iso.ToScreen(ground));

        void LateUpdate()
        {
            if (_target != null)
                _focus = Vector2.SmoothDamp(_focus, Clamp(Project(_target.position)), ref _velocity, _followSmoothTime,
                    Mathf.Infinity, Time.unscaledDeltaTime);

            // 흔들림 세기는 trauma²에 비례 → 약한 타격은 은은하게, 강한 타격은 확실하게.
            var shake = Vector2.zero;
            if (_trauma > 0)
            {
                var power = _trauma * _trauma * _maxShakeOffset;
                var t = Time.unscaledTime * 30f;
                shake = new Vector2(Mathf.PerlinNoise(t, 0) - 0.5f, Mathf.PerlinNoise(0, t) - 0.5f) * 2f * power;
                _trauma = Mathf.Max(0, _trauma - Time.unscaledDeltaTime * 1.8f);
            }
            Apply(shake);
        }

        Vector2 Project(Vector3 ground) => Iso.ToScreen(ground + Vector3.up * _focusHeight);

        void Apply(Vector2 shake)
        {
            var p = _focus + shake;
            transform.position = new Vector3(p.x, p.y, -20f);
        }

        /// <summary>화면 좌표 p를 바닥(높이 0) 지면 좌표로 바꿔 경계 안으로 넣은 뒤 다시 화면 좌표로 돌린다.</summary>
        Vector2 Clamp(Vector2 p)
        {
            if (!_groundBounds.HasValue) return p;
            var b = _groundBounds.Value;
            var x = Mathf.Clamp(p.x + 2f * p.y, b.xMin, b.xMax);
            var z = Mathf.Clamp(2f * p.y - p.x, b.yMin, b.yMax);
            return Iso.ToScreen(x, 0, z);
        }
    }
}
