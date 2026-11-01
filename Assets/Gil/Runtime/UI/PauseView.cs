using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>일시정지 메뉴(Esc).</summary>
    public sealed class PauseView : MenuView
    {
        public void Open(Action onSave, bool canSave, Action onLoad, bool hasSave, Action onTitle)
        {
            Title.text = "일시정지";
            Body.text = "이동 방향키 · 달리기 Shift · 공격 Z · 구르기 X · 점프 C\n과부하 A · 회복약 S · 상호작용 Space";
            Menu.Clear();
            Menu.Add("계속하기", Close);
            Menu.Add("저장하기", () => { onSave(); Close(); }, canSave);
            Menu.Add("불러오기", () => { Close(); onLoad(); }, hasSave);
            Menu.Add("타이틀로", () => { Close(); onTitle(); });
            LayoutMenu();
            Show();
            Systems.TimeController.PushPause();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Hide();
            Systems.TimeController.PopPause();
        }

        protected override void Update()
        {
            if (InputReady && Game.Input.PausePressed)
            {
                Close();
                return;
            }
            base.Update();
        }
    }
}
