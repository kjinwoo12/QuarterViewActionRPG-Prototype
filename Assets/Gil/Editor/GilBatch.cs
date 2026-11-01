using System;
using System.IO;
using System.Linq;
using Gil.Core.Util;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Gil.Editor
{
    /// <summary>
    /// 명령줄(-batchmode -executeMethod) 진입점. CI나 GUI 없는 검증에서 쓴다. 성공하면 종료 코드 0, 실패하면 1.
    ///
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod Gil.Editor.GilBatch.Setup -logFile -
    ///   Unity.exe -batchmode -nographics -projectPath . -executeMethod Gil.Editor.GilBatch.ValidateData -logFile -
    ///   Unity.exe -batchmode -projectPath . -executeMethod Gil.Editor.GilBatch.BuildWindows -logFile -
    ///   Unity.exe -batchmode -projectPath . -executeMethod Gil.Editor.GilBatch.BakeSprites -logFile -      (그래픽 필요: -nographics 없이)
    ///   Unity.exe -batchmode -projectPath . -executeMethod Gil.Editor.GilBatch.BakeSelfTest -logFile -
    ///   Unity.exe -batchmode -projectPath . -executeMethod Gil.Editor.GilBatch.BakeKayKitPlayer [-gilModel Rogue] -logFile -
    /// </summary>
    public static class GilBatch
    {
        /// <summary>프로젝트 설정(2D 렌더러·빌드 목록)을 적용·점검하고 데이터를 검증한다.</summary>
        public static void Setup() => Run(() =>
        {
            GilProjectSetup.EnsureAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var problems = GilProjectSetup.Check();
            foreach (var p in problems) Debug.LogError($"[GilBatch] 설정 점검: {p}");
            return CheckData() && problems.Count == 0;
        });

        public static void ValidateData() => Run(CheckData);

        /// <summary>Windows 빌드(플레이어 전용 코드 컴파일 확인). 결과는 Builds/Windows.</summary>
        public static void BuildWindows() => Run(() =>
        {
            GilProjectSetup.EnsureAll();
            var output = Path.Combine("Builds", "Windows", "GilSample.exe");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[GilBatch] 빌드 결과: {report.summary.result}, 오류 {report.summary.totalErrors}개, {report.summary.totalSize / (1024 * 1024)}MB → {output}");
            return report.summary.result == BuildResult.Succeeded;
        });

        /// <summary>프로젝트의 모든 SpriteBakeProfile을 굽는다.</summary>
        public static void BakeSprites() => Run(() =>
        {
            var count = SpriteBaker.BakeAll();
            Debug.Log($"[GilBatch] 스프라이트 베이크: 프로필 {count}개");
            return true;
        });

        /// <summary>
        /// Assets/ThirdParty/KayKit의 CC0 팩으로 주인공 프로필을 만들고 바로 굽는다. 모델 이름은 -gilModel 인자로 바꿀 수 있다(기본 Rogue).
        /// </summary>
        public static void BakeKayKitPlayer() => Run(() =>
        {
            var profile = KayKitBake.CreateOrUpdateProfile(Arg("-gilModel"));
            var folder = SpriteBaker.Bake(profile);
            var ok = SpriteBakerSelfTest.Verify(profile.CharacterId, folder);
            var screens = Path.Combine("Temp", "GilScreens");
            Directory.CreateDirectory(screens);
            foreach (var png in Directory.GetFiles(folder, "*.png"))
                File.Copy(png, Path.Combine(screens, "player_" + Path.GetFileName(png)), true);
            return ok;
        });

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>
        /// 외부 에셋 없이 베이커 전체 경로를 검증한다: 기본 도형으로 만든 인형 + 코드로 만든 클립 →
        /// 8방향 시트·art.json 출력 → 시트를 잘라 CharacterArt 에셋을 만드는지까지. 결과 이미지는 Temp/GilScreens에 남긴다.
        /// </summary>
        public static void BakeSelfTest() => Run(() =>
        {
            const string id = "_bake_selftest";
            var doll = SpriteBakerSelfTest.CreateDoll();
            var profile = ScriptableObject.CreateInstance<SpriteBakeProfile>();
            profile.CharacterId = id;
            profile.Model = doll;
            profile.Clips.Add(new SpriteBakeProfile.ClipEntry { Name = "idle", Clip = SpriteBakerSelfTest.IdleClip(), Fps = 8, Loop = true });
            profile.Clips.Add(new SpriteBakeProfile.ClipEntry { Name = "jab", Clip = SpriteBakerSelfTest.JabClip(), Fps = 12, Loop = false });
            var folder = SpriteBaker.Bake(profile);
            UnityEngine.Object.DestroyImmediate(doll);

            var ok = SpriteBakerSelfTest.Verify(id, folder);
            var screens = Path.Combine("Temp", "GilScreens");
            Directory.CreateDirectory(screens);
            foreach (var png in Directory.GetFiles(folder, "*.png"))
                File.Copy(png, Path.Combine(screens, "bake_" + Path.GetFileName(png)), true);
            AssetDatabase.DeleteAsset(folder);
            return ok;
        });

        static bool CheckData()
        {
            var report = GilProjectSetup.Validate(out var loadError);
            if (loadError != null)
            {
                Debug.LogError($"[GilBatch] 데이터 불러오기 실패: {loadError}");
                return false;
            }
            foreach (var d in report.Items)
            {
                if (d.Severity == Severity.Error) Debug.LogError($"[GilData] {d}");
                else Debug.LogWarning($"[GilData] {d}");
            }
            Debug.Log($"[GilBatch] 데이터 검증: 오류 {report.ErrorCount}개, 경고 {report.WarningCount}개");
            return !report.HasErrors;
        }

        static void Run(Func<bool> body)
        {
            var ok = false;
            try
            {
                ok = body();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            Debug.Log(ok ? "[GilBatch] 성공" : "[GilBatch] 실패");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
