using System.Collections.Generic;
using UnityEngine;

// Tutorial interaktif (uji HP 27 Sep, menggantikan tutorial pesan-saja 26 Sep) dan zona abu-abu untuk mengulangnya.
// Setiap langkah punya target yang disorot HUD (KidFriendlyHud.Guide: lampu sorot + tangan + kartu) dan baru lanjut
// sesudah pemain benar-benar melakukannya:
//   Drive → ToDepot → StopAtDepot → (bawa paket) FollowRoute ⇄ Star / Boost / Stopover → StopAtHouse
//   → HelpZone → Mailbox → ToFinish → Done (tutorial pertama: langsung ke lobby)
//                        ↘ FinishInfo (ulang lewat kotak abu-abu: kapsul hanya dikenalkan, lalu permainan lanjut).
// Pemain baru langsung masuk tutorial sesudah layar muat, tanpa lobby (Start). Ekspres, rapuh, pelindung, dan
// istirahat dikenalkan dengan sorotan singkat saat pertama kali muncul di permainan biasa (GuideTip, HUD).
public partial class DeliveryGameManager
{
    public enum GuideStep
    {
        None, Drive, ToDepot, StopAtDepot, FollowRoute, Star, Boost, Stopover, StopAtHouse, HelpZone, Mailbox, ToFinish,
        FinishInfo, Done
    }

    [System.Flags]
    public enum GuideTip { None = 0, Star = 1, Boost = 2, Stopover = 4, Letter = 8, Rest = 16, Express = 32, Fragile = 64, Shield = 128 }

    // Ukuran zona ulang tutorial (lebih kecil dari zona lain: 2,6 unit) dan letaknya dari pojok kanan bawah plaza gudang,
    // di kanan titik mulai truk: satu-satunya sudut plaza yang tidak dilewati jalur jalan masuk → ambil paket → kapsul.
    public const float TutorialPadSize = 2.6f;
    private static readonly Vector2 TutorialPadInset = new Vector2(2.4f, 1.9f);
    // Jarak dekat/jauh (histeresis) untuk pindah ke langkah berhenti atau sorotan bintang/kilat.
    private const float TutorialHouseNear = 8f, TutorialHouseFar = 11f;
    private const float GuideStopNear = 7f, GuideStopFar = 10f;
    private const float GuidePickupNear = 7f, GuidePickupFar = 10f;
    // Langkah pertama selesai sesudah truk benar-benar menempuh jarak ini (bukan sekadar menyentuh joystick).
    private const float GuideDriveDistance = 2.5f;
    public const float GuideFinishInfoSeconds = 4.5f, GuideDoneSeconds = 2.4f;

    private bool forceTutorial;
    private StopZoneView tutorialZone;
    private float tutorialDwell;
    private bool tutorialPromptLatched;
    private float guideTravel;
    private Vector2 guideLastPosition;
    private float guideStepStarted;
    private DeliveryMarker guidePickup;
    private readonly HashSet<DeliveryMarker> skippedPickups = new HashSet<DeliveryMarker>();
    private DeliverySideQuests.Quest guideQuest;
    private readonly List<Vector2> guideRoute = new List<Vector2>();

    public GuideStep Guide { get; private set; }
    // true = tutorial pemain baru (atau sesudah hapus progres): berakhir di kapsul lalu ke lobby.
    public bool FirstTutorial { get; private set; }
    public float GuideStepAge => Time.unscaledTime - guideStepStarted;
    // Naik setiap kali sorotan harus diputar ulang: langkah berganti, atau bintang/kilat yang disorot berganti.
    public int GuideVersion { get; private set; }
    public bool GuideActive => Guide != GuideStep.None;
    // Selama tutorial belum sampai ke kapsul, kapsul disembunyikan supaya tidak mengalihkan perhatian.
    private bool GuideHidesFinish => Guide != GuideStep.None && Guide != GuideStep.ToFinish && Guide != GuideStep.FinishInfo;
    // Sesudah paket tutorial diserahkan, paket berikutnya di gudang ditahan sampai tutorial selesai.
    public bool GuideHoldsParcel => Guide == GuideStep.HelpZone || Guide == GuideStep.Mailbox || Guide == GuideStep.ToFinish ||
        Guide == GuideStep.FinishInfo || Guide == GuideStep.Done;

    public Vector2 TutorialPadCenter => TutorialPadCenterFor(Town);

    public static Vector2 TutorialPadCenterFor(DeliveryTown town)
    {
        Rect plaza = town.PlazaArea;
        return new Vector2(plaza.xMax - TutorialPadInset.x, plaza.yMin + TutorialPadInset.y);
    }

    public float TutorialPadProgress01 => tutorialDwell / StopSeconds;
    public bool TutorialPadShown => tutorialZone != null;

    // Titik dunia yang disorot langkah ini (null = sorotan UI: joystick/tombol atau peta kecil/panah, diatur HUD).
    public Vector2? GuideWorldTarget
    {
        get
        {
            switch (Guide)
            {
                case GuideStep.ToDepot:
                case GuideStep.StopAtDepot:
                case GuideStep.StopAtHouse:
                    return ActiveMarker != null ? (Vector2)ActiveMarker.transform.position : (Vector2?)null;
                case GuideStep.Star:
                case GuideStep.Boost:
                    return guidePickup != null ? (Vector2)guidePickup.transform.position : (Vector2?)null;
                case GuideStep.Stopover:
                    return Activities != null && Activities.HasStopover ? Activities.BonusPosition : (Vector2?)null;
                case GuideStep.HelpZone:
                    return guideQuest != null ? guideQuest.Position : (Vector2?)null;
                case GuideStep.Mailbox:
                    return SideQuests.DropoffPosition;
                case GuideStep.ToFinish:
                case GuideStep.FinishInfo:
                case GuideStep.Done:
                    return Town != null ? Town.FinishCenter : (Vector2?)null;
                default:
                    return null;
            }
        }
    }

    // Jari-jari lubang sorotan dalam unit dunia (sebesar zona/pickup yang disorot).
    public float GuideWorldRadius
    {
        get
        {
            switch (Guide)
            {
                case GuideStep.ToDepot:
                case GuideStep.StopAtDepot: return 2.9f;
                case GuideStep.Star:
                case GuideStep.Boost: return 1.4f;
                case GuideStep.ToFinish:
                case GuideStep.FinishInfo:
                case GuideStep.Done: return Mathf.Max(2.2f, Town.FinishSize.magnitude * 0.45f);
                default: return 2.2f;
            }
        }
    }

    // Tujuan panah dan jarak di bar selama langkah sesudah paket tutorial (bantuan warga, kotak surat, kapsul).
    private Vector2? GuideNavigation
    {
        get
        {
            switch (Guide)
            {
                case GuideStep.HelpZone: return guideQuest != null ? guideQuest.Position : (Vector2?)null;
                case GuideStep.Mailbox: return SideQuests.DropoffPosition;
                case GuideStep.FinishInfo: return Town.FinishCenter;
                default: return null;
            }
        }
    }

    // Panah dunia (dipakai langkah ikuti rute saat peta kecil disembunyikan).
    public bool ArrowShown => arrow != null && arrow.enabled;
    public Vector2 ArrowPosition => arrow != null ? (Vector2)arrow.transform.position : Vector2.zero;

    public bool TipSeen(GuideTip tip) => Progress != null && (Progress.SeenTips & (int)tip) != 0;

    public void MarkTip(GuideTip tip)
    {
        if (Progress == null || TipSeen(tip)) return;
        Progress.SeenTips |= (int)tip;
        Progress.Save();
    }

    private void SetGuide(GuideStep step, bool replay = false)
    {
        if (Guide == step && !replay) return;
        Guide = step;
        guideStepStarted = Time.unscaledTime;
        GuideVersion++;
        switch (step)
        {
            case GuideStep.Drive:
                guideTravel = 0f;
                guideLastPosition = Car != null ? (Vector2)Car.transform.position : Vector2.zero;
                break;
            // Yang sudah diajarkan tutorial tidak dikenalkan lagi oleh petunjuk sekali pakai.
            case GuideStep.Star: MarkTip(GuideTip.Star); break;
            case GuideStep.Boost: MarkTip(GuideTip.Boost); break;
            case GuideStep.Stopover: MarkTip(GuideTip.Stopover); break;
            case GuideStep.HelpZone: MarkTip(GuideTip.Letter); break;
        }
    }

    private void BeginGuide(bool first)
    {
        FirstTutorial = first;
        guidePickup = null;
        guideQuest = null;
        skippedPickups.Clear();
        SetGuide(GuideStep.Drive, true);
    }

    private void EndGuide()
    {
        FirstTutorial = false;
        guidePickup = null;
        guideQuest = null;
        skippedPickups.Clear();
        SetGuide(GuideStep.None);
    }

    // Dipanggil tiap frame permainan (Update), sesudah zona dan parkir.
    private void UpdateTutorialGuide()
    {
        if (Guide == GuideStep.None || Guide == GuideStep.Done) return;
        Vector2 truck = Car.transform.position;
        if (TutorialTrip && Carrying) { UpdateTripGuide(truck); return; }
        switch (Guide)
        {
            case GuideStep.Drive:
                guideTravel += Vector2.Distance(truck, guideLastPosition);
                guideLastPosition = truck;
                if (IsInsideActiveZone(truck)) SetGuide(GuideStep.StopAtDepot);
                else if (guideTravel >= GuideDriveDistance) SetGuide(GuideStep.ToDepot);
                break;
            case GuideStep.ToDepot:
                if (IsInsideActiveZone(truck)) SetGuide(GuideStep.StopAtDepot);
                break;
            case GuideStep.StopAtDepot:
                if (!IsInsideActiveZone(truck)) SetGuide(GuideStep.ToDepot);
                break;
            case GuideStep.HelpZone:
                if (SideQuests.CarriedLetters > 0) SetGuide(GuideStep.Mailbox);
                else if (guideQuest == null || guideQuest.Completed || !SideQuests.Quests.Contains(guideQuest)) AfterSideQuest();
                break;
            case GuideStep.Mailbox:
                if (SideQuests.CarriedLetters == 0) AfterSideQuest();
                break;
            case GuideStep.FinishInfo:
                if (GuideStepAge >= GuideFinishInfoSeconds) EndGuide();
                break;
        }
    }

    // Selama paket tutorial di truk: bintang/kilat terdekat di depan disorot lebih dulu, lalu kotak singgah, lalu rumah.
    private void UpdateTripGuide(Vector2 truck)
    {
        DeliveryMarker pickup = NearestGuidePickup(truck);
        if (pickup != null)
        {
            bool changed = pickup != guidePickup;
            guidePickup = pickup;
            SetGuide(pickup.Kind == DeliveryMarkerKind.Star ? GuideStep.Star : GuideStep.Boost, changed);
            return;
        }
        guidePickup = null;
        if (Activities.HasStopover)
        {
            float stop = Vector2.Distance(truck, Activities.BonusPosition);
            bool near = stop < (Guide == GuideStep.Stopover ? GuideStopFar : GuideStopNear);
            SetGuide(near ? GuideStep.Stopover : GuideStep.FollowRoute);
            return;
        }
        float house = Vector2.Distance(truck, TargetPosition);
        bool atHouse = house < (Guide == GuideStep.StopAtHouse ? TutorialHouseFar : TutorialHouseNear);
        SetGuide(atHouse ? GuideStep.StopAtHouse : GuideStep.FollowRoute);
    }

    // Bintang/kilat tutorial dalam jangkauan. Yang sudah disorot lalu dilewati tanpa diambil tidak disorot lagi.
    private DeliveryMarker NearestGuidePickup(Vector2 truck)
    {
        DeliveryMarker best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (DeliveryMarker pickup in Activities.Pickups)
        {
            if (pickup == null || skippedPickups.Contains(pickup)) continue;
            if (pickup.Kind != DeliveryMarkerKind.Star && pickup.Kind != DeliveryMarkerKind.Boost) continue;
            float distance = Vector2.Distance(truck, pickup.transform.position);
            if (distance > (pickup == guidePickup ? GuidePickupFar : GuidePickupNear))
            {
                if (pickup == guidePickup) skippedPickups.Add(pickup);
                continue;
            }
            if (distance < bestDistance) { bestDistance = distance; best = pickup; }
        }
        return best;
    }

    // Paket tutorial sampai: warga minta tolong antar surat di rute pulang (misi sampingan diajarkan sungguhan).
    private void OnTutorialDelivered(Vector2 house)
    {
        guidePickup = null;
        Town.TraceRoute(house, Town.Depot, guideRoute);
        guideQuest = SideQuests.OfferNearRoute(DeliverySideQuests.Kind.Letter, guideRoute);
        if (guideQuest != null) SetGuide(GuideStep.HelpZone);
        else AfterSideQuest();
    }

    // Tutorial pertama: pulang ke kapsul (FinishRequested mengarahkan panah dan sorotan kamera ke kapsul).
    // Ulang tutorial: kapsul hanya dikenalkan, permainan lanjut.
    private void AfterSideQuest()
    {
        guideQuest = null;
        if (FirstTutorial)
        {
            FinishRequested = true;
            SetGuide(GuideStep.ToFinish);
        }
        else SetGuide(GuideStep.FinishInfo);
    }

    // Truk berhenti penuh di kapsul pada tutorial pertama: pesan selesai sebentar, lalu lobby (bukan rangkuman).
    private void CompleteFirstTutorial()
    {
        SetGuide(GuideStep.Done);
        Car.StopImmediately();
        if (finishZone != null) { finishZone.Complete(); finishZone = null; }
        DeliveryAudio.Play(DeliveryAudio.Cue.Delivery);
        StartCoroutine(Confetti(Town.FinishCenter));
        if (IsValidationRunning) FinishTutorialToLobby();
        else StartCoroutine(FinishTutorialLater());
    }

    private System.Collections.IEnumerator FinishTutorialLater()
    {
        yield return new WaitForSecondsRealtime(GuideDoneSeconds);
        if (Guide == GuideStep.Done) FinishTutorialToLobby();
    }

    private void FinishTutorialToLobby()
    {
        EndGuide();
        FinishRequested = false;
        if (!IsValidationRunning) GetComponent<KidFriendlyHud>().OpenLobby();
        // Sesi baru dari titik mulai saat lobby menutupi layar (tanpa teleport di tengah permainan).
        ResetRun();
        Car.PlaceAt(Town.StartPosition, Town.StartHeading);
    }

    // ---------- Zona ulang tutorial (abu-abu) ----------

    // Tampil hanya saat tidak ada kiriman tertunda dan tidak sedang tutorial / pulang / misi khusus.
    private void UpdateTutorialZone()
    {
        bool show = !lobbyView && !FinishBlocked && !FinishRequested && !TutorialActive && !GuideActive && !SpecialTarget.HasValue &&
            Town != null && Town.PlazaArea.width > 0f;
        if (show && tutorialZone == null)
        {
            tutorialZone = StopZoneView.Create(Town, "tutorial", TutorialPadCenter, 0f, null);
            tutorialDwell = 0f;
        }
        if (!show && tutorialZone != null) { tutorialZone.Dismiss(); tutorialZone = null; tutorialDwell = 0f; }
        if (tutorialZone != null) tutorialZone.SetProgress(TutorialPadProgress01);
    }

    // Aturan jeda sama dengan zona lain (HANDOFF 14). Terpicu sekali lalu terkunci sampai truk keluar zona.
    public void AdvanceTutorialZone(float seconds)
    {
        if (MenuOpen) return;
        UpdateTutorialZone();
        if (tutorialZone == null) return;
        bool inside = StopZoneView.InsidePad(Car.transform.position, TutorialPadCenter, 0f, "tutorial");
        if (!inside) tutorialPromptLatched = false;
        bool stopped = inside && !tutorialPromptLatched && Car.CurrentSpeed < 0.3f;
        tutorialDwell = Mathf.Clamp(tutorialDwell + Mathf.Max(0f, seconds) * (stopped ? 1f : -2.5f), 0f, StopSeconds);
        tutorialZone.SetProgress(TutorialPadProgress01);
        if (tutorialDwell < StopSeconds - StopZone.Epsilon) return;
        tutorialDwell = 0f;
        tutorialPromptLatched = true;
        GetComponent<KidFriendlyHud>().OpenTutorialPrompt();
    }

    // "Ya": sesi baru dalam keadaan tutorial dari titik mulai. Koin dan skin tidak berubah. Ulang tutorial tidak berakhir
    // di lobby: sesudah surat sampai, kapsul dikenalkan lalu permainan lanjut (permintaan pengguna 27 Sep).
    // Truk dipindah saat popup masih menutupi layar; titik mulai hanya beberapa langkah dari zona ini.
    public void AnswerTutorialPrompt(bool start)
    {
        var hud = GetComponent<KidFriendlyHud>();
        if (!start) { hud.ResumeGame(); return; }
        forceTutorial = true;
        ResetRun();
        Car.PlaceAt(Town.StartPosition, Town.StartHeading);
        hud.ResumeGame();
    }
}
