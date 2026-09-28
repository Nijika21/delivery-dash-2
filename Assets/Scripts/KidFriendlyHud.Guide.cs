using UnityEngine;
using UnityEngine.UIElements;
using Step = DeliveryGameManager.GuideStep;
using Tip = DeliveryGameManager.GuideTip;

// Lapisan tutorial interaktif (uji HP 27 Sep: "spotlight menyorot dari full ke satu titik, jari jangan diam").
// Rangkuman acuan coach mark gim kasual seluler: layar meredup lalu lampu sorot menyempit dari seluruh layar ke target
// (0,7 dtk, out-cubic), lubang bernapas ±5 px dengan riak sonar, tangan bergerak sesuai aksinya (geser joystick, tahan
// tombol, menunjuk sambil bergoyang), dan kartu teks besar di sisi lubang yang tidak tertutup tangan. Sentuhan tembus
// (semua pickingMode Ignore), jadi pemain langsung mencoba. Untuk target di dunia, redup dilepas sesudah 1,6 dtk supaya
// jalan terlihat saat menyetir; lingkar, tangan, dan kartu tetap. Di luar tutorial, fitur yang baru pertama kali muncul
// (ekspres, rapuh, pelindung, istirahat, dst.) mendapat sorotan singkat sekali (GuideTip).
// Lapisan ini UIDocument sendiri di panel yang sama (sortingOrder lebih tinggi), jadi tidak ikut terhapus atau ikut
// dianimasikan saat layar HUD dikloning ulang.
public partial class KidFriendlyHud
{
    private enum GuideHand { None, Drag, Press, Point }

    private struct GuideFrame
    {
        public Rect Hole;
        public bool Round;
        public string Text, Icon;
        public GuideHand Hand;
        public int Key;
        public bool HoldDim, Tip;
        public int Milestone;
    }

    private const float GuideRevealSeconds = 0.7f, GuideDimAlpha = 0.62f, TipDimAlpha = 0.45f, TipSeconds = 5.5f;
    private const float HandTipX = 0.405f, HandTipY = 0.03f;
    private const int GuideMilestones = 7;

    private UIDocument guideDocument;
    private VisualElement guideLayer, guideHand, guideRipple, guideCard, guideCardIcon, guideDots;
    private Label guideCardText, guideCardBadge;
    private GuideSpotlight guideSpot;
    private int guideKey = int.MinValue;
    private float guideRevealAt, guideDimReleaseAt;
    private bool guideDimReleased, guideHoleReady, guideCardPlaced, guideQuiet;
    private int guideStepsSeen;
    private Rect guideHoleShown;
    private Vector2 guideCardPosition;
    private string guideCardIconClass;
    private Tip guideTip;
    private float guideTipAt;
    private int guideTipCount;
    private DeliveryMarker guideTipMarker;
    private DeliverySideQuests.Quest guideTipQuest;

    private void UpdateGuide()
    {
        if (toolkitRoot?.Root == null || gameManager?.Town == null || gameManager.Progress == null) return;
        bool canShow = page == Page.Game && IsHudScreen(toolkitScreen) && toolkitScreen != 18 && toolkitScreen != 19 &&
            !gameManager.IsShowingDestination && gameManager.Minimap?.Expanded != true;
        UpdateGuideTip(canShow);
        if (!gameManager.GuideActive) guideStepsSeen = 0;
        var frame = new GuideFrame();
        bool visible = canShow && (gameManager.GuideActive || guideTip != Tip.None) && EnsureGuideLayer() && BuildGuideFrame(ref frame);
        if (!visible)
        {
            if (guideLayer != null) guideLayer.style.display = DisplayStyle.None;
            guideKey = int.MinValue;
            return;
        }
        guideLayer.style.display = DisplayStyle.Flex;
        float now = Time.unscaledTime;
        if (frame.Key != guideKey)
        {
            // Langkah yang sudah pernah tampil (mis. kembali ke "ikuti peta" sesudah tiap bintang) muncul tanpa
            // layar gelap dan tanpa bunyi, supaya tidak mengganggu; cincin sorot, tangan, dan kartu tetap ada.
            int bit = frame.Tip ? 0 : 1 << (int)gameManager.Guide;
            guideQuiet = (guideStepsSeen & bit) != 0;
            guideStepsSeen |= bit;
            if (guideKey != int.MinValue && !guideQuiet) DeliveryAudio.Play(DeliveryAudio.Cue.Notice);
            guideKey = frame.Key;
            guideRevealAt = now;
            guideDimReleased = false;
            guideHoleReady = false;
            guideCardPlaced = false;
        }
        float age = now - guideRevealAt;
        SetGuideCard(frame);
        AnimateSpotlight(frame, age, now);
        AnimateHand(frame, age);
        PlaceGuideCard(frame, age);
    }

    // Tangan tutorial turunan Twemoji (CC-BY 4.0) wajib dikreditkan; templat layar 25 dibangkitkan, jadi baris ini
    // disisipkan saat layar diikat (sumber dan perubahan: ArtSource/tangan-tap.LICENSE.txt).
    private void AddGuideCredit()
    {
        VisualElement last = boundScreen?.Q<VisualElement>("small-3");
        if (last?.parent == null || boundScreen.Q<VisualElement>("small-twemoji") != null) return;
        var credit = new Label("Ikon tangan tutorial diadaptasi dari Twemoji, lisensi CC-BY 4.0.") { name = "small-twemoji", pickingMode = PickingMode.Ignore };
        credit.AddToClassList("sc-story__small");
        last.parent.Insert(last.parent.IndexOf(last) + 1, credit);
        // Kredit musik 2.0 (CC BY wajib; CC0 disebut sebagai terima kasih).
        string[] music =
        {
            "Musik lobby: “Fun Adventure” oleh HitCtrl, lisensi CC-BY 3.0 (opengameart.org).",
            "Musik jalan: “Seaside Village” oleh Leonardo Paz, lisensi CC-BY 4.0 (opengameart.org); “Forget Me Not” dan “Hot Springs Town” oleh Kistol (CC0).",
        };
        for (int i = 0; i < music.Length; i++)
        {
            var line = new Label(music[i]) { name = "small-musik-" + i, pickingMode = PickingMode.Ignore };
            line.AddToClassList("sc-story__small");
            credit.parent.Insert(credit.parent.IndexOf(credit) + 1 + i, line);
        }
    }

    // ---------- Lapisan ----------

    private bool EnsureGuideLayer()
    {
        if (guideDocument == null)
        {
            var host = new GameObject("Lapisan tutorial", typeof(UIDocument));
            host.transform.SetParent(transform, false);
            guideDocument = host.GetComponent<UIDocument>();
            guideDocument.sortingOrder = 100f;
            BuildGuideLayer();
        }
        PanelSettings panelSettings = toolkitRoot.GetComponent<UIDocument>().panelSettings;
        if (panelSettings == null) return false;
        if (guideDocument.panelSettings != panelSettings) guideDocument.panelSettings = panelSettings;
        VisualElement root = guideDocument.rootVisualElement;
        if (root == null) return false;
        if (guideLayer.parent != root)
        {
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            root.Add(guideLayer);
        }
        if (guideLayer.styleSheets.count == 0) AdoptGuideStyles();
        // Ukuran dari akar dokumen: lapisan sendiri berukuran 0 selama disembunyikan (display None).
        return guideLayer.panel != null && GuideArea.width > 0f && GuideArea.height > 0f;
    }

    private Rect GuideArea => guideDocument.rootVisualElement.layout;

    private VisualElement GuideSpace => guideDocument.rootVisualElement;

    // Lembar gaya D5 (kartu, huruf Fredoka, kelas ikon) menempel di elemen teratas templat HUD; salin ke lapisan ini.
    private void AdoptGuideStyles()
    {
        void Take(VisualElement element)
        {
            if (element == null) return;
            for (int i = 0; i < element.styleSheets.count; i++)
                if (!guideLayer.styleSheets.Contains(element.styleSheets[i])) guideLayer.styleSheets.Add(element.styleSheets[i]);
        }
        VisualElement source = toolkitRoot.Root;
        Take(source);
        foreach (VisualElement child in source.Children()) Take(child);
        Take(source.Q<VisualElement>("layar"));
    }

    private void BuildGuideLayer()
    {
        guideLayer = new VisualElement { name = "tutorial", pickingMode = PickingMode.Ignore };
        Fill(guideLayer);
        guideSpot = new GuideSpotlight();
        guideLayer.Add(guideSpot);

        guideRipple = new VisualElement { name = "riak", pickingMode = PickingMode.Ignore };
        guideRipple.style.position = Position.Absolute;
        SetBorder(guideRipple, 5f, Color.white);
        guideLayer.Add(guideRipple);

        guideHand = new VisualElement { name = "tangan", pickingMode = PickingMode.Ignore };
        guideHand.AddToClassList("ikon");
        guideHand.AddToClassList("ikon--i-tap");
        guideHand.style.position = Position.Absolute;
        guideHand.style.transformOrigin = new TransformOrigin(Length.Percent(HandTipX * 100f), Length.Percent(HandTipY * 100f), 0f);
        guideLayer.Add(guideHand);

        guideCard = new VisualElement { name = "kartu", pickingMode = PickingMode.Ignore };
        guideCard.AddToClassList("dd-card");
        guideCard.style.position = Position.Absolute;
        guideCard.style.paddingLeft = 20; guideCard.style.paddingRight = 24;
        guideCard.style.paddingTop = 16; guideCard.style.paddingBottom = 16;
        guideCardBadge = new Label("BARU!") { pickingMode = PickingMode.Ignore };
        guideCardBadge.AddToClassList("dd-card__title");
        guideCardBadge.style.alignSelf = Align.FlexStart;
        guideCardBadge.style.fontSize = 18;
        guideCardBadge.style.color = Color.white;
        guideCardBadge.style.backgroundColor = (Color)new Color32(0xF0, 0x9A, 0x2C, 255);
        guideCardBadge.style.paddingLeft = 12; guideCardBadge.style.paddingRight = 12;
        guideCardBadge.style.paddingTop = 2; guideCardBadge.style.paddingBottom = 2;
        SetRadius(guideCardBadge, 14f);
        guideCardBadge.style.marginBottom = 8;
        guideCard.Add(guideCardBadge);
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        guideCardIcon = new VisualElement { pickingMode = PickingMode.Ignore };
        guideCardIcon.style.width = 60; guideCardIcon.style.height = 60;
        guideCardIcon.style.flexShrink = 0;
        guideCardIcon.style.marginRight = 16;
        row.Add(guideCardIcon);
        guideCardText = new Label { pickingMode = PickingMode.Ignore };
        guideCardText.AddToClassList("dd-card__title");
        guideCardText.style.whiteSpace = WhiteSpace.Normal;
        guideCardText.style.flexShrink = 1;
        guideCardText.style.flexGrow = 1;
        row.Add(guideCardText);
        guideCard.Add(row);
        guideDots = new VisualElement { pickingMode = PickingMode.Ignore };
        guideDots.style.flexDirection = FlexDirection.Row;
        guideDots.style.justifyContent = Justify.Center;
        guideDots.style.marginTop = 12;
        for (int i = 0; i < GuideMilestones; i++)
        {
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.style.width = 14; dot.style.height = 14;
            dot.style.marginLeft = 4; dot.style.marginRight = 4;
            SetRadius(dot, 7f);
            guideDots.Add(dot);
        }
        guideCard.Add(guideDots);
        guideLayer.Add(guideCard);
    }

    private static void Fill(VisualElement element)
    {
        element.style.position = Position.Absolute;
        element.style.left = 0; element.style.top = 0; element.style.right = 0; element.style.bottom = 0;
    }

    private static void SetBorder(VisualElement element, float width, Color color)
    {
        element.style.borderTopWidth = width; element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width; element.style.borderRightWidth = width;
        element.style.borderTopColor = color; element.style.borderBottomColor = color;
        element.style.borderLeftColor = color; element.style.borderRightColor = color;
    }

    // ---------- Isi tiap langkah ----------

    private bool BuildGuideFrame(ref GuideFrame frame)
    {
        if (gameManager.GuideActive) return BuildStepFrame(ref frame);
        return BuildTipFrame(ref frame);
    }

    private bool BuildStepFrame(ref GuideFrame f)
    {
        bool portrait = toolkitRoot.IsPortrait;
        Step step = gameManager.Guide;
        f.Key = gameManager.GuideVersion;
        f.Milestone = Milestone(step);
        f.Hand = GuideHand.Point;
        f.Round = true;
        switch (step)
        {
            case Step.Drive:
                if (portrait)
                {
                    f.Text = "Tahan jempol di lingkaran ini, lalu geser untuk menyetir";
                    f.Icon = "ikon--i-tap";
                    f.Hand = GuideHand.Drag;
                    f.HoldDim = true;
                    return UiHole("joy", 18f, ref f.Hole);
                }
                f.Text = "Tahan GAS untuk jalan. Tombol panah untuk belok";
                f.Icon = "ikon--i-gas";
                f.Hand = GuideHand.Press;
                f.HoldDim = true;
                f.Round = false;
                return UiHole("ctl:gas", 14f, ref f.Hole);
            case Step.ToDepot:
                f.Text = "Setir ke kotak jingga di depan gudang";
                f.Icon = "a-zona-paket";
                break;
            case Step.StopAtDepot:
                f.Text = portrait ? "Berhenti di sini! Lepas jempol sampai kotaknya penuh" : "Berhenti di sini! Lepas GAS sampai kotaknya penuh";
                f.Icon = "a-zona-paket";
                f.Hand = GuideHand.None;
                break;
            case Step.FollowRoute:
                bool toStop = gameManager.Activities != null && gameManager.Activities.HasStopover;
                bool map = gameManager.Minimap != null && gameManager.Minimap.Visible;
                f.Text = map ? (toStop ? "Lihat peta kecil: ikuti garisnya ke kotak singgah hijau" : "Lihat peta kecil: ikuti garisnya ke rumah tujuan")
                    : toStop ? "Ikuti panah ke kotak singgah hijau" : "Ikuti panah biru ke rumah tujuan";
                f.Icon = map ? "ikon--i-map" : "ikon--i-house";
                return NavigationHole(ref f);
            case Step.Star:
                f.Text = "Bintang! Lewati untuk dapat +1 koin";
                f.Icon = "ikon--i-star";
                break;
            case Step.Boost:
                f.Text = "Kilat! Lewati supaya truk ngebut 4 detik";
                f.Icon = "ikon--i-bolt";
                break;
            case Step.Stopover:
                f.Text = "Kotak singgah! Berhenti di kotak hijau sampai penuh. Bonus +5 koin saat paket sampai";
                f.Icon = "a-zona-singgah";
                break;
            case Step.StopAtHouse:
                f.Text = "Sudah dekat! Berhenti di kotak biru depan rumah sampai penuh";
                f.Icon = "ikon--i-house";
                break;
            case Step.HelpZone:
                f.Text = "Warga minta tolong! Berhenti di kotak kuning untuk ambil surat";
                f.Icon = "a-zona-bantuan";
                break;
            case Step.Mailbox:
                f.Text = "Antar suratnya ke kotak surat depan gudang, berhenti sampai penuh";
                f.Icon = "a-zona-surat";
                break;
            case Step.ToFinish:
                f.Text = "Hebat! Terakhir, pulang ke kapsul biru dan berhenti di sana";
                f.Icon = "ikon--i-park";
                break;
            case Step.FinishInfo:
                f.Text = "Mau selesai main? Berhenti di kapsul biru ini. Sekarang main bebas!";
                f.Icon = "ikon--i-park";
                break;
            case Step.Done:
                f.Text = "Tutorial selesai! Kamu siap jadi kurir hebat!";
                f.Icon = "ikon--i-check";
                f.Hand = GuideHand.None;
                break;
            default:
                return false;
        }
        Vector2? target = gameManager.GuideWorldTarget;
        if (target.HasValue && WorldHole(target.Value, gameManager.GuideWorldRadius, ref f.Hole)) return true;
        // Target di luar layar: sorot petunjuk arahnya (peta kecil atau panah), kartu tetap sama.
        return NavigationHole(ref f);
    }

    private static int Milestone(Step step)
    {
        switch (step)
        {
            case Step.Drive: return 0;
            case Step.ToDepot: case Step.StopAtDepot: return 1;
            case Step.FollowRoute: case Step.Star: case Step.Boost: case Step.Stopover: return 2;
            case Step.StopAtHouse: return 3;
            case Step.HelpZone: return 4;
            case Step.Mailbox: return 5;
            default: return 6;
        }
    }

    private bool NavigationHole(ref GuideFrame f)
    {
        if (gameManager.Minimap != null && gameManager.Minimap.Visible && UiHole("mini", 10f, ref f.Hole))
        {
            f.Round = false;
            return true;
        }
        return gameManager.ArrowShown && WorldHole(gameManager.ArrowPosition, 1.6f, ref f.Hole);
    }

    private bool UiHole(string elementName, float pad, ref Rect hole)
    {
        VisualElement element = boundScreen?.Q<VisualElement>(elementName);
        if (element == null || element.panel == null || element.resolvedStyle.display == DisplayStyle.None) return false;
        Rect bounds = element.worldBound;
        if (float.IsNaN(bounds.width) || bounds.width <= 0f) return false;
        // Panel sama dengan HUD: koordinat dunia panel langsung dipetakan ke lapisan.
        Rect local = GuideSpace.WorldToLocal(bounds);
        hole = new Rect(local.x - pad, local.y - pad, local.width + pad * 2f, local.height + pad * 2f);
        return true;
    }

    private bool WorldHole(Vector2 world, float worldRadius, ref Rect hole)
    {
        Camera camera = Camera.main;
        if (camera == null || guideLayer.panel == null) return false;
        Vector2 center = WorldToLayer(camera, world);
        float radius = Vector2.Distance(center, WorldToLayer(camera, world + new Vector2(worldRadius, 0f)));
        Rect layer = GuideArea;
        // Pusat di dalam layar sudah cukup: setidaknya seperempat lingkaran terlihat (kotak di tepi layar tetap disorot).
        if (center.x < 0f || center.y < 0f || center.x > layer.width || center.y > layer.height) return false;
        radius = Mathf.Clamp(radius, 48f, 190f);
        hole = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        return true;
    }

    private Vector2 WorldToLayer(Camera camera, Vector2 world)
    {
        Vector3 screen = camera.WorldToScreenPoint(world);
        Vector2 panel = RuntimePanelUtils.ScreenToPanel(guideLayer.panel, new Vector2(screen.x, Screen.height - screen.y));
        return GuideSpace.WorldToLocal(panel);
    }

    // ---------- Petunjuk sekali pakai di luar tutorial ----------

    private void UpdateGuideTip(bool canShow)
    {
        if (guideTip != Tip.None)
        {
            // Waktu tampil hanya berjalan selama terlihat (sorotan rumah atau menu menundanya).
            if (!canShow) { guideTipAt += Time.unscaledDeltaTime; return; }
            if (Time.unscaledTime - guideTipAt > TipSeconds || !TipStillValid()) guideTip = Tip.None;
            return;
        }
        if (!canShow || gameManager.GuideActive) return;
        Tip next = NextTip();
        if (next == Tip.None) return;
        gameManager.MarkTip(next);
        guideTip = next;
        guideTipAt = Time.unscaledTime;
        guideTipCount++;
    }

    private Tip NextTip()
    {
        DeliveryActivities activities = gameManager.Activities;
        guideTipMarker = null;
        guideTipQuest = null;
        if (activities != null && gameManager.Carrying)
        {
            if (activities.IsExpress && activities.ExpressRunning && !gameManager.TipSeen(Tip.Express)) return Tip.Express;
            if (activities.IsFragile && !gameManager.TipSeen(Tip.Fragile)) return Tip.Fragile;
            if (activities.HasStopover && !gameManager.TipSeen(Tip.Stopover)) return Tip.Stopover;
            foreach (Tip tip in new[] { Tip.Shield, Tip.Star, Tip.Boost })
            {
                if (gameManager.TipSeen(tip)) continue;
                guideTipMarker = NearestPickup(tip == Tip.Shield ? DeliveryMarkerKind.Shield : tip == Tip.Star ? DeliveryMarkerKind.Star
                    : DeliveryMarkerKind.Boost);
                if (guideTipMarker != null) return tip;
            }
        }
        foreach (Tip tip in new[] { Tip.Rest, Tip.Letter })
        {
            if (gameManager.TipSeen(tip)) continue;
            guideTipQuest = NearestQuest(tip == Tip.Rest ? DeliverySideQuests.Kind.Rest : DeliverySideQuests.Kind.Letter);
            if (guideTipQuest != null) return tip;
        }
        return Tip.None;
    }

    private bool TipStillValid()
    {
        DeliveryActivities activities = gameManager.Activities;
        switch (guideTip)
        {
            case Tip.Express: return activities != null && activities.IsExpress && activities.ExpressRunning && gameManager.Carrying;
            case Tip.Fragile: return activities != null && activities.IsFragile && gameManager.Carrying;
            case Tip.Stopover: return activities != null && activities.HasStopover && gameManager.Carrying;
            case Tip.Shield: case Tip.Star: case Tip.Boost: return guideTipMarker != null;
            case Tip.Rest: case Tip.Letter: return guideTipQuest != null && !guideTipQuest.Completed && !guideTipQuest.Collected;
            default: return false;
        }
    }

    private DeliveryMarker NearestPickup(DeliveryMarkerKind kind)
    {
        DeliveryMarker best = null;
        float bestDistance = 7f;
        Vector2 truck = driver.transform.position;
        foreach (DeliveryMarker pickup in gameManager.Activities.Pickups)
        {
            if (pickup == null || pickup.Kind != kind) continue;
            float distance = Vector2.Distance(truck, pickup.transform.position);
            if (distance < bestDistance) { bestDistance = distance; best = pickup; }
        }
        return best;
    }

    private DeliverySideQuests.Quest NearestQuest(DeliverySideQuests.Kind kind)
    {
        if (gameManager.SideQuests == null) return null;
        DeliverySideQuests.Quest best = null;
        float bestDistance = 7f;
        Vector2 truck = driver.transform.position;
        foreach (DeliverySideQuests.Quest quest in gameManager.SideQuests.Quests)
        {
            if (quest.Completed || quest.Collected || quest.Type != kind) continue;
            float distance = Vector2.Distance(truck, quest.Position);
            if (distance < bestDistance) { bestDistance = distance; best = quest; }
        }
        return best;
    }

    private bool BuildTipFrame(ref GuideFrame f)
    {
        f.Key = 1000000 + guideTipCount;
        f.Tip = true;
        f.Milestone = -1;
        f.Hand = GuideHand.Point;
        f.Round = true;
        switch (guideTip)
        {
            case Tip.Express:
                f.Text = "Paket ekspres! Antar sebelum waktunya habis, bonus +5 koin";
                f.Icon = "ikon--i-bolt";
                f.Round = false;
                return UiHole("chip:bolt", 8f, ref f.Hole);
            case Tip.Fragile:
                f.Text = "Paket rapuh! Jangan menabrak. Tiga kali menabrak, bonusnya hilang";
                f.Icon = "ikon--i-crack";
                f.Round = false;
                return UiHole("chip:crack", 8f, ref f.Hole);
            case Tip.Stopover:
                f.Text = "Bonus singgah! Berhenti di kotak hijau dulu, lalu antar paket. +5 koin";
                f.Icon = "a-zona-singgah";
                if (WorldHole(gameManager.Activities.BonusPosition, 2.2f, ref f.Hole)) return true;
                f.Round = false;
                return UiHole("chip:rest", 8f, ref f.Hole);
            case Tip.Shield:
                f.Text = "Ambil perisai! Satu tabrakan jadi tidak dihitung";
                f.Icon = "ikon--i-shield";
                return guideTipMarker != null && WorldHole(guideTipMarker.transform.position, 1.4f, ref f.Hole);
            case Tip.Star:
                f.Text = "Lewati bintang untuk +1 koin. Kumpulkan semua, bonus +2!";
                f.Icon = "ikon--i-star";
                return guideTipMarker != null && WorldHole(guideTipMarker.transform.position, 1.4f, ref f.Hole);
            case Tip.Boost:
                f.Text = "Lewati kilat supaya truk ngebut 4 detik!";
                f.Icon = "ikon--i-bolt";
                return guideTipMarker != null && WorldHole(guideTipMarker.transform.position, 1.4f, ref f.Hole);
            case Tip.Rest:
                f.Text = "Kotak kuning istirahat: berhenti sampai penuh, dapat +4 koin dan ngebut!";
                f.Icon = "ikon--i-rest";
                return guideTipQuest != null && WorldHole(guideTipQuest.Position, 2.2f, ref f.Hole);
            case Tip.Letter:
                f.Text = "Warga minta tolong! Berhenti di kotak kuning, lalu antar suratnya ke kotak surat";
                f.Icon = "a-zona-bantuan";
                return guideTipQuest != null && WorldHole(guideTipQuest.Position, 2.2f, ref f.Hole);
            default:
                return false;
        }
    }

    // ---------- Gerak ----------

    private void AnimateSpotlight(GuideFrame frame, float age, float now)
    {
        float follow = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f);
        guideHoleShown = guideHoleReady ? LerpRect(guideHoleShown, frame.Hole, follow) : frame.Hole;
        guideHoleReady = true;
        Rect layer = GuideArea;
        float reveal = Ease.OutCubic(age / GuideRevealSeconds);
        // Mulai dari lingkaran selebar layar di sekitar target, menyempit ke target.
        float big = new Vector2(layer.width, layer.height).magnitude * 1.1f;
        var start = new Rect(guideHoleShown.center - Vector2.one * big, Vector2.one * big * 2f);
        Rect hole = LerpRect(start, guideHoleShown, reveal);
        if (age > GuideRevealSeconds) hole = Grow(hole, 5f * Mathf.Sin((age - GuideRevealSeconds) * Mathf.PI * 2f / 1.4f));
        float half = Mathf.Min(hole.width, hole.height) * 0.5f;
        float radius = frame.Round ? half : Mathf.Lerp(half, Mathf.Min(24f, half), reveal);

        float dim = guideQuiet ? 0f : (frame.Tip ? TipDimAlpha : GuideDimAlpha) * Mathf.Clamp01(age / 0.25f);
        bool release = frame.HoldDim ? joystickPointer >= 0 || toolkitGas || toolkitLeft || toolkitRight
            : age >= (frame.Tip ? 1.3f : 1.6f);
        if (release && !guideDimReleased) { guideDimReleased = true; guideDimReleaseAt = now; }
        if (guideDimReleased) dim *= 1f - Mathf.Clamp01((now - guideDimReleaseAt) / 0.4f);

        guideSpot.Hole = hole;
        guideSpot.Radius = radius;
        guideSpot.Dim = dim;
        guideSpot.RingAlpha = Mathf.Clamp01((age - 0.3f) / 0.3f);
        guideSpot.Sonar = age > GuideRevealSeconds ? (age - GuideRevealSeconds) % 1.3f / 1.3f : -1f;
        guideSpot.MarkDirtyRepaint();
    }

    private float GuideHandSize => toolkitRoot.IsPortrait ? 116f : 100f;

    private bool HandBelow(Rect hole) => hole.yMax + GuideHandSize + 24f < GuideArea.height - 16f;

    private void AnimateHand(GuideFrame frame, float age)
    {
        float appear = Mathf.Clamp01((age - 0.45f) / 0.35f);
        if (frame.Hand == GuideHand.None || appear <= 0f)
        {
            guideHand.style.display = DisplayStyle.None;
            guideRipple.style.display = DisplayStyle.None;
            return;
        }
        guideHand.style.display = DisplayStyle.Flex;
        float size = GuideHandSize;
        Rect hole = guideHoleShown;
        Vector2 center = hole.center;
        Vector2 tip = center;
        float rotation = 0f, press = 1f, ripple = -1f;
        float t = age - 0.45f;
        switch (frame.Hand)
        {
            case GuideHand.Drag:
            {
                // Tekan di tengah, geser ke depan, belok sedikit, kembali, angkat, jeda (1,8 dtk).
                float c = t % 1.8f;
                var forward = new Vector2(0f, -72f);
                var turn = new Vector2(52f, -52f);
                if (c < 0.2f) { press = Mathf.Lerp(1f, 0.88f, c / 0.2f); ripple = c / 0.5f; }
                else if (c < 0.7f) { press = 0.88f; tip = center + forward * Ease.OutCubic((c - 0.2f) / 0.5f); ripple = c / 0.5f; }
                else if (c < 1.0f) { press = 0.88f; tip = center + Vector2.Lerp(forward, turn, Ease.InOutSine((c - 0.7f) / 0.3f)); }
                else if (c < 1.3f) { press = 0.88f; tip = center + turn * (1f - Ease.InOutSine((c - 1.0f) / 0.3f)); }
                else if (c < 1.5f) press = Mathf.Lerp(0.88f, 1f, (c - 1.3f) / 0.2f);
                break;
            }
            case GuideHand.Press:
            {
                // Tahan tombol: turun dan mengecil, riak menyebar, tahan, lepas (1,4 dtk).
                float c = t % 1.4f;
                float down = c < 0.18f ? c / 0.18f : c < 0.9f ? 1f : c < 1.1f ? 1f - (c - 0.9f) / 0.2f : 0f;
                press = Mathf.Lerp(1f, 0.88f, down);
                tip = center + new Vector2(0f, 8f * down);
                if (c >= 0.18f && c < 0.9f) ripple = (c - 0.18f) / 0.72f;
                break;
            }
            default:
            {
                // Menunjuk dari bawah lubang (atau dari atas kalau dekat tepi bawah), mengangguk ke target sambil bergoyang.
                bool below = HandBelow(hole);
                float gap = 8f + 14f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 0.9f));
                tip = below ? new Vector2(center.x, hole.yMax + gap) : new Vector2(center.x, hole.yMin - gap);
                rotation = (below ? 0f : 180f) + 6f * Mathf.Sin(t * Mathf.PI * 2f / 1.1f);
                break;
            }
        }
        float scale = press * Mathf.LerpUnclamped(0.6f, 1f, Ease.OutBack(appear));
        guideHand.style.width = size;
        guideHand.style.height = size;
        guideHand.style.left = tip.x - HandTipX * size;
        guideHand.style.top = tip.y - HandTipY * size;
        guideHand.style.rotate = new Rotate(Angle.Degrees(rotation));
        guideHand.style.scale = new Scale(new Vector3(scale, scale, 1f));
        guideHand.style.opacity = appear;

        if (ripple < 0f || ripple > 1f) { guideRipple.style.display = DisplayStyle.None; return; }
        float rippleSize = Mathf.Lerp(30f, 130f, Ease.OutCubic(ripple));
        guideRipple.style.display = DisplayStyle.Flex;
        guideRipple.style.width = rippleSize;
        guideRipple.style.height = rippleSize;
        guideRipple.style.left = tip.x - rippleSize * 0.5f;
        guideRipple.style.top = tip.y - rippleSize * 0.5f;
        SetRadius(guideRipple, rippleSize * 0.5f);
        guideRipple.style.opacity = 0.9f * (1f - ripple);
    }

    // ---------- Kartu ----------

    private void SetGuideCard(GuideFrame frame)
    {
        if (guideCardText.text != frame.Text) guideCardText.text = frame.Text;
        guideCardText.style.fontSize = toolkitRoot.IsPortrait ? 30 : 26;
        if (guideCardIconClass != frame.Icon)
        {
            guideCardIconClass = frame.Icon;
            guideCardIcon.ClearClassList();
            guideCardIcon.AddToClassList(frame.Icon.StartsWith("ikon--", System.StringComparison.Ordinal) ? "ikon" : "sc-bg");
            guideCardIcon.AddToClassList(frame.Icon);
            // Ikon GAS dan centang berwarna putih untuk tombol hijau; di kartu putih diwarnai hijau tombolnya.
            guideCardIcon.style.unityBackgroundImageTintColor = frame.Icon == "ikon--i-gas" || frame.Icon == "ikon--i-check"
                ? (Color)new Color32(0x3E, 0x9A, 0x40, 255) : Color.white;
        }
        guideCardBadge.style.display = frame.Tip ? DisplayStyle.Flex : DisplayStyle.None;
        guideDots.style.display = frame.Milestone >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
        for (int i = 0; i < guideDots.childCount; i++)
        {
            bool done = i < frame.Milestone, current = i == frame.Milestone;
            guideDots[i].style.backgroundColor = (Color)(done ? new Color32(0x3F, 0x8F, 0x3E, 255)
                : current ? new Color32(0xF0, 0x9A, 0x2C, 255) : new Color32(0xDC, 0xE7, 0xE9, 255));
            float dot = current ? 18f : 14f;
            guideDots[i].style.width = dot;
            guideDots[i].style.height = dot;
            SetRadius(guideDots[i], dot * 0.5f);
        }
    }

    private void PlaceGuideCard(GuideFrame frame, float age)
    {
        Rect layer = GuideArea;
        bool portrait = toolkitRoot.IsPortrait;
        float width = portrait ? Mathf.Min(layer.width - 40f, 560f) : Mathf.Min(520f, layer.width * 0.42f);
        guideCard.style.width = width;
        float height = guideCard.resolvedStyle.height;
        if (float.IsNaN(height) || height <= 0f) height = 140f;
        Rect hole = guideHoleShown;
        float top = portrait ? 118f : 96f, bottom = 24f, hand = GuideHandSize;
        float y;
        if (frame.Hand == GuideHand.Drag || frame.Hand == GuideHand.Press) y = hole.yMin - 40f - height;
        else if (frame.Hand == GuideHand.Point && HandBelow(hole))
            y = hole.yMin - 28f - height >= top ? hole.yMin - 28f - height : hole.yMax + hand + 28f;
        else if (frame.Hand == GuideHand.Point)
            y = hole.yMax + 28f + height <= layer.height - bottom ? hole.yMax + 28f : hole.yMin - hand - 28f - height;
        else y = hole.yMax + 28f + height <= layer.height - bottom ? hole.yMax + 28f : hole.yMin - 28f - height;
        float maxY = Mathf.Max(top, layer.height - height - bottom);
        y = Mathf.Clamp(y, top, maxY);
        float x = Mathf.Clamp(hole.center.x - width * 0.5f, 16f, Mathf.Max(16f, layer.width - width - 16f));
        // Layar pendek (mendatar): kalau kartu menutupi lubang atau tangan, taruh di samping lubang.
        Rect keep = hole;
        if (frame.Hand == GuideHand.Point)
            keep = HandBelow(hole) ? Rect.MinMaxRect(hole.xMin, hole.yMin, hole.xMax, hole.yMax + hand + 22f)
                : Rect.MinMaxRect(hole.xMin, hole.yMin - hand - 22f, hole.xMax, hole.yMax);
        if (new Rect(x, y, width, height).Overlaps(keep))
        {
            if (keep.xMax + 28f + width <= layer.width - 16f) x = keep.xMax + 28f;
            else if (keep.xMin - 28f - width >= 16f) x = keep.xMin - 28f - width;
            y = Mathf.Clamp(hole.center.y - height * 0.5f, top, maxY);
        }
        var target = new Vector2(x, y);
        guideCardPosition = guideCardPlaced ? Vector2.Lerp(guideCardPosition, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f)) : target;
        guideCardPlaced = true;
        guideCard.style.left = guideCardPosition.x;
        guideCard.style.top = guideCardPosition.y;
        // Muncul sesudah lampu sorot hampir sampai (out-back), lalu mengambang pelan.
        float appear = Mathf.Clamp01((age - 0.3f) / 0.3f);
        float scale = Mathf.LerpUnclamped(0.85f, 1f, Ease.OutBack(appear));
        guideCard.style.opacity = appear;
        guideCard.style.scale = new Scale(new Vector3(scale, scale, 1f));
        guideCard.style.translate = new Translate(0f, 3f * Mathf.Sin(age * Mathf.PI * 2f / 2.4f));
    }

    private static Rect LerpRect(Rect a, Rect b, float t) => Rect.MinMaxRect(
        Mathf.LerpUnclamped(a.xMin, b.xMin, t), Mathf.LerpUnclamped(a.yMin, b.yMin, t),
        Mathf.LerpUnclamped(a.xMax, b.xMax, t), Mathf.LerpUnclamped(a.yMax, b.yMax, t));

    private static Rect Grow(Rect rect, float amount) =>
        new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
}

// Redup layar penuh dengan lubang (isi aturan ganjil-genap: persegi layar + lubang bersudut bulat), garis putih di tepi
// lubang, dan riak sonar kuning yang melebar.
public sealed class GuideSpotlight : VisualElement
{
    public Rect Hole;
    public float Radius, Dim, RingAlpha;
    public float Sonar = -1f;

    public GuideSpotlight()
    {
        pickingMode = PickingMode.Ignore;
        style.position = Position.Absolute;
        style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
        generateVisualContent += Draw;
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect area = contentRect;
        if (area.width <= 0f || area.height <= 0f) return;
        Painter2D painter = context.painter2D;
        if (Dim > 0.003f)
        {
            painter.fillColor = new Color(0.03f, 0.08f, 0.1f, Dim);
            painter.BeginPath();
            painter.MoveTo(new Vector2(area.xMin, area.yMin));
            painter.LineTo(new Vector2(area.xMax, area.yMin));
            painter.LineTo(new Vector2(area.xMax, area.yMax));
            painter.LineTo(new Vector2(area.xMin, area.yMax));
            painter.ClosePath();
            RoundRect(painter, Hole, Radius);
            painter.Fill(FillRule.OddEven);
        }
        if (RingAlpha <= 0.003f) return;
        painter.lineWidth = 6f;
        painter.strokeColor = new Color(1f, 1f, 1f, RingAlpha);
        painter.BeginPath();
        RoundRect(painter, Expand(Hole, 3f), Radius + 3f);
        painter.Stroke();
        if (Sonar < 0f) return;
        float grow = 6f + Sonar * 46f;
        painter.lineWidth = 1f + 5f * (1f - Sonar);
        painter.strokeColor = new Color(1f, 0.84f, 0.3f, RingAlpha * (1f - Sonar));
        painter.BeginPath();
        RoundRect(painter, Expand(Hole, grow), Radius + grow);
        painter.Stroke();
    }

    private static Rect Expand(Rect rect, float amount) =>
        new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);

    private static void RoundRect(Painter2D painter, Rect rect, float radius)
    {
        radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
        painter.ArcTo(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), radius);
        painter.ArcTo(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), radius);
        painter.ArcTo(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), radius);
        painter.ArcTo(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), radius);
        painter.ClosePath();
    }
}
