using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DeliveryTown
{
    private readonly List<Vector2> occupiedLots = new List<Vector2>();

    public int TutorialHouseIndex()
    {
        if (tutorialHouseIndex >= 0 && tutorialHouseIndex < Houses.Count) return tutorialHouseIndex;
        for (int i = 0; i < Houses.Count; i++)
            if (Houses[i].IsTutorial) return i;
        // Identify the actual authored blue shell house, not a shuffled list index.
        SpriteRenderer left = null;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
            if (renderer.name == "Rumah Pasar Sore" && renderer.sprite != null
                && renderer.sprite.name.StartsWith("house4purple", StringComparison.Ordinal)
                && (left == null || renderer.bounds.center.x < left.bounds.center.x)) left = renderer;
        if (left == null) return -1;
        int closest = -1;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < Houses.Count; i++)
        {
            if (!Houses[i].Name.StartsWith("Pasar Sore ", StringComparison.Ordinal)) continue;
            float candidate = Vector2.Distance(Houses[i].Door, left.bounds.center);
            if (candidate < distance) { distance = candidate; closest = i; }
        }
        return closest;
    }

    private void BuildPlan()
    {
        LayoutVersion = CurrentLayoutVersion;
        // Each edge joins two junctions. The depot has two exits; no ornamental outer loop.
        Road("Gang Sortir", -8,-1, -13,9, -23,18);
        Road("Jalan Pos", 8,-1, 16,7, 24,16);
        Road("Pasar Sore", -23,18, -6,22, 11,25, 24,16);
        Road("Kampung Mangga", -23,18, -39,23, -49,31, -46,41);
        Road("Gang Melati", -23,18, -25,34, -20,45, -11,55);
        Road("Jalan Bengkel", 24,16, 40,21, 44,30, 35,40);
        Road("Kampung Warna", 24,16, 18,30, 6,33, 0,44);
        Road("Kebun Barat", -46,41, -40,53, -26,59, -11,55);
        Road("Tepi Telaga", 35,40, 34,53, 23,61, 11,55);
        Road("Tanjakan Jambu", -46,41, -55,56, -49,68, -38,73);
        Road("Kampung Bambu", -38,73, -25,80, -13,85, 0,80);
        Road("Jalan Sekolah", 0,80, 5,74, 0,66);
        Road("Lengkung Kenanga", 0,80, 18,77, 30,86, 42,78);
        Road("Kampung Nelayan", 42,78, 52,63, 44,51, 35,40);
        Road("Kebun Jeruk", -38,73, -54,76, -65,83, -62,92);
        Road("Jalan Perbukitan", -62,92, -57,108, -34,116, -14,106, 4,111);
        Road("Kampung Layang", 4,111, 24,115, 43,105, 39,93, 42,78);
        Road("Jalan Tambak", 42,78, 61,83, 70,93, 64,101);
        Road("Punggung Bukit", 64,101, 53,120, 29,129, 12,123, 4,111);
        var ring = new List<Vector2>();
        for (int i=0;i<=48;i++)
        {
            float a=i*Mathf.PI*2/48;
            ring.Add(new Vector2(Mathf.Cos(a)*11,55+Mathf.Sin(a)*11));
        }
        AddCurve(ring.ToArray(),false);
        CurvedRoads[CurvedRoads.Count-1].Name="Alun-alun";
        Pavement.Add(new Rect(-12,-16,24,16));
        foreach (var road in CurvedRoads)
            for(int i=0;i<road.Points.Length;i+=3)
                if(!RoadPoints.Exists(p=>Vector2.Distance(p,road.Points[i])<3)) RoadPoints.Add(road.Points[i]);
    }

    private void Road(string name, params float[] xy)
    {
        var anchors=new Vector2[xy.Length/2];
        for(int i=0;i<anchors.Length;i++)anchors[i]=new Vector2(xy[i*2],xy[i*2+1]);
        AddCurve(anchors,true);
        CurvedRoads[CurvedRoads.Count-1].Name=name;
    }

    public static Vector2 AlongRoad(RoadPath road,float fraction,out Vector2 tangent)
    {
        float length=0;
        for(int i=1;i<road.Points.Length;i++)length+=Vector2.Distance(road.Points[i-1],road.Points[i]);
        float remaining=Mathf.Clamp01(fraction)*length;
        for(int i=1;i<road.Points.Length;i++)
        {
            Vector2 delta=road.Points[i]-road.Points[i-1];
            if(remaining<=delta.magnitude || i==road.Points.Length-1)
            {
                tangent=delta.normalized;
                return road.Points[i-1]+tangent*Mathf.Min(remaining,delta.magnitude);
            }
            remaining-=delta.magnitude;
        }
        tangent=Vector2.up;
        return road.Points[0];
    }

    private bool LotClear(Vector2 center,float radius)
    {
        if(!Extent.Contains(center+Vector2.one*radius) || !Extent.Contains(center-Vector2.one*radius))return false;
        if(occupiedLots.Exists(p=>Vector2.Distance(p,center)<radius+3.6f))return false;
        foreach(var lake in new[]{new Vector2(44,64),new Vector2(43,70),new Vector2(38,68)})
            if(Vector2.Distance(center,lake)<radius+5)return false;
        // Keep every corner and edge off asphalt, including neighbouring roads at junctions.
        for(int i=0;i<16;i++)
        {
            float a=i*Mathf.PI/8;
            if(IsPaved(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius))return false;
        }
        return !IsPaved(center);
    }

    private void BuildOrganicHomes()
    {
        var rng=new System.Random(709);
        for(int edge=0;edge<CurvedRoads.Count;edge++)
        {
            RoadPath road=CurvedRoads[edge];
            int count=0;
            int wanted=edge<2?1:edge==CurvedRoads.Count-1?3:3;
            for(int attempt=0;attempt<100 && count<wanted;attempt++)
            {
                // Seeded, irregular lots; stable between editor and device, not a row of pairs.
                float t=0.16f+(float)rng.NextDouble()*0.68f;
                Vector2 street=AlongRoad(road,t,out Vector2 tangent);
                Vector2 normal=new Vector2(-tangent.y,tangent.x)*(rng.Next(2)==0?-1:1);
                float setback=6.8f+(float)rng.NextDouble()*2.3f;
                Vector2 center=street+normal*setback;
                if(!LotClear(center,3.5f) || !IsSafe(street,1.55f))continue;
                // Keep the fountain's interior open. Houses around it belong outside the ring.
                if(Vector2.Distance(center,new Vector2(0,55))<19)continue;
                occupiedLots.Add(center);
                float angle=Mathf.Atan2(normal.y,normal.x)*Mathf.Rad2Deg-90;
                Quaternion turn=Quaternion.Euler(0,0,angle);
                float height=4.1f+(float)rng.NextDouble()*0.8f;
                Vector2 door=center-normal*(height*0.43f);
                var path=Shape("Akses pintu",square,(street+door)*0.5f,
                    new Vector2(1.05f,Vector2.Distance(street,door)),new Color32(196,183,148,255),-25);
                path.transform.rotation=turn;
                var yard=Shape("Pekarangan",square,center,new Vector2(6.2f,6.8f),
                    new Color32((byte)rng.Next(128,157),(byte)rng.Next(158,180),104,255),-27);
                yard.transform.rotation=turn;
                Sprite sprite=catalog.houseSprites[rng.Next(catalog.houseSprites.Length)];
                var house=Asset("Rumah "+road.Name,sprite,center,height);
                // Rotate around the visible centre, preserving imported custom pivots.
                house.transform.rotation=turn;
                house.transform.position=(Vector3)center-turn*Vector3.Scale(sprite.bounds.center,house.transform.localScale);
                Vector2 mailbox=door+(Vector2)(turn*Vector2.right)*2.1f;
                RectShape("Kotak surat",new Rect(mailbox-Vector2.one*0.22f,Vector2.one*0.44f),new Color32(175,75,51,255),-2);
                Prop(center+normal*2.4f+(Vector2)(turn*Vector2.right)*2.5f,rng.Next(3),1.2f);
                Houses.Add(new Address { Name=road.Name+" "+(count+1),Stop=street,Door=door,RoadIndex=edge });
                count++;
            }
            if(count==0)throw new InvalidOperationException("Jalan tanpa alamat: "+road.Name);
        }
    }

    private void BuildOrganicLandmarks()
    {
        Vector2 fountain=new Vector2(0,55);
        Disc("Taman alun-alun",fountain,14,new Color32(153,177,116,255),-18);
        Disc("Bibir air mancur",fountain,8,new Color32(204,195,168,255),-16);
        Disc("Air mancur",fountain,6.5f,new Color32(86,154,162,255),-15);
        Disc("Riak air",fountain,3.2f,new Color32(139,192,192,255),-14);
        Disc("Pilar air",fountain,1.2f,new Color32(223,216,183,255),-13);
        BuildNaturalLake();
        // Small market stalls, checked against both roads and house lots.
        for(int i=0;i<7;i++)
        {
            Vector2 at=new Vector2(-7+i*3.7f,12+(i%3)*1.1f);
            if(!LotClear(at,2))continue;
            occupiedLots.Add(at);
            RectShape("Meja pasar",new Rect(at-new Vector2(1.3f,0.8f),new Vector2(2.6f,1.6f)),new Color32(154,105,65,255),-4);
            for(int stripe=0;stripe<5;stripe++)
                RectShape("Tenda pasar",new Rect(at.x-1.4f+stripe*0.56f,at.y,0.56f,1.3f),
                    stripe%2==0?new Color32(201,102,76,255):new Color32(228,219,182,255),-3);
        }
        for(int i=0;i<18;i++)
        {
            Vector2 at=new Vector2(-49+(i%5)*4.1f,91+(i/5)*4.7f);
            if(LotClear(at,2))Prop(at,1,2.8f);
        }
    }

    private void PlantOrganicDetails()
    {
        var rng=new System.Random(812);
        foreach(var road in CurvedRoads)
        {
            for(int i=1;i<road.Points.Length;i+=2)
            {
                Vector2 delta=(road.Points[i]-road.Points[i-1]).normalized;
                Vector2 side=new Vector2(-delta.y,delta.x)*(rng.Next(2)==0?-1:1);
                Vector2 at=road.Points[i]+side*(4.7f+(float)rng.NextDouble()*3);
                if(!LotClear(at,1.9f) || Vector2.Distance(at,new Vector2(0,55))<7)continue;
                Prop(at,rng.Next(3),1.2f+(float)rng.NextDouble()*1.6f);
            }
        }
    }

    private void BuildNaturalLake()
    {
        // One continuous concave shoreline, softened with corner cutting; not overlapping disks.
        var shore=new List<Vector2> {
            new Vector2(33,68),new Vector2(34,71),new Vector2(37,73),new Vector2(39,72.5f),
            new Vector2(40,71),new Vector2(39.8f,69.5f),new Vector2(42.8f,68),new Vector2(44,64),
            new Vector2(43,61),new Vector2(40,60),new Vector2(38,62),new Vector2(37,65),new Vector2(34,66) };
        for(int pass=0;pass<3;pass++)
        {
            var smooth=new List<Vector2>();
            for(int i=0;i<shore.Count;i++)
            {
                Vector2 a=shore[i],b=shore[(i+1)%shore.Count];
                smooth.Add(Vector2.Lerp(a,b,0.25f));smooth.Add(Vector2.Lerp(a,b,0.75f));
            }
            shore=smooth;
        }
        const int size=256;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        var colors=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            Vector2 p=new Vector2(32+(x+0.5f)*17/size,58+(y+0.5f)*18/size);
            bool inside=false;float edge=float.MaxValue;
            for(int i=0,j=shore.Count-1;i<shore.Count;j=i++)
            {
                Vector2 a=shore[j],b=shore[i],d=b-a;
                edge=Mathf.Min(edge,Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude)));
                if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            colors[y*size+x]=inside?(edge<0.65f?new Color32(113,173,171,255):new Color32(86,154,162,255))
                :edge<0.5f?new Color32(177,179,137,255):Color.clear;
            if(colors[y*size+x].a>0 && IsPaved(p))throw new InvalidOperationException("Tepian danau menyentuh jalan: "+p);
        }
        texture.SetPixels(colors);texture.Apply();
        Sprite lake=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*0.5f,size);
        Shape("Telaga tepian alami",lake,new Vector2(40.5f,67),new Vector2(17,18),Color.white,-26);
        foreach(Vector2 reed in new[]{new Vector2(34.5f,68),new Vector2(39,72.5f),new Vector2(41,61),new Vector2(42.5f,65)})
            for(int i=0;i<3;i++)RectShape("Rumput tepian",new Rect(reed.x+i*0.22f,reed.y,0.12f,0.65f+i*0.15f),new Color32(73,115,73,255),-24);
    }
}
