using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TruckSkinBaker
{
    private const int CurrentVersion = 2;
    private const string CatalogPath = "Assets/Resources/TruckSkinCatalog.asset";
    private const string ArchivePath = "Assets/Archive/TruckSkinCatalogV1.asset";
    private static readonly string[] Slugs =
    {
        "kurir", "kepik", "awan", "pisang", "rimba",
        "zebra", "stroberi", "balap", "pelangi", "galaksi"
    };

    [MenuItem("Delivery Dash/Perbarui Corak Truk")]
    public static void Bake()
    {
        string hash = CalculateSourceHash();
        TruckSkinCatalog catalog = AssetDatabase.LoadAssetAtPath<TruckSkinCatalog>(CatalogPath);
        if (catalog != null && catalog.Version == CurrentVersion && catalog.SourceHash == hash
            && catalog.Skins != null && catalog.Skins.Length == Slugs.Length) return;

        if (catalog != null && catalog.Version < CurrentVersion
            && AssetDatabase.LoadAssetAtPath<TruckSkinCatalog>(ArchivePath) == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Archive"))
                AssetDatabase.CreateFolder("Assets", "Archive");
            if (!AssetDatabase.CopyAsset(CatalogPath, ArchivePath))
                throw new InvalidOperationException("Katalog skin v1 tidak dapat diarsipkan.");
        }
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<TruckSkinCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var sprites = new Sprite[Slugs.Length];
        Vector2Int expectedSize = default;
        for (int i = 0; i < Slugs.Length; i++)
        {
            string path = $"Assets/UI/Art/Skin/{Slugs[i]}.png";
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null) throw new InvalidOperationException("Sprite skin tidak ditemukan: " + path);
            var size = new Vector2Int((int)sprites[i].rect.width, (int)sprites[i].rect.height);
            if (i == 0) expectedSize = size;
            else if (size != expectedSize) throw new InvalidOperationException($"Ukuran skin {Slugs[i]} tidak sama.");
        }

        catalog.Skins = sprites;
        catalog.Version = CurrentVersion;
        catalog.SourceHash = hash;
        EditorUtility.SetDirty(catalog);

        DeliveryContentCatalog content = AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>("Assets/Resources/DeliveryContentCatalog.asset");
        if (content != null)
        {
            content.truckSkins = catalog;
            content.publicSkinSprites = sprites;
            EditorUtility.SetDirty(content);
        }

        AssetDatabase.SaveAssets();
        Validate(catalog);
        Debug.Log($"Skin v2 siap: {sprites.Length} sprite {expectedSize.x}x{expectedSize.y}, hash {hash.Substring(0, 12)}.");
    }

    private static string CalculateSourceHash()
    {
        using SHA256 sha = SHA256.Create();
        using var buffer = new MemoryStream();
        foreach (string slug in Slugs)
        {
            byte[] name = Encoding.UTF8.GetBytes(slug + "\n");
            buffer.Write(name, 0, name.Length);
            byte[] bytes = File.ReadAllBytes($"Assets/UI/Art/Skin/{slug}.png");
            buffer.Write(bytes, 0, bytes.Length);
        }
        return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void Validate(TruckSkinCatalog catalog)
    {
        if (catalog.Skins == null || catalog.Skins.Length != 10)
            throw new InvalidOperationException("Katalog skin publik harus berisi tepat 10 sprite.");
        if (CourierProgress.Prices.Length != 10 || CourierProgress.Prices[9] != 1200)
            throw new InvalidOperationException("Tabel harga skin berubah.");
        if ((1023 & ((1 << CourierProgress.Names.Length) - 1)) != 1023)
            throw new InvalidOperationException("Mask kepemilikan 10 skin tidak sah.");
    }
}
