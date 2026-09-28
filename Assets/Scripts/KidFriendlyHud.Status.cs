using System;
using DeliveryDash.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Isi HUD yang berubah selama bermain (Design/d5/hud.json): bar tujuan, jarak rute, kolom chip,
// toast, dan lingkar berhenti di sekitar truk. Susunan awal tetap dari UXML hasil generator.
public partial class KidFriendlyHud
{
    private readonly List<Vector2> hudRoute = new List<Vector2>();
    private readonly HashSet<string> shownChips = new HashSet<string>();
    private string chipSignature;
    private VisualElement chipColumn;
    private float nextDistanceUpdate;
    private string distanceText = string.Empty;
    private string toastText = string.Empty;

    private int shownFragileHits;

    // 18 (peta besar) dan 19 (jeda) menampilkan HUD hidup di bawah lapisannya, jadi ikut diperbarui.
    private static bool IsHudScreen(int screen) => screen >= 9 && screen <= 21 || screen == 30;

    private void BindHudStatus()
    {
        chipSignature = null;
        chipColumn = null;
        // null = belum pernah diterapkan: toast templat (teks contoh) atau toast buatan (kosong) selalu diselaraskan
        // pada pembaruan pertama, termasuk disembunyikan kalau tidak ada pesan.
        toastText = null;
        nextDistanceUpdate = 0f;
        if (!IsHudScreen(toolkitScreen) || boundScreen == null) return;
        EnsureChipColumn();
        EnsureToast();
        // hud.json pesan.batas: maks. 2 baris. Di CSS pil dan teks menyusut sendiri; di UI Toolkit harus eksplisit,
        // kalau tidak teks panjang tetap satu baris dan keluar tepi layar (portrait 16/17/29).
        VisualElement toast = boundScreen.Q<VisualElement>("msg");
        if (toast != null)
        {
            toast.style.flexShrink = 1f;
            toast.style.maxWidth = Length.Percent(100f);
            Label toastLabel = toast.Q<Label>("teks");
            if (toastLabel != null)
            {
                toastLabel.style.flexShrink = 1f;
                toastLabel.style.whiteSpace = WhiteSpace.Normal;
            }
        }
        // Menyimpang dari D5 atas permintaan pengguna (26 Sep): tidak ada lingkar progres di sekitar truk. Progres
        // singgah/bantuan/surat tampil di bantalan zonanya sendiri (StopZoneView.SetProgress), sama seperti antar paket.

        // Templat desain berisi keadaan contoh (layar 12: segmen rapuh pertama merah, "1/3"). Tanpa ini contoh itu
        // sempat tampil satu bingkai setiap layar HUD dikloning ulang (bug 26 Sep: meter terbaca merah-kosong-kosong
        // lalu kembali kosong). Bersihkan contoh, lalu isi status sebenarnya di bingkai yang sama.
        boundScreen.Query<VisualElement>(className: "dd-meter__seg").ForEach(segment => segment.RemoveFromClassList("is-hit"));
        UpdateHudStatus();
    }

    private void UpdateHudStatus()
    {
        if (!IsHudScreen(toolkitScreen) || boundScreen == null || gameManager?.Town == null) return;
        UpdateDestination();
        UpdateChips();
        UpdateToast();
        UpdateSpotlight();
    }

    // ---------- Sorotan tujuan (hud.json sorotan, gerak.json sorotan-kartu + kendali redup) ----------

    private void UpdateSpotlight()
    {
        bool showing = gameManager.IsShowingDestination;
        boundScreen.Query<VisualElement>(className: "sc-ctl").ForEach(control => control.EnableInClassList("is-disabled", showing));
        boundScreen.Query<VisualElement>(className: "sc-reverse").ForEach(control => control.EnableInClassList("is-disabled", showing));
        VisualElement card = boundScreen.Q<VisualElement>(className: "sc-spot");
        if (card == null) return;
        card.style.display = showing ? DisplayStyle.Flex : DisplayStyle.None;
        if (!showing) return;

        string address = gameManager.AddressName;
        if (card.ClassListContains("sc-spot--warn"))
        {
            Label body = card.Q<Label>("body");
            if (body != null) body.text = gameManager.Carrying ? $"Paket untuk {address} masih di truk." : "Surat masih di truk.";
        }
        else
        {
            Label title = card.Q<Label>("title");
            if (title != null) title.text = address;
        }

        float remaining = gameManager.DestinationPreviewRemaining;
        VisualElement fill = card.Q<VisualElement>("fill");
        if (fill != null) fill.style.width = Length.Percent(100f * remaining / DeliveryGameManager.DestinationPreviewSeconds);
        // Kartu muncul 220 ms: opacity 0→1, skala 0,92→1 (out-back).
        float t = Mathf.Clamp01((DeliveryGameManager.DestinationPreviewSeconds - remaining) / 0.22f);
        const float back = 1.70158f;
        float eased = 1f + (back + 1f) * Mathf.Pow(t - 1f, 3f) + back * Mathf.Pow(t - 1f, 2f);
        // Kartu hilang 160 ms terakhir (in-cubic), termasuk sesudah dilewati dengan ketukan.
        float fade = Mathf.Clamp01(remaining / 0.16f);
        card.style.opacity = t * (1f - Ease.InCubic(1f - fade));
        float scale = Mathf.LerpUnclamped(0.92f, 1f, eased);
        card.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    // ---------- Bar tujuan ----------

    private void UpdateDestination()
    {
        VisualElement bar = boundScreen.Q<VisualElement>("hudbar");
        if (bar == null) return;
        string text = DestinationText(out string icon);
        Label label = bar.Q<Label>("text");
        if (label != null) label.text = text;
        VisualElement iconElement = bar.Q<VisualElement>("hud-dest") ?? (bar.childCount > 0 ? bar[0] : null);
        if (iconElement != null && !iconElement.ClassListContains(icon))
        {
            iconElement.ClearClassList();
            iconElement.AddToClassList("sc-hud-dest");
            if (icon.StartsWith("ikon--", StringComparison.Ordinal)) iconElement.AddToClassList("ikon");
            else iconElement.AddToClassList("sc-bg");
            iconElement.AddToClassList(icon);
        }

        if (Time.unscaledTime >= nextDistanceUpdate)
        {
            nextDistanceUpdate = Time.unscaledTime + 0.25f;
            distanceText = FormatDistance(RouteDistance());
        }
        Label distance = bar.Q<Label>("dist");
        if (distance != null)
        {
            distance.text = distanceText;
            // hud.json jarakJingga: tutorial menuju gudang pertama kali (sebelum paket pertama diambil).
            distance.EnableInClassList("dd-hudbar__dist--orange",
                gameManager.TutorialActive && !gameManager.Carrying && gameManager.Deliveries == 0);
        }
    }

    private string DestinationText(out string icon)
    {
        int letters = gameManager.SideQuests != null ? gameManager.SideQuests.CarriedLetters : 0;
        if (gameManager.FinishRequested)
        {
            if (gameManager.Carrying) { icon = "ikon--i-house"; return gameManager.AddressName; }
            if (letters > 0) { icon = "a-zona-surat"; return "Kotak surat depan gudang"; }
            icon = "ikon--i-park";
            return gameManager.Town.IsInsideFinishParking(driver.transform.position) ? "Parkir selesai kerja" : "Kembali ke gudang";
        }
        if (gameManager.Guide == DeliveryGameManager.GuideStep.HelpZone) { icon = "a-zona-bantuan"; return "Bantu warga"; }
        if (gameManager.Guide == DeliveryGameManager.GuideStep.FinishInfo) { icon = "ikon--i-park"; return "Kapsul selesai kerja"; }
        DeliveryActivities activities = gameManager.Activities;
        if (activities != null && activities.HasStopover && activities.GuideToBonus)
        {
            icon = "a-zona-singgah";
            // Kotak singgah tutorial bisa berada di titik rute tanpa nama jalan.
            return string.IsNullOrEmpty(activities.StopoverStreet) ? "Kotak singgah" : "Singgah di " + activities.StopoverStreet;
        }
        if (gameManager.Carrying) { icon = "ikon--i-house"; return gameManager.AddressName; }
        if (letters > 0) { icon = "a-zona-surat"; return "Kotak surat depan gudang"; }
        icon = "a-zona-paket";
        return "Ambil paket di gudang";
    }

    // Sisa jarak menyusuri rute jalan (bukan garis lurus). Rute grid dilangkahi tiap 3 sel supaya
    // tangga 4 arah tidak menggelembungkan jarak diagonal.
    private float RouteDistance()
    {
        Vector2 from = driver.transform.position;
        Vector2 target = gameManager.NavigationTarget;
        gameManager.Town.TraceRoute(from, target, hudRoute);
        if (hudRoute.Count == 0) return Vector2.Distance(from, target);
        float total = Vector2.Distance(from, hudRoute[0]);
        Vector2 previous = hudRoute[0];
        for (int i = 3; i < hudRoute.Count; i += 3)
        {
            total += Vector2.Distance(previous, hudRoute[i]);
            previous = hudRoute[i];
        }
        return total + Vector2.Distance(previous, hudRoute[hudRoute.Count - 1]);
    }

    // HANDOFF 14: chip "Berhenti p%" dengan p = floor(100·progres/T), dari nilai yang sama dengan isi lingkar.
    public static int StopPercent(float progress01) => Mathf.FloorToInt(Mathf.Clamp01(progress01) * 100f + 0.0001f);

    public static string FormatDistance(float meters)
    {
        meters = Mathf.Max(0f, meters);
        int value = meters >= 20f ? Mathf.RoundToInt(meters / 5f) * 5 : Mathf.RoundToInt(meters);
        return value + " m";
    }

    // ---------- Kolom chip ----------

    private void EnsureChipColumn()
    {
        chipColumn = boundScreen.Q<VisualElement>("chips");
        if (chipColumn != null) return;
        var anchor = new VisualElement { name = "@chips", pickingMode = PickingMode.Ignore };
        anchor.AddToClassList("sc-abs");
        anchor.style.left = toolkitRoot.IsPortrait ? 20 : 24;
        anchor.style.top = toolkitRoot.IsPortrait ? 104 : 102;
        chipColumn = new VisualElement { name = "chips", pickingMode = PickingMode.Ignore };
        chipColumn.AddToClassList("sc-chips");
        anchor.Add(chipColumn);
        VisualElement message = boundScreen.Q<VisualElement>("@msg");
        if (message != null) boundScreen.Insert(boundScreen.IndexOf(message), anchor);
        else boundScreen.Add(anchor);
    }

    private void UpdateChips()
    {
        if (chipColumn == null) return;
        var wanted = new List<string>();
        DeliveryActivities activities = gameManager.Activities;
        int helpCount = gameManager.SideQuests != null ? gameManager.SideQuests.ActiveCount : 0;
        if (helpCount > 0) wanted.Add("bantuan");
        if (activities != null)
        {
            if (activities.IsExpress && activities.ExpressRunning && gameManager.Carrying) wanted.Add("bolt");
            else if (activities.IsFragile && gameManager.Carrying) wanted.Add("crack");
            else if (activities.HasStopover) wanted.Add(activities.GuideToBonus ? "rest-house" : "rest-stop");
            // hud.json: chip bintang hanya di perjalanan biasa (bukan ekspres, rapuh, atau singgah).
            bool plainTrip = !activities.IsExpress && !activities.IsFragile && !activities.HasStopover;
            if (plainTrip && !gameManager.TutorialTrip && activities.StarGoal > 0 && gameManager.Carrying) wanted.Add("star");
            if (activities.HasShield) wanted.Add("shield");
        }
        int letters = gameManager.SideQuests != null ? gameManager.SideQuests.CarriedLetters : 0;
        if (letters > 0) wanted.Add("letter");

        string signature = string.Join("|", wanted) + (toolkitRoot.IsPortrait ? "|p" : "|l");
        if (signature != chipSignature)
        {
            chipSignature = signature;
            RebuildChips(wanted);
        }

        SetChipText("chip:bantuan", toolkitRoot.IsPortrait ? $"Bantuan {helpCount}/3" : $"Bantuan warga {helpCount}/3");
        if (activities == null) return;
        int seconds = activities.ExpressSecondsLeft;
        SetChipText("chip:bolt", null, $"{seconds / 60}:{seconds % 60:00}");
        SetChipText("chip:rest", null, StopPercent(activities.StopoverProgress) + "%");
        SetChipText("chip:star", $"Bintang {activities.StarsCollected}/{activities.StarGoal}");
        SetChipText("chip:letter", "Surat " + letters);
        VisualElement meter = chipColumn.Q<VisualElement>("meter");
        int hits = activities.IsFragile ? activities.FragileHits : 0;
        if (meter != null)
        {
            for (int i = 0; i < meter.childCount; i++) meter[i].EnableInClassList("is-hit", i < hits);
            // gerak.json rapuh-kena: segmen baru membesar lalu kembali, chip rapuh bergoyang.
            if (hits > shownFragileHits && hits <= meter.childCount)
            {
                UiMotion.Play(meter[hits - 1], "rapuh-kena", "segmen");
                UiMotion.Play(chipColumn.Q<VisualElement>("chip:crack"), "rapuh-kena", "chip");
            }
        }
        shownFragileHits = hits;
    }

    private void RebuildChips(List<string> wanted)
    {
        var leaving = new Dictionary<string, (VisualElement chip, Rect rect)>();
        for (int i = chipColumn.childCount - 1; i >= 0; i--)
        {
            string elementName = chipColumn[i].name ?? string.Empty;
            if (elementName.StartsWith("chip:", StringComparison.Ordinal))
                leaving[elementName.Substring(5)] = (chipColumn[i], chipColumn[i].layout);
            if (elementName.StartsWith("chip:", StringComparison.Ordinal) || elementName.StartsWith("btn:arahkan", StringComparison.Ordinal))
                chipColumn.RemoveAt(i);
        }
        var current = new HashSet<string>();
        foreach (string key in wanted)
        {
            string id = key.StartsWith("rest", StringComparison.Ordinal) ? "rest" : key;
            current.Add(id);
            VisualElement chip = BuildChip(key);
            chipColumn.Add(chip);
            if (!shownChips.Contains(id)) PlayChipEnter(chip);
            if (id == "rest")
            {
                bool toHouse = key == "rest-house";
                VisualElement button = BuildButton(toHouse ? "btn:arahkan-ke-rumah" : "btn:arahkan-ke-tempat-berhenti",
                    toHouse ? "ikon--i-house" : "ikon--i-rest", toHouse ? "Arahkan ke rumah" : "Arahkan ke tempat berhenti");
                button.style.marginTop = 4;
                chipColumn.Add(button);
                BindElementClick(button, () => { gameManager.Activities?.ToggleBonusGuide(); chipSignature = null; });
            }
        }
        // gerak.json chip-keluar: chip yang hilang mengecil dan memudar 150 ms di tempatnya semula.
        foreach (string id in shownChips)
        {
            if (current.Contains(id) || !leaving.TryGetValue(id, out (VisualElement chip, Rect rect) old) || !UiMotion.Loaded) continue;
            if (float.IsNaN(old.rect.width) || chipColumn.parent == null) continue;
            VisualElement ghost = old.chip;
            ghost.style.position = Position.Absolute;
            ghost.style.left = chipColumn.layout.x + old.rect.x;
            ghost.style.top = chipColumn.layout.y + old.rect.y;
            chipColumn.parent.Add(ghost);
            UiMotion.Play(ghost, "chip-keluar", done: ghost.RemoveFromHierarchy);
        }
        shownChips.Clear();
        shownChips.UnionWith(current);
    }

    private static VisualElement BuildChip(string key)
    {
        switch (key)
        {
            case "bantuan": return Chip("chip:bantuan", "dd-chip--help", "a-zona-bantuan", "Bantuan warga", null);
            case "bolt": return Chip("chip:bolt", "dd-chip--bolt", "ikon--i-bolt", "Ekspres", "0:00");
            case "star": return Chip("chip:star", "dd-chip--star", "ikon--i-star", "Bintang", null);
            case "shield": return Chip("chip:shield", "dd-chip--shield", "ikon--i-shield", "Pelindung", null);
            case "letter": return Chip("chip:letter", null, "ikon--i-letter", "Surat", null);
            case "crack":
                VisualElement fragile = Chip("chip:crack", "dd-chip--warn", "ikon--i-crack", "Rapuh", null);
                var meter = new VisualElement { name = "meter", pickingMode = PickingMode.Ignore };
                meter.AddToClassList("dd-meter");
                meter.AddToClassList("ml-8");
                for (int i = 1; i <= DeliveryActivities.FragileLimit; i++)
                {
                    var segment = new VisualElement { name = "seg-" + i, pickingMode = PickingMode.Ignore };
                    segment.AddToClassList("dd-meter__seg");
                    meter.Add(segment);
                }
                fragile.Add(meter);
                return fragile;
            default: return Chip("chip:rest", "dd-chip--help", "ikon--i-rest", "Berhenti", "0%");
        }
    }

    private static VisualElement Chip(string name, string variant, string icon, string text, string dwell)
    {
        var chip = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
        chip.AddToClassList("dd-chip");
        if (variant != null) chip.AddToClassList(variant);
        var glyph = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
        glyph.AddToClassList("dd-chip__icon");
        glyph.AddToClassList(icon.StartsWith("ikon--", StringComparison.Ordinal) ? "ikon" : "sc-bg");
        glyph.AddToClassList(icon);
        chip.Add(glyph);
        chip.Add(new Label(text) { name = "teks", pickingMode = PickingMode.Ignore });
        if (dwell != null)
        {
            var value = new Label(dwell) { name = "dwell", pickingMode = PickingMode.Ignore };
            value.AddToClassList("sc-dwell");
            chip.Add(value);
        }
        return chip;
    }

    private static VisualElement BuildButton(string name, string icon, string text)
    {
        var button = new VisualElement { name = name };
        button.AddToClassList("dd-btn");
        button.AddToClassList("dd-btn--quiet");
        button.AddToClassList("dd-btn--small");
        var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
        glyph.AddToClassList("dd-btn__icon");
        glyph.AddToClassList("ikon");
        glyph.AddToClassList(icon);
        button.Add(glyph);
        button.Add(new Label(text) { name = "teks", pickingMode = PickingMode.Ignore });
        return button;
    }

    private void SetChipText(string chipName, string text, string dwell = null)
    {
        VisualElement chip = chipColumn.Q<VisualElement>(chipName);
        if (chip == null) return;
        if (text != null) { Label label = chip.Q<Label>("teks"); if (label != null) label.text = text; }
        if (dwell != null) { Label label = chip.Q<Label>("dwell"); if (label != null) label.text = dwell; }
    }

    // gerak.json chip-masuk: 200 ms, opacity 0→1, skala 0,6→1, out-back.
    private static void PlayChipEnter(VisualElement chip)
    {
        Tween(chip, 0.2f, t =>
        {
            float eased = Ease.OutBack(t);
            chip.style.opacity = Mathf.Clamp01(t * 1.6f);
            chip.style.scale = new Scale(Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased));
        });
    }

    // ---------- Toast ----------

    private void EnsureToast()
    {
        if (boundScreen.Q<VisualElement>("msg") != null) return;
        bool portrait = toolkitRoot.IsPortrait;
        var anchor = new VisualElement { name = "@msg", pickingMode = PickingMode.Ignore };
        anchor.AddToClassList("sc-abs");
        anchor.AddToClassList("sc-row");
        anchor.style.justifyContent = Justify.Center;
        anchor.style.alignItems = Align.Center;
        if (portrait) { anchor.style.left = 20; anchor.style.right = 20; anchor.style.top = 312; }
        else { anchor.style.left = 300; anchor.style.right = 316; anchor.style.bottom = 36; }
        var message = new VisualElement { name = "msg", pickingMode = PickingMode.Ignore };
        message.AddToClassList("dd-msg");
        var badge = new VisualElement { name = "badge", pickingMode = PickingMode.Ignore };
        badge.AddToClassList("dd-msg__badge");
        var glyph = new VisualElement { name = "ikon-info", pickingMode = PickingMode.Ignore };
        glyph.AddToClassList("dd-msg__glyph");
        glyph.AddToClassList("ikon");
        glyph.AddToClassList("ikon--i-info");
        badge.Add(glyph);
        message.Add(badge);
        message.Add(new Label { name = "teks", pickingMode = PickingMode.Ignore });
        anchor.Add(message);
        boundScreen.Add(anchor);
    }

    // Prioritas (hud.json pesan): pesan berwaktu → petunjuk tetap bantuan warga → tanpa toast.
    private void UpdateToast()
    {
        VisualElement message = boundScreen.Q<VisualElement>("msg");
        if (message == null) return;
        // Selama tutorial, kartu sorotan yang memberi petunjuk; toast hanya untuk pesan hasil (koin, surat sampai).
        string text = !string.IsNullOrEmpty(gameManager.MessageText) ? gameManager.MessageText
            : gameManager.SideQuests != null && !gameManager.GuideActive ? gameManager.SideQuests.Hint ?? string.Empty : string.Empty;
        // Selama sorotan, kartu sorotan yang bicara; toast menyusul sesudahnya (teks.json selesai.belum).
        if (gameManager.IsShowingDestination) text = string.Empty;
        Label label = message.Q<Label>("teks");
        if (text == toastText) return;
        bool first = toastText == null;
        bool wasShown = !string.IsNullOrEmpty(toastText);
        toastText = text;
        if (string.IsNullOrEmpty(text) && first)
        {
            message.style.display = DisplayStyle.None;
            return;
        }
        if (string.IsNullOrEmpty(text))
        {
            float direction = toolkitRoot.IsPortrait ? -8f : 8f;
            Tween(message, 0.16f, t =>
            {
                message.style.opacity = 1f - Ease.InCubic(t);
                message.style.translate = new Translate(0, direction * t);
                if (t >= 1f) message.style.display = DisplayStyle.None;
            });
            return;
        }
        if (label != null) label.text = text;
        ApplyToastKind(message, ToastKind(text));
        message.style.display = DisplayStyle.Flex;
        float from = toolkitRoot.IsPortrait ? -24f : 24f;
        Tween(message, wasShown ? 0.12f : 0.22f, t =>
        {
            message.style.opacity = Mathf.Clamp01(t * 1.4f);
            message.style.translate = new Translate(0, Mathf.LerpUnclamped(from, 0f, Ease.OutBack(t)));
        });
    }

    // teks.json pesan → jenis: good (hijau, centang), warn (kuning, !), selain itu info (biru, i).
    // Dijaga UiChecks.ValidateToastKinds terhadap semua pesan di teks.json.
    private static readonly string[] GoodPrefixes =
    {
        "Paket sampai!", "Berhasil!", "Dapat satu bintang", "Semua bintang terkumpul", "Pelindung paket!",
        "Sudah berhenti!", "Surat sampai!", "Siap jalan lagi!", "Boleh selesai!"
    };
    private static readonly string[] WarnPrefixes =
        { "Paket ekspres:", "Jangan menabrak!", "Bonus habis.", "Waktu bonus habis.", "Selesaikan pengantaran" };

    public static string ToastKind(string text)
    {
        if (string.IsNullOrEmpty(text)) return "info";
        foreach (string prefix in GoodPrefixes) if (text.StartsWith(prefix, StringComparison.Ordinal)) return "good";
        foreach (string prefix in WarnPrefixes) if (text.StartsWith(prefix, StringComparison.Ordinal)) return "warn";
        return "info";
    }

    private static void ApplyToastKind(VisualElement message, string kind)
    {
        message.EnableInClassList("dd-msg--good", kind == "good");
        message.EnableInClassList("dd-msg--warn", kind == "warn");
        VisualElement glyph = message.Q<VisualElement>(className: "dd-msg__glyph");
        if (glyph == null) return;
        glyph.EnableInClassList("ikon--i-check", kind == "good");
        glyph.EnableInClassList("ikon--i-warn", kind == "warn");
        glyph.EnableInClassList("ikon--i-info", kind == "info");
    }

    private static void Tween(VisualElement element, float duration, Action<float> apply)
    {
        float start = Time.unscaledTime;
        apply(0f);
        IVisualElementScheduledItem item = null;
        item = element.schedule.Execute(() =>
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / duration);
            apply(t);
            if (t >= 1f) item.Pause();
        }).Every(16);
    }
}
