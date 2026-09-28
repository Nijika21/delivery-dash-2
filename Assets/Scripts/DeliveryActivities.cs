using System.Collections.Generic;
using UnityEngine;

// Optional rewards only: never close a road, alter steering, or take away a parcel.
public class DeliveryActivities : MonoBehaviour
{
    private readonly List<DeliveryMarker> pickups = new List<DeliveryMarker>();
    private DeliveryGameManager game;
    private DeliveryContentCatalog catalog;
    private int collectedStars;
    private int activity;
    private int starGoal;
    private bool fountainVisited;
    private int startingImpacts;
    private int observedImpacts, protectedImpacts, shields;
    private float deadline;
    private bool expressExpiredShown, fragileSpentShown;
    private float stampDwell;
    private DeliveryMarker stamp;
    private string stampStreet;
    private bool tutorial;
    public bool GuideToBonus { get; private set; }
    public bool HasStopover => stamp != null;
    public string StopoverStreet => stampStreet ?? string.Empty;
    public float StopoverProgress => stampDwell/1.5f;
    public Vector2 BonusPosition => stamp == null ? game.TargetPosition : (Vector2)stamp.transform.position;
    public bool LastContractWon { get; private set; }
    public string LastResult { get; private set; }
    public IReadOnlyList<DeliveryMarker> Pickups => pickups;
    public string Hint { get; private set; }
    public bool IsExpress => !tutorial && activity==1;
    public bool ExpressRunning => IsExpress && Time.time<=deadline;
    public int ExpressSecondsLeft => Mathf.Max(0,Mathf.CeilToInt(deadline-Time.time));
    public int StarsCollected => collectedStars;
    public int StarGoal => starGoal;
    // Benturan tercatat di FixedUpdate truk, sebelum Update aktivitas sempat menyerapnya ke pelindung. Hitung serapan
    // setiap kali dibaca supaya HUD tidak sempat melihat benturan yang sebenarnya tertahan pelindung (bug 26 Sep:
    // segmen rapuh menyala sekejap lalu padam lagi).
    public bool HasShield { get { RefreshProtection(); return shields>0; } }
    public bool IsFragile => !tutorial && activity==2;
    public const int FragileLimit=3;
    public int FragileHits
    {
        get { RefreshProtection(); return Mathf.Clamp(game.Car.ImpactCount-startingImpacts-protectedImpacts,0,FragileLimit); }
    }
    public string FragileSummary => $"Rapuh {FragileHits}/{FragileLimit} · "
        +(FragileHits>=FragileLimit?"Bonus habis, tetap antar paket.":"Jangan menabrak! Bonus +5.")
        +(shields>0?" Pelindung aktif.":"");
    public string FragileStatus => $"Paket rapuh · Benturan {FragileHits}/{FragileLimit}\n"
        +(FragileHits>=FragileLimit?"Bonus habis. Paket tetap boleh diantar."
        :"Jangan menabrak! Bonus +5 jika kurang dari 3 benturan.")
        +(shields>0?" Pelindung aktif: 1 benturan.":"");

    public void Configure(DeliveryGameManager manager, DeliveryContentCatalog content)
    {
        game = manager;
        catalog = content;
    }

    public void BeginTrip(bool isTutorial = false)
    {
        Clear();
        tutorial = isTutorial;
        if (tutorial)
        {
            activity = 0;
            SpawnTutorialAids();
            return;
        }
        // A normal trip is useful too; optional contracts are not forced every time.
        activity = Random.value<0.75f ? Random.Range(1,4) : 0;
        startingImpacts=game.Car.ImpactCount;
        observedImpacts=startingImpacts;
        float routeLength=0;
        var candidates = new List<Vector2>();
        // Place optional rewards along the actual route rather than across grass.
        var route=new List<Vector2>();
        game.Town.TraceRoute(game.Town.Depot,game.TargetPosition,route);
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 at=route[i];
            routeLength+=Vector2.Distance(route[i-1],at);
            if (Vector2.Distance(at,game.Town.Depot)>6 && game.Town.IsSafe(at,1.5f)
                && !candidates.Exists(p => Vector2.Distance(p,at)<1.8f)) candidates.Add(at);
        }
        deadline=Time.time+Mathf.Max(35,routeLength/4.5f+20);
        starGoal=Mathf.Min(3,candidates.Count);
        for (int i = 0; i < starGoal; i++)
            Spawn(DeliveryMarkerKind.Star,candidates[(i+1)*candidates.Count/4],catalog.starSprite);
        if (candidates.Count > 0) Spawn(DeliveryMarkerKind.Boost,candidates[candidates.Count/2],catalog.boostSprite);
        else Spawn(DeliveryMarkerKind.Boost,game.Town.Depot,catalog.boostSprite);
        if(IsFragile && candidates.Count>2)Spawn(DeliveryMarkerKind.Shield,candidates[candidates.Count/3],catalog.packageSprite);
        // A few optional boosts on other streets; route assistance above is guaranteed.
        int stopCount=game.Town.ActivityStopCount;
        for(int i=0;i<3 && stopCount>0;i++)
        {
            if(game.Town.TryActivityStop((game.Deliveries*5+i*7+3)%stopCount,out Vector2 extra,out _)
                && !pickups.Exists(p=>Vector2.Distance(p.transform.position,extra)<3))
                Spawn(DeliveryMarkerKind.Boost,extra,catalog.boostSprite);
        }
        if(activity==3)
        {
            int first=stopCount>2?2:0;
            int start=stopCount>0?Random.Range(first,stopCount):0;
            for(int i=0;i<stopCount;i++)
            {
                if(!game.Town.TryActivityStop((start+i)%stopCount,out Vector2 atStop,out string street))continue;
                int before=pickups.Count;
                Spawn(DeliveryMarkerKind.Stamp,atStop,catalog.customerSprite);
                if(pickups.Count>before){stamp=pickups[pickups.Count-1];stampStreet=street;}
                break;
            }
            // Missing/unsafe stops must not leave an impossible bonus objective.
            if(stamp==null)activity=0;
        }
        RefreshHint();
    }

    // Tutorial (permintaan pengguna 27 Sep: bintang dan kilat jangan setengah-setengah): bintang, kilat, dan kotak singgah
    // diletakkan sungguhan di rute rumah tutorial, berurutan bintang → kilat → singgah → bintang, dan HUD menyorot
    // masing-masing saat truk mendekat. Ekspres dan rapuh dikenalkan saat pertama kali muncul di permainan biasa.
    private void SpawnTutorialAids()
    {
        var route = new List<Vector2>();
        game.Town.TraceRoute(game.Town.Depot, game.TargetPosition, route);
        var candidates = new List<Vector2>();
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 at = route[i];
            if (Vector2.Distance(at, game.Town.Depot) > 6 && Vector2.Distance(at, game.TargetPosition) > 5 && game.Town.IsSafe(at, 1.5f)
                && !candidates.Exists(p => Vector2.Distance(p, at) < 3f)) candidates.Add(at);
        }
        int count = candidates.Count;
        if (count == 0) return;
        Vector2 At(float fraction) => candidates[Mathf.Clamp(Mathf.RoundToInt(fraction * (count - 1)), 0, count - 1)];
        // Rute pendek (sedikit titik): yang pertama dilepas bintang kedua, lalu singgah, lalu kilat.
        float[] plan = count >= 4 ? new[] { 0f, 0.35f, 0.65f, 1f } : count == 3 ? new[] { 0f, 0.5f, 1f }
            : count == 2 ? new[] { 0f, 1f } : new[] { 0f };
        Spawn(DeliveryMarkerKind.Star, At(plan[0]), catalog.starSprite);
        starGoal = 1;
        if (plan.Length >= 2) Spawn(DeliveryMarkerKind.Boost, At(plan[1]), catalog.boostSprite);
        if (plan.Length >= 3) SpawnTutorialStopover(At(plan[2]));
        if (plan.Length >= 4) { Spawn(DeliveryMarkerKind.Star, At(plan[3]), catalog.starSprite); starGoal = 2; }
    }

    // Kotak singgah tutorial: pemberhentian kota terdekat kalau dekat rute, selain itu langsung di titik rute.
    private void SpawnTutorialStopover(Vector2 onRoute)
    {
        Vector2 position = onRoute;
        string street = string.Empty;
        float best = 4f;
        for (int i = 0; i < game.Town.ActivityStopCount; i++)
        {
            if (!game.Town.TryActivityStop(i, out Vector2 atStop, out string name)) continue;
            float distance = Vector2.Distance(atStop, onRoute);
            if (distance < best) { best = distance; position = atStop; street = name; }
        }
        int before = pickups.Count;
        Spawn(DeliveryMarkerKind.Stamp, position, catalog.customerSprite);
        if (pickups.Count == before) return;
        stamp = pickups[pickups.Count - 1];
        stampStreet = street;
        activity = 3;
        GuideToBonus = true;
    }

    public void Clear()
    {
        foreach (var pickup in pickups) if(pickup!=null) Destroy(pickup.gameObject);
        pickups.Clear();
        collectedStars=0;
        shields=0;protectedImpacts=0;
        activity=0;
        fountainVisited=false;
        Hint=string.Empty;
        stamp=null;stampDwell=0;GuideToBonus=false;
        expressExpiredShown=false;fragileSpentShown=false;
    }

    private void Spawn(DeliveryMarkerKind kind, Vector2 position, Sprite icon)
    {
        // Titik logika di sini; gambarnya pickup stiker (bintang/kilat/perisai) atau zona singgah hijau.
        GameObject root=new GameObject(kind==DeliveryMarkerKind.Star?"Bintang bonus":kind==DeliveryMarkerKind.Shield?"Pelindung paket"
            :kind==DeliveryMarkerKind.Stamp?"Singgah":"Kilat");
        root.transform.SetParent(transform,false);
        root.transform.position=position;
        var marker=root.AddComponent<DeliveryMarker>();
        marker.Configure(kind);
        if(kind==DeliveryMarkerKind.Stamp)
        {
            marker.ZoneKind="singgah";
            marker.ZoneView=StopZoneView.Create(game.Town,"singgah",position,0f,null);
        }
        else
        {
            string sticker=kind==DeliveryMarkerKind.Star?"bintang":kind==DeliveryMarkerKind.Shield?"perisai":"kilat";
            marker.PickupView=PickupView.Create(game.Town,sticker,position,game.Town.transform);
            // Cadangan kalau sprite dunia belum diimpor: gambar lama tetap terlihat.
            if(marker.PickupView==null && icon!=null)
            {
                var picture=game.Town.Asset(root.name,icon,position,1.65f);
                picture.sortingOrder=12;
                picture.transform.SetParent(root.transform,true);
            }
        }
        pickups.Add(marker);
    }

    private void Update()
    {
        if(game==null || game.Car==null || !game.Carrying || Time.timeScale==0) return;
        for(int i=pickups.Count-1;i>=0;i--)
        {
            var marker=pickups[i];
            if(marker==null){pickups.RemoveAt(i);continue;}
            if(marker.Kind==DeliveryMarkerKind.Stamp)continue;
            if(Vector2.Distance(game.Car.transform.position,marker.transform.position)<1.65f) Collect(marker);
        }
        if(activity==3 && stamp!=null)
            AdvanceStopover(Time.deltaTime);
        // Usulan ekspres.habis (disetujui Gerbang Desain 5): chip ekspres hilang dan pesan ini tampil sekali.
        if(IsExpress && !expressExpiredShown && Time.time>deadline)
        {
            expressExpiredShown=true;
            game.AwardBonus(0,"Waktu bonus habis. Tetap antar paketnya, ya!");
        }
        if(IsFragile && !fragileSpentShown && FragileHits>=FragileLimit)
        {
            fragileSpentShown=true;
            game.AwardBonus(0,"Bonus habis. Paket tetap boleh diantar.");
        }
        RefreshHint();
    }

    public void AdvanceStopover(float seconds)
    {
        if(stamp==null || !game.Carrying)return;
        bool parked=Vector2.Distance(game.Car.transform.position,stamp.transform.position)<2 && game.Car.CurrentSpeed<0.3f;
        stampDwell=Mathf.MoveTowards(stampDwell,parked?1.5f:0,Mathf.Max(0,seconds)*(parked?1:2.5f));
        if(stamp.ZoneView!=null)stamp.ZoneView.SetProgress(stampDwell/1.5f);
        if(stampDwell>=1.5f)Collect(stamp);
    }

    public bool Collect(DeliveryMarker marker)
    {
        if(!game.Carrying || marker==null || !pickups.Contains(marker)
            || (marker.Kind==DeliveryMarkerKind.Stamp && stampDwell<1.5f)
            || Vector2.Distance(game.Car.transform.position,marker.transform.position)>2.4f || !marker.CollectOnce())return false;
        pickups.Remove(marker);
        marker.PlayCollected();
        RefreshProtection();
        if(marker.Kind==DeliveryMarkerKind.Stamp)
        {
            fountainVisited=true;
            stamp=null;GuideToBonus=false;
            game.AwardBonus(0,"Sudah berhenti! Sekarang antar paket ke rumah.");
        }
        else if(marker.Kind==DeliveryMarkerKind.Boost)
        {
            game.Car.StartBoost(4);
            DeliveryAudio.Play(DeliveryAudio.Cue.Boost);
            game.AwardBonus(0,"Lebih cepat selama 4 detik!");
        }
        else if(marker.Kind==DeliveryMarkerKind.Shield)
        {
            shields=1;
            game.AwardBonus(0,"Pelindung paket! Satu benturan berikutnya tidak mengurangi bonus.");
            DeliveryAudio.Play(DeliveryAudio.Cue.Pickup);
        }
        else
        {
            collectedStars++;
            game.CountStarPickup();
            game.AwardBonus(1,"Dapat satu bintang!");
            if(collectedStars==starGoal)game.AwardBonus(2,"Semua bintang terkumpul! Bonus +2 koin");
        }
        Destroy(marker.gameObject);
        RefreshHint();
        return true;
    }

    public int CompleteTrip()
    {
        RefreshProtection();
        LastContractWon=activity==1?Time.time<=deadline:activity==2?FragileHits<FragileLimit
            :activity==3 && fountainVisited;
        int bonus=LastContractWon?5:0;
        LastResult=activity==0?"Paket sampai! +12 koin.":LastContractWon?"Paket sampai! +12 koin dan bonus +5 koin."
            :"Paket sampai! +12 koin. Bonusnya bisa dicoba lagi nanti.";
        Clear();
        return bonus;
    }

    public void ToggleBonusGuide(){if(stamp!=null)GuideToBonus=!GuideToBonus;}

    private void RefreshHint()
    {
        if (tutorial) return;
        RefreshProtection();
        Hint=activity==0?$"Antar paket ke rumah biru. Bintang bonus: {collectedStars}/{starGoal}"
            :activity==1?$"Paket ekspres: {Mathf.Max(0,Mathf.CeilToInt(deadline-Time.time))} dtk untuk bonus +5"
            :activity==2?FragileStatus
            :fountainVisited?"Sudah berhenti! Sekarang antar ke rumah untuk bonus +5 koin."
            :$"Bonus +5: berhenti di kotak singgah {stampStreet} sampai penuh, lalu antar paket.";
    }

    private void RefreshProtection()
    {
        int newImpacts=Mathf.Max(0,game.Car.ImpactCount-observedImpacts);
        int absorbed=Mathf.Min(shields,newImpacts);
        protectedImpacts+=absorbed;shields-=absorbed;observedImpacts=game.Car.ImpactCount;
    }
}
