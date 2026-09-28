using System;
using UnityEngine;

[Serializable]
public sealed class TownLayoutData
{
    public string format;
    public int version;
    public string id;
    public string name;
    public string layoutHash;
    public TownExtent extent;
    public float roadWidth;
    public TownField field;
    public TownAnchors anchors;
    public TownRoad[] roads;
    public TownHouse[] houses;
    public TownActivityStop[] activityStops;
    public TownProp[] props;
    public TownLake[] lakes;
    public TownMarking[] markings;
    public TownCollider[] colliders;

    public static TownLayoutData Parse(TextAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        TownLayoutData data = JsonUtility.FromJson<TownLayoutData>(asset.text);
        if (data == null || data.format != "delivery-dash-town" || data.version != 1)
            throw new InvalidOperationException($"Data kota tidak dikenali: {asset.name}");
        if (data.roads == null || data.roads.Length == 0 || data.houses == null || data.houses.Length == 0)
            throw new InvalidOperationException($"Data kota tidak lengkap: {asset.name}");
        return data;
    }
}

[Serializable] public struct TownPoint { public float x; public float y; public Vector2 Vector => new Vector2(x, y); }
[Serializable] public struct TownExtent { public float xMin, yMin, xMax, yMax; public Rect Rect => Rect.MinMaxRect(xMin, yMin, xMax, yMax); }
[Serializable] public struct TownBox { public TownPoint center, size; }
[Serializable] public struct TownStart { public TownPoint pos; public float heading; }
[Serializable] public struct TownFinish { public TownPoint center, size; public float angle; }
[Serializable] public struct TownPlaza { public TownPoint center, size; public float radius; }
[Serializable] public struct TownDriveway { public TownPoint from, to; public float width; public string road; }

[Serializable]
public sealed class TownAnchors
{
    public TownPoint pickup;
    public TownStart start;
    public TownFinish finish;
    public TownBox warehouse;
    public TownPoint letterDrop;
    public TownPoint exit;
    public TownPlaza plaza;
    public TownDriveway driveway;
}

[Serializable] public sealed class TownRoad { public string id, name, roadClass, from, to; public float length; public TownPoint[] points; }
[Serializable] public sealed class TownHouse
{
    public string id, name, district, type, road;
    public string colorway;
    public TownPoint center, door, stop;
    public float angle, weight, route;
    public bool tutorial;
}
[Serializable] public sealed class TownActivityStop { public TownPoint pos; public string road; public float angle; }
[Serializable] public sealed class TownProp { public string kind; public TownPoint pos; public float height; public bool flip; }
[Serializable] public sealed class TownLake { public string id, name; public TownPoint[] shore; }
[Serializable] public struct TownField { public float x0, y0, cell; public int nx, ny; }
[Serializable] public struct TownMarking { public float x0, y0, x1, y1; }
[Serializable] public struct TownCollider { public float x, y, width, height; public Rect Rect => new Rect(x, y, width, height); }
