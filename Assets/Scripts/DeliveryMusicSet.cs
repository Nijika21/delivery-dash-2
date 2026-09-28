using UnityEngine;

// Daftar musik 2.0 (Assets/Audio/DeliveryMusic.asset). Hanya dirujuk scene 2.0 lewat DeliveryMusicBank,
// jadi APK Classic (scene Classic saja) tidak ikut membawa musik.
[CreateAssetMenu(menuName = "Delivery Dash/Music Set")]
public sealed class DeliveryMusicSet : ScriptableObject
{
    public AudioClip lobby;
    public AudioClip[] play;
}
