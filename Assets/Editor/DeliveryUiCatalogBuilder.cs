using DeliveryDash.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;

namespace DeliveryDash.Editor
{
    public static class DeliveryUiCatalogBuilder
    {
        private const string Path = "Assets/Resources/DeliveryUiCatalog.asset";

        [MenuItem("Delivery Dash/UI/Perbarui Katalog Layar D5")]
        public static void Build()
        {
            PrepareFonts();
            DeliveryUiCatalog catalog = AssetDatabase.LoadAssetAtPath<DeliveryUiCatalog>(Path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DeliveryUiCatalog>();
                AssetDatabase.CreateAsset(catalog, Path);
            }
            catalog.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DeliveryDashPanelSettings.asset");
            catalog.motion = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/UI/Motion/gerak.json");
            catalog.landscape = new VisualTreeAsset[30];
            catalog.portrait = new VisualTreeAsset[30];
            string[] files = System.IO.Directory.GetFiles("Assets/UI/Screens", "*-l.uxml");
            foreach (string file in files)
            {
                string name = System.IO.Path.GetFileName(file);
                if (!int.TryParse(name.Substring(0, 2), out int number)) continue;
                string normalized = file.Replace('\\', '/');
                catalog.landscape[number - 1] = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(normalized);
                catalog.portrait[number - 1] = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(normalized.Replace("-l.uxml", "-p.uxml"));
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Katalog layar UI D5 siap.");
        }

        [MenuItem("Delivery Dash/UI/Siapkan Font D5")]
        public static void PrepareFonts()
        {
            foreach (string name in new[] { "Fredoka-Bold", "Fredoka-SemiBold", "Andika-Regular", "Andika-Bold" })
            {
                string fontPath = "Assets/UI/Fonts/" + name;
                if (AssetDatabase.LoadAssetAtPath<FontAsset>(fontPath + " SDF.asset") != null) continue;
                Font source = AssetDatabase.LoadAssetAtPath<Font>(fontPath + ".ttf");
                if (source == null) throw new System.IO.FileNotFoundException("D5 font source missing: " + name);
                FontAsset asset = FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                asset.name = name + " SDF";
                AssetDatabase.CreateAsset(asset, fontPath + " SDF.asset");
                AssetDatabase.AddObjectToAsset(asset.material, asset);
                foreach (Texture2D texture in asset.atlasTextures)
                    AssetDatabase.AddObjectToAsset(texture, asset);
                EditorUtility.SetDirty(asset);
            }
            ApplyGalleryLineHeight("Fredoka-Bold", 974, 236, 1000);
            ApplyGalleryLineHeight("Fredoka-SemiBold", 974, 236, 1000);
            ApplyGalleryLineHeight("Andika-Regular", 2500, 800, 2048);
            ApplyGalleryLineHeight("Andika-Bold", 2500, 800, 2048);
            AssetDatabase.SaveAssets();
            foreach (string path in System.IO.Directory.GetFiles("Assets/UI/Styles", "*.uss"))
                AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            Debug.Log("D5_FONTS_READY");
        }

        // Galeri desain memakai line-height 1,15 (build-gate4.mjs .uss); USS tidak punya line-height, jadi kotak baris
        // ditiru lewat metrik font: tinggi baris = 1,15 em dan garis dasar di posisi CSS, yaitu setengah tinggi baris
        // + (ascent − descent)/2 dari atas. Ascent/descent = hhea/typo TTF (keduanya sama, USE_TYPO_METRICS).
        // Idempoten: dihitung dari angka TTF, bukan dari nilai aset yang mungkin sudah diubah.
        public const float GalleryLineHeight = 1.15f;

        private static void ApplyGalleryLineHeight(string name, float ascent, float descent, float unitsPerEm)
        {
            string path = "Assets/UI/Fonts/" + name + " SDF.asset";
            FontAsset asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (asset == null) throw new System.IO.FileNotFoundException("D5 font asset missing: " + path);
            FaceInfo face = asset.faceInfo;
            float em = face.pointSize;
            float lineHeight = GalleryLineHeight * em;
            float baseline = lineHeight * 0.5f + (ascent - descent) / unitsPerEm * em * 0.5f;
            face.lineHeight = lineHeight;
            face.ascentLine = baseline;
            face.descentLine = baseline - lineHeight;
            asset.faceInfo = face;
            EditorUtility.SetDirty(asset);
        }
    }
}
