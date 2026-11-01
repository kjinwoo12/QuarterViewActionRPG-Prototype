using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gil.Runtime.UI
{
    /// <summary>머리 위로 떠오르는 피해 숫자·짧은 문구. 지면 좌표를 투영해 따라가며 위로 떠오르다 사라진다.</summary>
    public sealed class PopupView : MonoBehaviour
    {
        sealed class Popup
        {
            public TMP_Text Label;
            public Vector3 World;
            public float Born;
            public float Scale;
            public Vector2 Drift;
        }

        [SerializeField] RectTransform _root;
        [SerializeField, Tooltip("떠오르는 숫자 한 개의 프리팹. Prefabs/UI/PopupLabel")] TMP_Text _labelPrefab;
        [SerializeField, Tooltip("떠 있는 시간(초)")] float _lifetime = 0.8f;
        readonly List<Popup> _active = new List<Popup>();
        readonly Stack<TMP_Text> _pool = new Stack<TMP_Text>();

        public void Damage(Vector3 position, int amount, bool critical, bool onPlayer)
        {
            var color = onPlayer ? new Color(1f, 0.35f, 0.35f)
                : critical ? new Color(1f, 0.82f, 0.25f) : Color.white;
            Spawn(position, critical ? $"{amount}!" : amount.ToString(), color, critical ? 1.35f : 1f);
        }

        public void Text(Vector3 position, string text, Color color, float scale) => Spawn(position, text, color, scale);

        void Spawn(Vector3 position, string text, Color color, float scale)
        {
            var label = _pool.Count > 0 ? _pool.Pop() : CreateLabel();
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            _active.Add(new Popup
            {
                Label = label,
                World = position + Vector3.up * 2.0f,
                Born = Time.unscaledTime,
                Scale = scale,
                Drift = new Vector2(Random.Range(-30f, 30f), 0),
            });
        }

        TMP_Text CreateLabel() => Instantiate(_labelPrefab, _root);

        void LateUpdate()
        {
            var cam = Game.Camera;
            var canvasScale = _root.lossyScale.x > 0 ? _root.lossyScale.x : 1f;
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                var age = (Time.unscaledTime - p.Born) / _lifetime;
                if (age >= 1f || cam == null)
                {
                    p.Label.gameObject.SetActive(false);
                    _pool.Push(p.Label);
                    _active.RemoveAt(i);
                    continue;
                }
                var screen = cam.GroundToScreenPoint(p.World);
                var rise = Mathf.Sqrt(age) * 70f;
                p.Label.rectTransform.position = new Vector3(screen.x + p.Drift.x * age * canvasScale,
                    screen.y + rise * canvasScale, 0);
                // 처음에 툭 튀어나왔다가 줄어드는 느낌
                var pop = age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f;
                p.Label.rectTransform.localScale = Vector3.one * p.Scale * pop;
                var c = p.Label.color;
                c.a = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
                p.Label.color = c;
            }
        }
    }
}
