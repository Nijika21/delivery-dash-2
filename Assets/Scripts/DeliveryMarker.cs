using UnityEngine;

public enum DeliveryMarkerKind
{
    Package,
    Customer,
    Star,
    Boost,
    Stamp,
    Shield
}

// Titik logika tujuan/pickup. Gambarnya terpisah (StopZoneView untuk zona, PickupView untuk pickup) supaya
// animasi selesai/diambil tetap berjalan sesudah titik logikanya dihapus.
public class DeliveryMarker : MonoBehaviour
{
    [SerializeField] private DeliveryMarkerKind kind;
    private bool collected;

    public DeliveryMarkerKind Kind => kind;
    public StopZoneView ZoneView { get; set; }
    public PickupView PickupView { get; set; }
    // Sudut bantalan zona (rumah menghadap jalan); dipakai uji "di dalam zona".
    public float ZoneAngle { get; set; }
    public string ZoneKind { get; set; }

    public void Configure(DeliveryMarkerKind markerKind)
    {
        kind = markerKind;
    }

    public bool CollectOnce()
    {
        if (collected) return false;
        collected = true;
        return true;
    }

    // Selesai: zona memutar lencana centang, pickup memutar animasi ambil. Keduanya menghapus dirinya sendiri.
    public void PlayCollected()
    {
        if (ZoneView != null) { ZoneView.Complete(); ZoneView = null; }
        if (PickupView != null) { PickupView.Collect(); PickupView = null; }
    }

    private void OnDestroy()
    {
        if (ZoneView != null && !ZoneView.Completed) ZoneView.Dismiss();
        if (PickupView != null) Destroy(PickupView.gameObject);
    }
}
