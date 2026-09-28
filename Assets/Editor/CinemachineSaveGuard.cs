using System.Reflection;
using UnityEditor;

// Cinemachine "Save During Play" menawarkan menyimpan perubahan CinemachineCamera saat keluar Play (dialog keep /
// don't keep). Di game ini kamera memang diatur kode saat Play (lensa portrait/landscape, bingkai lobby, target ikut
// truk), jadi nilai itu tidak boleh pernah ditulis balik ke scene. Fitur itu dimatikan setiap editor dimuat.
// Pengaturannya per pengguna (EditorPrefs); lewat refleksi supaya tidak bergantung pada rakitan editor Cinemachine.
[InitializeOnLoad]
public static class CinemachineSaveGuard
{
    static CinemachineSaveGuard()
    {
        PropertyInfo enabled = System.Type.GetType("Unity.Cinemachine.Editor.SaveDuringPlay, Unity.Cinemachine.Editor")
            ?.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Static);
        if (enabled != null && enabled.CanWrite && (bool)enabled.GetValue(null)) enabled.SetValue(null, false);
    }
}
