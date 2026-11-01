using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gil.Runtime.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gil.Editor
{
    /// <summary>
    /// 굽기. 미리보기 씬(작업 중인 씬과 분리)에 모델·카메라·조명을 두고, 방향마다 모델을 돌려 프레임을 찍는다.
    /// 카메라는 게임 투영과 같은 각도(45° 회전, 30° 내려다보기 = 2:1 아이소)이고,
    /// 2D 렌더러가 기본이어도 3D 조명이 되도록 이 카메라만 Universal 렌더러로 그린다.
    /// </summary>
    public static class SpriteBaker
    {
        /// <summary>시트 한 줄 = 한 방향. 화면 아래(S)부터 시계 방향.</summary>
        public static readonly string[] Directions = { "S", "SW", "W", "NW", "N", "NE", "E", "SE" };

        public const string OutputRoot = CharacterArtBuilder.Root;

        [MenuItem("Gil/스프라이트 베이크 ▸ 모든 프로필", priority = 60)]
        public static void BakeAllMenu()
        {
            var count = BakeAll();
            EditorUtility.DisplayDialog("Gil 스프라이트 베이커", count > 0 ? $"{count}개 프로필을 구웠습니다." : "SpriteBakeProfile이 없습니다.", "확인");
        }

        public static int BakeAll()
        {
            var profiles = AssetDatabase.FindAssets("t:SpriteBakeProfile")
                .Select(g => AssetDatabase.LoadAssetAtPath<SpriteBakeProfile>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).ToList();
            foreach (var p in profiles) Bake(p);
            return profiles.Count;
        }

        /// <summary>지면 방향(게임 좌표) 중 화면에서 angle(도)로 보이는 방향. 투영의 역.</summary>
        public static Vector3 GroundDirectionForScreenAngle(float angle)
        {
            var a = angle * Mathf.Deg2Rad;
            // 화면 = ((x - z) / 2, (x + z) / 4) 이므로 x - z = 2cos, x + z = 4sin
            return new Vector3(Mathf.Cos(a) + 2f * Mathf.Sin(a), 0, 2f * Mathf.Sin(a) - Mathf.Cos(a)).normalized;
        }

        public static string Bake(SpriteBakeProfile profile)
        {
            if (profile.Model == null) throw new ArgumentException($"{profile.name}: Model이 비어 있습니다");
            if (profile.Clips.Count == 0 || profile.Clips.All(c => c.Clip == null))
                throw new ArgumentException($"{profile.name}: 클립이 없습니다");

            var folder = OutputRoot + profile.CharacterId;
            Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var rig = Setup(profile, scene);
                var clipJson = new List<string>();
                foreach (var entry in profile.Clips.Where(c => c.Clip != null))
                {
                    var sheet = BakeClip(profile, rig, entry);
                    var path = $"{folder}/{entry.Name}.png";
                    File.WriteAllBytes(path, sheet.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(sheet);
                    clipJson.Add($"    \"{entry.Name}\": {{ \"sheet\": \"{entry.Name}\", \"fps\": {entry.Fps.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"loop\": {(entry.Loop ? "true" : "false")} }}");
                }
                File.WriteAllText($"{folder}/art.json", Manifest(profile, rig.ModelHeight, clipJson), new UTF8Encoding(false));
                rig.Dispose();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.Refresh();
            CharacterArtBuilder.Build(folder);
            Debug.Log($"[GilBake] {profile.CharacterId}: {profile.Clips.Count}개 동작 × {Directions.Length}방향 → {folder}");
            return folder;
        }

        sealed class Rig : IDisposable
        {
            public GameObject Model;
            public Animator Animator;
            public Camera Camera;
            public RenderTexture Target;
            public float ModelHeight;

            public void Dispose()
            {
                if (Target != null)
                {
                    Target.Release();
                    UnityEngine.Object.DestroyImmediate(Target);
                }
            }
        }

        static Rig Setup(SpriteBakeProfile p, Scene scene)
        {
            var model = (GameObject)UnityEngine.Object.Instantiate(p.Model);
            SceneManager.MoveGameObjectToScene(model, scene);
            model.transform.position = Vector3.zero;
            var animator = model.GetComponentInChildren<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (p.HiddenParts.Contains(t.name)) t.gameObject.SetActive(false);
            if (p.TextureOverride != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    // 복사본 재질에만 적용해 원본 FBX 재질은 건드리지 않는다.
                    var materials = r.sharedMaterials.Select(m => m != null ? new Material(m) : null).ToArray();
                    foreach (var m in materials.Where(m => m != null))
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", p.TextureOverride);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", p.TextureOverride);
                    }
                    r.sharedMaterials = materials;
                }
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);

            var lightGo = new GameObject("BakeKeyLight");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = p.KeyLight;
            light.intensity = 1.3f;
            // 화면 왼쪽 위에서 비추는 빛(게임 소품 메시와 같은 방향)
            lightGo.transform.rotation = Quaternion.Euler(50f, 10f, 0f);

            var cameraGo = new GameObject("BakeCamera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.AddComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = p.CaptureHeight * 0.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.cullingMask = ~0;
            // 게임 투영과 같은 각도: 45° 돌리고 30° 내려다본다(2:1 아이소메트릭).
            camera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            var up = camera.transform.up;
            // 발(원점)이 칸 아래쪽 PivotY에 오도록 카메라 중심을 올린다.
            var lift = (0.5f - p.PivotY) * p.CaptureHeight / Vector3.Dot(Vector3.up, up);
            camera.transform.position = Vector3.up * lift - camera.transform.forward * 20f;
            UseUniversalRenderer(camera);

            var size = p.FrameSize * Mathf.Max(1, p.Supersample);
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, name = "BakeTarget" };
            target.Create();
            camera.targetTexture = target;

            RenderSettings.ambientLight = p.Ambient;
            return new Rig { Model = model, Animator = animator, Camera = camera, Target = target, ModelHeight = bounds.size.y };
        }

        /// <summary>프로젝트 기본이 2D 렌더러여도 이 카메라는 3D 조명이 되는 Universal 렌더러로 그린다.</summary>
        static void UseUniversalRenderer(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)) return;
            var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
            for (var i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (item != null && !(item is Renderer2DData))
                {
                    data.SetRenderer(i);
                    return;
                }
            }
            Debug.LogWarning("[GilBake] Universal(3D) 렌더러가 없어 2D 렌더러로 찍습니다 — 3D 조명이 빠질 수 있습니다.");
        }

        static Texture2D BakeClip(SpriteBakeProfile p, Rig rig, SpriteBakeProfile.ClipEntry entry)
        {
            var clip = entry.Clip;
            var frames = entry.FrameCount > 0 ? entry.FrameCount : Mathf.Max(1, Mathf.RoundToInt(clip.length * entry.Fps));
            var cell = p.FrameSize;
            var sheet = new Texture2D(cell * frames, cell * Directions.Length, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            sheet.SetPixels32(new Color32[sheet.width * sheet.height]);

            var graph = PlayableGraph.Create("GilBake");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Bake", rig.Animator);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            output.SetSourcePlayable(playable);
            graph.Play();

            try
            {
                for (var d = 0; d < Directions.Length; d++)
                {
                    var ground = GroundDirectionForScreenAngle(CharacterArt.DirectionAngles[Directions[d]]);
                    rig.Model.transform.rotation = Quaternion.LookRotation(ground);
                    for (var f = 0; f < frames; f++)
                    {
                        // 루프 동작은 마지막 프레임이 첫 프레임과 겹치지 않게 [0, 길이)에서 고르게 뽑는다.
                        var t = entry.Loop ? clip.length * f / frames : clip.length * f / Mathf.Max(1, frames - 1);
                        playable.SetTime(t);
                        graph.Evaluate(0f);
                        rig.Model.transform.position = Vector3.zero;
                        var pixels = Capture(rig);
                        var frame = Pixelate(p, pixels, rig.Target.width);
                        // 시트 좌표는 아래에서부터라 줄(방향) 순서를 뒤집는다(위 줄 = Directions[0]).
                        sheet.SetPixels32(f * cell, (Directions.Length - 1 - d) * cell, cell, cell, frame);
                    }
                }
            }
            finally
            {
                graph.Destroy();
            }
            sheet.Apply(false);
            return sheet;
        }

        static Color32[] Capture(Rig rig)
        {
            rig.Camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rig.Target;
            var tex = new Texture2D(rig.Target.width, rig.Target.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rig.Target.width, rig.Target.height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = previous;
            var pixels = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>
        /// 픽셀화: 크게 찍은 이미지를 칸 크기로 줄이면서(알파 가중 평균) 반투명을 잘라 선명한 외곽을 만들고,
        /// 색 단계를 줄여 셀 셰이딩처럼 정리한 뒤 1픽셀 외곽선을 두른다.
        /// </summary>
        public static Color32[] Pixelate(SpriteBakeProfile p, Color32[] source, int sourceSize)
        {
            var size = p.FrameSize;
            var k = Mathf.Max(1, sourceSize / size);
            var result = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (var sy = 0; sy < k; sy++)
                for (var sx = 0; sx < k; sx++)
                {
                    var c = source[(y * k + sy) * sourceSize + x * k + sx];
                    var ca = c.a / 255f;
                    r += c.r * ca;
                    g += c.g * ca;
                    b += c.b * ca;
                    a += ca;
                }
                var coverage = a / (k * k);
                if (coverage < p.AlphaCutoff || a <= 0) continue;
                result[y * size + x] = new Color32(Quantize(r / a, p.ColorSteps), Quantize(g / a, p.ColorSteps), Quantize(b / a, p.ColorSteps), 255);
            }
            if (!p.Outline) return result;

            var outlined = (Color32[])result.Clone();
            var outline = (Color32)p.OutlineColor;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (result[y * size + x].a != 0) continue;
                var near = (x > 0 && result[y * size + x - 1].a != 0) || (x < size - 1 && result[y * size + x + 1].a != 0)
                           || (y > 0 && result[(y - 1) * size + x].a != 0) || (y < size - 1 && result[(y + 1) * size + x].a != 0);
                if (near) outlined[y * size + x] = outline;
            }
            return outlined;
        }

        static byte Quantize(float value, int steps)
        {
            if (steps <= 1) return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
            var v = value / 255f;
            v = Mathf.Round(v * (steps - 1)) / (steps - 1);
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        static string Manifest(SpriteBakeProfile p, float modelHeight, List<string> clips)
        {
            // 카메라 화면 1m = 게임 화면 약 0.707유닛(아이소 투영의 가로 축척). 여기에 SD 배율을 곱한다.
            var unitsPerCaptureMeter = 0.707f * p.WorldScale;
            var ppu = p.FrameSize / (p.CaptureHeight * unitsPerCaptureMeter);
            var height = Mathf.Max(0.5f, modelHeight * unitsPerCaptureMeter * 0.95f);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"_comment\": \"Gil 스프라이트 베이커가 생성. 직접 고치지 말고 SpriteBakeProfile을 바꿔 다시 구울 것\",\n");
            sb.Append($"  \"frameWidth\": {p.FrameSize}, \"frameHeight\": {p.FrameSize},\n");
            sb.Append($"  \"pixelsPerUnit\": {ppu.ToString("0.###", inv)},\n");
            sb.Append($"  \"pivotX\": 0.5, \"pivotY\": {p.PivotY.ToString("0.###", inv)},\n");
            sb.Append($"  \"height\": {height.ToString("0.###", inv)},\n");
            if (!Mathf.Approximately(p.Brightness, 1f))
                sb.Append($"  \"brightness\": {p.Brightness.ToString("0.###", inv)},\n");
            if (p.HeartPixel != Vector2.zero)
            {
                var hx = (p.HeartPixel.x - p.FrameSize * 0.5f) / ppu;
                var hy = (p.HeartPixel.y - p.FrameSize * p.PivotY) / ppu;
                sb.Append($"  \"heartX\": {hx.ToString("0.###", inv)}, \"heartY\": {hy.ToString("0.###", inv)},\n");
            }
            sb.Append($"  \"directions\": [{string.Join(", ", Directions.Select(d => $"\"{d}\""))}],\n");
            sb.Append("  \"layout\": \"rows\",\n");
            sb.Append("  \"clips\": {\n");
            sb.Append(string.Join(",\n", clips));
            sb.Append("\n  }\n}\n");
            return sb.ToString();
        }
    }
}
