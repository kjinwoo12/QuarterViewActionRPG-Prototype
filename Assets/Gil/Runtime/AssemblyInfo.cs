using System.Runtime.CompilerServices;

// PlayMode 스모크 테스트가 대화 진행·대시 같은 내부 진입점을 직접 부를 수 있게 한다(입력 장치 흉내 없이).
[assembly: InternalsVisibleTo("Gil.Tests.PlayMode")]
// 에디터가 데이터 에셋(Data/*)을 채울 수 있게 한다(예전 JSON 변환).
[assembly: InternalsVisibleTo("Gil.Editor")]
