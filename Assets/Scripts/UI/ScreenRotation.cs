using UnityEngine;

namespace DeliveryDash.UI
{
    // Orientasi layar manual (permintaan pengguna 27 Sep): pemain tidak perlu memutar HP atau mematikan kunci rotasi.
    // Otomatis = ikut sensor dan kunci rotasi sistem (bawaan); Tegak / Mendatar = dikunci. Tombol putar (lobby, menu jeda,
    // Classic) mengunci ke kebalikan orientasi sekarang; baris Layar di Pengaturan memutar Otomatis → Tegak → Mendatar.
    // Classic (rakitan terpisah) memakai kunci PlayerPrefs yang sama tanpa merujuk kelas ini.
    public static class ScreenRotation
    {
        public enum Mode { Auto, Portrait, Landscape }

        public const string PrefKey = "Screen.Orientation";

        public static Mode Current => (Mode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);

        public static string Label(Mode mode) => mode == Mode.Portrait ? "Tegak" : mode == Mode.Landscape ? "Mendatar" : "Otomatis";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved() => Apply(Current);

        public static void Set(Mode mode)
        {
            PlayerPrefs.SetInt(PrefKey, (int)mode);
            PlayerPrefs.Save();
            Apply(mode);
        }

        // Tegak ⇄ mendatar dari orientasi yang sedang tampil.
        public static void Toggle() => Set(UiRoot.PortraitScreen ? Mode.Landscape : Mode.Portrait);

        public static void Cycle() => Set(Current == Mode.Auto ? Mode.Portrait : Current == Mode.Portrait ? Mode.Landscape : Mode.Auto);

        private static void Apply(Mode mode)
        {
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = mode != Mode.Landscape;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = mode != Mode.Portrait;
            Screen.autorotateToLandscapeRight = mode != Mode.Portrait;
            // Orientasi tetap langsung memutar layar walau kunci rotasi sistem menyala.
            Screen.orientation = mode == Mode.Portrait ? ScreenOrientation.Portrait
                : mode == Mode.Landscape ? ScreenOrientation.LandscapeLeft : ScreenOrientation.AutoRotation;
        }
    }
}
