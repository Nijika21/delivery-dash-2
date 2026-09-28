using UnityEngine.UIElements;

// Sakelar musik (permintaan pengguna 28 Sep): terpisah dari Suara. Templat D5 hanya punya Suara, jadi baris Pengaturan
// dan tombol menu jeda dipasang saat layar diikat, sepola dengan Suara (sakelar is-on / "Musik: {aktif|mati}").
public partial class KidFriendlyHud
{
    private void AddMusicControls()
    {
        if (boundScreen == null) return;
        // Pengaturan: baris Musik tepat sesudah Suara (sebelum baris Layar).
        VisualElement soundRow = boundScreen.Q<VisualElement>("row:suara");
        if (soundRow?.parent != null && boundScreen.Q<VisualElement>("row:musik") == null)
        {
            var row = new VisualElement { name = "row:musik" };
            row.AddToClassList("sc-set__row");
            row.Add(MusicGlyph("sc-set__icon"));
            var label = new Label("Musik") { name = "label", pickingMode = PickingMode.Ignore };
            label.AddToClassList("sc-set__label");
            row.Add(label);
            var toggle = new VisualElement { name = "toggle", pickingMode = PickingMode.Ignore };
            toggle.AddToClassList("dd-toggle");
            var knob = new VisualElement { name = "knob", pickingMode = PickingMode.Ignore };
            knob.AddToClassList("dd-toggle__knob");
            toggle.Add(knob);
            row.Add(toggle);
            soundRow.parent.Insert(soundRow.parent.IndexOf(soundRow) + 1, row);
        }
        // Menu jeda: tombol Musik tepat sesudah tombol Suara.
        VisualElement sound = boundScreen.Q<VisualElement>("btn:suara-aktif");
        if (sound?.parent != null && boundScreen.Q<VisualElement>("btn:musik-aktif") == null)
        {
            var button = new VisualElement { name = "btn:musik-aktif" };
            button.AddToClassList("dd-btn");
            button.AddToClassList("dd-btn--quiet");
            button.Add(MusicGlyph("dd-btn__icon"));
            button.Add(new Label("Musik: aktif") { name = "teks", pickingMode = PickingMode.Ignore });
            sound.parent.Insert(sound.parent.IndexOf(sound) + 1, button);
        }
    }

    private static VisualElement MusicGlyph(string className)
    {
        var glyph = new VisualElement { name = "ikon-musik", pickingMode = PickingMode.Ignore };
        glyph.AddToClassList(className);
        glyph.AddToClassList("ikon");
        glyph.AddToClassList("ikon--i-musik");
        return glyph;
    }

    private void BindMusicControls()
    {
        BindClick("row:musik", ToggleMusic);
        BindClick("btn:musik-aktif", ToggleMusic);
    }

    private void ToggleMusic()
    {
        DeliveryAudio.ToggleMusic();
        UpdateToolkitText();
    }

    private void UpdateMusicText()
    {
        boundScreen?.Q<VisualElement>("row:musik")?.Q<VisualElement>("toggle")?.EnableInClassList("is-on", DeliveryAudio.MusicEnabled);
        Label pauseMusic = boundScreen?.Q<VisualElement>("btn:musik-aktif")?.Q<Label>("teks");
        if (pauseMusic != null) pauseMusic.text = DeliveryAudio.MusicEnabled ? "Musik: aktif" : "Musik: mati";
    }
}
