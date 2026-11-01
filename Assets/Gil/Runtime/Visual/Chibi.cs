using System.Collections.Generic;
using UnityEngine;

namespace Gil.Runtime.Visual
{
    /// <summary>
    /// 임시 외형의 뼈대: root / visual / hip / torso / head / arm_L / arm_R / hand_L / hand_R / leg_L / leg_R.
    /// 오른쪽을 보는 자세 기준으로 _L이 먼 쪽(몸 뒤), _R이 가까운 쪽(몸 앞)이다. 왼쪽을 보면 root를 좌우 반전한다.
    /// 팔·다리 뼈의 회전 0°는 아래로 늘어뜨린 상태, +는 앞(바라보는 쪽)으로 드는 방향.
    /// 뼈와 부위 스프라이트는 씬·프리팹에 배치돼 있고, 이 클래스는 <see cref="ChibiAnimator"/>가 그 뼈를 찾아 쓰는 참조 묶음이다.
    /// </summary>
    [System.Serializable]
    public sealed class ChibiRig
    {
        public Transform Root;
        public Transform Visual;
        public Transform Hip;
        public Transform Torso;
        public Transform Head;
        public Transform ArmL;
        public Transform ArmR;
        public Transform HandL;
        public Transform HandR;
        public Transform LegL;
        public Transform LegR;
        public Transform Heart;

        /// <summary>정면(카메라 쪽)을 볼 때만 보이는 부위(얼굴, 가슴 장식).</summary>
        public List<GameObject> FrontOnly = new List<GameObject>();
        /// <summary>등을 보일 때만 보이는 부위(뒷머리).</summary>
        public List<GameObject> BackOnly = new List<GameObject>();

        public bool Hovering;
        [Tooltip("머리 위 표시를 띄울 높이(화면 유닛)")]
        public float Height = 1.2f;

        [SerializeField, HideInInspector] Vector3[] _rest = new Vector3[0];

        [System.NonSerialized] Transform[] _bones;

        Transform[] Bones => _bones != null && _bones.Length > 0 && _bones[0] == Visual
            ? _bones
            : _bones = new[] { Visual, Hip, Torso, Head, ArmL, ArmR, HandL, HandR, LegL, LegR };

        /// <summary>뼈의 기본 위치(배치할 때 기록해 둔 값).</summary>
        public Vector3 Rest(Transform bone)
        {
            if (bone == null) return Vector3.zero;
            var i = System.Array.IndexOf(Bones, bone);
            return i >= 0 && i < _rest.Length ? _rest[i] : bone.localPosition;
        }

        /// <summary>지금 뼈 위치를 기본 위치로 기록한다. 씬·프리팹에서 뼈를 옮겼다면 다시 불러 저장한다.</summary>
        public void CaptureRest()
        {
            var bones = Bones;
            _rest = new Vector3[bones.Length];
            for (var i = 0; i < bones.Length; i++)
                if (bones[i] != null) _rest[i] = bones[i].localPosition;
        }
    }
}
