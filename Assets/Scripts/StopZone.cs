using UnityEngine;

public sealed class StopZone : MonoBehaviour
{
    [SerializeField] private float requiredSeconds = 0.75f;
    [SerializeField] private float progress;
    public float Progress01 => requiredSeconds <= 0f ? 1f : Mathf.Clamp01(progress / requiredSeconds);
    // Toleransi galat float: 45 × (1/60) terjumlah jadi 0,7499999, tetap harus terpicu di frame ke-45.
    public const float Epsilon = 0.0001f;
    public bool Ready => progress >= requiredSeconds - Epsilon;

    public void Configure(float seconds)
    {
        requiredSeconds = Mathf.Max(0.01f, seconds);
        progress = 0f;
    }

    public bool AdvanceDwell(bool stopped, float seconds)
    {
        float delta = Mathf.Max(0f, seconds) * (stopped ? 1f : -2.5f);
        progress = Mathf.Clamp(progress + delta, 0f, requiredSeconds);
        if (progress < Epsilon) progress = 0f;
        return Ready;
    }
}
