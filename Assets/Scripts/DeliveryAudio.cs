using System;
using UnityEngine;

// Short, locally synthesized sounds: no network, external packs or streaming decoder.
public sealed class DeliveryAudio : MonoBehaviour
{
    public enum Cue { Click, Pickup, Delivery, Coin, Boost, Bump, Purchase, Notice }
    private static DeliveryAudio instance;
    private AudioSource effects, engine;
    private AudioClip[] clips;
    private AudioClip engineClip;
    private Driver car;
    private DeliveryGameManager game;
    private float nextCoinTime;
    public static bool Enabled => PlayerPrefs.GetInt("Courier.Sound",1)!=0;
    // Permintaan pengguna 28 Sep: musik punya sakelar sendiri, lepas dari suara efek (klik, koin, mesin).
    public static bool MusicEnabled => PlayerPrefs.GetInt("Courier.Music",1)!=0;
    public static void ToggleMusic(){PlayerPrefs.SetInt("Courier.Music",MusicEnabled?0:1);PlayerPrefs.Save();}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics(){instance=null;}

    public void Configure(DeliveryGameManager manager)
    {
        instance=this;game=manager;car=manager.Car;
        effects=gameObject.AddComponent<AudioSource>();
        effects.playOnAwake=false;effects.spatialBlend=0;effects.ignoreListenerPause=true;
        engine=gameObject.AddComponent<AudioSource>();
        engine.playOnAwake=false;engine.spatialBlend=0;engine.loop=true;
        clips=new[]
        {
            Melody("Tombol",new[]{640f},0.045f),
            Melody("Paket diambil",new[]{392f,523.25f},0.10f),
            Melody("Paket sampai",new[]{523.25f,659.25f,783.99f},0.14f),
            Melody("Koin",new[]{880f,1174.66f},0.065f),
            Melody("Bantuan cepat",new[]{330f,440f,660f},0.075f),
            Melody("Benturan lembut",new[]{100f,65f},0.055f),
            Melody("Skin dibeli",new[]{523.25f,783.99f,1046.5f},0.12f),
            Melody("Perhatian",new[]{440f,349.23f},0.11f)
        };
        const int rate=22050;
        float[] samples=new float[rate];
        for(int i=0;i<samples.Length;i++)
        {
            float t=(float)i/rate;
            samples[i]=(Mathf.Sin(2*Mathf.PI*70*t)*0.5f+Mathf.Sin(2*Mathf.PI*140*t)*0.15f)
                *(0.85f+0.15f*Mathf.Sin(2*Mathf.PI*14*t));
        }
        engineClip=AudioClip.Create("Mesin truk",samples.Length,1,rate,false);
        engineClip.SetData(samples,0);engine.clip=engineClip;engine.volume=0;
    }

    private static AudioClip Melody(string name,float[] notes,float length)
    {
        const int rate=22050;
        int segment=Mathf.CeilToInt(length*rate);
        float[] samples=new float[segment*notes.Length];
        for(int n=0;n<notes.Length;n++)for(int i=0;i<segment;i++)
        {
            float t=(float)i/rate;
            float envelope=Mathf.Min(1,t/0.008f)*Mathf.Clamp01((length-t)/0.025f);
            samples[n*segment+i]=Mathf.Sin(2*Mathf.PI*notes[n]*t)*envelope*0.45f;
        }
        AudioClip clip=AudioClip.Create(name,samples.Length,1,rate,false);
        clip.SetData(samples,0);return clip;
    }

    public static void Play(Cue cue)
    {
        if(instance==null || !Enabled || DeliveryGameManager.IsValidationRunning || Application.isBatchMode || !Application.isFocused)return;
        if(cue==Cue.Coin && Time.unscaledTime<instance.nextCoinTime)return;
        if(cue==Cue.Coin)instance.nextCoinTime=Time.unscaledTime+0.09f;
        instance.effects.PlayOneShot(instance.clips[(int)cue],cue==Cue.Bump?0.28f:0.55f);
    }

    public static void Toggle()
    {
        bool enabled=!Enabled;PlayerPrefs.SetInt("Courier.Sound",enabled?1:0);PlayerPrefs.Save();
        if(!enabled && instance!=null){instance.effects.Stop();instance.engine.Stop();}
        if(enabled)Play(Cue.Click);
    }

    private void Update()
    {
        if(engine==null)return;
        bool running=Enabled && Application.isFocused && game!=null && !game.MenuOpen
            && Time.timeScale>0 && !DeliveryGameManager.IsValidationRunning;
        float volume=running?Mathf.Lerp(0.018f,0.075f,Mathf.Clamp01(car.CurrentSpeed/7.8f)):0;
        engine.volume=Mathf.MoveTowards(engine.volume,volume,Time.unscaledDeltaTime*0.4f);
        engine.pitch=Mathf.Lerp(0.8f,1.65f,Mathf.Clamp01(car.CurrentSpeed/7.8f));
        if(running && !engine.isPlaying)engine.Play();
        if(!running && engine.volume<=0.001f)engine.Stop();
    }

    private void OnApplicationFocus(bool focused)
    {if(!focused){if(effects!=null)effects.Stop();if(engine!=null)engine.Stop();}}
    private void OnApplicationPause(bool paused)
    {if(paused){if(effects!=null)effects.Stop();if(engine!=null)engine.Stop();}}
    private void OnDestroy()
    {
        if(instance==this)instance=null;
        if(clips!=null)foreach(var clip in clips)if(clip!=null)Destroy(clip);
        if(engineClip!=null)Destroy(engineClip);
    }
}
