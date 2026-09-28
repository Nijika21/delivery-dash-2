using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pratinjau kota di editor. Scene utama (KotaPaket) hanya berisi kamera dan truk; kotanya dibangun saat Play oleh
// DeliveryGameManager dari layout v7. Supaya tab Scene/Game tidak abu-abu kosong, kota peta terakhir (LastMode)
// dibangun sebagai objek HideFlags.DontSave: tidak pernah tersimpan ke scene, tidak membuat scene kotor, tidak ikut
// build, dan dihapus sebelum masuk Play (DeliveryGameManager selalu membangun kotanya sendiri).
// Truk dan kamera scene (posisi lama di luar kota) dipindah sementara ke titik mulai, persis seperti PlaceAt saat
// Play; posisi aslinya dikembalikan sebelum Play, sebelum scene disimpan, dan sebelum skrip dimuat ulang, jadi file
// scene tidak pernah berubah karena pratinjau ini.
// Matikan lewat menu Delivery Dash/Pratinjau Kota di Editor. Tidak jalan di batch mode (tes dan alat tangkap).
[InitializeOnLoad]
public static class TownEditorPreview
{
    private const string MenuPath = "Delivery Dash/Pratinjau Kota di Editor";
    private const string PrefKey = "DeliveryDash.TownEditorPreview";
    private const string PreviewName = "Pratinjau kota (editor, tidak disimpan)";
    private static DeliveryTown preview;
    private static bool framed;

    // Perpindahan sementara objek scene: dikembalikan hanya kalau objeknya belum digeser pengguna.
    private sealed class Staged
    {
        public Transform Target;
        public Vector3 OriginalPosition, AppliedPosition;
        public Quaternion OriginalRotation, AppliedRotation;
    }
    private static readonly System.Collections.Generic.List<Staged> staged = new System.Collections.Generic.List<Staged>();
    private static Camera stagedCamera;
    private static Color stagedBackground;

    static TownEditorPreview()
    {
        if (Application.isBatchMode) return;
        EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += Refresh;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Remove();
            else if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Refresh;
        };
        AssemblyReloadEvents.beforeAssemblyReload += Remove;
        EditorSceneManager.sceneSaving += (_, _) => Remove();
        EditorSceneManager.sceneSaved += _ => EditorApplication.delayCall += Refresh;
        EditorSceneManager.sceneClosing += (_, _) => Remove();
        EditorApplication.delayCall += Refresh;
    }

    private static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(PrefKey, !Enabled);
        Refresh();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return !Application.isBatchMode;
    }

    private static void Refresh()
    {
        Remove();
        if (Application.isBatchMode || !Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "KotaPaket") return;
        // Kota yang memang disimpan di scene tidak ditimpa.
        foreach (DeliveryTown town in Object.FindObjectsByType<DeliveryTown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((town.gameObject.hideFlags & HideFlags.DontSave) == 0) return;
        var catalog = AssetDatabase.LoadAssetAtPath<DeliveryContentCatalog>("Assets/Resources/DeliveryContentCatalog.asset");
        if (catalog == null) return;
        string map = CourierProgress.Load().LastMode == "blok-paket" ? "blok-paket" : "kota-paket";
        TextAsset layout = map == "blok-paket" ? catalog.blokPaketLayout : catalog.kotaPaketLayout;
        if (layout == null) return;

        var root = new GameObject(PreviewName) { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(root, scene);
        preview = root.AddComponent<DeliveryTown>();
        try
        {
            preview.Build(catalog, layout);
        }
        catch (System.Exception error)
        {
            Debug.LogWarning("Pratinjau kota editor gagal dibangun: " + error.Message);
            Remove();
            return;
        }
        // Semua anak ikut DontSave + tidak bisa disunting: pratinjau murni, bukan isi scene.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        // Collider pratinjau tidak dibutuhkan di editor dan tidak boleh ikut tes fisika editor.
        foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;

        // Truk di titik mulai (seperti Driver.PlaceAt), kamera di atasnya, latar kamera rumput seperti saat Play.
        Vector3 start = new Vector3(preview.StartPosition.x, preview.StartPosition.y, 0f);
        Quaternion heading = Quaternion.Euler(0f, 0f, preview.StartHeading);
        Driver car = Object.FindFirstObjectByType<Driver>();
        if (car != null) Stage(car.transform, start, heading);
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Stage(camera.transform, new Vector3(start.x, start.y, camera.transform.position.z), camera.transform.rotation);
        foreach (Unity.Cinemachine.CinemachineCamera follow in Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
            Stage(follow.transform, new Vector3(start.x, start.y, follow.transform.position.z), follow.transform.rotation);
        stagedCamera = Camera.main;
        if (stagedCamera != null)
        {
            stagedBackground = stagedCamera.backgroundColor;
            stagedCamera.backgroundColor = new Color32(0x78, 0xA3, 0x55, 255);
        }

        // Tab Scene diarahkan ke gudang sekali per sesi editor, supaya langsung terlihat.
        if (!framed && SceneView.lastActiveSceneView != null)
        {
            framed = true;
            SceneView view = SceneView.lastActiveSceneView;
            view.in2DMode = true;
            view.LookAt(new Vector3(preview.StartPosition.x, preview.StartPosition.y, 0f), Quaternion.identity, 30f);
        }
        SceneView.RepaintAll();
    }

    private static void Stage(Transform target, Vector3 position, Quaternion rotation)
    {
        staged.Add(new Staged
        {
            Target = target, OriginalPosition = target.position, OriginalRotation = target.rotation,
            AppliedPosition = position, AppliedRotation = rotation
        });
        target.SetPositionAndRotation(position, rotation);
    }

    private static void Restore()
    {
        for (int i = staged.Count - 1; i >= 0; i--)
        {
            Staged entry = staged[i];
            if (entry.Target == null) continue;
            bool untouched = (entry.Target.position - entry.AppliedPosition).sqrMagnitude < 1e-6f &&
                Quaternion.Angle(entry.Target.rotation, entry.AppliedRotation) < 0.01f;
            if (untouched) entry.Target.SetPositionAndRotation(entry.OriginalPosition, entry.OriginalRotation);
        }
        staged.Clear();
        if (stagedCamera != null) stagedCamera.backgroundColor = stagedBackground;
        stagedCamera = null;
    }

    private static void Remove()
    {
        Restore();
        if (preview != null)
        {
            preview.ReleaseVisualAssets();
            Object.DestroyImmediate(preview.gameObject);
        }
        preview = null;
        // Sisa pratinjau dari muat ulang domain sebelumnya (referensi statis hilang, objeknya masih ada).
        foreach (DeliveryTown town in Object.FindObjectsByType<DeliveryTown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (town.gameObject.name == PreviewName && (town.gameObject.hideFlags & HideFlags.DontSave) != 0)
            {
                town.ReleaseVisualAssets();
                Object.DestroyImmediate(town.gameObject);
            }
    }
}
