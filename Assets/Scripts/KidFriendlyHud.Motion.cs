using System.Collections.Generic;
using DeliveryDash.UI;
using UnityEngine.UIElements;

// Gerak pergantian layar dan popup (gerak.json popup-buka, popup-tutup, layar-ganti; alur.json).
// Popup di atas dasar yang sama (mis. lobby → pengaturan, HUD → jeda) hanya menggerakkan latar + kartu;
// dasar berbeda (lobby ↔ garasi, lobby → HUD, selesai kerja → lobby) memakai layar-ganti. HUD → HUD tanpa gerak.
public partial class KidFriendlyHud
{
    private int previousToolkitScreen;

    // Layar dasar tempat sebuah layar tampil: popup lobby → 2, dialog garasi → 6, semua HUD/popup permainan → 9.
    private static int BaseScreen(int screen)
    {
        switch (screen)
        {
            case 2: case 3: case 4: case 5: case 23: case 24: case 25: case 27: return 2;
            case 6: case 7: case 8: case 28: return 6;
            case 29: case 30: return 9;
            default: return screen >= 9 && screen <= 22 ? 9 : screen;
        }
    }

    private static VisualElement OverlayScrim(VisualElement screen) =>
        screen?.Q<VisualElement>("scrim") ?? screen?.Q<VisualElement>("dim");

    private static VisualElement OverlayCard(VisualElement screen) =>
        screen?.Q<VisualElement>(className: "dd-dialog") ?? screen?.Q<VisualElement>(className: "sc-bigmap")
        ?? screen?.Q<VisualElement>(className: "sc-story");

    private void PlayScreenTransition(List<VisualElement> previous)
    {
        if (!UiMotion.Loaded || toolkitRoot?.Root == null) return;
        VisualElement root = toolkitRoot.Root;
        VisualElement fresh = root.Q<VisualElement>("layar");
        VisualElement old = null;
        foreach (VisualElement element in previous)
            old = old ?? (element.name == "layar" ? element : element.Q<VisualElement>("layar"));
        if (fresh == null || old == null) return;

        int from = previousToolkitScreen, to = toolkitScreen;
        VisualElement oldCard = from == 1 ? null : OverlayCard(old), newCard = to == 1 ? null : OverlayCard(fresh);
        if (BaseScreen(from) != BaseScreen(to))
        {
            KeepLeaving(root, previous, false);
            foreach (VisualElement element in previous) UiMotion.Play(element, "layar-ganti", "layar lama");
            UiMotion.Play(fresh, "layar-ganti", "layar baru");
            RemoveLater(root, previous, UiMotion.Length("layar-ganti", "layar lama"));
            return;
        }
        if (newCard != null)
        {
            // Dari popup lain: latar sudah gelap, hanya kartu baru yang muncul.
            if (oldCard == null) UiMotion.Play(OverlayScrim(fresh), "popup-buka", "scrim");
            UiMotion.Play(newCard, "popup-buka", "dialog");
            return;
        }
        if (oldCard != null)
        {
            KeepLeaving(root, previous, true);
            UiMotion.Play(oldCard, "popup-tutup", "dialog");
            UiMotion.Play(OverlayScrim(old), "popup-tutup", "scrim");
            RemoveLater(root, previous, UiMotion.Length("popup-tutup", "scrim"));
        }
    }

    // Layar lama dipasang lagi di atas layar baru hanya untuk gerak keluar: tidak bisa diketuk,
    // dan untuk popup-tutup hanya latar + kartu yang tersisa (dasar di bawahnya sudah digambar layar baru).
    private static void KeepLeaving(VisualElement root, List<VisualElement> previous, bool overlayOnly)
    {
        foreach (VisualElement element in previous)
        {
            // Layar lama dipatok di tempatnya semula. Tanpa ini ia ikut aliran flex root di bawah layar baru,
            // sehingga kartu popup melompat ke bawah sebelum memudar.
            UnityEngine.Rect was = element.layout;
            root.Add(element);
            element.style.position = Position.Absolute;
            if (!float.IsNaN(was.width))
            {
                element.style.left = was.x;
                element.style.top = was.y;
                element.style.width = was.width;
                element.style.height = was.height;
            }
            element.pickingMode = PickingMode.Ignore;
            element.Query<VisualElement>().ForEach(child => child.pickingMode = PickingMode.Ignore);
            if (!overlayOnly) continue;
            VisualElement screen = element.name == "layar" ? element : element.Q<VisualElement>("layar");
            if (screen == null) continue;
            VisualElement scrim = OverlayScrim(screen), card = OverlayCard(screen);
            foreach (VisualElement child in screen.Children())
                if (child != scrim && (card == null || !child.Contains(card) && child != card))
                    child.style.display = DisplayStyle.None;
        }
    }

    // ---------- Layar memuat (layar 01, alur.json memuat) ----------
    // Saat aplikasi mulai: logo + bar muat terisi 1 dtk, lalu otomatis ke lobby (layar-ganti). Kota sudah dibangun
    // sebelum HUD ada, jadi layar ini hanya pembuka singkat. Sekali per peluncuran (tidak saat kembali dari Classic).
    private const float LoadingSeconds = 1f;
    private static bool loadingShown;
    private float loadingStart = -1f;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLaunchFlags()
    {
        loadingShown = false;
    }

    private bool ShowingLoading()
    {
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (loadingStart < 0f)
        {
            if (loadingShown) return false;
            loadingShown = true;
            loadingStart = now;
        }
        return now - loadingStart < LoadingSeconds;
    }

    private void UpdateLoading()
    {
        if (toolkitScreen != 1 || loadingStart < 0f) return;
        VisualElement fill = boundScreen?.Q<VisualElement>("load")?.Q<VisualElement>("fill");
        if (fill == null) return;
        float t = UnityEngine.Mathf.Clamp01((UnityEngine.Time.realtimeSinceStartup - loadingStart) / LoadingSeconds);
        fill.style.width = Length.Percent(100f * UiMotion.Evaluate("out-cubic", t));
    }

    // ---------- Koin bertambah (gerak.json koin-plus) ----------
    // Label +N muncul lalu naik menghilang, chip koin memantul, angka menghitung naik 500 ms (jeda 200 ms).
    // Koin berkurang (beli skin) langsung berganti tanpa gerak.
    private int coinTarget = -1;
    private float coinShown, coinCountFrom, coinCountStart = -1f;

    private void UpdateCoins()
    {
        int coins = gameManager.Progress.Coins;
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (coinTarget < 0 || coins < coinTarget)
        {
            coinTarget = coins;
            coinShown = coins;
            coinCountStart = -1f;
        }
        else if (coins > coinTarget)
        {
            int gain = coins - coinTarget;
            coinCountFrom = coinShown;
            coinTarget = coins;
            coinCountStart = now + 0.2f;
            PlayCoinGain(gain);
        }
        if (coinCountStart >= 0f && now >= coinCountStart)
        {
            float t = (now - coinCountStart) / 0.5f;
            coinShown = UnityEngine.Mathf.Lerp(coinCountFrom, coinTarget, UnityEngine.Mathf.Clamp01(t));
            if (t >= 1f) coinCountStart = -1f;
        }
        SetNestedLabel("coins", "teks", UnityEngine.Mathf.RoundToInt(coinShown).ToString());
    }

    private void PlayCoinGain(int gain)
    {
        VisualElement coins = boundScreen?.Q<VisualElement>("coins");
        if (coins == null || !UiMotion.Loaded) return;
        Label plus = boundScreen.Q<Label>("koin-plus");
        if (plus == null)
        {
            plus = new Label { name = "koin-plus", pickingMode = PickingMode.Ignore };
            plus.AddToClassList("sc-dwell");
            plus.style.position = Position.Absolute;
            // Disetujui pengguna (26 Sep): hijau tua --dd-green-800 langsung di atas dunia sulit dibaca
            // (kontras ±2,8 di rumput, ±1,5 di aspal), jadi putih bergaris tepi tinta.
            plus.style.color = UnityEngine.Color.white;
            plus.style.unityTextOutlineColor = new UnityEngine.Color(0.114f, 0.165f, 0.184f, 1f);
            plus.style.unityTextOutlineWidth = 3f;
            boundScreen.Add(plus);
        }
        // Posisi bingkai 15, dihitung dari chip koin (induknya beda per layar, mis. garasi): chip di kanan layar →
        // label 62 px di kirinya (top +12); chip di kiri (HUD portrait) → label 56 px di kanannya (left 196, top 112).
        UnityEngine.Rect chip = coins.worldBound;
        UnityEngine.Vector2 local = boundScreen.WorldToLocal(chip.position);
        bool chipOnLeft = chip.center.x < boundScreen.worldBound.center.x;
        plus.style.left = chipOnLeft ? local.x + chip.width + 56f : local.x - 62f;
        plus.style.top = local.y + (chipOnLeft ? 14f : 12f);
        plus.text = "+" + gain;
        UiMotion.Play(plus, "koin-plus", "label");
        UiMotion.Play(coins, "koin-plus", "chip");
    }

    // ---------- Rangkuman selesai kerja (layar 22, gerak.json rangkuman) ----------
    // Baris ke-n muncul 80 ms × n sesudah popup-buka selesai; total koin menghitung naik 600 ms.
    private float summaryStart = -1f;

    private void BindSummary()
    {
        summaryStart = -1f;
        VisualElement summary = boundScreen?.Q<VisualElement>("summary");
        if (toolkitScreen != 22 || page != Page.FinishWork || summary == null) return;
        SetNestedLabel("stat:box", "num", gameManager.Deliveries.ToString());
        SetNestedLabel("stat:star", "num", gameManager.StarPickups.ToString());
        float popup = UiMotion.Length("popup-buka", "dialog");
        summaryStart = UnityEngine.Time.realtimeSinceStartup + popup;
        for (int i = 0; i < summary.childCount; i++)
            UiMotion.Play(summary[i], "rangkuman", extraDelay: popup + 0.08f * i);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (summaryStart < 0f || toolkitScreen != 22 || page != Page.FinishWork) return;
        Label coins = boundScreen?.Q<VisualElement>("stat:coin")?.Q<Label>("num");
        if (coins == null) return;
        float t = UnityEngine.Mathf.Clamp01((UnityEngine.Time.realtimeSinceStartup - summaryStart - 0.08f) / 0.6f);
        coins.text = "+" + UnityEngine.Mathf.RoundToInt(gameManager.Stars * t);
    }

    // ---------- Kendali redup (gerak.json kontrol-redup) ----------
    // .sc-ctl.is-disabled = opacity 0,4 di USS; transisi 150 ms out-cubic ditambahkan ke transisi tekan 80 ms yang sudah ada.
    private void BindControlFade()
    {
        boundScreen?.Query<VisualElement>(className: "sc-ctl").ForEach(control =>
        {
            control.style.transitionProperty = new List<StylePropertyName>
                { "translate", "border-bottom-width", "height", "margin-bottom", "opacity" };
            control.style.transitionDuration = new List<TimeValue>
            {
                new TimeValue(80, TimeUnit.Millisecond), new TimeValue(80, TimeUnit.Millisecond),
                new TimeValue(80, TimeUnit.Millisecond), new TimeValue(80, TimeUnit.Millisecond),
                new TimeValue(150, TimeUnit.Millisecond)
            };
            control.style.transitionTimingFunction = new List<EasingFunction>
                { EasingMode.Ease, EasingMode.Ease, EasingMode.Ease, EasingMode.Ease, EasingMode.EaseOutCubic };
        });
    }

    // ---------- Petunjuk awal lama (layar 09) ----------
    // Digantikan tutorial interaktif (KidFriendlyHud.Guide, uji HP 27 Sep); kartu templatnya tidak ditampilkan.
    private void HideTemplateCoach()
    {
        VisualElement coach = boundScreen?.Q<VisualElement>(className: "sc-coach");
        if (coach != null) coach.style.display = DisplayStyle.None;
    }


    private static void RemoveLater(VisualElement root, List<VisualElement> previous, float seconds)
    {
        root.schedule.Execute(() =>
        {
            foreach (VisualElement element in previous)
            {
                UiMotion.Stop(element);
                element.RemoveFromHierarchy();
            }
        }).StartingIn((long)(seconds * 1000f) + 16);
    }
}
