using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gil.Core.Util;
using Gil.Runtime.Visual;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Gil.Editor
{
    /// <summary>
    /// 캐릭터 폴더(Assets/Gil/Art/Characters/{id}: art.json + 시트 PNG)를 게임이 쓰는 형태로 만든다.
    /// 1) 시트마다 임포트 설정에서 칸(frameWidth × frameHeight)을 스프라이트로 자른다(이름 {시트}_{방향}_{번호}).
    /// 2) 동작 → 방향 → 프레임을 묶은 CharacterArt 에셋({id}.asset)을 만들거나 갱신한다. 이미 있으면 같은 에셋을 고쳐 씬 참조가 유지된다.
    /// 스프라이트 베이커가 굽고 나서 부르고, 시트를 손으로 바꿨을 때는 Gil ▸ 캐릭터 외형 다시 만들기 메뉴로 부른다.
    ///
    /// art.json 형식: frameWidth, frameHeight, pixelsPerUnit, pivotX/pivotY(칸 기준 0~1), brightness, heartX/heartY(유닛),
    /// directions(시트 위 줄부터 방향 이름), layout("rows" = 한 줄 한 방향, "files" = 방향마다 {시트}_{방향}.png),
    /// clips: { 이름: { sheet, fps, loop } }.
    /// </summary>
    public static class CharacterArtBuilder
    {
        public const string Root = "Assets/Gil/Art/Characters/";

        static readonly string[] DefaultDirections = { "S", "SW", "W", "NW", "N", "NE", "E", "SE" };

        [MenuItem("Gil/캐릭터 외형 다시 만들기 (Art/Characters)", priority = 61)]
        static void RebuildAllMenu()
        {
            var count = RebuildAll();
            EditorUtility.DisplayDialog("Gil", $"캐릭터 외형 {count}개를 다시 만들었습니다.", "확인");
        }

        public static int RebuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/'))) return 0;
            var count = 0;
            foreach (var dir in AssetDatabase.GetSubFolders(Root.TrimEnd('/')))
            {
                if (!File.Exists(Path.Combine(dir, "art.json"))) continue;
                Build(dir);
                count++;
            }
            return count;
        }

        /// <summary>folder: Assets/Gil/Art/Characters/{id}. 만든(갱신한) 에셋을 돌려준다.</summary>
        public static CharacterArt Build(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            var id = Path.GetFileName(folder);
            var manifestPath = folder + "/art.json";
            var root = new JsonObject(Json.Parse(File.ReadAllText(manifestPath)), manifestPath);

            var frame = new Vector2Int(root.Int("frameWidth"), root.Int("frameHeight"));
            var ppu = root.Float("pixelsPerUnit", 64f);
            var pivot = new Vector2(root.Float("pivotX", 0.5f), root.Float("pivotY", 0.1f));
            var files = root.String("layout", "rows") == "files";
            var directions = new List<string>(root.Strings("directions"));
            if (directions.Count == 0) directions.AddRange(DefaultDirections);
            foreach (var d in directions)
                if (!CharacterArt.DirectionAngles.ContainsKey(d))
                    throw new FormatException($"{manifestPath}: 알 수 없는 방향 '{d}' (E, NE, N, NW, W, SW, S, SE 중 하나)");
            Vector2? heart = root.Has("heartX") ? new Vector2(root.Float("heartX"), root.Float("heartY")) : (Vector2?)null;

            var clips = new List<CharacterArt.Clip>();
            var clipsJson = root.Object("clips");
            foreach (var name in clipsJson.Keys)
            {
                var c = clipsJson.Object(name);
                var clip = new CharacterArt.Clip { Name = name, Fps = c.Float("fps", 10f), Loop = c.Bool("loop", false) };
                var sheet = c.String("sheet", name);
                if (files)
                {
                    foreach (var dir in directions)
                    {
                        var path = $"{folder}/{sheet}_{dir}.png";
                        if (!File.Exists(path)) continue;
                        var frames = Slice(path, frame, ppu, pivot, new[] { dir });
                        clip.Directions.Add(new CharacterArt.Direction { Name = dir, Frames = frames[dir] });
                    }
                }
                else
                {
                    var path = $"{folder}/{sheet}.png";
                    if (!File.Exists(path))
                    {
                        Debug.LogWarning($"[GilArt] {id}: 시트 '{sheet}.png'가 없어 '{name}' 동작을 건너뜁니다");
                        continue;
                    }
                    var frames = Slice(path, frame, ppu, pivot, directions);
                    foreach (var dir in directions)
                        if (frames.TryGetValue(dir, out var f)) clip.Directions.Add(new CharacterArt.Direction { Name = dir, Frames = f });
                }
                if (clip.Directions.Count > 0) clips.Add(clip);
            }
            if (clips.All(c => c.Name != "idle")) throw new FormatException($"{manifestPath}: clips에 idle이 필요합니다");

            var assetPath = $"{folder}/{id}.asset";
            var art = AssetDatabase.LoadAssetAtPath<CharacterArt>(assetPath);
            var created = art == null;
            if (created) art = ScriptableObject.CreateInstance<CharacterArt>();
            art.SetBakeResult(frame, heart, clips);
            art.SetBrightness(root.Float("brightness", 1f));
            if (created) AssetDatabase.CreateAsset(art, assetPath);
            else EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GilArt] {assetPath}: 동작 {clips.Count}개 ({string.Join(", ", clips.Select(c => c.Name))})");
            return art;
        }

        /// <summary>
        /// 시트를 칸마다 스프라이트로 자르게 임포트 설정을 맞추고, 위 줄부터 rows[i] 방향의 프레임 배열을 돌려준다.
        /// 칸이 시트보다 많은 방향은 건너뛴다.
        /// </summary>
        static Dictionary<string, Sprite[]> Slice(string path, Vector2Int frame, float ppu, Vector2 pivot, IList<string> rows)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            // 픽셀 아트: 점 필터, 무압축, 밉맵 없음. 칸 크기가 같으므로 메시는 사각형 그대로(빈 칸도 자리를 지킨다).
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            // 축소 없이(npot 그대로, 최대 8192) 임포트했으므로 텍스처 크기 = 원본 크기.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var width = texture.width;
            var height = texture.height;
            var columns = Mathf.Max(1, width / frame.x);
            var rowCount = Mathf.Min(rows.Count, Mathf.Max(1, height / frame.y));
            var sheet = Path.GetFileNameWithoutExtension(path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            // 이미 자른 적이 있으면 같은 이름의 스프라이트 id를 유지한다(다른 에셋의 참조가 끊기지 않게).
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            for (var r = 0; r < rowCount; r++)
            {
                var y = height - (r + 1) * frame.y;
                for (var i = 0; i < columns; i++)
                {
                    var name = $"{sheet}_{rows[r]}_{i}";
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(i * frame.x, y, frame.x, frame.y),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = existing.TryGetValue(name, out var guid) ? guid : GUID.Generate(),
                    });
                }
            }
            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            var result = new Dictionary<string, Sprite[]>();
            for (var r = 0; r < rowCount; r++)
            {
                var frames = new Sprite[columns];
                for (var i = 0; i < columns; i++)
                    if (!sprites.TryGetValue($"{sheet}_{rows[r]}_{i}", out frames[i]))
                        throw new InvalidOperationException($"{path}: 스프라이트 {sheet}_{rows[r]}_{i}가 만들어지지 않았습니다");
                result[rows[r]] = frames;
            }
            return result;
        }
    }
}
