using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.UI
{
    // Perbedaan perilaku browser (galeri desain) dan UI Toolkit yang tidak bisa ditulis di USS.
    public static class UiParity
    {
        // Radius "pil" (--dd-radius-pill: 999px). Browser memperkecil semua radius dengan faktor yang sama sehingga
        // sudutnya setengah lingkaran; UI Toolkit menjepit tiap sumbu sendiri sehingga pil panjang jadi oval.
        // Elemen ber-radius ≥ 500 px ditandai sekali, lalu radiusnya = setengah sisi terpendek setiap kali ukurannya berubah.
        public const float PillThreshold = 500f;
        private const string PillClass = "dd-pill-auto";

        public static void FixPills(VisualElement root)
        {
            if (root == null) return;
            root.Query<VisualElement>().ForEach(element =>
            {
                FixPill(element);
                if (element is Label label) FixLabelAlign(label);
            });
        }

        private static void FixPill(VisualElement element)
        {
            if (!element.ClassListContains(PillClass))
            {
                if (element.resolvedStyle.borderTopLeftRadius < PillThreshold) return;
                element.AddToClassList(PillClass);
                element.RegisterCallback<GeometryChangedEvent>(_ => FixPill(element));
            }
            float width = element.resolvedStyle.width, height = element.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0f || height <= 0f) return;
            float radius = PillRadius(width, height);
            if (element.style.borderTopLeftRadius.keyword == StyleKeyword.Undefined &&
                Mathf.Abs(element.style.borderTopLeftRadius.value.value - radius) < 0.25f) return;
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        public static float PillRadius(float width, float height) => Mathf.Min(width, height) * 0.5f;

        // Label ber-display:flex di galeri (mis. .dd-hudbar__dist: tinggi 44, align-items/justify-content center):
        // browser memusatkan teks sebagai item flex baris; UI Toolkit menggambar teks Label di kiri atas kotaknya.
        // Arah bawaan CSS = baris: justify-content → horizontal, align-items → vertikal.
        public static void FixLabelAlign(Label label)
        {
            IResolvedStyle resolved = label.resolvedStyle;
            Align align = resolved.alignItems;
            Justify justify = resolved.justifyContent;
            bool vertical = align == Align.Center || align == Align.FlexEnd;
            bool horizontal = justify == Justify.Center || justify == Justify.FlexEnd;
            if (!vertical && !horizontal) return;
            TextAnchor current = resolved.unityTextAlign;
            int column = horizontal ? (justify == Justify.Center ? 1 : 2) : (int)current % 3;
            int row = vertical ? (align == Align.Center ? 1 : 2) : (int)current / 3;
            TextAnchor wanted = (TextAnchor)(row * 3 + column);
            if (current != wanted) label.style.unityTextAlign = wanted;
        }
    }
}
