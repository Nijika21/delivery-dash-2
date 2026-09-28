using System;
using System.IO;
using DeliveryDash.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeliveryDash.Editor.Tests;

namespace DeliveryDash.Editor
{
    public static class DeliveryUiSpikeTools
    {
        private const string ScenePath = "Assets/Scenes/UiSpike.unity";
        private const string PanelSettingsPath = "Assets/UI/DeliveryDashPanelSettings.asset";

        [MenuItem("Delivery Dash/UI/Buat atau Perbarui Spike U0")]
        public static void CreateOrRefresh()
        {
            EnsureFolder("Assets/UI");
            EnsureFolder("Assets/Scenes");

            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "Delivery Dash Panel Settings";
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1280, 720);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100f;
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssets();

            Scene previous = SceneManager.GetActiveScene();
            if (previous.IsValid() && previous.isDirty)
            {
                throw new InvalidOperationException(
                    "Scene aktif memiliki perubahan yang belum disimpan. Simpan atau batalkan sendiri sebelum membuat spike U0.");
            }

            bool emptyUntitled = previous.IsValid() && string.IsNullOrEmpty(previous.path)
                && previous.rootCount == 0;
            bool replaceGeneratedScene = previous.IsValid()
                && string.Equals(previous.path, ScenePath, StringComparison.OrdinalIgnoreCase);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                emptyUntitled || replaceGeneratedScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(67, 112, 73, 255);
            camera.orthographic = true;

            GameObject rootObject = new GameObject("U0 UI Toolkit Spike", typeof(UIDocument), typeof(UiRoot), typeof(UiSpikeHarness));
            UIDocument document = rootObject.GetComponent<UIDocument>();
            document.panelSettings = panelSettings;

            UiRoot root = rootObject.GetComponent<UiRoot>();
            UiSpikeHarness harness = rootObject.GetComponent<UiSpikeHarness>();
            harness.Configure(
                root,
                LoadTree("Assets/UI/Screens/02-lobby-l.uxml"),
                LoadTree("Assets/UI/Screens/02-lobby-p.uxml"),
                LoadTree("Assets/UI/Screens/10-mengantar-paket-l.uxml"),
                LoadTree("Assets/UI/Screens/10-mengantar-paket-p.uxml"));

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!emptyUntitled && !replaceGeneratedScene)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("DELIVERY_DASH_U0_SCENE_READY: " + ScenePath);
        }

        public static void CreateAndValidateBatch()
        {
            CreateOrRefresh();
            ValidateStaticAssets();
        }

        public static void PrepareD5AssetsBatch()
        {
            ArtImportRules.ApplyAndBuildAtlases();
            TruckSkinBaker.Bake();
            CreateOrRefreshPanelSettings();
            DeliveryUiCatalogBuilder.Build();
            DeliveryProjectSetup.Apply();
            SceneArchiveSetup.PrepareLeanMainScene();
            ClassicSetup.Prepare();
            ValidateStaticAssets();
            TownV7Checks.ValidateAll();
            UiChecks.ValidateAll();
            TouchInputChecks.ValidateAll();
        }

        private static void CreateOrRefreshPanelSettings()
        {
            EnsureFolder("Assets/UI");
            PanelSettings settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Delivery Dash/UI/Periksa Aset Spike U0")]
        public static void ValidateStaticAssets()
        {
            string[] paths =
            {
                "Assets/UI/Screens/02-lobby-l.uxml",
                "Assets/UI/Screens/02-lobby-p.uxml",
                "Assets/UI/Screens/10-mengantar-paket-l.uxml",
                "Assets/UI/Screens/10-mengantar-paket-p.uxml"
            };

            foreach (string path in paths)
            {
                VisualTreeAsset tree = LoadTree(path);
                TemplateContainer clone = tree.CloneTree();
                if (clone.Q<VisualElement>("layar") == null)
                {
                    throw new InvalidOperationException("Root layar tidak ditemukan: " + path);
                }
            }

            TemplateContainer lobby = LoadTree(paths[0]).CloneTree();
            Require(lobby, "btn:mulai");
            Require(lobby, "iconbtn:help");
            Require(lobby, "iconbtn:gear");

            TemplateContainer delivery = LoadTree(paths[3]).CloneTree();
            Require(delivery, "iconbtn:pause");
            Require(delivery, "joy");

            Debug.Log("DELIVERY_DASH_U0_STATIC_OK");
        }

        private static VisualTreeAsset LoadTree(string path)
        {
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (tree == null)
            {
                throw new FileNotFoundException("UXML tidak dapat dimuat.", path);
            }

            return tree;
        }

        private static void Require(VisualElement root, string name)
        {
            if (root.Q<VisualElement>(name) == null)
            {
                throw new InvalidOperationException("Elemen wajib tidak ditemukan: " + name);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }
    }
}
