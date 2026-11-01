using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>지역 전환용 검은 화면. 일시정지와 무관하게 실시간으로 진행한다.</summary>
    public sealed class FadeView : MonoBehaviour
    {
        [SerializeField] Image _image;

        public float Alpha => _image.color.a;

        public void SetInstant(float alpha)
        {
            StopAllCoroutines();
            Apply(alpha);
        }

        public IEnumerator To(float alpha, float duration)
        {
            var from = Alpha;
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Apply(Mathf.Lerp(from, alpha, t / duration));
                yield return null;
            }
            Apply(alpha);
        }

        void Apply(float alpha)
        {
            var c = _image.color;
            c.a = alpha;
            _image.color = c;
            // 완전히 투명할 때는 클릭을 막지 않는다.
            _image.raycastTarget = alpha > 0.01f;
            _image.transform.SetAsLastSibling();
        }
    }
}
