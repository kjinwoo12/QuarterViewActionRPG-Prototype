using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>제목·본문·세로 메뉴로 이루어진 단순한 창들의 공통 구성.</summary>
    public abstract class MenuView : ModalView
    {
        [SerializeField] protected TMP_Text Title;
        [SerializeField] protected TMP_Text Body;
        [SerializeField] protected VerticalMenu Menu;
        [SerializeField] RectTransform _menuRect;

        /// <summary>메뉴를 패널 하단 가운데에 다시 배치한다(항목 수에 맞춰).</summary>
        protected void LayoutMenu(float width = 420f, float bottom = 40f)
        {
            _menuRect.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(width, Menu.TotalHeight));
        }

        protected virtual void Update()
        {
            if (InputReady) Menu.HandleInput(Game.Input);
        }
    }
}
