using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>데이터(JSON)에 적힌 색 문자열을 읽고 밝기를 조절하는 도우미.</summary>
    public static class Colors
    {
        /// <summary>"#rrggbb" 또는 "rrggbb". 읽지 못하면 눈에 띄게 자홍색.</summary>
        public static Color Hex(string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c))
                return c;
            return Color.magenta;
        }

        /// <summary>밝기만 바꾼 색(알파 유지).</summary>
        public static Color Shade(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }
}
