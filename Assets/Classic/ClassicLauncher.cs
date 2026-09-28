using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeliveryDash.Classic
{
    // Lobby dan kendali Classic 1.0. Permintaan pengguna 28 Sep: tidak ada elemen 2.0 sama sekali (templat, font, ikon,
    // gaya tombol, PanelSettings 2.0). Semua dibangun dari kode dengan font bawaan Unity dan tombol kotak abu-abu polos,
    // sengaja seadanya seperti versi pertama. Dari gerbang 2.0 ada tulisan "Classic versi 1.0"; APK Classic sendiri
    // hanya judul, Mulai, dan Keluar.
    public sealed class ClassicLauncher : MonoBehaviour
    {
        // Ukuran layar tiruan untuk alat tangkap layar editor (Logs/captures); null = layar asli. Sama seperti UiRoot 2.0.
        public static Vector2Int? ScreenOverride;
        private static Vector2Int ScreenSize => ScreenOverride ?? new Vector2Int(Screen.width, Screen.height);

        private static readonly Color ButtonFace = new Color32(0xC0, 0xC0, 0xC0, 255);
        private static readonly Color ButtonPressed = new Color32(0x9A, 0x9A, 0x9A, 255);
        private static readonly Color ButtonEdge = new Color32(0x40, 0x40, 0x40, 255);
        private static readonly Color HoldIdle = new Color(0f, 0f, 0f, 0.55f), HoldPressed = new Color(0f, 0f, 0f, 0.8f);

        private UIDocument document;
        private PanelSettings runtimePanel;
        private ThemeStyleSheet emptyTheme;
        private Font plainFont;
        private ClassicDriver driver;
        private bool portraitMode;
        private bool started;
        private bool startWhenLandscape;
        private bool gas;
        private bool brake;
        private bool left;
        private bool right;
        private Vector2Int lastSize;
        private Rect lastSafeArea;

        // Dimainkan dari aplikasi 2.0 (gerbang Classic) bila scene lobby 2.0 ikut di-build; APK Classic hanya punya scene ini.
        private static bool FromDeliveryDash2 =>
            Application.CanStreamedLevelBeLoaded("KotaPaket") || Application.CanStreamedLevelBeLoaded("SampleScene");

        private void Awake()
        {
            document = gameObject.AddComponent<UIDocument>();
            runtimePanel = ScriptableObject.CreateInstance<PanelSettings>();
            emptyTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            runtimePanel.themeStyleSheet = emptyTheme;
            runtimePanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanel.screenMatchMode = PanelScreenMatchMode.Expand;
            document.panelSettings = runtimePanel;
            plainFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            driver = FindFirstObjectByType<ClassicDriver>();
            Time.timeScale = 0f;
            Rebuild();
        }

        private void Update()
        {
            Vector2Int size = ScreenSize;
            bool isPortrait = size.y > size.x;
            if (portraitMode != isPortrait)
            {
                if (!isPortrait && startWhenLandscape) { startWhenLandscape = false; started = true; }
                Time.timeScale = !isPortrait && started ? 1f : 0f;
                Rebuild();
            }
            else if (size != lastSize || Screen.safeArea != lastSafeArea) Rebuild();
            // Escape (desktop) / tombol Kembali Android (Input System memetakannya ke escapeKey).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }

        // Kembali satu langkah: saat main → jeda ke lobby Classic; di lobby Classic → keluar.
        public void Back()
        {
            if (started) PauseToMenu();
            else ExitClassic();
        }

        public bool Started => started;

        public void PauseToMenu()
        {
            if (!started) return;
            started = false;
            gas = brake = left = right = false;
            PushTouchState();
            Time.timeScale = 0f;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (runtimePanel != null) Destroy(runtimePanel);
            if (emptyTheme != null) Destroy(emptyTheme);
        }

        // Panel acuan 1280×720 / 720×1280 dengan skala Expand; tepi aman HP diberi jarak.
        private float Scale
        {
            get
            {
                Vector2 reference = portraitMode ? new Vector2(720f, 1280f) : new Vector2(1280f, 720f);
                return Mathf.Max(0.01f, Mathf.Min(ScreenSize.x / reference.x, ScreenSize.y / reference.y));
            }
        }

        private Rect SafeArea => ScreenOverride.HasValue ? new Rect(0, 0, ScreenSize.x, ScreenSize.y) : Screen.safeArea;

        private void Rebuild()
        {
            portraitMode = ScreenSize.y > ScreenSize.x;
            lastSize = ScreenSize;
            lastSafeArea = Screen.safeArea;
            if (runtimePanel != null)
                runtimePanel.referenceResolution = portraitMode ? new Vector2Int(720, 1280) : new Vector2Int(1280, 720);
            VisualElement root = document.rootVisualElement;
            root.Clear();
            root.style.unityFontDefinition = FontDefinition.FromFont(plainFont);
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            if (started && !portraitMode) BuildTouchControls(root);
            else BuildLobby(root);
        }

        private void BuildLobby(VisualElement root)
        {
            var menu = new VisualElement { name = "layar" };
            menu.style.position = Position.Absolute;
            menu.style.left = 0;
            menu.style.right = 0;
            menu.style.top = 0;
            menu.style.bottom = 0;
            menu.style.alignItems = Align.Center;
            menu.style.justifyContent = Justify.Center;
            // Latar gelap polos (portrait) atau gelap tembus pandang di atas peta (mendatar) supaya tulisan terbaca.
            menu.style.backgroundColor = portraitMode ? new Color(0.1f, 0.1f, 0.1f, 1f) : new Color(0f, 0f, 0f, 0.45f);
            root.Add(menu);

            Label title = PlainText("Delivery Dash", portraitMode ? 64 : 72);
            title.name = "judul";
            menu.Add(title);
            if (FromDeliveryDash2)
            {
                Label version = PlainText("Classic versi 1.0", 24);
                version.name = "versi";
                menu.Add(version);
            }
            VisualElement start = PlainButton("btn:mulai", "Mulai", StartClassic);
            start.style.marginTop = 36;
            menu.Add(start);
            VisualElement quit = PlainButton("btn:keluar", "Keluar", ExitClassic);
            quit.style.marginTop = 14;
            menu.Add(quit);
        }

        private Label PlainText(string text, int size)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size;
            label.style.color = Color.white;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityTextOutlineColor = Color.black;
            label.style.unityTextOutlineWidth = 0.2f;
            return label;
        }

        // Tombol kotak abu-abu bertepi gelap, tanpa ikon, tanpa sudut membulat.
        private VisualElement PlainButton(string name, string caption, System.Action action)
        {
            var button = new Label(caption) { name = name };
            button.style.width = 240;
            button.style.height = 64;
            button.style.fontSize = 28;
            button.style.color = Color.black;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.backgroundColor = ButtonFace;
            button.style.borderTopWidth = 2;
            button.style.borderLeftWidth = 2;
            button.style.borderRightWidth = 2;
            button.style.borderBottomWidth = 2;
            button.style.borderTopColor = Color.white;
            button.style.borderLeftColor = Color.white;
            button.style.borderRightColor = ButtonEdge;
            button.style.borderBottomColor = ButtonEdge;
            button.pickingMode = PickingMode.Position;
            button.RegisterCallback<PointerDownEvent>(_ => button.style.backgroundColor = ButtonPressed);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = ButtonFace);
            button.RegisterCallback<PointerUpEvent>(_ =>
            {
                button.style.backgroundColor = ButtonFace;
                action();
            });
            return button;
        }

        private void StartClassic()
        {
            if (portraitMode)
            {
                // Classic dimainkan mendatar: Mulai di portrait memutar layar, lalu permainan jalan begitu mendatar.
                startWhenLandscape = true;
                RotateToLandscape();
                return;
            }
            started = true;
            Time.timeScale = 1f;
            Rebuild();
        }

        // Sama dengan DeliveryDash.UI.ScreenRotation (rakitan 2.0, tidak dirujuk dari sini): kunci PlayerPrefs yang sama,
        // 2 = Mendatar, jadi pilihan ini tetap berlaku sesudah kembali ke lobby 2.0 (bisa diubah di Pengaturan).
        private static void RotateToLandscape()
        {
            PlayerPrefs.SetInt("Screen.Orientation", 2);
            PlayerPrefs.Save();
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        private void BuildTouchControls(VisualElement root)
        {
            // Tombol jeda selalu ada (desktop dan sentuh) supaya pemain bisa kembali ke lobby tanpa menutup aplikasi.
            AddPauseButton(root);
            if (!Application.isMobilePlatform && Touchscreen.current == null) return;
            // Uji HP 27 Sep: tombol 136 px, celah 28 px, 32 px dari tepi supaya mudah dipencet.
            AddHold(root, "<", Edge, null, value => left = value);
            AddHold(root, ">", Edge + HoldSize + HoldGap, null, value => right = value);
            AddHold(root, "REM", null, Edge + HoldSize + HoldGap, value => brake = value);
            AddHold(root, "GAS", null, Edge, value => gas = value);
        }

        private const float HoldSize = 136f, HoldGap = 28f, Edge = 32f;

        private void AddHold(VisualElement root, string caption, float? leftEdge, float? rightEdge, System.Action<bool> set)
        {
            Label control = new Label(caption);
            control.style.position = Position.Absolute;
            Rect safe = SafeArea;
            float scale = Scale;
            control.style.bottom = Edge + safe.yMin / scale;
            if (leftEdge.HasValue) control.style.left = leftEdge.Value + safe.xMin / scale;
            if (rightEdge.HasValue) control.style.right = rightEdge.Value + (ScreenSize.x - safe.xMax) / scale;
            control.style.width = HoldSize;
            control.style.height = HoldSize;
            control.style.unityTextAlign = TextAnchor.MiddleCenter;
            control.style.fontSize = 36;
            control.style.color = Color.white;
            control.style.backgroundColor = HoldIdle;
            control.style.borderTopWidth = 2;
            control.style.borderLeftWidth = 2;
            control.style.borderRightWidth = 2;
            control.style.borderBottomWidth = 2;
            control.style.borderTopColor = Color.white;
            control.style.borderLeftColor = Color.white;
            control.style.borderRightColor = Color.white;
            control.style.borderBottomColor = Color.white;
            control.pickingMode = PickingMode.Position;
            control.RegisterCallback<PointerDownEvent>(evt =>
            {
                set(true);
                control.style.backgroundColor = HoldPressed;
                PushTouchState();
                control.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            control.RegisterCallback<PointerUpEvent>(evt =>
            {
                control.style.backgroundColor = HoldIdle;
                set(false);
                PushTouchState();
                if (control.HasPointerCapture(evt.pointerId)) control.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
            control.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                control.style.backgroundColor = HoldIdle;
                set(false);
                PushTouchState();
            });
            root.Add(control);
        }

        private void AddPauseButton(VisualElement root)
        {
            VisualElement pause = PlainButton("btn:jeda", "Jeda", PauseToMenu);
            pause.style.position = Position.Absolute;
            Rect safe = SafeArea;
            float scale = Scale;
            pause.style.top = 16 + (ScreenSize.y - safe.yMax) / scale;
            pause.style.left = 16 + safe.xMin / scale;
            pause.style.width = 110;
            pause.style.height = 52;
            pause.style.fontSize = 24;
            pause.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            root.Add(pause);
        }

        private void PushTouchState() => driver?.SetTouchControls(gas, brake, left, right);

        private static void ExitClassic()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("KotaPaket")) SceneManager.LoadScene("KotaPaket");
            else if (Application.CanStreamedLevelBeLoaded("SampleScene")) SceneManager.LoadScene("SampleScene");
            else Application.Quit();
        }
    }
}
