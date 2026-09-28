using System;
using System.Collections.Generic;
using System.Linq;
using DeliveryDash.Classic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.Editor
{
    public static class ClassicSetup
    {
        private const string SourceScene = "Assets/Scenes/SampleScene.unity";
        private const string ClassicScene = "Assets/Classic/Classic.unity";

        [MenuItem("Delivery Dash/Siapkan Classic 1.0")]
        public static void Prepare()
        {
            EnsureTag("Customer");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ClassicScene) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceScene, ClassicScene))
                    throw new InvalidOperationException("Scene Classic gagal disalin dari baseline.");
                var scene = EditorSceneManager.OpenScene(ClassicScene, OpenSceneMode.Single);
                foreach (Driver old in UnityEngine.Object.FindObjectsByType<Driver>(FindObjectsSortMode.None))
                {
                    GameObject target = old.gameObject;
                    UnityEngine.Object.DestroyImmediate(old);
                    target.AddComponent<ClassicDriver>();
                }
                foreach (Collision old in UnityEngine.Object.FindObjectsByType<Collision>(FindObjectsSortMode.None))
                {
                    GameObject target = old.gameObject;
                    UnityEngine.Object.DestroyImmediate(old);
                    if (target.GetComponent<ClassicCollision>() == null) target.AddComponent<ClassicCollision>();
                }
            }

            var classic = EditorSceneManager.OpenScene(ClassicScene, OpenSceneMode.Single);
            EnsureClassicLoop();
            EnsureHouses();
            EnsureDecor();
            EnsureLauncher();
            EditorSceneManager.MarkSceneDirty(classic);
            EditorSceneManager.SaveScene(classic, ClassicScene);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(item => item.path == ClassicScene)) scenes.Add(new EditorBuildSettingsScene(ClassicScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CLASSIC_SETUP_OK: scene terisolasi siap dan tidak memakai Courier.*.");
        }

        private static void EnsureClassicLoop()
        {
            ClassicDriver driver = UnityEngine.Object.FindFirstObjectByType<ClassicDriver>();
            if (driver == null) throw new InvalidOperationException("Mobil Classic tidak ditemukan.");
            GameObject car = driver.gameObject;
            if (car.GetComponent<ClassicCollision>() == null) car.AddComponent<ClassicCollision>();
            if (car.GetComponent<ParticleSystem>() == null)
            {
                ParticleSystem indicator = car.AddComponent<ParticleSystem>();
                var main = indicator.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSpeed = 5f;
                main.startSize = 0.35f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(244f / 255f, 164f / 255f, 66f / 255f));
                main.playOnAwake = false;
                var emission = indicator.emission;
                emission.rateOverTime = 30f;
                var renderer = indicator.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 50;
            }

        }

        // Konsep pengguna 27 Sep (bootcamp + sedikit improvisasi): lima rumah di ujung jalan buntu dan pulau rumput kecil
        // (ditandai pengguna di peta), semuanya pelanggan: paket boleh diantar ke rumah mana saja, tidak dikunci ke satu tujuan.
        // Kotak hijau latihan tabrakan diganti tempat paket dari aset asli env1 (Classic = game sebelum 2.0, jadi tanpa aset 2.0). Rumah lama di pinggir jalan dihapus.
        private const string HousesRoot = "Classic Houses";
        private const float HouseScale = 0.8f;
        // Pemicu lebih besar dari gambar supaya truk yang berhenti di ujung buntu (dibatasi dinding) tetap menyentuhnya.
        private const float TriggerPadding = 1.6f;

        private static readonly (string sprite, Vector2 at)[] Houses =
        {
            ("house1front", new Vector2(-18.47f, 25.72f)),     // pulau kecil kiri atas (Col27)
            ("house2front", new Vector2(17.27f, 22.8f)),       // ujung buntu atas kanan
            ("house3frontpink", new Vector2(12.4f, 3.2f)),     // ujung buntu kait (end_0)
            ("house4green", new Vector2(21.65f, 4.3f)),        // ujung buntu kanan (end_0 (1))
            ("house5front", new Vector2(-12.42f, -7.48f)),     // pulau kecil kiri bawah (Col28)
        };

        private static void EnsureHouses()
        {
            foreach (string stale in new[] { "Classic Customer", HousesRoot, "Classic Depot", YardRoot })
            {
                GameObject old = GameObject.Find(stale);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }
            var root = new GameObject(HousesRoot);
            foreach (var (sprite, at) in Houses)
            {
                var house = new GameObject(sprite) { tag = "Customer" };
                house.transform.SetParent(root.transform, false);
                house.transform.position = new Vector3(at.x, at.y, 0f);
                house.transform.localScale = Vector3.one * HouseScale;
                SpriteRenderer renderer = house.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite("Assets/env1/Houses/" + sprite + ".png");
                renderer.sortingOrder = 1;
                BoxCollider2D trigger = house.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = renderer.sprite.bounds.size + Vector3.one * TriggerPadding;
            }
            EnsurePassableIslands();
            EnsurePackageYard();
        }

        // Permintaan pengguna 28 Sep (seperti versi 1.0 asli): rumah di pulau rumput kecil bisa ditembus truk, jadi blok
        // kecil (≤ 4 unit) yang memuat rumah tidak lagi padat. Rumah di ujung jalan buntu tetap dibatasi dinding ujung
        // jalan supaya truk tidak keluar peta.
        // Hanya dua rumah pulau kecil (Col27, Col28); blok kecil di samping rumah ujung buntu (mis. Col13) tetap padat.
        private static readonly string[] IslandBlocks = { "Col27", "Col28" };

        private static void EnsurePassableIslands()
        {
            foreach (BoxCollider2D block in UnityEngine.Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
            {
                if (!block.name.StartsWith("Col")) continue;
                bool island = IslandBlocks.Contains(block.name);
                if (block.isTrigger == island) continue;
                block.isTrigger = island;
                Debug.Log((island ? "Pulau rumah bisa ditembus: " : "Blok padat lagi: ") + block.name);
            }
        }

        // Tempat paket menempati tapak kotak hijau (padat, 2,21 × 2,21) supaya jalur truk tidak berubah: pagar kayu terbuka ke
        // arah truk, tumpukan kardus, lampu jalan dan semak; semuanya aset asli env1. Posisi relatif pusat tapak, ukuran
        // = lebar (atau tinggi, untuk pagar tegak) terlihat dalam unit dunia.
        private const string YardRoot = "Classic Package Yard";
        private const string Props = "Assets/env1/Props & Stuff/";

        private static readonly (string path, Vector2 at, float size, bool byHeight, int order)[] YardParts =
        {
            (Props + "fence horizontal.png", new Vector2(-0.5f, 1.02f), 1.05f, false, 3),
            (Props + "fence horizontal.png", new Vector2(0.5f, 1.02f), 1.05f, false, 3),
            (Props + "fencevertical.png", new Vector2(1.05f, 0.5f), 1.05f, true, 3),
            (Props + "fencevertical.png", new Vector2(1.05f, -0.5f), 1.05f, true, 3),
            (Props + "fence horizontal.png", new Vector2(-0.5f, -1.02f), 1.05f, false, 6),
            (Props + "fence horizontal.png", new Vector2(0.5f, -1.02f), 1.05f, false, 6),
            ("Assets/env1/package.png", new Vector2(0.35f, 0.3f), 0.8f, false, 3),
            ("Assets/env1/package.png", new Vector2(-0.2f, 0.15f), 0.75f, false, 4),
            ("Assets/env1/package.png", new Vector2(0.2f, -0.3f), 0.8f, false, 5),
            (Props + "bush.png", new Vector2(0.95f, -0.95f), 0.6f, false, 7),
            (Props + "streetlight.png", new Vector2(-1.05f, 0.2f), 1.2f, true, 7),
        };

        private static void EnsurePackageYard()
        {
            GameObject square = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(item => item.gameObject)
                .FirstOrDefault(item => item.name == "Square" && Vector2.Distance(item.transform.position, new Vector2(5.41f, 1.89f)) < 0.5f);
            Vector3 at = square != null ? square.transform.position : new Vector3(5.41f, 1.89f, 0f);
            Vector2 footprint = square != null ? (Vector2)square.GetComponent<Collider2D>().bounds.size : new Vector2(2.21f, 2.21f);
            if (square != null) UnityEngine.Object.DestroyImmediate(square);
            var yard = new GameObject(YardRoot);
            yard.transform.position = at;
            yard.AddComponent<BoxCollider2D>().size = footprint;
            foreach (var (path, offset, size, byHeight, order) in YardParts)
            {
                Sprite sprite = LoadSprite(path);
                var part = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
                part.transform.SetParent(yard.transform, false);
                part.transform.localPosition = offset;
                Vector2 visible = sprite.bounds.size;
                part.transform.localScale = Vector3.one * (size / (byHeight ? visible.y : visible.x));
                SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = order;
            }
        }

        // Permintaan pengguna 27 Sep "mapnya kasih dekorasi, jangan plain": pohon, semak, batu, tunggul (aset asli env1)
        // disebar acak tetap (benih 27), seadanya: di petak rumput (blok Col*) dan di pinggir luar jalan sejauh yang terlihat
        // kamera (≤ 4 unit dari potongan jalan), bukan seluruh rumput. Titik dipakai hanya kalau di dalam blok Col (menjorok 0,6)
        // atau di luar semua potongan jalan; jauh dari rumah,
        // tempat paket, paket dan titik mulai truk. Tanpa collider: blok sudah padat, rumput luar tidak terjangkau.
        private const string DecorRoot = "Classic Decor";
        private static readonly (string sprite, float size, int weight)[] DecorKinds =
        {
            ("tree", 1.5f, 5), ("tree2", 1.4f, 4), ("bush", 0.9f, 5), ("rock1", 0.45f, 2), ("rock2", 0.55f, 2), ("treestump", 0.5f, 1),
        };

        private static void EnsureDecor()
        {
            GameObject old = GameObject.Find(DecorRoot);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(DecorRoot);
            var blocks = UnityEngine.Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
                .Where(item => item.name.StartsWith("Col") && !item.isTrigger).Select(item => item.bounds).ToList();
            string[] roadNames = { "neighbourhood", "straight", "l_", "t_", "curve", "end", "cross" };
            var roads = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(item => roadNames.Any(item.name.StartsWith)).Select(item => item.bounds).ToList();
            var keepClear = GameObject.FindGameObjectsWithTag("Customer").Select(item => (Vector2)item.transform.position)
                .Concat(GameObject.FindGameObjectsWithTag("Package").Select(item => (Vector2)item.transform.position))
                .Concat(new[] { new Vector2(5.41f, 1.89f), (Vector2)UnityEngine.Object.FindFirstObjectByType<ClassicDriver>().transform.position }).ToList();
            Bounds grass = GameObject.Find("grass_0").GetComponent<SpriteRenderer>().bounds;
            var sprites = DecorKinds.ToDictionary(kind => kind.sprite, kind => LoadSprite("Assets/env1/Props & Stuff/" + kind.sprite + ".png"));
            int totalWeight = DecorKinds.Sum(kind => kind.weight);
            var random = new System.Random(27);
            var placed = new List<Vector2>();
            bool InBlock(Vector2 at) => blocks.Any(box => at.x > box.min.x + 0.6f && at.x < box.max.x - 0.6f && at.y > box.min.y + 0.6f && at.y < box.max.y - 0.6f);
            bool Near(Vector2 at, float margin) => roads.Any(box => at.x > box.min.x - margin && at.x < box.max.x + margin && at.y > box.min.y - margin && at.y < box.max.y + margin);
            bool Roadside(Vector2 at) => !Near(at, 0.8f) && Near(at, 4f);
            for (float x = grass.min.x + 1f; x < grass.max.x - 1f; x += 1.7f)
                for (float y = grass.min.y + 1f; y < grass.max.y - 1f; y += 1.7f)
                {
                    var at = new Vector2(x + (float)(random.NextDouble() - 0.5) * 1.2f, y + (float)(random.NextDouble() - 0.5) * 1.2f);
                    double chance = random.NextDouble();
                    bool block = InBlock(at);
                    if (!block && !Roadside(at)) continue;
                    if (chance > (block ? 0.22 : 0.4)) continue;
                    if (keepClear.Any(point => Vector2.Distance(point, at) < 2.3f) || placed.Any(point => Vector2.Distance(point, at) < 1.2f)) continue;
                    int pick = random.Next(totalWeight);
                    var kind = DecorKinds.First(item => (pick -= item.weight) < 0);
                    var prop = new GameObject(kind.sprite);
                    prop.transform.SetParent(root.transform, false);
                    prop.transform.position = at;
                    Sprite sprite = sprites[kind.sprite];
                    float scale = kind.size / sprite.bounds.size.x * (0.85f + (float)random.NextDouble() * 0.3f);
                    prop.transform.localScale = new Vector3(random.Next(2) == 0 ? scale : -scale, scale, 1f);
                    SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    // Yang lebih bawah digambar di depan (tampak atas-miring seperti rumah).
                    renderer.sortingOrder = 1 + Mathf.Clamp(Mathf.RoundToInt((20f - at.y) * 0.5f), 0, 30);
                    placed.Add(at);
                }
            Debug.Log("Dekorasi Classic: " + placed.Count + " objek.");
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("Sprite rumah tidak ditemukan: " + path);
            return sprite;
        }

        private static void EnsureLauncher()
        {
            ClassicLauncher launcher = UnityEngine.Object.FindFirstObjectByType<ClassicLauncher>();
            // UI Classic dibangun kode tanpa aset 2.0 (PanelSettings, templat, font), jadi tidak ada yang dirujuk di sini.
            if (launcher == null) new GameObject("Lobby Classic 1.0").AddComponent<ClassicLauncher>();
        }

        private static void EnsureTag(string tag)
        {
            SerializedObject manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = manager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            manager.ApplyModifiedProperties();
        }
    }
}
