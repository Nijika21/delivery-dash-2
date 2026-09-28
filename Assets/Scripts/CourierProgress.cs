using System;
using UnityEngine;

[Serializable]
public class CourierProgress
{
    public int Coins;
    public int OwnedMask=1;
    public int Equipped;
    public int CompletedDeliveries;
    public bool TutorialComplete;
    public string LastMode="kota-paket";
    public bool NeedsTutorial => !TutorialComplete && CompletedDeliveries == 0;
    // Petunjuk sorot sekali pakai yang sudah pernah tampil (DeliveryGameManager.GuideTip, bitmask).
    public int SeenTips;
    public string LastDailyDate="";
    public int DailyDay;
    public static readonly int[] DailyRewards={10,12,15,18,20,25,35};
    public bool CanClaimDaily(DateTime date)=>string.CompareOrdinal(date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),LastDailyDate)>0;
    public int NextDailyDay(DateTime date)
    {
        bool yesterday=DateTime.TryParseExact(LastDailyDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,out DateTime last) && (date.Date-last.Date).Days==1;
        return yesterday?Mathf.Clamp(DailyDay,0,7)%7+1:1;
    }
    public bool ClaimDaily(DateTime date)
    {
        if(!CanClaimDaily(date))return false;
        DailyDay=NextDailyDay(date);
        LastDailyDate=date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
        Coins+=DailyRewards[DailyDay-1];return true;
    }
    public static readonly string[] Names={"Kurir", "Kepik", "Awan", "Pisang", "Rimba", "Zebra", "Stroberi", "Balap", "Pelangi", "Galaksi"};
    // Harga bulat, jarak makin lebar ke atas (permintaan pengguna 26 Sep: maksimal 1200, sebelumnya 1000).
    public static readonly int[] Prices={0,150,300,400,500,600,700,850,1000,1200};
    public static readonly string[] Descriptions={"Warna asli truk kurir.", "Merah dengan bintik hitam.",
        "Biru langit berhias awan.", "Kuning dengan bintik cokelat.", "Hijau dengan motif daun.",
        "Belang hitam putih.", "Merah muda, krim, dan meses warna-warni.",
        "Abu tua dengan garis balap jingga.", "Enam warna dari depan sampai belakang.",
        "Ungu malam penuh bintang."};
    public static readonly Color[] Colors={Color.white,new Color32(255,183,111,255),new Color32(135,229,182,255),
        new Color32(244,167,196,255),new Color32(251,220,108,255),new Color32(125,187,242,255),
        new Color32(192,231,144,255),new Color32(234,128,112,255),new Color32(220,167,244,255),new Color32(126,155,215,255)};
    public bool Owns(int index)=>index>=0 && index<Names.Length && (OwnedMask&(1<<index))!=0;
    public bool Select(int index)
    {
        if(index<0 || index>=Names.Length)return false;
        if(!Owns(index))
        {
            if(Coins<Prices[index])return false;
            Coins-=Prices[index];OwnedMask|=1<<index;
        }
        Equipped=index;return true;
    }
    public static CourierProgress Load()
    {
        CourierProgress result=null;
        bool hasCurrent=PlayerPrefs.HasKey("Courier.Progress");
        if(hasCurrent)
        {
            try { result=JsonUtility.FromJson<CourierProgress>(PlayerPrefs.GetString("Courier.Progress","")); }
            catch(ArgumentException exception) { Debug.LogWarning("Progres kurir tidak dapat dibaca: "+exception.Message); }
        }
        if(result==null) result=new CourierProgress();
        if(!hasCurrent)
        {
            int stamps=PlayerPrefs.GetInt("Courier.Stamps",0);
            // Preserve previously earned colors when moving from the old stamp system.
            if(stamps>=3)result.OwnedMask|=2;
            if(stamps>=8)result.OwnedMask|=4;
            if(stamps>=15)result.OwnedMask|=8;
            result.Equipped=PlayerPrefs.GetInt("Courier.Livery",0);
        }
        result.Coins=Mathf.Max(0,result.Coins);result.OwnedMask=(result.OwnedMask&1023)|1;
        if(result.Equipped<0 || result.Equipped>=Names.Length || !result.Owns(result.Equipped))result.Equipped=0;
        if(result.LastMode!="kota-paket" && result.LastMode!="blok-paket" && result.LastMode!="classic")
            result.LastMode="kota-paket";
        return result;
    }
    public void Save()
    {
        if(DeliveryGameManager.IsValidationRunning)return;
        PlayerPrefs.SetString("Courier.Progress",JsonUtility.ToJson(this));PlayerPrefs.Save();
    }
}
