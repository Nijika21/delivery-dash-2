using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeliveryDash.Editor
{
    // Musik 2.0: pengaturan impor (terkompresi Vorbis supaya APK tidak bengkak) dan penanda DeliveryMusicBank di scene 2.0.
    // Classic tidak disentuh: scene-nya tidak punya penanda, jadi berkas musik tidak ikut ke APK Classic.
    public static class DeliveryMusicSetup
    {
        public const string SetPath = "Assets/Audio/DeliveryMusic.asset";
        private static readonly string[] Scenes = { "Assets/Scenes/KotaPaket.unity", "Assets/Scenes/SampleScene.unity" };

        [MenuItem("Delivery Dash/Pasang Musik 2.0")]
        public static void Install()
        {
            var set = AssetDatabase.LoadAssetAtPath<DeliveryMusicSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<DeliveryMusicSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            set.lobby = Music("Assets/Audio/Music/lobby-fun-adventure.ogg", true);
            set.play = new[]
            {
                Music("Assets/Audio/Music/main-seaside-village.wav", true),
                Music("Assets/Audio/Music/main-forget-me-not.ogg", true),
                Music("Assets/Audio/Music/main-hot-springs-town.mp3", true),
            };
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            string active = EditorSceneManager.GetActiveScene().path;
            foreach (string path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                DeliveryMusicBank bank = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if ((bank = root.GetComponentInChildren<DeliveryMusicBank>(true)) != null) break;
                if (bank == null) bank = new GameObject("Musik 2.0").AddComponent<DeliveryMusicBank>();
                bank.music = set;
                EditorUtility.SetDirty(bank);
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                bool onDisk = File.ReadAllText(path).Contains("Musik 2.0");
                Debug.Log($"MUSIK_2_SCENE {path}: simpan {saved}, ada di berkas {onDisk}, akar {scene.rootCount}");
            }
            if (!string.IsNullOrEmpty(active) && File.Exists(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
            Debug.Log("MUSIK_2_SIAP");
        }

        // Musik panjang: Vorbis, dialirkan (streaming) supaya hemat RAM. Kualitas 0,5 ≈ 1 MB per menit stereo.
        public static AudioClip Music(string path, bool streaming)
        {
            Configure(path, new AudioImporterSampleSettings
            {
                loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.5f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            }, !streaming);
            return Load(path);
        }

        // Efek pendek: diurai saat dimuat supaya berbunyi tanpa jeda.
        public static AudioClip Effect(string path)
        {
            Configure(path, new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.7f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            }, true);
            return Load(path);
        }

        private static void Configure(string path, AudioImporterSampleSettings settings, bool preload)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) throw new FileNotFoundException("Audio tidak ditemukan: " + path);
            settings.preloadAudioData = preload;
            importer.defaultSampleSettings = settings;
            importer.ClearSampleSettingOverride("Android");
            importer.loadInBackground = !preload;
            importer.SaveAndReimport();
        }

        private static AudioClip Load(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new FileNotFoundException("AudioClip tidak termuat: " + path);
            return clip;
        }
    }
}
