using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiRoot : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset landscapeTemplate;
        [SerializeField] private VisualTreeAsset portraitTemplate;

        private UIDocument document;
        private PanelSettings runtimePanel;
        private bool isPortrait;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private VisualTreeAsset clonedTemplate;
        // Posisi acuan anak langsung "layar" saat templat dipasang: left, top (NaN = tidak dipakai) dan pusat x, y.
        private readonly Dictionary<VisualElement, Vector4> referenceOffsets = new Dictionary<VisualElement, Vector4>();
        private readonly Dictionary<VisualElement, Vector4> codeAnchors = new Dictionary<VisualElement, Vector4>();
        private readonly HashSet<VisualElement> released = new HashSet<VisualElement>();
        private Vector2 anchorReference, anchorPanel;
        private bool offsetsPending;

        // Dipanggil sesudah layar diganti ke templat lain (bukan rotasi): isi layar lama yang sudah dilepas dari pohon,
        // supaya pemanggil bisa memutar gerak keluar (popup-tutup / layar-ganti) lalu membuangnya.
        public event Action<List<VisualElement>> ScreenReplaced;

        // Ukuran layar tiruan untuk alat tangkap layar editor (Logs/captures); null = layar asli.
        public static Vector2Int? ScreenOverride;
        public static bool PortraitScreen => ScreenOverride.HasValue ? ScreenOverride.Value.y > ScreenOverride.Value.x : Screen.height > Screen.width;

        public VisualElement Root => document == null ? null : document.rootVisualElement;
        public bool IsPortrait => isPortrait;
        // Tinggi area aman dalam unit panel (bukan piksel layar); 0 sebelum layar pertama terpasang.
        public float SafeHeight { get; private set; }

        public void ConfigureTemplates(VisualTreeAsset landscape, VisualTreeAsset portrait)
        {
            landscapeTemplate = landscape;
            portraitTemplate = portrait;
            Refresh(true);
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (document == null)
            {
                document = GetComponent<UIDocument>();
            }

            Refresh(true);
        }

        private void Update()
        {
            Refresh(false);
            UiParity.FixPills(Root);
        }

        private void OnDestroy()
        {
            if (runtimePanel != null) Destroy(runtimePanel);
        }

        public void Refresh(bool force)
        {
            if (document == null)
            {
                return;
            }

            Vector2Int screenSize = ScreenOverride ?? new Vector2Int(Screen.width, Screen.height);
            bool portrait = screenSize.y > screenSize.x;
            Rect safeArea = ScreenOverride.HasValue ? new Rect(0, 0, screenSize.x, screenSize.y) : Screen.safeArea;
            bool orientationChanged = portrait != isPortrait;
            bool screenChanged = screenSize != lastScreenSize || safeArea != lastSafeArea;

            if (runtimePanel == null && document.panelSettings != null)
            {
                runtimePanel = Instantiate(document.panelSettings);
                document.panelSettings = runtimePanel;
            }
            if (runtimePanel != null)
            {
                runtimePanel.referenceResolution = portrait ? new Vector2Int(720, 1280) : new Vector2Int(1280, 720);
                // Expand: panel selalu ≥ ukuran acuan di kedua sumbu (20:9 lebih lebar, 4:3 lebih tinggi), jadi tata letak
                // desain tidak pernah terjepit; jangkar kanan/bawah ikut tepi layar.
                runtimePanel.screenMatchMode = PanelScreenMatchMode.Expand;
            }

            VisualElement root = Root;
            if (root == null)
            {
                return;
            }

            if (force || orientationChanged || root.childCount == 0)
            {
                isPortrait = portrait;
                VisualTreeAsset template = portrait ? portraitTemplate : landscapeTemplate;
                if (template != null)
                {
                    var previous = new List<VisualElement>(root.Children());
                    bool replaced = template != clonedTemplate && !orientationChanged && previous.Count > 0;
                    root.Clear();
                    template.CloneTree(root);
                    codeAnchors.Clear();
                    released.Clear();
                    referenceOffsets.Clear();
                    clonedTemplate = template;
                    offsetsPending = true;
                    if (replaced) ScreenReplaced?.Invoke(previous);
                }
            }

            if (force || orientationChanged || screenChanged)
            {
                ApplySafeArea(safeArea, screenSize);
                lastSafeArea = safeArea;
                lastScreenSize = screenSize;
            }

            // Sesudah ukuran layar dan area aman terpasang: posisi acuan dibaca dan jangkar diterapkan di frame yang sama.
            // Diulang tiap frame sampai berhasil: sesudah ganti peta (scene dimuat ulang) templat pertama dipasang sebelum panel
            // siap, dan dulu jangkar tidak pernah terpasang (tombol lobby tetap di posisi acuan 720×1280; uji HP 27 Sep).
            if (offsetsPending)
            {
                ApplySafeArea(safeArea, screenSize);
                offsetsPending = !RecordReferenceOffsets();
            }
        }

        public VisualElement Pick(Vector2 screenPosition)
        {
            if (Root?.panel == null)
            {
                return null;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(Root.panel, screenPosition);
            return Root.panel.Pick(panelPosition);
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            return Pick(screenPosition) != null;
        }

        // Posisi sebaris UXML tidak terbaca lewat element.style di runtime (keyword Null), jadi dibaca dari resolvedStyle
        // pada tata letak pertama: sisi yang auto dilaporkan sama dengan sisi lawannya (top ditulis ⇔ bottom == top,
        // left ditulis ⇔ right == left), sisi yang ditulis bernilai sendiri. Hanya left/top yang ditulis sendirian yang digeser.
        private bool RecordReferenceOffsets()
        {
            VisualElement screen = Root?.Q<VisualElement>("layar");
            if (screen == null) return true;
            // Laporan uji HP 27 Sep: menunggu GeometryChangedEvent membuat layar baru tergambar satu frame di posisi acuan
            // (tombol Mulai/peta "lompat ke tengah" lalu kembali di 20:9). Tata letak dihitung langsung di sini supaya jangkar
            // terpasang sebelum frame pertama digambar.
            // Tata letak baru sah kalau bingkai sudah seukuran panel (ApplySafeArea); kalau belum, coba lagi frame berikutnya.
            if (!LayoutNow(screen) || Mathf.Abs(screen.layout.width - anchorPanel.x) > 1f || Mathf.Abs(screen.layout.height - anchorPanel.y) > 1f)
                return false;
            referenceOffsets.Clear();
            CaptureOffsets(screen);
            return true;
        }

        private void CaptureOffsets(VisualElement screen)
        {
            foreach (VisualElement child in screen.Children())
            {
                IResolvedStyle style = child.resolvedStyle;
                if (style.position != Position.Absolute) continue;
                Rect box = child.layout;
                // Kedua sisi ditulis sama (mis. sc-fill 0/0) juga terbaca sama; bedanya jarak sebenarnya ke sisi lawan ikut nilai itu.
                bool leftOnly = Mathf.Approximately(style.right, style.left) && !Mathf.Approximately(screen.layout.width - box.xMax, style.right);
                bool topOnly = Mathf.Approximately(style.bottom, style.top) && !Mathf.Approximately(screen.layout.height - box.yMax, style.bottom);
                float left = leftOnly ? style.left : float.NaN;
                float top = topOnly ? style.top : float.NaN;
                if (released.Contains(child)) continue;
                if (float.IsNaN(left) && float.IsNaN(top)) continue;
                referenceOffsets[child] = new Vector4(left, top, box.center.x, box.center.y);
            }
            // Jangkar yang dipasang kode (Anchor) sebelum pembacaan ini tetap berlaku.
            foreach (KeyValuePair<VisualElement, Vector4> pair in codeAnchors)
                if (pair.Key.parent == screen) referenceOffsets[pair.Key] = pair.Value;
            ApplyAnchors();
        }

        // Elemen yang ditambahkan kode sesudah templat dipasang (mis. joystick portrait yang tidak ada di templat 13/20)
        // ikut digeser seperti elemen templat: posisi acuan left/top dan ukurannya dalam unit acuan.
        public void Anchor(VisualElement child, float left, float top, float width, float height)
        {
            var at = new Vector4(left, top, left + width * 0.5f, top + height * 0.5f);
            codeAnchors[child] = at;
            referenceOffsets[child] = at;
            ApplyAnchors();
        }

        // Elemen yang posisinya diatur kode sepenuhnya (mis. kisi garasi portrait menempel ke bawah): keluar dari jangkar.
        public void Release(VisualElement child)
        {
            released.Add(child);
            codeAnchors.Remove(child);
            referenceOffsets.Remove(child);
        }

        private static MethodInfo validateLayout;

        // BaseVisualElementPanel.ValidateLayout (gaya + tata letak) bersifat internal; dipanggil lewat refleksi.
        private static bool LayoutNow(VisualElement element)
        {
            object panel = element.panel;
            if (panel == null) return false;
            try
            {
                validateLayout ??= panel.GetType().GetMethod("ValidateLayout",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (validateLayout == null) return false;
                validateLayout.Invoke(panel, null);
                return !float.IsNaN(element.layout.width) && element.layout.width > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Uji HP 27 Sep (portrait 20:9): baris atas (koin, tombol ikon, judul garasi) terlalu mepet ke tepi atas HP, dan
        // kelompok Mulai di lobby terlalu rapat ke tepi bawah. Elemen yang dijangkar ke atas turun PortraitTopInset; kelompok
        // bawah lobby (juga di popup lobby yang memuat salinannya) naik PortraitLobbyLift, tetap di bawah. Hanya portrait.
        private const float PortraitTopInset = 28f, PortraitLobbyLift = 64f;
        private static readonly HashSet<string> LobbyBottom = new HashSet<string> { "@btn:mulai", "@modebtn", "@side:garasi", "@side:hadiah" };

        private void ApplyAnchors()
        {
            Vector2 extra = anchorPanel - anchorReference;
            bool portrait = anchorReference.y > anchorReference.x;
            foreach (KeyValuePair<VisualElement, Vector4> pair in referenceOffsets)
            {
                Vector4 at = pair.Value;
                if (!float.IsNaN(at.x)) pair.Key.style.left = at.x + extra.x * Share(at.z / anchorReference.x);
                if (float.IsNaN(at.y)) continue;
                float share = Share(at.w / anchorReference.y);
                float top = at.y + extra.y * share;
                if (portrait && share == 0f) top += PortraitTopInset;
                if (portrait && LobbyBottom.Contains(pair.Key.name)) top -= PortraitLobbyLift;
                pair.Key.style.top = top;
            }
        }

        // Bagian selisih panel − acuan yang dipakai menurut letak pusat elemen di acuan: kiri/atas (< 40 %) diam, kanan/bawah
        // (> 60 %) ikut tepi, tengah (mis. peta besar, dialog) bergeser setengahnya supaya tetap di tengah.
        private static float Share(float center01) => center01 < 0.4f ? 0f : center01 > 0.6f ? 1f : 0.5f;

        private void ApplySafeArea(Rect safeArea, Vector2Int screenSize)
        {
            VisualElement screen = Root?.Q<VisualElement>("layar");
            if (screen == null || screenSize.x <= 0 || screenSize.y <= 0)
            {
                return;
            }

            // Skala Expand: piksel layar per unit panel = sisi terkecil relatif terhadap acuan.
            Vector2 reference = isPortrait ? new Vector2(720f, 1280f) : new Vector2(1280f, 720f);
            float scale = Mathf.Min(screenSize.x / reference.x, screenSize.y / reference.y);
            // USS .sc--l/.sc--p mematok bingkai 1280×720 / 720×1280 (ukuran acuan desain). Dengan Expand panel lebih besar
            // di 20:9, 16:10, 4:3, jadi bingkai diisi selebar panel supaya scrim menutup layar dan jangkar kanan/bawah
            // menempel ke tepi layar sungguhan (bukan tepi 1280/720).
            Vector2 panel = new Vector2(screenSize.x / scale, screenSize.y / scale);
            screen.style.width = panel.x;
            screen.style.height = panel.y;
            // Generator menulis posisi mutlak acuan dengan left/top (mis. tombol Mulai dan joystick portrait: top 1104/922).
            // Elemen di kanan/bawah acuan digeser sejauh selisih panel − acuan (lihat Share) supaya tetap menempel ke
            // tepi kanan/bawah seperti di desain (20:9 portrait: Mulai tidak melayang di tengah layar); separuh kiri/atas tetap.
            anchorReference = reference;
            anchorPanel = panel;
            ApplyAnchors();
            screen.style.paddingLeft = safeArea.xMin / scale;
            screen.style.paddingRight = (screenSize.x - safeArea.xMax) / scale;
            screen.style.paddingBottom = safeArea.yMin / scale;
            screen.style.paddingTop = (screenSize.y - safeArea.yMax) / scale;
            SafeHeight = safeArea.height / scale;
        }
    }
}
