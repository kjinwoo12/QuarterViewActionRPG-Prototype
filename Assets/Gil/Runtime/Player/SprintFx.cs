using Gil.Runtime.Visual;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gil.Runtime.Player
{
    /// <summary>
    /// 달리기(Sprint) 연출: 발밑에서 달리는 반대 방향으로 회갈색 먼지 퍼프를 걸음 간격마다 몽글 터뜨리고, 달리기 시작 순간에는 조금 더 큰 퍼프를 한 번 낸다.
    /// 먼지는 이 프리팹(Prefabs/Fx/SprintFx) 안의 ParticleSystem 하나로 뿌린다(실행 중에 오브젝트를 만들지 않는다). 입자 수명·커지며 흐려지는 곡선은 그 ParticleSystem에서 고친다.
    /// Player 프리팹 안에 배치돼 있고, PlayerController의 달리기 상태를 매 프레임 읽는다.
    /// </summary>
    [DefaultExecutionOrder(110)] // IsoView(100)가 외형 위치를 맞춘 뒤 정렬 순서를 따라간다.
    public sealed class SprintFx : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("달리기 상태를 읽을 플레이어")] PlayerController _player;
        [SerializeField, Tooltip("플레이어 외형의 정렬 그룹(먼지를 플레이어 바로 뒤에 그린다)")] SortingGroup _sourceGroup;
        [SerializeField, Tooltip("먼지 입자")] ParticleSystem _dust;

        [Header("걸음 먼지")]
        [SerializeField, Tooltip("퍼프를 터뜨리는 걸음 간격(초)"), Range(0.05f, 0.6f)] float _stepInterval = 0.16f;
        [SerializeField, Tooltip("한 걸음에 나오는 입자 수"), Range(1, 20)] int _stepCount = 4;
        [SerializeField, Tooltip("걸음 퍼프 입자 크기(시작)")] float _stepSize = 0.12f;
        [SerializeField, Tooltip("뒤로 퍼지는 속도(m/s, 화면 기준)")] float _speed = 0.9f;
        [SerializeField, Tooltip("퍼지는 각도(도, 반대 방향 기준 좌우)"), Range(0f, 90f)] float _spread = 35f;

        [Header("출발 먼지")]
        [SerializeField, Tooltip("달리기 시작 순간 나오는 입자 수"), Range(0, 40)] int _startCount = 9;
        [SerializeField, Tooltip("출발 퍼프 입자 크기(시작)")] float _startSize = 0.2f;

        [Header("색")]
        [SerializeField, Tooltip("먼지 색(밝은 쪽)")] Color _colorA = new Color(0.72f, 0.66f, 0.58f, 0.75f);
        [SerializeField, Tooltip("먼지 색(어두운 쪽). 입자마다 두 색 사이에서 고른다")] Color _colorB = new Color(0.52f, 0.46f, 0.4f, 0.75f);

        bool _wasSprinting;
        float _sinceStep;

        void LateUpdate()
        {
            var sprinting = _player != null && _player.IsSprinting;
            if (sprinting)
            {
                if (!_wasSprinting)
                {
                    Puff(_startCount, _startSize, 1.3f);
                    _sinceStep = 0;
                }
                else
                {
                    _sinceStep += Time.deltaTime;
                    if (_sinceStep >= _stepInterval)
                    {
                        _sinceStep -= _stepInterval;
                        Puff(_stepCount, _stepSize, 1f);
                    }
                }
            }
            _wasSprinting = sprinting;
        }

        /// <summary>발밑에서 달리는 반대 방향(화면 좌표)으로 퍼프 하나.</summary>
        void Puff(int count, float size, float speedScale)
        {
            if (_dust == null || count <= 0) return;
            var feet = _player.transform.position;
            feet.y = Mathf.Max(0f, feet.y - 0.02f);
            var origin = Iso.ToScreen(feet);
            var back = Iso.ToScreen(feet - _player.Facing) - origin;
            back.z = 0;
            back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.down;

            var renderer = _dust.GetComponent<ParticleSystemRenderer>();
            if (_sourceGroup != null)
            {
                renderer.sortingLayerID = _sourceGroup.sortingLayerID;
                renderer.sortingOrder = _sourceGroup.sortingOrder - 1;
            }

            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (var i = 0; i < count; i++)
            {
                var dir = Quaternion.AngleAxis(Random.Range(-_spread, _spread), Vector3.forward) * back;
                p.position = origin + (Vector3)(Random.insideUnitCircle * 0.06f);
                p.velocity = (dir + Vector3.up * 0.25f) * _speed * speedScale * Random.Range(0.6f, 1.2f);
                p.startSize = size * Random.Range(0.75f, 1.25f);
                p.startColor = Color.Lerp(_colorA, _colorB, Random.value);
                _dust.Emit(p, 1);
            }
        }
    }
}
