using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
public partial class DeliveryGameManager : MonoBehaviour
{
    public static DeliveryGameManager Instance { get; private set; }
    public int Deliveries { get; private set; }
    // Koin yang didapat sesi ini (nama lama "Stars"); rangkuman Selesai kerja = koin didapat.
    public int Stars { get; private set; }
    // Bintang yang diambil sesi ini (rangkuman Selesai kerja).
    public int StarPickups { get; private set; }
    public void CountStarPickup() => StarPickups++;
    public void AddRunCoins(int coins) => Stars += coins;
    public string StatusText { get; private set; }
    public string GuideText { get; private set; }
    public string MessageText { get; private set; }
    public DeliveryTown Town { get; private set; }
    public Driver Car { get; private set; }
    public DeliveryMarker ActiveMarker { get; private set; }
    public bool Carrying { get; private set; }
    public Vector2 TargetPosition => ActiveMarker == null ? Town.Depot : (Vector2)ActiveMarker.transform.position;
    private DeliveryContentCatalog catalog;
    private SpriteRenderer arrow;
    private DeliveryTown.Address address;
    private float messageUntil;
    private bool ready;
    private DeliveryDeck deck;
    private int houseIndex;
    public bool TutorialTrip { get; private set; }
    private bool tutorialPending;
    // Sesi tutorial: dari awal (belum ambil paket) sampai paket tutorial sampai (layar 09).
    public bool TutorialActive => tutorialPending || TutorialTrip;
    private float parkingDwell;
    private bool parkingPromptLatched;
    public DeliveryActivities Activities { get; private set; }
    public string BonusText => Activities == null ? string.Empty : Activities.Hint;
    public int RemainingHomes => deck == null ? 0 : deck.Remaining;
    public static bool IsValidationRunning;
    public CourierProgress Progress { get; private set; }
    public DeliveryMinimap Minimap { get; private set; }
    public DeliverySideQuests SideQuests { get; private set; }
    public bool MenuOpen { get; set; }
    public int CareerStamps { get; private set; }
    public int Livery { get; private set; }
    public int UnlockedLiveries => 1 + (CareerStamps>=3?1:0) + (CareerStamps>=8?1:0) + (CareerStamps>=15?1:0);
    public string RewardText => $"Cap {CareerStamps} | Corak truk {Livery+1}/{UnlockedLiveries}";
    public bool FinishRequested { get; private set; }
    public bool HasPendingDelivery => Carrying || (SideQuests != null && SideQuests.CarriedLetters > 0);
    public Vector2 FinishTarget => Carrying ? TargetPosition : SideQuests.CarriedLetters > 0
        ? SideQuests.DropoffPosition : Town.FinishParkingArea.center;
    public Vector2 NavigationTarget => SpecialTarget.HasValue ? SpecialTarget.Value : GuideNavigation ?? (FinishRequested ? FinishTarget
        : Activities!=null && Activities.GuideToBonus ? Activities.BonusPosition : TargetPosition);
    // Tujuan misi khusus; null = alur antar biasa.
    public Vector2? SpecialTarget { get; private set; }
    private bool specialToHouse;
    public string AddressName => Carrying ? address.Name : string.Empty;
    public bool IsShowingDestination { get; private set; }
    // Sorotan tujuan: 2 dtk waktu nyata (hud.json sorotan); sisa waktunya menggerakkan timer kartu sorotan.
    public const float DestinationPreviewSeconds = 2f;
    private float previewEndsAt;
    public float DestinationPreviewRemaining => IsShowingDestination ? Mathf.Max(0f, previewEndsAt - Time.realtimeSinceStartup) : 0f;
    private Coroutine destinationPreview;
    private Camera previewCamera;
    private Unity.Cinemachine.CinemachineBrain previewBrain;
    private bool brainWasEnabled;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private Vector2 previewedFinishTarget;
    // Pratinjau rumah tujuan (layar 14, disetujui pengguna 26 Sep): kamera menyorot rumah sesudah paket diambil,
    // hanya di 3 pengantaran pertama tiap sesi supaya tidak mengganggu; bisa dilewati dengan satu ketukan.
    public const int HousePreviewsPerSession = 3;
    private int housePreviews;
    private StopZoneView finishZone;
    // Kapsul selesai kerja (permintaan pengguna 26 Sep): selalu ada selama tidak ada kiriman tertunda (paket,
    // surat); hilang begitu paket diambil. Cara muncul (koreksi pengguna 26 Sep malam):
    // - mulai main dari lobby (baru atau lanjut): pudar masuk dari transparan ke solid, tanpa kedip;
    // - misi selesai (kiriman terakhir diserahkan): berkedip saat itu juga;
    // - tombol selesai kerja: kamera meluncur ke kapsul lalu kapsul berkedip. Kalau masih ada kiriman, sorotan ke
    //   kapsul menunggu paket selesai melompat ke pintu (finishPreviewAfter).
    // Selama berkedip kapsul belum bisa dipakai.
    public const float FinishBlinkSeconds = 1.2f, FinishFadeSeconds = 0.6f;
    private bool finishFadeIn;
    private float finishPreviewAfter;
    public bool FinishBlocked
    {
        get
        {
            return HasPendingDelivery;
        }
    }
    public bool FinishReady => finishZone != null && (IsValidationRunning || !finishZone.Blinking);
    private Sprite arrowToDepot, arrowToHouse;
    public const float StopSeconds = 0.75f;
    public float ParkingProgress01 => parkingDwell / StopSeconds;
    private readonly Color parcelColor = new Color32(0xF0,0x9A,0x2C,255);
    private readonly Color homeColor = new Color32(0x1D,0xB4,0xE8,255);
    private Unity.Cinemachine.CinemachineCamera followCamera;
    private bool cameraWasPortrait;
    private Vector2Int cameraScreen;
    private Transform cameraTarget;
    private bool lobbyView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // AfterSceneLoad hanya jalan sekali per peluncuran; ganti peta (SelectMode) dan Keluar dari Classic memuat
        // ulang scene sehingga pengelola lama ikut hancur. sceneLoaded memasang pengelola baru di setiap muat tunggal.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureManager(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) EnsureManager(scene);
    }

    private static void EnsureManager(Scene scene)
    {
        if((scene.name!="SampleScene" && scene.name!="KotaPaket") || FindFirstObjectByType<DeliveryGameManager>()!=null)return;
        var manager=new GameObject("Pengantaran paket");
        SceneManager.MoveGameObjectToScene(manager,scene);
        manager.AddComponent<DeliveryGameManager>();
    }

    private IEnumerator Start()
    {
        Instance=this;
        Car=FindFirstObjectByType<Driver>();
        catalog=Resources.Load<DeliveryContentCatalog>("DeliveryContentCatalog");
        if(Car==null||catalog==null){Debug.LogError("Mobil atau katalog aset belum tersedia.");yield break;}
        Progress=CourierProgress.Load();
        string selectedMap = Progress.LastMode == "blok-paket" ? "blok-paket" : "kota-paket";
        TextAsset selectedLayout = selectedMap == "blok-paket" ? catalog.blokPaketLayout : catalog.kotaPaketLayout;
        Town=FindFirstObjectByType<DeliveryTown>();
        if (selectedLayout != null && (Town == null || Town.LayoutVersion != DeliveryTown.CurrentLayoutVersion || Town.MapId != selectedMap))
        {
            if (Town != null) Town.gameObject.SetActive(false);
            Town = null;
        }
        // Preserve the authored scene. Replace its test environment only for this play session.
        foreach(GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if(root==gameObject||root==Car.gameObject||(Town!=null && root==Town.gameObject)||root.GetComponent<Camera>()!=null||
                root.name=="CinemachineCamera"||root.name=="Global Light 2D")continue;
            root.SetActive(false);
        }
        foreach(Collision oldHandler in FindObjectsByType<Collision>(FindObjectsSortMode.None))oldHandler.enabled=false;
        if (Town == null)
        {
            Town=new GameObject(selectedMap == "blok-paket" ? "Blok Paket v7" : "Kota Paket v7").AddComponent<DeliveryTown>();
            Town.Build(catalog, selectedLayout);
        }
        Car.PlaceAt(Town.StartPosition,Town.StartHeading);
        // Di luar peta terlihat rumput, bukan warna latar bawaan kamera.
        if (Camera.main != null) Camera.main.backgroundColor = new Color32(0x78, 0xA3, 0x55, 255);
        followCamera = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (followCamera != null)
        {
            // Cinemachine drives Main Camera; it must not render a second perspective view.
            Camera duplicateCamera = followCamera.GetComponent<Camera>();
            if (duplicateCamera != null) duplicateCamera.enabled = false;
            // SmartUpdate memindah kamera ke jadwal FixedUpdate sesudah lama mengikuti truk (Rigidbody). Lobby
            // membekukan waktu (timeScale 0), FixedUpdate tidak jalan, jadi kamera lobby sesudah "Selesai kerja"
            // tertinggal di posisi main (laporan pengguna 26 Sep). Truk memakai interpolasi, jadi LateUpdate tetap mulus.
            var brain = Camera.main != null ? Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
            if (brain != null) brain.UpdateMethod = Unity.Cinemachine.CinemachineBrain.UpdateMethods.LateUpdate;
            // Kamera melihat 1,5 unit ke depan truk seperti acuan desain (aset/dunia/*.json: kamera = truk + 1,5·arah).
            cameraTarget = new GameObject("Titik kamera").transform;
            cameraTarget.SetParent(Car.transform, false);
            cameraTarget.localPosition = new Vector3(0f, CameraLookAhead, 0f);
            followCamera.Follow = cameraTarget;
            cameraWasPortrait = DeliveryDash.UI.UiRoot.PortraitScreen;
            UpdateCameraFraming(true);
        }
        KidFriendlyHud hud=gameObject.AddComponent<KidFriendlyHud>();hud.Configure(Car,this);
        Activities=gameObject.AddComponent<DeliveryActivities>();
        Activities.Configure(this,catalog);
        CareerStamps=PlayerPrefs.GetInt("Courier.Stamps",0);
        Livery=Mathf.Clamp(PlayerPrefs.GetInt("Courier.Livery",0),0,UnlockedLiveries-1);
        gameObject.AddComponent<DeliveryAudio>().Configure(this);
        // Penanda musik ikut dinonaktifkan bersama akar scene lain di atas; rujukannya tetap terbaca.
        DeliveryMusicSet music=FindFirstObjectByType<DeliveryMusicBank>(FindObjectsInactive.Include)?.music;
        if(music!=null)gameObject.AddComponent<DeliveryMusic>().Configure(music,()=>hud.MusicMode);
        Livery=Progress.Equipped;Car.SetLivery(Livery);
        Minimap=gameObject.AddComponent<DeliveryMinimap>();Minimap.Configure(this);
        SideQuests=gameObject.AddComponent<DeliverySideQuests>();SideQuests.Configure(this);
        Application.targetFrameRate=60;
        CreateArrow();
        ready=true;
        ResetRun();
        // Pemain baru langsung bermain dengan tutorial sesudah layar muat (permintaan pengguna 27 Sep); lobby menyusul
        // sesudah tutorial selesai di kapsul.
        if(!IsValidationRunning){if(GuideActive)hud.ResumeGame();else hud.OpenLobby();}
        // Let the follow camera settle before taking the editor preview.
        yield return new WaitForSeconds(3f);
#if UNITY_EDITOR
        System.IO.Directory.CreateDirectory("Logs");
        ScreenCapture.CaptureScreenshot("Logs/delivery-dash-preview.png");
#endif
        Debug.Log($"Kota siap: {Town.Houses.Count} alamat, {Town.BarrierCount} penghalang trotoar.");
    }

    // newSession = false hanya untuk akhir misi khusus di tengah sesi: dunia dirapikan, hitungan rangkuman tetap.
    public void ResetRun(bool newSession = true)
    {
        if(!ready)return;
        CancelDestinationPreview();
        FinishRequested=false;
        SpecialTarget=null;
        if(ActiveMarker!=null)Destroy(ActiveMarker.gameObject);
        Carrying=false;
        if(newSession){Deliveries=0;Stars=0;StarPickups=0;housePreviews=0;}
        parkingDwell=0;parkingPromptLatched=false;
        UpdateFinishZone();
        // Tutorial untuk pemain baru, atau saat diminta lewat zona abu-abu (forceTutorial).
        int tutorialHouse = Progress.NeedsTutorial || forceTutorial ? Town.TutorialHouseIndex() : -1;
        bool firstTutorial = Progress.NeedsTutorial && !forceTutorial;
        forceTutorial = false;
        tutorialPending = tutorialHouse >= 0;
        TutorialTrip = false;
        if (tutorialPending) BeginGuide(firstTutorial); else EndGuide();
        tutorialPromptLatched = false;
        UpdateTutorialZone();
        float[] weights = new float[Town.Houses.Count];
        for (int i = 0; i < weights.Length; i++) weights[i] = Town.Houses[i].Weight;
        deck=new DeliveryDeck(Town.Houses.Count,Random.Range(0,int.MaxValue),tutorialHouse,weights);
        Activities.Clear();
        SideQuests?.ResetQuests();
        Car.ClearBoost();
        // ResetRun tidak memindah truk (tanpa teleport di tengah permainan, permintaan pengguna 26 Sep). Sesi baru
        // (Start, Selesai kerja, ulang tutorial) memindah truk ke titik mulai sendiri saat layar tertutup lobby/popup.
        SpawnDepotParcel();
        // Tutorial memakai kartu sorotan sendiri, bukan pesan.
        if (tutorialPending) ShowMessage(string.Empty, 0f);
        else ShowMessage("Berhenti di kotak jingga depan gudang untuk mengambil paket.",7f);
    }

    private void Update()
    {
        UpdateCameraFraming(false);
        if(!ready || MenuOpen || Time.timeScale==0)return;
        if(FinishRequested && FinishTarget!=previewedFinishTarget && Time.time>=finishPreviewAfter)ShowFinishDestination();
        if(IsShowingDestination)return;
        if(Time.time>messageUntil)MessageText=string.Empty;
        AdvanceParking(Time.deltaTime);
        if (MenuOpen) return;
        AdvanceTutorialZone(Time.deltaTime);
        if (MenuOpen) return;
        UpdateTutorialGuide();
        if(ActiveMarker==null)return;
        float distance=Vector2.Distance(Car.transform.position,TargetPosition);
        GuideText=FinishRequested ? (HasPendingDelivery?"Selesaikan pengantaran dahulu.":"Boleh selesai. Berhenti di kotak parkir biru.")
            :Carrying?$"Rumah {address.Name}  •  {distance:0} m":$"Kembali ke gudang  •  {distance:0} m";
        AdvanceActiveDwell(Time.deltaTime);
    }

    // HANDOFF 14: diam di zona (pusat truk di dalam bantalan dan |kecepatan| < 0,3) → +dt, selain itu −2,5·dt.
    public void AdvanceActiveDwell(float seconds)
    {
        if (ActiveMarker == null || (FinishRequested && !Carrying)) return;
        StopZone zone = ActiveMarker.GetComponent<StopZone>();
        if (zone == null) return;
        bool stopped = IsInsideActiveZone(Car.transform.position) && Car.CurrentSpeed < 0.3f;
        if (zone.AdvanceDwell(stopped, seconds)) TryInteract(ActiveMarker.gameObject);
    }

    public bool IsInsideActiveZone(Vector2 point)
    {
        if (ActiveMarker == null) return false;
        return StopZoneView.InsidePad(point, ActiveMarker.transform.position, ActiveMarker.ZoneAngle,
            string.IsNullOrEmpty(ActiveMarker.ZoneKind) ? "paket" : ActiveMarker.ZoneKind);
    }

    // Lobby dan layar di atasnya memakai bingkai tetap seperti acuan desain (a-w-lobi, Tools/art/capture-world.sh):
    // gudang, parkiran, dan kota sekitarnya terlihat. Kamera ikut truk tidak dipakai di sini karena menu membekukan
    // timeScale sehingga kamera tidak pernah sampai ke truk.
    public void SetLobbyView(bool value)
    {
        if (lobbyView == value) return;
        lobbyView = value;
        UpdateCameraFraming(true);
        // Keluar lobby = mulai main: kapsul yang muncul sekarang pudar masuk; yang muncul nanti (misi selesai) berkedip.
        finishFadeIn = !value;
        if (ready) { UpdateFinishZone(); UpdateTutorialZone(); }
        finishFadeIn = false;
    }

    private void UpdateCameraFraming(bool force)
    {
        if (followCamera == null) return;
        bool portrait = DeliveryDash.UI.UiRoot.PortraitScreen;
        Vector2Int screenSize = DeliveryDash.UI.UiRoot.ScreenOverride ?? new Vector2Int(Screen.width, Screen.height);
        if (!force && portrait == cameraWasPortrait && screenSize == cameraScreen) return;
        cameraScreen = screenSize;
        cameraWasPortrait = portrait;
        var lens = followCamera.Lens;
        if (lobbyView && Town != null)
        {
            // Landscape 17 px/unit, pusat 23,8 kanan dan 11 atas parkir selesai; portrait 19 px/unit, pusat 5,6 kanan dan 5,1 bawah.
            Vector2 center = Town.FinishCenter + (portrait ? new Vector2(5.6f, -5.1f) : new Vector2(23.8f, 11f));
            followCamera.Follow = null;
            followCamera.transform.position = new Vector3(center.x, center.y, followCamera.transform.position.z);
            // Tinggi panel UI sebenarnya (UiRoot: skala Expand dari acuan 1280×720 / 720×1280), supaya dunia tetap
            // 17/19 px panel per unit di layar 20:9 maupun 4:3.
            Vector2 screen = screenSize;
            Vector2 reference = portrait ? new Vector2(720f, 1280f) : new Vector2(1280f, 720f);
            float panelHeight = screen.y / Mathf.Min(screen.x / reference.x, screen.y / reference.y);
            lens.OrthographicSize = panelHeight / (portrait ? 19f : 17f) * 0.5f;
        }
        else
        {
            if (cameraTarget != null) followCamera.Follow = cameraTarget;
            lens.OrthographicSize = portrait ? 10.5f : 6.5f;
        }
        followCamera.Lens = lens;
        followCamera.PreviousStateIsValid = false;
    }

    public void RequestFinishWork()
    {
        // Pemain memilih berhenti di tengah tutorial: panduan dihentikan supaya tidak menyorot kotak yang sudah hilang.
        // Tutorial pertama yang belum selesai akan mulai lagi di sesi berikutnya (TutorialComplete masih false).
        if(GuideActive&&Guide!=GuideStep.ToFinish&&Guide!=GuideStep.FinishInfo)EndGuide();
        FinishRequested=true;
        GetComponent<KidFriendlyHud>().ResumeGame();
        ShowFinishDestination();
    }

    private void ShowFinishDestination()
    {
        previewedFinishTarget=FinishTarget;
        // Selama tutorial kartu panduan sudah menyuruh ke kapsul; pesan kedua hanya menutupi tangan penunjuk.
        if(!GuideActive)ShowMessage(HasPendingDelivery?"Selesaikan pengantaran dahulu.":"Boleh selesai! Berhenti di kotak parkir biru.",4f);
        // Kamera sampai di parkiran setelah meluncur 0,4 dtk; kapsul berkedip di sana seolah baru muncul.
        if(!FinishBlocked)BlinkFinishZone(IsValidationRunning?0f:0.4f);
        if(!IsValidationRunning)destinationPreview=StartCoroutine(PreviewDestination(previewedFinishTarget));
    }

    // Sorotan menunggu paket selesai melompat masuk truk (waktu permainan), supaya paket tidak beku di udara.
    private IEnumerator PreviewHouseAfterHop(Vector2 target)
    {
        yield return new WaitForSeconds(ParcelHop.Duration);
        destinationPreview=null;
        if(!Carrying||MenuOpen||FinishRequested)yield break;
        destinationPreview=StartCoroutine(PreviewDestination(target));
    }

    private IEnumerator PreviewDestination(Vector2 target)
    {
        CancelDestinationPreview();
        previewCamera=Camera.main;
        if(previewCamera==null)yield break;
        previewBrain=previewCamera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        brainWasEnabled=previewBrain!=null && previewBrain.enabled;
        savedCameraPosition=previewCamera.transform.position;
        savedCameraRotation=previewCamera.transform.rotation;
        if(previewBrain!=null)previewBrain.enabled=false;
        Car.StopImmediately();
        IsShowingDestination=true;Time.timeScale=0;previewEndsAt=Time.realtimeSinceStartup+DestinationPreviewSeconds;
        // gerak.json kamera-sorot: meluncur ke tujuan 0–0,2, diam, kembali 0,8–1 (in-out-sine), bukan melompat.
        Vector3 from=savedCameraPosition, to=new Vector3(target.x,target.y,savedCameraPosition.z);
        while(Time.realtimeSinceStartup<previewEndsAt)
        {
            float t=1f-DestinationPreviewRemaining/DestinationPreviewSeconds;
            float lerp=t<0.2f?Ease.InOutSine(t/0.2f):t<0.8f?1f:1f-Ease.InOutSine((t-0.8f)/0.2f);
            previewCamera.transform.position=Vector3.LerpUnclamped(from,to,lerp);
            yield return null;
        }
        RestoreDestinationCamera();
        destinationPreview=null;
    }

    // Ketukan saat sorotan: langsung ke fase kembali. Kurva simetris, jadi saat masih meluncur (t < 0,2) kamera
    // berbalik dari titik yang sama (sisa = waktu yang sudah lewat); sesudahnya sisa 0,4 dtk untuk meluncur pulang.
    public void SkipDestinationPreview()
    {
        if(!IsShowingDestination)return;
        float elapsed=DestinationPreviewSeconds-DestinationPreviewRemaining;
        float remaining=Mathf.Min(DestinationPreviewRemaining,Mathf.Min(elapsed,0.2f*DestinationPreviewSeconds));
        previewEndsAt=Time.realtimeSinceStartup+remaining;
    }

    public void CancelDestinationPreview()
    {
        if(destinationPreview!=null){StopCoroutine(destinationPreview);destinationPreview=null;}
        RestoreDestinationCamera();
    }

    private void RestoreDestinationCamera()
    {
        if(!IsShowingDestination)return;
        if(previewCamera!=null)previewCamera.transform.SetPositionAndRotation(savedCameraPosition,savedCameraRotation);
        if(previewBrain!=null)previewBrain.enabled=brainWasEnabled;
        IsShowingDestination=false;Time.timeScale=MenuOpen || Minimap?.Expanded==true?0:1;
    }

    // Area selesai kerja memakai aturan jeda yang sama dengan zona lain (HANDOFF 14): keluar/bergerak menurun 2,5×,
    // tidak mereset. Terpicu sekali lalu terkunci sampai truk keluar area.
    public void AdvanceParking(float seconds)
    {
        if (MenuOpen) return;
        UpdateFinishZone();
        bool inside = Town.IsInsideFinishParking(Car.transform.position);
        if (!inside) parkingPromptLatched=false;
        // Kapsul yang tidak tampil atau masih berkedip tidak bisa dipakai.
        bool stopped = inside && FinishReady && !parkingPromptLatched && Car.CurrentSpeed < 0.3f;
        parkingDwell = Mathf.Clamp(parkingDwell + Mathf.Max(0,seconds) * (stopped ? 1f : -2.5f), 0f, StopSeconds);
        UpdateFinishZone();
        if (parkingDwell < StopSeconds - StopZone.Epsilon) return;
        parkingDwell=0;parkingPromptLatched=true;
        UpdateFinishZone();
        // Tutorial pertama berakhir di kapsul: langsung ke lobby, tanpa dialog selesai kerja.
        if(Guide==GuideStep.ToFinish && FirstTutorial){CompleteFirstTutorial();return;}
        if(HasPendingDelivery){RequestFinishWork();return;}
        GetComponent<KidFriendlyHud>().OpenFinishWork();
    }

    private void UpdateFinishZone()
    {
        bool show = !lobbyView && !FinishBlocked && !GuideHidesFinish && Town != null && Town.FinishSize.sqrMagnitude > 0f;
        if (show && finishZone == null)
        {
            finishZone = StopZoneView.Create(Town, "kapsul", Town.FinishCenter, Town.FinishAngle, null);
            // Masih menunggu sorotan selesai kerja (ShowFinishDestination memanggil Blink lagi): tetap tak terlihat dulu.
            if (!IsValidationRunning)
            {
                if (FinishRequested) finishZone.Blink(ParcelHop.Duration + 0.4f, FinishBlinkSeconds);
                else if (finishFadeIn) finishZone.FadeIn(FinishFadeSeconds);
                else finishZone.Blink(0f, FinishBlinkSeconds);
            }
        }
        if (!show && finishZone != null) { finishZone.Dismiss(); finishZone = null; parkingDwell = 0f; }
        if (finishZone != null) finishZone.SetProgress(ParkingProgress01);
    }

    private void BlinkFinishZone(float delay)
    {
        if (finishZone == null) UpdateFinishZone();
        if (finishZone == null) return;
        finishZone.Blink(delay, FinishBlinkSeconds);
    }

    public void AnswerFinishWork(bool finish)
    {
        if(finish && HasPendingDelivery){RequestFinishWork();return;}
        if(finish && (!Town.IsInsideFinishParking(Car.transform.position)
            || Car.CurrentSpeed>=0.3f)){RequestFinishWork();return;}
        parkingDwell=0;
        // Permintaan pengguna (26 Sep): tidak pernah ada teleport ke gudang. "Belum" hanya menutup dialog; truk tetap
        // di tempat parkirnya dan pertanyaan tidak muncul lagi sampai truk keluar area selesai (parkingPromptLatched).
        if (!finish) FinishRequested=false;
        var hud=GetComponent<KidFriendlyHud>();
        // Selesai kerja = akhir sesi: rangkuman sudah tampil, jadi sesi berikutnya mulai dari nol (truk, tujuan, hitungan).
        // Sesi baru mulai dari titik spawn (permintaan pengguna 26 Sep). Bukan teleport di tengah permainan: lobby sudah
        // menutupi layar saat truk dipindah.
        if (finish) { FinishRequested=false;hud.OpenLobby();ResetRun();Car.PlaceAt(Town.StartPosition,Town.StartHeading); }
        else hud.ResumeGame();
    }

    private void LateUpdate()
    {
        if (!ready || arrow == null || Camera.main == null) return;
        // Lobby polos seperti acuan (capture-world.sh polos=1): zona tujuan baru tampil saat bermain.
        // Pulang ke parkiran (layar 21): zona ambil paket bukan tujuan, jadi disembunyikan (paket baru tidak diambil).
        // Sesudah paket tutorial: paket berikutnya ditahan sampai tutorial selesai.
        bool hideZone = lobbyView || (FinishRequested && !Carrying) || GuideHoldsParcel;
        if (ActiveMarker != null && ActiveMarker.ZoneView != null && ActiveMarker.ZoneView.gameObject.activeSelf == hideZone)
            ActiveMarker.ZoneView.gameObject.SetActive(!hideZone);
        // Panah desain (Gerbang Desain 3/4, gate3-client.js): 3,4 unit dari truk, menunjuk lurus ke tujuan, 2,2 unit.
        // Disembunyikan saat truk sudah hampir di tujuan supaya tidak menutupi zona.
        Vector2 navigationTarget=NavigationTarget;
        Vector2 truck=Car.transform.position;
        Vector2 toTarget=navigationTarget-truck;
        // Permintaan pengguna (26 Sep): panah dunia menggantikan minimap. Minimap tampil = panah hilang, dan sebaliknya.
        bool minimapShown=Minimap!=null && Minimap.Visible;
        arrow.enabled=!MenuOpen && !minimapShown && (ActiveMarker!=null || FinishRequested || SpecialTarget.HasValue) && toTarget.magnitude>ArrowHideDistance;
        SetArrow(SpecialTarget.HasValue ? specialToHouse : Carrying || FinishRequested || GuideNavigation.HasValue
            || (Activities!=null && Activities.GuideToBonus));
        if(!arrow.enabled)return;
        Vector2 direction=toTarget.normalized;
        arrow.transform.position=new Vector3(truck.x+direction.x*ArrowOffset,truck.y+direction.y*ArrowOffset,-1f);
        arrow.transform.rotation=Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.up,direction));
    }

    public const float CameraLookAhead=1.5f;
    public const float ArrowOffset=3.4f, ArrowSize=2.2f, ArrowHideDistance=4.2f;

    private void SpawnDepotParcel()
    {
        Carrying=false;
        StatusText=Deliveries==0?"Ambil paket di gudang":"Paket berikutnya menunggu di gudang";
        ActiveMarker=MakeMarker(DeliveryMarkerKind.Package,Town.Depot,0f);
        Town.SetDestination(Town.Depot);
        SetArrow(false);
    }

    public void TryInteract(GameObject touched)
    {
        if(!ready||touched==null)return;
        if(FinishRequested && !Carrying)return;
        if(GuideHoldsParcel && !Carrying)return;
        DeliveryMarker marker=touched.GetComponentInParent<DeliveryMarker>();
        if(marker==null||marker!=ActiveMarker)return;
        StopZone stopZone = marker.GetComponent<StopZone>();
        if (stopZone != null && !stopZone.Ready) return;
        // Verify state and proximity BEFORE consuming a trigger, preventing wrong or repeated deliveries.
        if(!IsInsideActiveZone(Car.transform.position))return;
        if(Carrying && marker.Kind!=DeliveryMarkerKind.Customer)return;
        if(!Carrying && marker.Kind!=DeliveryMarkerKind.Package)return;
        if(!marker.CollectOnce())return;
        Vector2 point=marker.transform.position;
        marker.PlayCollected();
        Destroy(marker.gameObject);
        ActiveMarker=null;
        if(!Carrying)
        {
            DeliveryAudio.Play(DeliveryAudio.Cue.Pickup);
            ParcelHop.Play(catalog.packageSprite,Town.LoadingDock,Car.transform.position,Car.transform,true);
            Carrying=true;
            // Visit every address without repeating until all have been served.
            houseIndex=deck.Reserve();
            address=Town.Houses[houseIndex];
            ActiveMarker=MakeMarker(DeliveryMarkerKind.Customer,address.Stop,Town.HouseAngle(houseIndex));
            Town.SetDestination(address.Stop);
            TutorialTrip = tutorialPending;
            Activities.BeginTrip(TutorialTrip);
            SetArrow(true);
            if(!IsValidationRunning && housePreviews<HousePreviewsPerSession)
            {
                housePreviews++;
                destinationPreview=StartCoroutine(PreviewHouseAfterHop(address.Stop));
            }
            StatusText="Antar paket ke rumah "+address.Name;
            if (TutorialTrip) ShowMessage("Paket siap! Ayo antar.", 2.5f);
            else ShowMessage(Activities.IsExpress ? $"Paket ekspres: {Activities.ExpressSecondsLeft} dtk untuk bonus +5"
                : Activities.IsFragile ? "Jangan menabrak! Bonus +5 jika kurang dari 3 benturan."
                : Activities.HasStopover ? Activities.Hint
                : "Paket siap! Ikuti petunjuk menuju rumah.",6f);
        }
        else
        {
            deck.Complete(houseIndex);
            DeliveryAudio.Play(DeliveryAudio.Cue.Delivery);
            ParcelHop.Play(catalog.packageSprite,Car.transform.position,address.Door,null,false);
            finishPreviewAfter=Time.time+ParcelHop.Duration;
            int bonus=Activities.CompleteTrip();
            Carrying=false;Deliveries++;Stars+=12+bonus;
            if (!TutorialTrip) SideQuests.OnDeliveryCompleted(Random.value);
            // Tutorial dianggap selesai begitu paketnya sampai; keluar aplikasi sesudah ini tidak mengulanginya.
            if (TutorialTrip) Progress.TutorialComplete=true;
            if(!IsValidationRunning)
            {
                Progress.Coins+=12+bonus;Progress.CompletedDeliveries++;
                Progress.Save();
            }
            if (TutorialTrip) { tutorialPending=false; OnTutorialDelivered(address.Stop); }
            string result=Activities.LastResult;
            if(Activities.LastContractWon && !IsValidationRunning)
            {
                CareerStamps++;
            }
            SpawnDepotParcel();
            ShowMessage(result,TutorialTrip?3f:6f);
            TutorialTrip=false;
        }
        if (Carrying) StartCoroutine(Confetti(point));
        else StartCoroutine(ConfettiAfterHop(address.Door));
    }

    // Konfeti muncul saat paket mendarat di pintu (akhir paket-keluar).
    private IEnumerator ConfettiAfterHop(Vector2 door)
    {
        yield return new WaitForSeconds(ParcelHop.Duration * 0.81f);
        yield return Confetti(door);
    }

    public void CycleLivery()
    {
        for(int i=1;i<=10;i++)if(Progress.Owns((Livery+i)%10)){SelectSkin((Livery+i)%10);break;}
    }

    public bool SelectSkin(int index)
    {
        bool purchased=!Progress.Owns(index);
        if(!Progress.Select(index))return false;
        DeliveryAudio.Play(purchased?DeliveryAudio.Cue.Purchase:DeliveryAudio.Cue.Click);
        Livery=Progress.Equipped;Car.SetLivery(Livery);Progress.Save();
        return true;
    }

    public void BeginSpecialMission()
    {
        if (ActiveMarker != null) Destroy(ActiveMarker.gameObject);
        ActiveMarker = null;
        Carrying = false;
        FinishRequested = false;
        SpecialTarget = null;
        Activities?.Clear();
        SideQuests?.ResetQuests();
    }

    public void SetSpecialStatus(string status, string guide)
    {
        StatusText = status;
        GuideText = guide;
    }

    public void SetSpecialGuide(string guide) => GuideText = guide;

    public void SetSpecialTarget(Vector2? target, bool toHouse)
    {
        SpecialTarget = target;
        specialToHouse = toHouse;
    }

    public void EndSpecialMission()
    {
        ResetRun(false);
    }

    public void ShowSpecialMessage(string text) => ShowMessage(text, 5f);

    public void SelectMode(string mode)
    {
        if (mode != "kota-paket" && mode != "blok-paket" && mode != "classic") return;
        if (mode == "classic")
        {
            if (Application.CanStreamedLevelBeLoaded("Classic")) SceneManager.LoadScene("Classic");
            else Debug.LogWarning("Scene Classic belum tersedia dalam profil scene.");
            return;
        }
        if (Progress.LastMode == mode) return;
        Progress.LastMode = mode;
        Progress.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetProgressConfirmed()
    {
        Progress=new CourierProgress();Progress.Save();CareerStamps=0;Livery=0;
        if(!IsValidationRunning){PlayerPrefs.DeleteKey("Courier.Stamps");PlayerPrefs.DeleteKey("Courier.Livery");PlayerPrefs.Save();}
        Car.SetLivery(0);ResetRun();SideQuests.ResetQuests();
    }

    // Legacy collision component compatibility; no punitive text or direction changes.
    public void NotifyBump() { }

    public void AwardBonus(int stars,string message)
    {
        if(stars>0)DeliveryAudio.Play(DeliveryAudio.Cue.Coin);
        Stars+=stars;
        if(!IsValidationRunning && stars>0){Progress.Coins+=stars;Progress.Save();}
        ShowMessage(message,2.5f);
    }

    // Zona ambil (jingga, 4,2 unit) atau antar (biru, 2,9 unit, menghadap rumah). Muncul hanya saat jadi tujuan.
    private DeliveryMarker MakeMarker(DeliveryMarkerKind kind,Vector2 p,float angle)
    {
        string zoneKind = kind==DeliveryMarkerKind.Package ? "paket" : "rumah";
        var root=new GameObject(kind==DeliveryMarkerKind.Package?"Ambil paket":"Berhenti di rumah");
        root.transform.SetParent(Town.transform,false);
        root.transform.position=p;
        DeliveryMarker marker=root.AddComponent<DeliveryMarker>();
        marker.Configure(kind);
        marker.ZoneKind=zoneKind;
        marker.ZoneAngle=angle;
        StopZone zone=root.AddComponent<StopZone>();
        zone.Configure(StopSeconds);
        marker.ZoneView=StopZoneView.Create(Town,zoneKind,p,angle,zone);
        return marker;
    }

    public void AdvanceActiveStop(float seconds)
    {
        if (ActiveMarker == null) return;
        StopZone zone = ActiveMarker.GetComponent<StopZone>();
        zone?.AdvanceDwell(true, seconds);
    }

    // Panah petunjuk bergaya token: jingga ke gudang, biru ke rumah (Gerbang Desain 2).
    private void CreateArrow()
    {
        GameObject go=new GameObject("Panah petunjuk");
        arrow=go.AddComponent<SpriteRenderer>();
        arrowToDepot=catalog.World("panah-jingga");
        arrowToHouse=catalog.World("panah-biru");
        arrow.sortingOrder=40;
        SetArrow(false);
    }

    private void SetArrow(bool toHouse)
    {
        if(arrow==null)return;
        Sprite sprite=toHouse?arrowToHouse:arrowToDepot;
        arrow.sprite=sprite;
        arrow.color=Color.white;
        if(sprite!=null)arrow.transform.localScale=Vector3.one*(ArrowSize/Mathf.Max(0.0001f,sprite.bounds.size.y));
    }

    private void ShowMessage(string text,float seconds){MessageText=text;messageUntil=Time.time+seconds;}

    private IEnumerator Confetti(Vector2 origin)
    {
        SpriteRenderer[] dots=new SpriteRenderer[12];
        for(int i=0;i<dots.Length;i++)dots[i]=Town.Disc("Konfeti",origin,0.14f,i%2==0?parcelColor:homeColor,35);
        for(float time=0;time<0.65f;time+=Time.deltaTime)
        {
            // konfeti-antar: opacity 1 → 0 pada 150 ms terakhir.
            float alpha=Mathf.Clamp01((0.65f-time)/0.15f);
            for(int i=0;i<dots.Length;i++)
            {
                float angle=i*Mathf.PI*2/dots.Length;
                dots[i].transform.position=origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*time*3;
                Color c=dots[i].color;c.a=alpha;dots[i].color=c;
            }
            yield return null;
        }
        foreach(SpriteRenderer dot in dots)if(dot!=null)Destroy(dot.gameObject);
    }

    private void OnDestroy()
    {
        CancelDestinationPreview();
        if(Instance==this)Instance=null;
        if(arrow!=null)Destroy(arrow.gameObject);
    }
}
