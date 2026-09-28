using System.Collections.Generic;
using UnityEngine;

// Optional jobs: accepted letters survive expiry and can be carried together.
public class DeliverySideQuests : MonoBehaviour
{
    public enum Kind { Letter, Rest }
    public class Quest
    {
        public Vector2 Position;
        public string Name;
        public Kind Type;
        public int OfferedAt;
        public bool Completed;
        public bool Collected;
        public float Dwell;
        public StopZoneView Marker;
    }
    public readonly List<Quest> Quests=new List<Quest>();
    private DeliveryGameManager game;
    private int lastOffer=-2;
    private StopZoneView dropoff;
    public Vector2 DropoffPosition => game != null && game.Town != null ? game.Town.LetterDropoff : new Vector2(1,-7);
    public string Hint { get; private set; }
    // Progres berhenti tertinggi di zona bantuan warga yang belum diambil (untuk lingkar di sekitar truk).
    public float HelpProgress01 { get; private set; }
    public int CarriedLetters
    {
        get { int count=0;foreach(var quest in Quests)if(quest.Collected && !quest.Completed)count++;return count; }
    }
    public int ActiveCount
    {
        get { int count=0;foreach(var q in Quests)if(!q.Completed)count++;return count; }
    }
    public void Configure(DeliveryGameManager manager){game=manager;ResetQuests();}

    public void ResetQuests()
    {
        foreach(var quest in Quests)if(quest.Marker!=null)Destroy(quest.Marker.gameObject);
        Quests.Clear();Hint="";lastOffer=-2;
        if(dropoff!=null)Destroy(dropoff.gameObject);
        dropoff=null;
    }

    public void OnDeliveryCompleted(float roll)
    {
        foreach(var quest in Quests)
            if(!quest.Completed && !quest.Collected && game.Deliveries-quest.OfferedAt>=3)
            {
                quest.Completed=true;
                if(quest.Marker!=null)quest.Marker.Dismiss();
            }
        Quests.RemoveAll(q=>q.Completed);
        if(roll<0.25f && game.Deliveries-lastOffer>=2 && ActiveCount<3)
            TryOffer(Random.value<0.65f?Kind.Letter:Kind.Rest);
    }

    public bool TryOffer(Kind kind)
    {
        if(ActiveCount>=3)return false;
        int count=game.Town.ActivityStopCount;
        if(count==0)return false;
        int start=Random.Range(0,count);
        for(int i=0;i<count;i++)
        {
            if(!game.Town.TryActivityStop((start+i)%count,out Vector2 p,out _)
                || Quests.Exists(q=>!q.Completed && Vector2.Distance(q.Position,p)<6))continue;
            AddQuest(kind,p);
            return true;
        }
        return false;
    }

    // Tutorial: tawaran di pemberhentian yang paling dekat ke bagian tengah rute (rute pulang rumah → gudang), bukan acak.
    public Quest OfferNearRoute(Kind kind,List<Vector2> route)
    {
        if(ActiveCount>=3 || route==null || route.Count==0)return null;
        int from=route.Count>=4?route.Count/4:0, to=route.Count>=4?route.Count*3/4:route.Count-1;
        Vector2 best=default;
        float bestDistance=float.PositiveInfinity;
        for(int i=0;i<game.Town.ActivityStopCount;i++)
        {
            if(!game.Town.TryActivityStop(i,out Vector2 p,out _)
                || Quests.Exists(q=>!q.Completed && Vector2.Distance(q.Position,p)<6))continue;
            for(int j=from;j<=to;j++)
            {
                float distance=Vector2.Distance(p,route[j]);
                if(distance<bestDistance){bestDistance=distance;best=p;}
            }
        }
        return float.IsPositiveInfinity(bestDistance)?null:AddQuest(kind,best);
    }

    private Quest AddQuest(Kind kind,Vector2 p)
    {
        // Zona bantuan warga (kuning) hanya ada selama tawaran berlaku.
        var marker=StopZoneView.Create(game.Town,"bantuan",p,0f,null);
        var quest=new Quest{Position=p,Name=kind==Kind.Letter?"Antar surat":"Istirahat sebentar",
            Type=kind,OfferedAt=game.Deliveries,Marker=marker};
        Quests.Add(quest);
        lastOffer=game.Deliveries;
        return quest;
    }

    private void Update()
    {
        if(game==null || game.MenuOpen || Time.timeScale==0)return;
        Tick(Time.deltaTime);
    }

    public static float AdvanceDwell(float value,bool parked,float seconds)
        => Mathf.MoveTowards(value,parked?2:0,Mathf.Max(0,seconds)*(parked?1:2.5f));

    public void Tick(float seconds)
    {
        Hint="";
        HelpProgress01=0f;
        int letters=0;
        float dropoffProgress=0f;
        bool dropped=false;
        foreach(var quest in Quests)
        {
            if(quest.Completed)continue;
            float distance=Vector2.Distance(game.Car.transform.position,quest.Position);
            bool parked=distance<2 && game.Car.CurrentSpeed<0.3f;
            quest.Dwell=AdvanceDwell(quest.Dwell,parked,seconds);
            if(quest.Collected)
            {
                letters++;
                dropoffProgress=Mathf.Max(dropoffProgress,quest.Dwell/2f);
                Hint="Bawa surat ke kotak surat depan gudang. Berhenti di sana.";
                if(quest.Dwell>=2)
                {
                    quest.Completed=true;letters--;dropped=true;
                    game.AwardBonus(8,"Surat sampai! Terima kasih. +8 koin");
                }
                continue;
            }
            if(distance<5)Hint=quest.Type==Kind.Letter
                ?"Bantu antar surat? Berhenti di kotak sampai penuh. +8 koin"
                :"Mau istirahat? Berhenti di kotak sampai penuh. +4 koin";
            HelpProgress01=Mathf.Max(HelpProgress01,quest.Dwell/2f);
            if(quest.Marker!=null)quest.Marker.SetProgress(quest.Dwell/2f);
            if(quest.Dwell<2)continue;
            if(quest.Marker!=null)quest.Marker.Complete();
            quest.Marker=null;
            if(quest.Type==Kind.Letter)
            {
                quest.Collected=true;quest.Position=DropoffPosition;quest.Dwell=0;letters++;
                game.AwardBonus(0,"Surat dibawa! Antar ke kotak surat depan gudang.");
            }
            else
            {
                quest.Completed=true;game.Car.StartBoost(4);
                game.AwardBonus(4,"Siap jalan lagi! +4 koin dan lebih cepat 4 detik.");
            }
        }
        // Zona taruh surat (merah bata) di kotak surat depan gudang, hanya selama ada surat di truk.
        if(letters>0 && dropoff==null)dropoff=StopZoneView.Create(game.Town,"surat",DropoffPosition,0f,null);
        if(dropoff!=null)
        {
            if(letters>0)dropoff.SetProgress(dropoffProgress);
            else
            {
                if(dropped)dropoff.Complete();else dropoff.Dismiss();
                dropoff=null;
            }
        }
    }
}
