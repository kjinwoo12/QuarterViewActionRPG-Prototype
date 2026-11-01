using System.Collections.Generic;
using Gil.Runtime.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Gil.Runtime.World
{
    /// <summary>점광원 깜빡임(경광등, 고장 난 형광등).</summary>
    public sealed class LightPulse : MonoBehaviour
    {
        public Light2D Light;
        public float Speed = 4f;
        public float Min = 0.2f;
        public bool Strobe;
        float _base;

        void Start() => _base = Light != null ? Light.intensity : 1f;

        void Update()
        {
            if (Light == null) return;
            var s = Strobe ? (Mathf.Repeat(Time.time * Speed, 1f) < 0.5f ? 1f : Min) : Mathf.Lerp(Min, 1f, Mathf.Sin(Time.time * Speed) * 0.5f + 0.5f);
            Light.intensity = _base * s;
        }
    }
}
