using UnityEngine;

namespace Gil.Runtime.UI
{
    /// <summary>
    /// 실행 중에 코드가 바꿔 칠하는 UI 색(선택된 메뉴, 심장 게이지, 알림 등). 에셋 하나(Assets/Gil/Settings/UiTheme.asset)를
    /// 화면 프리팹들이 함께 참조한다. 프리팹에 처음부터 칠해 둔 색은 프리팹에서 고친다.
    /// </summary>
    [CreateAssetMenu(menuName = "Gil/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("글자")]
        [Tooltip("강조(청록). 이름 없는 화자 등")] public Color Accent = new Color(0.3f, 0.92f, 1f);
        [Tooltip("따뜻한 강조(주황). 배너, 과부하 준비")] public Color AccentWarm = new Color(1f, 0.78f, 0.35f);
        [Tooltip("기본 글자")] public Color Text = new Color(0.95f, 0.95f, 0.93f);
        [Tooltip("보조 글자")] public Color DimText = new Color(0.7f, 0.72f, 0.75f);

        [Header("세로 메뉴")]
        [Tooltip("선택된 항목 배경")] public Color MenuSelected = new Color(0.3f, 0.92f, 1f, 0.22f);
        [Tooltip("선택되지 않은 항목 배경")] public Color MenuIdle = new Color(1f, 1f, 1f, 0.05f);

        [Header("HUD")]
        [Tooltip("심장(과부하) 게이지")] public Color Heart = new Color(1f, 0.22f, 0.32f);
    }
}
