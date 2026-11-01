using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>데모 끝(첫 사냥 보고 완료) 결산 화면.</summary>
    public sealed class DemoEndView : MenuView
    {
        public void Show(string stats, Action onKeepPlaying, Action onTitle)
        {
            Title.text = "체험판 끝 — 「진료소 → 장터 → 첫 사냥」";
            Body.text = stats;
            Menu.Clear();
            Menu.Add("계속 둘러보기", () => { Close(); onKeepPlaying(); });
            Menu.Add("타이틀로", () => { Close(); onTitle(); });
            LayoutMenu();
            Show();
            Systems.TimeController.PushPause();
        }

        void Close()
        {
            Hide();
            Systems.TimeController.PopPause();
        }
    }
}
