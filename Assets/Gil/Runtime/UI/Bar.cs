using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>가로 막대(체력/게이지). 채움 이미지의 오른쪽 앵커로 비율을 나타내 스프라이트 없이 동작한다.</summary>
    public sealed class Bar : MonoBehaviour
    {
        [SerializeField, Tooltip("채움 이미지. 오른쪽 앵커(anchorMax.x)를 비율로 움직인다.")] Image _fill;

        public RectTransform Root => (RectTransform)transform;
        public Image Fill => _fill;

        public void Set(float normalized)
        {
            var rt = _fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1);
        }

    }
}
