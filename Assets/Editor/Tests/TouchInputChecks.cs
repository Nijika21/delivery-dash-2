using System;
using System.IO;
using DeliveryDash.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.Editor.Tests
{
    public static class TouchInputChecks
    {
        [MenuItem("Delivery Dash/Periksa Input Sentuh")]
        public static void ValidateAll()
        {
            DeliveryUiCatalog catalog = AssetDatabase.LoadAssetAtPath<DeliveryUiCatalog>("Assets/Resources/DeliveryUiCatalog.asset");
            Require(catalog != null, "katalog UI tersedia");
            TemplateContainer landscape = catalog.Get(10, false).CloneTree();
            TemplateContainer portrait = catalog.Get(10, true).CloneTree();
            RequireControls(landscape, "ctl:left", "ctl:right", "ctl:gas", "ctl:rem");
            RequireControls(portrait, "joy");

            float forward = Driver.AdvanceSpeed(0f, true, false, 9f, 4f, 0.5f);
            float coast = Driver.AdvanceSpeed(forward, false, false, 9f, 4f, 0.5f);
            float stopped = Driver.AdvanceSpeed(forward, false, true, 9f, 4f, 1f);
            float reverse = Driver.AdvanceSpeed(0f, false, true, 9f, 4f, 0.5f);
            Require(forward > 0f, "gas mempercepat kendaraan");
            Require(coast >= 0f && coast < forward, "melepas gas memperlambat kendaraan");
            Require(Mathf.Abs(stopped) < 0.001f, "rem menghentikan laju maju sebelum mundur");
            Require(reverse < 0f, "menahan rem dari diam menjalankan mundur");
            Require(File.Exists("Assets/Classic/Classic.unity"), "scene Classic tersedia");
            Debug.Log("D5_TOUCH_CHECKS_OK: kontrol landscape, joystick portrait, rem, gas, mundur, dan Classic tersedia.");
        }

        private static void RequireControls(VisualElement root, params string[] names)
        {
            foreach (string name in names)
                Require(root.Q<VisualElement>(name) != null, "kontrol tersedia: " + name);
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("D5_TOUCH_FAIL: " + description);
        }
    }
}
