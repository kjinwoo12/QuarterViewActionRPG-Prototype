using System.Collections.Generic;
using Gil.Runtime.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Gil.Runtime.World
{
    /// <summary>네온 간판 깜빡임. 대부분은 은은하게 숨 쉬고, 가끔 한 번씩 꺼졌다 켜진다.</summary>
    public sealed class NeonFlicker : MonoBehaviour
    {
        [SerializeField, Tooltip("깜빡일 간판 글자(월드 TextMeshPro)")] TMP_Text _label;
        [SerializeField, Tooltip("간판 기본 색")] Color _color;
        [SerializeField, Tooltip("간판마다 깜빡임 박자를 어긋나게 하는 값")] float _phase;
        float _nextGlitch;

        void Start()
        {
            _nextGlitch = Time.time + 2f + Mathf.Abs(_phase) * 0.6f;
        }

        void Update()
        {
            if (_label == null) return;
            var k = 0.88f + Mathf.Sin(Time.time * 2.2f + _phase) * 0.12f;
            if (Time.time > _nextGlitch)
            {
                k = 0.25f;
                if (Time.time > _nextGlitch + 0.08f) _nextGlitch = Time.time + Random.Range(3f, 9f);
            }
            _label.color = new Color(_color.r * k, _color.g * k, _color.b * k, _color.a);
        }
    }
}
