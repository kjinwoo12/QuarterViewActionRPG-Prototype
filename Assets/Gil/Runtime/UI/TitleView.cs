using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>타이틀 화면.</summary>
    public sealed class TitleView : ModalView
    {
        [SerializeField] VerticalMenu _menu;
        [SerializeField] RectTransform _menuRect;
        [SerializeField] TMP_Text _footer;

        public void Open(bool hasSave, Action onNew, Action onContinue, Action onQuit)
        {
            _menu.Clear();
            _menu.Add("새로 시작", onNew);
            _menu.Add(hasSave ? "이어하기" : "이어하기  (저장 없음)", onContinue, hasSave);
            _menu.Add("종료", onQuit);
            // 위치는 프리팹(Prefabs/UI/Screens/Title)에 배치된 대로 두고 높이만 항목 수에 맞춘다.
            _menuRect.sizeDelta = new Vector2(_menuRect.sizeDelta.x, _menu.TotalHeight);
            Show();
        }

        public void Close() => Hide();

        void Update()
        {
            if (InputReady) _menu.HandleInput(Game.Input);
        }
    }
}
