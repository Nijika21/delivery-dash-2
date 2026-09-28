using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DeliveryDash.UI;
using UnityEngine;
using UnityEngine.UIElements;

public partial class KidFriendlyHud
{
    private bool toolkitActive;
    private DeliveryUiCatalog toolkitCatalog;
    private UiRoot toolkitRoot;
    private VisualElement boundScreen;
    private int toolkitScreen;
    private bool toolkitLeft, toolkitRight, toolkitGas, toolkitBrake;
    private int joystickPointer = -1;
    private VisualElement joystick;
    private VisualElement joystickKnob;
    private VisualElement joystickSurface;
    // Joystick mengambang (hud.json portrait): pusat = sentuhan pertama, jari-jari 88 px acuan, zona mati 0,18.
    private const float JoystickRadius = 88f;
    // Posisi diam di kanan bawah (permintaan pengguna 27 Sep, jempol kanan): cermin templat left 92 → 720 − 92 − 176.
    // Jari boleh mulai di mana saja pada separuh bawah layar; alas pindah ke sentuhan pertama.
    private const float JoystickRestLeft = 720f - 92f - JoystickRadius * 2f, JoystickRestTop = 922f;
    private const float JoystickZone = 0.5f;
    private Vector2 joystickCenter, joystickHome, knobOffset;
    // true selama alas dipindah ke jari (sampai animasi pulang selesai): posisi diam tidak dibaca ulang saat itu.
    private bool joystickAway;
    private bool controlsPortrait;
    private readonly Dictionary<string, int> holdPointers = new Dictionary<string, int>();

    private void SetupToolkit()
    {
        toolkitCatalog = Resources.Load<DeliveryUiCatalog>("DeliveryUiCatalog");
        if (toolkitCatalog == null || toolkitCatalog.panelSettings == null) return;
        GameObject rootObject = new GameObject("Antarmuka D5", typeof(UIDocument), typeof(UiRoot));
        rootObject.transform.SetParent(transform, false);
        PanelSettings panelSettings = toolkitCatalog.panelSettings;
        rootObject.GetComponent<UIDocument>().panelSettings = panelSettings;
        toolkitRoot = rootObject.GetComponent<UiRoot>();
        UiMotion.Load(toolkitCatalog.motion);
        toolkitRoot.ScreenReplaced += PlayScreenTransition;
        toolkitActive = true;
        RefreshToolkit(true);
    }

    private int CurrentScreen()
    {
        if (ShowingLoading()) return 1;
        switch (page)
        {
            case Page.Lobby: return 2;
            case Page.Daily: return 3;
            case Page.Mode: return 4;
            case Page.Settings: return 5;
            case Page.Shop:
                return pendingSkin < 0 ? 6 : insufficientCoins ? 8 : 7;
            case Page.Pause: return 19;
            case Page.FinishWork: return 22;
            case Page.TutorialPrompt: return 22;
            case Page.ExitWarning: return 23;
            case Page.ResetWarning: return 24;
            case Page.About: return 25;
            default:
                if (gameManager.Minimap != null && gameManager.Minimap.Expanded) return 18;
                // Sorotan saat selesai kerja diminta memakai layar 20 (kartu kuning) / 21; layar 14 = sorotan rumah tujuan.
                if (gameManager.FinishRequested) return gameManager.HasPendingDelivery ? 20 : 21;
                if (gameManager.IsShowingDestination) return 14;
                if (gameManager.Activities != null && gameManager.Activities.IsFragile) return 12;
                if (gameManager.Activities != null && gameManager.Activities.HasStopover && gameManager.Activities.GuideToBonus) return 13;
                if (gameManager.SideQuests != null && gameManager.SideQuests.CarriedLetters > 0) return 17;
                // Layar 11 = ekspres dengan minimap tertutup (templatnya memang tanpa minimap). Saat minimap dibuka,
                // ekspres memakai layar 10; chip ekspres dibangun runtime (UpdateChips), jadi tetap tampil.
                if (gameManager.Activities != null && gameManager.Activities.IsExpress &&
                    (gameManager.Minimap == null || !gameManager.Minimap.Visible)) return 11;
                return gameManager.TutorialActive ? 9 : 10;
        }
    }

    private void RefreshToolkit(bool force)
    {
        if (!toolkitActive || toolkitRoot == null) return;
        int screen = CurrentScreen();
        // Uji HP 28 Sep: spam tombol suara (dan tombol lain yang tidak mengganti layar) membuat seluruh UI berkedip
        // karena templat dikloning ulang tiap ketukan. Layar yang sama cukup diperbarui isinya (semua Update*Ui
        // membaca keadaan, jadi hasilnya sama dengan klon baru).
        if (screen == toolkitScreen && boundScreen?.panel != null)
        {
            UpdateToolkitText();
            return;
        }
        lastScreenSwap = Time.unscaledTime;
        previousToolkitScreen = toolkitScreen;
        toolkitScreen = screen;
            toolkitRoot.ConfigureTemplates(toolkitCatalog.Get(screen, false), toolkitCatalog.Get(screen, true));
        boundScreen = null;
        BindToolkitIfNeeded();
    }

    private void BindToolkitIfNeeded()
    {
        VisualElement screen = toolkitRoot?.Root?.Q<VisualElement>("layar");
        if (screen == null || screen == boundScreen) return;
        boundScreen = screen;
        AddPortraitMapToggle();
        AddRotateControls();
        AddMusicControls();
        BindRotateControls();
        BindMusicControls();
        AddGuideCredit();
        BindClick("btn:mulai", ResumeGame);
        BindClick("iconbtn:pause", () => SetPage(Page.Pause));
        BindClick("iconbtn:map", ToggleSmallMap);
        BindClick("mini", OpenBigMap);
        BindClick("btn:tutup-peta", CloseBigMap);
        BindClick("btn:peta-kecil-tampil", ToggleSmallMap);
        BindClick("btn:lanjutkan", ResumeGame);
        BindClick("btn:garasi", () => OpenToolkitShop(page));
        BindClick("btn:selesai-kerja-ke", gameManager.RequestFinishWork);
        // "Bantu ke halaman gudang" (teleport) dihapus atas permintaan pengguna (26 Sep); truk yang tersangkut keluar
        // dengan mundur/putar di tempat (Driver). Tombol templat D5 disembunyikan.
        VisualElement recoverButton = boundScreen.Q<VisualElement>("btn:bantu-ke-halaman");
        if (recoverButton != null) recoverButton.style.display = DisplayStyle.None;
        BindClick("side:garasi", () => OpenToolkitShop(Page.Lobby));
        BindClick("side:hadiah", () => SetPage(Page.Daily));
        BindClick("modebtn", () => SetPage(Page.Mode));
        BindClick("iconbtn:gear", () => SetPage(Page.Settings));
        BindClick("iconbtn:help", () => SetPage(Page.About));
        BindClick("iconbtn:close", CloseCurrentOverlay);
        BindClick("close", ReturnToLobby);
        BindClick("btn:kembali", () => SetPage(shopReturn));
        BindClick("btn:ambil-koin", ClaimDaily);
        BindClick("row:suara", ToggleSound);
        BindClick("btn:suara-aktif", ToggleSound);
        BindClick("row:keluar-game", RequestExit);
        BindClick("danger:hapus-semua-progres", () => SetPage(Page.ResetWarning));
        BindClick("btn:batal-simpan-progres", () => SetPage(Page.Settings));
        BindClick("btn:ya-hapus", () => { gameManager.ResetProgressConfirmed(); SetPage(Page.Lobby); });
        BindClick("btn:tidak", () =>
        {
            if (page == Page.FinishWork) gameManager.AnswerFinishWork(false);
            else if (page == Page.TutorialPrompt) gameManager.AnswerTutorialPrompt(false);
            // Dialog keluar (23) juga memakai "btn:tidak"; dulu jatuh ke batal-beli, jadi tidak terjadi apa-apa (uji HP 27 Sep).
            else if (page == Page.ExitWarning) CancelExit();
            else CancelShopDialog();
        });
        BindClick("btn:ya-ke-lobby", () =>
        {
            if (page == Page.TutorialPrompt) gameManager.AnswerTutorialPrompt(true);
            else gameManager.AnswerFinishWork(true);
        });
        DressTutorialPrompt();
        BindClick("btn:ya-keluar", ConfirmExit);
        BindClick("btn:ok", CancelShopDialog);
        BindClick("btn:ya-beli", BuyPendingSkin);
        BindClick("mapcard:kota-paket", () => gameManager.SelectMode("kota-paket"));
        BindClick("mapcard:blok-paket", () => gameManager.SelectMode("blok-paket"));
        BindClick("mapcard:classic", () => gameManager.SelectMode("classic"));
        for (int i = 0; i < CourierProgress.Names.Length; i++)
        {
            int skin = i;
            BindClick("skin:" + CourierProgress.Names[i].ToLowerInvariant(), () => SelectToolkitSkin(skin));
        }
        BindHold("ctl:left", value => toolkitLeft = value);
        BindHold("ctl:right", value => toolkitRight = value);
        BindHold("ctl:gas", value => toolkitGas = value);
        BindHold("ctl:rem", value => toolkitBrake = value);
        BindJoystick();
        PinGarageGrid();
        BindMinimap();
        BindScrim();
        BindHudStatus();
        BindSummary();
        BindControlFade();
        HideTemplateCoach();
        StyleScrollers();
        UpdateToolkitText();
    }

    private void UpdateToolkit()
    {
        CloseDailyAfterClaim();
        if (CurrentScreen() != toolkitScreen) RefreshToolkit(true);
        BindToolkitIfNeeded();
        UpdateToolkitText();
        // Rotasi: kendali orientasi lama tidak punya pasangan di templat baru, jadi semua dilepas (hud.json: reset saat rotasi).
        if (toolkitRoot.IsPortrait != controlsPortrait)
        {
            controlsPortrait = toolkitRoot.IsPortrait;
            ReleaseControls();
        }
        // Pesan akhir tutorial (2,4 dtk sebelum lobby): truk diam.
        bool blocked = IsMenuOpen || gameManager.IsShowingDestination || gameManager.Minimap?.Expanded == true ||
            gameManager.Guide == DeliveryGameManager.GuideStep.Done;
        // Sorotan tujuan bisa dilewati dengan satu ketukan di mana saja (sentuh atau klik), bukan tombol keyboard.
        if (page == Page.Game && !IsMenuOpen && gameManager.IsShowingDestination &&
            (UnityEngine.InputSystem.Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true ||
             UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame == true))
            gameManager.SkipDestinationPreview();
        if (blocked)
        {
            if (joystickPointer >= 0 || toolkitGas || toolkitBrake || toolkitLeft || toolkitRight) ReleaseControls();
            driver.SetTouchInput(0f, 0f);
            return;
        }
        if (!toolkitRoot.IsPortrait)
            driver.SetTouchControls(toolkitGas, toolkitBrake, toolkitLeft ? 1f : toolkitRight ? -1f : 0f);
        else if (joystickPointer < 0)
            driver.HoldStill();
        else
            ApplyJoystick();
    }

    // Lepas semua kendali sentuh: joystick, tombol tahan, dan input truk.
    private void ReleaseControls()
    {
        ResetJoystick();
        toolkitLeft = toolkitRight = toolkitGas = toolkitBrake = false;
        holdPointers.Clear();
        boundScreen?.Query<VisualElement>(className: "sc-ctl").ForEach(control => control.RemoveFromClassList("is-pressed"));
        driver?.SetTouchInput(0f, 0f);
    }

    // Sorotan tujuan (HANDOFF 13 baris 4): tombol HUD dan kendali tidak menerima sentuhan selama 2 dtk.
    private bool HudInputLocked => page == Page.Game && gameManager != null && gameManager.IsShowingDestination;

    private void UpdateToolkitText()
    {
        if (boundScreen == null || gameManager?.Progress == null) return;
        UpdateCoins();
        SetNestedLabel("chip:box", "teks", gameManager.Progress.CompletedDeliveries.ToString());
        SetNestedLabel("modebtn", "name", ModeName(gameManager.Progress.LastMode));
        VisualElement message = boundScreen.Q<VisualElement>("msg");
        if (IsHudScreen(toolkitScreen)) UpdateHudStatus();
        else if (message != null)
        {
            Label label = message.Q<Label>("teks");
            if (label != null) label.text = gameManager.MessageText ?? string.Empty;
            message.style.display = string.IsNullOrEmpty(gameManager.MessageText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        // teks.json: Pengaturan = label "Suara" + sakelar (is-on); menu jeda = "Suara: {aktif|mati}".
        boundScreen.Q<VisualElement>("row:suara")?.Q<VisualElement>("toggle")?.EnableInClassList("is-on", DeliveryAudio.Enabled);
        Label pauseSound = boundScreen.Q<VisualElement>("btn:suara-aktif")?.Q<Label>("teks");
        if (pauseSound != null) pauseSound.text = DeliveryAudio.Enabled ? "Suara: aktif" : "Suara: mati";
        UpdateMinimapUi();
        UpdateRotateText();
        UpdateMusicText();
        UpdateSummary();
        UpdateGuide();
        UpdateLoading();
        UpdateDailyUi();
        UpdateModeUi();
        UpdateShopUi();
    }

    // Sesudah layar berganti, ketukan diabaikan sebentar: spam buka/tutup popup tidak menumpuk gerak keluar-masuk
    // (latar berkedip). Cukup pendek supaya ketukan biasa tidak terasa tertahan.
    private const float SwapCooldown = 0.2f;
    private float lastScreenSwap = -1f;
    private bool SwapCooling => Time.unscaledTime - lastScreenSwap < SwapCooldown;

    private void BindClick(string name, Action action)
    {
        VisualElement element = boundScreen?.Q<VisualElement>(name);
        if (element != null) BindElementClick(element, action);
    }

    // Ketuk seperti tombol sungguhan (HANDOFF 4): .is-pressed selama jari menahan (:active UI Toolkit hanya hidup
    // untuk elemen ber-Clickable), aksi dijalankan saat dilepas di atas tombol yang sama; geser keluar = batal.
    // Bagian yang bisa diketuk di dalamnya (mis. .sc-side__btn di dalam side:garasi) ikut ditekan.
    private void BindElementClick(VisualElement element, Action action)
    {
        // Saat menyetir, tombol HUD tidak bisa difokus keyboard: navigasi UI memakai WASD/panah (sama dengan tombol
        // setir), jadi fokus melompat antar tombol dan minimap membesar-mengecil (.is-focus skala 1,05) setiap W
        // ditekan, dan Spasi/Enter bisa menekan tombol yang terfokus. Menu (jeda 19, peta besar 18) tetap bisa.
        element.focusable = !IsHudScreen(toolkitScreen) || toolkitScreen == 18 || toolkitScreen == 19;
        EnsureHitArea(element);
        int pointer = -1;
        bool pointerFocus = false;
        void SetPressed(bool pressed)
        {
            element.EnableInClassList("is-pressed", pressed);
            element.Query<VisualElement>().Where(child => child != element && child.pickingMode == PickingMode.Position)
                .ForEach(child => child.EnableInClassList("is-pressed", pressed));
        }
        void Release()
        {
            if (pointer < 0) return;
            int id = pointer;
            pointer = -1;
            SetPressed(false);
            if (element.HasPointerCapture(id)) element.ReleasePointer(id);
        }
        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || pointer >= 0) return;
            if (HudInputLocked) { evt.StopPropagation(); return; }
            pointer = evt.pointerId;
            pointerFocus = true;
            element.CapturePointer(pointer);
            SetPressed(true);
            evt.StopPropagation();
        });
        element.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (evt.pointerId == pointer) SetPressed(HitRect(element).Contains(evt.position));
        });
        element.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.pointerId != pointer) return;
            bool inside = HitRect(element).Contains(evt.position);
            Release();
            evt.StopPropagation();
            if (!inside || SwapCooling) return;
            DeliveryAudio.Play(DeliveryAudio.Cue.Click);
            action();
        });
        element.RegisterCallback<PointerCancelEvent>(_ => Release());
        element.RegisterCallback<PointerCaptureOutEvent>(_ => Release());
        element.RegisterCallback<NavigationSubmitEvent>(evt =>
        {
            if (SwapCooling) { evt.StopPropagation(); return; }
            DeliveryAudio.Play(DeliveryAudio.Cue.Click);
            action();
            evt.StopPropagation();
        });
        element.RegisterCallback<FocusInEvent>(_ => { if (!pointerFocus) element.AddToClassList("is-focus"); });
        element.RegisterCallback<FocusOutEvent>(_ => { pointerFocus = false; element.RemoveFromClassList("is-focus"); });
    }

    // Disetujui pengguna (26 Sep): target sentuh minimal 64 px acuan (HANDOFF 9), tombol silang popup 72 px.
    // Tampilan tidak berubah: anak transparan "area-ketuk" memperluas bidang ketuk di sekitar tombol kecil
    // (UI Toolkit tetap memilih anak di luar batas induk selama induk tidak memotong isinya).
    private const float MinimumHit = 64f, CloseHit = 72f;

    private static void EnsureHitArea(VisualElement element)
    {
        element.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            float target = element.name == "iconbtn:close" ? CloseHit : MinimumHit;
            float width = element.resolvedStyle.width, height = element.resolvedStyle.height;
            VisualElement hit = element.Q<VisualElement>("area-ketuk");
            if (float.IsNaN(width) || float.IsNaN(height) || width >= target && height >= target)
            {
                hit?.RemoveFromHierarchy();
                return;
            }
            if (hit == null)
            {
                hit = new VisualElement { name = "area-ketuk", pickingMode = PickingMode.Position };
                hit.style.position = Position.Absolute;
                element.Add(hit);
            }
            float padX = Mathf.Max(0f, (target - width) * 0.5f), padY = Mathf.Max(0f, (target - height) * 0.5f);
            // Posisi absolut anak dihitung dari kotak padding induk: kompensasi tebal tepi supaya tetap sepusat.
            hit.style.left = -padX - element.resolvedStyle.borderLeftWidth;
            hit.style.top = -padY - element.resolvedStyle.borderTopWidth;
            hit.style.width = width + padX * 2f;
            hit.style.height = height + padY * 2f;
        });
    }

    private static Rect HitRect(VisualElement element)
    {
        Rect bounds = element.worldBound;
        VisualElement hit = element.Q<VisualElement>("area-ketuk");
        if (hit == null || hit.parent != element) return bounds;
        Rect extra = hit.worldBound;
        return Rect.MinMaxRect(Mathf.Min(bounds.xMin, extra.xMin), Mathf.Min(bounds.yMin, extra.yMin),
            Mathf.Max(bounds.xMax, extra.xMax), Mathf.Max(bounds.yMax, extra.yMax));
    }

    // Tombol tahan (GAS/REM/kiri/kanan). Jari tetap milik tombolnya sampai Up/Cancel (HANDOFF 13), juga saat
    // templat HUD berganti di tengah tahan (mis. 10 -> 11 saat kontrak ekspres mulai): tombol baru mengambil alih jari.
    private void BindHold(string name, Action<bool> set)
    {
        VisualElement element = boundScreen?.Q<VisualElement>(name);
        if (element == null) return;
        void Release(int pointerId)
        {
            if (!holdPointers.TryGetValue(name, out int owner) || owner != pointerId) return;
            holdPointers.Remove(name);
            set(false);
            element.RemoveFromClassList("is-pressed");
            if (element.HasPointerCapture(pointerId)) element.ReleasePointer(pointerId);
        }
        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            evt.StopPropagation();
            if (HudInputLocked || holdPointers.ContainsKey(name)) return;
            holdPointers[name] = evt.pointerId;
            set(true);
            element.AddToClassList("is-pressed");
            element.CapturePointer(evt.pointerId);
        });
        element.RegisterCallback<PointerUpEvent>(evt => { Release(evt.pointerId); evt.StopPropagation(); });
        element.RegisterCallback<PointerCancelEvent>(evt => Release(evt.pointerId));
        element.RegisterCallback<PointerCaptureOutEvent>(evt => { if (element.panel != null) Release(evt.pointerId); });
        if (holdPointers.TryGetValue(name, out int held))
        {
            element.AddToClassList("is-pressed");
            element.CapturePointer(held);
        }
    }

    private void BindJoystick()
    {
        joystick = boundScreen?.Q<VisualElement>("joy") ?? AddMissingJoystick();
        joystickKnob = joystick?.Q<VisualElement>("knob");
        if (joystick == null) return;
        joystick.pickingMode = PickingMode.Position;
        if (joystickPointer < 0) joystick.style.left = JoystickRestLeft;
        toolkitRoot.Anchor(joystick, JoystickRestLeft, JoystickRestTop, JoystickRadius * 2f, JoystickRadius * 2f);
        // Posisi diam = posisi templat; kenop di tengah alas (UXML menyimpan cuplikan kenop yang sedang ditarik).
        // Gaya sebaris UXML tidak terbaca lewat element.style (keyword Null), dan UiRoot menggeser elemen bawah pada
        // rasio lain sesudah tata letak pertama; jadi posisi diam dibaca dari resolvedStyle setiap tata letak berubah
        // selama alas ada di tempatnya. Templat baru saat jari masih menahan memakai posisi diam sebelumnya.
        VisualElement homePad = joystick;
        if (joystickPointer < 0) joystickAway = false;
        homePad.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (homePad == joystick && !joystickAway)
                joystickHome = new Vector2(homePad.resolvedStyle.left, homePad.resolvedStyle.top);
        });
        if (joystickKnob != null)
        {
            joystickKnob.pickingMode = PickingMode.Ignore;
            joystickKnob.style.left = 41f;
            joystickKnob.style.top = 41f;
            joystickKnob.style.translate = new Translate(0f, 0f);
        }
        AddJoystickZone();
        VisualElement surface = boundScreen;
        joystickSurface = surface;
        surface.RegisterCallback<PointerDownEvent>(BeginJoystick, TrickleDown.TrickleDown);
        surface.RegisterCallback<PointerMoveEvent>(MoveJoystick);
        surface.RegisterCallback<PointerUpEvent>(EndJoystick);
        surface.RegisterCallback<PointerCancelEvent>(evt => { if (evt.pointerId == joystickPointer) ResetJoystick(); });
        // Pengambilalihan jari oleh permukaan templat baru mengirim CaptureOut ke permukaan lama: abaikan.
        surface.RegisterCallback<PointerCaptureOutEvent>(_ => { if (surface == joystickSurface) ResetJoystick(); });
        if (joystickPointer >= 0)
        {
            PlaceJoystick(joystickCenter);
            surface.CapturePointer(joystickPointer);
            if (joystickKnob != null) joystickKnob.style.translate = new Translate(knobOffset.x, knobOffset.y);
        }
    }

    // Garasi (uji HP 27 Sep, "cari referensi"). Portrait mengikuti pola layar koleksi game kasual (Subway Surfers,
    // Hill Climb Racing): etalase besar skin terpilih di atas (truk besar di tengah, nama + keterangan di bawahnya),
    // kisi koleksi di bagian bawah menempel ke tepi bawah; ruang lebih pada layar tinggi masuk ke etalase, bukan jadi celah.
    // Mendatar lebar (20:9): pratinjau dan kisi bergeser setengah seperti judul, jadi susunan desain 1280 di tengah layar.
    private const float GarageGap = 20f, GarageBottom = 24f, GarageTextHeight = 120f;

    private void PinGarageGrid()
    {
        if (BaseScreen(toolkitScreen) != 6) return;
        VisualElement grid = boundScreen?.Q<VisualElement>("@grid");
        if (grid == null) return;
        if (!toolkitRoot.IsPortrait)
        {
            // Lebar semu membuat pusat jangkar tepat 640 (bagian geser 0,5); tinggi 0 menjaga posisi atas.
            VisualElement wide = boundScreen.Q<VisualElement>("@preview");
            if (wide != null) toolkitRoot.Anchor(wide, 24f, 108f, (640f - 24f) * 2f, 0f);
            toolkitRoot.Anchor(grid, 410f, 108f, (640f - 410f) * 2f, 0f);
            return;
        }
        VisualElement preview = boundScreen.Q<VisualElement>("preview");
        VisualElement stage = preview?.Q<VisualElement>("stage");
        VisualElement truck = stage?.Q<VisualElement>("truck");
        VisualElement text = stage?.Q<VisualElement>("@grup");
        if (preview == null || stage == null || truck == null || text == null) return;
        // Kisi: tinggi sesuai isi, menempel ke bawah (jangkar atas templat dilepas).
        toolkitRoot.Release(grid);
        grid.style.top = StyleKeyword.Auto;
        grid.style.height = StyleKeyword.Auto;
        grid.style.bottom = GarageBottom;
        // Etalase: kolom di tengah, mengisi dari bawah judul sampai di atas kisi.
        stage.style.flexDirection = FlexDirection.Column;
        stage.style.height = StyleKeyword.Auto;
        stage.style.flexGrow = 1f;
        preview.style.height = StyleKeyword.Auto;
        text.style.width = StyleKeyword.Auto;
        text.style.marginLeft = 0f;
        text.style.marginTop = 16f;
        text.style.alignItems = Align.Center;
        foreach (Label label in text.Query<Label>().ToList()) label.style.unityTextAlign = TextAnchor.UpperCenter;
        VisualElement screen = boundScreen;
        void Fit()
        {
            if (preview.panel == null || float.IsNaN(grid.layout.height) || grid.layout.height <= 0f) return;
            float bottom = screen.layout.height - grid.layout.yMin + GarageGap;
            if (Mathf.Abs(preview.resolvedStyle.bottom - bottom) > 0.5f) preview.style.bottom = bottom;
            // Truk sebesar ruang etalase (rasio gambar 96:204), dibatasi supaya tetap tajam.
            float room = preview.layout.height - GarageTextHeight - 40f;
            float height = Mathf.Clamp(room, 180f, 380f);
            if (Mathf.Abs(truck.resolvedStyle.height - height) > 0.5f)
            {
                truck.style.height = height;
                truck.style.width = height * 96f / 204f;
            }
        }
        grid.RegisterCallback<GeometryChangedEvent>(_ => Fit());
        preview.RegisterCallback<GeometryChangedEvent>(_ => Fit());
    }

    // Laporan uji HP 27 Sep: templat portrait 13 (singgah), dan 20 (pulang membawa paket) tidak punya joystick,
    // jadi analog hilang selama layar itu tampil. Layar menyetir tanpa joystick diberi alas yang sama dengan layar 10
    // (dijangkar ke posisi diam kanan bawah oleh BindJoystick), digeser ke bawah seperti elemen templat.
    private VisualElement AddMissingJoystick()
    {
        if (boundScreen == null || !toolkitRoot.IsPortrait) return null;
        bool driving = toolkitScreen >= 9 && toolkitScreen <= 13 || toolkitScreen == 17 || toolkitScreen == 20
            || toolkitScreen == 21 || toolkitScreen == 30;
        if (!driving) return null;
        var pad = new VisualElement { name = "joy", pickingMode = PickingMode.Ignore };
        pad.AddToClassList("sc-abs");
        pad.AddToClassList("sc-joy");
        var knob = new VisualElement { name = "knob", pickingMode = PickingMode.Ignore };
        knob.AddToClassList("sc-joy__knob");
        pad.Add(knob);
        boundScreen.Add(pad);
        return pad;
    }

    // Laporan uji HP 27 Sep ("joystick gak bisa di mana saja"): "layar" dan isinya picking-mode Ignore, jadi sentuhan di
    // tempat kosong tidak mengenai elemen apa pun dan BeginJoystick tidak pernah terpanggil; hanya alas joystick yang bisa
    // disentuh. Alas sentuh transparan menutup separuh bawah layar, paling belakang supaya tombol di atasnya tetap menang.
    private void AddJoystickZone()
    {
        VisualElement zone = boundScreen.Q<VisualElement>("joyzone");
        if (zone == null)
        {
            zone = new VisualElement { name = "joyzone", pickingMode = PickingMode.Position };
            boundScreen.Insert(0, zone);
        }
        // Bukan elemen templat: UiRoot tidak boleh menggesernya ke bawah seperti elemen jangkar bawah (20:9).
        toolkitRoot.Release(zone);
        zone.style.position = Position.Absolute;
        zone.style.left = 0f;
        zone.style.right = 0f;
        zone.style.bottom = 0f;
        zone.style.top = new Length((1f - JoystickZone) * 100f, LengthUnit.Percent);
    }

    private void BeginJoystick(PointerDownEvent evt)
    {
        if (!toolkitRoot.IsPortrait || joystickPointer >= 0 || HudInputLocked || IsInteractiveTarget(evt.target as VisualElement)) return;
        // Zona kendali = separuh bawah layar.
        Rect screen = boundScreen.worldBound;
        if (evt.position.y < screen.yMin + screen.height * (1f - JoystickZone)) return;
        joystickPointer = evt.pointerId;
        joystickCenter = evt.position;
        knobOffset = Vector2.zero;
        UiMotion.Stop(joystick);
        joystick.style.opacity = StyleKeyword.Null;
        PlaceJoystick(joystickCenter);
        UiMotion.Play(joystick, "joystick", "muncul (alas)");
        joystickSurface.CapturePointer(evt.pointerId);
        AdvanceJoystick(evt.position);
        evt.StopPropagation();
    }

    private void PlaceJoystick(Vector2 panelCenter)
    {
        if (joystick == null) return;
        Vector2 local = boundScreen.WorldToLocal(panelCenter);
        joystickAway = true;
        joystick.style.left = local.x - JoystickRadius;
        joystick.style.top = local.y - JoystickRadius;
    }

    private void MoveJoystick(PointerMoveEvent evt)
    {
        if (evt.pointerId == joystickPointer) AdvanceJoystick(evt.position);
    }

    private void EndJoystick(PointerUpEvent evt)
    {
        if (evt.pointerId != joystickPointer) return;
        ResetJoystick();
        evt.StopPropagation();
    }

    private static bool IsInteractiveTarget(VisualElement target)
    {
        for (VisualElement node = target; node != null; node = node.parent)
        {
            string elementName = node.name ?? string.Empty;
            if (elementName.StartsWith("btn:") || elementName.StartsWith("iconbtn:") ||
                elementName.StartsWith("ctl:") || elementName.StartsWith("side:") ||
                elementName == "modebtn" || elementName == "mini") return true;
        }
        return false;
    }

    // Arah dihitung dari pusat sentuhan pertama (bukan worldBound, yang belum ikut layout baru di frame yang sama).
    // Kenop mengikuti jari tanpa perataan, dibatasi jari-jari 88 px.
    private void AdvanceJoystick(Vector2 panelPosition)
    {
        if (joystick == null) return;
        knobOffset = Vector2.ClampMagnitude(panelPosition - joystickCenter, JoystickRadius);
        ApplyJoystick();
        if (joystickKnob != null) joystickKnob.style.translate = new Translate(knobOffset.x, knobOffset.y);
    }

    // Juga tiap frame selama jempol diam (UpdateToolkit): arah relatif ke truk berubah saat truk berbelok, jadi tanpa ini
    // truk terus berputar melewati arah jempol (uji HP 27 Sep).
    private void ApplyJoystick()
    {
        Vector2 value = new Vector2(knobOffset.x, -knobOffset.y) / JoystickRadius;
        float magnitude = value.magnitude;
        if (magnitude < 0.18f) value = Vector2.zero;
        driver.SetDirectionalInput(value, Mathf.InverseLerp(0.18f, 1f, magnitude));
    }

    // Lepas (gerak.json joystick): kenop kembali ke tengah 120 ms out-back, alas memudar 150 ms (jeda 120 ms),
    // lalu alas kembali ke posisi diamnya.
    private void ResetJoystick()
    {
        int releasedPointer = joystickPointer;
        joystickPointer = -1;
        if (joystickSurface != null && releasedPointer >= 0 && joystickSurface.HasPointerCapture(releasedPointer))
            joystickSurface.ReleasePointer(releasedPointer);
        driver?.HoldStill();
        if (releasedPointer < 0 || joystick == null) return;
        VisualElement pad = joystick, knob = joystickKnob;
        Vector2 from = knobOffset;
        knobOffset = Vector2.zero;
        Tween(pad, 0.27f, t =>
        {
            if (joystickPointer >= 0 || pad != joystick) return;
            float seconds = t * 0.27f;
            float back = UiMotion.Evaluate("out-back", Mathf.Clamp01(seconds / 0.12f));
            if (knob != null) knob.style.translate = new Translate(from.x * (1f - back), from.y * (1f - back));
            pad.style.opacity = 1f - UiMotion.Evaluate("in-cubic", Mathf.Clamp01((seconds - 0.12f) / 0.15f));
            if (t < 1f) return;
            pad.style.left = joystickHome.x;
            pad.style.top = joystickHome.y;
            joystickAway = false;
            pad.style.opacity = StyleKeyword.Null;
            UiMotion.Play(pad, "joystick", "muncul (alas)");
        });
    }

    // Gulir (cerita 25, garasi): browser di galeri desain memakai scrollbar tipis tanpa panah; scroller
    // bawaan UI Toolkit (abu-abu, tombol panah, 16 px) memakan lebar teks. Jadi batang 8 px, pegangan bulat, tanpa panah.
    private void StyleScrollers()
    {
        boundScreen?.Query<ScrollView>().ForEach(scroll =>
        {
            Scroller bar = scroll.verticalScroller;
            if (bar == null) return;
            bar.lowButton.style.display = DisplayStyle.None;
            bar.highButton.style.display = DisplayStyle.None;
            bar.style.width = 8f;
            bar.style.marginLeft = 4f;
            bar.style.borderLeftWidth = 0f;
            bar.style.backgroundColor = Color.clear;
            bar.slider.style.marginTop = bar.slider.style.marginBottom = 0f;
            VisualElement tracker = bar.slider.Q<VisualElement>(className: BaseSlider<float>.trackerUssClassName);
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(0.114f, 0.165f, 0.184f, 0.08f);
                tracker.style.borderTopWidth = tracker.style.borderBottomWidth = tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 0f;
                SetRadius(tracker, 4f);
            }
            VisualElement dragger = bar.slider.Q<VisualElement>(className: BaseSlider<float>.draggerUssClassName);
            if (dragger != null)
            {
                dragger.style.backgroundColor = new Color(0.114f, 0.165f, 0.184f, 0.35f);
                dragger.style.borderTopWidth = dragger.style.borderBottomWidth = dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 0f;
                dragger.style.left = 0f;
                dragger.style.width = 8f;
                SetRadius(dragger, 4f);
            }
        });
    }

    private static void SetRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
    }

    private void BindMinimap()
    {
        if (boundScreen == null || gameManager?.Town == null) return;
        DeliveryContentCatalog content = Resources.Load<DeliveryContentCatalog>("DeliveryContentCatalog");
        Sprite mapSprite = gameManager.Town.MapId == "blok-paket" ? content?.blokPaketMinimap : content?.kotaPaketMinimap;
        // Gambar peta = keluaran baker peta aktif (minimap-ui.png, seluruh extent). Lapisan rute/penanda/truk
        // dipasang di "peta-overlay": di minimap kecil saudara gambar "map", di peta besar anak "map".
        boundScreen.Query<VisualElement>(name: "peta-overlay").ForEach(overlay =>
        {
            bool big = overlay.parent?.name == "map";
            VisualElement image = big ? overlay.parent : overlay.parent?.Q<VisualElement>("map");
            if (image != null && mapSprite != null)
            {
                image.style.backgroundImage = new StyleBackground(mapSprite);
                if (big) image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.Add(new DeliveryMinimapElement(gameManager, image, big));
        });
    }

    private void BindScrim()
    {
        VisualElement scrim = boundScreen?.Q<VisualElement>("scrim");
        if (scrim != null)
            boundScreen.Query<VisualElement>(className: "dd-dialog").ForEach(dialog => dialog.pickingMode = PickingMode.Position);
        if (scrim != null && (toolkitScreen == 3 || toolkitScreen == 4 || toolkitScreen == 5 || toolkitScreen == 8))
            BindBackdrop(scrim);

        if (toolkitScreen == 18)
        {
            VisualElement bigMap = boundScreen.Q<VisualElement>("bigmap");
            if (bigMap != null) bigMap.pickingMode = PickingMode.Position;
            BindBackdrop(boundScreen.Q<VisualElement>("dim"));
        }
        else if (toolkitScreen == 25)
        {
            VisualElement story = boundScreen.Q<VisualElement>("story");
            if (story != null) story.pickingMode = PickingMode.Position;
            BindBackdrop(boundScreen.Q<VisualElement>("dim"));
        }
    }

    private void BindBackdrop(VisualElement backdrop)
    {
        if (backdrop == null) return;
        backdrop.pickingMode = PickingMode.Position;
        backdrop.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.button != 0 || evt.target != backdrop) return;
            DeliveryAudio.Play(DeliveryAudio.Cue.Click);
            CloseCurrentOverlay();
            evt.StopPropagation();
        });
    }

    // Popup ulang tutorial (permintaan pengguna 26 Sep): D5 tidak punya layar ini, jadi dialog layar 22 (popup di atas
    // HUD) dipakai ulang dan isinya diganti di sini (UXML dibangkitkan dari Design/, tidak disunting tangan).
    private void DressTutorialPrompt()
    {
        if (page != Page.TutorialPrompt || boundScreen == null) return;
        VisualElement summary = boundScreen.Q<VisualElement>("summary");
        if (summary != null) summary.style.display = DisplayStyle.None;
        VisualElement dialog = boundScreen.Q<VisualElement>(className: "dd-dialog");
        dialog?.RemoveFromClassList("dd-dialog--wide");
        Label title = dialog?.Q<Label>("title");
        if (title != null) title.text = "Ulang tutorial?";
        Label text = dialog?.Q<Label>("text");
        if (text != null) text.text = "Truk kembali ke titik mulai dan kamu belajar lagi dari awal. Koin dan skin tetap aman.";
        VisualElement yes = boundScreen.Q<VisualElement>("btn:ya-ke-lobby");
        if (yes == null) return;
        Label yesText = yes.Q<Label>("teks");
        if (yesText != null) yesText.text = "Ya, mulai";
        VisualElement glyph = yes.Q<VisualElement>(className: "dd-btn__icon");
        if (glyph != null) { glyph.RemoveFromClassList("ikon--i-check"); glyph.AddToClassList("ikon--i-play"); }
    }

    private void UpdateMinimapUi()
    {
        if (gameManager?.Minimap == null) return;
        VisualElement mini = boundScreen.Q<VisualElement>("mini");
        if (mini != null && toolkitScreen != 18)
            mini.style.display = gameManager.Minimap.Visible ? DisplayStyle.Flex : DisplayStyle.None;
        VisualElement toggle = boundScreen.Q<VisualElement>("iconbtn:map");
        if (toggle != null) toggle.EnableInClassList("is-on", gameManager.Minimap.Visible);
        Label pauseToggle = boundScreen.Q<VisualElement>("btn:peta-kecil-tampil")?.Q<Label>("teks");
        // teks.json: "Peta kecil: {tampil|sembunyi}", sepola dengan "Suara: aktif".
        if (pauseToggle != null) pauseToggle.text = gameManager.Minimap.Visible ? "Peta kecil: tampil" : "Peta kecil: sembunyi";
    }

    // Permintaan pengguna (26 Sep, menyimpang dari D5): portrait juga punya ikon peta di HUD untuk buka/tutup minimap,
    // sama seperti landscape (kiri tombol jeda, jarak 84 seperti landscape), bukan lewat menu jeda. Bar tujuan
    // dipendekkan 84 supaya tidak tertimpa; tombol "Peta kecil" di menu jeda portrait disembunyikan (landscape pun tidak punya).
    private void AddPortraitMapToggle()
    {
        if (!toolkitRoot.IsPortrait || boundScreen == null) return;
        VisualElement pauseMapRow = boundScreen.Q<VisualElement>("btn:peta-kecil-tampil");
        if (pauseMapRow != null) pauseMapRow.style.display = DisplayStyle.None;
        VisualElement pauseAnchor = boundScreen.Q<VisualElement>("@iconbtn:pause");
        if (pauseAnchor == null || boundScreen.Q<VisualElement>("iconbtn:map") != null) return;
        var anchor = new VisualElement { name = "@iconbtn:map", pickingMode = PickingMode.Ignore };
        anchor.AddToClassList("sc-abs");
        var button = new VisualElement { name = "iconbtn:map" };
        button.AddToClassList("dd-iconbtn");
        var glyph = new VisualElement { name = "ikon-map", pickingMode = PickingMode.Ignore };
        glyph.AddToClassList("dd-iconbtn__glyph");
        glyph.AddToClassList("ikon");
        glyph.AddToClassList("ikon--i-map");
        button.Add(glyph);
        anchor.Add(button);
        pauseAnchor.parent.Insert(pauseAnchor.parent.IndexOf(pauseAnchor), anchor);
        // Uji HP 28 Sep: dipasang lewat jangkar (acuan 720 lebar, 104 dari kanan, ikon 72) supaya ikut geseran tepi
        // kanan dan inset atas 20:9 seperti tombol jeda; dengan right/top tetap, ikon ini naik dan tidak sejajar.
        toolkitRoot.Anchor(anchor, 720f - 104f - 72f, 16f, 72f, 72f);
        VisualElement hudbar = boundScreen.Q<VisualElement>("@hudbar");
        if (hudbar != null) hudbar.style.width = 512f;
    }

    private void ToggleSmallMap()
    {
        gameManager.Minimap?.Toggle();
        // Templat sama (hanya minimap tampil/sembunyi, diatur UpdateMinimapUi): tidak dipasang ulang, supaya bar tujuan
        // tidak bergetar (laporan uji HP 27 Sep). Ekspres berganti 10 ⇄ 11, jadi templat baru hanya saat layarnya berubah.
        if (CurrentScreen() != toolkitScreen) RefreshToolkit(true);
        else UpdateToolkitText();
    }

    private void OpenBigMap()
    {
        ReleaseControls();
        gameManager.Minimap?.SetExpanded(true);
        Time.timeScale = 0f;
        driver.StopImmediately();
        RefreshToolkit(true);
    }

    private void CloseBigMap()
    {
        gameManager.Minimap?.SetExpanded(false);
        Time.timeScale = 1f;
        RefreshToolkit(true);
    }

    private void CloseCurrentOverlay()
    {
        if (gameManager?.Minimap?.Expanded == true) { CloseBigMap(); return; }
        if (page == Page.Shop && pendingSkin >= 0) { CancelShopDialog(); return; }
        if (page == Page.ExitWarning) { CancelExit(); return; }
        if (page == Page.ResetWarning) { SetPage(Page.Settings); return; }
        if (page == Page.FinishWork) { gameManager.AnswerFinishWork(false); return; }
        if (page == Page.TutorialPrompt) { gameManager.AnswerTutorialPrompt(false); return; }
        if (page == Page.Pause) { ResumeGame(); return; }
        ReturnToLobby();
    }

    private void OpenToolkitShop(Page from)
    {
        shopReturn = from;
        pendingSkin = -1;
        SetPage(Page.Shop);
    }

    private void SelectToolkitSkin(int index)
    {
        if (gameManager.Progress.Owns(index))
        {
            gameManager.SelectSkin(index);
            RefreshToolkit(true);
            return;
        }
        pendingSkin = index;
        insufficientCoins = gameManager.Progress.Coins < CourierProgress.Prices[index];
        RefreshToolkit(true);
    }

    private void BuyPendingSkin()
    {
        if (pendingSkin < 0) return;
        if (gameManager.SelectSkin(pendingSkin)) pendingSkin = -1;
        else insufficientCoins = true;
        RefreshToolkit(true);
    }

    private void CancelShopDialog() { pendingSkin = -1; RefreshToolkit(true); }
    private void ReturnToLobby() => SetPage(Page.Lobby);
    private void ToggleSound() { DeliveryAudio.Toggle(); RefreshToolkit(true); }
    private void ClaimDaily()
    {
        if (!gameManager.Progress.CanClaimDaily(DateTime.Now)) { ReturnToLobby(); return; }
        if (gameManager.Progress.ClaimDaily(DateTime.Now))
        {
            gameManager.Progress.Save();
            DeliveryAudio.Play(DeliveryAudio.Cue.Coin);
            claimAnimationDay = gameManager.Progress.DailyDay;
            // Uji HP 27 Sep: sesudah koin diambil popup menutup sendiri, setelah centang sempat terlihat.
            claimCloseAt = Time.unscaledTime + ClaimCloseDelay;
        }
        RefreshToolkit(true);
    }

    private const float ClaimCloseDelay = 0.9f;
    private float claimCloseAt = -1f;

    private void CloseDailyAfterClaim()
    {
        if (claimCloseAt < 0f) return;
        if (page != Page.Daily) { claimCloseAt = -1f; return; }
        if (Time.unscaledTime < claimCloseAt) return;
        claimCloseAt = -1f;
        ReturnToLobby();
    }

    private int claimAnimationDay;

    // gerak.json titik-merah: titik berdenyut terus selama kondisinya benar.
    private static void SetRedDot(VisualElement dot, bool visible)
    {
        if (dot == null) return;
        dot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (visible && !UiMotion.IsPlaying(dot)) UiMotion.Play(dot, "titik-merah");
        else if (!visible) UiMotion.Stop(dot);
    }

    private void UpdateDailyUi()
    {
        DateTime today = DateTime.Now;
        bool available = gameManager.Progress.CanClaimDaily(today);
        VisualElement lobbyReward = boundScreen.Q<VisualElement>("side:hadiah");
        VisualElement rewardDot = lobbyReward?.Q<VisualElement>("dot");
        SetRedDot(rewardDot, available);
        // hud.json titikMerah.garasi: ada skin publik yang belum dimiliki dan harganya ≤ koin.
        VisualElement garageButton = boundScreen.Q<VisualElement>("side:garasi")?.Q<VisualElement>("btn");
        if (garageButton != null)
        {
            bool affordable = false;
            for (int i = 1; i < CourierProgress.Prices.Length; i++)
                affordable |= !gameManager.Progress.Owns(i) && CourierProgress.Prices[i] <= gameManager.Progress.Coins;
            VisualElement garageDot = garageButton.Q<VisualElement>("dot");
            if (garageDot == null && affordable)
            {
                garageDot = new VisualElement { name = "dot", pickingMode = PickingMode.Ignore };
                garageDot.AddToClassList("sc-dot");
                garageButton.Add(garageDot);
            }
            SetRedDot(garageDot, affordable);
        }
        if (toolkitScreen != 3) return;

        int next = available ? gameManager.Progress.NextDailyDay(today) : gameManager.Progress.DailyDay % 7 + 1;
        int done = available ? (next == 1 ? 0 : next - 1) : gameManager.Progress.DailyDay;
        for (int day = 1; day <= CourierProgress.DailyRewards.Length; day++)
        {
            VisualElement card = boundScreen.Q<VisualElement>("day:" + day);
            if (card == null) continue;
            bool claimed = day <= done;
            bool ready = available && day == next;
            card.EnableInClassList("is-claimed", claimed);
            card.EnableInClassList("is-ready", ready);
            // gerak.json hari-siap: kartu siap berdenyut 1,04 selama popup terbuka.
            if (ready && !UiMotion.IsPlaying(card)) UiMotion.Play(card, "hari-siap");
            else if (!ready && UiMotion.IsPlaying(card)) { UiMotion.Stop(card); card.style.scale = StyleKeyword.Null; }
            card.EnableInClassList("is-next", !claimed && !ready);
            VisualElement doneMark = card.Q<VisualElement>("done");
            if (doneMark == null)
            {
                doneMark = new VisualElement { name = "done", pickingMode = PickingMode.Ignore };
                doneMark.AddToClassList("sc-day__done");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("dd-chip__icon");
                icon.AddToClassList("ikon");
                icon.AddToClassList("ikon--i-check");
                doneMark.Add(icon);
                card.Add(doneMark);
            }
            doneMark.style.display = claimed ? DisplayStyle.Flex : DisplayStyle.None;
            // gerak.json hari-ambil: lencana centang kartu yang baru diambil muncul memantul.
            if (claimed && day == claimAnimationDay) { claimAnimationDay = 0; UiMotion.Play(doneMark, "hari-ambil"); }
            Label state = card.Q<Label>("state");
            if (state != null)
            {
                int daysAway = (day - next + CourierProgress.DailyRewards.Length) % CourierProgress.DailyRewards.Length + (available ? 0 : 1);
                state.text = claimed ? "Diambil" : ready ? "Siap!" : Math.Max(1, daysAway) + " hari lagi";
            }
        }
        VisualElement claim = boundScreen.Q<VisualElement>("btn:ambil-koin");
        Label claimText = claim?.Q<Label>("teks");
        if (claimText != null)
            claimText.text = available ? $"Ambil {CourierProgress.DailyRewards[next - 1]} koin" : "Sampai besok!";
        // Disetujui pengguna (26 Sep): "Sampai besok!" hanya menutup popup, jadi tidak memakai gaya hadiah jingga
        // (anak bisa mengira masih ada koin); ikon koin juga disembunyikan.
        if (claim != null)
        {
            claim.EnableInClassList("dd-btn--reward", available);
            claim.EnableInClassList("dd-btn--quiet", !available);
            VisualElement claimIcon = claim.Q<VisualElement>("ikon-coin");
            if (claimIcon != null) claimIcon.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void UpdateModeUi()
    {
        if (toolkitScreen != 4) return;
        foreach (string mode in new[] { "kota-paket", "blok-paket", "classic" })
        {
            VisualElement card = boundScreen.Q<VisualElement>("mapcard:" + mode);
            if (card != null) card.EnableInClassList("is-selected", gameManager.Progress.LastMode == mode);
        }
    }

    private VisualElement shopCardsScreen;
    private string shopCardsSignature;

    private void UpdateShopUi()
    {
        if (toolkitScreen != 6 && toolkitScreen != 7 && toolkitScreen != 8 && toolkitScreen != 28) return;

        CourierProgress progress = gameManager.Progress;
        int equippedPublic = progress.Equipped;
        // Kartu dibangun ulang hanya saat kepemilikan/pilihan berubah, bukan tiap frame.
        string signature = $"{progress.OwnedMask}|{equippedPublic}";
        bool rebuild = shopCardsScreen != boundScreen || shopCardsSignature != signature;
        shopCardsScreen = boundScreen;
        shopCardsSignature = signature;
        for (int index = 0; rebuild && index < CourierProgress.Names.Length; index++)
        {
            string slug = CourierProgress.Names[index].ToLowerInvariant();
            VisualElement card = boundScreen.Q<VisualElement>("skin:" + slug);
            if (card == null) continue;

            bool owned = progress.Owns(index);
            bool equipped = equippedPublic == index;
            card.EnableInClassList("is-locked", !owned);
            card.EnableInClassList("is-equipped", equipped);
            BuildSkinCard(card, index, owned, equipped);
        }

        int selected = Mathf.Clamp(progress.Equipped, 0, CourierProgress.Names.Length - 1);
        VisualElement mainPreview = boundScreen.Q<VisualElement>("preview");
        if (mainPreview != null) UpdateSkinPreview(mainPreview, selected, false);

        if (pendingSkin < 0 || pendingSkin >= CourierProgress.Names.Length) return;
        VisualElement dialog = boundScreen.Q<VisualElement>(insufficientCoins ? "dialog:koin-kurang" : "dialog:mau-beli-ini");
        if (dialog == null) return;
        SetSkinClass(dialog.Q<VisualElement>("preview")?.Q<VisualElement>("truck"), pendingSkin, insufficientCoins);
        Label dialogName = dialog.Q<Label>("name");
        if (dialogName != null) dialogName.text = CourierProgress.Names[pendingSkin];
        Label dialogPrice = dialog.Q<VisualElement>("price")?.Q<Label>("teks");
        if (dialogPrice != null) dialogPrice.text = CourierProgress.Prices[pendingSkin].ToString();
        Label dialogText = insufficientCoins ? dialog.Q<Label>("text") : null;
        if (dialogText != null)
            dialogText.text = $"Kumpulkan {Mathf.Max(1, CourierProgress.Prices[pendingSkin] - progress.Coins)} koin lagi dengan mengantar paket.";
    }

    // Isi kartu skin dibangun ulang dengan susunan persis templat desain (06-garasi): kartu templat berbeda susunan
    // per keadaan (milikmu / dipakai / terkunci), jadi menambal kartu contoh meninggalkan gembok atau harga yang salah.
    private static void BuildSkinCard(VisualElement card, int index, bool owned, bool equipped)
    {
        card.Clear();
        var preview = new VisualElement { name = "preview", pickingMode = PickingMode.Ignore };
        preview.AddToClassList("dd-skin__preview");
        var truck = new VisualElement { name = "truck", pickingMode = PickingMode.Ignore };
        truck.AddToClassList("sc-bg");
        truck.AddToClassList("dd-skin__truck");
        SetSkinClass(truck, index, !owned);
        preview.Add(truck);
        if (equipped)
        {
            var tag = new Label("Dipakai") { name = "tag", pickingMode = PickingMode.Ignore };
            tag.AddToClassList("dd-skin__tag");
            preview.Add(tag);
        }
        if (!owned)
        {
            var lockMark = new VisualElement { name = "lock", pickingMode = PickingMode.Ignore };
            lockMark.AddToClassList("dd-skin__lock");
            lockMark.Add(Icon("ikon-lock", "dd-skin__lock-glyph", "ikon--i-lock"));
            preview.Add(lockMark);
        }
        card.Add(preview);
        var name = new Label(CourierProgress.Names[index]) { name = "name", pickingMode = PickingMode.Ignore };
        name.AddToClassList("dd-skin__name");
        card.Add(name);
        if (owned)
        {
            var free = new VisualElement { name = "free", pickingMode = PickingMode.Ignore };
            free.AddToClassList("dd-skin__free");
            free.Add(new Label(equipped ? "Dipakai" : "Milikmu") { name = "teks", pickingMode = PickingMode.Ignore });
            card.Add(free);
        }
        else
        {
            var price = new VisualElement { name = "price", pickingMode = PickingMode.Ignore };
            price.AddToClassList("dd-skin__price");
            price.Add(Icon("ikon-coin", "dd-skin__price-icon", "ikon--i-coin"));
            price.Add(new Label(CourierProgress.Prices[index].ToString()) { name = "teks", pickingMode = PickingMode.Ignore });
            card.Add(price);
        }
    }

    private static VisualElement Icon(string name, string role, string glyph)
    {
        var icon = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
        icon.AddToClassList(role);
        icon.AddToClassList("ikon");
        icon.AddToClassList(glyph);
        return icon;
    }

    private static void UpdateSkinPreview(VisualElement preview, int index, bool locked)
    {
        SetSkinClass(preview.Q<VisualElement>("truck"), index, locked);
        Label name = preview.Q<Label>("name");
        Label description = preview.Q<Label>("desc") ?? preview.Q<Label>("body");
        if (name != null) name.text = CourierProgress.Names[index];
        if (description != null) description.text = CourierProgress.Descriptions[index];
    }

    private static void SetSkinClass(VisualElement truck, int index, bool locked)
    {
        if (truck == null) return;
        for (int candidate = 0; candidate < CourierProgress.Names.Length; candidate++)
        {
            string candidateSlug = CourierProgress.Names[candidate].ToLowerInvariant();
            truck.RemoveFromClassList("a-skin-" + candidateSlug);
            truck.RemoveFromClassList("a-skin-" + candidateSlug + "-kunci");
        }
        string slug = CourierProgress.Names[index].ToLowerInvariant();
        truck.AddToClassList("a-skin-" + slug + (locked && index > 0 ? "-kunci" : string.Empty));
    }

    private void SetNestedLabel(string parentName, string labelName, string value)
    {
        Label label = boundScreen.Q<VisualElement>(parentName)?.Q<Label>(labelName);
        if (label != null) label.text = value;
    }

    private static string ModeName(string mode) => mode == "blok-paket" ? "Blok Paket" : mode == "classic" ? "Classic" : "Kota Paket";
}
