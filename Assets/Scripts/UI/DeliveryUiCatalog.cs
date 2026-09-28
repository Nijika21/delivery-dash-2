using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.UI
{
    [CreateAssetMenu(menuName = "Delivery Dash/UI Catalog")]
    public sealed class DeliveryUiCatalog : ScriptableObject
    {
        public PanelSettings panelSettings;
        public VisualTreeAsset[] landscape;
        public VisualTreeAsset[] portrait;
        // Design/d5/gerak.json (salinan Assets/UI/Motion/gerak.json) untuk UiMotion.
        public TextAsset motion;

        public VisualTreeAsset Get(int screen, bool portraitMode)
        {
            VisualTreeAsset[] list = portraitMode ? portrait : landscape;
            int index = screen - 1;
            return list != null && index >= 0 && index < list.Length ? list[index] : null;
        }
    }
}
