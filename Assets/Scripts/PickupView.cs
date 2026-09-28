using System.Collections;
using UnityEngine;

// Pickup stiker bintang/kilat/perisai (revisi Gerbang Desain 4): benda 2,1 unit tepat di tengah kilau berwarna 3,2 unit.
// Gerak: pickup-bob (2,4 dtk, naik 0,1 unit + skala 1,04; kilau 0,8↔1) dan pickup-ambil (benda 250 ms, kilau 300 ms).
public sealed class PickupView : MonoBehaviour
{
    public const float ItemSize = 2.1f;
    public const float GlowSize = ItemSize * 1.52f;

    private SpriteRenderer item;
    private SpriteRenderer glow;
    private float phase;
    private bool collecting;
    private Vector3 itemScale, glowScale;

    public static PickupView Create(DeliveryTown town, string kind, Vector2 position, Transform parent)
    {
        Sprite itemSprite = town.Catalog.World(kind);
        Sprite glowSprite = town.Catalog.World("kilau-" + kind);
        if (itemSprite == null) return null;
        var root = new GameObject("Pickup " + kind);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var view = root.AddComponent<PickupView>();
        int order = DeliveryTown.StandingOrder(position.y);
        if (glowSprite != null)
        {
            view.glow = town.WorldSprite("Kilau", glowSprite, position, Vector2.one * GlowSize, 0f, order - 1, root.transform);
            view.glow.color = new Color(1f, 1f, 1f, 0.8f);
        }
        view.item = town.WorldSprite("Benda", itemSprite, position, Vector2.one * ItemSize, 0f, order, root.transform);
        view.itemScale = view.item.transform.localScale;
        view.glowScale = view.glow != null ? view.glow.transform.localScale : Vector3.one;
        view.phase = Random.value * 2.4f;
        return view;
    }

    private void Update()
    {
        if (collecting || item == null) return;
        float t = Mathf.Repeat(Time.time + phase, 2.4f) / 2.4f;
        float k = t < 0.5f ? Ease.InOutSine(t / 0.5f) : 1f - Ease.InOutSine((t - 0.5f) / 0.5f);
        item.transform.localPosition = new Vector3(0f, 0.1f * k, 0f);
        item.transform.localScale = itemScale * (1f + 0.04f * k);
        if (glow != null) glow.color = new Color(1f, 1f, 1f, 0.8f + 0.2f * k);
    }

    public void Collect()
    {
        if (collecting) return;
        collecting = true;
        transform.SetParent(null, true);
        StartCoroutine(CollectRoutine());
    }

    private IEnumerator CollectRoutine()
    {
        Vector3 startScale = item != null ? item.transform.localScale : Vector3.one;
        for (float time = 0f; time < 0.3f; time += Time.deltaTime)
        {
            float a = Ease.OutCubic(Mathf.Clamp01(time / 0.25f));
            float b = Ease.OutCubic(Mathf.Clamp01(time / 0.3f));
            if (item != null)
            {
                item.transform.localScale = startScale * Mathf.Lerp(1f, 1.35f, a);
                item.color = new Color(1f, 1f, 1f, 1f - a);
            }
            if (glow != null)
            {
                glow.transform.localScale = glowScale * Mathf.Lerp(1f, 1.6f, b);
                glow.color = new Color(1f, 1f, 1f, 1f - b);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
