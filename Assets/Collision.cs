using UnityEngine;

// Kept for compatibility with the original tutorial scene.
public class Collision : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        DeliveryGameManager.Instance?.TryInteract(other.gameObject);
    }
}
