using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDash.Editor.Tests
{
    // Bot uji main: masuk Play Mode dan memainkan beberapa pengantaran di Kota Paket lalu Blok Paket.
    // Truk dipindah per bingkai menyusuri Town.TraceRoute (kecepatan 3,5–6 unit/dtk waktu game) dan berhenti di zona
    // sampai jeda penuh, jadi bintang di rute, kontrak (ekspres/rapuh/singgah), dan misi sampingan berjalan dengan aturan
    // game sungguhan. Batasan: teleport tidak menabrak apa pun, jadi kontrak rapuh selalu menang.
    // Hasil: Logs/playtest-bot.txt (tabel per pengantaran) dan log PLAYTEST_BOT_OK / PLAYTEST_BOT_ANOMALI.
    // "Unity.exe -batchmode -projectPath . -executeMethod DeliveryDash.Editor.Tests.PlaytestBot.RunBatch" (tanpa -quit)
    // Courier.* dicadangkan sebelum Play (CourierPrefsBackup, juga bila Play terhenti) dan dipulihkan sesudahnya;
    // IsValidationRunning mencegah koin tersimpan.
    public static class PlaytestBot
    {
        private const string Flag = "DeliveryDash.PlaytestBot";
        private const string ExitFlag = "DeliveryDash.PlaytestBotExit";
        private const string BackupKey = "DeliveryDash.PlaytestBotBackup";
        private const string MapKey = "DeliveryDash.PlaytestBotMap";
        private const string ReportKey = "DeliveryDash.PlaytestBotReport";
        private const string AnomalyKey = "DeliveryDash.PlaytestBotAnomaly";
        private const string ReportPath = "Logs/playtest-bot.txt";
        private static readonly string[] Maps = { "kota-paket", "blok-paket" };
        private const string NextMapKey = "DeliveryDash.PlaytestBotNextMap";
        private const int TripsPerMap = 10;
        private const float GameSpeed = 3f; // timeScale: waktu game 3× lebih cepat, aturan tetap sama
        // Batas aturan (HANDOFF): +12 antar, +5 kontrak, +1 per bintang (≤3), +2 semua bintang; surat +8, istirahat +4.
        private const int DeliveryCap = 12 + 5 + 3 + 2;

        // Tumpukan coroutine buatan sendiri: yield return IEnumerator menjalankan anak itu dulu (seperti coroutine Unity).
        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static FieldInfo latch;
        private static bool leaving; // sesi Play ini sedang ditutup untuk peta berikutnya
        private static int lastFrame = -1;
        private static readonly System.Random dice = new System.Random(20260925);

        [MenuItem("Delivery Dash/Uji/Bot Uji Main")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
            if (!CourierPrefsBackup.OpenScene("Assets/Scenes/KotaPaket.unity"))
            {
                Debug.Log("PLAYTEST_BOT_ANOMALI: dibatalkan (scene belum disimpan)");
                if (SessionState.GetBool(ExitFlag, false)) { SessionState.SetBool(ExitFlag, false); EditorApplication.Exit(1); }
                return;
            }
            // Cadangan tertunda (sesi bot/tangkapan yang terhenti) dipulihkan dulu, tidak dicadangkan ulang.
            CourierPrefsBackup.Begin(BackupKey);
            SessionState.SetInt(MapKey, 0);
            SessionState.SetString(ReportKey, string.Empty);
            SessionState.SetBool(AnomalyKey, false);
            SetLastMode(Maps[0]);
            SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(ExitFlag, true);
            Run();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Flag, false)) return;
                bool nextMap = SessionState.GetBool(NextMapKey, false);
                // Play dihentikan / dikompilasi ulang di tengah uji: pulihkan Courier.* dan hentikan bot.
                if (state == PlayModeStateChange.ExitingPlayMode && !nextMap)
                {
                    SessionState.SetBool(Flag, false);
                    DeliveryGameManager.IsValidationRunning = false;
                    CourierPrefsBackup.End(BackupKey);
                    Debug.LogWarning("PLAYTEST_BOT: Play berhenti sebelum selesai; Courier.* dipulihkan.");
                }
                // Peta berikutnya dijalankan di sesi Play baru bila pindah scene di dalam Play gagal.
                if (state == PlayModeStateChange.EnteredEditMode && nextMap)
                {
                    SessionState.SetBool(NextMapKey, false);
                    EditorApplication.EnterPlaymode();
                }
            };
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || leaving) return;
            if (Time.frameCount == lastFrame) return; // satu langkah per bingkai game
            lastFrame = Time.frameCount;
            try
            {
                if (stack.Count == 0)
                {
                    DeliveryGameManager game = DeliveryGameManager.Instance;
                    if (game?.Town == null || game.Activities == null || game.SideQuests == null || game.Minimap == null) return;
                    latch = typeof(DeliveryGameManager).GetField("parkingPromptLatched", BindingFlags.Instance | BindingFlags.NonPublic);
                    stack.Push(Play(SessionState.GetInt(MapKey, 0)));
                }
                while (stack.Count > 0)
                {
                    IEnumerator top = stack.Peek();
                    bool more = top.MoveNext();
                    if (stack.Count == 0) break; // Finish() di dalam langkah ini
                    if (more)
                    {
                        if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                        break;
                    }
                    stack.Pop();
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Note("GAGAL: " + error.Message, true);
                Finish();
            }
        }

        private sealed class Trip
        {
            public string Map, Contract;
            public int Coins, Stars, StarGoal, Bonus, SideCoins, Letters, Rests;
            public float Seconds;
            public bool Won;
        }

        private static IEnumerator Play(int mapIndex)
        {
            for (int m = mapIndex; m < Maps.Length; m++)
            {
                DeliveryGameManager game = DeliveryGameManager.Instance;
                if (game.Town.MapId != Maps[m]) Note($"ANOMALI: peta dimuat {game.Town.MapId}, diminta {Maps[m]}", true);
                yield return PlayMap(game, Maps[m]);
                if (m + 1 >= Maps.Length) break;

                // Jalur ganti peta yang sama dengan DeliveryGameManager.SelectMode: simpan LastMode lalu muat ulang scene.
                SetLastMode(Maps[m + 1]);
                DeliveryGameManager.IsValidationRunning = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                float until = Time.realtimeSinceStartup + 6f;
                DeliveryGameManager next = null;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    next = DeliveryGameManager.Instance;
                    if (next != null && next != game && next.Town != null && next.SideQuests != null && next.Minimap != null) break;
                    next = null;
                }
                if (next == null)
                {
                    Note("ANOMALI: setelah SceneManager.LoadScene (ganti peta) tidak ada DeliveryGameManager baru; peta berikutnya dijalankan di sesi Play baru.", true);
                    SessionState.SetInt(MapKey, m + 1);
                    leaving = true;
                    SessionState.SetBool(NextMapKey, true);
                    EditorApplication.ExitPlaymode();
                    yield break;
                }
                Note($"Ganti peta lewat muat ulang scene: OK ({next.Town.MapId}).", false);
                yield return null;
            }
            Finish();
        }

        private static IEnumerator PlayMap(DeliveryGameManager game, string map)
        {
            DeliveryGameManager.IsValidationRunning = true;
            var hud = game.GetComponent<KidFriendlyHud>();
            hud.ResumeGame();
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
            int walletBefore = game.Progress.Coins;
            var trips = new List<Trip>();
            var seenDone = new HashSet<DeliverySideQuests.Quest>();

            for (int t = 0; t < TripsPerMap; t++)
            {
                var trip = new Trip { Map = map };
                int startCoins = game.Stars;
                float startTime = Time.time;
                float speed = 3.5f + (float)dice.NextDouble() * 2.5f;
                int letters = 0, rests = 0;
                Func<bool> countQuests = () =>
                {
                    foreach (var quest in game.SideQuests.Quests)
                        if (quest.Completed && seenDone.Add(quest)) { if (quest.Collected) letters++; else rests++; }
                    return false;
                };

                // 1. Misi sampingan yang ditawarkan diambil dulu (seperti anak yang mau koin tambahan).
                foreach (var quest in new List<DeliverySideQuests.Quest>(game.SideQuests.Quests))
                {
                    if (quest.Completed || quest.Collected) continue;
                    yield return DriveTo(game, quest.Position, speed, countQuests);
                    yield return Park(game, () => quest.Completed || quest.Collected, 5f, countQuests);
                    if (quest.Collected && !quest.Completed)
                    {
                        yield return DriveTo(game, quest.Position, speed, countQuests); // posisi = kotak surat
                        yield return Park(game, () => quest.Completed, 5f, countQuests);
                    }
                }
                countQuests();
                int sideCoins = game.Stars - startCoins;

                // 2. Ambil paket di gudang.
                yield return DriveTo(game, game.TargetPosition, speed, countQuests);
                yield return Park(game, () => game.Carrying, 5f, countQuests);
                if (!game.Carrying) { Note($"ANOMALI {map} #{t + 1}: paket tidak terambil di gudang.", true); break; }
                while (game.IsShowingDestination) yield return null;
                var activities = game.Activities;
                trip.Contract = game.TutorialTrip ? "tutorial" : activities.IsExpress ? "ekspres" : activities.IsFragile ? "rapuh"
                    : activities.HasStopover ? "singgah" : "biasa";
                trip.StarGoal = activities.StarGoal;

                // 3. Singgah (bila ada), lalu antar ke rumah. Bintang di rute terkumpul oleh DeliveryActivities.Update.
                int stars = 0;
                Func<bool> watch = () => { countQuests(); stars = Mathf.Max(stars, game.Carrying ? activities.StarsCollected : stars); return false; };
                if (activities.HasStopover)
                {
                    Vector2 stop = activities.BonusPosition;
                    yield return DriveTo(game, stop, speed, watch);
                    yield return Park(game, () => !activities.HasStopover, 5f, watch);
                }
                yield return DriveTo(game, game.TargetPosition, speed, watch);
                yield return Park(game, () => !game.Carrying, 5f, watch);
                if (game.Carrying) { Note($"ANOMALI {map} #{t + 1}: paket tidak terantar.", true); break; }
                countQuests();

                trip.Coins = game.Stars - startCoins;
                trip.Stars = stars;
                trip.Won = activities.LastContractWon;
                trip.Bonus = trip.Won ? 5 : 0;
                trip.SideCoins = sideCoins;
                trip.Letters = letters;
                trip.Rests = rests;
                trip.Seconds = Time.time - startTime;
                trips.Add(trip);

                int expected = 12 + trip.Bonus + trip.Stars + (trip.StarGoal > 0 && trip.Stars == trip.StarGoal ? 2 : 0);
                int delivery = trip.Coins - trip.SideCoins;
                string where = $"{map} #{t + 1}";
                if (delivery != expected) Note($"ANOMALI {where}: koin antar {delivery}, seharusnya {expected}.", true);
                if (delivery > DeliveryCap) Note($"ANOMALI {where}: koin antar {delivery} > batas {DeliveryCap}.", true);
                if (trip.SideCoins != 8 * letters + 4 * rests) Note($"ANOMALI {where}: koin misi sampingan {trip.SideCoins} ≠ 8×{letters} surat + 4×{rests} istirahat.", true);
                if (trip.Bonus > 0 && (trip.Contract == "biasa" || trip.Contract == "tutorial")) Note($"ANOMALI {where}: bonus kontrak tanpa kontrak.", true);
                if (trip.StarGoal > 3 || trip.Stars > trip.StarGoal) Note($"ANOMALI {where}: bintang {trip.Stars}/{trip.StarGoal}.", true);
                if (trip.Coins > DeliveryCap + 12) Note($"ANOMALI {where}: {trip.Coins} koin dalam satu pengantaran (> ~34).", true);
            }

            var table = new StringBuilder();
            table.AppendLine($"Peta {map} ({trips.Count} pengantaran, kecepatan bot 3,5–6 unit/dtk):");
            table.AppendLine("  #  kontrak   menang  bintang  bonus  sampingan(surat/istirahat)  koin  waktu-game");
            int total = 0;
            for (int i = 0; i < trips.Count; i++)
            {
                Trip trip = trips[i];
                total += trip.Coins;
                table.AppendLine($"  {i + 1,2} {trip.Contract,-9} {(trip.Contract == "biasa" || trip.Contract == "tutorial" ? "-" : trip.Won ? "ya" : "tidak"),-7} " +
                    $"{trip.Stars}/{trip.StarGoal,-6} +{trip.Bonus,-5} +{trip.SideCoins} ({trip.Letters}/{trip.Rests}){"",-17} {trip.Coins,4}  {trip.Seconds,6:0.0} dtk");
            }
            table.AppendLine($"  Koin sesi (DeliveryGameManager.Stars) = {game.Stars}, jumlah tabel = {total}.");
            if (game.Stars != total) Note($"ANOMALI {map}: koin sesi {game.Stars} ≠ jumlah per pengantaran {total}.", true);
            if (game.Progress.Coins != walletBefore) Note($"ANOMALI {map}: dompet pemain berubah selama uji ({walletBefore} → {game.Progress.Coins}).", true);
            Note(table.ToString(), false);
            DeliveryGameManager.IsValidationRunning = false;
        }

        private static IEnumerator DriveTo(DeliveryGameManager game, Vector2 target, float speed, Func<bool> each)
        {
            var route = new List<Vector2>();
            game.Town.TraceRoute(game.Car.transform.position, target, route);
            if (route.Count == 0) route.Add(target);
            // TraceRoute memindah tujuan peta kecil; kembalikan ke tujuan permainan (singgah/misi bukan tujuan utama).
            if ((target - game.TargetPosition).sqrMagnitude > 0.01f) game.Town.SetDestination(game.TargetPosition);
            Vector2 at = game.Car.transform.position;
            int next = 0;
            float giveUp = Time.time + 240f;
            while (next < route.Count && Time.time < giveUp)
            {
                KeepPlaying(game);
                float step = speed * Time.deltaTime;
                while (step > 0f && next < route.Count)
                {
                    Vector2 to = route[next];
                    float left = Vector2.Distance(at, to);
                    if (left <= step) { at = to; step -= left; next++; }
                    else { Vector2 dir = (to - at) / left; at += dir * step; step = 0f; }
                }
                Vector2 heading = next < route.Count ? route[next] - at : target - (Vector2)game.Car.transform.position;
                float angle = heading.sqrMagnitude > 1e-4f ? Vector2.SignedAngle(Vector2.up, heading) : game.Car.transform.eulerAngles.z;
                game.Car.PlaceAt(at, angle);
                each?.Invoke();
                yield return null;
            }
        }

        private static IEnumerator Park(DeliveryGameManager game, Func<bool> done, float seconds, Func<bool> each)
        {
            float until = Time.time + seconds;
            while (!done() && Time.time < until)
            {
                KeepPlaying(game);
                each?.Invoke();
                yield return null;
            }
        }

        private static void KeepPlaying(DeliveryGameManager game)
        {
            var hud = game.GetComponent<KidFriendlyHud>();
            if (hud.IsMenuOpen) hud.ResumeGame();
            if (!game.IsShowingDestination && !game.MenuOpen) Time.timeScale = GameSpeed;
            // Truk bot berhenti (PlaceAt) di area parkir selesai kerja bukan berarti ingin selesai kerja.
            latch?.SetValue(game, true);
        }

        private static void Note(string text, bool anomaly)
        {
            Debug.Log("PLAYTEST_BOT " + text);
            SessionState.SetString(ReportKey, SessionState.GetString(ReportKey, string.Empty) + text + "\n");
            if (anomaly) SessionState.SetBool(AnomalyKey, true);
        }

        private static void Finish()
        {
            stack.Clear();
            SessionState.SetBool(Flag, false);
            DeliveryGameManager.IsValidationRunning = false;
            Time.timeScale = 1f;
            CourierPrefsBackup.End(BackupKey);
            bool anomaly = SessionState.GetBool(AnomalyKey, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, SessionState.GetString(ReportKey, string.Empty));
            Debug.Log(anomaly ? "PLAYTEST_BOT_ANOMALI: lihat " + ReportPath : "PLAYTEST_BOT_OK: " + ReportPath);
            if (SessionState.GetBool(ExitFlag, false))
            {
                SessionState.SetBool(ExitFlag, false);
                EditorApplication.Exit(anomaly ? 1 : 0);
            }
            else if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        // Hanya LastMode yang diganti (sementara; dipulihkan dari cadangan di Finish).
        private static void SetLastMode(string mode)
        {
            CourierProgress progress = CourierProgress.Load();
            progress.LastMode = mode;
            PlayerPrefs.SetString("Courier.Progress", JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }
    }
}
