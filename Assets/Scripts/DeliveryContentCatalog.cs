using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Delivery Dash/Content Catalog")]
public class DeliveryContentCatalog : ScriptableObject
{
    public Sprite packageSprite;
    public Sprite customerSprite;
    public Sprite starSprite;
    public Sprite boostSprite;
    public Sprite grassSprite;
    public Sprite straightRoad;
    public Sprite curveRoad;
    public Sprite tRoad;
    public Sprite crossRoad;
    public Sprite endRoad;
    public Sprite[] houseSprites;
    public Sprite[] propSprites;
    [Header("Delivery Dash 2")]
    public TruckSkinCatalog truckSkins;
    public Sprite[] publicSkinSprites;
    public Sprite kotaPaketMinimap;
    public Sprite blokPaketMinimap;
    public TextAsset kotaPaketLayout;
    public TextAsset blokPaketLayout;
    public TextAsset kotaPaketPavementSdf;
    public TextAsset blokPaketPavementSdf;
    public TextAsset kotaPaketGroundField;
    public TextAsset blokPaketGroundField;
    [Header("Dunia v7 (Assets/Art/World, dari Tools/art/export-world.mjs)")]
    public Sprite[] worldSprites;
    public Shader townGroundShader;
    public Shader zoneFillShader;
    public Shader vertexColorShader;
    // Direferensikan langsung supaya ikut ke build; Shader.Find saja membuat shader ini dibuang saat build.
    public Shader flatShader;

    private Dictionary<string, Sprite> worldLookup;

    // Id = nama file tanpa ekstensi, mis. "pelana-senja", "lampu", "zona-paket-penuh".
    public Sprite World(string id)
    {
        if (worldLookup == null)
        {
            worldLookup = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
            foreach (Sprite sprite in worldSprites ?? System.Array.Empty<Sprite>())
                if (sprite != null) worldLookup[sprite.name] = sprite;
        }
        return id != null && worldLookup.TryGetValue(id, out Sprite found) ? found : null;
    }
}
