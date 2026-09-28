using System;
using System.IO;
using DeliveryDash.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.Editor.Tests
{
    public static class UiChecks
    {
        private const string CatalogPath = "Assets/Resources/DeliveryUiCatalog.asset";

        [MenuItem("Delivery Dash/Periksa UI D5")]
        public static void ValidateAll()
        {
            DeliveryUiCatalog catalog = AssetDatabase.LoadAssetAtPath<DeliveryUiCatalog>(CatalogPath);
            Require(catalog != null, "katalog UI tersedia");
            Require(catalog.panelSettings != null, "Panel Settings terhubung");
            Require(catalog.landscape != null && catalog.landscape.Length >= 26, "katalog landscape lengkap");
            Require(catalog.portrait != null && catalog.portrait.Length >= 26, "katalog portrait lengkap");

            int checks = 4;
            foreach (string name in new[] { "Fredoka-Bold", "Fredoka-SemiBold", "Andika-Regular", "Andika-Bold" })
            {
                var font = AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(
                    "Assets/UI/Fonts/" + name + " SDF.asset");
                Require(font != null && font.sourceFontFile != null, "font SDF dan sumber tersedia: " + name); checks++;
                Require(font.atlasWidth == 1024 && font.atlasHeight == 1024 && font.atlasPadding == 9
                    && font.atlasPopulationMode == UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic,
                    "konfigurasi atlas font sesuai D5: " + name); checks++;
                Require(font.material != null && AssetDatabase.Contains(font.material)
                    && font.atlasTextures.Length > 0 && AssetDatabase.Contains(font.atlasTextures[0]),
                    "material dan atlas font tersimpan: " + name); checks++;
            }
            for (int screen = 1; screen <= 26; screen++)
            {
                checks += ValidateTree(catalog.Get(screen, false), screen, "landscape");
                checks += ValidateTree(catalog.Get(screen, true), screen, "portrait");
            }

            checks += RequireElements(catalog.Get(2, false), "btn:mulai", "side:garasi", "side:hadiah", "modebtn", "iconbtn:gear");
            checks += RequireElements(catalog.Get(3, false), "scrim", "iconbtn:close", "btn:ambil-koin", "day:1", "day:7");
            checks += RequireElements(catalog.Get(4, false), "mapcard:kota-paket", "mapcard:blok-paket", "mapcard:classic");
            checks += RequireElements(catalog.Get(4, true), "mapcard:kota-paket", "mapcard:blok-paket", "mapcard:classic");
            checks += ValidateShop(catalog.Get(6, false));
            checks += ValidateShop(catalog.Get(6, true));
            checks += RequireElements(catalog.Get(7, false), "dialog:mau-beli-ini", "btn:tidak", "btn:ya-beli");
            checks += RequireElements(catalog.Get(7, true), "dialog:mau-beli-ini", "btn:tidak", "btn:ya-beli");
            checks += RequireElements(catalog.Get(8, false), "dialog:koin-kurang", "btn:ok");
            checks += RequireElements(catalog.Get(8, true), "dialog:koin-kurang", "btn:ok");
            checks += RequireElements(catalog.Get(10, false), "iconbtn:pause", "iconbtn:map", "mini", "ctl:left", "ctl:right", "ctl:gas", "ctl:rem");
            checks += RequireElements(catalog.Get(10, true), "iconbtn:pause", "mini", "joy");
            checks += RequireElements(catalog.Get(13, false), "btn:arahkan-ke-rumah");
            checks += RequireElements(catalog.Get(18, false), "btn:tutup-peta", "map");
            checks += RequireElements(catalog.Get(18, true), "iconbtn:close", "map");
            checks += RequireElements(catalog.Get(19, true), "btn:lanjutkan", "btn:peta-kecil-tampil");
            checks += RequireElements(catalog.Get(24, false), "btn:batal-simpan-progres", "btn:ya-hapus");
            checks += ValidateToastKinds();
            Require(catalog.motion != null && catalog.motion.text.Contains("\"popup-buka\""), "gerak.json terhubung ke katalog UI"); checks++;

            string hudSource = File.ReadAllText("Assets/Scripts/KidFriendlyHud.cs");
            Require(!hudSource.Contains("OnGUI("), "HUD tidak memakai IMGUI"); checks++;
            Require(!File.Exists("Assets/Scripts/KidFriendlyHud.Menus.cs"), "fallback menu IMGUI sudah dihapus"); checks++;
            Require(CourierProgress.Names.Length == CourierProgress.Prices.Length
                && CourierProgress.Names.Length == CourierProgress.Descriptions.Length,
                "nama, harga, dan deskripsi skin sinkron"); checks++;
            string minimapSource = File.ReadAllText("Assets/Scripts/DeliveryMinimap.cs");
            Require(!minimapSource.Contains("GUI."), "minimap tidak memakai IMGUI"); checks++;
            checks += RequireElements(catalog.Get(25, false), "story", "body", "iconbtn:close");
            checks += RequireElements(catalog.Get(25, true), "story", "body", "iconbtn:close");
            Require(IsScrollView(catalog.Get(25, false), "body"), "cerita landscape dapat digulir"); checks++;
            Require(IsScrollView(catalog.Get(25, true), "body"), "cerita portrait dapat digulir"); checks++;
            Debug.Log($"D5_UI_CHECKS_OK: {checks} pemeriksaan.");
        }

        private static int ValidateTree(VisualTreeAsset tree, int screen, string orientation)
        {
            Require(tree != null, $"layar {screen:00} {orientation} tersedia");
            TemplateContainer clone = tree.CloneTree();
            Require(clone.Q<VisualElement>("layar") != null, $"layar {screen:00} {orientation} memiliki root layar");
            return 2;
        }

        private static int RequireElements(VisualTreeAsset tree, params string[] names)
        {
            TemplateContainer clone = tree.CloneTree();
            foreach (string name in names)
                Require(clone.Q<VisualElement>(name) != null, $"elemen interaktif tersedia: {name}");
            return names.Length;
        }

        private static bool IsScrollView(VisualTreeAsset tree, string name)
        {
            return tree.CloneTree().Q<ScrollView>(name) != null;
        }

        private static int ValidateShop(VisualTreeAsset tree)
        {
            TemplateContainer clone = tree.CloneTree();
            int checks = 0;
            foreach (string skin in CourierProgress.Names)
            {
                VisualElement card = clone.Q<VisualElement>("skin:" + skin.ToLowerInvariant());
                Require(card != null, "kartu skin tersedia: " + skin); checks++;
                Require(card.Q<VisualElement>("preview")?.Q<VisualElement>("truck") != null,
                    "preview skin tersedia: " + skin); checks++;
            }
            Require(clone.Q<VisualElement>("preview") != null,
                "preview skin aktif tersedia"); checks++;
            return checks;
        }

        // Setiap pesan (pesan.json, dari katalog teks desain) tampil dengan jenis toast yang sama (info/warn/good); {isian} diganti contoh.
        private static int ValidateToastKinds()
        {
            string json = File.ReadAllText("Assets/Editor/Tests/pesan.json");
            int start = json.IndexOf("\"pesan\"", StringComparison.Ordinal), end = json.IndexOf("\"skin\"", start, StringComparison.Ordinal);
            var entries = System.Text.RegularExpressions.Regex.Matches(json.Substring(start, end - start),
                @"""teks"":\s*""((?:[^""\\]|\\.)*)"",\s*""jenis"":\s*""(\w+)""");
            Require(entries.Count >= 25, "pesan.json terbaca");
            foreach (System.Text.RegularExpressions.Match entry in entries)
            {
                string text = System.Text.RegularExpressions.Regex.Replace(entry.Groups[1].Value, @"\{[^}]+\}", "5");
                string kind = KidFriendlyHud.ToastKind(text);
                Require(kind == entry.Groups[2].Value, $"jenis toast \"{text}\" = {entry.Groups[2].Value} (dapat {kind})");
            }
            return entries.Count + 1;
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("D5_UI_FAIL: " + description);
        }
    }
}
