using UnityEngine;
using UnityEngine.Rendering;

namespace Gil.Runtime.Visual
{
    /// <summary>바닥 그림자(타원). 높이 올라가면 발밑 지면에 남아 공중에 있다는 걸 알려 준다.</summary>
    public sealed class GroundShadow : MonoBehaviour
    {
        [SerializeField] Transform _follow;
        [SerializeField] SpriteRenderer _sprite;
        float _groundHeight;

        /// <summary>그림자가 놓일 지면 높이(발판 위면 발판 높이).</summary>
        public void SetGround(float height) => _groundHeight = height;

        void LateUpdate()
        {
            if (_follow == null)
            {
                Destroy(gameObject);
                return;
            }
            var p = _follow.position;
            var air = Mathf.Max(0, p.y - _groundHeight);
            var onPlatform = _groundHeight > 0.05f;
            // 발판 위 그림자는 월드 층에서 발판보다 앞, 그 위에 선 주인보다는 살짝 뒤에 그려야 보인다. 바닥에서는 바닥 장식 층이다.
            transform.position = Iso.ToScreen(new Vector3(p.x, _groundHeight, p.z)) + (onPlatform ? Iso.SortNudge(-5) : Vector3.zero);
            var k = Mathf.Clamp(1f - air * 0.15f, 0.5f, 1f);
            transform.localScale = Vector3.one * k;
            _sprite.sortingOrder = onPlatform ? 0 : Iso.DecalOrder + 10;
        }
    }
}
