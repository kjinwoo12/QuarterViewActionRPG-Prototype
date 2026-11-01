using System.Linq;
using Gil.Core.Data;
using Gil.Core.Dialogue;
using Gil.Core.Quests;
using Gil.Runtime.Combat;
using Gil.Runtime.Visual;
using UnityEngine;

namespace Gil.Runtime.World
{
    /// <summary>출입구가 잠기는 조건. 씬 인스펙터에서 고를 수 있게 람다 대신 열거형 + 인자로 둔다.</summary>
    public enum PortalLock
    {
        None,
        /// <summary>플래그(LockArg)가 아직 없으면 잠김.</summary>
        RequireFlag,
        /// <summary>퀘스트(LockArg)를 아직 받지 않았으면 잠김.</summary>
        RequireQuestStarted,
        /// <summary>이 지역의 웨이브가 진행 중이면 잠김.</summary>
        WavesCleared,
    }
}
