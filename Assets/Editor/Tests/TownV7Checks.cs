using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeliveryDash.Editor.Tests
{
    public static class TownV7Checks
    {
        private static readonly string[] LayoutPaths =
        {
            "Assets/World/kota-paket/town.unity.json",
            "Assets/World/blok-paket/town.unity.json"
        };

        [MenuItem("Delivery Dash/Periksa Data Kota v7")]
        public static void ValidateAll()
        {
            int checks = 0;
            foreach (string path in LayoutPaths) checks += ValidateLayout(path);
            string scenePath = File.Exists("Assets/Scenes/KotaPaket.unity")
                ? "Assets/Scenes/KotaPaket.unity" : "Assets/Scenes/SampleScene.unity";
            if (new FileInfo(scenePath).Length >= 50L * 1024L * 1024L)
                throw new InvalidOperationException("Scene utama melebihi 50 MB: " + scenePath);
            Debug.Log($"TOWN_V7_CHECKS_OK: {checks} pemeriksaan untuk {LayoutPaths.Length} peta.");
        }

        public static void ValidateRuntime(DeliveryTown town)
        {
            if (town == null || town.LayoutVersion != DeliveryTown.CurrentLayoutVersion)
                throw new InvalidOperationException("Runtime tidak memakai tata kota v7.");
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(
                town.MapId == "blok-paket" ? LayoutPaths[1] : LayoutPaths[0]);
            TownLayoutData data = TownLayoutData.Parse(source);
            if (town.Houses.Count != data.houses.Length) throw new InvalidOperationException("Jumlah rumah runtime tidak cocok dengan bake.");
            if (town.BarrierCount != data.colliders.Length) throw new InvalidOperationException("Jumlah collider runtime tidak cocok dengan bake.");
            if (Vector2.Distance(town.Depot, data.anchors.pickup.Vector) > 0.01f)
                throw new InvalidOperationException("Anchor depot runtime tidak cocok dengan bake.");
        }

        private static int ValidateLayout(string path)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) throw new FileNotFoundException("Data kota tidak ditemukan.", path);
            TownLayoutData data = TownLayoutData.Parse(asset);
            int checks = 0;
            Require(data.houses.Length >= 64 && data.houses.Length <= 80, data.id + ": jumlah rumah 64–80"); checks++;
            Require(data.colliders != null && data.colliders.Length > 0, data.id + ": collider tersedia"); checks++;
            Require(data.activityStops != null && data.activityStops.Length > 0, data.id + ": titik kegiatan tersedia"); checks++;
            Require(data.anchors != null, data.id + ": anchor tersedia"); checks++;

            var nodes = new HashSet<string>(StringComparer.Ordinal);
            var links = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var roadIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TownRoad road in data.roads)
            {
                Require(road.points != null && road.points.Length >= 2, data.id + ": jalan punya geometri");
                Require(roadIds.Add(road.id), data.id + ": ID jalan unik " + road.id);
                nodes.Add(road.from); nodes.Add(road.to);
                AddLink(links, road.from, road.to); AddLink(links, road.to, road.from);
                checks += 2;
            }
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            foreach (string first in nodes) { reached.Add(first); queue.Enqueue(first); break; }
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!links.TryGetValue(current, out List<string> next)) continue;
                foreach (string node in next) if (reached.Add(node)) queue.Enqueue(node);
            }
            Require(reached.Count == nodes.Count, data.id + ": graf jalan terhubung"); checks++;

            var districtCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int tutorials = 0;
            foreach (TownHouse house in data.houses)
            {
                Require(roadIds.Contains(house.road), data.id + ": rumah merujuk jalan valid");
                Require(house.weight > 0f, data.id + ": bobot rumah positif");
                Require(DistanceToRoads(house.stop.Vector, data.roads) <= data.roadWidth * 0.75f,
                    data.id + ": titik berhenti rumah berada di jalan");
                if (house.tutorial) tutorials++;
                string district = house.district ?? string.Empty;
                districtCounts[district] = districtCounts.TryGetValue(district, out int count) ? count + 1 : 1;
                checks += 3;
            }
            Require(tutorials == 1, data.id + ": tepat satu rumah tutorial"); checks++;
            int min = int.MaxValue, max = 0;
            foreach (int count in districtCounts.Values) { min = Mathf.Min(min, count); max = Mathf.Max(max, count); }
            Require(districtCounts.Count > 1 && max - min <= Mathf.Max(4, data.houses.Length / 5),
                data.id + ": kawasan seimbang"); checks++;

            Rect plaza = RectFromCenter(data.anchors.plaza.center.Vector, data.anchors.plaza.size.Vector);
            Require(plaza.Contains(data.anchors.pickup.Vector), data.id + ": pickup berada di pelataran depot"); checks++;
            Require(plaza.Contains(data.anchors.start.pos.Vector), data.id + ": posisi mulai berada di pelataran depot"); checks++;
            Require(data.anchors.finish.size.Vector.x > 0f && data.anchors.finish.size.Vector.y > 0f,
                data.id + ": area selesai valid"); checks++;

            return checks;
        }

        private static void AddLink(Dictionary<string, List<string>> links, string from, string to)
        {
            if (!links.TryGetValue(from, out List<string> list)) links[from] = list = new List<string>();
            list.Add(to);
        }

        private static float DistanceToRoads(Vector2 point, TownRoad[] roads)
        {
            float best = float.PositiveInfinity;
            foreach (TownRoad road in roads)
            for (int i = 1; i < road.points.Length; i++)
                best = Mathf.Min(best, DistanceToSegment(point, road.points[i - 1].Vector, road.points[i].Vector));
            return best;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float denominator = delta.sqrMagnitude;
            if (denominator < 0.00001f) return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / denominator);
            return Vector2.Distance(point, a + delta * t);
        }

        private static Rect RectFromCenter(Vector2 center, Vector2 size) => new Rect(center - size * 0.5f, size);

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("TOWN_V7_FAIL: " + description);
        }
    }
}
