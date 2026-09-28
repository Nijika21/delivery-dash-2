using UnityEngine;

// Stores the minimap presentation state. The map itself is rendered by
// DeliveryMinimapElement with UI Toolkit's Painter2D.
public class DeliveryMinimap : MonoBehaviour
{
    public bool Visible { get; private set; }
    public bool Expanded { get; private set; }

    public void Configure(DeliveryGameManager manager)
    {
        Visible = PlayerPrefs.GetInt("Courier.Minimap", 1) == 1;
    }

    public void Toggle()
    {
        Visible = !Visible;
        Expanded = false;
        if (DeliveryGameManager.IsValidationRunning) return;
        PlayerPrefs.SetInt("Courier.Minimap", Visible ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetExpanded(bool value)
    {
        Expanded = Visible && value;
    }

    public static Vector2 Project(Vector2 world, Rect view, Rect panel, float inset = 12)
    {
        Vector2 projected = new Vector2(
            panel.x + (world.x - view.x) / view.width * panel.width,
            panel.yMax - (world.y - view.y) / view.height * panel.height);
        Vector2 delta = projected - panel.center;
        float factor = Mathf.Min(1, Mathf.Min(
            (panel.width / 2 - inset) / Mathf.Max(Mathf.Abs(delta.x), 0.001f),
            (panel.height / 2 - inset) / Mathf.Max(Mathf.Abs(delta.y), 0.001f)));
        return panel.center + delta * factor;
    }

    public static Matrix4x4 RotateIcon(Matrix4x4 hudMatrix, Vector2 pivot, float angle)
    {
        return hudMatrix * Matrix4x4.Translate(pivot)
            * Matrix4x4.Rotate(Quaternion.Euler(0, 0, angle)) * Matrix4x4.Translate(-pivot);
    }
}
