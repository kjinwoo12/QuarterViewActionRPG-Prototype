using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 시뮬레이션 오브젝트(지면 좌표)를 따라다니는 화면 쪽 오브젝트.
    /// 위치를 아이소 투영한다. 따라갈 대상이 사라지면 스스로 사라진다.
    /// 앞뒤는 Unity 정렬(Custom Axis)이 이 오브젝트의 위치로 정한다(Iso 참고). Order in Layer는 SortingGroup에서 직접 정한다.
    /// 캐릭터처럼 여러 스프라이트로 된 외형은 SortingGroup으로 한 덩어리로 정렬된다.
    /// 에디터에서도 돌아서, 씬 뷰에서 시뮬레이션 오브젝트를 옮기면 외형이 바로 따라온다(편집 중에는 지우지 않는다).
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    public sealed class IsoView : MonoBehaviour
    {
        [SerializeField, Tooltip("따라갈 시뮬레이션 오브젝트(지면 좌표). 씬 뷰에서 이 오브젝트를 옮기면 외형이 따라온다")] Transform _follow;

        [Tooltip("같은 자리에서 앞뒤를 가르는 정렬 보정(단계). 양수면 앞으로 당긴다. 보이는 위치는 바뀌지 않는다")] public int SortBias;
        public Transform Follow => _follow;

        void LateUpdate()
        {
            if (_follow == null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            Sync();
        }

        public void Sync()
        {
            if (_follow == null) return;
            var p = _follow.position;
            var screen = Iso.ToScreen(p) + Iso.SortNudge(SortBias);
            // 편집 중에는 값이 바뀔 때만 써서 씬이 계속 수정됨으로 표시되지 않게 한다.
            if (transform.position != screen) transform.position = screen;
        }

        void OnDrawGizmosSelected()
        {
            if (_follow == null) return;
            // 지면 위치(발밑)와 외형을 잇는 선. 시뮬레이션 오브젝트가 어디 있는지 씬 뷰에서 바로 보인다.
            var ground = Iso.ToScreen(new Vector3(_follow.position.x, 0f, _follow.position.z));
            Gizmos.color = new Color(0.3f, 0.92f, 1f, 0.9f);
            Gizmos.DrawLine(ground, transform.position);
            Gizmos.DrawWireSphere(ground, 0.12f);
        }
    }
}
