using System;
using System.IO;
using System.Text;
using DeliveryDash.Classic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DeliveryDash.Editor.Tests
{
    // Regresi tinjauan kode 26 Sep: sesudah Mulai di Classic (landscape) pemain harus bisa kembali ke lobby tanpa menutup
    // aplikasi. Diuji di Play Mode scene Classic untuk desktop 1280×720 dan Android landscape tiruan 2400×1080 (layar sentuh):
    // tombol Jeda, Escape/Kembali Android (escapeKey), lalu Keluar dari lobby Classic memuat KotaPaket.
    // Batch: "Unity.exe -batchmode -executeMethod DeliveryDash.Editor.Tests.ClassicExitChecks.RunBatch"
    // Hasil: Logs/classic-exit-checks.txt (PASS/FAIL).
    public static class ClassicExitChecks
    {
        private const string Flag = "DeliveryDash.ClassicExitChecks";
        private const string ExitFlag = "DeliveryDash.ClassicExitChecks.Exit";
        private static int step;
        private static int waitFrame;
        private static int deadline;
        private static int checks;
        private static Keyboard addedKeyboard;
        private static Touchscreen addedTouch;
        private static InputSettings.EditorInputBehaviorInPlayMode? savedInputBehavior;
        private static InputSettings.BackgroundBehavior? savedBackground;

        [MenuItem("Delivery Dash/Uji Keluar dari Classic")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
            if (!CourierPrefsBackup.OpenScene("Assets/Classic/Classic.unity")) { Finish(false, "scene Classic tidak dapat dibuka"); return; }
            step = 0;
            checks = 0;
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(ExitFlag, true);
            Run();
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL Classic: " + description);
            checks++;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if (Time.frameCount < waitFrame) return;
            try
            {
                ClassicLauncher launcher = UnityEngine.Object.FindFirstObjectByType<ClassicLauncher>();
                VisualElement root = launcher == null ? null : launcher.GetComponent<UIDocument>()?.rootVisualElement;
                switch (step)
                {
                    case 0: // Desktop landscape.
                        if (root == null) return;
                        ClassicLauncher.ScreenOverride = new Vector2Int(1280, 720);
                        Next(3);
                        break;
                    case 1:
                        Check(!launcher.Started && Time.timeScale == 0f && root.Q("btn:mulai") != null, "lobby Classic tampil sebelum Mulai");
                        Click(root.Q("btn:mulai"));
                        Check(launcher.Started && Time.timeScale == 1f, "Mulai menjalankan Classic");
                        Check(root.Q("btn:jeda") != null, "tombol Jeda ada saat main di desktop");
                        if (Touchscreen.current == null) Check(FindLabel(root, "GAS") == null, "desktop tanpa layar sentuh tidak menampilkan kontrol sentuh");
                        Click(root.Q("btn:jeda"));
                        Check(!launcher.Started && Time.timeScale == 0f, "Jeda menghentikan permainan");
                        Check(root.Q("btn:mulai") != null && root.Q("btn:keluar") != null, "Jeda kembali ke lobby Classic dengan Mulai dan Keluar");
                        // Escape: mulai lagi lalu tekan escapeKey lewat Input System.
                        Click(root.Q("btn:mulai"));
                        Check(launcher.Started, "Mulai lagi sesudah jeda");
                        if (Keyboard.current == null) addedKeyboard = InputSystem.AddDevice<Keyboard>();
                        // Di editor (apalagi batch) keyboard hanya sampai ke game kalau Game View fokus; paksa selama tes.
                        savedInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        savedBackground = InputSystem.settings.backgroundBehavior;
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));
                        Next(2);
                        break;
                    case 2:
                        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                        Check(!launcher.Started && Time.timeScale == 0f && root.Q("btn:keluar") != null, "Escape/Kembali saat main membuka lobby Classic");
                        // Android landscape tiruan: layar sentuh + rasio 20:9.
                        if (Touchscreen.current == null) addedTouch = InputSystem.AddDevice<Touchscreen>();
                        ClassicLauncher.ScreenOverride = new Vector2Int(2400, 1080);
                        Next(3);
                        break;
                    case 3:
                        Click(root.Q("btn:mulai"));
                        Check(launcher.Started && Time.timeScale == 1f, "Mulai di Android landscape");
                        Check(FindLabel(root, "GAS") != null && FindLabel(root, "REM") != null
                            && FindLabel(root, "<") != null && FindLabel(root, ">") != null, "kontrol sentuh tampil di Android landscape");
                        Check(root.Q("btn:jeda") != null, "tombol Jeda ada di Android landscape");
                        Next(2);
                        break;
                    case 4:
                    {
                        VisualElement pause = root.Q("btn:jeda");
                        Rect box = pause.worldBound;
                        Rect gas = FindLabel(root, "GAS").worldBound;
                        Check(box.width > 0f && !box.Overlaps(gas) && !box.Overlaps(FindLabel(root, "<").worldBound), "Jeda tidak menutupi kontrol sentuh");
                        Check(box.xMin >= 0f && box.yMin >= 0f && box.xMax <= root.worldBound.xMax && box.yMax <= root.worldBound.yMax, "Jeda berada di dalam layar");
                        launcher.Back(); // tombol Kembali Android = escapeKey, jalur yang sama dengan Escape.
                        Check(!launcher.Started && Time.timeScale == 0f && root.Q("btn:keluar") != null, "Kembali Android membuka lobby Classic");
                        Click(root.Q("btn:keluar"));
                        deadline = Time.frameCount + 600;
                        Next(1);
                        break;
                    }
                    case 5:
                        // KotaPaket berat; tunggu sampai aktif (lobby 2.0 sendiri yang mengatur jeda waktu).
                        if (SceneManager.GetActiveScene().name != "KotaPaket" && Time.frameCount < deadline) return;
                        Check(SceneManager.GetActiveScene().name == "KotaPaket", "Keluar memuat lobby 2.0 (KotaPaket), aktif: " + SceneManager.GetActiveScene().name);
                        Finish(true, null);
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(false, error.Message);
            }
        }

        private static void Next(int frames)
        {
            step++;
            waitFrame = Time.frameCount + frames;
        }

        private static void Click(VisualElement element)
        {
            if (element == null) throw new InvalidOperationException("FAIL Classic: tombol tidak ditemukan");
            using (PointerUpEvent up = PointerUpEvent.GetPooled())
            {
                up.target = element;
                element.SendEvent(up);
            }
        }

        private static Label FindLabel(VisualElement root, string text) => root.Query<Label>().Where(label => label.text == text).First();

        private static void Finish(bool ok, string error)
        {
            SessionState.SetBool(Flag, false);
            ClassicLauncher.ScreenOverride = null;
            if (savedInputBehavior.HasValue) InputSystem.settings.editorInputBehaviorInPlayMode = savedInputBehavior.Value;
            savedInputBehavior = null;
            if (savedBackground.HasValue) InputSystem.settings.backgroundBehavior = savedBackground.Value;
            savedBackground = null;
            if (addedKeyboard != null) InputSystem.RemoveDevice(addedKeyboard);
            if (addedTouch != null) InputSystem.RemoveDevice(addedTouch);
            addedKeyboard = null;
            addedTouch = null;
            var report = new StringBuilder(ok ? $"PASS: {checks} pemeriksaan keluar dari Classic (desktop + Android landscape)." : "FAIL: " + error);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/classic-exit-checks.txt", report.ToString());
            if (ok) Debug.Log("CLASSIC_EXIT_CHECKS_OK: " + report);
            else Debug.LogError("CLASSIC_EXIT_CHECKS_FAIL: " + report);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            if (SessionState.GetBool(ExitFlag, false))
            {
                SessionState.SetBool(ExitFlag, false);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
