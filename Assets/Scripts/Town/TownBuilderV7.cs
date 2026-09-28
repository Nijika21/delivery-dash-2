using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DeliveryTown
{
    private void BuildV7(TownLayoutData data)
    {
        if (data.anchors == null) throw new InvalidOperationException("Anchor kota v7 tidak tersedia.");
        LayoutVersion = CurrentLayoutVersion;
        mapId = data.id;
        townExtent = data.extent.Rect;
        roadHalfWidth = data.roadWidth * 0.5f;
        Pavement.Clear();
        Houses.Clear();
        RoadPoints.Clear();
        CurvedRoads.Clear();
        activityStops.Clear();
        activityStreetNames.Clear();

        warehouse = RectFromCenter(data.anchors.warehouse.center.Vector, data.anchors.warehouse.size.Vector);
        warehouseRect = warehouse;
        finishCenter = data.anchors.finish.center.Vector;
        finishSize = data.anchors.finish.size.Vector;
        finishAngle = data.anchors.finish.angle;
        Rect finish = RotatedBounds(finishCenter, finishSize, finishAngle);
        Rect plaza = RectFromCenter(data.anchors.plaza.center.Vector, data.anchors.plaza.size.Vector);
        Pavement.Add(plaza);
        // Jalan masuk gudang (lebar 5) menyambung pelataran ke jalan; tanpa ini grid rute memutus pelataran.
        Vector2 driveFrom = data.anchors.driveway.from.Vector, driveTo = data.anchors.driveway.to.Vector;
        if (data.anchors.driveway.width > 0f && (driveTo - driveFrom).sqrMagnitude > 0.01f)
        {
            Vector2 min = Vector2.Min(driveFrom, driveTo), max = Vector2.Max(driveFrom, driveTo);
            float half = data.anchors.driveway.width * 0.5f;
            bool vertical = Mathf.Abs(driveTo.x - driveFrom.x) < Mathf.Abs(driveTo.y - driveFrom.y);
            Vector2 pad = vertical ? new Vector2(half, 0f) : new Vector2(0f, half);
            Pavement.Add(Rect.MinMaxRect(min.x - pad.x, min.y - pad.y, max.x + pad.x, max.y + pad.y));
        }

        int tutorialIndex = -1;
        for (int i = 0; i < data.roads.Length; i++)
        {
            TownRoad source = data.roads[i];
            var points = new Vector2[source.points.Length];
            Vector2 min = source.points[0].Vector;
            Vector2 max = min;
            for (int p = 0; p < points.Length; p++)
            {
                points[p] = source.points[p].Vector;
                min = Vector2.Min(min, points[p]);
                max = Vector2.Max(max, points[p]);
                if (p % 3 == 0) RoadPoints.Add(points[p]);
            }
            CurvedRoads.Add(new RoadPath
            {
                Name = source.name,
                Points = points,
                Bounds = Rect.MinMaxRect(min.x - roadHalfWidth, min.y - roadHalfWidth, max.x + roadHalfWidth, max.y + roadHalfWidth)
            });
        }

        var roadIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < data.roads.Length; i++) roadIndices[data.roads[i].id] = i;
        houseCenters.Clear();
        houseAngles.Clear();
        for (int i = 0; i < data.houses.Length; i++)
        {
            TownHouse source = data.houses[i];
            if (source.tutorial) tutorialIndex = i;
            houseCenters.Add(source.center.Vector);
            houseAngles.Add(source.angle);
            Houses.Add(new Address
            {
                Id = source.id,
                Name = source.name,
                Stop = source.stop.Vector,
                Door = source.door.Vector,
                RoadIndex = roadIndices.TryGetValue(source.road, out int road) ? road : 0,
                Weight = Mathf.Max(0.00001f, source.weight),
                DepotExit = data.anchors.exit.Vector,
                IsTutorial = source.tutorial
            });
        }
        foreach (TownActivityStop stop in data.activityStops ?? Array.Empty<TownActivityStop>())
        {
            activityStops.Add(stop.pos.Vector);
            activityStreetNames.Add(roadIndices.TryGetValue(stop.road, out int road) ? data.roads[road].name : "Kota");
        }

        // Baker memakai sudut matematika (90 = utara); rotasi Unity truk 0 = menghadap utara.
        ConfigureAnchors(data.anchors.pickup.Vector, data.anchors.start.pos.Vector, data.anchors.start.heading - 90f,
            data.anchors.letterDrop.Vector, finish, data.anchors.plaza.center.Vector,
            data.anchors.driveway.to.Vector, tutorialIndex);
        plazaSize = data.anchors.plaza.size.Vector;

        // Tabrakan v7 dibake dari SDF aspal (dinding di pita pinggir jalan, gedung gudang padat; lihat field.mjs wallGrid); rute dan uji jalan membaca SDF yang sama.
        TextAsset paveField = data.id == "blok-paket" ? catalog.blokPaketPavementSdf : catalog.kotaPaketPavementSdf;
        pavementField = paveField != null && data.field.nx > 1 && paveField.bytes.Length == data.field.nx * data.field.ny * 2
            ? new SdfField(paveField.bytes, data.field.nx, data.field.ny, new Vector2(data.field.x0, data.field.y0), data.field.cell)
            : null;
        DrawWorldV7(data);
        BuildV7Colliders(data.colliders);
    }

    private void BuildV7Colliders(TownCollider[] colliders)
    {
        Transform parent = new GameObject("Tabrakan kota v7").transform;
        parent.SetParent(transform, false);
        foreach (TownCollider source in colliders ?? Array.Empty<TownCollider>())
        {
            Rect rect = source.Rect;
            GameObject wall = new GameObject("Trotoar padat v3");
            wall.layer = BarrierLayer;
            wall.transform.SetParent(parent, false);
            wall.transform.position = rect.center;
            wall.AddComponent<BoxCollider2D>().size = rect.size;
        }
        CollisionVersion = CurrentCollisionVersion;
    }

    private static Rect RectFromCenter(Vector2 center, Vector2 size) => new Rect(center - size * 0.5f, size);

    private static Rect RotatedBounds(Vector2 center, Vector2 size, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float width = Mathf.Abs(Mathf.Cos(radians)) * size.x + Mathf.Abs(Mathf.Sin(radians)) * size.y;
        float height = Mathf.Abs(Mathf.Sin(radians)) * size.x + Mathf.Abs(Mathf.Cos(radians)) * size.y;
        return RectFromCenter(center, new Vector2(width, height));
    }

    private static Rect Expanded(Rect rect, float amount)
    {
        rect.min -= Vector2.one * amount;
        rect.max += Vector2.one * amount;
        return rect;
    }
}
