using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>사망 화면. 마고의 정비소에서 다시 깨어난다.</summary>
    public sealed class DeathView : MenuView
    {
        public void Show(Action onRespawn)
        {
            Title.text = "심장이 멎었다…";
            Body.text = "인공 심장이 마지막으로 한 번 뛰었다.\n누군가 당신을 정비소로 끌고 간다.";
            Menu.Clear();
            Menu.Add("정비소에서 다시 깨어나기", () => { Hide(); onRespawn(); });
            LayoutMenu(480f);
            Show();
        }
    }
}
