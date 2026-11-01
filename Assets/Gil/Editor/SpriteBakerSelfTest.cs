using System.IO;
using System.Linq;
using Gil.Runtime.Visual;
using UnityEditor;
using UnityEngine;

namespace Gil.Editor
{
    /// <summary>베이커 자체 검증용 인형과 클립(외부 에셋 없이).</summary>
    public static class SpriteBakerSelfTest
    {
        public static GameObject CreateDoll()
        {
            var root = new GameObject("BakeDoll");
            Part(root.transform, "body", PrimitiveType.Capsule, new Vector3(0, 0.9f, 0), new Vector3(0.45f, 0.45f, 0.3f), new Color(0.95f, 0.95f, 0.92f));
            Part(root.transform, "head", PrimitiveType.Sphere, new Vector3(0, 1.55f, 0), Vector3.one * 0.45f, new Color(1f, 0.56f, 0.72f));
            Part(root.transform, "nose", PrimitiveType.Cube, new Vector3(0, 1.55f, 0.22f), Vector3.one * 0.08f, new Color(0.9f, 0.2f, 0.2f));
            foreach (var (name, x) in new[] { ("arm_L", -0.3f), ("arm_R", 0.3f) })
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(x, 1.25f, 0);
                Part(pivot, "sleeve", PrimitiveType.Capsule, new Vector3(0, -0.3f, 0), new Vector3(0.15f, 0.3f, 0.15f), new Color(0.86f, 0.12f, 0.2f));
            }
            foreach (var x in new[] { -0.12f, 0.12f })
                Part(root.transform, "leg", PrimitiveType.Capsule, new Vector3(x, 0.35f, 0), new Vector3(0.18f, 0.35f, 0.18f), new Color(0.1f, 0.1f, 0.13f));
            root.AddComponent<Animator>();
            return root;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            renderer.sharedMaterial = material;
        }

        public static AnimationClip IdleClip()
        {
            var clip = new AnimationClip { name = "idle", frameRate = 30 };
            clip.SetCurve("head", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0, 1, 0));
            clip.SetCurve("head", typeof(Transform), "m_LocalPosition.y", new AnimationCurve(new Keyframe(0, 1.55f), new Keyframe(0.5f, 1.6f), new Keyframe(1, 1.55f)));
            clip.SetCurve("head", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0, 1, 0));
            return clip;
        }

        /// <summary>오른팔을 앞으로 뻗는 잽.</summary>
        public static AnimationClip JabClip()
        {
            var clip = new AnimationClip { name = "jab", frameRate = 30 };
            var keys = new[] { (0f, 0f), (0.15f, -95f), (0.4f, 0f) };
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            var w = new AnimationCurve();
            foreach (var (t, angle) in keys)
            {
                var q = Quaternion.Euler(angle, 0, 0);
                x.AddKey(t, q.x);
                y.AddKey(t, q.y);
                z.AddKey(t, q.z);
                w.AddKey(t, q.w);
            }
            clip.SetCurve("arm_R", typeof(Transform), "m_LocalRotation.x", x);
            clip.SetCurve("arm_R", typeof(Transform), "m_LocalRotation.y", y);
            clip.SetCurve("arm_R", typeof(Transform), "m_LocalRotation.z", z);
            clip.SetCurve("arm_R", typeof(Transform), "m_LocalRotation.w", w);
            return clip;
        }

        /// <summary>출력 파일과 런타임 읽기를 확인한다. 실패 사유는 로그 오류로 남긴다.</summary>
        public static bool Verify(string id, string folder)
        {
            var ok = true;
            void Fail(string message)
            {
                Debug.LogError("[GilBake 자체 검증] " + message);
                ok = false;
            }

            if (!File.Exists(Path.Combine(folder, "art.json"))) Fail("art.json이 없다");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>($"{folder}/{id}.asset");
            if (art == null)
            {
                Fail("CharacterArt 에셋이 만들어지지 않았다");
                return false;
            }
            foreach (var clip in new[] { "idle", "jab" })
            {
                var c = art.Get(clip);
                if (c == null)
                {
                    Fail($"{clip} 클립이 없다");
                    continue;
                }
                if (c.Directions.Count != SpriteBaker.Directions.Length) Fail($"{clip}: 방향 {c.Directions.Count}개 (8개여야 함)");
                if (c.Directions.Count == 0 || c.Directions.Any(d => d.Frames == null || d.Frames.Length == 0 || d.Frames.Any(f => f == null)))
                {
                    Fail($"{clip}: 비어 있는 프레임이 있다");
                    continue;
                }
                var tex = c.Directions[0].Frames[0].texture;
                var path = Path.Combine(folder, clip + ".png");
                var png = new Texture2D(2, 2);
                png.LoadImage(File.ReadAllBytes(path));
                var opaque = png.GetPixels32().Count(p => p.a > 0);
                // 칸마다 인형이 찍혀 있어야 한다(완전히 빈 시트면 카메라·조명·렌더러 설정이 틀린 것).
                var cells = (png.width / art.FrameSize.x) * (png.height / art.FrameSize.y);
                if (opaque < cells * 40) Fail($"{clip}: 불투명 픽셀 {opaque}개 — 칸 {cells}개에 비해 너무 적다(빈 화면?)");
                Debug.Log($"[GilBake 자체 검증] {clip}: {png.width}×{png.height}px, 칸 {cells}개, 불투명 {opaque}px, 시트 {tex.name}");
                Object.DestroyImmediate(png);
            }
            return ok;
        }
    }
}
