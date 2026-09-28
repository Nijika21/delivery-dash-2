using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDash.Classic
{
    public sealed class ClassicCollision : MonoBehaviour
    {
        // Loop tanpa batas (konsep pengguna 27 Sep): paket diambil, diantar ke rumah mana saja, lalu paket muncul lagi di depan
        // gudang. Pesan sukses tampil sebentar tiap antaran; paket baru muncul sesudah jeda singkat.
        private const float MessageSeconds = 2f, RespawnSeconds = 1f;

        private bool carrying;
        private ParticleSystem packageIndicator;
        private GameObject successMessage;
        private GameObject package;
        private Transform deliveredHouse;
        private int deliveries;

        public bool HasPackage => carrying;
        public bool Delivered => deliveries > 0;
        public int Deliveries => deliveries;

        private void Awake()
        {
            packageIndicator = GetComponent<ParticleSystem>();
            if (packageIndicator != null) packageIndicator.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Teks "PACKAGE DELIVERED" (Bangers) disembunyikan saat mulai dan tampil saat paket diserahkan. Permintaan pengguna
        // 28 Sep (seperti versi 1.0 asli): teksnya muncul di atas rumah yang dituju, bukan di kiri atas layar.
        private void Start()
        {
            successMessage = CreateSuccessMessage();
            successMessage.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Package") && !carrying)
            {
                carrying = true;
                package = other.gameObject;
                CancelInvoke(nameof(HidePackage));
                Invoke(nameof(HidePackage), 0.5f);
                packageIndicator?.Play();
                Debug.Log("Paket diambil.");
                return;
            }

            // The tutorial's last slide omits this state check. Keep the original loop,
            // but do not allow a delivery when the truck is empty.
            if (!other.CompareTag("Customer") || !carrying) return;
            carrying = false;
            packageIndicator?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            deliveries++;
            deliveredHouse = other.transform;
            if (successMessage != null) { successMessage.SetActive(true); PlaceMessage(); }
            CancelInvoke(nameof(HideMessage));
            Invoke(nameof(HideMessage), MessageSeconds);
            CancelInvoke(nameof(RespawnPackage));
            Invoke(nameof(RespawnPackage), RespawnSeconds);
            Debug.Log("Paket terkirim.");
        }

        private void LateUpdate()
        {
            if (successMessage != null && successMessage.activeSelf) PlaceMessage();
        }

        // Teks ditaruh di titik layar rumah (kamera mengikuti truk, jadi dihitung ulang tiap bingkai).
        private void PlaceMessage()
        {
            Camera view = Camera.main;
            if (view == null || deliveredHouse == null) return;
            Canvas canvas = successMessage.GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector3 screen = view.WorldToScreenPoint(deliveredHouse.position);
            ((RectTransform)successMessage.transform).anchoredPosition = new Vector2(screen.x, screen.y) / scale;
        }

        private void HidePackage()
        {
            if (package != null && carrying) package.SetActive(false);
        }

        private void HideMessage()
        {
            if (successMessage != null) successMessage.SetActive(false);
        }

        // Paket yang sama dipakai ulang di tempat asalnya (depan gudang).
        private void RespawnPackage()
        {
            if (package != null && !carrying) package.SetActive(true);
        }

        // Kanvas layar seperti di tutorial (Canvas + teks, huruf 36); jangkar kiri bawah supaya posisi = titik layar rumah.
        // Satu perbaikan teknis yang diizinkan R1: skala mengikuti layar dari acuan 1280×720 supaya tidak mengecil di HP.
        private static GameObject CreateSuccessMessage()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var message = new GameObject("SuccessMsg", typeof(RectTransform), typeof(Text), typeof(Outline));
            message.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)message.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(326f, 43f);
            var text = message.GetComponent<Text>();
            text.text = "PACKAGE DELIVERED";
            text.font = Resources.Load<Font>("Classic/Bangers-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            message.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return message;
        }
    }
}
