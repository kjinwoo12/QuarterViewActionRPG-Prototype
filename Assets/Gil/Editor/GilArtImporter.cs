using UnityEditor;
using UnityEngine;

namespace Gil.Editor
{
    /// <summary>
    /// 캐릭터 시트(Assets/Gil/Art/Characters 아래 PNG)를 처음 넣을 때 픽셀 아트 임포트 설정(점 필터, 무압축, 밉맵 없음)을 맞춘다.
    /// 칸 자르기와 CharacterArt 에셋 만들기는 <see cref="CharacterArtBuilder"/>가 art.json을 보고 한다(스프라이트 베이커가 자동으로 부른다).
    /// </summary>
    public sealed class GilArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(CharacterArtBuilder.Root)) return;
            // 이미 설정된 파일(다시 임포트)은 건드리지 않는다. 자르기 설정은 빌더가 정한다.
            if (!assetImporter.importSettingsMissing) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
        }
    }
}
