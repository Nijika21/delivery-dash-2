using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tangkapan dunia v7 tanpa Play Mode: kota dibangun di scene kosong sementara (tidak disimpan),
// lalu kamera ortografis dirender ke PNG di Logs/captures/dunia-*.png. Jalankan batch TANPA -nographics.
public static class WorldPreviewCapture
{
    private struct Shot { public string Name; public Vector2 Center; public float Size; public bool Portrait; }

    [MenuItem("Delivery Dash/Tangkap Pratinjau Dunia v7")]
    public static void Capture()
    {
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("WORLD_CAPTURE_SKIPPED: scene aktif punya perubahan belum disimpan.");
            return;
        }
        var catalog = AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>("Assets/Resources/DeliveryContentCatalog.asset");
        Directory.CreateDirectory("Logs/captures");
        int written = 0;
        foreach (string map in new[] { "kota-paket", "blok-paket" })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TextAsset layout = map == "blok-paket" ? catalog.blokPaketLayout : catalog.kotaPaketLayout;
            var town = new GameObject("Kota").AddComponent<DeliveryTown>();
            town.Build(catalog, layout);

            // Contoh tujuan supaya zona, pickup, dan panah ikut terlihat.
            StopZoneView.Create(town, "paket", town.Depot, 0f, null).SetProgress(0.6f);
            StopZoneView.Create(town, "kapsul", town.FinishCenter, town.FinishAngle, null).SetProgress(0.3f);
            int house = Mathf.Min(3, town.Houses.Count - 1);
            StopZoneView.Create(town, "rumah", town.Houses[house].Stop, town.HouseAngle(house), null).SetProgress(0.85f);
            StopZoneView.Create(town, "surat", town.LetterDropoff, 0f, null);
            if (town.TryActivityStop(0, out Vector2 stop, out _)) StopZoneView.Create(town, "bantuan", stop, 0f, null).SetProgress(0.4f);
            PickupView.Create(town, "bintang", town.StartPosition + new Vector2(4f, -6f), town.transform);
            PickupView.Create(town, "kilat", town.StartPosition + new Vector2(7f, -6f), town.transform);
            foreach (var zone in Object.FindObjectsByType<StopZoneView>(FindObjectsSortMode.None))
            {
                zone.transform.localScale = FullScale(zone);
                zone.GetComponent<SpriteRenderer>().color = Color.white;
            }
            Sprite truck = catalog.publicSkinSprites != null && catalog.publicSkinSprites.Length > 0 ? catalog.publicSkinSprites[0] : null;
            if (truck != null)
                town.WorldSprite("Truk", truck, town.StartPosition, new Vector2(truck.bounds.size.x / truck.bounds.size.y * 2.2f, 2.2f),
                    town.StartHeading, 0);

            Vector2 depot = town.Depot;
            Vector2 houseCenter = town.HouseCenter(house);
            var shots = new[]
            {
                new Shot { Name = "gudang-l", Center = depot + new Vector2(0f, -2f), Size = 6.5f },
                new Shot { Name = "gudang-p", Center = depot + new Vector2(0f, -3f), Size = 10.5f, Portrait = true },
                new Shot { Name = "rumah-l", Center = houseCenter, Size = 6.5f },
                new Shot { Name = "tengah-l", Center = Vector2.zero, Size = 13f },
                new Shot { Name = "luas", Center = town.Extent.center, Size = town.Extent.height * 0.5f },
            };
            foreach (Shot shot in shots)
            {
                Render(town, shot, $"Logs/captures/dunia-{map}-{shot.Name}.png");
                written++;
            }
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log($"WORLD_CAPTURE_OK: {written} gambar di Logs/captures");
    }

    private static Vector3 FullScale(StopZoneView zone)
    {
        // Tangkapan diam: pakai skala penuh, bukan awal animasi muncul (0,6).
        return zone.transform.localScale / 0.6f;
    }

    private static void Render(DeliveryTown town, Shot shot, string path)
    {
        int width = shot.Portrait ? 720 : 1280, height = shot.Portrait ? 1280 : 720;
        var go = new GameObject("Kamera tangkap");
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = shot.Size;
        camera.transform.position = new Vector3(shot.Center.x, shot.Center.y, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(0x3A, 0x4A, 0x30, 255);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(go);
    }
}
