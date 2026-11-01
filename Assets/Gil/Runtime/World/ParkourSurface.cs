using System.Collections.Generic;
using Gil.Runtime.Visual;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Gil.Runtime.World
{
    /// <summary>
    /// 파쿠르 가능한 지형 표시. 플레이어가 대시로 부딪히면 높이에 따라 넘기(Vault) 또는 오르기(Climb)를 한다.
    /// 높이는 충돌체 윗면에서 읽으므로 레벨 디자이너는 체크만 하면 된다.
    /// </summary>
    public sealed class ParkourSurface : MonoBehaviour
    {
        public bool Vaultable;
        public bool Climbable;

        public float TopHeight
        {
            get
            {
                var c = GetComponent<Collider>();
                return c != null ? c.bounds.max.y : transform.position.y;
            }
        }
    }
}
