using System;
using UnityEngine;

// Musik latar 2.0, sesuai replika yang disetujui pengguna (28 Sep):
// - lobby (termasuk garasi, hadiah, mode, pengaturan, cerita): satu lagu berulang;
// - main (Kota Paket dan Blok Paket sama saja): daftar lagu diputar bergantian terus;
// - hening saat layar muat;
// - pindah suasana: pudar keluar 0,6 dtk lalu pudar masuk 1 dtk. Mengikuti sakelar Musik (Courier.Music).
public sealed class DeliveryMusic : MonoBehaviour
{
    public enum Mode { Silent, Lobby, Play }

    private const float Volume = 0.45f;
    private const float FadeIn = 1f;
    private const float FadeOut = 0.6f;
    private AudioSource source;
    private DeliveryMusicSet set;
    private Func<Mode> wanted;
    private Mode playing = Mode.Silent;
    private int track;
    private bool suspended;

    public void Configure(DeliveryMusicSet music, Func<Mode> mode)
    {
        set = music;
        wanted = mode;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0f; source.ignoreListenerPause = true; source.volume = 0f;
    }

    private static bool Allowed => DeliveryAudio.MusicEnabled && !DeliveryGameManager.IsValidationRunning && !Application.isBatchMode;

    private void Update()
    {
        if (source == null || set == null || suspended) return;
        Mode want = Allowed && wanted != null ? wanted() : Mode.Silent;
        if (want != playing)
        {
            // Pudar keluar dulu, baru ganti lagu.
            source.volume = Mathf.MoveTowards(source.volume, 0f, Time.unscaledDeltaTime * Volume / FadeOut);
            if (source.isPlaying && source.volume > 0.001f) return;
            source.Stop();
            playing = want;
            if (playing != Mode.Silent) StartClip();
            return;
        }
        if (playing == Mode.Silent) return;
        // Lagu main habis → lagu berikutnya.
        if (playing == Mode.Play && !source.isPlaying)
        {
            track++;
            StartClip();
            return;
        }
        source.volume = Mathf.MoveTowards(source.volume, Volume, Time.unscaledDeltaTime * Volume / FadeIn);
    }

    private void StartClip()
    {
        AudioClip clip = playing == Mode.Lobby ? set.lobby : Pick();
        if (clip == null) { playing = Mode.Silent; return; }
        source.clip = clip;
        source.loop = playing == Mode.Lobby;
        source.volume = 0f;
        source.Play();
    }

    private AudioClip Pick()
    {
        if (set.play == null || set.play.Length == 0) return null;
        return set.play[((track % set.play.Length) + set.play.Length) % set.play.Length];
    }

    // Aplikasi ke latar belakang: tahan di tempat (bukan dianggap lagu habis), lanjut saat kembali.
    private void OnApplicationPause(bool paused) => Suspend(paused);
    private void OnApplicationFocus(bool focused) => Suspend(!focused);

    private void Suspend(bool value)
    {
        if (source == null || suspended == value) return;
        suspended = value;
        if (value) source.Pause();
        else source.UnPause();
    }
}
