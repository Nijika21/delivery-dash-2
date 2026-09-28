using DeliveryDash.Editor;
using UnityEditor;
using UnityEngine;

// Satu langkah sesudah `node Tools/art/export-world.mjs` / `node Tools/town-baker/bake.mjs --out Assets/World`:
// impor ulang sprite dunia, bangun atlas, lalu isi katalog (sprite dunia, shader, data kota).
public static class WorldArtSetup
{
    [MenuItem("Delivery Dash/Siapkan Aset Dunia v7")]
    public static void Prepare()
    {
        AssetDatabase.Refresh();
        ArtImportRules.ApplyAndBuildAtlases();
        DeliveryProjectSetup.Apply();
        var catalog = AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>("Assets/Resources/DeliveryContentCatalog.asset");
        int sprites = catalog != null && catalog.worldSprites != null ? catalog.worldSprites.Length : 0;
        bool shaders = catalog != null && catalog.townGroundShader != null && catalog.zoneFillShader != null && catalog.vertexColorShader != null;
        Debug.Log($"WORLD_ART_READY: {sprites} sprite dunia, shader {(shaders ? "lengkap" : "KURANG")}.");
    }
}
