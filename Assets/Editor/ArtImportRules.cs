using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public sealed class ArtImportRules : AssetPostprocessor
{
    private const string PublicArt = "Assets/UI/Art/";
    // Sprite dunia v7 dari Tools/art/export-world.mjs. Zona tidak masuk atlas (shader isi-zona butuh UV 0..1).
    private const string WorldArt = "Assets/Art/World/";

    private void OnPreprocessTexture()
    {
        bool world = assetPath.StartsWith(WorldArt, StringComparison.Ordinal);
        if (!world && !assetPath.StartsWith(PublicArt, StringComparison.Ordinal)) return;
        int maxSize = world ? 1024 : 512;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = maxSize;
        importer.alphaIsTransparency = true;
        if (world)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100;
        }

        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = maxSize;
        android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);
    }

    [MenuItem("Delivery Dash/Siapkan Aset UI D5")]
    public static void ApplyAndBuildAtlases()
    {
        ReimportPngs(PublicArt);
        ReimportPngs(WorldArt);
        CreateAtlas("Assets/UI/DeliveryDashPublic.spriteatlas", "Assets/UI/Art");
        CreateAtlas("Assets/Art/World.spriteatlas", "Assets/Art/World/Rumah", "Assets/Art/World/Properti",
            "Assets/Art/World/Depot", "Assets/Art/World/Pickup", "Assets/Art/World/Panah");
        AssetDatabase.SaveAssets();
        Debug.Log("Aset UI D5 + dunia v7 siap: aturan impor dan atlas publik dan dunia terpisah.");
    }

    private static void ReimportPngs(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (string file in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
            AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
    }

    private static void CreateAtlas(string path, params string[] packablePaths)
    {
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, path);
        }
        SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        packing.padding = 4;
        atlas.SetPackingSettings(packing);
        TextureImporterPlatformSettings android = atlas.GetPlatformSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.ASTC_6x6;
        atlas.SetPlatformSettings(android);
        atlas.Remove(atlas.GetPackables());
        foreach (string packablePath in packablePaths)
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(packablePath);
            if (folder != null) atlas.Add(new[] { folder });
        }
        EditorUtility.SetDirty(atlas);
    }
}
