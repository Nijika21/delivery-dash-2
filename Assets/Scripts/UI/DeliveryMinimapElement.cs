using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeliveryDash.UI
{
    // Lapisan peta (hud.json → minimap / petaBesar, build-gate4.mjs miniMap/bigMap):
    // - kecil: gambar peta 3,2 px/unit digeser supaya truk selalu di tengah jendela; titik rute tiap 12 px (tikungan dihaluskan);
    //   penanda berjari-jari 15 ditahan 22 px dari tepi; panah truk skala 1,1.
    // - besar: seluruh peta dimuat ke kotak peta; titik rute tiap 11 px; penanda berjari-jari 17; panah truk skala 1,3.
    // Penanda: gudang paket selalu, tujuan aktif, dan bantuan warga yang masih ditawarkan; peta besar juga parkir selesai.
    public sealed class DeliveryMinimapElement : VisualElement
    {
        public const float SmallPixelsPerUnit = 3.2f;
        private const float EdgePad = 22f;
        private static readonly Color RouteFill = new Color32(0x0A, 0x8D, 0xC2, 255);
        private static readonly Color TruckFill = new Color32(0x1D, 0xB4, 0xE8, 255);

        private readonly DeliveryGameManager game;
        private readonly VisualElement mapImage;
        private readonly bool big;
        private readonly List<Vector2> route = new List<Vector2>();
        // Rute dihitung ulang 10×/dtk (Dijkstra), posisi peta dan panah tiap bingkai supaya tidak tersendat.
        private float nextRouteAt;
        private bool placed;
        // Ukuran jendela terakhir per orientasi (kecil/besar). HUD dikloning ulang setiap layar berganti (ambil/antar
        // paket, ekspres, singgah); dengan ukuran ini peta langsung ditaruh di posisi benar pada bingkai pertama,
        // bukan menunggu tata letak (tanpa ini minimap sempat kosong/melompat = "hilang-hilangan").
        private static readonly Vector2[] knownSize = new Vector2[4];
        private int SizeSlot => (big ? 2 : 0) + (UiRoot.PortraitScreen ? 1 : 0);
        private Vector2 Size => float.IsNaN(layout.width) || layout.width <= 0f ? knownSize[SizeSlot] : layout.size;
        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<VisualElement> markers = new List<VisualElement>();
        private readonly List<(string kind, Vector2 world)> wanted = new List<(string, Vector2)>();
        // Panah truk digambar paling atas (desain: truckMark sesudah penanda).
        private readonly VisualElement truckLayer = new VisualElement { name = "peta-truk", pickingMode = PickingMode.Ignore };

        public DeliveryMinimapElement(DeliveryGameManager gameManager, VisualElement image, bool bigMap)
        {
            game = gameManager;
            mapImage = image;
            big = bigMap;
            name = "peta-lapisan";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            generateVisualContent += DrawRoute;
            truckLayer.style.position = Position.Absolute;
            truckLayer.style.left = 0;
            truckLayer.style.top = 0;
            truckLayer.style.right = 0;
            truckLayer.style.bottom = 0;
            truckLayer.generateVisualContent += DrawTruck;
            Add(truckLayer);
            schedule.Execute(Refresh).Every(0);
            Refresh();
            // Belum pernah ada ukuran (layar HUD pertama): gambar disembunyikan sampai posisi pertama dihitung.
            if (!big && mapImage != null && !placed) mapImage.style.visibility = Visibility.Hidden;
            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        private float Radius => big ? 17f : 15f;
        private float DotStep => big ? 11f : 12f;
        private float DotRadius => big ? 3.2f : 3.4f;
        private float TruckScale => big ? 1.3f : 1.1f;

        private void Refresh()
        {
            if (!float.IsNaN(layout.width) && layout.width > 0f) knownSize[SizeSlot] = layout.size;
            Vector2 size = Size;
            if (game == null || game.Town == null || game.Car == null || size.x <= 0f) return;
            Rect extent = game.Town.Extent;
            if (!big && mapImage != null)
            {
                // Gambar peta = seluruh extent pada 3,2 px/unit; truk di tengah jendela.
                Vector2 truck = game.Car.transform.position;
                mapImage.style.width = extent.width * SmallPixelsPerUnit;
                mapImage.style.height = extent.height * SmallPixelsPerUnit;
                mapImage.style.left = size.x * 0.5f - (truck.x - extent.xMin) * SmallPixelsPerUnit;
                mapImage.style.top = size.y * 0.5f - (extent.yMax - truck.y) * SmallPixelsPerUnit;
                if (!placed) { placed = true; mapImage.style.visibility = StyleKeyword.Null; }
            }
            if (Time.unscaledTime >= nextRouteAt)
            {
                nextRouteAt = Time.unscaledTime + 0.1f;
                route.Clear();
                game.Town.TraceRoute(game.Car.transform.position, game.NavigationTarget, route);
            }
            UpdateMarkers();
            truckLayer.BringToFront();
            MarkDirtyRepaint();
            truckLayer.MarkDirtyRepaint();
        }

        private Vector2 ToLocal(Vector2 world)
        {
            Rect extent = game.Town.Extent;
            if (big)
            {
                Vector2 size = Size;
                float scale = Mathf.Min(size.x / extent.width, size.y / extent.height);
                Vector2 offset = new Vector2(size.x - extent.width * scale, size.y - extent.height * scale) * 0.5f;
                return offset + new Vector2((world.x - extent.xMin) * scale, (extent.yMax - world.y) * scale);
            }
            Vector2 truck = game.Car.transform.position;
            Vector2 window = Size;
            return new Vector2(window.x * 0.5f + (world.x - truck.x) * SmallPixelsPerUnit,
                window.y * 0.5f - (world.y - truck.y) * SmallPixelsPerUnit);
        }

        private void UpdateMarkers()
        {
            wanted.Clear();
            wanted.Add(("paket", game.Town.Depot));
            if (big) wanted.Add(("selesai", game.Town.FinishCenter));
            // Zona yang sedang aktif di dunia (desain: zones minus paket), urutan = urutan gambar.
            if (game.Carrying || game.SpecialTarget.HasValue)
                wanted.Add(("rumah", game.SpecialTarget ?? game.TargetPosition));
            if (game.Activities != null && game.Activities.HasStopover)
                wanted.Add(("singgah", game.Activities.BonusPosition));
            if (game.SideQuests != null)
            {
                if (game.SideQuests.CarriedLetters > 0) wanted.Add(("surat", game.SideQuests.DropoffPosition));
                foreach (DeliverySideQuests.Quest quest in game.SideQuests.Quests)
                    if (!quest.Completed && !quest.Collected) wanted.Add(("bantuan", quest.Position));
            }
            if (!big && game.FinishRequested && !game.HasPendingDelivery) wanted.Add(("selesai", game.Town.FinishCenter));

            while (markers.Count < wanted.Count)
            {
                var marker = new VisualElement { pickingMode = PickingMode.Ignore };
                marker.style.position = Position.Absolute;
                marker.AddToClassList("sc-bg");
                Add(marker);
                markers.Add(marker);
            }
            // Gambar penanda 38 px = radius 17 + tepi 2 (aset.json); minimap kecil memakai radius 15.
            float size = 38f * Radius / 17f;
            for (int i = 0; i < markers.Count; i++)
            {
                VisualElement marker = markers[i];
                if (i >= wanted.Count) { marker.style.display = DisplayStyle.None; continue; }
                (string kind, Vector2 world) = wanted[i];
                Vector2 at = ToLocal(world);
                if (!big)
                {
                    Vector2 window = Size;
                    at.x = Mathf.Clamp(at.x, EdgePad, window.x - EdgePad);
                    at.y = Mathf.Clamp(at.y, EdgePad, window.y - EdgePad);
                }
                marker.style.display = DisplayStyle.Flex;
                string className = "peta-" + kind;
                if (!marker.ClassListContains(className))
                {
                    foreach (string other in new[] { "paket", "rumah", "bantuan", "surat", "singgah", "selesai" })
                        marker.RemoveFromClassList("peta-" + other);
                    marker.AddToClassList(className);
                }
                marker.style.width = size;
                marker.style.height = size;
                marker.style.left = at.x - size * 0.5f;
                marker.style.top = at.y - size * 0.5f;
            }
        }

        private void DrawRoute(MeshGenerationContext context)
        {
            if (game == null || game.Town == null || game.Car == null) return;
            Painter2D painter = context.painter2D;

            // Titik rute: sisa rute dari truk ke tujuan, berjarak tetap dalam piksel layar. Jarak dihitung dari
            // TUJUAN ke belakang, jadi titik diam di peta saat truk berjalan (dihitung dari truk, semua titik ikut
            // bergeser tiap bingkai dan tampak berdenyut).
            points.Clear();
            points.Add(ToLocal(game.Car.transform.position));
            foreach (Vector2 point in route) points.Add(ToLocal(point));
            // Uji HP 28 Sep ("titik rute kurang rapi"): rute sudah mengikuti tengah jalan (DeliveryTown.TraceRoute);
            // di sini tikungan dihaluskan sekali (Chaikin) dan titik sedikit lebih besar dengan tepi putih lebih tegas.
            Smooth(points);
            float step = DotStep, carry = step * 0.5f, dot = DotRadius;
            painter.fillColor = RouteFill;
            painter.strokeColor = Color.white;
            painter.lineWidth = 1.6f;
            for (int i = points.Count - 1; i > 0; i--)
            {
                Vector2 a = points[i], b = points[i - 1];
                float length = Vector2.Distance(a, b);
                while (carry <= length)
                {
                    Vector2 at = Vector2.Lerp(a, b, carry / length);
                    // Titik yang sudah menempel panah truk tidak digambar (tertutup panah dan terlihat berkedip).
                    if (i > 1 || Vector2.Distance(at, points[0]) > step)
                    {
                        painter.BeginPath();
                        painter.Arc(at, dot, 0f, 360f);
                        painter.Fill();
                        painter.Stroke();
                    }
                    carry += step;
                }
                carry -= length;
            }
        }

        // Chaikin: tiap ruas diganti titik 1/4 dan 3/4; ujung (truk dan tujuan) tetap.
        private readonly List<Vector2> smoothed = new List<Vector2>();
        private void Smooth(List<Vector2> path)
        {
            if (path.Count < 3) return;
            smoothed.Clear();
            smoothed.Add(path[0]);
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                if (i > 0) smoothed.Add(Vector2.Lerp(a, b, 0.25f));
                if (i < path.Count - 2) smoothed.Add(Vector2.Lerp(a, b, 0.75f));
            }
            smoothed.Add(path[path.Count - 1]);
            path.Clear();
            path.AddRange(smoothed);
        }

        private void DrawTruck(MeshGenerationContext context)
        {
            if (game == null || game.Town == null || game.Car == null) return;
            Painter2D painter = context.painter2D;
            // Panah truk: M11 0 L-8 -8 L-4 0 L-8 8 Z, menghadap arah truk.
            Vector2 center = ToLocal(game.Car.transform.position);
            float heading = (game.Car.transform.eulerAngles.z + 90f) * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(heading), -Mathf.Sin(heading)) * TruckScale;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 P(float x, float y) => center + forward * x + side * y;
            painter.fillColor = TruckFill;
            painter.strokeColor = Color.white;
            painter.lineWidth = 2.6f * TruckScale;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            painter.MoveTo(P(11f, 0f));
            painter.LineTo(P(-8f, -8f));
            painter.LineTo(P(-4f, 0f));
            painter.LineTo(P(-8f, 8f));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }
    }
}
