using System;
using System.Collections.Generic;

namespace Gil.Core.Items
{
    /// <summary>
    /// 상평통보 화폐 단위. 값은 모두 푼 단위 정수로 다루고, 보일 때만 냥·전·푼으로 나눈다.
    /// 1냥 = 10전 = 100푼.
    /// </summary>
    public static class Money
    {
        public const int PunPerJeon = 10;
        public const int PunPerNyang = 100;

        /// <summary>"3냥 5전 2푼"처럼 쓴다. 0인 단위는 빼고, 0이면 "0푼".</summary>
        public static string Format(int pun)
        {
            if (pun == 0) return "0푼";
            var abs = Math.Abs(pun);
            var parts = new List<string>(3);
            if (abs / PunPerNyang > 0) parts.Add($"{abs / PunPerNyang}냥");
            if (abs % PunPerNyang / PunPerJeon > 0) parts.Add($"{abs % PunPerNyang / PunPerJeon}전");
            if (abs % PunPerJeon > 0) parts.Add($"{abs % PunPerJeon}푼");
            return (pun < 0 ? "-" : "") + string.Join(" ", parts);
        }
    }
}
