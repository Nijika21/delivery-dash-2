using System.Collections;
using UnityEngine;

// Tampilan zona berhenti (Gerbang Desain 2, bentuk A) dan kapsul selesai kerja.
// Logika jeda tetap di StopZone; komponen ini hanya menggambar nilai yang sama (HANDOFF 14: "isi lingkar = progres/T").
// Gerak: zona-muncul (300 ms out-back), zona-isi (tepi terisi melingkar), zona-pop (lencana centang 330 ms), hilang 200 ms.
public sealed class StopZoneView : MonoBehaviour
{
    private static readonly RingShape PadRing = new RingShape(256f, 256f, 18f, 18f, 220f, 220f, 66f);
    private static readonly RingShape CapsuleRing = new RingShape(520f, 280f, 12f, 12f, 496f, 256f, 108f);

    private SpriteRenderer body;
    private SpriteRenderer badge;
    private Material material;
    private StopZone zone;
    private RingShape ring;
    private Vector3 fullScale;
    private float bornAt;
    // Kedip muncul (waktu nyata, tetap jalan saat sorotan membekukan waktu): <0 = tidak berkedip.
    private float blinkStart = -1f, blinkSeconds;
    // Muncul pelan tanpa membesar (kapsul saat mulai main): >0 = lama pudar masuk.
    private float fadeSeconds, fadeStart;
    private bool leaving;

    public bool Blinking => blinkStart >= 0f;

    public string Kind { get; private set; }
    public float Progress01 { get; private set; }
    public float FillAngle01 { get; private set; }
    public bool Completed { get; private set; }

    // Ukuran dunia per jenis (pratinjau Gerbang 3): paket 4,2; rumah 2,9; bantuan/surat/singgah 3,6; ulang tutorial 2,6.
    public static float PadSize(string kind) => kind == "paket" ? 4.2f : kind == "rumah" ? 2.9f
        : kind == "tutorial" ? DeliveryGameManager.TutorialPadSize : 3.6f;

    public static StopZoneView Create(DeliveryTown town, string kind, Vector2 center, float angle, StopZone logic)
    {
        bool capsule = kind == "kapsul";
        DeliveryContentCatalog catalog = town.Catalog;
        Sprite baseSprite = catalog.World(capsule ? "kapsul" : "zona-" + kind);
        Sprite fullSprite = catalog.World(capsule ? "kapsul-penuh" : "zona-" + kind + "-penuh");
        Vector2 size = capsule
            ? new Vector2(town.FinishSize.x * 520f / 496f, town.FinishSize.y * 280f / 256f)
            : Vector2.one * PadSize(kind);
        var go = new GameObject(capsule ? "Kapsul selesai kerja" : "Zona " + kind);
        go.transform.SetParent(town.transform, false);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        var view = go.AddComponent<StopZoneView>();
        view.Kind = kind;
        view.zone = logic;
        view.ring = capsule ? CapsuleRing : PadRing;
        view.body = go.AddComponent<SpriteRenderer>();
        view.body.sortingOrder = DeliveryTown.ZoneOrder;
        if (baseSprite != null)
        {
            view.body.sprite = baseSprite;
            Vector2 bounds = baseSprite.bounds.size;
            view.fullScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
            Shader shader = catalog.zoneFillShader != null ? catalog.zoneFillShader : Shader.Find("Delivery Dash/Zone Fill");
            if (shader != null && fullSprite != null)
            {
                view.material = new Material(shader) { name = "Isi zona " + kind };
                view.material.SetTexture("_FullTex", fullSprite.texture);
                view.material.SetVector("_Aspect", new Vector4(view.ring.Width, view.ring.Height, 0f, 0f));
                view.body.sharedMaterial = view.material;
            }
        }
        else view.fullScale = Vector3.one;
        Sprite check = capsule ? null : catalog.World("zona-" + kind + "-centang");
        if (check != null)
        {
            var badgeObject = new GameObject("Centang");
            badgeObject.transform.SetParent(go.transform, false);
            view.badge = badgeObject.AddComponent<SpriteRenderer>();
            view.badge.sprite = check;
            view.badge.sortingOrder = DeliveryTown.ZoneOrder + 1;
            view.badge.enabled = false;
            // Anak zona: skala induk sudah memetakan 256 px ke ukuran zona; putar balik supaya centang tegak.
            badgeObject.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
        view.bornAt = Time.time;
        view.transform.localScale = view.fullScale * 0.6f;
        view.SetAlpha(0f);
        view.SetProgress(0f);
        return view;
    }

    public void Bind(StopZone logic) => zone = logic;

    // "Di dalam zona" untuk ambil/antar: pusat truk di dalam garis tepi bantalan (220/256 sisi, sudut 66/256).
    public static bool InsidePad(Vector2 point, Vector2 center, float angle, string kind)
    {
        float size = PadSize(kind);
        return InsideRoundedRect(point, center, Vector2.one * size * 220f / 256f, size * 66f / 256f, angle);
    }

    public static bool InsideRoundedRect(Vector2 point, Vector2 center, Vector2 size, float radius, float angle)
    {
        Vector2 local = Quaternion.Euler(0f, 0f, -angle) * (point - center);
        Vector2 half = size * 0.5f;
        radius = Mathf.Min(radius, Mathf.Min(half.x, half.y));
        Vector2 q = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y)) - half + Vector2.one * radius;
        float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        return outside <= 0f;
    }

    // Progres = pecahan jeda (progres/T); tepi terisi sepanjang garis mulai dari tengah atas searah jarum jam.
    public void SetProgress(float progress01)
    {
        Progress01 = Mathf.Clamp01(progress01);
        FillAngle01 = ring.AngleAtArc(Progress01);
        if (material != null) material.SetFloat("_Progress", FillAngle01);
    }

    private void LateUpdate()
    {
        if (zone != null && !Completed) SetProgress(zone.Progress01);
        if (leaving || Completed) return;
        if (blinkStart >= 0f)
        {
            transform.localScale = fullScale;
            float blink = (Time.realtimeSinceStartup - blinkStart) / blinkSeconds;
            if (blink < 0f) { SetAlpha(0f); return; }
            // 3,5 gelombang: mulai tak terlihat, berkedip tiga kali, berakhir penuh.
            if (blink < 1f) { SetAlpha(0.5f - 0.5f * Mathf.Cos(blink * Mathf.PI * 7f)); return; }
            blinkStart = -1f;
        }
        if (fadeSeconds > 0f)
        {
            float fade = Mathf.Clamp01((Time.realtimeSinceStartup - fadeStart) / fadeSeconds);
            transform.localScale = fullScale;
            SetAlpha(Ease.InOutSine(fade));
            if (fade >= 1f) { fadeSeconds = 0f; bornAt = Time.time - 1f; }
            return;
        }
        float t = Mathf.Clamp01((Time.time - bornAt) / 0.3f);
        transform.localScale = fullScale * Mathf.LerpUnclamped(0.6f, 1f, Ease.OutBack(t));
        SetAlpha(t);
    }

    // Muncul dengan berkedip seperti baru lahir (kapsul selesai kerja). delay = tetap tak terlihat dulu (mis. menunggu
    // kamera sorot sampai). Sesudahnya tampil penuh tanpa animasi muncul biasa.
    public void Blink(float delay, float seconds)
    {
        fadeSeconds = 0f;
        blinkStart = Time.realtimeSinceStartup + delay;
        blinkSeconds = Mathf.Max(0.01f, seconds);
        bornAt = Time.time - 1f;
    }

    // Muncul dari transparan ke solid tanpa kedip dan tanpa membesar (waktu nyata). Dipakai kapsul saat mulai main.
    public void FadeIn(float seconds)
    {
        blinkStart = -1f;
        fadeSeconds = Mathf.Max(0.01f, seconds);
        fadeStart = Time.realtimeSinceStartup;
    }

    // Zona penuh: lencana centang pop, lalu zona hilang dan dihapus.
    public void Complete()
    {
        if (Completed) return;
        Completed = true;
        SetProgress(1f);
        transform.SetParent(null, true);
        StartCoroutine(CompleteRoutine());
    }

    // Zona tidak lagi jadi tujuan (tanpa selesai): langsung hilang.
    public void Dismiss()
    {
        if (leaving) return;
        if (!isActiveAndEnabled) { Destroy(gameObject); return; }
        StartCoroutine(Leave());
    }

    private IEnumerator CompleteRoutine()
    {
        transform.localScale = fullScale;
        SetAlpha(1f);
        if (badge != null)
        {
            badge.enabled = true;
            for (float time = 0f; time < 0.33f; time += Time.deltaTime)
            {
                float t = time / 0.33f;
                float scale = t < 0.45f ? Mathf.LerpUnclamped(0.6f, 1.15f, Ease.OutBack(t / 0.45f))
                    : Mathf.Lerp(1.15f, 1f, Ease.OutCubic((t - 0.45f) / 0.55f));
                badge.transform.localScale = Vector3.one * scale;
                badge.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.45f));
                yield return null;
            }
            badge.transform.localScale = Vector3.one;
            badge.color = Color.white;
        }
        yield return new WaitForSeconds(0.25f);
        yield return Leave();
    }

    private IEnumerator Leave()
    {
        leaving = true;
        Vector3 start = transform.localScale;
        // Waktu nyata: zona yang dibuang saat menu terbuka (timeScale 0, mis. ResetRun dari lobby) tetap hilang.
        for (float time = 0f; time < 0.2f; time += Time.unscaledDeltaTime)
        {
            float t = Ease.InCubic(time / 0.2f);
            transform.localScale = Vector3.Lerp(start, fullScale * 0.8f, t);
            SetAlpha(1f - t);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void SetAlpha(float alpha)
    {
        if (body != null) body.color = new Color(1f, 1f, 1f, alpha);
        if (badge != null && badge.enabled) badge.color = new Color(1f, 1f, 1f, Mathf.Min(badge.color.a, alpha));
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    // Garis tepi persegi bersudut bulat di ruang piksel gambar (y ke bawah), mulai tengah atas searah jarum jam.
    public readonly struct RingShape
    {
        public readonly float Width, Height;
        private readonly float x, y, w, h, r;

        public RingShape(float width, float height, float x, float y, float w, float h, float r)
        {
            Width = width; Height = height; this.x = x; this.y = y; this.w = w; this.h = h; this.r = r;
        }

        public float Length => 2f * (w - 2f * r) + 2f * (h - 2f * r) + 2f * Mathf.PI * r;

        // Pecahan panjang tepi → pecahan sudut (0 = atas, searah jarum jam) yang dipakai shader Zone Fill.
        public float AngleAtArc(float fraction)
        {
            if (fraction <= 0f) return 0f;
            if (fraction >= 1f) return 1f;
            Vector2 p = PointAt(fraction * Length);
            float dx = p.x - Width * 0.5f, dy = Height * 0.5f - p.y;
            float a = Mathf.Atan2(dx, dy) / (2f * Mathf.PI);
            if (a < 0f) a += 1f;
            // Dekat akhir putaran titik bisa jatuh tepat di 0; jangan membuat isi melompat kosong.
            return a < 0.0001f ? 1f : a;
        }

        private Vector2 PointAt(float s)
        {
            float cx = x + w * 0.5f, halfTop = w * 0.5f - r, side = h - 2f * r, bottom = w - 2f * r, arc = Mathf.PI * r * 0.5f;
            if (s <= halfTop) return new Vector2(cx + s, y);
            s -= halfTop;
            if (s <= arc) return Arc(x + w - r, y + r, -90f, s);
            s -= arc;
            if (s <= side) return new Vector2(x + w, y + r + s);
            s -= side;
            if (s <= arc) return Arc(x + w - r, y + h - r, 0f, s);
            s -= arc;
            if (s <= bottom) return new Vector2(x + w - r - s, y + h);
            s -= bottom;
            if (s <= arc) return Arc(x + r, y + h - r, 90f, s);
            s -= arc;
            if (s <= side) return new Vector2(x, y + h - r - s);
            s -= side;
            if (s <= arc) return Arc(x + r, y + r, 180f, s);
            s -= arc;
            return new Vector2(x + r + s, y);
        }

        private Vector2 Arc(float cx, float cy, float startDeg, float s)
        {
            float a = (startDeg * Mathf.Deg2Rad) + s / r;
            return new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
        }
    }
}

// Pelonggaran dari gerak.json (nama sama dengan CSS/easings.net).
public static class Ease
{
    public static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; t = Mathf.Clamp01(t) - 1f; return 1f + c3 * t * t * t + c1 * t * t; }
    public static float OutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
    public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
    public static float InSine(float t) => 1f - Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
    public static float OutSine(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
    public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) * 0.5f;
}
