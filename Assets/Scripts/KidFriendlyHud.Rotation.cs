using DeliveryDash.UI;
using UnityEngine.UIElements;

// Putar layar manual (permintaan pengguna 27 Sep): ikon putar di lobby, tombol di menu jeda, dan baris "Layar" di
// Pengaturan (Otomatis / Tegak / Mendatar). Templat D5 tidak punya elemen ini, jadi dipasang saat layar diikat.
public partial class KidFriendlyHud
{
    private void AddRotateControls()
    {
        if (boundScreen == null) return;
        // Lobby: ikon di kiri tombol bantuan dan pengaturan.
        VisualElement gear = boundScreen.Q<VisualElement>("@iconbtn:gear");
        if (gear?.parent != null && boundScreen.Q<VisualElement>("iconbtn:putar") == null)
        {
            var anchor = new VisualElement { name = "@iconbtn:putar", pickingMode = PickingMode.Ignore };
            anchor.style.marginRight = 12;
            var button = new VisualElement { name = "iconbtn:putar" };
            button.AddToClassList("dd-iconbtn");
            button.Add(RotateGlyph("dd-iconbtn__glyph"));
            anchor.Add(button);
            gear.parent.Insert(0, anchor);
        }
        // Menu jeda: sesudah tombol suara.
        VisualElement sound = boundScreen.Q<VisualElement>("btn:suara-aktif");
        if (sound?.parent != null && boundScreen.Q<VisualElement>("btn:putar-layar") == null)
        {
            var button = new VisualElement { name = "btn:putar-layar" };
            button.AddToClassList("dd-btn");
            button.AddToClassList("dd-btn--quiet");
            button.Add(RotateGlyph("dd-btn__icon"));
            button.Add(new Label("Putar layar") { name = "teks", pickingMode = PickingMode.Ignore });
            sound.parent.Insert(sound.parent.IndexOf(sound) + 1, button);
        }
        // Pengaturan: baris Layar sesudah Suara; ketuk untuk berganti mode.
        VisualElement soundRow = boundScreen.Q<VisualElement>("row:suara");
        if (soundRow?.parent != null && boundScreen.Q<VisualElement>("row:layar") == null)
        {
            var row = new VisualElement { name = "row:layar" };
            row.AddToClassList("sc-set__row");
            row.Add(RotateGlyph("sc-set__icon"));
            var label = new Label("Layar") { name = "label", pickingMode = PickingMode.Ignore };
            label.AddToClassList("sc-set__label");
            row.Add(label);
            var value = new Label(ScreenRotation.Label(ScreenRotation.Current)) { name = "nilai", pickingMode = PickingMode.Ignore };
            value.AddToClassList("sc-set__label");
            value.style.flexGrow = 0;
            value.style.color = (UnityEngine.Color)new UnityEngine.Color32(0x0A, 0x8D, 0xC2, 255);
            value.style.marginRight = 8;
            row.Add(value);
            var chevron = new VisualElement { name = "ikon-chev", pickingMode = PickingMode.Ignore };
            chevron.AddToClassList("sc-set__chev");
            chevron.AddToClassList("ikon");
            chevron.AddToClassList("ikon--i-chev");
            row.Add(chevron);
            soundRow.parent.Insert(soundRow.parent.IndexOf(soundRow) + 1, row);
        }
    }

    private static VisualElement RotateGlyph(string className)
    {
        var glyph = new VisualElement { name = "ikon-putar", pickingMode = PickingMode.Ignore };
        glyph.AddToClassList(className);
        glyph.AddToClassList("ikon");
        glyph.AddToClassList("ikon--i-putar");
        return glyph;
    }

    private void BindRotateControls()
    {
        BindClick("iconbtn:putar", ScreenRotation.Toggle);
        BindClick("btn:putar-layar", ScreenRotation.Toggle);
        BindClick("row:layar", () => { ScreenRotation.Cycle(); UpdateToolkitText(); });
    }

    private void UpdateRotateText()
    {
        Label value = boundScreen?.Q<VisualElement>("row:layar")?.Q<Label>("nilai");
        if (value != null) value.text = ScreenRotation.Label(ScreenRotation.Current);
    }
}
