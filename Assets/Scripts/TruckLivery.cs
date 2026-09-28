using System.Collections.Generic;
using UnityEngine;

public class TruckLivery : MonoBehaviour
{
    public struct Mark { public Rect Rect; public Color Color; public Mark(float x,float y,float w,float h,Color c){Rect=new Rect(x,y,w,h);Color=c;} }
    private Sprite square;
    private readonly List<GameObject> pieces=new List<GameObject>();
    public static IEnumerable<Mark> Marks(int index)
    {
        Color ink=new Color32(47,65,73,255),light=new Color32(255,244,210,255);
        if(index==1){yield return new Mark(-0.55f,-0.5f,1.1f,0.16f,light);yield return new Mark(-0.08f,-0.65f,0.16f,0.5f,light);}
        if(index==2 || index==7)for(int i=0;i<2;i++)yield return new Mark(-0.35f+i*0.5f,-0.85f,0.16f,1.15f,index==2?light:ink);
        if(index==3)for(int i=0;i<3;i++){float x=-0.32f+i*0.32f;yield return new Mark(x,-0.55f+(i%2)*0.3f,0.18f,0.18f,light);}
        if(index==4 || index==5)for(int i=0;i<3;i++)yield return new Mark(-0.55f,-0.75f+i*0.35f,1.1f,0.12f,index==4?ink:light);
        if(index==6)for(int i=0;i<5;i++)yield return new Mark(-0.4f+(i%3)*0.3f,-0.65f+(i/3)*0.4f,0.09f,0.16f,ink);
        if(index==8)for(int i=0;i<3;i++)yield return new Mark(-0.5f+i*0.22f,-0.75f+i*0.3f,0.6f,0.14f,light);
        if(index==9){yield return new Mark(-0.4f,-0.7f,0.12f,0.4f,light);yield return new Mark(-0.54f,-0.56f,0.4f,0.12f,light);yield return new Mark(0.25f,-0.1f,0.18f,0.18f,light);}
    }
    public void Apply(int index)
    {
        foreach(var piece in pieces){piece.SetActive(false);Destroy(piece);}pieces.Clear();
        if(square==null)square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*0.5f,1);
        foreach(var mark in Marks(index))
        {
            var go=new GameObject("Corak");go.transform.SetParent(transform,false);
            go.transform.localPosition=mark.Rect.center;go.transform.localScale=new Vector3(mark.Rect.width,mark.Rect.height,1);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=square;sr.color=mark.Color;sr.sortingOrder=19;pieces.Add(go);
        }
    }
    private void OnDestroy(){if(square!=null)Destroy(square);}
}
