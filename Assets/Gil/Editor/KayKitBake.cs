using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gil.Editor
{
    /// <summary>
    /// KayKit(CC0) 캐릭터 + 애니메이션 팩으로 주인공 베이크 프로필을 자동으로 만든다.
    /// 팩은 Assets/ThirdParty/KayKit 아래 어디에 풀어도 된다. 모델 FBX를 이름으로 고르고(-gilModel 인자, 기본 Rogue),
    /// 같은 폴더의 모든 FBX에서 게임 동작 이름에 맞는 클립을 키워드로 찾는다(KayKit 클립은 Rig_Medium 공용 리그라 그대로 붙는다).
    /// 나중에 Mixamo 동작이나 VRoid 모델로 바꿀 때는 이 프로필 애셋의 Model/Clip 칸만 갈아 끼우면 된다.
    /// </summary>
    public static class KayKitBake
    {
        public const string Root = "Assets/ThirdParty/KayKit";
        public const string ProfilePath = "Assets/Gil/Art/Bake/PlayerKayKit.asset";

        /// <summary>게임 동작 → 클립 이름 후보(앞쪽이 우선, 소문자 포함 검사, 공백으로 나눈 단어는 모두 포함).</summary>
        static readonly (string name, float fps, bool loop, string[] keys)[] Moves =
        {
            ("idle", 8, true, new[] { "unarmed_idle", "idle_a", "idle" }),
            ("run", 12, true, new[] { "running_a", "run" }),
            ("jab", 14, false, new[] { "unarmed_melee_attack_punch_a", "punch_a", "punch" }),
            ("straight", 14, false, new[] { "unarmed_melee_attack_punch_b", "punch_b", "punch" }),
            ("spin_kick", 14, false, new[] { "unarmed_melee_attack_kick", "kick", "spin" }),
            ("rolling", 14, false, new[] { "dodge_forward", "dodge", "roll" }),
            ("vault", 12, false, new[] { "jump_full_short", "jump_full", "jump" }),
            ("climb", 12, false, new[] { "climb", "jump_start" }),
            ("air", 8, true, new[] { "jump_idle", "fall" }),
            ("land", 12, false, new[] { "jump_land", "land" }),
            ("overdrive", 12, false, new[] { "unarmed_pose", "cheer", "spellcast_raise" }),
            ("hurt", 12, false, new[] { "hit_a", "hit" }),
            ("die", 10, false, new[] { "death_a", "death" }),
        };

        [MenuItem("Gil/스프라이트 베이크 ▸ KayKit 주인공 프로필 만들기")]
        public static void CreateMenu()
        {
            var profile = CreateOrUpdateProfile(null);
            Selection.activeObject = profile;
        }

        public static SpriteBakeProfile CreateOrUpdateProfile(string modelName)
        {
            if (!AssetDatabase.IsValidFolder(Root))
                throw new InvalidOperationException($"{Root} 폴더가 없습니다. KayKit 캐릭터·애니메이션 팩(CC0)을 여기에 풀어 주세요.");
            var fbxPaths = AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (fbxPaths.Count == 0) throw new InvalidOperationException($"{Root} 아래에 FBX가 없습니다.");

            var model = PickModel(fbxPaths, string.IsNullOrEmpty(modelName) ? "rogue" : modelName.ToLowerInvariant());
            if (model == null) throw new InvalidOperationException("캐릭터 모델 FBX를 찾지 못했습니다.");

            var clips = fbxPaths
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .GroupBy(c => c.name).Select(g => g.First())
                .ToList();

            var profile = AssetDatabase.LoadAssetAtPath<SpriteBakeProfile>(ProfilePath);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ProfilePath));
                profile = ScriptableObject.CreateInstance<SpriteBakeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.CharacterId = "player";
            profile.Model = model;
            profile.CaptureHeight = 2.4f;
            profile.HeartPixel = new Vector2(34, 36);
            // 맨손 격투라 무기·망토는 숨기고, 재채색 텍스처(hero_texture.png)가 팩 폴더에 있으면 입힌다.
            profile.HiddenParts = new List<string> { "1H_Crossbow", "2H_Crossbow", "Knife", "Knife_Offhand", "Throwable", "Rogue_Cape" };
            profile.TextureOverride = AssetDatabase.FindAssets("hero_texture t:Texture2D", new[] { Root })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault();
            profile.Clips.Clear();
            var used = new HashSet<AnimationClip>();
            foreach (var move in Moves)
            {
                var clip = Find(clips, move.keys, used);
                if (clip == null)
                {
                    Debug.LogWarning($"[GilBake] KayKit: '{move.name}'에 맞는 클립 없음 — 런타임 대체 동작으로 넘어갑니다.");
                    continue;
                }
                // 같은 클립을 두 동작이 나눠 쓰면(jab/straight) 런타임에서 구분이 안 되니 가능한 한 다른 클립을 고른다.
                used.Add(clip);
                profile.Clips.Add(new SpriteBakeProfile.ClipEntry { Name = move.name, Clip = clip, Fps = move.fps, Loop = move.loop });
                Debug.Log($"[GilBake] KayKit: {move.name} ← {clip.name} ({clip.length:0.00}s)");
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GilBake] KayKit 프로필: 모델 {model.name}, 동작 {profile.Clips.Count}개 → {ProfilePath}");
            return profile;
        }

        static GameObject PickModel(List<string> fbxPaths, string wanted)
        {
            // 애니메이션 전용 FBX(Rig_Medium_* 등)는 렌더러가 없으니 제외하고, 이름이 정확히 맞는 것 → 포함하는 것 순으로.
            var models = fbxPaths
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(g => g != null && g.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                .ToList();
            return models.FirstOrDefault(g => g.name.ToLowerInvariant() == wanted)
                   ?? models.FirstOrDefault(g => g.name.ToLowerInvariant().Contains(wanted) && !g.name.ToLowerInvariant().Contains("hooded"))
                   ?? models.FirstOrDefault(g => g.name.ToLowerInvariant().Contains(wanted))
                   ?? models.FirstOrDefault();
        }

        static AnimationClip Find(List<AnimationClip> clips, string[] keys, HashSet<AnimationClip> used)
        {
            AnimationClip reused = null;
            foreach (var key in keys)
            {
                var words = key.Split(' ');
                var matches = clips.Where(c => words.All(w => c.name.ToLowerInvariant().Contains(w))).OrderBy(c => c.name.Length).ToList();
                var fresh = matches.FirstOrDefault(c => !used.Contains(c));
                if (fresh != null) return fresh;
                if (reused == null && matches.Count > 0) reused = matches[0];
            }
            return reused;
        }
    }
}
