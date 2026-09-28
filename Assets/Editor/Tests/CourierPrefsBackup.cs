using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDash.Editor.Tests
{
    // Cadangan Courier.* untuk alat Play Mode (tangkap layar, bot uji main). Disimpan di SessionState supaya bertahan
    // melewati muat ulang domain, Play yang dihentikan, atau crash skrip: cadangan tertunda dipulihkan dulu dan dipakai
    // lagi, tidak pernah dicadangkan ulang dari data yang sudah diubah alat.
    public static class CourierPrefsBackup
    {
        private static readonly string[] IntKeys = { "Courier.Sound", "Courier.Music", "Courier.Minimap", "Courier.Stamps", "Courier.Livery" };
        private static readonly string[] StringKeys = { "Courier.Progress" };
        private const string Empty = "-"; // cadangan sah tanpa kunci apa pun

        public static bool Pending(string key) => !string.IsNullOrEmpty(SessionState.GetString(key, string.Empty));

        // Awal sesi alat: pulihkan cadangan tertunda (sesi lama tidak selesai) atau buat cadangan baru.
        public static void Begin(string key)
        {
            string pending = SessionState.GetString(key, string.Empty);
            if (!string.IsNullOrEmpty(pending))
            {
                Debug.LogWarning($"{key}: cadangan Courier.* dari sesi yang tidak selesai dipulihkan dulu.");
                Apply(pending);
                return;
            }
            SessionState.SetString(key, Capture());
        }

        // Akhir sesi (normal, dihentikan, atau gagal): pulihkan lalu hapus cadangan. Aman dipanggil berulang.
        public static void End(string key)
        {
            string pending = SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(pending)) return;
            Apply(pending);
            SessionState.EraseString(key);
        }

        private static string Capture()
        {
            var text = new StringBuilder(Empty + "\n");
            foreach (string name in IntKeys)
                if (PlayerPrefs.HasKey(name)) text.Append(name).Append("\ti\t").Append(PlayerPrefs.GetInt(name)).Append('\n');
            foreach (string name in StringKeys)
                if (PlayerPrefs.HasKey(name))
                    text.Append(name).Append("\ts\t").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(PlayerPrefs.GetString(name)))).Append('\n');
            return text.ToString();
        }

        private static void Apply(string backup)
        {
            foreach (string name in IntKeys) PlayerPrefs.DeleteKey(name);
            foreach (string name in StringKeys) PlayerPrefs.DeleteKey(name);
            foreach (string line in backup.Split('\n'))
            {
                string[] part = line.Split('\t');
                if (part.Length != 3) continue;
                if (part[1] == "i") PlayerPrefs.SetInt(part[0], int.Parse(part[2]));
                else PlayerPrefs.SetString(part[0], Encoding.UTF8.GetString(Convert.FromBase64String(part[2])));
            }
            PlayerPrefs.Save();
        }

        // Membuka scene tanpa membuang perubahan yang belum disimpan: batch → batal dengan log jelas,
        // interaktif → tanya pengguna (Batal = tidak jadi). false = alat tidak boleh lanjut.
        public static bool OpenScene(string path)
        {
            Scene active = SceneManager.GetActiveScene();
            bool dirty = false;
            for (int i = 0; i < SceneManager.sceneCount; i++) dirty |= SceneManager.GetSceneAt(i).isDirty;
            if (!dirty && SceneManager.sceneCount == 1 && active.path == path) return true;
            if (dirty)
            {
                if (Application.isBatchMode)
                {
                    Debug.LogError($"Scene terbuka punya perubahan yang belum disimpan; {path} tidak dibuka supaya perubahan itu tidak hilang.");
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            }
            EditorSceneManager.OpenScene(path);
            return true;
        }
    }
}
