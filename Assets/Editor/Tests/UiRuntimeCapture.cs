using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeliveryDash.Classic;
using DeliveryDash.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.Editor.Tests
{
    // Tangkapan layar runtime (dunia + UI Toolkit) di Play Mode untuk dibandingkan dengan acuan desain.
    // Setiap layar dibuat dari keadaan permainan sungguhan (ambil paket, kontrak, tawaran bantuan, selesai kerja, ...).
    // Hasil: Logs/captures/ui-<nomor-layar>-<l|p>.png. Jalankan batch TANPA -nographics:
    // "Unity.exe -batchmode -projectPath . -executeMethod DeliveryDash.Editor.Tests.UiRuntimeCapture.CaptureBatch"
    // Kunci Courier.* dicadangkan sebelum dan dipulihkan sesudahnya; koin yang diubah untuk dialog beli hanya di memori.
    public static class UiRuntimeCapture
    {
        private const string Flag = "DeliveryDash.UiCapture";
        private const string ExitFlag = "DeliveryDash.UiCaptureExit";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string BackupKey = "DeliveryDash.UiCaptureBackup";

        private sealed class Shot
        {
            public string Name;
            public bool Portrait;
            public Vector2Int Size;
            public bool Aspect; // tangkapan rasio layar (-ddAspect): nama berkas diberi ukuran
            public Action<KidFriendlyHud, DeliveryGameManager> Setup;
            public double Wait = 1.2;
        }

        private static List<Shot> shots;
        private static int index;
        private static int stage;
        private static double waitUntil;
        // Bacaan di atas putih diambil 2 bingkai sesudah bacaan di atas hitam (bukan 0,25 dtk): elemen yang terus beranimasi
        // (timer ekspres, denyut) hampir tidak bergerak di antaranya sehingga alfa hasil gabungan tidak berbayang.
        private static int whiteFrame;
        private static Texture2D overBlack;
        private static int originalCoins, originalDeliveries;
        private static bool originalTutorial;
        private static string only;

        [MenuItem("Delivery Dash/UI/Tangkap Layar Runtime")]
        public static void Capture()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
            if (!CourierPrefsBackup.OpenScene("Assets/Scenes/KotaPaket.unity")) { Abort(); return; }
            // Dicadangkan sebelum Play (Start game memuat Courier.* ini); cadangan tertunda dipulihkan dulu.
            CourierPrefsBackup.Begin(BackupKey);
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        // Argumen opsional "-ddCapture 10-,19-" membatasi ke layar yang namanya diawali salah satu awalan itu.
        public static void CaptureBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-ddCapture");
            SessionState.SetString("DeliveryDash.UiCaptureOnly", at >= 0 && at + 1 < args.Length ? args[at + 1] : string.Empty);
            // -ddAspect: tiap layar ditangkap pada 20:9 (2400×1080), 16:10 (1280×800), 4:3 (1024×768) dan kebalikannya
            // untuk portrait, bukan 1280×720 → Logs/captures/rasio/ui-<layar>-<l|p>-<L>x<T>.png.
            SessionState.SetBool("DeliveryDash.UiCaptureAspect", Array.IndexOf(args, "-ddAspect") >= 0);
            // -ddJoy: hanya uji alas joystick portrait (720×1280 dan 1080×2400), log JOY_CHECK_OK / JOY_CHECK_FAIL.
            SessionState.SetBool(JoyFlag, Array.IndexOf(args, "-ddJoy") >= 0);
            SessionState.SetBool(ExitFlag, true);
            Capture();
        }

        private static void Abort()
        {
            Debug.Log("UI_CAPTURE_FAIL: dibatalkan");
            if (SessionState.GetBool(ExitFlag, false)) { SessionState.SetBool(ExitFlag, false); EditorApplication.Exit(1); }
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.update += Tick;
            // Play dihentikan / skrip dikompilasi ulang di tengah tangkapan: pulihkan Courier.* dan matikan flag.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingPlayMode) return;
                SessionState.SetBool(ClassicFlag, false);
                ClassicLauncher.ScreenOverride = null;
                if (!SessionState.GetBool(Flag, false)) return;
                SessionState.SetBool(Flag, false);
                SessionState.SetBool(JoyFlag, false);
                UiRoot.ScreenOverride = null;
                CourierPrefsBackup.End(BackupKey);
                Debug.LogWarning("UI_CAPTURE: Play berhenti sebelum selesai; Courier.* dipulihkan.");
            };
        }

        // Layar 26 (lobby Classic 1.0) ada di scene Classic terpisah (ClassicLauncher, bukan UiRoot/KidFriendlyHud).
        // Classic tidak memakai Courier.*, jadi tidak ada cadangan. Hasil: Logs/captures/ui-26-lobby-1-0-pintu-classic-<l|p>.png
        // (dengan -ddAspect juga ukuran rasio di Logs/captures/rasio).
        private const string ClassicFlag = "DeliveryDash.UiCaptureClassic";
        private const string ClassicName = "26-lobby-1-0-pintu-classic";

        public static void CaptureClassicBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            SessionState.SetBool("DeliveryDash.UiCaptureAspect", Array.IndexOf(args, "-ddAspect") >= 0);
            SessionState.SetBool(ExitFlag, true);
            if (!CourierPrefsBackup.OpenScene("Assets/Classic/Classic.unity")) { Abort(); return; }
            SessionState.SetBool(ClassicFlag, true);
            EditorApplication.EnterPlaymode();
        }

        private static void ClassicTick()
        {
            ClassicLauncher launcher = UnityEngine.Object.FindFirstObjectByType<ClassicLauncher>();
            UIDocument document = launcher == null ? null : launcher.GetComponent<UIDocument>();
            if (document?.panelSettings == null || Camera.main == null) return;
            try
            {
                if (shots == null)
                {
                    bool aspect = SessionState.GetBool("DeliveryDash.UiCaptureAspect", false);
                    shots = new List<Shot>();
                    foreach (bool portrait in new[] { false, true })
                        foreach (Vector2Int size in aspect ? AspectSizes : new[] { new Vector2Int(1280, 720) })
                            shots.Add(new Shot { Name = ClassicName, Portrait = portrait, Aspect = aspect, Wait = 0.8,
                                Size = portrait ? new Vector2Int(size.y, size.x) : size });
                    index = 0;
                    stage = 0;
                    Directory.CreateDirectory(aspect ? "Logs/captures/rasio" : "Logs/captures");
                }
                if (EditorApplication.timeSinceStartup < waitUntil) return;
                if (index >= shots.Count) { FinishClassic(true); return; }
                Shot shot = shots[index];
                PanelSettings panel = document.panelSettings;
                switch (stage)
                {
                    case 0:
                        ClassicLauncher.ScreenOverride = shot.Size;
                        panel.targetTexture = NewTarget(shot.Size);
                        panel.clearColor = true;
                        panel.colorClearValue = Color.black;
                        waitUntil = EditorApplication.timeSinceStartup + shot.Wait;
                        stage = 1;
                        break;
                    case 1:
                        overBlack = Read(panel.targetTexture);
                        panel.colorClearValue = Color.white;
                        document.rootVisualElement.MarkDirtyRepaint();
                        whiteFrame = Time.frameCount + 2;
                        stage = 2;
                        break;
                    case 2:
                        if (Time.frameCount < whiteFrame) return;
                        Texture2D overWhite = Read(panel.targetTexture);
                        Texture2D world = RenderWorld(shot.Size);
                        Texture2D result = Composite(world, overBlack, overWhite);
                        string suffix = shot.Portrait ? "p" : "l";
                        string path = shot.Aspect ? $"Logs/captures/rasio/ui-{shot.Name}-{suffix}-{shot.Size.x}x{shot.Size.y}.png"
                            : $"Logs/captures/ui-{shot.Name}-{suffix}.png";
                        LayoutReport(document.rootVisualElement, shot.Name + "-" + suffix, shot.Size);
                        File.WriteAllBytes(path, result.EncodeToPNG());
                        Debug.Log($"UI_CAPTURE {path}");
                        RenderTexture target = panel.targetTexture;
                        panel.targetTexture = null;
                        panel.clearColor = false;
                        if (target != null) UnityEngine.Object.DestroyImmediate(target);
                        UnityEngine.Object.DestroyImmediate(overBlack);
                        UnityEngine.Object.DestroyImmediate(overWhite);
                        UnityEngine.Object.DestroyImmediate(world);
                        UnityEngine.Object.DestroyImmediate(result);
                        index++;
                        stage = 0;
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                FinishClassic(false);
            }
        }

        private static void FinishClassic(bool ok)
        {
            SessionState.SetBool(ClassicFlag, false);
            ClassicLauncher.ScreenOverride = null;
            Debug.Log(ok ? $"UI_CAPTURE_OK: {index} gambar Classic" : "UI_CAPTURE_FAIL");
            shots = null;
            if (SessionState.GetBool(ExitFlag, false))
            {
                SessionState.SetBool(ExitFlag, false);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            else EditorApplication.ExitPlaymode();
        }

        private static void Tick()
        {
            if (SessionState.GetBool(ClassicFlag, false) && EditorApplication.isPlaying) { ClassicTick(); return; }
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            KidFriendlyHud hud = UnityEngine.Object.FindFirstObjectByType<KidFriendlyHud>();
            UiRoot root = UnityEngine.Object.FindFirstObjectByType<UiRoot>();
            DeliveryGameManager game = DeliveryGameManager.Instance;
            if (hud == null || root == null || game?.Town == null || game.Progress == null) return;
            try
            {
                if (shots == null) Begin(game);
                Step(hud, root, game);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(false, game);
            }
        }

        private static void Begin(DeliveryGameManager game)
        {
            originalCoins = game.Progress.Coins;
            originalDeliveries = game.Progress.CompletedDeliveries;
            originalTutorial = game.Progress.TutorialComplete;
            // Acuan desain menampilkan peta kecil; preferensi pemain dipulihkan di Finish.
            if (!game.Minimap.Visible) game.Minimap.Toggle();
            only = SessionState.GetString("DeliveryDash.UiCaptureOnly", string.Empty);
            bool aspect = SessionState.GetBool("DeliveryDash.UiCaptureAspect", false);
            joy = SessionState.GetBool(JoyFlag, false);
            joyPassed = 0;
            joyFailed = 0;
            Vector2Int[] sizes = joy ? new[] { new Vector2Int(1280, 720), new Vector2Int(2400, 1080) }
                : aspect ? AspectSizes : new[] { new Vector2Int(1280, 720) };
            aspect |= joy;
            shots = new List<Shot>();
            foreach (bool portrait in joy ? new[] { true } : new[] { false, true })
                foreach (Vector2Int size in sizes)
                    foreach (Shot shot in joy ? new[] { JoystickReset() } : Scenarios())
                    {
                        shot.Portrait = portrait;
                        shot.Size = portrait ? new Vector2Int(size.y, size.x) : size;
                        shot.Aspect = aspect;
                        if (Wanted(shot.Name)) shots.Add(shot);
                    }
            index = 0;
            stage = 0;
            Directory.CreateDirectory(aspect ? "Logs/captures/rasio" : "Logs/captures");
        }

        private const string JoyFlag = "DeliveryDash.UiCaptureJoy";
        private static bool joy;
        private static int joyPassed, joyFailed;

        // Uji alas joystick portrait: posisi diam = cermin templat di kanan (left 452, top 922 pada acuan 720×1280; alas di bawah
        // layar ikut tepi bawah pada layar lebih tinggi, lihat UiRoot.ApplyAnchors), lalu alas dipindah ke "jari" dan
        // ResetJoystick harus mengembalikannya ke posisi itu (bukan 0,0). Langkah dijadwalkan di panel (waktu nyata);
        // gambar diambil sesudah langkah terakhir sebagai bukti.
        private static Shot JoystickReset() => new Shot { Name = "joy-reset", Wait = 2.4, Setup = (hud, game) =>
        {
            Reset(hud, game);
            SetPage(hud, "Game");
            UiRoot root = UnityEngine.Object.FindFirstObjectByType<UiRoot>();
            string size = $"{UiRoot.ScreenOverride.Value.x}x{UiRoot.ScreenOverride.Value.y}";
            root.Root.schedule.Execute(() =>
            {
                VisualElement pad = root.Root.Q<VisualElement>("joy");
                VisualElement screen = root.Root.Q<VisualElement>("layar");
                if (pad == null || screen == null) { JoyResult(size, false, "alas joystick tidak ada di layar Game"); return; }
                Vector2 expected = new Vector2(720f - 92f - 176f, 922f + screen.layout.height - 1280f); // diam di kanan bawah
                Vector2 home = Position(pad);
                Vector2 stored = (Vector2)hud.GetType().GetField("joystickHome", Any).GetValue(hud);
                Rect area = screen.worldBound;
                // Sentuhan sungguhan di tempat kosong separuh bawah (kiri, tengah, kanan) harus mengenai layar, bukan lolos.
                string misses = string.Empty;
                foreach (Vector2 at in new[] { new Vector2(0.12f, 0.9f), new Vector2(0.5f, 0.62f), new Vector2(0.88f, 0.7f), new Vector2(0.2f, 0.55f) })
                {
                    Vector2 point = new Vector2(area.xMin + area.width * at.x, area.yMin + area.height * at.y);
                    Rect zoneBox = screen.Q<VisualElement>("joyzone")?.layout ?? Rect.zero;
                    VisualElement hit = screen.panel.Pick(point);
                    if (hit == null || !(hit == screen || screen.Contains(hit))) misses += $" {at}->{hit?.name}/{hit?.parent?.name} zona {zoneBox} layar {screen.layout}";
                }
                SetField(hud, "joystickPointer", 7);
                Call(hud, "PlaceJoystick", new Vector2(area.xMin + area.width * 0.7f, area.yMin + area.height * 0.8f));
                root.Root.schedule.Execute(() =>
                {
                    Vector2 away = Position(pad);
                    Call(hud, "ResetJoystick");
                    root.Root.schedule.Execute(() =>
                    {
                        Vector2 back = Position(pad);
                        bool ok = misses.Length == 0 && Near(home, expected) && Near(stored, expected) && !Near(away, expected) && Near(back, expected);
                        JoyResult(size, ok, $"templat {expected}, diam {home}, joystickHome {stored}, dipindah {away}, sesudah ResetJoystick {back}, sentuhan lolos:{(misses.Length == 0 ? " tidak ada" : misses)}");
                    }).StartingIn(700);
                }).StartingIn(200);
            }).StartingIn(800);
        } };

        private static Vector2 Position(VisualElement element) => new Vector2(element.resolvedStyle.left, element.resolvedStyle.top);
        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1f;

        private static void JoyResult(string size, bool ok, string detail)
        {
            if (ok) joyPassed++; else joyFailed++;
            Debug.Log($"JOY_CHECK {size} {(ok ? "PASS" : "FAIL")}: {detail}");
        }

        private static readonly Vector2Int[] AspectSizes = { new Vector2Int(2400, 1080), new Vector2Int(1280, 800), new Vector2Int(1024, 768) };

        private static bool Wanted(string name)
        {
            if (string.IsNullOrEmpty(only)) return true;
            foreach (string prefix in only.Split(','))
                if (name.StartsWith(prefix.Trim(), StringComparison.Ordinal)) return true;
            return false;
        }

        private static IEnumerable<Shot> Scenarios()
        {
            yield return new Shot { Name = "01-layar-memuat", Wait = 0.6, Setup = (hud, game) =>
            {
                Reset(hud, game);
                SetPage(hud, "Lobby");
                SetField(hud, "loadingShown", false);
            } };
            yield return Menu("02-lobby", "Lobby");
            yield return Menu("03-hadiah-harian-popup", "Daily");
            yield return Menu("04-pilih-kota", "Mode");
            yield return Menu("05-pengaturan", "Settings");
            yield return new Shot { Name = "06-garasi", Setup = (hud, game) => { Reset(hud, game); Call(hud, "OpenToolkitShop", Page("Lobby")); } };
            yield return new Shot { Name = "07-dialog-beli", Setup = (hud, game) => BuyDialog(hud, game, 100000) };
            yield return new Shot { Name = "08-dialog-koin-kurang", Setup = (hud, game) => BuyDialog(hud, game, 0) };
            // Progres tutorial hanya diubah di memori; Reset/Finish mengembalikannya dan PlayerPrefs dicadangkan.
            yield return new Shot { Name = "09-mulai-kerja-tutorial", Setup = (hud, game) =>
            {
                Reset(hud, game);
                game.Progress.TutorialComplete = false;
                game.Progress.CompletedDeliveries = 0;
                game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
                SetPage(hud, "Game");
            } };
            yield return new Shot { Name = "10-mengantar-paket", Wait = 1.6, Setup = (hud, game) => Trip(hud, game, a => a.IsExpress == false && a.IsFragile == false && a.HasStopover == false) };
            // Acuan 11 = peta kecil tertutup: disembunyikan lewat Minimap.Toggle supaya tombol peta juga tidak is-on.
            yield return new Shot { Name = "11-paket-ekspres", Wait = 1.6, Setup = (hud, game) =>
            {
                Trip(hud, game, a => a.IsExpress);
                if (game.Minimap.Visible) game.Minimap.Toggle();
            } };
            yield return new Shot { Name = "12-paket-rapuh", Wait = 1.6, Setup = (hud, game) => Trip(hud, game, a => a.IsFragile) };
            yield return new Shot { Name = "13-singgah", Wait = 1.6, Setup = (hud, game) =>
            {
                Trip(hud, game, a => a.HasStopover);
                if (!game.Activities.GuideToBonus) game.Activities.ToggleBonusGuide();
            } };
            // Ditangkap saat label +N koin sedang tampil (koin-plus 0,12–0,9 dtk).
            yield return new Shot { Name = "15-paket-sampai", Wait = 0.5, Setup = (hud, game) =>
            {
                Trip(hud, game, a => a.IsExpress == false && a.IsFragile == false && a.HasStopover == false);
                if (game.ActiveMarker == null) return;
                game.Car.PlaceAt(game.ActiveMarker.transform.position, game.Town.StartHeading);
                for (int i = 0; i < 6 && game.Carrying; i++) game.AdvanceActiveDwell(0.5f);
            } };
            // 14 (sorotan rumah tujuan): game belum memanggil sorotan rumah (hanya saat Selesai kerja diminta, layar 20/21),
            // jadi coroutine sorotan yang sama dijalankan ke rumah tujuan dan ditangkap di tengahnya (seperti 20).
            yield return new Shot { Name = "14-sorotan-tujuan", Wait = 0.9, Setup = (hud, game) =>
            {
                Trip(hud, game, a => a.IsExpress == false && a.IsFragile == false && a.HasStopover == false);
                MethodInfo preview = typeof(DeliveryGameManager).GetMethod("PreviewDestination", Any);
                if (preview == null) { Debug.LogWarning("UI_CAPTURE: PreviewDestination tidak ada, 14 dilewati."); return; }
                Coroutine running = game.StartCoroutine((System.Collections.IEnumerator)preview.Invoke(game, new object[] { game.TargetPosition }));
                SetField(game, "destinationPreview", running);
            } };
            yield return new Shot { Name = "16-tawaran-bantuan-warga", Wait = 1.6, Setup = (hud, game) =>
            {
                Reset(hud, game);
                SetPage(hud, "Game");
                game.SideQuests.TryOffer(DeliverySideQuests.Kind.Rest);
            } };
            yield return new Shot { Name = "17-membawa-surat", Wait = 1.6, Setup = (hud, game) =>
            {
                Reset(hud, game);
                SetPage(hud, "Game");
                if (game.SideQuests.TryOffer(DeliverySideQuests.Kind.Letter))
                    game.SideQuests.Quests[game.SideQuests.Quests.Count - 1].Collected = true;
            } };
            yield return new Shot { Name = "18-peta-besar", Setup = (hud, game) =>
            {
                Reset(hud, game);
                SetPage(hud, "Game");
                if (!game.Minimap.Visible) game.Minimap.Toggle();
                Call(hud, "OpenBigMap");
            } };
            yield return new Shot { Name = "19-menu-jeda", Setup = (hud, game) => { Reset(hud, game); SetPage(hud, "Game"); SetPage(hud, "Pause"); } };
            yield return new Shot { Name = "20-pulang-saat-masih-membawa-paket", Wait = 0.9, Setup = (hud, game) =>
            {
                // Ditangkap di tengah sorotan (kartu kuning + timer), seperti acuan desain.
                Trip(hud, game, _ => true);
                game.RequestFinishWork();
            } };
            yield return new Shot { Name = "21-pulang-ke-parkiran", Wait = 1.6, Setup = (hud, game) =>
            {
                Reset(hud, game);
                SetPage(hud, "Game");
                game.RequestFinishWork();
                game.CancelDestinationPreview();
            } };
            yield return new Shot { Name = "22-selesai-kerja", Setup = (hud, game) => { Reset(hud, game); SetPage(hud, "Game"); SetPage(hud, "FinishWork"); } };
            yield return Menu("23-keluar-dari-game", "ExitWarning");
            yield return Menu("24-hapus-progres", "ResetWarning");
            yield return Menu("25-cerita-dan-kredit", "About");
        }

        private static Shot Menu(string name, string page) =>
            new Shot { Name = name, Setup = (hud, game) => { Reset(hud, game); SetPage(hud, page); } };

        private static void Reset(KidFriendlyHud hud, DeliveryGameManager game)
        {
            game.Progress.Coins = originalCoins;
            game.Progress.CompletedDeliveries = originalDeliveries;
            game.Progress.TutorialComplete = originalTutorial;
            SetField(hud, "pendingSkin", -1);
            SetField(hud, "insufficientCoins", false);
            SetField(hud, "loadingShown", true); // layar memuat hanya di skenario 01
            SetField(hud, "loadingStart", -1f);
            SetField(hud, "coinTarget", -1); // koin dipulihkan tangkapan bukan "koin bertambah": jangan munculkan +N
            game.CancelDestinationPreview();
            if (!game.Minimap.Visible) game.Minimap.Toggle(); // 11 menutupnya
            if (game.Minimap.Expanded) game.Minimap.SetExpanded(false);
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
        }


        private static void BuyDialog(KidFriendlyHud hud, DeliveryGameManager game, int coins)
        {
            Reset(hud, game);
            Call(hud, "OpenToolkitShop", Page("Lobby"));
            int skin = -1;
            for (int i = CourierProgress.Prices.Length - 1; i >= 1; i--)
                if (!game.Progress.Owns(i)) { skin = i; break; }
            if (skin < 0) { Debug.LogWarning("UI_CAPTURE: semua skin sudah dimiliki, dialog beli dilewati."); return; }
            game.Progress.Coins = coins;
            Call(hud, "SelectToolkitSkin", skin);
        }

        // Ambil paket sungguhan: truk ditaruh di zona paket lalu jeda berhenti dimajukan (HANDOFF 14).
        private static void Pickup(DeliveryGameManager game, bool skipPreview)
        {
            if (game.ActiveMarker == null) return;
            game.Car.PlaceAt(game.ActiveMarker.transform.position, game.Town.StartHeading);
            for (int i = 0; i < 6 && !game.Carrying; i++) game.AdvanceActiveDwell(0.5f);
            if (skipPreview) game.CancelDestinationPreview();
            game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
        }

        private static void Trip(KidFriendlyHud hud, DeliveryGameManager game, Func<DeliveryActivities, bool> wanted)
        {
            Reset(hud, game);
            SetPage(hud, "Game");
            Pickup(game, true);
            for (int i = 0; i < 80 && !wanted(game.Activities); i++) game.Activities.BeginTrip(false);
            if (!wanted(game.Activities)) Debug.LogWarning("UI_CAPTURE: keadaan kontrak yang diminta tidak didapat.");
        }

        private static void Step(KidFriendlyHud hud, UiRoot root, DeliveryGameManager game)
        {
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            if (index >= shots.Count) { Finish(true, game); return; }
            Shot shot = shots[index];
            Vector2Int size = shot.Size;
            PanelSettings panel = root.GetComponent<UIDocument>().panelSettings;
            switch (stage)
            {
                case 0:
                    // Target dipasang sebelum layar dibuka supaya tata letak dihitung dari ukuran gambar, bukan layar batch.
                    UiRoot.ScreenOverride = size;
                    panel.targetTexture = NewTarget(size);
                    panel.clearColor = true;
                    panel.colorClearValue = Color.black;
                    shot.Setup(hud, game);
                    waitUntil = EditorApplication.timeSinceStartup + shot.Wait;
                    stage = 1;
                    break;
                case 1:
                    overBlack = Read(panel.targetTexture);
                    panel.colorClearValue = Color.white;
                    root.Root?.MarkDirtyRepaint();
                    whiteFrame = Time.frameCount + 2;
                    stage = 2;
                    break;
                case 2:
                    if (Time.frameCount < whiteFrame) return;
                    Texture2D overWhite = Read(panel.targetTexture);
                    Texture2D world = RenderWorld(size);
                    Texture2D result = Composite(world, overBlack, overWhite);
                    string path = shot.Aspect
                        ? $"Logs/captures/rasio/ui-{shot.Name}-{(shot.Portrait ? "p" : "l")}-{size.x}x{size.y}.png"
                        : $"Logs/captures/ui-{shot.Name}-{(shot.Portrait ? "p" : "l")}.png";
                    LayoutReport(root.Root, shot.Name + (shot.Portrait ? "-p" : "-l"), size);
                    File.WriteAllBytes(path, result.EncodeToPNG());
                    Debug.Log($"UI_CAPTURE {path}");
                    RenderTexture target = panel.targetTexture;
                    panel.targetTexture = null;
                    panel.clearColor = false;
                    if (target != null) UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(overBlack);
                    UnityEngine.Object.DestroyImmediate(overWhite);
                    UnityEngine.Object.DestroyImmediate(world);
                    UnityEngine.Object.DestroyImmediate(result);
                    index++;
                    stage = 0;
                    break;
            }
        }

        // Cek tata letak otomatis tiap tangkapan (log UI_LAYOUT): elemen yang bisa disentuh (pickingMode Position, terlihat,
        // bukan lapisan selebar layar) harus di dalam panel, sisi terpendek ≥ 64 unit acuan, dan tidak bertumpuk dengan
        // elemen sentuh lain yang bukan induk/anaknya. Tumpukan yang sudah ada di 1280×720 berarti bawaan desain.
        private static void LayoutReport(VisualElement panelRoot, string name, Vector2Int size)
        {
            if (panelRoot == null) return;
            Rect panel = panelRoot.worldBound;
            // Urutan pohon = urutan gambar. Elemen di belakang lapisan penuh layar yang bisa disentuh (scrim popup) tidak bisa
            // disentuh, jadi hanya lapisan teratas + isinya dan yang sesudahnya yang diperiksa.
            var ordered = new List<VisualElement>();
            panelRoot.Query<VisualElement>().ForEach(ordered.Add);
            int top = -1;
            for (int i = 0; i < ordered.Count; i++)
                if (FullScreenLayer(ordered[i], panel)) top = i;
            var touch = new List<VisualElement>();
            for (int i = Math.Max(0, top); i < ordered.Count; i++)
                if (Touchable(ordered[i], panel)) touch.Add(ordered[i]);
            var issues = new List<string>();
            foreach (VisualElement element in touch)
            {
                Rect r = element.worldBound;
                if (r.xMin < panel.xMin - 0.5f || r.yMin < panel.yMin - 0.5f || r.xMax > panel.xMax + 0.5f || r.yMax > panel.yMax + 0.5f)
                    issues.Add($"keluar-layar {element.name} [{r.xMin:0},{r.yMin:0} {r.width:0}x{r.height:0}]");
                if (Mathf.Min(r.width, r.height) < 63.5f) issues.Add($"kecil {element.name} {r.width:0}x{r.height:0}");
            }
            for (int i = 0; i < touch.Count; i++)
                for (int j = i + 1; j < touch.Count; j++)
                {
                    VisualElement a = touch[i], b = touch[j];
                    if (Related(a, b)) continue;
                    Rect ra = a.worldBound, rb = b.worldBound;
                    float w = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin), h = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);
                    if (w > 2f && h > 2f) issues.Add($"tumpuk {a.name} × {b.name} ({w:0}x{h:0})");
                }
            Debug.Log($"UI_LAYOUT {name} {size.x}x{size.y} panel {panel.width:0}x{panel.height:0}: {touch.Count} elemen sentuh; "
                + (issues.Count == 0 ? "OK" : string.Join("; ", issues)));
        }

        private static bool Touchable(VisualElement element, Rect panel)
        {
            if (element.pickingMode != PickingMode.Position || element.name == "layar" || element.panel == null) return false;
            Rect r = element.worldBound;
            if (r.width <= 0f || r.height <= 0f || r.width >= panel.width * 0.9f && r.height >= panel.height * 0.9f) return false;
            for (VisualElement at = element; at != null; at = at.parent)
                if (at.resolvedStyle.display == DisplayStyle.None || at.resolvedStyle.visibility == Visibility.Hidden || at.resolvedStyle.opacity < 0.01f)
                    return false;
            return true;
        }

        private static bool FullScreenLayer(VisualElement element, Rect panel)
        {
            if (element.pickingMode != PickingMode.Position || element.name == "layar" || element.panel == null) return false;
            Rect r = element.worldBound;
            if (r.width < panel.width * 0.9f || r.height < panel.height * 0.9f) return false;
            for (VisualElement at = element; at != null; at = at.parent)
                if (at.resolvedStyle.display == DisplayStyle.None || at.resolvedStyle.visibility == Visibility.Hidden || at.resolvedStyle.opacity < 0.01f)
                    return false;
            return true;
        }

        private static bool Related(VisualElement a, VisualElement b)
        {
            for (VisualElement at = a.parent; at != null; at = at.parent) if (at == b) return true;
            for (VisualElement at = b.parent; at != null; at = at.parent) if (at == a) return true;
            return false;
        }

        private static object Page(string page) =>
            Enum.Parse(typeof(KidFriendlyHud).GetNestedType("Page", BindingFlags.NonPublic), page);

        private static void SetPage(KidFriendlyHud hud, string page) => Call(hud, "SetPage", Page(page));

        private static void Call(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, Any);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, args);
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, Any);
            if (info == null) throw new MissingFieldException(target.GetType().Name, field);
            info.SetValue(target, value);
        }

        private static RenderTexture NewTarget(Vector2Int size)
        {
            // Format sama seperti layar; di proyek Linear GPU mencampur UI di ruang linear (Composite mengikutinya).
            var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            target.Create();
            return target;
        }

        private static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        private static Texture2D RenderWorld(Vector2Int size)
        {
            Camera camera = Camera.main;
            RenderTexture target = NewTarget(size);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            Texture2D image = Read(target);
            UnityEngine.Object.DestroyImmediate(target);
            return image;
        }

        // UI dirender dua kali (di atas hitam dan putih) supaya alfa tepat: 1 − a = putih − hitam, warna premultiplied = hitam.
        // Proyek memakai color space Gamma (campuran sRGB = browser, lihat StaticChecks). Kalau suatu saat Linear, GPU
        // mencampur UI di ruang linear, jadi rumus itu hanya benar setelah piksel dilinearkan (cabang linear di bawah).
        private static Texture2D Composite(Texture2D world, Texture2D black, Texture2D white)
        {
            Color[] w = world.GetPixels(), b = black.GetPixels(), wh = white.GetPixels();
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var output = new Color[w.Length];
            for (int i = 0; i < w.Length; i++)
            {
                Color bl = b[i], whi = wh[i], wo = w[i];
                if (linear) { bl = bl.linear; whi = whi.linear; wo = wo.linear; }
                float rest = Mathf.Clamp01(((whi.r - bl.r) + (whi.g - bl.g) + (whi.b - bl.b)) / 3f);
                var mixed = new Color(bl.r + wo.r * rest, bl.g + wo.g * rest, bl.b + wo.b * rest, 1f);
                output[i] = linear ? mixed.gamma : mixed;
            }
            var result = new Texture2D(world.width, world.height, TextureFormat.RGB24, false);
            result.SetPixels(output);
            result.Apply();
            return result;
        }

        private static void Finish(bool ok, DeliveryGameManager game)
        {
            SessionState.SetBool(Flag, false);
            UiRoot.ScreenOverride = null;
            if (game != null && game.Progress != null && shots != null)
            {
                game.Progress.Coins = originalCoins;
                game.Progress.CompletedDeliveries = originalDeliveries;
                game.Progress.TutorialComplete = originalTutorial;
            }
            CourierPrefsBackup.End(BackupKey);
            if (joy && shots != null)
            {
                bool passed = joyFailed == 0 && joyPassed == shots.Count;
                Debug.Log(passed ? $"JOY_CHECK_OK: {joyPassed} ukuran" : $"JOY_CHECK_FAIL: {joyPassed} lulus, {joyFailed} gagal dari {shots.Count}");
                ok &= passed;
                joy = false;
                SessionState.SetBool(JoyFlag, false);
            }
            Debug.Log(ok ? $"UI_CAPTURE_OK: {index} gambar di Logs/captures" : "UI_CAPTURE_FAIL");
            shots = null;
            if (SessionState.GetBool(ExitFlag, false))
            {
                SessionState.SetBool(ExitFlag, false);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            else EditorApplication.ExitPlaymode();
        }
    }
}
