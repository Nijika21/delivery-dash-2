using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryDash.Editor
{
    public static class SceneArchiveSetup
    {
        private const string MainScene = "Assets/Scenes/KotaPaket.unity";
        private const string BootstrapScene = "Assets/Scenes/SampleScene.unity";
        private const string ArchiveFolder = "Assets/Archive";
        private const string ArchiveScene = ArchiveFolder + "/KotaLama.unity";
        private const long MaximumSceneBytes = 50L * 1024L * 1024L;

        [MenuItem("Delivery Dash/Pindahkan Kota Lama ke Arsip")]
        public static void PrepareLeanMainScene()
        {
            if (!File.Exists(MainScene) || !File.Exists(BootstrapScene))
                throw new FileNotFoundException("Scene utama atau scene bootstrap tidak tersedia.");

            if (new FileInfo(MainScene).Length < MaximumSceneBytes)
            {
                EnsureArchiveExists();
                return;
            }

            EnsureArchiveExists();

            // Keep KotaPaket's GUID stable for build profiles. Only its serialized contents
            // become the small bootstrap scene; the complete authored town remains archived.
            File.Copy(BootstrapScene, MainScene, true);
            AssetDatabase.ImportAsset(MainScene, ImportAssetOptions.ForceUpdate);

            if (new FileInfo(MainScene).Length >= MaximumSceneBytes)
                throw new InvalidOperationException("Scene bootstrap masih melebihi batas 50 MB.");

            UnityEngine.Debug.Log("KOTA_LAMA_DIARSIPKAN: scene utama kini bootstrap runtime di bawah 50 MB.");
        }

        // Scene utama hanya bootstrap runtime: truk (Driver), Main Camera, CinemachineCamera, dan Global Light 2D.
        // Lingkungan tutorial lama (jalan, rumah, collider) disingkirkan; salinan utuhnya tetap di SampleScene
        // dan kota lama di Archive/KotaLama.unity. GUID KotaPaket.unity tidak berubah.
        [MenuItem("Delivery Dash/Bersihkan Scene Bootstrap")]
        public static void CleanBootstrapScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Scene aktif punya perubahan belum disimpan; simpan atau buang dulu.");
            if (!File.Exists(ArchiveScene)) throw new FileNotFoundException("Arsip kota lama belum ada: " + ArchiveScene);
            var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            var kept = new List<string>();
            var removed = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool keep = root.GetComponentInChildren<Driver>(true) != null || root.GetComponent<Camera>() != null
                    || root.name == "CinemachineCamera" || root.name == "Global Light 2D";
                (keep ? kept : removed).Add(root.name);
                if (!keep) UnityEngine.Object.DestroyImmediate(root);
            }
            if (!kept.Exists(n => n == "Main Camera") || !kept.Exists(n => n == "CinemachineCamera") || kept.Count < 4)
                throw new InvalidOperationException("Objek bootstrap wajib tidak lengkap: " + string.Join(", ", kept));
            if (removed.Count > 0) EditorSceneManager.SaveScene(scene);
            Debug.Log($"BOOTSTRAP_BERSIH: simpan {kept.Count} ({string.Join(", ", kept)}), singkirkan {removed.Count}.");
        }

        private static void EnsureArchiveExists()
        {
            if (!AssetDatabase.IsValidFolder(ArchiveFolder))
                AssetDatabase.CreateFolder("Assets", "Archive");
            if (File.Exists(ArchiveScene)) return;
            if (!AssetDatabase.CopyAsset(MainScene, ArchiveScene))
                throw new IOException("Kota lama tidak dapat disalin ke arsip.");
            AssetDatabase.ImportAsset(ArchiveScene, ImportAssetOptions.ForceUpdate);
        }
    }
}
