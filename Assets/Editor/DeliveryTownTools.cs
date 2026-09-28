using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeliveryDash.Editor.Tests;
using UnityEngine.SceneManagement;

public static class DeliveryTownTools
{
    // Batch entry point: bake the same editable scene used by the player, then test it.
    public static void BakeAndTest()
    {
        TruckSkinBaker.Bake();
        EditorSceneManager.OpenScene("Assets/Scenes/KotaPaket.unity");
        var town = UnityEngine.Object.FindFirstObjectByType<DeliveryTown>();
        // D5 keeps the authored legacy scene in Archive and builds the selected v7 map at runtime.
        // Retain the old upgrade path for developer scenes that still contain an editable town.
        if (town != null)
        {
            if (town.LayoutVersion < DeliveryTown.CurrentLayoutVersion) UpgradeTown();
            RebuildRoadCollision();
            UpdateDepotParking();
        }
        SessionState.SetBool("DeliveryDash.BatchTest", true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeBatchTest()
    {
        EditorApplication.update += RunPendingBatchTest;
    }

    private static void RunPendingBatchTest()
    {
        if (!SessionState.GetBool("DeliveryDash.BatchTest", false) ||
            !EditorApplication.isPlaying || DeliveryGameManager.Instance?.Town == null) return;
        SessionState.SetBool("DeliveryDash.BatchTest", false);
        ValidatePlay();
        string report = File.ReadAllText("Logs/delivery-play-checks.txt");
        EditorApplication.Exit(report.StartsWith("PASS:") ? 0 : 1);
    }

    [MenuItem("Delivery Dash/Perbarui Kota Berkelok")]
    public static void UpgradeTown()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Hentikan Play dahulu.");
        var old=UnityEngine.Object.FindFirstObjectByType<DeliveryTown>();
        if(old==null)throw new InvalidOperationException("Buka KotaPaket dahulu.");
        if(old.LayoutVersion>=6)throw new InvalidOperationException("Kota baru sudah tersedia.");
        // Preserve the previous town as an inactive, editable backup.
        var town=new GameObject("Kota paket").AddComponent<DeliveryTown>();
        try { town.Build(Resources.Load<DeliveryContentCatalog>("DeliveryContentCatalog")); }
        catch { UnityEngine.Object.DestroyImmediate(town.gameObject); throw; }
        string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/World/WindingTown");
        AssetDatabase.CreateFolder("Assets/World",Path.GetFileName(folder));
        town.SaveVisualAssets(folder);
        old.gameObject.SetActive(false);
        old.name="Kota sebelumnya (cadangan)";
        var follow=UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if(follow!=null)
        {
            var duplicate=follow.GetComponent<Camera>();
            if(duplicate!=null)duplicate.enabled=false;
            var lens=follow.Lens;lens.OrthographicSize=6.5f;follow.Lens=lens;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
        if (!EditorSceneManager.SaveScene(town.gameObject.scene)) throw new IOException("Map belum berhasil disimpan.");
        Selection.activeGameObject=town.gameObject;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0,44,0),new Vector3(144,136,1)),false);
        Debug.Log("WINDING_TOWN_SAVED");
    }
    [MenuItem("Delivery Dash/Perbarui Area Parkir")]
    public static void UpdateDepotParking()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
        var town=UnityEngine.Object.FindFirstObjectByType<DeliveryTown>();
        if(town==null)throw new InvalidOperationException("Buka KotaPaket dahulu.");
        town.EnsureDepotParking();
        EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
        if(!EditorSceneManager.SaveScene(town.gameObject.scene))throw new IOException("Parkir belum tersimpan.");
        Debug.Log("DEPOT_PARKING_SAVED");
    }

    [MenuItem("Delivery Dash/Sesuaikan Pembatas Jalan")]
    public static void RebuildRoadCollision()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
        var town = UnityEngine.Object.FindFirstObjectByType<DeliveryTown>();
        if (town == null) throw new InvalidOperationException("Buka KotaPaket dahulu.");
        if (town.CollisionVersion == DeliveryTown.CurrentCollisionVersion) return;
        Transform old = town.transform.Find("Tabrakan trotoar dan bangunan");
        Transform replacement = town.BuildBarriers();
        Undo.RegisterCreatedObjectUndo(replacement.gameObject, "Sesuaikan pembatas jalan");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        Physics2D.SyncTransforms();
        EditorUtility.SetDirty(town);
        EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
        if (!EditorSceneManager.SaveScene(town.gameObject.scene))
            throw new IOException("Pembatas baru belum tersimpan di scene.");
        Debug.Log("ROAD_COLLISION_SAVED: " + town.BarrierCount);
    }

    [MenuItem("Delivery Dash/Perbaiki Aset Jalan")]
    public static void RepairRoads()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
        var town = UnityEngine.Object.FindFirstObjectByType<DeliveryTown>();
        if (town == null) throw new InvalidOperationException("Buka KotaPaket dahulu.");
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/World/RoadRepair");
        AssetDatabase.CreateFolder("Assets/World", Path.GetFileName(folder));
        town.RepairSavedSquare(folder);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
        EditorSceneManager.SaveScene(town.gameObject.scene);
        Debug.Log("ROAD_ASSETS_REPAIRED");
    }
    [MenuItem("Delivery Dash/Lengkapi Detail Tepi Jalan")]
    public static void AddDetails()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
        var town = UnityEngine.Object.FindFirstObjectByType<DeliveryTown>();
        if (town == null) throw new InvalidOperationException("Buka KotaPaket dahulu.");
        town.AddRoadsideDetails();
        EditorSceneManager.MarkSceneDirty(town.gameObject.scene);
        EditorSceneManager.SaveScene(town.gameObject.scene);
        Debug.Log("Detail tepi jalan tersimpan.");
    }

    [MenuItem("Delivery Dash/Simpan Kota sebagai Scene")]
    public static void SaveTownScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Hentikan Play dahulu.");
        if (UnityEngine.Object.FindFirstObjectByType<DeliveryTown>() != null)
            throw new InvalidOperationException("Kota sudah tersedia. Edit objeknya langsung di Hierarchy.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "SampleScene") throw new InvalidOperationException("Buka SampleScene dahulu.");
        const string path = "Assets/Scenes/KotaPaket.unity";
        if (File.Exists(path)) throw new InvalidOperationException("Scene KotaPaket sudah ada; tidak ditimpa.");
        Driver car = UnityEngine.Object.FindFirstObjectByType<Driver>();
        if (car == null) throw new InvalidOperationException("Mobil tidak ditemukan.");
        // Save into a NEW scene, including existing unsaved work; leave the original file intact.
        if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Scene tidak dapat disimpan.");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == car.gameObject || root.GetComponent<Camera>() != null ||
                root.name == "CinemachineCamera" || root.name == "Global Light 2D") continue;
            root.SetActive(false);
        }
        if (!AssetDatabase.IsValidFolder("Assets/World")) AssetDatabase.CreateFolder("Assets", "World");
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/World/TownVisuals");
        AssetDatabase.CreateFolder("Assets/World", Path.GetFileName(folder));
        DeliveryTown town = new GameObject("Kota paket").AddComponent<DeliveryTown>();
        town.Build(Resources.Load<DeliveryContentCatalog>("DeliveryContentCatalog"));
        town.SaveVisualAssets(folder);
        car.transform.SetPositionAndRotation(town.StartPosition, Quaternion.identity);
        var follow = UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (follow != null)
        {
            var lens = follow.Lens;
            lens.OrthographicSize = 9f;
            follow.Lens = lens;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        Selection.activeGameObject = town.gameObject;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0,37,0), new Vector3(108,122,1)), false);
        Debug.Log("KOTA_SCENE_SAVED: " + path);
    }

    // Batch: -executeMethod DeliveryTownTools.ValidateFinishPreviewBatch (hasil di Logs/delivery-camera-checks.txt).
    public static void ValidateFinishPreviewBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/KotaPaket.unity");
        if (File.Exists("Logs/delivery-camera-checks.txt")) File.Delete("Logs/delivery-camera-checks.txt");
        SessionState.SetBool("DeliveryDash.CameraBatch", true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeCameraBatch()
    {
        double readyAt = 0;
        bool started = false;
        EditorApplication.update += () =>
        {
            if (!SessionState.GetBool("DeliveryDash.CameraBatch", false) || !EditorApplication.isPlaying
                || DeliveryGameManager.Instance?.Town == null || Camera.main == null) return;
            if (!started)
            {
                // Beri kota dan kamera satu detik untuk mapan, lalu masuk permainan dan minta selesai kerja.
                if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 1.0; return; }
                if (EditorApplication.timeSinceStartup < readyAt) return;
                started = true;
                DeliveryGameManager.Instance.GetComponent<KidFriendlyHud>().ResumeGame();
                ValidateFinishPreview();
                return;
            }
            if (!File.Exists("Logs/delivery-camera-checks.txt")) return;
            SessionState.SetBool("DeliveryDash.CameraBatch", false);
            EditorApplication.Exit(File.ReadAllText("Logs/delivery-camera-checks.txt").StartsWith("PASS") ? 0 : 1);
        };
    }

    // Batch: kamera lobby sesudah "Selesai kerja" harus sama dengan lobby awal (laporan pengguna 26 Sep).
    // Hasil: Logs/delivery-lobby-camera.txt dan Logs/captures/lobby-awal.png, lobby-sesudah-selesai.png.
    private const string LobbyCameraFlag = "DeliveryDash.LobbyCameraBatch", LobbyCameraBackup = "DeliveryDash.LobbyCameraPrefs";
    public static void ValidateLobbyCameraBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/KotaPaket.unity");
        DeliveryDash.Editor.Tests.CourierPrefsBackup.Begin(LobbyCameraBackup);
        SessionState.SetBool(LobbyCameraFlag, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeLobbyCameraBatch()
    {
        int stage = 0;
        double next = 0;
        Vector3 firstPosition = default;
        float firstSize = 0f;
        EditorApplication.update += () =>
        {
            if (!SessionState.GetBool(LobbyCameraFlag, false) || !EditorApplication.isPlaying) return;
            var game = DeliveryGameManager.Instance;
            Camera camera = Camera.main;
            if (game?.Town == null || camera == null) return;
            double now = EditorApplication.timeSinceStartup;
            if (next == 0) { next = now + 1.5; return; }
            if (now < next) return;
            var hud = game.GetComponent<KidFriendlyHud>();
            switch (stage++)
            {
                case 0:
                    firstPosition = camera.transform.position; firstSize = camera.orthographicSize;
                    SaveCameraShot(camera, "Logs/captures/lobby-awal.png");
                    hud.ResumeGame();
                    // Benar-benar mengemudi (bukan dipindah) supaya kamera mengikuti truk seperti saat bermain.
                    game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
                    game.Car.SetTouchInput(1f, 0.3f);
                    next = now + 2.5;
                    break;
                case 1:
                    game.Car.SetTouchInput(0f, 0f);
                    game.Car.PlaceAt(game.Town.FinishCenter, game.Town.FinishAngle);
                    next = now + 1.0;
                    break;
                case 2:
                    game.AnswerFinishWork(true);
                    next = now + 1.5;
                    break;
                default:
                    SaveCameraShot(camera, "Logs/captures/lobby-sesudah-selesai.png");
                    bool same = Vector2.Distance(camera.transform.position, firstPosition) < 0.05f
                        && Mathf.Abs(camera.orthographicSize - firstSize) < 0.01f;
                    string result = (same ? "PASS" : "FAIL") + $": lobby awal {firstPosition:F2} ukuran {firstSize:F2}; "
                        + $"sesudah selesai kerja {camera.transform.position:F2} ukuran {camera.orthographicSize:F2}";
                    File.WriteAllText("Logs/delivery-lobby-camera.txt", result);
                    SessionState.SetBool(LobbyCameraFlag, false);
                    DeliveryDash.Editor.Tests.CourierPrefsBackup.End(LobbyCameraBackup);
                    EditorApplication.Exit(same ? 0 : 1);
                    break;
            }
        };
    }

    private static void SaveCameraShot(Camera camera, string path)
    {
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = previous;
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
    }

    [MenuItem("Delivery Dash/Uji Sorot Selesai Kerja")]
    public static void ValidateFinishPreview()
    {
        var game=DeliveryGameManager.Instance;
        if(!EditorApplication.isPlaying || game==null || Camera.main==null)
            throw new InvalidOperationException("Jalankan Play dahulu.");
        var camera=Camera.main;
        var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        bool brainEnabled=brain!=null && brain.enabled;
        Vector3 truck=game.Car.transform.position;
        Vector2 target=game.FinishTarget;
        bool sawPreview=false, sawCentered=false;
        double started=EditorApplication.timeSinceStartup;
        game.RequestFinishWork();
        EditorApplication.CallbackFunction poll=null;
        poll=()=>
        {
            string result=null;
            if(!EditorApplication.isPlaying || game==null)result="FAIL: Play interrupted during camera check.";
            else if(game.IsShowingDestination)
            {
                sawPreview=true;
                // Kamera meluncur ke tujuan lalu kembali (gerak kamera-sorot, 2fb7e6f), jadi hanya fase diam di tengah
                // yang harus tepat di tujuan; truk dan waktu beku sepanjang sorotan.
                if(Vector2.Distance(camera.transform.position,target)<=0.05f)sawCentered=true;
                if(Time.timeScale!=0 || Vector3.Distance(game.Car.transform.position,truck)>0.01f)
                    result="FAIL: Preview must freeze truck/timers.";
            }
            else if(sawPreview)
            {
                double elapsed=EditorApplication.timeSinceStartup-started;
                result=!sawCentered?"FAIL: Preview never centered the destination."
                    :elapsed>=1.9 && elapsed<4 && Time.timeScale==1 && !game.MenuOpen
                    && (brain==null || brain.enabled==brainEnabled)
                    && Vector3.Distance(game.Car.transform.position,truck)<0.01f
                    ?"PASS: Destination centered; truck and timers frozen; two-second preview restores follow camera and gameplay."
                    :"FAIL: Camera follow/timing/gameplay not restored after preview.";
            }
            if(result==null && EditorApplication.timeSinceStartup-started>5)result="FAIL: Camera preview timed out.";
            if(result==null)return;
            EditorApplication.update-=poll;
            if(game!=null)game.CancelDestinationPreview();
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/delivery-camera-checks.txt",result);
            if(result.StartsWith("PASS"))Debug.Log(result);else Debug.LogError(result);
        };
        EditorApplication.update+=poll;
    }

    [MenuItem("Delivery Dash/Uji Jalan dan Pengantaran")]
    public static void ValidatePlay()
    {
        DeliveryGameManager game = DeliveryGameManager.Instance;
        if (!EditorApplication.isPlaying || game == null || game.Town == null)
            throw new InvalidOperationException("Tekan Play dan tunggu kota selesai dimuat.");
        StringBuilder report = new StringBuilder();
        int checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            checks++;
        }
        void InteractAfterDwell(DeliveryGameManager manager, GameObject marker)
        {
            manager.AdvanceActiveStop(0.75f);
            manager.TryInteract(marker);
        }
        try
        {
            DeliveryGameManager.IsValidationRunning=true;
            TownV7Checks.ValidateRuntime(game.Town);
            game.GetComponent<KidFriendlyHud>().ResumeGame();
            checks += StopZoneChecks.Run(game);
            var gridFixture=new GameObject("Grid activity test");
            try
            {
                var grid=gridFixture.AddComponent<DeliveryTown>();
                Check(grid.ActivityStopCount==0 && !grid.TryActivityStop(0,out _,out _),"Empty map rejects activity stop safely");
                grid.Pavement.Add(new Rect(-20,7,40,6));
                grid.Houses.Add(new DeliveryTown.Address { Name="Rumah grid",Stop=new Vector2(0,10) });
                grid.Houses.Add(new DeliveryTown.Address { Name="Di luar jalan",Stop=new Vector2(0,20) });
                Check(grid.ActivityStopCount==2,"Grid addresses provide activity candidates without curves");
                Check(grid.TryActivityStop(0,out Vector2 stop,out string street) && stop==new Vector2(0,10)
                    && street=="Rumah grid","Grid activity uses safe address stop and its name");
                Check(!grid.TryActivityStop(1,out _,out _),"Activity rejects address outside pavement");
                Check(!grid.TryActivityStop(-1,out _,out _) && !grid.TryActivityStop(2,out _,out _),"Invalid activity indices rejected");
            }
            finally { UnityEngine.Object.DestroyImmediate(gridFixture); }
            float speed=0;
            speed=Driver.AdvanceSpeed(speed,true,false,6.5f,2.7f,0.02f);
            Check(speed>0 && speed<0.2f,"Gas accelerates gradually");
            speed=6.5f;
            for(int i=0;i<100;i++)speed=Driver.AdvanceSpeed(speed,true,true,6.5f,2.7f,0.02f);
            Check(speed==0,"Gas plus brake stops without reversing");
            speed=6.5f;
            float firstBrake=Driver.AdvanceSpeed(speed,false,true,6.5f,2.7f,0.02f);
            Check(firstBrake>0 && firstBrake<speed,"Brake slows before reverse");
            for(int i=0;i<100;i++)speed=Driver.AdvanceSpeed(speed,false,true,6.5f,2.7f,0.02f);
            Check(Mathf.Abs(speed+2.7f)<0.001f,"Held brake reverses at limited speed");
            Check(Driver.AdvanceSpeed(2,false,false,6.5f,2.7f,0.02f)>0,"Release gas coasts gradually");
            Check(!Driver.IsMeaningfulImpact(1.9f,0.04f,0.04f),"Slow parking contact does not damage fragile parcel");
            Check(!Driver.IsMeaningfulImpact(6.5f,0.02f,0.13f),"Fast glancing scrape is not a damaging impact");
            Check(!Driver.IsMeaningfulImpact(6.5f,0,0.13f),"Driving freely cannot count an impact");
            Check(Driver.IsMeaningfulImpact(6.5f,0.10f,0.13f),"Strong blocked motion counts as impact");
            var wallet=new CourierProgress();
            Check(wallet.NeedsTutorial,"New progress starts with tutorial");
            wallet.TutorialComplete=true;
            Check(!wallet.NeedsTutorial,"Completed tutorial is not repeated");
            wallet.TutorialComplete=false;wallet.CompletedDeliveries=1;
            Check(!wallet.NeedsTutorial,"Existing players keep random sessions");
            var firstHomes=new System.Collections.Generic.HashSet<int>();
            for(int seed=0;seed<40;seed++)firstHomes.Add(new DeliveryDeck(20,seed).Reserve());
            Check(firstHomes.Count>5,"Normal sessions do not force first home");
            Check(!wallet.Select(9),"Cannot buy skin without coins");
            Check(CourierProgress.Prices[0]==0 && CourierProgress.Prices[1]==150 && CourierProgress.Prices[9]==1200,
                "Original is free; paid skins range from 150 to 1200 coins");
            for(int skin=1;skin<10;skin++)
            {
                var purchase=new CourierProgress { Coins=CourierProgress.Prices[skin]-1 };
                Check(!purchase.Select(skin) && !purchase.Owns(skin),"Insufficient coins keep skin locked "+skin);
                purchase.Coins++;
                Check(purchase.Select(skin) && purchase.Coins==0 && purchase.Equipped==skin,
                    "Exact price unlocks and equips skin "+skin);
            }
            wallet.Coins=5700;
            for(int skin=1;skin<10;skin++)Check(wallet.Select(skin),"Direct skin purchase "+skin);
            Check(wallet.Coins==0 && wallet.OwnedMask==1023,"All ten skins cost exactly 5700 coins");
            Check(wallet.Select(1) && wallet.Coins==0,"Owned skin equip is free");
            Check(!wallet.Select(-1) && !wallet.Select(10),"Invalid skins rejected");
            var day=new DateTime(2026,1,1);
            wallet=new CourierProgress();
            for(int i=0;i<7;i++)
            {
                Check(wallet.NextDailyDay(day.AddDays(i))==i+1,"Daily reward ordered");
                Check(wallet.ClaimDaily(day.AddDays(i)),"Daily claim works");
                Check(!wallet.ClaimDaily(day.AddDays(i)),"No duplicate daily claim");
            }
            Check(wallet.Coins==135,"Weekly reward total");
            Check(wallet.NextDailyDay(day.AddDays(7))==1,"Week wraps to first day");
            Check(wallet.NextDailyDay(day.AddDays(9))==1,"Missed day resets weekly sequence");
            Check(!wallet.CanClaimDaily(day),"Clock rollback does not duplicate rewards");
            var roundTrip=JsonUtility.FromJson<CourierProgress>(JsonUtility.ToJson(wallet));
            Check(roundTrip.Coins==135 && roundTrip.DailyDay==7,"Progress serialization");
            Rect view=new Rect(-24,-24,48,48),panel=new Rect(0,0,222,222);
            Check(Vector2.Distance(DeliveryMinimap.Project(Vector2.zero,view,panel),panel.center)<0.01f,"Minimap center mapping");
            Vector2 mapEdge=DeliveryMinimap.Project(new Vector2(999,999),view,panel);
            Check(mapEdge.x<=210 && mapEdge.y>=12,"Minimap icons clamp inside panel");
            foreach(float uiScale in new[]{0.45f,0.73f,1f,1.5f})
            foreach(Vector2 pivot in new[]{new Vector2(909,279),new Vector2(510,333),new Vector2(812,370)})
            for(int heading=0;heading<360;heading+=15)
            {
                Matrix4x4 hudTransform=Matrix4x4.TRS(new Vector3(289,123,0),Quaternion.identity,Vector3.one*uiScale);
                Matrix4x4 icon=DeliveryMinimap.RotateIcon(hudTransform,pivot,-heading);
                Vector3 expected=hudTransform.MultiplyPoint3x4(pivot);
                Check(Vector3.Distance(icon.MultiplyPoint3x4(pivot),expected)<0.001f,
                    "Minimap truck stays at projected world position while rotating/scaling");
                Vector3 actualTip=icon.MultiplyPoint3x4(pivot+Vector2.up*-10)-expected;
                float radians=heading*Mathf.Deg2Rad;
                Vector3 expectedTip=new Vector3(-Mathf.Sin(radians),-Mathf.Cos(radians),0)*10*uiScale;
                Check(Vector3.Distance(actualTip,expectedTip)<0.001f,"Minimap heading matches world truck heading");
            }
            var town = game.Town;
            var car = game.Car;
            Check(town.LayoutVersion >= DeliveryTown.CurrentLayoutVersion, "Current map version saved");
            // Peta v7 punya jalan tanpa rumah (Bundaran Kota, penghubung); cukup pastikan tiap alamat menempel ke jalan yang ada.
            if (town.LayoutVersion >= 7)
                Check(town.Houses.TrueForAll(h => h.RoadIndex >= 0 && h.RoadIndex < town.CurvedRoads.Count), "Every address belongs to a road");
            else
                for (int road = 0; road < town.CurvedRoads.Count; road++)
                {
                    int edge = road;
                    Check(town.Houses.Exists(h => h.RoadIndex == edge), "Every road serves an address: " + town.CurvedRoads[road].Name);
                }
            for(int seed=0;seed<80;seed++)
            {
                int tutorialHouse = town.TutorialHouseIndex();
                Check(tutorialHouse >= 0,"Authored blue shell tutorial house exists");
                var deck=new DeliveryDeck(town.Houses.Count,seed,tutorialHouse);
                Check(deck.Reserve()==tutorialHouse,"Tutorial house first only when requested");
                int last=-1;
                for(int round=0;round<3;round++)
                {
                    var seen=new System.Collections.Generic.HashSet<int>();
                    for(int home=0;home<town.Houses.Count;home++)
                    {
                        int target=deck.Reserve();
                        Check(seen.Add(target),"No repeated home within round");
                        Check(deck.Reserve()==target,"Pickup reservation stable");
                        if(home==0 && round>0)Check(target!=last,"No immediate repeat across rounds");
                        Check(deck.Complete(target),"Successful delivery removes home");
                        Check(!deck.Complete(target),"Duplicate completion ignored");
                        last=target;
                    }
                    Check(deck.Remaining==0,"All homes served before reshuffle");
                }
            }
            Physics2D.SyncTransforms();
            Check(town.CollisionVersion == DeliveryTown.CurrentCollisionVersion, "Fine curb collision saved in scene");
            // Tapak kapsul truk (Driver.FootprintCenter): seberapa jauh badan truk melewati tepi aspal.
            // Dinding bake v7 ada di pita pinggir jalan (SDF 0,2 ± 0,18), jadi badan boleh menutup pita krem dan
            // sedikit pita abu (sampai 0,48; batas tes 0,5), tapi tidak sampai rumput. Peta lama tanpa SDF: tiap lingkaran di aspal.
            bool v7Field = town.LayoutVersion >= 7;
            // Diukur di keliling tapak (24 titik per lingkaran), bukan SDF pusat + jari-jari: di fillet persimpangan
            // nilai SDF bukan jarak persis, selisihnya sampai 0,1.
            float Overhang(Vector2 center, float heading)
            {
                float worst = float.NegativeInfinity;
                for (int c = 0; c < Driver.FootprintCircles; c++)
                {
                    Vector2 circle = Driver.FootprintCenter(center, heading, c);
                    for (int k = 0; k < 24; k++)
                    {
                        float t = k * Mathf.PI / 12f;
                        worst = Mathf.Max(worst, town.PaveDistance(circle + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * Driver.CollisionRadius));
                    }
                }
                return worst;
            }
            bool FootprintOnRoad(Vector2 center, float heading)
            {
                if (v7Field) return Overhang(center, heading) <= 0.5f;
                for (int c = 0; c < Driver.FootprintCircles; c++)
                    if (!town.IsSafe(Driver.FootprintCenter(center, heading, c), Driver.CollisionRadius - 0.04f)) return false;
                return true;
            }
            // At either side of each road segment the truck must be able to use
            // the asphalt, not stop at an unnecessarily inset invisible boundary.
            // Laporan pengguna 26 Sep: truk menabrak padahal masih di aspal hitam. Truk searah jalan, digeser ke samping:
            // harus bergerak lepas (jalan melebar di persimpangan) atau berhenti dengan badan sudah menyentuh pita pinggir.
            foreach (var road in town.CurvedRoads)
            for (int segment = 1; segment < road.Points.Length; segment++)
            {
                Vector2 delta = road.Points[segment] - road.Points[segment - 1];
                if (delta.sqrMagnitude < 0.01f) continue;
                Vector2 middle = (road.Points[segment] + road.Points[segment - 1]) * 0.5f;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
                float along = Vector2.SignedAngle(Vector2.up, delta);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 movement = normal * side;
                    Vector2 edge = car.ResolveMovement(middle, movement * 4f, along);
                    bool free = Vector2.Distance(edge, middle) > 3.99f;
                    Check(Vector2.Dot(edge - middle, movement) >= 3f - Driver.FootprintReach(along, movement) - 0.40f,
                        "Asphalt clearance " + road.Name + "/" + segment + "/" + side);
                    if (v7Field) Check(free || Overhang(edge, along) >= -0.08f,
                        "Side contact reaches road edge band " + road.Name + "/" + segment + "/" + side + " overhang=" + Overhang(edge, along).ToString("F2"));
                }
            }
            town.SetDestination(town.Depot);
            Check(town.IsSafe(town.StartPosition), "Start clear of buildings");
            // Titik berhenti v7 ada di lajur (1,5 unit dari garis tengah); grid rute (jarak aman 1,3)
            // cukup punya sel terjangkau dalam 2 unit, karena TraceRoute menempel ke sel terdekat.
            bool NearReachable(Vector2 point)
            {
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (dx * dx + dy * dy <= 4 && town.IsReachable(point + new Vector2(dx, dy))) return true;
                return false;
            }
            foreach (var house in town.Houses)
                Check(town.IsReachable(house.Stop) || NearReachable(house.Stop), "Road route to " + house.Name);
            foreach (Vector2 point in town.RoadPoints)
            {
                Check(town.IsReachable(point), "Connected street " + point);
                for (int angle = 0; angle < 360; angle += 30)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad), Mathf.Sin(angle*Mathf.Deg2Rad));
                    float sweepHeading = angle - 90f;
                    Vector2 result = car.ResolveMovement(point, direction*25f, sweepHeading);
                    Check(FootprintOnRoad(result, sweepHeading), "Swept curb collision " + point + " / " + angle + " -> " + result.ToString("F5"));
                }
            }
            // v7: gudang di utara pelataran (dok menghadap selatan). Peta lama: gudang di selatan titik uji.
            bool warehouseNorth = town.LayoutVersion >= 7;
            Rect warehouseArea = town.WarehouseArea;
            Vector2 toward = warehouseNorth ? Vector2.up : Vector2.down;
            Vector2 warehouseProbe = warehouseNorth ? new Vector2(warehouseArea.center.x, warehouseArea.yMin - 2.5f) : new Vector2(-5,-8);
            float warehouseFace = warehouseNorth ? warehouseArea.yMin : warehouseArea.yMax;
            float probeHeading = warehouseNorth ? 0f : 180f;
            float WarehouseGap(Vector2 point) => (warehouseFace - point.y) * toward.y;
            // Moncong truk (FootprintReach ke arah gudang) boleh menutup pita pinggir di depan dok, maksimal 0,45.
            float noseReach = Driver.FootprintReach(probeHeading, toward);
            Vector2 hit = car.ResolveMovement(warehouseProbe, toward*20, probeHeading);
            Check(WarehouseGap(hit) > noseReach - 0.45f, "Warehouse blocks entry");
            Vector2 away = car.ResolveMovement(hit, -toward*2, probeHeading);
            Check(Vector2.Dot(away - hit, -toward) > 1.9f, "Can back away from warehouse");
            float rotation = car.transform.eulerAngles.z;
            car.ResolveMovement(new Vector2(0,0), Vector2.right*40);
            Check(Mathf.Abs(Mathf.DeltaAngle(rotation,car.transform.eulerAngles.z))<0.01f, "Collision never forces rotation");
            SimulationMode2D previousMode = Physics2D.simulationMode;
            var hud = game.GetComponent<KidFriendlyHud>();
            hud.enabled = false;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                int impactsBefore = car.ImpactCount;
                car.PlaceAt(warehouseProbe, probeHeading);
                car.SetTouchInput(1, 0);
                for (int frame=0; frame<150; frame++)
                {
                    car.SendMessage("Update");
                    car.SendMessage("FixedUpdate");
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Vector2 wall = car.GetComponent<Rigidbody2D>().position;
                Check(WarehouseGap(wall) > noseReach - 0.5f, "Held throttle cannot penetrate warehouse");
                Check(car.ImpactCount == impactsBefore + 1, "Sustained throttle against wall counts one meaningful impact");
                car.SetTouchInput(-1, 0);
                for (int frame=0; frame<40; frame++)
                {
                    car.SendMessage("Update");
                    car.SendMessage("FixedUpdate");
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Check(Vector2.Dot(car.GetComponent<Rigidbody2D>().position - wall, -toward) > 1f, "Reverse escapes after sustained collision");
                car.SetTouchInput(0, 1);
                float before = car.GetComponent<Rigidbody2D>().rotation;
                for (int frame=0; frame<20; frame++)
                {
                    car.SendMessage("Update");
                    car.SendMessage("FixedUpdate");
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
                Check(Mathf.Abs(Mathf.DeltaAngle(before,car.GetComponent<Rigidbody2D>().rotation)) > 30, "Can steer while stopped");
                // Exercise actual rigidbody steps at curbs and corners, not just standalone casts.
                for (int pointIndex = 0; pointIndex < town.RoadPoints.Count; pointIndex += 7)
                {
                    for (int heading = 0; heading < 360; heading += 45)
                    {
                        car.PlaceAt(town.RoadPoints[pointIndex], heading);
                        car.SetTouchInput(1, 0);
                        for (int frame = 0; frame < 160; frame++)
                        {
                            car.SendMessage("Update");
                            car.SendMessage("FixedUpdate");
                            Physics2D.Simulate(Time.fixedDeltaTime);
                        }
                        Vector2 contact = car.GetComponent<Rigidbody2D>().position;
                        Check(FootprintOnRoad(contact, car.GetComponent<Rigidbody2D>().rotation), "Held curb collision " + pointIndex + "/" + heading);
                        car.SetTouchInput(-1, 0);
                        // Allow braking from full forward speed, then gradual reverse acceleration.
                        // The old 0.7 s window assumed an instantaneous direction change.
                        for (int frame = 0; frame < 80; frame++)
                        {
                            car.SendMessage("Update");
                            car.SendMessage("FixedUpdate");
                            Physics2D.Simulate(Time.fixedDeltaTime);
                        }
                        Check(Vector2.Distance(contact, car.GetComponent<Rigidbody2D>().position) > 0.5f,
                            "Reverse from curb " + pointIndex + "/" + heading + " start=" + town.RoadPoints[pointIndex]
                            + " contact=" + contact.ToString("F5") + " after=" + car.GetComponent<Rigidbody2D>().position.ToString("F5"));
                    }
                }
            }
            finally { Physics2D.simulationMode = previousMode; hud.enabled = true; }
            // Exercise the real first-delivery flow without touching the player's saved progress.
            bool savedTutorial = game.Progress.TutorialComplete;
            int savedDeliveries = game.Progress.CompletedDeliveries;
            int savedCoins = game.Progress.Coins;
            try
            {
                game.Progress.TutorialComplete=false;
                game.Progress.CompletedDeliveries=0;
                game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
                // Tutorial interaktif (27 Sep): kendali → gudang → bintang/kilat/singgah → rumah → surat warga → kapsul → lobby.
                var guideUpdate=typeof(DeliveryGameManager).GetMethod("UpdateTutorialGuide",
                    System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                void Guide()=>guideUpdate.Invoke(game,null);
                Check(game.Guide==DeliveryGameManager.GuideStep.Drive && game.FirstTutorial,"New player starts guided tutorial at the controls");
                car.PlaceAt(town.Depot);Guide();
                Check(game.Guide==DeliveryGameManager.GuideStep.StopAtDepot,"Guide asks to stop inside the parcel zone");
                InteractAfterDwell(game, game.ActiveMarker.gameObject);
                Check(game.TutorialTrip && game.Carrying,"New player enters actual tutorial trip");
                Check(Vector2.Distance(game.TargetPosition,town.Houses[town.TutorialHouseIndex()].Stop)<0.01f,
                    "Tutorial targets authored left blue shell house");
                Check(System.Linq.Enumerable.Any(game.Activities.Pickups,p=>p.Kind==DeliveryMarkerKind.Star)
                    && System.Linq.Enumerable.Any(game.Activities.Pickups,p=>p.Kind==DeliveryMarkerKind.Boost) && game.Activities.HasStopover,
                    "Tutorial route teaches a star, a boost and a stopover");
                Check(!game.Activities.IsExpress && !game.Activities.IsFragile,"Tutorial has no express or fragile contract");
                DeliveryMarker firstStar=System.Linq.Enumerable.First(game.Activities.Pickups,p=>p.Kind==DeliveryMarkerKind.Star);
                car.PlaceAt(firstStar.transform.position);Guide();
                Check(game.Guide==DeliveryGameManager.GuideStep.Star && game.GuideWorldTarget.HasValue,"Guide spotlights a nearby star");
                car.PlaceAt(game.TargetPosition);InteractAfterDwell(game, game.ActiveMarker.gameObject);
                Check(!game.Carrying && game.Deliveries==1 && game.Stars==12,"Tutorial delivers for exactly twelve coins");
                Check(game.Progress.TutorialComplete,"Tutorial counts as complete once its parcel arrives");
                Check(game.Guide==DeliveryGameManager.GuideStep.HelpZone && game.SideQuests.Quests.Count==1,
                    "Tutorial continues with a letter side quest");
                game.TryInteract(game.ActiveMarker.gameObject);
                Check(!game.Carrying,"Next parcel waits until the tutorial ends");
                car.PlaceAt(game.SideQuests.Quests[0].Position);game.SideQuests.Tick(2.5f);Guide();
                Check(game.SideQuests.CarriedLetters==1 && game.Guide==DeliveryGameManager.GuideStep.Mailbox,
                    "Letter collected, guide points to the mailbox");
                car.PlaceAt(game.SideQuests.DropoffPosition);game.SideQuests.Tick(2.5f);Guide();
                Check(game.SideQuests.CarriedLetters==0 && game.Guide==DeliveryGameManager.GuideStep.ToFinish && game.FinishRequested,
                    "After the letter the first tutorial heads to the capsule");
                car.PlaceAt(town.FinishCenter);game.AdvanceParking(1f);
                Check(game.Guide==DeliveryGameManager.GuideStep.None && !game.FinishRequested && !game.TutorialActive,
                    "Stopping at the capsule ends the first tutorial");
                car.PlaceAt(town.Depot);InteractAfterDwell(game, game.ActiveMarker.gameObject);
                Check(!game.TutorialTrip && game.Activities.Pickups.Count>0,"Second trip enables normal gameplay");
                Check(game.Progress.Coins==savedCoins,"Validation never changes player wallet");
                // Selesai kerja di tengah tutorial: kotak paket hilang, jadi panduan harus berhenti (uji HP 27 Sep).
                game.Progress.TutorialComplete=false;
                game.Progress.CompletedDeliveries=0;
                game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
                Check(game.GuideActive,"Fresh player run starts the guide again");
                game.RequestFinishWork();
                Check(!game.GuideActive && game.FinishRequested,"Finishing work mid-tutorial stops the guide");
            }
            finally
            {
                game.Progress.TutorialComplete=savedTutorial;
                game.Progress.CompletedDeliveries=savedDeliveries;
            }
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
            for (int i=0;i<town.Houses.Count;i++)
            {
                Check(Vector2.Distance(game.TargetPosition,town.Depot)<0.01f, "Same depot each trip");
                car.PlaceAt(town.Depot);
                var pickup = game.ActiveMarker.gameObject;
                InteractAfterDwell(game, pickup);
                Check(game.Carrying, "Parcel collected");
                foreach(var aid in game.Activities.Pickups)
                    Check(aid.Kind!=DeliveryMarkerKind.Shield || game.Activities.IsFragile,"Shields only spawn for fragile contracts");
                if(game.Activities.IsFragile)
                {
                    Check(game.Activities.FragileStatus.Contains("0/3"),"Fragile HUD starts with explicit hit counter");
                    // Inject recorded impacts only in the editor test; restore before real collection.
                    var impactSetter=typeof(Driver).GetProperty(nameof(Driver.ImpactCount)).GetSetMethod(true);
                    int before=car.ImpactCount;
                    try
                    {
                        for(int hits=1;hits<=4;hits++)
                        {
                            impactSetter.Invoke(car,new object[]{before+hits});
                            Check(game.Activities.FragileHits==Mathf.Min(hits,3),"Fragile counter clamps at three hits");
                            Check(game.Activities.FragileStatus.Contains(hits>=3?"Bonus habis":"Jangan menabrak"),
                                "Fragile HUD explains active or exhausted bonus");
                        }
                        // Pelindung menyerap benturan saat hitungan dibaca, bukan menunggu Update aktivitas (bug 26 Sep:
                        // segmen rapuh menyala sekejap lalu padam).
                        System.Reflection.FieldInfo Field(string name)=>typeof(DeliveryActivities).GetField(name,
                            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                        impactSetter.Invoke(car,new object[]{before});
                        Field("observedImpacts").SetValue(game.Activities,before);
                        Field("protectedImpacts").SetValue(game.Activities,0);
                        Field("shields").SetValue(game.Activities,1);
                        impactSetter.Invoke(car,new object[]{before+1});
                        Check(game.Activities.FragileHits==0 && !game.Activities.HasShield,"Shield absorbs a hit before the HUD can read it");
                        Field("protectedImpacts").SetValue(game.Activities,0);
                        Field("observedImpacts").SetValue(game.Activities,before);
                    }
                    finally { impactSetter.Invoke(car,new object[]{before}); }
                }
                var route=new System.Collections.Generic.List<Vector2>();
                town.TraceRoute(town.Depot,game.TargetPosition,route);
                Check(route.Count>1 && Vector2.Distance(route[route.Count-1],game.TargetPosition)<0.01f,"Minimap route reaches exact goal");
                bool assisted=false;
                foreach(var reward in game.Activities.Pickups)
                    if(reward.Kind==DeliveryMarkerKind.Boost)
                        foreach(var node in route)if(Vector2.Distance(node,reward.transform.position)<1.5f){assisted=true;break;}
                Check(game.TutorialTrip ? game.Activities.Pickups.Count==0 : assisted,"Tutorial is plain; other routes have assistance");
                int routeStars=0;
                foreach(var reward in game.Activities.Pickups)if(reward.Kind==DeliveryMarkerKind.Star)routeStars++;
                Check(routeStars==(game.TutorialTrip?0:3),"Three stars on every normal delivery route");
                game.TryInteract(pickup);
                Check(game.Deliveries==i, "Duplicate pickup ignored");
                foreach (var reward in new System.Collections.Generic.List<DeliveryMarker>(game.Activities.Pickups))
                {
                    Check(town.IsSafe(reward.transform.position, 1.5f), "Bonus on safe road");
                    car.PlaceAt(reward.transform.position);
                    if(reward.Kind==DeliveryMarkerKind.Stamp)
                    {
                        Check(!game.Activities.Collect(reward), "Stopover cannot be collected without parking");
                        game.Activities.AdvanceStopover(1.6f);
                        Check(!game.Activities.HasStopover,"Stopover completes after parking");
                        Check(!game.Activities.Collect(reward),"Duplicate stamp ignored");
                        continue;
                    }
                    Check(game.Activities.Collect(reward), "Bonus collected once");
                    Check(!game.Activities.Collect(reward), "Duplicate bonus ignored");
                    if (reward.Kind == DeliveryMarkerKind.Boost)
                    {
                        Check(car.IsBoosted, "Boost activated");
                        car.ClearBoost();
                        Check(!car.IsBoosted, "Boost can reset cleanly");
                    }
                }
                car.PlaceAt(game.TargetPosition);
                var delivery = game.ActiveMarker.gameObject;
                InteractAfterDwell(game, delivery);
                game.TryInteract(delivery);
                Check(!game.Carrying && game.Deliveries==i+1, "Deliver exactly once");
                Check(game.Activities.Pickups.Count == 0, "Previous trip bonuses cleared");
            }
            game.SideQuests.ResetQuests();
            Check(game.SideQuests.Quests.Count==0,"No forced quests at session start");
            Check(game.SideQuests.TryOffer(DeliverySideQuests.Kind.Letter),"Offer first letter");
            Check(game.SideQuests.TryOffer(DeliverySideQuests.Kind.Letter),"Offer second letter");
            Check(game.SideQuests.TryOffer(DeliverySideQuests.Kind.Rest),"Offer third optional quest");
            Check(!game.SideQuests.TryOffer(DeliverySideQuests.Kind.Letter),"Three-quest cap enforced");
            var letter=game.SideQuests.Quests[0];
            car.PlaceAt(letter.Position);game.SideQuests.Tick(2.1f);
            Check(letter.Collected && !letter.Completed,"Letter quest collects at stop");
            var secondLetter=game.SideQuests.Quests[1];
            car.PlaceAt(secondLetter.Position);game.SideQuests.Tick(2.1f);
            Check(secondLetter.Collected && !secondLetter.Completed,"Two letters can be carried together");
            car.PlaceAt(town.Depot);game.SideQuests.Tick(2.1f);
            Check(!letter.Completed && !secondLetter.Completed,"Parcel pickup is not letter dropoff");
            car.PlaceAt(game.SideQuests.DropoffPosition);game.SideQuests.Tick(1);
            Check(letter.Dwell>0 && letter.Dwell<2,"Dropoff progress fills gradually");
            car.PlaceAt(town.StartPosition);game.SideQuests.Tick(0.1f);
            Check(letter.Dwell>0 && letter.Dwell<1,"Leaving decays progress without instant reset");
            car.PlaceAt(game.SideQuests.DropoffPosition);game.SideQuests.Tick(2.1f);
            Check(letter.Completed && secondLetter.Completed,"Both letters delivered at separate dropoff");
            int coinsBefore=game.Stars;game.SideQuests.Tick(3);
            Check(game.Stars==coinsBefore,"Side quest cannot pay twice");
            var rest=game.SideQuests.Quests[2];car.PlaceAt(rest.Position);game.SideQuests.Tick(2.1f);
            Check(rest.Completed && car.IsBoosted,"Rest stop pays and boosts");
            Check(game.SideQuests.TryOffer(DeliverySideQuests.Kind.Letter),"Completed quests free capacity");
            var expiring=game.SideQuests.Quests[game.SideQuests.Quests.Count-1];
            expiring.OfferedAt=game.Deliveries-3;game.SideQuests.OnDeliveryCompleted(1);
            Check(expiring.Completed,"Unaccepted quest expires after three deliveries");
            Check(game.SideQuests.TryOffer(DeliverySideQuests.Kind.Letter),"New quest after expiry");
            var accepted=game.SideQuests.Quests[game.SideQuests.Quests.Count-1];
            car.PlaceAt(accepted.Position);game.SideQuests.Tick(2.1f);
            accepted.OfferedAt=game.Deliveries-10;game.SideQuests.OnDeliveryCompleted(1);
            Check(accepted.Collected && !accepted.Completed,"Accepted letter never expires");
            for(int trip=0;trip<4;trip++)
            {
                car.PlaceAt(game.TargetPosition);InteractAfterDwell(game, game.ActiveMarker.gameObject);
                Check(game.Carrying,"Main package can be collected while carrying side quest");
                car.PlaceAt(game.TargetPosition);InteractAfterDwell(game, game.ActiveMarker.gameObject);
                Check(!game.Carrying && accepted.Collected && !accepted.Completed && game.SideQuests.Quests.Contains(accepted),
                    "Accepted side quest survives real main delivery without replacement");
                Check(game.SideQuests.ActiveCount<=3,"Random offers respect total three-slot limit");
            }
            car.PlaceAt(game.TargetPosition);InteractAfterDwell(game, game.ActiveMarker.gameObject);
            game.RequestFinishWork();
            Check(game.FinishRequested && game.Carrying && game.NavigationTarget==game.TargetPosition,
                "Finish request directs carried package to its house first");
            car.PlaceAt(town.FinishParkingArea.center);game.AdvanceParking(1);
            Check(!game.MenuOpen && game.Carrying,"Parking cannot end work with main package");
            car.PlaceAt(game.TargetPosition);InteractAfterDwell(game, game.ActiveMarker.gameObject);
            Check(!game.Carrying && game.HasPendingDelivery && game.NavigationTarget==game.SideQuests.DropoffPosition,
                "After house delivery finish route points to carried letters");
            car.PlaceAt(town.Depot);InteractAfterDwell(game, game.ActiveMarker.gameObject);
            Check(!game.Carrying,"No automatic new parcel while finishing work");
            game.AnswerFinishWork(true);
            Check(!game.MenuOpen,"Accepted letters prevent returning to lobby");
            car.PlaceAt(game.SideQuests.DropoffPosition);game.SideQuests.Tick(2.1f);
            Check(!game.HasPendingDelivery && game.NavigationTarget==town.FinishParkingArea.center,
                "Clean hands route to parking, unaccepted offers do not block finish");
            game.AnswerFinishWork(true);
            Check(!game.MenuOpen,"Cannot finish away from parking even with no deliveries");
            game.SideQuests.ResetQuests();
            Check(KidFriendlyHud.SelectDrivingHint("","","rapuh",true)=="","Fragile hint is not drawn twice");
            Check(KidFriendlyHud.SelectDrivingHint("Paket sampai","Bawa surat","rapuh",true)=="Paket sampai",
                "Only one notification occupies driving hint bar");
            Check(KidFriendlyHud.SelectDrivingHint("","Bawa surat","rapuh",true)=="Bawa surat",
                "Side quest hint remains available alongside compact fragile counter");
            var hudMenu=game.GetComponent<KidFriendlyHud>();hudMenu.OpenLobby();
            hudMenu.RequestExit();hudMenu.CancelExit();
            Check(game.MenuOpen && Time.timeScale==0,"Cancel exit returns to lobby without resuming");
            Check(Time.timeScale==0 && game.MenuOpen,"Lobby pauses simulation");
            hudMenu.ResumeGame();Check(Time.timeScale==1 && !game.MenuOpen,"Resume restores simulation");
            hudMenu.RequestExit();
            Check(!game.MenuOpen && Time.timeScale==1,"In-game exit request is unavailable");
            hudMenu.SendMessage("OnApplicationPause",true);
            Check(game.MenuOpen && Time.timeScale==0,"Phone interruption pauses game and event timers");
            hudMenu.SendMessage("OnApplicationPause",false);
            Check(game.MenuOpen && Time.timeScale==0,"Phone resume waits for player to continue");
            hudMenu.ResumeGame();
            hudMenu.SendMessage("OnApplicationFocus",false);
            Check(game.MenuOpen && Time.timeScale==0,"Lost focus clears controls and pauses");
            hudMenu.ResumeGame();
            bool wasMapVisible=game.Minimap.Visible;
            if(!wasMapVisible)game.Minimap.Toggle();
            game.Minimap.SetExpanded(true);
            Check(game.Minimap.Expanded,"Visible map can expand");
            game.Minimap.Toggle();game.Minimap.SetExpanded(true);
            Check(!game.Minimap.Visible && !game.Minimap.Expanded,"Hidden map cannot remain expanded");
            if(wasMapVisible)game.Minimap.Toggle();
            int listeners=0;
            foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if(listener.isActiveAndEnabled)listeners++;
            Check(listeners==1,"Exactly one active audio listener");
            var audioSources=game.GetComponents<AudioSource>();
            Check(audioSources.Length==3,"Separate engine, sound-effect, and music sources");
            var bank=UnityEngine.Object.FindFirstObjectByType<DeliveryMusicBank>(FindObjectsInactive.Include);
            Check(game.GetComponent<DeliveryMusic>()!=null&&bank?.music?.lobby!=null&&bank.music.play!=null&&bank.music.play.Length==3,
                "2.0 music: lobby track and three driving tracks from the scene bank");
            Check(System.Array.Exists(audioSources,source=>source.loop && source.clip!=null && source.clip.samples>0),
                "Engine has a generated nonempty audio clip");
            foreach (Rect bay in new[] { town.FinishParkingArea })
            {
                car.PlaceAt(town.StartPosition);game.AdvanceParking(1);
                Check(!game.MenuOpen,"Normal forecourt does not end work");
                car.PlaceAt(bay.center);game.AdvanceParking(0.4f);
                Check(!game.MenuOpen,"Parking requires brief stop");
                game.AdvanceParking(0.4f);
                Check(game.MenuOpen && Time.timeScale==0,"Parking offers finish confirmation");
                Vector2 parked=car.transform.position;
                game.AnswerFinishWork(false);
                Check(!game.MenuOpen && Vector2.Distance(car.transform.position,parked)<0.01f,"No keeps the truck where it parked (no teleport)");
                game.AdvanceParking(1);
                Check(!game.MenuOpen,"No does not ask again until the truck leaves the bay");
                car.PlaceAt(town.StartPosition);game.AdvanceParking(1);
                car.PlaceAt(bay.center);game.AdvanceParking(1);game.AnswerFinishWork(true);
                Check(game.MenuOpen && Time.timeScale==0,"Yes returns to paused lobby");
                hudMenu.ResumeGame();car.PlaceAt(town.StartPosition);game.AdvanceParking(1);
            }
            report.AppendLine("PASS: " + checks + " checks; " + town.Houses.Count + " homes; " + town.BarrierCount + " barriers.");
            report.AppendLine("Routes connected; swept curb/warehouse collision; reverse escape; no forced rotation; full depot delivery cycle; 80 seeds over three shuffled rounds.");
            Debug.Log("DELIVERY_PLAY_CHECKS_OK: " + report);
        }
        catch (Exception error)
        {
            report.AppendLine(error.ToString());
            Debug.LogException(error);
        }
        finally
        {
            DeliveryGameManager.IsValidationRunning=false;
            game.ResetRun(); game.Car.PlaceAt(game.Town.StartPosition, game.Town.StartHeading);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/delivery-play-checks.txt", report.ToString());
        }
    }
}
