using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class DeliveryProjectSetup
    {
        private const string CatalogPath = "Assets/Resources/DeliveryContentCatalog.asset";

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>(CatalogPath) == null)
                {
                    Apply();
                }
            };
        }

        [MenuItem("Delivery Dash/Siapkan Proyek untuk Ponsel")]
        public static void Apply()
        {
            EnsureFolder("Assets/Resources");
            DeliveryContentCatalog catalog = AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DeliveryContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.packageSprite = Load("Assets/env1/package.png");
            catalog.customerSprite = Load("Assets/env1/Props & Stuff/monsterhead.png");
            catalog.starSprite = Load("Assets/env1/Props & Stuff/pickupitem.png");
            catalog.boostSprite = Load("Assets/env1/Props & Stuff/pickupspeed.png");
            catalog.grassSprite = Load("Assets/env1/grass.png");
            catalog.straightRoad = Load("Assets/env1/Road Pieces/straight.png");
            catalog.curveRoad = Load("Assets/env1/Road Pieces/curve.png");
            catalog.tRoad = Load("Assets/env1/Road Pieces/t.png");
            catalog.crossRoad = Load("Assets/env1/Road Pieces/cross.png");
            catalog.endRoad = Load("Assets/env1/Road Pieces/end.png");
            catalog.houseSprites = new[]
            {
                Load("Assets/env1/Houses/house1front.png"),
                Load("Assets/env1/Houses/house2front.png"),
                Load("Assets/env1/Houses/house3front.png"),
                Load("Assets/env1/Houses/house4green.png"),
                Load("Assets/env1/Houses/house4purple.png"),
                Load("Assets/env1/Houses/house5front.png")
            };
            catalog.propSprites = new[]
            {
                Load("Assets/env1/Props & Stuff/tree.png"),
                Load("Assets/env1/Props & Stuff/tree2.png"),
                Load("Assets/env1/Props & Stuff/bush.png"),
                Load("Assets/env1/Props & Stuff/streetlight.png"),
                Load("Assets/env1/Props & Stuff/rock1.png"),
                Load("Assets/env1/Props & Stuff/rock2.png")
            };
            catalog.kotaPaketLayout = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/World/kota-paket/town.unity.json");
            catalog.blokPaketLayout = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/World/blok-paket/town.unity.json");
            catalog.kotaPaketMinimap = Load("Assets/World/kota-paket/minimap-ui.png");
            catalog.blokPaketMinimap = Load("Assets/World/blok-paket/minimap-ui.png");
            catalog.kotaPaketPavementSdf = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/World/kota-paket/pavement-sdf.bytes");
            catalog.blokPaketPavementSdf = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/World/blok-paket/pavement-sdf.bytes");
            catalog.kotaPaketGroundField = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/World/kota-paket/ground-field.bytes");
            catalog.blokPaketGroundField = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/World/blok-paket/ground-field.bytes");
            var worldSprites = new System.Collections.Generic.List<Sprite>();
            if (AssetDatabase.IsValidFolder("Assets/Art/World"))
                foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/World" }))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                    if (sprite != null) worldSprites.Add(sprite);
                }
            worldSprites.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            catalog.worldSprites = worldSprites.ToArray();
            catalog.townGroundShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TownGround.shader");
            catalog.zoneFillShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/ZoneFill.shader");
            catalog.vertexColorShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/VertexColor.shader");
            catalog.flatShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            EditorUtility.SetDirty(catalog);

            PlayerSettings.companyName = "Delivery Dash Studio";
            PlayerSettings.productName = "Delivery Dash 2";
            PlayerSettings.bundleVersion = "2.0.0";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.deliverydash.dd2");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;

            AssetDatabase.SaveAssets();
            Debug.Log("Delivery Dash: katalog aset dan pengaturan ponsel selesai.");
        }

        [MenuItem("Delivery Dash/Build Web untuk Ponsel")]
        public static void BuildWebGl()
        {
            Apply();
            string outputPath = Path.GetFullPath("Builds/WebGL");
            Directory.CreateDirectory(outputPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { File.Exists("Assets/Scenes/KotaPaket.unity")
                    ? "Assets/Scenes/KotaPaket.unity" : "Assets/Scenes/SampleScene.unity" },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Build WebGL gagal: {report.summary.result}");
            }

            Debug.Log($"Delivery Dash: build WebGL selesai di {outputPath}");
        }

        [MenuItem("Delivery Dash/Build APK Android")]
        public static void BuildAndroid()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Hentikan Play sebelum build.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Pasang Android Build Support, SDK/NDK, dan OpenJDK melalui Unity Hub dahulu.");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Apply();
            ApplyAndroidIcons(PublicIconFolder);
            BuildApk(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(), "Builds/Android/DeliveryDash2.apk");
        }

        // Ikon aplikasi (keputusan pengguna 26–27 Sep): 2.0 = desain D1 (jalan angka 2), 1.0 = truk asli env1 miring di hijau
        // rumput. Tiap folder berisi legacy.png (512, kotak membulat), round.png (512), bg.png + fg.png (432, lapisan adaptive,
        // isi di area aman lingkaran 66%).
        public const string PublicIconFolder = "Assets/Art/AppIcon/DD2";
        public const string ClassicIconFolder = "Assets/Art/AppIcon/DD1";

        internal static void ApplyAndroidIcons(string folder)
        {
            Texture2D Load(string name)
            {
                string path = folder + "/" + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new FileNotFoundException("Ikon aplikasi tidak ditemukan: " + path);
                if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Texture2D legacy = Load("legacy"), round = Load("round"), background = Load("bg"), foreground = Load("fg");
            foreach (PlatformIconKind kind in new[] { AndroidPlatformIconKind.Adaptive, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy })
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (PlatformIcon icon in icons)
                {
                    if (kind == AndroidPlatformIconKind.Adaptive) icon.SetTextures(background, foreground);
                    else icon.SetTexture(kind == AndroidPlatformIconKind.Round ? round : legacy);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
        }

        // 1.0 "Delivery Dash" (keputusan pengguna 27 Sep): Classic apa adanya sebagai aplikasi sendiri (lobby Mulai/Keluar,
        // Keluar menutup aplikasi karena lobby 2.0 tidak ada di build), paket com.deliverydash.dd1 supaya terpasang
        // berdampingan dengan 2.0. Pengaturan 2.0 dikembalikan sesudah build.
        [MenuItem("Delivery Dash/Build APK Delivery Dash 1.0")]
        public static void BuildAndroidClassic()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Hentikan Play sebelum build.");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Apply();
            try
            {
                PlayerSettings.productName = "Delivery Dash";
                PlayerSettings.bundleVersion = "1.0.0";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.deliverydash.dd1");
                ApplyAndroidIcons(ClassicIconFolder);
                BuildApk(new[] { "Assets/Classic/Classic.unity" }, "Builds/Android/DeliveryDash.apk");
            }
            finally
            {
                Apply();
                ApplyAndroidIcons(PublicIconFolder);
            }
        }

        private static void BuildApk(string[] scenes, string output)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Pasang Android Build Support, SDK/NDK, dan OpenJDK melalui Unity Hub dahulu.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = false;
            EditorUserBuildSettings.buildAppBundle = false;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                // 2.0: semua scene aktif di Build Profile (KotaPaket + Classic); mode Classic memuat scene-nya lewat nama.
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4HC
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build Android gagal: " + report.summary.result);
            Debug.Log("APK_ANDROID_READY: " + report.summary.outputPath);
        }

        [MenuItem("Delivery Dash/Periksa Proyek")]
        public static void Validate()
        {
            Apply();
            string[] requiredAssets =
            {
                "Assets/Scenes/SampleScene.unity",
                "Assets/Driver.cs",
                "Assets/Scripts/DeliveryGameManager.cs",
                CatalogPath
            };

            foreach (string path in requiredAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw new InvalidOperationException($"Aset wajib tidak ditemukan: {path}");
                }
            }

            Debug.Log("DELIVERY_DASH_VALIDATION_OK");
        }

        private static Sprite Load(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Sprite tidak ditemukan: {path}");
            }

            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", folder);
        }
    }

    // Inspect the scene Unity actually builds, not whichever scene is open in the editor.
    public sealed class DeliverySceneBuildGuard : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (report == null || scene.name != "KotaPaket") return;
            DeliveryTown activeTown = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var town = root.GetComponent<DeliveryTown>();
                if (town != null && root.activeSelf) activeTown = town;
            }
            if (activeTown == null)
            {
                // D5: KotaPaket hanya bootstrap; kota v7 dibangun saat runtime dari katalog. Pastikan data kedua peta ikut.
                var catalog = AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>("Assets/Resources/DeliveryContentCatalog.asset");
                if (catalog == null || catalog.kotaPaketLayout == null || catalog.blokPaketLayout == null
                    || catalog.kotaPaketPavementSdf == null || catalog.blokPaketPavementSdf == null
                    || catalog.flatShader == null || catalog.townGroundShader == null || catalog.zoneFillShader == null || catalog.vertexColorShader == null)
                    throw new BuildFailedException("Katalog peta runtime belum lengkap. Jalankan Siapkan Proyek untuk Ponsel sebelum build.");
            }
            else if (activeTown.LayoutVersion < DeliveryTown.CurrentLayoutVersion || activeTown.BarrierCount == 0
                || activeTown.CollisionVersion != DeliveryTown.CurrentCollisionVersion)
                throw new BuildFailedException("Map baru belum tersimpan lengkap. Jalankan Perbarui Kota Berkelok sebelum build.");
            // Strip only the explicit backup from the build copy; retain it in the project.
            foreach (var root in scene.GetRootGameObjects())
                if (!root.activeSelf && root.name == "Kota sebelumnya (cadangan)" && root.GetComponent<DeliveryTown>() != null)
                    UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
