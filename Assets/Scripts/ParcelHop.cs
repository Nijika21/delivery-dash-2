using UnityEngine;

// Gerak paket-masuk (gudang → truk) dan paket-keluar (truk → pintu rumah), 630 ms, dari gerak.json.
// Paket tidak pernah ditaruh di atas truk (keputusan Gerbang Desain 2); status membawa paket ada di HUD.
public sealed class ParcelHop : MonoBehaviour
{
    public const float Duration = 0.63f;
    public const float ParcelHeight = 0.9f;

    private struct Key { public float T, V; public System.Func<float, float> E; public Key(float t, float v, System.Func<float, float> e = null) { T = t; V = v; E = e; } }

    private static readonly System.Func<float, float> Linear = t => t;
    private static readonly Key[] InLerp = { new Key(0, 0, Ease.OutCubic), new Key(0.429f, 0.35f, Ease.InSine), new Key(0.81f, 1, Ease.InCubic), new Key(1, 1) };
    private static readonly Key[] InLift = { new Key(0, 0, Ease.OutCubic), new Key(0.429f, 0.6f, Ease.InSine), new Key(0.81f, 0, Linear), new Key(1, 0) };
    private static readonly Key[] InScale = { new Key(0, 0.6f, Ease.OutCubic), new Key(0.143f, 1.1f, Ease.OutSine), new Key(0.429f, 1.3f, Ease.InSine), new Key(0.81f, 1, Ease.InCubic), new Key(1, 0.3f) };
    private static readonly Key[] OutLerp = { new Key(0, 0, Ease.OutCubic), new Key(0.5f, 0.65f, Ease.InSine), new Key(0.81f, 1, Ease.InCubic), new Key(1, 1) };
    private static readonly Key[] OutLift = { new Key(0, 0, Ease.OutCubic), new Key(0.5f, 0.6f, Ease.InSine), new Key(0.81f, 0, Linear), new Key(1, 0) };
    private static readonly Key[] OutScale = { new Key(0, 0.3f, Ease.OutCubic), new Key(0.143f, 1, Ease.OutSine), new Key(0.5f, 1.3f, Ease.InSine), new Key(0.81f, 1, Ease.InCubic), new Key(1, 0.3f) };
    // Sumber Gerbang Desain 2: paket tetap penuh sampai mendarat (43%–50%), lalu memudar.
    private static readonly Key[] Opacity = { new Key(0, 0, Ease.OutCubic), new Key(0.143f, 1, Linear), new Key(0.81f, 1, Ease.InCubic), new Key(1, 0) };

    private SpriteRenderer body;
    private Vector2 from;
    private Vector2 toPoint;
    private Transform toTarget;
    private bool inbound;
    private float started;
    private Vector3 unitScale;

    public static ParcelHop Play(Sprite sprite, Vector2 from, Vector2 to, Transform follow, bool intoTruck)
    {
        if (sprite == null) return null;
        var go = new GameObject(intoTruck ? "Paket masuk truk" : "Paket ke pintu");
        var hop = go.AddComponent<ParcelHop>();
        hop.body = go.AddComponent<SpriteRenderer>();
        hop.body.sprite = sprite;
        hop.body.sortingOrder = 20;
        hop.from = from;
        hop.toPoint = to;
        hop.toTarget = follow;
        hop.inbound = intoTruck;
        hop.started = Time.time;
        hop.unitScale = Vector3.one * (ParcelHeight / Mathf.Max(0.0001f, sprite.bounds.size.y));
        hop.Apply(0f);
        return hop;
    }

    private void Update()
    {
        float t = (Time.time - started) / Duration;
        Apply(Mathf.Clamp01(t));
        if (t >= 1f) Destroy(gameObject);
    }

    private void Apply(float t)
    {
        Vector2 to = toTarget != null ? (Vector2)toTarget.position : toPoint;
        float lerp = Eval(inbound ? InLerp : OutLerp, t);
        float lift = Eval(inbound ? InLift : OutLift, t);
        float scale = Eval(inbound ? InScale : OutScale, t);
        Vector2 at = Vector2.LerpUnclamped(from, to, lerp) + Vector2.up * lift;
        transform.localScale = unitScale * scale;
        transform.position = (Vector3)at - Vector3.Scale(body.sprite.bounds.center, transform.localScale);
        body.color = new Color(1f, 1f, 1f, Eval(Opacity, t));
    }

    private static float Eval(Key[] keys, float t)
    {
        if (t <= keys[0].T) return keys[0].V;
        for (int i = 1; i < keys.Length; i++)
        {
            if (t > keys[i].T) continue;
            Key a = keys[i - 1], b = keys[i];
            float u = (t - a.T) / Mathf.Max(0.0001f, b.T - a.T);
            return Mathf.LerpUnclamped(a.V, b.V, (a.E ?? Linear)(u));
        }
        return keys[keys.Length - 1].V;
    }
}
