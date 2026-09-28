using System;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor.Tests
{
    // Pintu batch untuk semua cek tanpa Play Mode: data kota v7, UI Toolkit, dan input sentuh.
    // "Unity.exe -batchmode -executeMethod DeliveryDash.Editor.Tests.StaticChecks.RunBatch"
    public static class StaticChecks
    {
        [MenuItem("Delivery Dash/Jalankan Cek Statis")]
        public static void Run()
        {
            // Color space Gamma: UI Toolkit mencampur warna tembus pandang (scrim, redup, label gelap, opacity) di sRGB
            // seperti galeri desain. Di Linear scrim --dd-scrim di atas dunia 133 jadi 86, bukan 60 (desain); warna pekat
            // dan dunia sama di keduanya. forceGammaRendering bukan pengganti: hanya untuk panel ke RenderTexture UNORM.
            if (PlayerSettings.colorSpace != ColorSpace.Gamma)
                throw new InvalidOperationException("FAIL: Player color space harus Gamma (campuran warna UI = desain).");
            TownV7Checks.ValidateAll();
            UiChecks.ValidateAll();
            TouchInputChecks.ValidateAll();
            Debug.Log("STATIC_CHECKS_OK");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
