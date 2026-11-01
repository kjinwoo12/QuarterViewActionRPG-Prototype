using System;
using Gil.Core.Data;
#if UNITY_EDITOR
using Gil.Runtime.Data;
using UnityEditor;
#else
using NUnit.Framework;
#endif

namespace Gil.Tests
{
    /// <summary>
    /// 실제 기획 데이터(Assets/Gil/Data/GameData.asset)를 읽는다.
    /// 데이터 에셋은 Unity에서만 읽을 수 있으므로 Unity 없이 도는 dotnet test(tools/Gil.Core.Tests)에서는 이 데이터를 쓰는 테스트를 건너뛴다.
    /// </summary>
    public static class TestData
    {
#if UNITY_EDITOR
        static GameDatabase _cached;

        static GameData Asset =>
            AssetDatabase.LoadAssetAtPath<GameData>(GameData.AssetPath)
            ?? throw new InvalidOperationException($"{GameData.AssetPath}를 찾을 수 없습니다");

        /// <summary>실제 데이터. 테스트끼리 공유하므로 수정하지 말 것.</summary>
        public static GameDatabase Real => _cached ?? (_cached = Asset.Build());

        /// <summary>JSON 파일 하나(quests, dialogues, npcs, areas)만 바꿔 끼운 새 데이터베이스(검증기 테스트용).</summary>
        public static GameDatabase With(string fileName, Func<string, string> patch)
        {
            var asset = Asset;
            return asset.Build(name => name == fileName ? patch(asset.JsonText(name)) : null);
        }
#else
        const string NeedsUnity = "데이터 에셋(Assets/Gil/Data)은 Unity EditMode 테스트에서만 읽습니다";

        public static GameDatabase Real
        {
            get
            {
                Assert.Ignore(NeedsUnity);
                return null;
            }
        }

        public static GameDatabase With(string fileName, Func<string, string> patch)
        {
            Assert.Ignore(NeedsUnity);
            return null;
        }
#endif
    }
}
