using System.Collections.Generic;
using Gil.Runtime.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Gil.Runtime.World
{
    /// <summary>위아래로 천천히 떠다니는 장식(홀로그램 등).</summary>
    public sealed class Hover : MonoBehaviour
    {
        Vector3 _base;
        float _phase;

        void Start()
        {
            _base = transform.localPosition;
            _phase = Random.value * 10f;
        }

        void Update() => transform.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * 1.5f + _phase) * 0.05f;
    }
}
