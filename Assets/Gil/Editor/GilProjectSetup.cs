using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gil.Core.Data;
using Gil.Core.Util;
using Gil.Runtime.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gil.Editor
{
    /// <summary>
    /// 프로젝트 설정 점검과 적용. 에디터를 열 때는 점검만 하고(아무것도 바꾸지 않는다) 어긋난 곳을 경고로 알린다.
    /// 고치는 것은 Gil ▸ 프로젝트 설정 적용 메뉴나 명령줄(<see cref="GilBatch.Setup"/>)로 한다:
    /// 2D 렌더러(URP Renderer2D)를 기본 렌더러로, 빌드 목록(타이틀 → 시스템 → 플레이어 → UI → 지역 씬).
    /// 씬·머티리얼은 만들지 않는다 — Assets/Gil 아래 에셋이 정본이다.
    /// </summary>
    [InitializeOnLoad]
    public static class GilProjectSetup
    {
        public const string TitleScenePath = "Assets/Gil/Scenes/Title.unity";
        public const string SystemsScenePath = "Assets/Gil/Scenes/Systems.unity";
        public const string PlayerScenePath = "Assets/Gil/Scenes/Player.unity";
        public const string UiScenePath = "Assets/Gil/Scenes/UI.unity";
        public const string AreaSceneDir = "Assets/Gil/Scenes/Areas";
        public const string Renderer2DPath = "Assets/Gil/Settings/Gil2DRenderer.asset";

        static GilProjectSetup()
        {
            // 임포트 도중에는 에셋을 읽기 어려우므로 한 틱 뒤에 점검한다. 배치 실행은 각 명령이 스스로 점검·적용한다.
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var problems = Check();
                if (problems.Count > 0)
                    Debug.LogWarning("[Gil] 프로젝트 설정 점검: " + string.Join(" / ", problems) + "\nGil ▸ 프로젝트 설정 적용 메뉴로 고칠 수 있습니다.");
            };
        }

        /// <summary>어긋난 설정 목록(비어 있으면 정상). 아무것도 바꾸지 않는다.</summary>
        public static List<string> Check()
        {
            var problems = new List<string>();
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DPath);
            if (renderer == null) problems.Add($"2D 렌더러({Renderer2DPath})가 없습니다");
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || renderer == null) continue;
                var so = new SerializedObject(asset);
                var list = so.FindProperty("m_RendererDataList");
                var index = so.FindProperty("m_DefaultRendererIndex").intValue;
                if (index < 0 || index >= list.arraySize || list.GetArrayElementAtIndex(index).objectReferenceValue != renderer)
                    problems.Add($"{asset.name}의 기본 렌더러가 2D 렌더러가 아닙니다");
            }
            var wanted = WantedBuildScenes();
            var current = EditorBuildSettings.scenes;
            if (current.Length < wanted.Count || wanted.Where((p, i) => current[i].path != p || !current[i].enabled).Any())
                problems.Add("빌드 목록 앞쪽이 「타이틀 → 시스템 → 플레이어 → UI → 지역 씬」이 아닙니다");
            if (current.Any(s => !File.Exists(s.path))) problems.Add("빌드 목록에 없는 씬이 들어 있습니다");
            return problems;
        }

        /// <summary>설정 적용. 이미 맞춰져 있으면 아무것도 바꾸지 않는다.</summary>
        public static void EnsureAll()
        {
            Ensure2DRenderer();
            EnsureBuildSettings();
        }

        [MenuItem("Gil/프로젝트 설정 적용 (2D 렌더러·빌드 목록)", priority = 30)]
        static void EnsureAllMenu()
        {
            EnsureAll();
            AssetDatabase.SaveAssets();
            var problems = Check();
            EditorUtility.DisplayDialog("Gil 프로젝트 설정", problems.Count == 0 ? "설정이 맞습니다 ✓" : string.Join("\n", problems), "확인");
        }

        [MenuItem("Gil/타이틀 씬 열기", priority = 0)]
        public static void OpenTitleScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(TitleScenePath);
        }

        /// <summary>시스템 씬을 열고 플레이어·UI 씬을 함께 연다(게임 중 상주하는 씬 셋을 한눈에 본다). 시스템 씬이 활성 씬이다.</summary>
        [MenuItem("Gil/게임 씬 열기 (시스템·플레이어·UI)", priority = 1)]
        public static void OpenDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SystemsScenePath);
            EditorSceneManager.OpenScene(PlayerScenePath, OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(UiScenePath, OpenSceneMode.Additive);
        }

        [MenuItem("Gil/타이틀부터 플레이 ▶", priority = 2)]
        public static void PlayFromTitle()
        {
            OpenTitleScene();
            if (SceneManager.GetActiveScene().path == TitleScenePath) EditorApplication.isPlaying = true;
        }

        [MenuItem("Gil/게임 데이터 검증", priority = 20)]
        public static void ValidateData()
        {
            var report = Validate(out var loadError);
            if (loadError != null)
            {
                EditorUtility.DisplayDialog("Gil 데이터 검증", "불러오기 실패\n\n" + loadError, "확인");
                return;
            }
            foreach (var d in report.Items)
            {
                if (d.Severity == Severity.Error) Debug.LogError($"[GilData] {d}");
                else Debug.LogWarning($"[GilData] {d}");
            }
            var summary = report.Items.Count == 0
                ? "문제 없음 ✓"
                : $"오류 {report.ErrorCount}개, 경고 {report.WarningCount}개\n\n" +
                  string.Join("\n", report.Items.Take(15).Select(d => d.ToString()));
            EditorUtility.DisplayDialog("Gil 데이터 검증", summary, "확인");
        }

        [MenuItem("Gil/세이브 파일 삭제", priority = 40)]
        public static void DeleteSave()
        {
            var path = Path.Combine(Application.persistentDataPath, "gil_save.json");
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Gil", "세이브 파일이 없습니다.", "확인");
                return;
            }
            if (EditorUtility.DisplayDialog("Gil", $"세이브 파일을 삭제할까요?\n{path}", "삭제", "취소"))
                File.Delete(path);
        }

        /// <summary>데이터 에셋(GameData)을 읽어 검증한다(플레이하지 않고도 기획 데이터 오류를 잡기 위함).</summary>
        public static DiagnosticList Validate(out string loadError)
        {
            loadError = null;
            try
            {
                var data = AssetDatabase.LoadAssetAtPath<GameData>(GameData.AssetPath)
                           ?? throw new FileNotFoundException($"{GameData.AssetPath}이(가) 없습니다");
                var db = data.Build();
                return DataValidator.Validate(db);
            }
            catch (System.Exception e)
            {
                loadError = e.Message;
                return new DiagnosticList();
            }
        }

        /// <summary>
        /// 2D 렌더러를 만들어 프로젝트의 모든 URP 에셋에 추가하고 기본 렌더러로 지정한다.
        /// Light2D·스프라이트 조명은 2D 렌더러에서만 동작한다. 기존 렌더러는 목록에 남겨 둔다(되돌리기 쉽게).
        /// </summary>
        public static void Ensure2DRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DPath);
            if (renderer == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Renderer2DPath));
                renderer = CreateWithUrpMenu() ?? CreateManually();
                Debug.Log($"[Gil] 2D 렌더러 생성: {Renderer2DPath}");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var list = so.FindProperty("m_RendererDataList");
                var index = -1;
                for (var i = 0; i < list.arraySize; i++)
                {
                    var item = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (item == renderer) index = i;
                    // 후처리 데이터(Bloom 셰이더 등)는 기존 렌더러에서 빌려 온다.
                    if (item != null && item != renderer) CopyPostProcessData(item, renderer);
                }
                if (index < 0)
                {
                    index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
                }
                var defaultIndex = so.FindProperty("m_DefaultRendererIndex");
                if (defaultIndex.intValue != index || so.hasModifiedProperties)
                {
                    defaultIndex.intValue = index;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    Debug.Log($"[Gil] {asset.name}: 기본 렌더러를 2D 렌더러로 지정");
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>URP의 "2D Renderer 만들기" 메뉴와 같은 경로(내부 API)를 쓴다. 버전이 달라 못 찾으면 null.</summary>
        static Renderer2DData CreateWithUrpMenu()
        {
            try
            {
                var menus = System.Type.GetType("UnityEditor.Rendering.Universal.Renderer2DMenus, Unity.RenderPipelines.Universal.Editor");
                var rendererType = System.Type.GetType("UnityEngine.Rendering.Universal.RendererType, Unity.RenderPipelines.Universal.Runtime");
                var create = menus?.GetMethod("CreateRendererAsset",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (create == null || rendererType == null) return null;
                var type2D = System.Enum.Parse(rendererType, "_2DRenderer");
                return create.Invoke(null, new object[] { Renderer2DPath, type2D, false, "Renderer" }) as Renderer2DData;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Gil] URP 메뉴 경로로 2D 렌더러를 만들지 못해 직접 만듭니다: {e.Message}");
                return null;
            }
        }

        static Renderer2DData CreateManually()
        {
            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, Renderer2DPath);
            return renderer;
        }

        static void CopyPostProcessData(Object from, Renderer2DData to)
        {
            var target = new SerializedObject(to);
            var dst = target.FindProperty("m_PostProcessData");
            if (dst == null || dst.objectReferenceValue != null) return;
            var src = new SerializedObject(from).FindProperty("m_PostProcessData");
            if (src == null || src.objectReferenceValue == null) return;
            dst.objectReferenceValue = src.objectReferenceValue;
            target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(to);
        }

        static List<string> WantedBuildScenes()
        {
            var paths = new List<string> { TitleScenePath, SystemsScenePath, PlayerScenePath, UiScenePath };
            paths.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { AreaSceneDir })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p));
            return paths;
        }

        /// <summary>
        /// 빌드 목록 앞쪽을 「Title → Systems → Player → UI → Scenes/Areas의 지역 씬들」로 맞추고, 파일이 없는 항목은 뺀다(이미 맞으면 그대로).
        /// 씬 자체는 만들지 않는다.
        /// </summary>
        public static void EnsureBuildSettings()
        {
            var paths = WantedBuildScenes();
            foreach (var p in paths.Where(p => !File.Exists(p)))
                Debug.LogWarning($"[Gil] {p}이(가) 없습니다.");
            paths = paths.Where(File.Exists).ToList();
            var current = EditorBuildSettings.scenes;
            var others = current.Where(s => !paths.Contains(s.path) && File.Exists(s.path)).ToList();
            var next = paths.Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();
            if (next.Length == current.Length && next.Select((s, i) => s.path == current[i].path && current[i].enabled == s.enabled).All(ok => ok)) return;
            EditorBuildSettings.scenes = next;
            Debug.Log($"[Gil] 빌드 목록: {string.Join(", ", next.Select(s => s.path))}");
        }
    }
}
