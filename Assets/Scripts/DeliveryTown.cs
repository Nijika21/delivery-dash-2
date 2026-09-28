using System;
using System.Collections.Generic;
using UnityEngine;

// A single shared pavement plan drives the rendering, collision and navigation.
public partial class DeliveryTown : MonoBehaviour
{
    public const int CurrentLayoutVersion = 7;
    public const int BarrierLayer = 2; // Built-in Ignore Raycast layer; explicitly included by the car.
    public List<Rect> Pavement = new List<Rect>();
    public List<Address> Houses = new List<Address>();
    public List<Vector2> RoadPoints = new List<Vector2>();
    public List<RoadPath> CurvedRoads = new List<RoadPath>();
    [SerializeField] private Rect townExtent = new Rect(-80f, -24f, 160f, 166f);
    [SerializeField] private string mapId = "legacy";
    [SerializeField] private float roadHalfWidth = 3f;
    public Rect Extent => townExtent;
    public string MapId => mapId;
    public int LayoutVersion;
    public const int CurrentCollisionVersion = 3;
    public int CollisionVersion;
    public int BarrierCount => GetComponentsInChildren<BoxCollider2D>().Length;
    private Rect warehouse = new Rect(-11f, -15f, 10f, 5f);
    public Rect WarehouseArea => warehouse;
    private SdfField pavementField;
    private readonly List<Vector2> activityStops = new List<Vector2>();
    private readonly List<string> activityStreetNames = new List<string>();
    private readonly List<Rect> streets = new List<Rect>();
    [SerializeField] private Sprite square;
    [SerializeField] private Sprite disk;
    [SerializeField] private Material flatMaterial;
    [SerializeField] private Font font;
    [SerializeField] private DeliveryContentCatalog catalog;
    private bool ownsVisualAssets;
    private Transform detailRoot;
    private readonly Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private Vector2 destination;

    [Serializable]
    public struct Address
    {
        public string Id;
        public string Name;
        public Vector2 Stop;
        public Vector2 Door;
        public int RoadIndex;
        public float Weight;
        public Vector2 DepotExit;
        public bool IsTutorial;
    }

    [Serializable]
    public class RoadPath
    {
        public Vector2[] Points;
        public Rect Bounds;
        public string Name;
    }

    // Archived grid maps have pavement and addresses, but no curved-road records.
    public int ActivityStopCount => activityStops.Count > 0 ? activityStops.Count : (CurvedRoads.Count > 0 ? CurvedRoads.Count : Houses.Count);

    public bool TryActivityStop(int index, out Vector2 position, out string streetName)
    {
        position = default;
        streetName = string.Empty;
        if (index < 0 || index >= ActivityStopCount) return false;
        if (activityStops.Count > 0)
        {
            position = activityStops[index];
            streetName = activityStreetNames[index];
        }
        else if (CurvedRoads.Count > 0)
        {
            RoadPath road = CurvedRoads[index];
            if (road == null || road.Points == null || road.Points.Length < 2) return false;
            position = AlongRoad(road, 0.5f, out _);
            streetName = road.Name;
        }
        else
        {
            position = Houses[index].Stop;
            streetName = Houses[index].Name;
        }
        return IsSafe(position, 1.5f);
    }

    public void Build(DeliveryContentCatalog content)
    {
        ownsVisualAssets = true;
        catalog = content;
        square = MakeSquare();
        disk = MakeDisk();
        flatMaterial = new Material(content != null && content.flatShader != null ? content.flatShader : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildPlan();
        DrawTown();
        BuildBarriers();
        Physics2D.SyncTransforms();
    }

    public void Build(DeliveryContentCatalog content, TextAsset layoutAsset)
    {
        if (layoutAsset == null) { Build(content); return; }
        ownsVisualAssets = true;
        catalog = content;
        square = MakeSquare();
        disk = MakeDisk();
        flatMaterial = new Material(content != null && content.flatShader != null ? content.flatShader : Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildV7(TownLayoutData.Parse(layoutAsset));
        Physics2D.SyncTransforms();
    }

    private void BuildLegacyPlan()
    {
        // Four districts, each with its own loop, plus a landscaped central square.
        Horizontal(-40, 40, 10);
        Horizontal(-40, 40, 28);
        Horizontal(-40, -10, 46);
        Horizontal(10, 40, 46);
        var wave = new List<Vector2>();
        for (int x = -40; x <= 40; x += 4) wave.Add(new Vector2(x, NorthRoadY(x)));
        AddCurve(wave.ToArray(), false);
        Horizontal(-40, 40, 82);
        foreach (float x in new[] { -40f, -20f, 20f, 40f }) Vertical(x, 10, 82);
        Vertical(0, -2, 36);
        Vertical(0, 56, 82);
        var ring = new List<Vector2>();
        for (int i = 0; i <= 48; i++)
        {
            float angle = i * Mathf.PI * 2f / 48;
            ring.Add(new Vector2(Mathf.Cos(angle) * 10, 46 + Mathf.Sin(angle) * 10));
        }
        AddCurve(ring.ToArray(), false);
        AddCurve(new[] { new Vector2(-40,28), new Vector2(-53,29), new Vector2(-62,40),
            new Vector2(-60,58), new Vector2(-51,69), new Vector2(-40,64) }, true);
        AddCurve(new[] { new Vector2(40,28), new Vector2(53,29), new Vector2(61,41),
            new Vector2(58,57), new Vector2(49,66), new Vector2(40,64) }, true);
        AddCurve(new[] { new Vector2(-40,82), new Vector2(-48,94), new Vector2(-34,103),
            new Vector2(-13,102), new Vector2(8,97), new Vector2(29,103),
            new Vector2(49,94), new Vector2(40,82) }, true);
        Pavement.AddRange(streets);
        Pavement.Add(new Rect(-12, -16, 24, 16)); // Roomy sorting depot forecourt.

        foreach (Rect road in streets)
        {
            bool horizontal = road.width >= road.height;
            float length = horizontal ? road.width : road.height;
            for (float t = 3f; t < length - 2f; t += 5f)
            {
                Vector2 p = horizontal ? new Vector2(road.xMin + t, road.center.y) : new Vector2(road.center.x, road.yMin + t);
                if (!RoadPoints.Exists(v => Vector2.Distance(v, p) < 3f)) RoadPoints.Add(p);
            }
        }
        foreach (RoadPath path in CurvedRoads)
        for (int i = 0; i < path.Points.Length; i += 3)
            if (!RoadPoints.Exists(v => Vector2.Distance(v, path.Points[i]) < 3f)) RoadPoints.Add(path.Points[i]);
    }

    private static float NorthRoadY(float x) => 64 + 4 * Mathf.Sin((x + 40) * Mathf.PI / 40);

    private void AddCurve(Vector2[] anchors, bool smooth)
    {
        var points = new List<Vector2>();
        if (!smooth) points.AddRange(anchors);
        else
        {
            for (int i = 0; i < anchors.Length - 1; i++)
            {
                Vector2 a = anchors[Mathf.Max(0, i - 1)], b = anchors[i];
                Vector2 c = anchors[i + 1], d = anchors[Mathf.Min(anchors.Length - 1, i + 2)];
                for (int step = 0; step < 8; step++)
                {
                    float t = step / 8f;
                    points.Add(0.5f * ((2*b) + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t));
                }
            }
            points.Add(anchors[anchors.Length - 1]);
        }
        Vector2 min = points[0], max = points[0];
        foreach (Vector2 point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        CurvedRoads.Add(new RoadPath { Points = points.ToArray(), Bounds = Rect.MinMaxRect(min.x-3,min.y-3,max.x+3,max.y+3) });
    }

    private void Horizontal(float left, float right, float y) => streets.Add(new Rect(left - 3f, y - 3f, right - left + 6f, 6f));
    private void Vertical(float x, float bottom, float top) => streets.Add(new Rect(x - 3f, bottom - 3f, 6f, top - bottom + 6f));

    public bool IsPaved(Vector2 p)
    {
        if (!Extent.Contains(p) || warehouse.Contains(p)) return false;
        if (pavementField != null) return pavementField.Sample(p) < 0f;
        foreach (Rect road in Pavement) if (road.Contains(p)) return true;
        foreach (RoadPath path in CurvedRoads)
        {
            if (!path.Bounds.Contains(p)) continue;
            for (int i = 1; i < path.Points.Length; i++)
            {
                Vector2 a = path.Points[i-1], delta = path.Points[i]-a;
                float t = Mathf.Clamp01(Vector2.Dot(p-a,delta) / Mathf.Max(delta.sqrMagnitude,0.0001f));
                if ((p-a-delta*t).sqrMagnitude < roadHalfWidth * roadHalfWidth) return true;
            }
        }
        return false;
    }

    // Jarak bertanda ke tepi aspal (negatif = di atas aspal). Peta lama tanpa medan SDF: -1 atau +1 dari IsPaved.
    public float PaveDistance(Vector2 p)
    {
        if (pavementField != null && Extent.Contains(p) && !warehouse.Contains(p)) return pavementField.Sample(p);
        return IsPaved(p) ? -1f : 1f;
    }

    public bool IsSafe(Vector2 p, float radius = 1.3f)
    {
        if (!IsPaved(p)) return false;
        foreach (Vector2Int direction in Directions)
            if (!IsPaved(p + (Vector2)direction * radius)) return false;
        return true;
    }

    private void DrawTown()
    {
        RectShape("Padang rumput", Extent, new Color32(112, 150, 78, 255), -30);
        Color32[] lawns = { new Color32(140,164,99,255), new Color32(121,158,95,255),
            new Color32(133,163,94,255), new Color32(119,155,108,255) };
        Disc("Lembah kebun",new Vector2(-43,91),47,lawns[0],-29);
        Disc("Kampung telaga",new Vector2(42,72),48,lawns[3],-29);
        Disc("Bukit utara",new Vector2(20,111),49,lawns[2],-29);

        // All outline layers precede ALL asphalt layers, leaving intersections fully open.
        foreach (Rect road in Pavement)
        {
            Rect curb = road; curb.min -= Vector2.one * 0.45f; curb.max += Vector2.one * 0.45f;
            RectShape("Trotoar", curb, new Color32(152,153,146,255), -24);
            Rect edge = road; edge.min -= Vector2.one * 0.10f; edge.max += Vector2.one * 0.10f;
            RectShape("Garis tepi", edge, new Color32(240,235,218,255), -23);
        }
        foreach (Rect road in Pavement) RectShape("Aspal", road, new Color32(40,42,40,255), -22);
        foreach (RoadPath path in CurvedRoads)
        {
            DrawCurve(path, 6.9f, new Color32(152,153,146,255), -24);
            DrawCurve(path, 6.2f, new Color32(240,235,218,255), -23);
            DrawCurve(path, 6f, new Color32(40,42,40,255), -22);
        }
        foreach (Vector2 p in RoadPoints)
        {
            int crossings = 0;
            foreach (Rect road in streets) if (road.Contains(p)) crossings++;
            if (crossings == 0) continue;
            if (crossings > 1) continue;
            bool alongX = false;
            foreach (Rect road in streets) if (road.Contains(p)) { alongX = road.width >= road.height; break; }
            RectShape("Marka", new Rect(p - (alongX ? new Vector2(0.7f,0.055f) : new Vector2(0.055f,0.7f)),
                alongX ? new Vector2(1.4f,0.11f) : new Vector2(0.11f,1.4f)), new Color32(168,169,152,255), -20);
        }

        BuildDepot();
        BuildOrganicHomes();
        BuildOrganicLandmarks();
        PlantOrganicDetails();
        // Perimeter trees visually close the town; collision exists along every pavement edge.
        for (int x = -66; x <= 66; x += 6)
        {
            if (!IsPaved(new Vector2(x, 138))) Prop(new Vector2(x, 138), 0, 2.7f);
            if (Mathf.Abs(x) > 15) Prop(new Vector2(x, 1), 1, 2.6f);
        }
        for (int y = 10; y < 90; y += 7)
        {
            Prop(new Vector2(-77,y), y % 2, 2.8f);
            Prop(new Vector2(77,y), y % 2, 2.8f);
        }
    }

    private void DrawCurve(RoadPath path, float width, Color color, int order)
    {
        for (int i = 0; i < path.Points.Length; i++)
        {
            Disc("Lengkung jalan", path.Points[i], width, color, order);
            if (i == 0) continue;
            Vector2 delta = path.Points[i] - path.Points[i-1];
            var strip = Shape("Sambungan jalan", square, (path.Points[i]+path.Points[i-1])*0.5f,
                new Vector2(delta.magnitude,width), color, order);
            strip.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        if (order != -22) return;
        for (int i = 1; i < path.Points.Length-1; i += 3)
        {
            Vector2 p = path.Points[i];
            if (streets.Exists(road => road.Contains(p))) continue;
            Vector2 delta = path.Points[i+1]-path.Points[i-1];
            var mark = Shape("Marka lengkung",square,p,new Vector2(1.15f,0.11f),new Color32(168,169,152,255),-20);
            mark.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
    }

    private void BuildLandmarks()
    {
        Disc("Tepi telaga",new Vector2(49,46),13,new Color32(204,195,168,255),-27);
        Disc("Telaga timur",new Vector2(49,46),11,new Color32(86,154,162,255),-26);
        RectShape("Dermaga",new Rect(43,44.5f,5,1.4f),new Color32(154,105,65,255),-4);
        for (int i=0;i<5;i++)
            RectShape("Papan dermaga",new Rect(43+i,44.5f,0.06f,1.4f),new Color32(96,74,51,255),-3);
        foreach (float x in new[] { -55f,-49f })
        foreach (float y in new[] { 40f,47f,54f }) Prop(new Vector2(x,y),1,3.1f);
        Label("KEBUN BUAH",new Vector2(-52,47),0.42f,Color.white);
        Label("TELAGA",new Vector2(49,46),0.42f,Color.white);
    }

    private void BuildDepot()
    {
        Color32 roof = new Color32(170,88,55,255);
        RectShape("Bayangan gudang", new Rect(-11.2f,-15.3f,10.4f,5.6f), new Color32(68,76,54,255), -6);
        RectShape("Gudang sortir", warehouse, new Color32(222,208,165,255), -5);
        RectShape("Atap gudang", new Rect(-11,-15,10,3.5f), roof, -4);
        for (float x = -10.5f; x < -1f; x += 1f)
            RectShape("Garis atap", new Rect(x,-14.8f,0.07f,3.1f), new Color32(137,64,45,255), -3);
        for (int i=0;i<3;i++)
        {
            RectShape("Pintu sortir", new Rect(-10+i*3,-11.4f,2,1.4f), new Color32(52,69,65,255), -3);
            RectShape("Ambang pintu", new Rect(-10+i*3,-10.15f,2,0.15f), new Color32(244,202,75,255), -2);
        }
        Label("GUDANG PAKET", new Vector2(-6,-12.9f), 0.46f, Color.white);
        Label("AMBIL DI SINI", Depot + Vector2.up*1.7f, 0.34f, new Color32(247,213,101,255));
        EnsureDepotParking();
        Label("HALAMAN SORTIR", new Vector2(5,-3),0.4f,new Color32(230,224,200,255));
        // A few original package sprites beside the loading doors.
        if(catalog.packageSprite!=null)
            for(int i=0;i<3;i++) Asset("Tumpukan paket",catalog.packageSprite,new Vector2(-9+i*1.3f,-8.9f),0.75f);
    }

    public void EnsureDepotParking()
    {
        if (transform.Find("Parkir selesai kerja") != null) return;
        // Keep the previous markings recoverable, but no longer visible.
        foreach (Transform child in transform)
            if (child.name == "Batas parkir") child.gameObject.SetActive(false);
        Transform previous = detailRoot;
        detailRoot = new GameObject("Parkir selesai kerja").transform;
        detailRoot.SetParent(transform, false);
        foreach (Rect bay in SplitFinishParkingBays())
        {
            RectShape("Area parkir", bay, new Color(0.24f,0.66f,0.95f,0.05f), -20);
            for (int layer=0; layer<5; layer++)
            {
                float inset=layer*0.08f;
                Rect r = Rect.MinMaxRect(bay.xMin+inset,bay.yMin+inset,bay.xMax-inset,bay.yMax-inset);
                Color color=new Color(0.24f,0.66f,0.95f,layer==0?0.85f:0.16f-layer*0.025f);
                RectShape("Garis parkir",new Rect(r.xMin,r.yMin,r.width,0.08f),color,-19);
                RectShape("Garis parkir",new Rect(r.xMin,r.yMax-0.08f,r.width,0.08f),color,-19);
                RectShape("Garis parkir",new Rect(r.xMin,r.yMin,0.08f,r.height),color,-19);
                RectShape("Garis parkir",new Rect(r.xMax-0.08f,r.yMin,0.08f,r.height),color,-19);
            }
            Label("SELESAI KERJA",bay.center,0.25f,new Color32(172,217,246,255));
        }
        detailRoot=previous;
    }

    public void AddRoadsideDetails()
    {
        if (transform.Find("Detail tepi jalan") != null) return;
        detailRoot = new GameObject("Detail tepi jalan").transform;
        detailRoot.SetParent(transform, false);
        // Planting strips and seating fill the road edges without obstructing turns.
        foreach (float x in new[] { -35f,-25f,-15f,15f,25f,35f })
        {
            RectShape("Kebun tepi jalan", new Rect(x-2.5f,3.5f,5,2), new Color32(149,170,108,255), -26);
            Prop(new Vector2(x-1.4f,4.5f), 1, 2.2f);
            Prop(new Vector2(x+1.5f,4.3f), 2, 0.9f);
            RectShape("Bangku", new Rect(x-0.2f,4,1.3f,0.4f), new Color32(154,105,65,255), -3);
        }
        foreach (float x in new[] { -44.5f,44.5f })
        foreach (float y in new[] { 19f,37f,55f,73f })
        {
            Prop(new Vector2(x,y),3,2f);
            Prop(new Vector2(x,y+3),2,1.1f);
        }
        foreach (float x in new[] { -24.5f,-15.5f,15.5f,24.5f })
        foreach (float y in new[] { 23f,59f,77f })
            Prop(new Vector2(x,y-1),1,2.5f);
        foreach (float x in new[] { -14f,14f })
        foreach (float y in new[] { -13f,-7f,-1f })
        {
            Prop(new Vector2(x,y),1,2.6f);
            Prop(new Vector2(x,y+2),2,1f);
        }
        foreach (float x in new[] { -4.5f,4.5f })
        {
            Prop(new Vector2(x,3),3,2f);
            Prop(new Vector2(x,0),2,1f);
        }
        detailRoot = null;
    }

    private void BuildHomes()
    {
        float[] xs = { -33,-27,-13,-7,7,13,27,33 };
        float[] ys = { 10,28,46,64,82 };
        string[] districts = { "Melati", "Kenanga", "Mawar", "Anggrek" };
        int index = 0;
        foreach(float row in ys)
        foreach(float x in xs)
        {
            float y = row == 64 ? NorthRoadY(x) : row;
            if (Mathf.Abs(x)<17 && y>=28 && y<=46) continue; // Reserve central garden.
            int district = (x<0?0:1)+(y>=46?2:0);
            Vector2 door = new Vector2(x,y+5.1f);
            Vector2 stop = new Vector2(x,y+1.4f);
            // Front sprites stay upright; their southern doors align with the northern curb.
            RectShape("Jalan setapak",new Rect(x-0.55f,y+3f,1.1f,3f),new Color32(207,193,155,255),-19);
            RectShape("Pekarangan",new Rect(x-2.4f,y+4.2f,4.8f,6.2f),new Color32(151,172,108,255),-26);
            if(catalog.houseSprites!=null && catalog.houseSprites.Length>0)
                Asset("Rumah "+districts[district],catalog.houseSprites[index%catalog.houseSprites.Length],new Vector2(x,y+7.4f),4.4f);
            string address = districts[district]+" "+(index+1);
            Label((index+1).ToString(),new Vector2(x+1.4f,y+4.2f),0.28f,new Color32(45,56,43,255));
            RectShape("Kotak surat",new Rect(x+1.5f,y+3.9f,0.4f,0.5f),new Color32(175,75,51,255),-4);
            Prop(new Vector2(x-1.9f,y+9f),2,0.75f);
            // Small, consistent roadside details stay on the non-drivable side of the curb.
            RectShape("Tiang lampu",new Rect(x-2.2f,y+3.5f,0.12f,1.4f),new Color32(72,79,66,255),-2);
            Disc("Lampu jalan",new Vector2(x-2.14f,y+4.9f),0.38f,new Color32(243,222,154,255),-1);
            RectShape("Pagar kebun",new Rect(x-2.5f,y+9.8f,5f,0.12f),new Color32(213,202,166,255),-4);
            for(int flower=0;flower<4;flower++)
            {
                Color petal=district%2==0?new Color32(214,155,122,255):new Color32(223,206,122,255);
                Disc("Bunga",new Vector2(x+1.7f,y+6f+flower*0.4f),0.25f,petal,-2);
            }
            Houses.Add(new Address {Name=address,Stop=stop,Door=door});
            index++;
        }
    }

    private void BuildPark()
    {
        Disc("Taman tengah",new Vector2(0,46),13,new Color32(153,177,116,255),-18);
        Disc("Pelataran air mancur",new Vector2(0,46),9.5f,new Color32(204,195,168,255),-16);
        Disc("Bibir kolam",new Vector2(0,46),7f,new Color32(104,116,105,255),-15);
        Disc("Air kolam",new Vector2(0,46),5.9f,new Color32(86,154,162,255),-14);
        Disc("Riak",new Vector2(0,46),3.4f,new Color32(139,192,192,255),-13);
        Disc("Pilar air mancur",new Vector2(0,46),1.6f,new Color32(223,216,183,255),-12);
        Disc("Puncak air",new Vector2(0,46),0.7f,new Color32(204,232,224,255),-11);
        foreach(Vector2 p in new[]{new Vector2(-4,42),new Vector2(4,42),new Vector2(-4,50),new Vector2(4,50)})
            Prop(p,2,1.3f);
        Label("TAMAN KOTA",new Vector2(0,50.8f),0.42f,new Color32(45,66,44,255));
        foreach(float x in new[]{-5.5f,5.5f})
        {
            RectShape("Bangku taman",new Rect(x-0.35f,44.8f,0.7f,2.4f),new Color32(154,105,65,255),-4);
            RectShape("Sandaran bangku",new Rect(x-0.5f,44.8f,0.13f,2.4f),new Color32(96,74,51,255),-3);
        }
    }

    public Transform BuildBarriers()
    {
        // Quarter-unit cells follow the INSIDE of the white asphalt edge. Keep the
        // conservative whole-cell test: widening by accepting cell centres opens
        // grass notches and lets the truck enter scenery at tight intersections.
        // Bake once in the editor; the mobile player only loads merged rectangles.
        const float cell = 0.25f;
        int w = Mathf.RoundToInt(Extent.width / cell);
        int h = Mathf.RoundToInt(Extent.height / cell);
        bool[,] blocked = new bool[w,h];
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            Vector2 min = new Vector2(Extent.x+x*cell,Extent.y+y*cell);
            blocked[x,y] = !CellInsideRoad(new Rect(min,Vector2.one*cell));
        }
        Transform parent = new GameObject("Tabrakan trotoar dan bangunan").transform;
        parent.SetParent(transform,false);
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            if(!blocked[x,y]) continue;
            int rw=1; while(x+rw<w && blocked[x+rw,y]) rw++;
            int rh=1;
            while(y+rh<h)
            {
                bool full=true;
                for(int dx=0;dx<rw;dx++) if(!blocked[x+dx,y+rh]) {full=false;break;}
                if(!full) break;
                rh++;
            }
            for(int dy=0;dy<rh;dy++) for(int dx=0;dx<rw;dx++) blocked[x+dx,y+dy]=false;
            GameObject wall=new GameObject("Trotoar padat");
            wall.layer=BarrierLayer;
            wall.transform.SetParent(parent,false);
            wall.transform.position=new Vector3(Extent.x+(x+rw*0.5f)*cell,Extent.y+(y+rh*0.5f)*cell,0);
            wall.AddComponent<BoxCollider2D>().size=new Vector2(rw,rh)*cell;
        }
        CollisionVersion = CurrentCollisionVersion;
        return parent;
    }

    private bool CellInsideRoad(Rect cell)
    {
        // All corners must belong to ONE convex road segment. Testing the union at
        // four corners alone misses narrow grass notches between intersecting bends.
        if(warehouse.Overlaps(cell))return false;
        Vector2[] corners={cell.min,new Vector2(cell.xMax,cell.y),cell.max,new Vector2(cell.x,cell.yMax)};
        foreach(Rect road in Pavement)
            if(road.xMin<=cell.xMin && road.xMax>=cell.xMax && road.yMin<=cell.yMin && road.yMax>=cell.yMax)return true;
        foreach(var road in CurvedRoads)
        {
            if(!road.Bounds.Overlaps(cell))continue;
            for(int i=1;i<road.Points.Length;i++)
            {
                Vector2 a=road.Points[i-1],d=road.Points[i]-a;
                bool inside=true;
                foreach(var p in corners)
                {
                    float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(d.sqrMagnitude,0.0001f));
                    if((p-a-d*t).sqrMagnitude>=roadHalfWidth*roadHalfWidth){inside=false;break;}
                }
                if(inside)return true;
            }
        }
        return false;
    }

    public void SetDestination(Vector2 point)
    {
        destination=point;
        distances.Clear();
        Vector2Int start=Vector2Int.RoundToInt(point);
        Queue<Vector2Int> pending=new Queue<Vector2Int>();
        distances[start]=0; pending.Enqueue(start);
        while(pending.Count>0)
        {
            Vector2Int p=pending.Dequeue();
            foreach(Vector2Int direction in Directions)
            {
                Vector2Int next=p+direction;
                if(distances.ContainsKey(next)||!IsSafe(next)) continue;
                distances[next]=distances[p]+1;
                pending.Enqueue(next);
            }
        }
        BuildRouteCosts(start);
    }

    // Uji HP 28 Sep ("titik rute belok-belok, tidak di tengah"): rute peta dulu menuruni jarak BFS 4 arah, jadi
    // jalurnya menempel ke sisi jalan mana saja dan berzig-zag. Biaya rute kini Dijkstra dengan langkah yang makin mahal
    // mendekati tepi aspal, sehingga rute mengikuti tengah jalan. NextWaypoint tetap memakai jarak BFS.
    private readonly Dictionary<Vector2Int, int> routeCosts = new Dictionary<Vector2Int, int>();

    private void BuildRouteCosts(Vector2Int start)
    {
        routeCosts.Clear();
        var open = new SortedSet<(int cost, int x, int y)>();
        routeCosts[start] = 0;
        open.Add((0, start.x, start.y));
        while (open.Count > 0)
        {
            var head = open.Min;
            open.Remove(head);
            var p = new Vector2Int(head.x, head.y);
            if (routeCosts[p] < head.cost) continue;
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = p + direction;
                if (!distances.ContainsKey(next)) continue;
                int cost = head.cost + StepCost(next);
                if (routeCosts.TryGetValue(next, out int known) && known <= cost) continue;
                routeCosts[next] = cost;
                open.Add((cost, next.x, next.y));
            }
        }
    }

    // Tengah jalan (≥ 3 unit dari tepi) berbiaya 10 per sel; makin dekat tepi makin mahal sampai 40.
    private int StepCost(Vector2Int cell)
    {
        float depth = pavementField != null ? -PaveDistance(cell) : ProbeDepth(cell);
        return 10 + Mathf.RoundToInt(Mathf.Clamp(3f - depth, 0f, 3f) * 10f);
    }

    // Peta tanpa medan jarak: jarak ke tepi diperkirakan dengan memeriksa 4 arah sampai 4 unit.
    private float ProbeDepth(Vector2 p)
    {
        for (int r = 1; r <= 4; r++)
            foreach (Vector2Int direction in Directions)
                if (!IsPaved(p + (Vector2)direction * r)) return r - 0.5f;
        return 4f;
    }

    public Vector2 NextWaypoint(Vector2 position)
    {
        Vector2Int nearest=Vector2Int.RoundToInt(position);
        if(!distances.ContainsKey(nearest))
        {
            float best=float.MaxValue;
            foreach(var pair in distances)
            {
                float d=((Vector2)pair.Key-position).sqrMagnitude;
                if(d<best) {best=d;nearest=pair.Key;}
            }
        }
        if(!distances.TryGetValue(nearest,out int remaining)) return position;
        if(remaining<3) return destination;
        Vector2Int at=nearest;
        for(int step=0;step<4;step++)
        {
            Vector2Int next=at;
            foreach(Vector2Int dir in Directions)
                if(distances.TryGetValue(at+dir,out int distance)&&distance<remaining)
                {remaining=distance;next=at+dir;}
            at=next;
        }
        return at;
    }

    public bool IsReachable(Vector2 point) => distances.ContainsKey(Vector2Int.RoundToInt(point));

    public void TraceRoute(Vector2 from, Vector2 target, List<Vector2> route)
    {
        if ((destination-target).sqrMagnitude > 0.01f || distances.Count == 0) SetDestination(target);
        route.Clear();
        Vector2Int at = Vector2Int.RoundToInt(from);
        if (!distances.ContainsKey(at))
        {
            float best = float.MaxValue;
            foreach (var pair in distances)
            {
                float distance = ((Vector2)pair.Key-from).sqrMagnitude;
                if (distance < best) { best=distance; at=pair.Key; }
            }
        }
        if (!routeCosts.TryGetValue(at, out int remaining)) return;
        route.Add(at);
        while (remaining > 0)
        {
            Vector2Int best = at;
            foreach (Vector2Int direction in Directions)
                if (routeCosts.TryGetValue(at+direction, out int next) && next < remaining)
                { best = at+direction; remaining = next; }
            if (best == at) break;
            at = best;
            route.Add(at);
        }
        SmoothRoute(route);
        route.Add(target);
    }

    // Tangga sel 4 arah dirata-rata (jendela 5 sel) supaya jalan miring/lengkung jadi garis halus; ujung tetap.
    private readonly List<Vector2> smoothBuffer = new List<Vector2>();
    private void SmoothRoute(List<Vector2> route)
    {
        const int reach = 2;
        if (route.Count <= reach * 2) return;
        smoothBuffer.Clear();
        for (int i = 0; i < route.Count; i++)
        {
            int k = Mathf.Min(reach, Mathf.Min(i, route.Count - 1 - i));
            Vector2 sum = Vector2.zero;
            for (int j = i - k; j <= i + k; j++) sum += route[j];
            smoothBuffer.Add(sum / (k * 2 + 1));
        }
        route.Clear();
        route.AddRange(smoothBuffer);
    }

    private void Prop(Vector2 position,int index,float height)
    {
        if(catalog.propSprites==null||catalog.propSprites.Length==0)return;
        Asset("Tanaman",catalog.propSprites[index%catalog.propSprites.Length],position,height);
    }

    public SpriteRenderer Asset(string name,Sprite sprite,Vector2 position,float height)
    {
        if(sprite==null)return null;
        GameObject go=new GameObject(name);go.transform.SetParent(detailRoot != null ? detailRoot : transform,false);
        SpriteRenderer sr=go.AddComponent<SpriteRenderer>();
        sr.sprite=sprite;sr.material=flatMaterial;sr.sortingOrder=-3;
        float scale=height/sprite.bounds.size.y;
        go.transform.localScale=Vector3.one*scale;
        // Correct custom sprite pivots: place visible bounds, not import pivot, at the lot center.
        go.transform.position=(Vector3)position-sprite.bounds.center*scale;
        return sr;
    }

    public SpriteRenderer RectShape(string name,Rect rect,Color color,int order)
    {
        SpriteRenderer sr=Shape(name,square,rect.center,rect.size,color,order);
        return sr;
    }

    public SpriteRenderer Disc(string name,Vector2 position,float diameter,Color color,int order)
        => Shape(name,disk,position,Vector2.one*diameter,color,order);

    private SpriteRenderer Shape(string name,Sprite sprite,Vector2 position,Vector2 scale,Color color,int order)
    {
        GameObject go=new GameObject(name);go.transform.SetParent(detailRoot != null ? detailRoot : transform,false);
        go.transform.position=position;go.transform.localScale=new Vector3(scale.x,scale.y,1);
        SpriteRenderer sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.color=color;
        sr.material=flatMaterial;sr.sortingOrder=order;return sr;
    }

    public void Label(string text,Vector2 position,float size,Color color)
    {
        GameObject go=new GameObject(text);go.transform.SetParent(transform,false);go.transform.position=position;
        TextMesh label=go.AddComponent<TextMesh>();label.text=text;label.font=font;label.fontSize=64;
        label.characterSize=size*0.1f;label.anchor=TextAnchor.MiddleCenter;label.color=color;
        MeshRenderer renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;renderer.sortingOrder=4;
    }

    private static Sprite MakeDisk()
    {
        const int n=64;Texture2D tex=new Texture2D(n,n,TextureFormat.RGBA32,false);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float d=Vector2.Distance(new Vector2(x+0.5f,y+0.5f),Vector2.one*n/2);
            tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(n/2f-d)));
        }
        tex.Apply();return Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*0.5f,n);
    }

    private static Sprite MakeSquare()
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
    }

    private void OnDestroy()
    {
        if (!ownsVisualAssets || !Application.isPlaying) return;
        ReleaseVisualAssets();
    }

    // Aset visual buatan Build (material, tekstur, mesh). Saat Play lewat OnDestroy; pratinjau kota di editor
    // (TownEditorPreview) memanggilnya sendiri sebelum objeknya dihapus, supaya tidak menumpuk di memori editor.
    public void ReleaseVisualAssets()
    {
        if (!ownsVisualAssets) return;
        ownsVisualAssets = false;
        ReleaseWorldV7();
        ReleaseAsset(flatMaterial);
        if (disk != null) { ReleaseAsset(disk.texture); ReleaseAsset(disk); }
        if (square != null) { ReleaseAsset(square.texture); ReleaseAsset(square); }
    }

    private static void ReleaseAsset(UnityEngine.Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }

#if UNITY_EDITOR
    public void SaveVisualAssets(string folder)
    {
        UnityEditor.AssetDatabase.CreateAsset(square.texture, folder + "/SquareTexture.asset");
        UnityEditor.AssetDatabase.CreateAsset(square, folder + "/Square.asset");
        UnityEditor.AssetDatabase.CreateAsset(disk.texture, folder + "/CircleTexture.asset");
        UnityEditor.AssetDatabase.CreateAsset(disk, folder + "/Circle.asset");
        UnityEditor.AssetDatabase.CreateAsset(flatMaterial, folder + "/TownMaterial.mat");
        int generated=0;
        foreach(var renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            Sprite sprite=renderer.sprite;
            if(sprite==null || UnityEditor.AssetDatabase.Contains(sprite))continue;
            if(!UnityEditor.AssetDatabase.Contains(sprite.texture))
                UnityEditor.AssetDatabase.CreateAsset(sprite.texture,folder+"/DetailTexture"+generated+".asset");
            UnityEditor.AssetDatabase.CreateAsset(sprite,folder+"/DetailSprite"+generated+".asset");
            generated++;
        }
        ownsVisualAssets = false;
    }

    public void RepairSavedSquare(string folder)
    {
        Sprite previous = square;
        square = MakeSquare();
        UnityEditor.AssetDatabase.CreateAsset(square.texture, folder + "/RoadTexture.asset");
        UnityEditor.AssetDatabase.CreateAsset(square, folder + "/RoadSprite.asset");
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.sprite == previous) renderer.sprite = square;
    }
#endif
}
