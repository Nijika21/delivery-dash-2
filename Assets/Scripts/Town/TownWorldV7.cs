using System;
using System.Collections.Generic;
using UnityEngine;

// Lapisan gambar kota v7 (Gerbang Desain 2 + 3): tanah SDF, marka, rumah, properti, gudang, dan kapsul selesai kerja.
// Semua ukuran dan posisi mengikuti penampil Gerbang 3 (Tools/town-baker/gate3-client.js), jadi Unity = pratinjau.
public partial class DeliveryTown
{
    // Urutan gambar: tanah < marka < benda datar < zona < benda berdiri (diurut y) < truk (0) < paket lompat/panah.
    public const int GroundOrder = -32000;
    public const int MarkingOrder = -31990;
    public const int FlatPropOrder = -31900;
    public const int ZoneOrder = -31000;
    public const float HouseSize = 6f;
    private static readonly Color MarkingColor = new Color32(0xA8, 0xA9, 0x98, 255);

    private readonly List<Vector2> houseCenters = new List<Vector2>();
    private readonly List<float> houseAngles = new List<float>();
    private Vector2 finishCenter;
    private Vector2 finishSize;
    private float finishAngle;
    private Rect warehouseRect;
    private Texture2D paveTexture;
    private Texture2D groundTexture;
    private Material groundMaterial;
    private Mesh groundMesh;
    private Mesh markingMesh;
    private Material markingMaterial;

    public Vector2 HouseCenter(int index) => index >= 0 && index < houseCenters.Count ? houseCenters[index] : Vector2.zero;
    public float HouseAngle(int index) => index >= 0 && index < houseAngles.Count ? houseAngles[index] : 0f;
    public Vector2 FinishCenter => finishCenter;
    public Vector2 FinishSize => finishSize;
    public float FinishAngle => finishAngle;
    // Titik asal paket lompat: pintu tengah gudang (sisi bawah gambar = muka gudang).
    public Vector2 LoadingDock => new Vector2(Depot.x, warehouseRect.yMin + 0.9f);
    public DeliveryContentCatalog Catalog => catalog;

    // Benda berdiri: yang lebih utara digambar lebih dulu. y ∈ [-200, 200] → -30000..-22000 (selalu di bawah truk).
    public static int StandingOrder(float y) => -30000 + Mathf.Clamp(Mathf.RoundToInt((200f - y) * 20f), 0, 8000);

    private void DrawWorldV7(TownLayoutData data)
    {
        Transform previous = detailRoot;
        detailRoot = new GameObject("Dunia v7").transform;
        detailRoot.SetParent(transform, false);

        BuildGround(data);
        BuildMarkings(data.markings);

        var houseRoot = Group("Rumah");
        for (int i = 0; i < data.houses.Length; i++)
        {
            TownHouse home = data.houses[i];
            string id = home.tutorial ? "tutorial" : home.type + "-" + home.colorway;
            Sprite sprite = catalog.World(id) ?? catalog.World("pelana-" + home.colorway);
            if (sprite == null) continue;
            Vector2 center = home.center.Vector;
            // Bayangan jatuh dengan offset dunia tetap (tidak ikut berputar).
            SpriteRenderer shadow = WorldSprite("Bayangan " + home.name, sprite, center + new Vector2(0.28f, -0.34f),
                Vector2.one * HouseSize, home.angle, StandingOrder(center.y) - 1, houseRoot);
            shadow.color = new Color(0.05f, 0.1f, 0.05f, 0.22f);
            WorldSprite("Rumah " + home.name, sprite, center, Vector2.one * HouseSize, home.angle, StandingOrder(center.y), houseRoot);
        }

        var propRoot = Group("Properti");
        Sprite[] originals = catalog.propSprites ?? Array.Empty<Sprite>();
        foreach (TownProp prop in data.props ?? Array.Empty<TownProp>())
        {
            Vector2 pos = prop.pos.Vector;
            Sprite sprite;
            Vector2 size;
            int order;
            if (prop.kind == "airMancur")
            {
                sprite = catalog.World("airMancur");
                size = Vector2.one * 18f;
                order = FlatPropOrder;
            }
            else
            {
                int original = prop.kind == "pohon" ? 0 : prop.kind == "pohon2" ? 1 : prop.kind == "semak" ? 2 : -1;
                sprite = original >= 0 ? (original < originals.Length ? originals[original] : null) : catalog.World(prop.kind);
                if (sprite == null) continue;
                float h = prop.height;
                size = original >= 0
                    ? new Vector2(h * sprite.bounds.size.x / sprite.bounds.size.y, h)
                    : Vector2.one * (h / 0.8f);
                pos.y += size.y * 0.36f;
                order = StandingOrder(prop.pos.y);
            }
            if (sprite == null) continue;
            SpriteRenderer renderer = WorldSprite(prop.kind, sprite, pos, size, 0f, order, propRoot);
            renderer.flipX = prop.flip;
        }

        Sprite depot = catalog.World("gudang");
        if (depot != null)
        {
            float height = warehouseRect.height;
            WorldSprite("Gudang paket", depot, warehouseRect.center, new Vector2(height * 512f / 300f, height), 0f,
                StandingOrder(warehouseRect.yMin), detailRoot);
        }
        detailRoot = previous;
    }

    private Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(detailRoot, false);
        return group;
    }

    public SpriteRenderer WorldSprite(string name, Sprite sprite, Vector2 center, Vector2 size, float angle, int order, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : detailRoot != null ? detailRoot : transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.material = flatMaterial;
        renderer.sortingOrder = order;
        Vector2 bounds = sprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, bounds.x), size.y / Mathf.Max(0.0001f, bounds.y), 1f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        go.transform.position = (Vector3)center - go.transform.rotation * Vector3.Scale(sprite.bounds.center, go.transform.localScale);
        return renderer;
    }

    private void BuildGround(TownLayoutData data)
    {
        TextAsset pave = data.id == "blok-paket" ? catalog.blokPaketPavementSdf : catalog.kotaPaketPavementSdf;
        TextAsset ground = data.id == "blok-paket" ? catalog.blokPaketGroundField : catalog.kotaPaketGroundField;
        Shader shader = catalog.townGroundShader != null ? catalog.townGroundShader : Shader.Find("Delivery Dash/Town Ground");
        TownField field = data.field;
        if (pave == null || ground == null || shader == null || field.nx <= 1 || field.ny <= 1)
        {
            Debug.LogWarning("Medan tanah v7 tidak lengkap; tanah digambar polos.");
            RectShape("Padang rumput v7", townExtent, new Color32(0x78, 0xA3, 0x55, 255), GroundOrder);
            return;
        }

        int count = field.nx * field.ny;
        byte[] paveBytes = pave.bytes;
        byte[] groundBytes = ground.bytes;
        if (paveBytes.Length != count * 2 || groundBytes.Length != count * 4)
            throw new InvalidOperationException($"Ukuran medan {data.id} tidak cocok dengan data kota.");

        bool half = SystemInfo.SupportsTextureFormat(TextureFormat.RHalf);
        if (half)
        {
            paveTexture = new Texture2D(field.nx, field.ny, TextureFormat.RHalf, false, true);
            var values = new ushort[count];
            for (int i = 0; i < count; i++)
                values[i] = Mathf.FloatToHalf((short)(paveBytes[i * 2] | paveBytes[i * 2 + 1] << 8) / 256f);
            paveTexture.SetPixelData(values, 0);
        }
        else
        {
            // Cadangan perangkat lama: jarak dikuantisasi 1/16 unit seperti kanal air/pelataran.
            paveTexture = new Texture2D(field.nx, field.ny, TextureFormat.R8, false, true);
            var values = new byte[count];
            for (int i = 0; i < count; i++)
            {
                float d = (short)(paveBytes[i * 2] | paveBytes[i * 2 + 1] << 8) / 256f;
                values[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(128f + d * 16f), 0, 255);
            }
            paveTexture.SetPixelData(values, 0);
        }
        paveTexture.name = "Medan aspal " + data.id;
        paveTexture.wrapMode = TextureWrapMode.Clamp;
        paveTexture.filterMode = FilterMode.Bilinear;
        paveTexture.Apply(false, true);

        groundTexture = new Texture2D(field.nx, field.ny, TextureFormat.RGBA32, false, true)
        {
            name = "Medan tanah " + data.id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        groundTexture.SetPixelData(groundBytes, 0);
        groundTexture.Apply(false, true);

        groundMaterial = new Material(shader) { name = "Tanah " + data.id };
        groundMaterial.SetTexture("_Pave", paveTexture);
        groundMaterial.SetTexture("_Ground", groundTexture);
        groundMaterial.SetFloat("_PaveQuant", half ? 0f : 1f);
        groundMaterial.SetVector("_Field", new Vector4(field.x0, field.y0, field.cell, 0f));
        groundMaterial.SetVector("_FieldSize", new Vector4(field.nx, field.ny, 0f, 0f));

        float x1 = field.x0 + (field.nx - 1) * field.cell, y1 = field.y0 + (field.ny - 1) * field.cell;
        groundMesh = new Mesh { name = "Tanah kota" };
        groundMesh.SetVertices(new[]
        {
            new Vector3(field.x0, field.y0), new Vector3(x1, field.y0), new Vector3(x1, y1), new Vector3(field.x0, y1)
        });
        groundMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        groundMesh.RecalculateBounds();
        var go = new GameObject("Tanah v7 (SDF)");
        go.transform.SetParent(detailRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = groundMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = groundMaterial;
        renderer.sortingOrder = GroundOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Marka putus-putus jalan (sudah berhenti sebelum persimpangan di baker), satu mesh.
    private void BuildMarkings(TownMarking[] markings)
    {
        if (markings == null || markings.Length == 0) return;
        const float halfWidth = 0.1f;
        var vertices = new List<Vector3>(markings.Length * 4);
        var colors = new List<Color>(markings.Length * 4);
        var triangles = new List<int>(markings.Length * 6);
        foreach (TownMarking m in markings)
        {
            Vector2 a = new Vector2(m.x0, m.y0), b = new Vector2(m.x1, m.y1);
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) continue;
            Vector2 n = new Vector2(-dir.y, dir.x).normalized * halfWidth;
            int k = vertices.Count;
            vertices.Add(a - n); vertices.Add(b - n); vertices.Add(b + n); vertices.Add(a + n);
            for (int i = 0; i < 4; i++) colors.Add(MarkingColor);
            triangles.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
        }
        markingMesh = new Mesh { name = "Marka jalan" };
        markingMesh.SetVertices(vertices);
        markingMesh.SetColors(colors);
        markingMesh.SetTriangles(triangles, 0);
        markingMesh.RecalculateBounds();
        var go = new GameObject("Marka jalan");
        go.transform.SetParent(detailRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = markingMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        Shader shader = catalog.vertexColorShader != null ? catalog.vertexColorShader : Shader.Find("Delivery Dash/Vertex Color");
        markingMaterial = shader != null ? new Material(shader) { name = "Marka jalan" } : null;
        renderer.sharedMaterial = markingMaterial != null ? markingMaterial : flatMaterial;
        renderer.sortingOrder = MarkingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void ReleaseWorldV7()
    {
        ReleaseAsset(groundMaterial);
        ReleaseAsset(paveTexture);
        ReleaseAsset(groundTexture);
        ReleaseAsset(groundMesh);
        ReleaseAsset(markingMesh);
        ReleaseAsset(markingMaterial);
    }
}
