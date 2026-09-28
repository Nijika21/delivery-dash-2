using System;
using UnityEngine;

// Pembaca field bake. Data int16 little-endian memakai jarak bertanda ×256.
public sealed class SdfField
{
    private readonly short[] samples;
    private readonly int width;
    private readonly int height;
    private readonly Vector2 origin;
    private readonly float cell;

    public SdfField(byte[] bytes, int width, int height, Vector2 origin, float cell)
    {
        if (bytes == null || bytes.Length != width * height * 2) throw new ArgumentException("Ukuran SDF tidak cocok.");
        this.width = width;
        this.height = height;
        this.origin = origin;
        this.cell = cell;
        samples = new short[width * height];
        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8);
    }

    public float Sample(Vector2 point)
    {
        float gx = (point.x - origin.x) / cell;
        float gy = (point.y - origin.y) / cell;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(gy), 0, height - 1);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = Mathf.Clamp01(gx - x0);
        float ty = Mathf.Clamp01(gy - y0);
        float a = Mathf.Lerp(samples[y0 * width + x0], samples[y0 * width + x1], tx);
        float b = Mathf.Lerp(samples[y1 * width + x0], samples[y1 * width + x1], tx);
        return Mathf.Lerp(a, b, ty) / 256f;
    }
}
