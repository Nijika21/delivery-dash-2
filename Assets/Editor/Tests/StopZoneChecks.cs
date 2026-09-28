using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor.Tests
{
    // HANDOFF 14 "Jeda berhenti": satu aturan untuk semua zona. Tes 1–7 dijalankan dengan dt tetap
    // 1/60, 1/30, dan 1/144 (tes 9), untuk kedua peta (tes 8). Dipanggil dari ValidatePlay (Play Mode).
    public static class StopZoneChecks
    {
        private static readonly float[] Steps = { 1f / 60f, 1f / 30f, 1f / 144f };
        private const string StopZoneGuid = "905a4fb31d115084d9e6c51ddc9f9b68";

        public static int Run(DeliveryGameManager game)
        {
            int checks = 0;
            void Check(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + description);
                checks++;
            }

            var fireTimes = new float[Steps.Length];
            for (int s = 0; s < Steps.Length; s++)
            {
                float dt = Steps[s];
                string fps = Mathf.RoundToInt(1f / dt) + " fps";
                var holder = new GameObject("Uji zona berhenti");
                try
                {
                    StopZone zone = holder.AddComponent<StopZone>();

                    // 1. Diam dari 0 → terpicu di frame pertama dengan progres ≥ 0,75 dtk, tidak lebih awal.
                    zone.Configure(0.75f);
                    int frame = 0;
                    while (!zone.AdvanceDwell(true, dt) && frame < 1000) frame++;
                    frame++;
                    Check(frame * dt >= 0.75f - 0.0001f && (frame - 1) * dt < 0.75f - 0.0001f, "Zona terpicu tepat di 0,75 dtk (" + fps + ")");
                    fireTimes[s] = frame * dt;

                    // 2. Kecepatan 0,31 di dalam zona tidak dihitung diam.
                    zone.Configure(0.75f);
                    for (int i = 0; i < Frames(0.5f, dt); i++) zone.AdvanceDwell(IsStill(true, 0.31f), dt);
                    Check(zone.Progress01 == 0f, "Kecepatan 0,31 tidak menaikkan progres (" + fps + ")");
                    Check(IsStill(true, -0.29f) && !IsStill(true, -0.31f) && !IsStill(false, 0f), "Aturan diam memakai |kecepatan| dan bentuk zona");

                    // 3. Diam 0,5, bergerak 0,1 → 0,25; diam lagi → terpicu setelah 0,5 tambahan.
                    zone.Configure(0.75f);
                    Advance(zone, true, 0.5f, dt);
                    Advance(zone, false, 0.1f, dt);
                    Check(Near(Seconds(zone), 0.25f, dt * 2.5f), "Bergerak 0,1 dtk menurunkan progres ke 0,25 (" + fps + ")");
                    int extra = 0;
                    while (!zone.AdvanceDwell(true, dt) && extra < 1000) extra++;
                    extra++;
                    Check(Mathf.Abs(extra * dt - 0.5f) <= dt * 3.5f, "Terpicu setelah 0,5 dtk tambahan (" + fps + ")");

                    // 4. Keluar zona di 0,5; 0,1 dtk di luar → 0,25; 0,2 dtk di luar → 0.
                    zone.Configure(0.75f);
                    Advance(zone, true, 0.5f, dt);
                    Advance(zone, IsStill(false, 0f), 0.1f, dt);
                    Check(Near(Seconds(zone), 0.25f, dt * 2.5f), "Keluar zona tidak mereset, hanya menurun (" + fps + ")");
                    Advance(zone, false, 0.1f, dt);
                    Check(Seconds(zone) == 0f, "0,2 dtk di luar zona kembali ke 0 (" + fps + ")");

                    // 7. Isi lingkar dan persen chip memakai nilai yang sama (selisih ≤ 1%).
                    zone.Configure(0.75f);
                    StopZoneView view = StopZoneView.Create(game.Town, "paket", game.Town.Depot + Vector2.up * 30f, 0f, zone);
                    try
                    {
                        for (int i = 0; i < Frames(0.75f, dt); i++)
                        {
                            zone.AdvanceDwell(true, dt);
                            view.SendMessage("LateUpdate");
                            Check(Mathf.Abs(view.Progress01 - zone.Progress01) <= 0.01f, "Isi lingkar = progres/T (" + fps + ")");
                            Check(Mathf.Abs(KidFriendlyHud.StopPercent(zone.Progress01) / 100f - zone.Progress01) <= 0.01f, "Persen chip = progres/T (" + fps + ")");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
                }
                finally { UnityEngine.Object.DestroyImmediate(holder); }

                checks += RunWithGame(game, dt, fps);
            }

            // 9. Hasil 30/60/144 fps sama, selisih maksimal satu frame (frame terpanjang = 1/30).
            Check(Mathf.Abs(fireTimes[0] - fireTimes[1]) <= Steps[1] + 0.0001f && Mathf.Abs(fireTimes[0] - fireTimes[2]) <= Steps[1] + 0.0001f,
                "Waktu terpicu sama di 30/60/144 fps");

            // 7b. Transparansi zona (kedip kapsul, muncul, hilang) lewat SpriteRenderer.color: di Unity 6 URP warna itu
            // hanya sampai ke shader lewat unity_SpriteColor (bug 26 Sep: kapsul tidak pernah terlihat berkedip).
            string zoneShader = System.IO.File.ReadAllText("Assets/Shaders/ZoneFill.shader");
            Check(zoneShader.Contains("unity_SpriteColor"), "Shader zona memakai warna SpriteRenderer (unity_SpriteColor)");

            // 7c. Cara muncul kapsul (koreksi pengguna 26 Sep malam): mulai main = pudar masuk tanpa kedip (langsung
            // bisa dipakai), misi selesai / tombol selesai kerja = kedip. Tidak ada lagi kedip "saat terlihat".
            {
                StopZoneView capsule = StopZoneView.Create(game.Town, "kapsul", game.Town.FinishCenter, game.Town.FinishAngle, null);
                try
                {
                    capsule.FadeIn(DeliveryGameManager.FinishFadeSeconds);
                    Check(!capsule.Blinking, "Kapsul pudar masuk tanpa kedip saat mulai main");
                    capsule.Blink(0f, DeliveryGameManager.FinishBlinkSeconds);
                    Check(capsule.Blinking, "Kapsul berkedip saat misi selesai / tombol selesai kerja");
                    capsule.FadeIn(DeliveryGameManager.FinishFadeSeconds);
                    Check(!capsule.Blinking, "Pudar masuk membatalkan kedip");
                }
                finally { UnityEngine.Object.DestroyImmediate(capsule.gameObject); }
                Check(!File.ReadAllText("Assets/Scripts/StopZoneView.cs").Contains("BlinkWhenSeen"), "Tidak ada kedip yang menunggu kapsul terlihat");
            }

            // 8. Kedua peta: pusat zona di jalan dan area selesai kerja membaca bentuk kapsul yang sama.
            DeliveryContentCatalog catalog = game.Town.Catalog;
            foreach (TextAsset layout in new[] { catalog.kotaPaketLayout, catalog.blokPaketLayout })
            {
                if (layout == null) continue;
                DeliveryTown town = game.Town;
                GameObject temporary = null;
                string wanted = layout == catalog.blokPaketLayout ? "blok-paket" : "kota-paket";
                if (town.MapId != wanted)
                {
                    temporary = new GameObject("Uji zona " + wanted);
                    town = temporary.AddComponent<DeliveryTown>();
                    town.Build(catalog, layout);
                }
                try
                {
                    Check(town.IsPaved(town.Depot) && StopZoneView.InsidePad(town.Depot, town.Depot, 0f, "paket"), "Zona ambil di jalan: " + wanted);
                    Check(town.IsInsideFinishParking(town.FinishCenter), "Kapsul selesai kerja berisi pusatnya: " + wanted);
                    Check(!town.IsInsideFinishParking(town.StartPosition), "Titik mulai di luar kapsul selesai kerja: " + wanted);
                    Check(town.IsPaved(town.LetterDropoff), "Zona surat di jalan: " + wanted);
                    // Zona ulang tutorial (abu-abu): di dalam plaza, tidak menimpa zona lain maupun truk di titik mulai.
                    Vector2 tutorialPad = DeliveryGameManager.TutorialPadCenterFor(town);
                    float half = DeliveryGameManager.TutorialPadSize * 0.5f;
                    var padRect = new Rect(tutorialPad - Vector2.one * half, Vector2.one * half * 2f);
                    Check(town.PlazaArea.Contains(padRect.min) && town.PlazaArea.Contains(padRect.max), "Zona tutorial di dalam plaza: " + wanted);
                    Check(town.IsPaved(tutorialPad), "Zona tutorial di lantai gudang: " + wanted);
                    Check(!padRect.Overlaps(new Rect(town.Depot - Vector2.one * StopZoneView.PadSize("paket") * 0.5f, Vector2.one * StopZoneView.PadSize("paket"))), "Zona tutorial tidak menimpa zona ambil: " + wanted);
                    Check(!padRect.Overlaps(new Rect(town.LetterDropoff - Vector2.one * StopZoneView.PadSize("surat") * 0.5f, Vector2.one * StopZoneView.PadSize("surat"))), "Zona tutorial tidak menimpa zona surat: " + wanted);
                    Check(!town.IsInsideFinishParking(padRect.min) && !town.IsInsideFinishParking(padRect.max) && !town.IsInsideFinishParking(tutorialPad), "Zona tutorial tidak menimpa kapsul: " + wanted);
                    Vector2 nearest = new Vector2(Mathf.Clamp(town.StartPosition.x, padRect.xMin, padRect.xMax), Mathf.Clamp(town.StartPosition.y, padRect.yMin, padRect.yMax));
                    Check(Vector2.Distance(nearest, town.StartPosition) >= 1.2f, "Truk di titik mulai tidak menyentuh zona tutorial: " + wanted);
                    for (int i = 0; i < town.Houses.Count; i++)
                        Check(town.IsPaved(town.Houses[i].Stop), "Zona rumah di jalan: " + wanted + " " + town.Houses[i].Name);
                }
                finally { if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
            }

            // 8. Classic tidak punya zona berhenti.
            string classicScene = "Assets/Classic/Classic.unity";
            Check(!File.Exists(classicScene) || !File.ReadAllText(classicScene).Contains(StopZoneGuid), "Scene Classic tanpa komponen StopZone");
            foreach (string script in Directory.GetFiles("Assets/Classic", "*.cs"))
                Check(!File.ReadAllText(script).Contains("StopZone"), "Skrip Classic tidak memakai StopZone: " + Path.GetFileName(script));
            // Loop tutorial lengkap (hlm. 96–112): paket, pelanggan, lalu pesan sukses "Package Delivered" berfont Bangers.
            Check(!File.Exists(classicScene) || File.ReadAllText(classicScene).Contains("m_TagString: Customer"), "Scene Classic punya rumah pelanggan (tag Customer)");
            Check(File.ReadAllText("Assets/Classic/ClassicCollision.cs").Contains("PACKAGE DELIVERED")
                && Resources.Load<Font>("Classic/Bangers-Regular") != null, "Classic menampilkan pesan sukses tutorial (Bangers)");
            return checks;
        }

        // Tes 1, 5, 6 lewat alur permainan sungguhan (GameManager), tanpa mengubah dompet pemain.
        private static int RunWithGame(DeliveryGameManager game, float dt, string fps)
        {
            int checks = 0;
            void Check(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException("FAIL: " + description);
                checks++;
            }
            Driver car = game.Car;
            DeliveryTown town = game.Town;
            var hud = game.GetComponent<KidFriendlyHud>();
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
            hud.ResumeGame();

            // 1 + 6. Diam di zona ambil: terpicu tepat sekali, tetap diam 3 dtk tidak mengambil lagi.
            car.PlaceAt(town.Depot);
            int pickups = 0, frames = 0, fireFrame = -1;
            bool wasCarrying = game.Carrying;
            for (; frames < Frames(0.75f + 3f, dt); frames++)
            {
                game.AdvanceActiveDwell(dt);
                if (game.Carrying && !wasCarrying) { pickups++; if (fireFrame < 0) fireFrame = frames + 1; }
                wasCarrying = game.Carrying;
            }
            Check(pickups == 1 && game.Deliveries == 0, "Tetap diam 3 dtk sesudah ambil: ambil tetap 1 (" + fps + ")");
            Check(fireFrame * dt >= 0.75f - 0.0001f && (fireFrame - 1) * dt < 0.75f - 0.0001f, "Ambil paket terpicu di 0,75 dtk (" + fps + ")");

            // 5. Jeda di progres 0,5 dtk → tetap 0,5 (Update berhenti saat timeScale 0).
            StopZone active = game.ActiveMarker != null ? game.ActiveMarker.GetComponent<StopZone>() : null;
            Check(active != null && active.Progress01 == 0f, "Zona tujuan baru mulai dari 0 (" + fps + ")");
            car.PlaceAt(game.TargetPosition);
            game.AdvanceActiveStop(0.5f);
            float before = active.Progress01;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                for (int i = 0; i < Frames(5f, dt); i++) game.SendMessage("Update");
            }
            finally { Time.timeScale = previousScale; }
            Check(Mathf.Approximately(active.Progress01, before), "Jeda membekukan progres (" + fps + ")");
            game.Minimap?.SetExpanded(true);
            Time.timeScale = 0f;
            try { for (int i = 0; i < Frames(1f, dt); i++) game.SendMessage("Update"); }
            finally { Time.timeScale = previousScale; game.Minimap?.SetExpanded(false); }
            Check(Mathf.Approximately(active.Progress01, before), "Peta besar membekukan progres (" + fps + ")");

            // 6. Area selesai kerja: dialog terbuka sekali sampai truk keluar area.
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
            hud.ResumeGame();
            car.PlaceAt(town.FinishCenter);
            int opened = 0;
            bool wasOpen = false;
            for (int i = 0; i < Frames(3.75f, dt); i++)
            {
                game.AdvanceParking(dt);
                if (game.MenuOpen && !wasOpen) opened++;
                wasOpen = game.MenuOpen;
                if (game.MenuOpen) { hud.ResumeGame(); wasOpen = false; }
            }
            Check(opened == 1, "Dialog selesai kerja terbuka sekali selama truk diam di area (" + fps + ")");
            car.PlaceAt(town.StartPosition);
            game.AdvanceParking(dt);
            car.PlaceAt(town.FinishCenter);
            for (int i = 0; i < Frames(0.75f, dt) + 1 && !game.MenuOpen; i++) game.AdvanceParking(dt);
            Check(game.MenuOpen, "Keluar area membuka kunci dialog selesai kerja (" + fps + ")");
            hud.ResumeGame();
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
            return checks;
        }

        public static bool IsStill(bool inside, float speed) => inside && Mathf.Abs(speed) < 0.3f;
        private static int Frames(float seconds, float dt) => Mathf.CeilToInt(seconds / dt - 0.0001f);
        private static float Seconds(StopZone zone) => zone.Progress01 * 0.75f;
        private static bool Near(float value, float expected, float tolerance) => Mathf.Abs(value - expected) <= tolerance;

        private static void Advance(StopZone zone, bool still, float seconds, float dt)
        {
            for (int i = 0; i < Frames(seconds, dt); i++) zone.AdvanceDwell(still, dt);
        }
    }
}
